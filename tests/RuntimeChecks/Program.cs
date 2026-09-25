using System.Reflection;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class RuntimeChecks
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--per-monitor-dpi"))
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
        }
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        try
        {
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
        if (args.Contains("--setup-startup"))
        {
            SetupStartupChecks.Run(Assembly.Load("QuickZoom"), args[0]);
            return;
        }
        if (args.Contains("--tracking-ui"))
        {
            TrackingUiChecks.Run(Assembly.Load("QuickZoom"), args[0], capture: true);
            return;
        }
        if (args.Contains("--tracking-native"))
        {
            TrackingChecks.RunNative(Assembly.Load("QuickZoom"));
            return;
        }
        if (args.Contains("--native-only"))
        {
            NativeChecks.Run(Assembly.Load("QuickZoom"));
            return;
        }
        string root = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(root);
        if (args.Contains("--release-only"))
        {
            RunReleaseChecks(Assembly.Load("QuickZoom"), root);
            return;
        }
        string path = Path.Combine(root, "settings.json");
        Assembly assembly = Assembly.Load("QuickZoom");
        Type contextType = assembly.GetType("QuickZoom.TrayContext", true)!;
        using var context = (IDisposable)contextType.GetConstructors()[0].Invoke([null, true, false, Keys.None]);
        void Set(string name, object? value) => contextType.GetField(name, Instance)!.SetValue(context, value);
        T Get<T>(string name) => (T)contextType.GetField(name, Instance)!.GetValue(context)!;
        object? Call(string name, params object?[] values) => contextType.GetMethod(name, Instance)!.Invoke(context, values);

        // Redirect every possible write before exercising the normal loader. The
        // screenshot constructor never starts the engine, tray, or input hooks.
        Set("_settingsPath", path);
        Set("_legacySettingsPath", path);
        Set("_screenshotMode", false);
        Set("_runtimeStopped", true); // suppress error dialogs in this headless test
        try
        {
            string valid = """
                {"ThemeMode":1,"UiFontSize":0,"StepPercent":46,"MaxPercent":600,
                 "EnableKey":17,"InvertEnabled":true,"CursorScale":290,"Fps":0}
                """;
            File.WriteAllText(path, valid);
            File.SetAttributes(path, FileAttributes.ReadOnly);
            Call("LoadSettings");
            Check(!Get<bool>("_settingsLoadFailed") && Get<int>("_stepPercent") == 46 &&
                Get<int>("_maxPercent") == 600 && Get<int>("_cursorScale") == 290 &&
                Get<int>("_fps") == 0 && Get<Keys>("_enableKey") == Keys.ControlKey,
                "valid read-only preferences load without any profile write");
            File.SetAttributes(path, FileAttributes.Normal);

            using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Call("LoadSettings");
                Call("SaveSettings");
                Call("FlushSettingsSave");
                Check(Get<bool>("_settingsLoadFailed"), "a sharing violation never enables default preference saves");
            }
            Check(File.ReadAllText(path) == valid, "a temporarily locked preferences file remains unchanged");
            Call("LoadSettings");
            Check(!Get<bool>("_settingsLoadFailed") && Get<int>("_stepPercent") == 46,
                "loading recovers after the file lock is released");

            string absentParent = Path.Combine(root, Guid.NewGuid().ToString("N"));
            Set("_settingsPath", Path.Combine(absentParent, "settings.json"));
            Set("_legacySettingsPath", Path.Combine(absentParent, "settings.json"));
            Call("LoadSettings");
            Check(!Directory.Exists(absentParent), "loading missing preferences does not create directories");
            Set("_settingsPath", path);
            Set("_legacySettingsPath", path);

            foreach (string invalid in new[] { "{", "null", "[]" })
            {
                File.WriteAllText(path, invalid);
                Call("LoadSettings");
                Call("SaveSettings");
                Call("FlushSettingsSave");
                Check(Get<bool>("_settingsLoadFailed") && File.ReadAllText(path) == invalid,
                    "malformed preferences remain intact and cannot be overwritten by defaults: " + invalid);
            }

            File.WriteAllText(path, valid);
            Call("LoadSettings");
            Set("_stepPercent", 35);
            Call("SaveSettings");
            object older = Get<object>("_pendingSettingsSave");
            Set("_stepPercent", 65);
            Call("SaveSettings");
            object newer = Get<object>("_pendingSettingsSave");
            Call("WriteSettingsSnapshot", newer);
            Call("WriteSettingsSnapshot", older);
            Check(ReadStep(path) == 65, "a delayed older save cannot overwrite a newer save");
            Set("_stepPercent", 75);
            Call("SaveSettings");
            Call("FlushSettingsSave");
            Call("WriteSettingsSnapshot", newer);
            Check(ReadStep(path) == 75, "shutdown flush cannot be overwritten by a queued worker");

            File.SetAttributes(path, FileAttributes.ReadOnly);
            Set("_stepPercent", 85);
            Call("SaveSettings");
            Call("FlushSettingsSave");
            Check(ReadStep(path) == 75 && Get<int>("_settingsSaveFailureNotified") == 1,
                "failed atomic save retains previous preferences and records a notification");
            File.SetAttributes(path, FileAttributes.Normal);
            Call("SaveSettings");
            Call("FlushSettingsSave");
            Check(ReadStep(path) == 85 && Get<int>("_settingsSaveFailureNotified") == 0,
                "saving recovers after a transient write failure");

            Set("_stepPercent", 95);
            Call("SaveSettings");
            Call("OnSettingsSaveTimerTick", null, EventArgs.Empty);
            Check(Get<Task>("_settingsSaveTask").Wait(TimeSpan.FromSeconds(10)),
                "the GUI save timer's background worker finishes");
            Set("_stepPercent", 30);
            Call("LoadSettings");
            Check(Get<int>("_stepPercent") == 95 && Get<int>("_settingsSaveFailureNotified") == 0,
                "GUI background saves persist and reload correctly");

            Type setup = assembly.GetType("QuickZoom.FirstRunSetup", true)!;
            MethodInfo readSelection = setup.GetMethod("ReadInitialSelection", Static)!;
            MethodInfo readSettings = setup.GetMethod("ReadSettingsObject", Static)!;
            Check(!Get<bool>("_strictDataMode") && !Get<bool>("_debugLoggingEnabled"),
                "missing privacy preferences default to Strict Data OFF and logging OFF");
            foreach (bool strict in new[] { false, true })
            {
                File.WriteAllText(path, "{\"StrictDataMode\":" + strict.ToString().ToLowerInvariant() +
                    ",\"DebugLoggingEnabled\":true,\"StepPercent\":46}");
                Call("LoadSettings");
                Check(Get<bool>("_strictDataMode") == strict && !Get<bool>("_debugLoggingEnabled"),
                    "privacy preference loads without silently enabling old logging flags: " + strict);
                object selection = readSelection.Invoke(null, [path])!;
                Check((bool)selection.GetType().GetProperty("StrictDataMode")!.GetValue(selection)! == strict,
                    "setup restores the chosen data mode: " + strict);
                setup.GetMethod("SaveSelection", Static)!.Invoke(null, [selection, path]);
                using JsonDocument saved = JsonDocument.Parse(File.ReadAllText(path));
                Check(saved.RootElement.GetProperty("StrictDataMode").GetBoolean() == strict &&
                    !saved.RootElement.TryGetProperty("DebugLoggingEnabled", out _) && ReadStep(path) == 46,
                    "setup persists privacy, preserves preferences and never persists logging consent");
            }
            foreach (int theme in new[] { 0, 1, 2 })
            {
                File.WriteAllText(path, valid.Replace("\"ThemeMode\":1", "\"ThemeMode\":" + theme));
                object selection = readSelection.Invoke(null, [path])!;
                Check((int)selection.GetType().GetProperty("ThemeMode")!.GetValue(selection)! == theme &&
                    (Keys)selection.GetType().GetProperty("EnableKey")!.GetValue(selection)! == Keys.ControlKey,
                    "replayed setup preserves saved theme and hotkey: " + theme);
                var settings = (JsonObject)readSettings.Invoke(null, [path])!;
                Check(settings["StepPercent"]!.GetValue<int>() == 46 && settings["MaxPercent"]!.GetValue<int>() == 600,
                    "setup retains preferences outside the wizard choices");
            }
            foreach (string invalid in new[] { "{", "null", "[]" })
            {
                File.WriteAllText(path, invalid);
                bool rejected = false;
                try { readSettings.Invoke(null, [path]); }
                catch (TargetInvocationException e) when (e.InnerException is JsonException) { rejected = true; }
                Check(rejected && File.ReadAllText(path) == invalid, "setup refuses to replace unreadable preferences: " + invalid);
            }

            File.WriteAllText(path, valid);
            Call("LoadSettings");
            using var diagnosticsOwner = new Control();
            using var diagnosticsSection = (Control)Call("BuildDiagnosticsSection", diagnosticsOwner)!;
            var toggles = Descendants(diagnosticsSection)
                .Where(control => control.GetType().Name == "ToggleSwitchControl").ToArray();
            Check(toggles.Length == 2 && toggles[1].Enabled, "About exposes Strict Data and opt-in logging controls");
            void Click(Control toggle) => toggle.GetType().GetMethod("OnClick", Instance)!.Invoke(toggle, [EventArgs.Empty]);
            Click(toggles[1]);
            Check(Get<bool>("_debugLoggingEnabled") && File.Exists(Path.Combine(root, "quickzoom-error.log")),
                "the About logging switch starts a local diagnostic session");
            Click(toggles[0]);
            Check(Get<bool>("_strictDataMode") && !Get<bool>("_debugLoggingEnabled") && !toggles[1].Enabled,
                "Strict Data immediately stops logging and disables its About control");
            Click(toggles[0]);
            Check(!Get<bool>("_strictDataMode") && !Get<bool>("_debugLoggingEnabled") && toggles[1].Enabled,
                "leaving Strict Data restores the option without silently restarting logging");
        }
        finally
        {
            Set("_screenshotMode", true);
            Set("_pendingSettingsSave", null);
            Get<System.Windows.Forms.Timer?>("_settingsSaveTimer")?.Dispose();
            Get<Control>("_uiInvoker").Dispose();
            if (File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal);
        }
        ReliabilityChecks.Run(assembly, root);
        RunReleaseChecks(assembly, root);
        Console.WriteLine("All runtime checks passed; no live app, scheduled task, or user preferences were changed.");
        if (!args.Contains("--no-render")) ValidateStartupCompletion(assembly);
    }

    private static void RunReleaseChecks(Assembly assembly, string root)
    {
        TrackingChecks.Run(assembly);
        TrackingUiChecks.Run(assembly, root);
        PersistenceReleaseChecks.Run(assembly, root);
        UiReleaseChecks.Run(assembly, root);
        NativeRecoveryChecks.Run(assembly, root);
        InputReleaseChecks.Run(assembly, root);
        StartupReleaseChecks.Run(assembly, root);
        SetupStartupChecks.Run(assembly, root);
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (Control child in control.Controls)
        {
            yield return child;
            foreach (Control nested in Descendants(child)) yield return nested;
        }
    }

    private static void ValidateStartupCompletion(Assembly assembly)
    {
        Type tileType = assembly.GetType("QuickZoom.FirstRunSetup+SetupStartupServiceTile", true)!;
        Type preferences = assembly.GetType("QuickZoom.AccessibilityPreferences", true)!;
        PropertyInfo highContrast = preferences.GetProperty("CaptureHighContrast", Static)!;
        bool originalHighContrast = (bool)highContrast.GetValue(null)!;
        try
        {
            foreach (bool reducedMotion in new[] { false, true })
            {
                highContrast.SetValue(null, reducedMotion);
                using var tile = (Control)Activator.CreateInstance(tileType, nonPublic: true)!;
                tile.Size = new System.Drawing.Size(900, 500);
                object palette = assembly.GetType("QuickZoom.ThemePalettes", true)!
                    .GetProperty("Dark", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
                tileType.GetMethod("ApplyTheme", Instance)!.Invoke(tile, [palette, 1f]);
                tile.CreateControl();
                FieldInfo state = tileType.GetField("_state", Instance)!;
                state.SetValue(tile, Enum.Parse(state.FieldType, "Installing"));
                tileType.GetField("_lastPaintedProgress", Instance)!.SetValue(tile, 0.47f);
                tileType.GetMethod("UpdateContent", Instance)!.Invoke(tile,
                    ["QuickZoom autostart", "Allow automatic startup.", "Starts after sign-in.",
                        "Keeps shortcuts available.", "Windows asks for permission.", "Verifying startup.",
                        Enum.Parse(state.FieldType, "Verifying")]);
                float stageProgress = (float)tileType.GetProperty("EstimatedProgress", Instance)!.GetValue(tile)!;
                Check(stageProgress >= 0.47f && stageProgress < 0.50f,
                    "verification continues from the rendered fill without jumping to 91 percent");
                using var frame = new System.Drawing.Bitmap(tile.Width, tile.Height);
                tileType.GetField("_completionProgress", Instance)!.SetValue(tile, 0.47f);
                tile.DrawToBitmap(frame, tile.ClientRectangle);
                tileType.GetField("_completionProgress", Instance)!.SetValue(tile, -1f);
                var progressBounds = (System.Drawing.Rectangle)tileType.GetField("_progressBounds", Instance)!.GetValue(tile)!;
                var dirtyRegion = System.Drawing.Rectangle.Inflate(progressBounds, 2, 2);
                long staticPixelsBefore = StaticPixels();
                var dirtyBounds = new List<System.Drawing.Rectangle>();
                tile.Invalidated += (_, e) => dirtyBounds.Add(e.InvalidRect);
                var task = (Task)tileType.GetMethod("CompleteProgressAsync", Instance)!.Invoke(tile, null)!;
                var animationTimer = (System.Windows.Forms.Timer)tileType.GetField("_animationTimer", Instance)!.GetValue(tile)!;
                Check(!animationTimer.Enabled, "completion stops the competing busy-animation timer");
                if (!reducedMotion)
                    Check((float)tileType.GetField("_completionProgress", Instance)!.GetValue(tile)! == 0.47f,
                        "verified completion starts from the last displayed frame");
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var intermediateFrames = new HashSet<float>();
                while (!task.IsCompleted && watch.ElapsedMilliseconds < 5000)
                {
                    Application.DoEvents();
                    using (var graphics = System.Drawing.Graphics.FromImage(frame))
                    {
                        graphics.SetClip(dirtyRegion);
                        using var paint = new PaintEventArgs(graphics, dirtyRegion);
                        tileType.GetMethod("OnPaintBackground", Instance)!.Invoke(tile, [paint]);
                        tileType.GetMethod("OnPaint", Instance)!.Invoke(tile, [paint]);
                    }
                    float painted = (float)tileType.GetField("_lastPaintedProgress", Instance)!.GetValue(tile)!;
                    if (painted > 0.48f && painted < 0.99f) intermediateFrames.Add(painted);
                    Thread.Sleep(5);
                }
                if (!reducedMotion)
                    Check(intermediateFrames.Count >= 5, "completion renders distinct intermediate liquid-fill frames, not just endpoints");
                Check(task.IsCompleted, "startup completion animation finishes without blocking the UI");
                task.GetAwaiter().GetResult();
                Check(dirtyBounds.Count > 0 && dirtyBounds.All(rect => System.Drawing.Rectangle.Inflate(progressBounds, 2, 2).Contains(rect)),
                    "completion invalidates only the progress bar, not the full startup card");
                Check(StaticPixels() == staticPixelsBefore, "partial animation paints leave the rest of the card unchanged");
                Check((float)tileType.GetField("_completionProgress", Instance)!.GetValue(tile)! == 1f &&
                    (float)tileType.GetField("_completionSuccessBlend", Instance)!.GetValue(tile)! == 1f &&
                    state.GetValue(tile)!.ToString() == "Verifying",
                    "full green bar is rendered before the caller announces Ready; reduced motion: " + reducedMotion);

                long StaticPixels()
                {
                    long hash = 17;
                    for (int y = 0; y < dirtyRegion.Top; y += 7)
                        for (int x = 0; x < frame.Width; x += 7)
                            hash = unchecked(hash * 31 + frame.GetPixel(x, y).ToArgb());
                    return hash;
                }
            }
        }
        finally { highContrast.SetValue(null, originalHighContrast); }
    }

    private static int ReadStep(string path)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("StepPercent").GetInt32();
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception("FAIL: " + description);
        Console.WriteLine("PASS: " + description);
    }
}
