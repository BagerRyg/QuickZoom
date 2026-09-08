using QuickZoom;

internal static class PrivacyChecks
{
    [STAThread]
    private static int Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "settings-test.json");
        FilePersistence.WriteAllTextAtomic(path, "{\"StepPercent\":30}");
        FilePersistence.WriteAllTextAtomic(path, "{\"StepPercent\":40}");
        Check(File.ReadAllText(path) == "{\"StepPercent\":40}", "atomic replacement");
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
        int evaluated = 0;
        ErrorLog.WriteAlways("test", (++evaluated).ToString());
        ErrorLog.WriteCrash("test", (++evaluated).ToString());
        ErrorLog.Write("test", (++evaluated).ToString());
        ErrorLog.EnsureLogFileExists();
        Check(evaluated == 0, "diagnostic arguments omitted from compiled calls");
        Check(!Directory.GetFiles(root, "*.log").Any(), "no diagnostic files");
        Console.WriteLine("All privacy regression checks passed.");
        return 0;
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
