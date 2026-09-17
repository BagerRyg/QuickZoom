using QuickZoom;
using System.Security.Principal;

internal static class PrivacyChecks
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Console.WriteLine("Runtime: .NET " + Environment.Version);
            Run(args);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void Run(string[] args)
    {
        string root = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(root);
        using WindowsIdentity original = WindowsIdentity.GetCurrent();
        bool elevated = new WindowsPrincipal(original).IsInRole(WindowsBuiltInRole.Administrator);
        Console.WriteLine("Elevated test process: " + elevated);
        LocalStorage.RunAsUser(() =>
        {
            using WindowsIdentity effective = WindowsIdentity.GetCurrent();
            Check(effective.User == original.User, "profile writes retain the same user");
            Check(!new WindowsPrincipal(effective).IsInRole(WindowsBuiltInRole.Administrator),
                "profile writes never use administrator privileges");
            if (elevated)
                Check(effective.ImpersonationLevel >= TokenImpersonationLevel.Impersonation,
                    "elevated saves use a file-access-capable impersonation token");
            LocalStorage.RunAsUser(() => FilePersistence.WriteAllTextAtomic(
                Path.Combine(root, "nested-test.json"), "{}"));
        });
        bool propagated = false;
        try { LocalStorage.RunAsUser(() => throw new InvalidOperationException("test callback")); }
        catch (InvalidOperationException) { propagated = true; }
        using (WindowsIdentity restored = WindowsIdentity.GetCurrent())
        {
            Check(propagated && restored.User == original.User &&
                new WindowsPrincipal(restored).IsInRole(WindowsBuiltInRole.Administrator) == elevated &&
                restored.ImpersonationLevel == original.ImpersonationLevel,
                "callback failures propagate and restore the original identity");
        }
        string path = Path.Combine(root, "settings-test.json");
        FilePersistence.WriteAllTextAtomic(path, "{\"StepPercent\":30}");
        FilePersistence.WriteAllTextAtomic(path, "{\"StepPercent\":40}");
        Check(File.ReadAllText(path) == "{\"StepPercent\":40}", "atomic replacement");
        Task.Run(() => FilePersistence.WriteAllTextAtomic(path, "{\"StepPercent\":50}"))
            .GetAwaiter().GetResult();
        Check(File.ReadAllText(path) == "{\"StepPercent\":50}", "background settings save");
        Check(Directory.GetFiles(root, "*.tmp").Length == 0, "temporary file cleanup");
        bool rejected = false;
        try { LocalStorage.RequireLocalPath(@"\\invalid.example\share\test.json"); }
        catch (IOException) { rejected = true; }
        Check(rejected, "UNC rejected before filesystem access");
        using var input = new TestInput { Text = "123", SelectionStart = 0, SelectionLength = 3 };
        foreach (int message in new[] { 0x300, 0x301, 0x302, 0x7B })
        {
            input.Deliver(message);
            Check(input.Text == "123", "clipboard/context message blocked: " + message);
        }
        Check(!input.ShortcutsEnabled && !input.AllowDrop, "clipboard shortcuts and drag/drop disabled");
        ValidateDiagnostics(Path.Combine(root, "diagnostics-" + Guid.NewGuid().ToString("N")));
        Console.WriteLine("All privacy regression checks passed.");
    }

    private static void ValidateDiagnostics(string directory)
    {
        string path = Path.Combine(directory, "test.log");
        ErrorLog.Stop();
        ErrorLog.WriteAlways("test", "private content");
        ErrorLog.WriteCrash("test", new Exception("private content"));
        Check(!Directory.Exists(directory), "logging and crash logging are off by default");
        Check(!ErrorLog.StartSession(true, path, "test") && !Directory.Exists(directory),
            "Strict Data blocks even an explicit logging request without creating files");
        Check(ErrorLog.StartSession(false, path, "test"), "standard mode permits explicit local logging");
        ErrorLog.WriteCrash("SaveSettings", new System.ComponentModel.Win32Exception(5, "SECRET path and document contents"));
        ErrorLog.WriteAlways("diagnostic", "SECRET path and document contents");
        Check(SpinWait.SpinUntil(() => File.ReadAllText(path).Contains("diagnostic"), 3000),
            "background diagnostics are written");
        string content = File.ReadAllText(path);
        Check(content.Contains("Win32Exception") && content.Contains("win32=5") && !content.Contains("SECRET"),
            "useful error codes are retained, but messages and personal content are omitted");
        ErrorLog.Stop();
        content = File.ReadAllText(path);
        ErrorLog.WriteCrash("after-stop", new Exception("ignored"));
        Check(File.ReadAllText(path) == content, "disabling logging also blocks crash writes and retains existing logs");

        using (var full = new FileStream(path, FileMode.Open, FileAccess.Write)) full.SetLength(ErrorLog.MaxFileBytes);
        Check(ErrorLog.StartSession(false, path, "rotation"), "logging rotates a full file");
        Check(new FileInfo(path).Length <= ErrorLog.MaxFileBytes &&
            new FileInfo(path + ".previous").Length <= ErrorLog.MaxFileBytes,
            "local logs are bounded to two 1 MB files");
        Parallel.For(0, 1000, i => ErrorLog.Write("Concurrent", "private " + i, line: i));
        ErrorLog.Stop();
        content = File.ReadAllText(path);
        Parallel.For(0, 100, i => ErrorLog.WriteCrash("Stopped", "private", line: i));
        Check(File.ReadAllText(path) == content, "Stop cancels queued and concurrent diagnostic writes");
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Check(!ErrorLog.StartSession(false, path, "locked") && !ErrorLog.IsEnabled,
                "a file-sharing failure leaves logging off without throwing");
        Check(!ErrorLog.StartSession(false, @"\\invalid.example\share\test.log", "remote"),
            "logging rejects remote storage");
        ErrorLog.Stop();
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new Exception("FAIL: " + label);
        Console.WriteLine("PASS: " + label);
    }

    private sealed class TestInput : ClipboardFreeTextBox
    {
        public void Deliver(int message)
        {
            Message value = Message.Create(Handle, message, IntPtr.Zero, IntPtr.Zero);
            WndProc(ref value);
        }
    }
}
