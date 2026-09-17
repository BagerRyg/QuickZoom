using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;

internal static class StartupReleaseChecks
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;

    internal static void Run(Assembly assembly, string root)
    {
        Type handoff = assembly.GetType("QuickZoom.StartupHandoff", true)!;
        using Process current = Process.GetCurrentProcess();
        bool Marked(string method) => (bool)handoff.GetMethod(method, Static)!.Invoke(null, [current])!;
        IDisposable Mark(string method) => (IDisposable?)handoff.GetMethod(method, Static)!.Invoke(null, null)
            ?? throw new InvalidOperationException("Could not create the startup handoff test signal.");
        Check(!Marked("IsReady"), "a live process alone must not count as ready");
        using (Mark("MarkYielding"))
        {
            Check(Marked("IsYielding"), "a yielding launcher advertises its exact process lifetime");
            Check(!Marked("IsReady"), "yielding must not imply successful initialization");
        }
        Check(!Marked("IsYielding"), "a failed handoff stops yielding after disposal");
        using (Mark("MarkReady"))
            Check(Marked("IsReady"), "initialization readiness can be observed with synchronization-only access");
        Check(!Marked("IsReady"), "shutdown removes readiness");
        Type program = assembly.GetType("QuickZoom.Program", true)!;
        MethodInfo completeYielding = program.GetMethod("CompleteStartupYielding", Static)!;
        FieldInfo retainedYielding = program.GetField("_startupYieldingMarker", Static)!;
        using (IDisposable pendingMarker = Mark("MarkYielding"))
        {
            try
            {
                completeYielding.Invoke(null, [pendingMarker, false, false]);
                Check(Marked("IsYielding") && ReferenceEquals(retainedYielding.GetValue(null), pendingMarker),
                    "a pending child's launcher stays marked as yielding until process exit");
            }
            finally { retainedYielding.SetValue(null, null); }
        }
        Check(!Marked("IsYielding"), "pending marker cleanup removes the signal");
        foreach ((bool ready, bool ownsMutex) in new[] { (true, false), (false, true) })
        {
            using IDisposable completedMarker = Mark("MarkYielding");
            completeYielding.Invoke(null, [completedMarker, ready, ownsMutex]);
            Check(!Marked("IsYielding"), "ready and recovered handoffs release the yielding signal");
        }
        MethodInfo launchResult = program.GetMethod("GetStartupTaskLaunchResult", Static)!;
        Check(launchResult.Invoke(null, [false, false])!.ToString() == "Pending",
            "a readiness timeout without mutex ownership must stop the launcher");
        Check(launchResult.Invoke(null, [false, true])!.ToString() == "Failed",
            "fallback startup is allowed only after mutex ownership is restored");
        Check(launchResult.Invoke(null, [true, false])!.ToString() == "Ready",
            "a ready replacement retains mutex ownership");

        Type service = assembly.GetType("QuickZoom.StartupTaskService", true)!;
        Type infoType = assembly.GetType("QuickZoom.StartupTaskInfo", true)!;
        Type statusType = assembly.GetType("QuickZoom.StartupTaskStatus", true)!;
        MethodInfo forCurrentUser = service.GetMethod("ForCurrentUser", Static)!;
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        object TaskInfo(string sid)
        {
            object info = Activator.CreateInstance(infoType)!;
            infoType.GetProperty("Status")!.SetValue(info, Enum.Parse(statusType, "Ready"));
            infoType.GetProperty("UserId")!.SetValue(info, sid);
            infoType.GetProperty("ExecutePath")!.SetValue(info, Path.Combine(root, "QuickZoom.exe"));
            return info;
        }
        object ownTask = TaskInfo(identity.User!.Value);
        Check(ReferenceEquals(ownTask, forCurrentUser.Invoke(null, [ownTask])), "the current user's verified task remains ready");
        object foreignTask = TaskInfo(identity.User.Value == "S-1-5-18" ? "S-1-5-19" : "S-1-5-18");
        object foreignResult = forCurrentUser.Invoke(null, [foreignTask])!;
        Check(infoType.GetProperty("Status")!.GetValue(foreignResult)!.ToString() == "Broken",
            "another user's logon task must not be reported as configured for this user");

        string versionsRoot = Path.Combine(root, "startup-version-retention");
        DirectoryInfo previous = Directory.CreateDirectory(Path.Combine(versionsRoot, "previous-working"));
        DirectoryInfo failedOne = Directory.CreateDirectory(Path.Combine(versionsRoot, "failed-first-attempt"));
        DirectoryInfo failedTwo = Directory.CreateDirectory(Path.Combine(versionsRoot, "failed-second-attempt"));
        DirectoryInfo installed = Directory.CreateDirectory(Path.Combine(versionsRoot, "verified-update"));
        previous.LastWriteTimeUtc = DateTime.UtcNow.AddHours(-3);
        failedOne.LastWriteTimeUtc = DateTime.UtcNow.AddHours(-2);
        failedTwo.LastWriteTimeUtc = DateTime.UtcNow.AddHours(-1);
        Type installer = assembly.GetType("QuickZoom.InstalledAppService", true)!;
        MethodInfo acquireInstall = installer.GetMethod("TryAcquireInstallTransaction", Static)!;
        string installMutexName = @"Local\QuickZoom.Installation.Test." + Guid.NewGuid().ToString("N");
        object?[] ownerArgs = [null, installMutexName, 50];
        using (var owner = (IDisposable?)acquireInstall.Invoke(null, ownerArgs))
        {
            Check(owner != null, "the first installation acquires the transaction lock");
            bool contenderEntered = false;
            Exception? contenderFailure = null;
            var contenderThread = new Thread(() =>
            {
                try
                {
                    object?[] contenderArgs = [null, installMutexName, 50];
                    using var contender = (IDisposable?)acquireInstall.Invoke(null, contenderArgs);
                    contenderEntered = contender != null;
                    Check(contender != null || !string.IsNullOrWhiteSpace(contenderArgs[0]?.ToString()),
                        "installation contention reports a controlled error");
                }
                catch (Exception ex) { contenderFailure = ex; }
            }) { IsBackground = true };
            contenderThread.Start();
            Check(contenderThread.Join(5000), "installation contention has a bounded deadline");
            if (contenderFailure != null) throw contenderFailure;
            Check(!contenderEntered, "a second installer cannot stage or prune during an active transaction");
        }
        using (var retry = (IDisposable?)acquireInstall.Invoke(null, ownerArgs))
            Check(retry != null, "an installation can retry after the prior transaction releases its lock");
        var obsolete = (IEnumerable<DirectoryInfo>)installer.GetMethod("SelectOldManagedVersions", Static)!.Invoke(null,
            [new[] { previous, failedOne, failedTwo, installed }, installed.FullName, installed.FullName, previous.FullName])!;
        string[] removed = obsolete.Select(directory => directory.Name).ToArray();
        Check(removed.Length == 2 && removed.Contains(failedOne.Name) && removed.Contains(failedTwo.Name),
            "failed attempts must not replace the previous working build in rollback retention");

        Type processOutput = assembly.GetType("QuickZoom.ProcessOutput", true)!;
        MethodInfo wait = processOutput.GetMethod("WaitForExitAsync", Static)!;
        using Process child = Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"),
            Arguments = "-NoProfile -NonInteractive -Command \"Start-Sleep -Seconds 30\"",
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Could not start the bounded-wait test child.");
        try
        {
            var watch = Stopwatch.StartNew();
            var pending = (Task<bool>)wait.Invoke(null, [child, TimeSpan.FromMilliseconds(100)])!;
            Check(!pending.GetAwaiter().GetResult() && watch.Elapsed < TimeSpan.FromSeconds(5),
                "an unresponsive helper returns control within its deadline");
            Check(!child.HasExited, "a deadline must not kill an installer mid-transaction");
            child.Kill(entireProcessTree: true);
            child.WaitForExit(5000);
            Check(((Task<bool>)wait.Invoke(null, [child, TimeSpan.FromSeconds(1)])!).GetAwaiter().GetResult(),
                "an exited helper can be observed on retry");
        }
        finally
        {
            if (!child.HasExited) child.Kill(entireProcessTree: true);
        }
        Console.WriteLine("PASS: startup handoff signals, user ownership, version retention, and bounded helper waits.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Startup release check failed: " + message);
    }
}
