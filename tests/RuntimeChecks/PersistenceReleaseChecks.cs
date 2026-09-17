using System.IO;
using System.Reflection;
using System.Text.Json;

internal static class PersistenceReleaseChecks
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static void Run(Assembly assembly, string root)
    {
        CheckReplacementRetry(assembly, root);
        Type type = assembly.GetType("QuickZoom.TrayContext", true)!;
        foreach (bool strict in new[] { false, true })
        {
            using var context = (IDisposable)type.GetConstructors()[0].Invoke([null, true, false, Keys.None]);
            void Set(string name, object? value) => type.GetField(name, Instance)!.SetValue(context, value);
            T Get<T>(string name) => (T)type.GetField(name, Instance)!.GetValue(context)!;
            object? Call(string name, params object?[] values) => type.GetMethod(name, Instance)!.Invoke(context, values);
            string path = Path.Combine(root, "slow-save-" + strict + ".json");
            File.WriteAllText(path, "{\"StepPercent\":30}");
            Set("_settingsPath", path);
            Set("_legacySettingsPath", path);
            Set("_screenshotMode", false);
            Set("_runtimeStopped", true);
            Call("LoadSettings");
            Set("_strictDataMode", strict);
            Set("_stepPercent", 84);
            Call("SaveSettings");
            using var held = new ManualResetEventSlim();
            Task slowWriter = Task.Run(() =>
            {
                lock (Get<object>("_settingsWriteSync"))
                {
                    held.Set();
                    Thread.Sleep(1800);
                }
            });
            try
            {
                if (!held.Wait(TimeSpan.FromSeconds(5))) throw new Exception("Slow save setup timed out.");
                Call("OnSettingsSaveTimerTick", null, EventArgs.Empty);
                Call("FlushSettingsSave");
                using JsonDocument saved = JsonDocument.Parse(File.ReadAllText(path));
                if (saved.RootElement.GetProperty("StepPercent").GetInt32() != 84 ||
                    saved.RootElement.GetProperty("StrictDataMode").GetBoolean() != strict)
                    throw new Exception("Shutdown returned before the slow in-flight settings save completed.");
                Console.WriteLine("PASS: shutdown preserves an in-flight save exceeding the worker wait; strict=" + strict);
            }
            finally
            {
                slowWriter.GetAwaiter().GetResult();
                Get<Task?>("_settingsSaveTask")?.GetAwaiter().GetResult();
                Set("_screenshotMode", true);
                Set("_runtimeStopped", false);
            }
        }
    }

    private static void CheckReplacementRetry(Assembly assembly, string root)
    {
        MethodInfo write = assembly.GetType("QuickZoom.FilePersistence", true)!
            .GetMethod("WriteAllTextAtomic", BindingFlags.Static | BindingFlags.NonPublic)!;
        string path = Path.Combine(root, "sharing-retry.json");
        File.WriteAllText(path, "original");
        using var held = new ManualResetEventSlim();
        Task reader = Task.Run(() =>
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            held.Set();
            Thread.Sleep(100);
        });
        if (!held.Wait(TimeSpan.FromSeconds(5))) throw new Exception("File sharing test setup timed out.");
        try { write.Invoke(null, [path, "updated"]); }
        finally { reader.GetAwaiter().GetResult(); }
        if (File.ReadAllText(path) != "updated") throw new Exception("A temporary file lock lost the settings save.");
        Console.WriteLine("PASS: atomic replacement recovers from a temporary sharing violation");

        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            bool rejected = false;
            try { write.Invoke(null, [path, "must-not-replace"]); }
            catch (TargetInvocationException ex) when (ex.InnerException is IOException) { rejected = true; }
            if (!rejected || File.ReadAllText(path) != "updated")
                throw new Exception("An exhausted sharing retry did not preserve the original settings.");
        }
        if (Directory.GetFiles(root, "sharing-retry.json.*.tmp").Length != 0)
            throw new Exception("An exhausted atomic save left temporary files behind.");
        Console.WriteLine("PASS: persistent sharing violations fail cleanly and preserve the previous file");
    }
}
