using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text.Json;

internal static class ReliabilityChecks
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> Failures = new();

    internal static void Run(Assembly assembly, string root)
    {
        Failures.Clear();
        Type type = assembly.GetType("QuickZoom.TrayContext", true)!;
        var context = (IDisposable)type.GetConstructors()[0].Invoke([null, true, false, Keys.None]);
        void Set(string name, object? value) => type.GetField(name, Instance)!.SetValue(context, value);
        T Get<T>(string name) => (T)type.GetField(name, Instance)!.GetValue(context)!;
        object? Call(string name, params object?[] values) => type.GetMethod(name, Instance)!.Invoke(context, values);
        string path = Path.Combine(root, "reliability.json");
        Set("_settingsPath", path);
        Set("_legacySettingsPath", path);
        Set("_runtimeStopped", true);
        Set("_screenshotMode", false);
        try
        {
            foreach (bool strict in new[] { false, true })
            {
                File.WriteAllText(path, JsonSerializer.Serialize(new { StrictDataMode = strict, StepPercent = 30 }));
                Call("LoadSettings");
                Set("_stepPercent", 40);
                Call("SaveSettings");
                Call("FlushSettingsSave");
                File.SetAttributes(path, FileAttributes.ReadOnly);
                Set("_stepPercent", 50);
                Call("SaveSettings");
                Call("FlushSettingsSave");
                File.SetAttributes(path, FileAttributes.Normal);
                Call("FlushSettingsSave");
                using var saved = JsonDocument.Parse(File.ReadAllText(path));
                Check(saved.RootElement.GetProperty("StepPercent").GetInt32() == 50 &&
                    saved.RootElement.GetProperty("StrictDataMode").GetBoolean() == strict,
                    "shutdown retries the latest failed save without requiring another edit; strict=" + strict);
            }

            foreach (int invalidKey in new[] { 0, -1, 256, 65536, int.MaxValue })
            {
                File.WriteAllText(path, JsonSerializer.Serialize(new
                {
                    EnableKey = invalidKey, InvertKey = invalidKey, FollowCursorKey = invalidKey,
                    StepPercent = int.MaxValue, MaxPercent = int.MinValue, SelectedMonitorDeviceNames = new string?[] { null, "missing" }
                }));
                Call("LoadSettings");
                Check(Get<Keys>("_enableKey") == Keys.Menu && Get<Keys>("_invertKey") == Keys.I &&
                    Get<Keys>("_followCursorKey") == Keys.F && Get<int>("_stepPercent") == 200 &&
                    Get<int>("_maxPercent") == 150 && !Get<bool>("_settingsLoadFailed"),
                    "invalid shortcuts and extreme numeric settings recover safely: " + invalidKey);
                Type setup = assembly.GetType("QuickZoom.FirstRunSetup", true)!;
                object selection = setup.GetMethod("ReadInitialSelection", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [path])!;
                Check((Keys)selection.GetType().GetProperty("EnableKey")!.GetValue(selection)! == Keys.Menu,
                    "setup also rejects invalid shortcut keys: " + invalidKey);
            }

            int geometryFailures = 0;
            foreach (Rectangle screen in new[] { new Rectangle(0, 0, 1920, 1080), new Rectangle(-1280, -720, 1280, 720), new Rectangle(0, 0, 800, 600) })
            foreach (string shape in new[] { "Rectangle", "Square", "Circle" })
            foreach (int size in new[] { 100, 360, 1400 })
            foreach (Point anchor in new[] { screen.Location, new Point(screen.Right - 1, screen.Bottom - 1), Point.Empty })
            {
                Set("_lensShape", Enum.Parse(type.GetField("_lensShape", Instance)!.FieldType, shape));
                Set("_lensSize", size);
                Set("_smoothedLensCenter", null);
                Rectangle lens = (Rectangle)Call("BuildLensBounds", anchor, screen)!;
                if (!screen.Contains(lens) || (shape != "Rectangle" && lens.Width != lens.Height)) geometryFailures++;
            }
            Check(geometryFailures == 0, "all lens shapes fit short displays and negative monitor coordinates (81 cases)");

            Set("_runtimeStopped", false);
            Set("_startupInitialized", true);
            string[] pressedFields = ["_enableKeyPressed", "_invertKeyPressed", "_followCursorKeyPressed", "_zoomModeCycleKeyPressed",
                "_leftMouseButtonPressed", "_rightMouseButtonPressed", "_zoomModeMouseChordTriggered", "_suppressLeftMouseButtonUp", "_suppressRightMouseButtonUp"];
            foreach (string name in pressedFields) Set(name, true);
            Call("RecoverAfterResume", "Test.Resume");
            Check(pressedFields.All(name => !Get<bool>(name)), "resume clears all held shortcut and mouse-chord state");

            int callbacks = 0;
            MethodInfo dispatch = type.GetMethod("RunOnUiThread", Instance, [typeof(string), typeof(Action)])!;
            Task.Run(() => dispatch.Invoke(context, ["Test.QueuedCallback", (Action)(() => callbacks++)])).GetAwaiter().GetResult();
            Set("_runtimeStopped", true);
            Application.DoEvents();
            Check(callbacks == 0, "a queued callback cannot restart work after shutdown");

            Set("_screenshotMode", true);
            int pages = 0;
            foreach (bool strict in new[] { false, true })
            foreach (bool dark in new[] { false, true })
            foreach (object language in Enum.GetValues(type.GetField("_language", Instance)!.FieldType))
            {
                Set("_strictDataMode", strict);
                Set("_useDarkTheme", dark);
                Set("_language", language);
                foreach (string pageName in new[] { "General", "Display", "Zoom", "Cursor", "Appearance", "Input", "About" })
                {
                    using var page = (Control)Call("Build" + pageName + "SettingsPage")!;
                    page.Size = new Size(1000, 700);
                    page.PerformLayout();
                    if (page.Controls.Count == 0) throw new Exception("Empty settings page: " + pageName);
                    pages++;
                }
            }
            Check(pages > 0, "all settings pages construct, lay out and dispose in every language, theme and data mode: " + pages);
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal);
            Set("_screenshotMode", true);
            Set("_pendingSettingsSave", null);
            Set("_runtimeStopped", false);
            Control invoker = Get<Control>("_uiInvoker");
            bool cleanupFailureInjected = false;
            var failingForm = new Form();
            failingForm.Disposed += (_, _) =>
            {
                cleanupFailureInjected = true;
                throw new InvalidOperationException("Deliberate shutdown callback failure.");
            };
            Set("_settingsWindow", failingForm);
            bool timerDisposed = false;
            var timer = new System.Windows.Forms.Timer();
            timer.Disposed += (_, _) => timerDisposed = true;
            Set("_settingsZoomModeApplyTimer", timer);
            context.Dispose();
            context.Dispose();
            Check(invoker.IsDisposed && cleanupFailureInjected && timerDisposed,
                "repeated disposal releases dispatcher and delayed mode timer despite a failing shutdown callback");
            Get<System.Windows.Forms.Timer?>("_settingsSaveTimer")?.Dispose();
            invoker.Dispose();
        }
        if (Failures.Count > 0) throw new Exception(string.Join(Environment.NewLine, Failures));
    }

    private static void Check(bool value, string description)
    {
        Console.WriteLine((value ? "PASS: " : "FAIL: ") + description);
        if (!value) Failures.Add(description);
    }
}
