using System.Reflection;

internal static class TrackingChecks
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    internal static void Run(Assembly assembly)
    {
        Type type = assembly.GetType("QuickZoom.TrackingController", true)!;
        Type modeType = assembly.GetType("QuickZoom.TrackingMode", true)!;
        Type snapshotType = assembly.GetType("QuickZoom.TrackingSnapshot", true)!;
        object controller = Activator.CreateInstance(type, true)!;
        object? Call(string name, params object?[] values) => type.GetMethod(name, Instance)!.Invoke(controller, values);
        bool Active() => (bool)type.GetProperty("KeyboardActive", Instance)!.GetValue(controller)!;
        void Mode(int value) => type.GetProperty("Mode", Instance)!.SetValue(controller, Enum.ToObject(modeType, value));
        object Sample(long at, Rectangle? caret, Rectangle? focus = null, int app = 1) =>
            Activator.CreateInstance(snapshotType, [new IntPtr(app), at, caret, focus])!;
        Point Resolve(Point mouse, long now, object? sample = null, int app = 1) =>
            (Point)Call("Resolve", mouse, new IntPtr(app), sample, now)!;
        var mouse = new Point(100, 100);
        var caret = new Rectangle(500, 400, 2, 20);
        Check(Resolve(mouse, 100) == mouse && !Active(), "automatic starts with the mouse");
        Call("ObserveKeyboard", Keys.A, 200L);
        Point typed = Resolve(mouse, 220, Sample(210, caret));
        Check(Active() && typed == new Point(501, 410), "typing switches to a fresh caret");
        Check(Resolve(mouse, 9000) == typed && Active(), "reading pauses and missing providers keep the view still");
        Call("ObservePointer", new Point(103, 102), 0x200, 9010L, 6);
        Check(Resolve(new Point(103, 102), 9011) == typed, "small pointer tremor does not steal typing focus");
        Call("ObservePointer", new Point(111, 100), 0x200, 9020L, 6);
        Check(Resolve(new Point(111, 100), 9021, Sample(9020, caret)) == new Point(111, 100) && !Active(),
            "deliberate movement immediately restores mouse control");
        Call("ObserveKeyboard", Keys.Right, 9100L);
        Check(Resolve(mouse, 9110, Sample(9000, caret)) == new Point(111, 100) && !Active(),
            "pre-input snapshots cannot move the view");
        Resolve(mouse, 9140, Sample(9130, caret));
        Check(Active(), "arrow navigation reacquires the caret");
        Call("ObservePointer", mouse, 0x201, 9200L, 6);
        Call("ObserveKeyboard", Keys.Right, 9210L);
        Check(Resolve(mouse, 9220, Sample(9215, caret)) == mouse && !Active(), "mouse dragging owns automatic tracking");
        Call("ObservePointer", mouse, 0x202, 9230L, 6);
        Call("ObserveKeyboard", Keys.Tab, 9300L);
        var button = new Rectangle(600, 300, 100, 32);
        Point focused = Resolve(mouse, 9320, Sample(9310, null, button));
        Check(Active() && button.Contains(focused), "Tab follows the focused control when there is no caret");
        Check(Resolve(mouse, 9400, Sample(9390, caret), app: 2) == focused,
            "foreground changes reject geometry from the previous app");
        Check(Resolve(mouse, 9420, Sample(9410, caret, app: 2), app: 2) == typed, "fresh foreground geometry is accepted");

        Call("Reset"); Mode(1);
        Resolve(mouse, 100);
        Call("ObserveKeyboard", Keys.A, 200L);
        Check(Resolve(mouse, 220, Sample(210, caret)) == mouse && !Active(), "Mouse only ignores keyboard geometry");
        Call("Reset"); Mode(2);
        Resolve(mouse, 100);
        Resolve(mouse, 220, Sample(210, caret));
        Call("ObservePointer", new Point(900, 800), 0x200, 300L, 6);
        Check(Resolve(new Point(900, 800), 310, Sample(290, caret)) == typed && Active(),
            "Keyboard and typing ignores mouse movement");
        Call("ObservePointer", new Point(900, 800), 0x201, 320L, 6);
        Check(Resolve(mouse, 350, Sample(340, new Rectangle(800, 600, 2, 20))) == typed,
            "keyboard mode holds still during mouse selection dragging");
        Call("ObservePointer", new Point(900, 800), 0x202, 360L, 6);
        Check(Resolve(mouse, 380, Sample(370, caret)) == typed, "keyboard mode resumes after drag release");
        Call("Reset"); Mode(0); Resolve(mouse, 100);
        foreach (Keys key in new[] { Keys.ControlKey, Keys.ShiftKey, Keys.Menu, Keys.VolumeUp, Keys.F5, Keys.PrintScreen })
            Call("ObserveKeyboard", key, 200L);
        Check(Resolve(mouse, 230, Sample(220, caret)) == mouse && !Active(), "modifiers and unrelated keys do not trigger tracking");
        Call("ObserveKeyboard", Keys.Left, 250L);
        Check((int)type.GetProperty("SelectionDirection", Instance)!.GetValue(controller)! == -1,
            "leftward selection supplies the active-end fallback direction");

        Rectangle Keep(Rectangle bounds, Size size, Rectangle target, Rectangle? previous) =>
            (Rectangle)type.GetMethod("KeepVisible", Static)!.Invoke(null, [bounds, size, target, previous])!;
        var screen = new Rectangle(0, 0, 1920, 1080);
        var view = new Rectangle(300, 200, 480, 270);
        Check(Keep(screen, view.Size, caret, view) == view, "typing inside the comfort area does not pan");
        Rectangle panned = Keep(screen, view.Size, new Rectangle(770, 400, 2, 20), view);
        Check(panned.Left > view.Left && panned.Top == view.Top, "edge typing pans only the required axis");
        Check(Keep(screen, view.Size, screen, view) == view, "large document focus never jumps to its centre");
        var negativeScreen = new Rectangle(-1920, -1080, 1920, 1080);
        Rectangle negative = Keep(negativeScreen, view.Size, new Rectangle(-1919, -1079, 1, 20), null);
        Check(negativeScreen.Contains(negative) && negative.Contains(-1919, -1079), "viewport clamps at negative monitor coordinates");
        Type reader = assembly.GetType("QuickZoom.FocusGeometryReader", true)!;
        object? Bounds(double[] values) => reader.GetMethod("Bounds", Static)!.Invoke(null, [values]);
        Check(Bounds([double.NaN, 0, 1, 10]) == null && Bounds([0, 0, 1, 0]) == null &&
              Bounds([0, 0, double.PositiveInfinity, 10]) == null, "invalid provider geometry is rejected");
        Check(Bounds([-200, 30, 0, 20]) is Rectangle { Width: 1 }, "zero-width caret geometry stays trackable");

        Type contextType = assembly.GetType("QuickZoom.TrayContext", true)!;
        using var context = (IDisposable)contextType.GetConstructors()[0].Invoke([null, true, false, Keys.None]);
        Type settingsType = contextType.GetNestedType("Settings", BindingFlags.NonPublic)!;
        foreach (string json in new[] { "{}", "{\"TrackingSource\":0}", "{\"TrackingMode\":999}" })
        {
            object settings = System.Text.Json.JsonSerializer.Deserialize(json, settingsType)!;
            contextType.GetMethod("ApplySettingsModel", Instance)!.Invoke(context, [settings]);
            Check(Convert.ToInt32(contextType.GetField("_trackingMode", Instance)!.GetValue(context)) == 0,
                "missing, legacy, or invalid tracking preference defaults to Automatic: " + json);
        }
        for (int mode = 0; mode < 3; mode++)
        {
            object settings = System.Text.Json.JsonSerializer.Deserialize("{\"TrackingMode\":" + mode + "}", settingsType)!;
            contextType.GetMethod("ApplySettingsModel", Instance)!.Invoke(context, [settings]);
            object current = contextType.GetMethod("CreateSettingsSnapshot", Instance)!.Invoke(context, null)!;
            Check((int)settingsType.GetProperty("TrackingMode")!.GetValue(current)! == mode,
                "tracking preference survives settings serialization: " + mode);
        }
    }

    // Explicit opt-in: creates its own disposable editor window and checks real
    // Windows caret/UIA providers. It does not install hooks or change OS zoom.
    internal static void RunNative(Assembly assembly)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        using var form = new Form { Text = "QuickZoom tracking validation", Size = new Size(800, 450), StartPosition = FormStartPosition.CenterScreen };
        using var editor = new RichTextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 18), Text = "Tracking validation\nA second line of sample text.\nA third line." };
        form.Controls.Add(editor);
        form.Show();
        form.Activate();
        editor.Focus();
        editor.SelectionStart = 8;
        Application.DoEvents();
        Probe(requireAutomation: false);
        form.Controls.Remove(editor);
        using var host = new System.Windows.Forms.Integration.ElementHost { Dock = DockStyle.Fill };
        var wpfEditor = new System.Windows.Controls.TextBox
        {
            Text = "Automation tracking validation\nA second line of sample text.",
            FontSize = 24,
            AcceptsReturn = true
        };
        host.Child = wpfEditor;
        form.Controls.Add(host);
        host.Focus();
        wpfEditor.Focus();
        wpfEditor.CaretIndex = 8;
        Application.DoEvents();
        Probe(requireAutomation: true);
        wpfEditor.CaretIndex = wpfEditor.Text.Length;
        Application.DoEvents();
        Probe(requireAutomation: true);
        form.Close();

        void Probe(bool requireAutomation)
        {
            Type readerType = assembly.GetType("QuickZoom.FocusGeometryReader", true)!;
            IntPtr foreground = form.Handle;
            Console.WriteLine("Waiting for the tracking validation editor to be foreground...");
            long activationDeadline = Environment.TickCount64 + 45000;
            while ((IntPtr)readerType.GetMethod("GetForegroundWindow", Static)!.Invoke(null, null)! != foreground &&
                   Environment.TickCount64 < activationDeadline)
            {
                Application.DoEvents();
                Thread.Sleep(25);
            }
            Application.DoEvents();
            object? reader = null;
            Exception? failure = null;
            Rectangle? nativeCaret = null;
            Rectangle? automationCaret = null;
            bool timeoutsMatch = false;
            IntPtr actualForeground = IntPtr.Zero;
            object? automationProcess = null;
            var thread = new Thread(() =>
            {
                try
                {
                    reader = Activator.CreateInstance(readerType, true)!;
                    actualForeground = (IntPtr)readerType.GetMethod("GetForegroundWindow", Static)!.Invoke(null, null)!;
                    object? snapshot = readerType.GetMethod("Read", Instance)!.Invoke(reader, [foreground, Environment.TickCount64, 1]);
                    nativeCaret = (Rectangle?)snapshot?.GetType().GetProperty("Caret")!.GetValue(snapshot);
                    readerType.GetMethod("EnsureClient", Instance)!.Invoke(reader, null);
                    object client = readerType.GetField("_client", Instance)!.GetValue(reader)!;
                    Type contract = assembly.GetType("QuickZoom.AutomationInterop+IClient", true)!;
                    timeoutsMatch = (uint)contract.GetMethod("GetConnectionTimeout")!.Invoke(client, null)! == 150 &&
                        (uint)contract.GetMethod("GetTransactionTimeout")!.Invoke(client, null)! == 200;
                    for (int attempt = 0; attempt < 30 && automationCaret == null; attempt++)
                    {
                        object element = contract.GetMethod("GetFocusedElement")!.Invoke(client, null)!;
                        try
                        {
                            automationProcess = assembly.GetType("QuickZoom.AutomationInterop+IElement", true)!
                            .GetMethod("GetCurrentPropertyValue")!.Invoke(element, [30002]);
                            automationCaret = (Rectangle?)readerType.GetMethod("ReadCaret", Instance)!.Invoke(reader, [element, 1]);
                        }
                        finally { System.Runtime.InteropServices.Marshal.ReleaseComObject(element); }
                        if (automationCaret == null) Thread.Sleep(50);
                    }
                }
                catch (Exception ex) { failure = ex; }
                finally { (reader as IDisposable)?.Dispose(); }
            })
            { IsBackground = true };
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
            long deadline = Environment.TickCount64 + 15000;
            while (thread.IsAlive && Environment.TickCount64 < deadline) { Application.DoEvents(); Thread.Sleep(10); }
            if (failure != null) throw new Exception("Native tracking provider test failed.", failure);
            Check(!thread.IsAlive, "real caret provider returns without blocking the UI");
            Rectangle expected = form.RectangleToScreen(form.ClientRectangle);
            Console.WriteLine($"Expected editor: {expected}; native caret: {nativeCaret}; UIA caret: {automationCaret}; timeouts: {timeoutsMatch}; foreground={actualForeground}/{foreground}; process={automationProcess}/{Environment.ProcessId}");
            Check(nativeCaret.HasValue && expected.IntersectsWith(nativeCaret.Value), "tracking geometry belongs to the focused test editor");
            Check(timeoutsMatch, "UIAutomation2 connection and transaction timeout slots are verified against Windows");
            if (requireAutomation)
                Check(automationCaret.HasValue && expected.IntersectsWith(automationCaret.Value), "UI Automation independently locates the WPF editor caret");
            Console.WriteLine($"Native caret: {nativeCaret}; UIA caret: {automationCaret}");
        }
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception("FAIL: " + description);
        Console.WriteLine("PASS: " + description);
    }
}
