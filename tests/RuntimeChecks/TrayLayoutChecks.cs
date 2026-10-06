using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

internal static class TrayLayoutChecks
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    internal static void Run(Assembly assembly, string root, bool capture = false)
    {
        Type drawing = assembly.GetType("QuickZoom.ControlDrawing", true)!;
        PropertyInfo fontScale = drawing.GetProperty("UiFontScale", Static)!;
        PropertyInfo windowsScale = drawing.GetProperty("FollowWindowsTextScale", Static)!;
        bool previousWindowsScale = (bool)windowsScale.GetValue(null)!;
        windowsScale.SetValue(null, false);
        float previousScale = (float)fontScale.GetValue(null)!;
        string output = Path.Combine(Path.GetFullPath(root), "tray-layout");
        Directory.CreateDirectory(output);
        try
        {
            var failures = new List<Exception>();
            foreach (string language in new[] { "English", "Danish" })
            foreach (bool dark in new[] { true, false })
            foreach (float scale in new[] { 1f, 1.5f, 2.25f })
            {
                try { CheckVariant(language, dark, scale); }
                catch (Exception ex)
                {
                    failures.Add(ex);
                    Console.Error.WriteLine(ex.Message);
                }
            }
            if (failures.Count > 0) throw new AggregateException("Constrained tray layout checks failed.", failures);
        }
        finally
        {
            fontScale.SetValue(null, previousScale);
            windowsScale.SetValue(null, previousWindowsScale);
        }

        void CheckVariant(string language, bool dark, float scale)
        {
            string variant = $"{language.ToLowerInvariant()}-{(dark ? "dark" : "light")}-{scale:0.##}";
            Type type = assembly.GetType("QuickZoom.TrayContext", true)!;
            using var context = (IDisposable)type.GetConstructors()[0].Invoke([null, true, false, Keys.None]);
            object? Call(string name, params object?[] values) => type.GetMethod(name, Instance)!.Invoke(context, values);
            T Get<T>(string name) => (T)type.GetField(name, Instance)!.GetValue(context)!;
            void Set(string name, object value) => type.GetField(name, Instance)!.SetValue(context, value);
            void SetEnum(string name, string value) => Set(name, Enum.Parse(type.GetField(name, Instance)!.FieldType, value));
            SetEnum("_language", language);
            SetEnum("_themeMode", dark ? "Dark" : "Light");
            SetEnum("_uiFontSize", "Default");
            Set("_useDarkTheme", dark);
            Set("_followCursor", true);
            Set("_zoomPercent", 100);
            Set("_invertColors", false);
            Call("ApplyUiFontScale");
            fontScale.SetValue(null, scale);
            Rectangle screen = SystemInformation.VirtualScreen;
            Point origin = new(screen.Right + 100, screen.Bottom + 100);
            Rectangle roomy = new(origin, new Size(4096, 3072));
            try
            {
                Call("ShowTrayPopup", new Point(screen.Right - 24, screen.Bottom - 24), false);
                Form popup = Get<Form>("_trayPopup");
                MethodInfo layout = popup.GetType().GetMethod("LayoutAnchored", Instance)!;
                Panel viewport = (Panel)popup.GetType().GetField("_scrollHost", Instance)!.GetValue(popup)!;
                MethodInfo captureWindow = type.GetMethod("CaptureWindow", Static)!;
                MethodInfo cloak = assembly.GetType("QuickZoom.WindowChrome", true)!.GetMethod("TrySetCloaked", Static)!;
                if (!(bool)cloak.Invoke(null, [popup, true])!) throw new Exception("Could not cloak the off-screen tray test window.");
                bool representative = language == "English" && dark && scale == 1f || language == "Danish" && !dark && scale == 2.25f;
                Arrange(roomy);
                Size originalSize = popup.Size;
                var originalRows = Descendants(popup).Where(IsAction).ToDictionary(control => control,
                    control => (control.Size, FontSizes: control.Controls.OfType<Label>().Select(label => label.Font.SizeInPoints).ToArray()));
                if (representative) Capture("roomy");
                foreach (Size size in new[] { new Size(800, 560), new Size(1024, 728), new Size(1280, 680) })
                {
                    Rectangle area = new(origin, size);
                    Arrange(area);
                    Verify(area, $"{size.Width}x{size.Height}");
                    Size compactSize = popup.Size;
                    float contentScale = (float)popup.GetType().GetProperty("ContentScale", Instance)!.GetValue(popup)!;
                    for (int repeat = 0; repeat < 4; repeat++) Arrange(area);
                    Check(popup.Size == compactSize &&
                        (float)popup.GetType().GetProperty("ContentScale", Instance)!.GetValue(popup)! == contentScale,
                        "repeated layout retains identical size", variant);
                    if (representative && size.Width == 800) Capture("800x560");
                    foreach (string mode in new[] { "MouseOnly", "KeyboardAndTyping", "Automatic" })
                    {
                        SetEnum("_trackingMode", mode);
                        Call("UpdateFollowingUi");
                        Arrange(area);
                        Verify(area, mode);
                    }
                    Set("_followCursor", false);
                    Call("UpdateFollowingUi");
                    Arrange(area);
                    Verify(area, "following paused");
                    Set("_followCursor", true);
                    Call("UpdateFollowingUi");
                    Call("ToggleDisplayOptions");
                    Arrange(area);
                    Check(Get<Control>("_displayOptionsHost").Visible, "display choices expanded", variant);
                    Verify(area, "display choices expanded");
                    Call("PopulateDisplayOptionsHost");
                    Arrange(area);
                    Verify(area, "display choices rebuilt while compact");
                    Call("ToggleDisplayOptions");
                    Arrange(area);
                    Verify(area, "display choices collapsed");
                    Arrange(roomy);
                    Check(popup.Size == originalSize && originalRows.All(entry =>
                        entry.Key.Size == entry.Value.Size && entry.Key.Controls.OfType<Label>()
                            .Select(label => label.Font.SizeInPoints).SequenceEqual(entry.Value.FontSizes)),
                        "a larger work area restores original tray and text dimensions", variant);
                }
                Check((float)fontScale.GetValue(null)! == scale, "fitting leaves global UI text scale unchanged", variant);
                if (language == "English" && dark && scale == 1f && Application.HighDpiMode == HighDpiMode.PerMonitorV2)
                    CheckDpiChanges();
                Console.WriteLine("PASS: tray fits constrained work areas without clipping or scrollbars; stable refresh and expansion: " + variant);

                void Arrange(Rectangle area)
                {
                    layout.Invoke(popup, [new Point(area.Right - 12, area.Bottom - 12), true, area]);
                    Application.DoEvents();
                    layout.Invoke(popup, [new Point(area.Right - 12, area.Bottom - 12), true, area]);
                }

                void Verify(Rectangle area, string state)
                {
                    string contextName = variant + "/" + state;
                    Check(area.Contains(popup.Bounds), "tray bounds fit working area", contextName);
                    Check(!viewport.AutoScroll && !viewport.VerticalScroll.Visible && !viewport.HorizontalScroll.Visible &&
                        (GetWindowLongPtr(viewport.Handle, -16).ToInt64() & 0x00300000L) == 0,
                        "no native scrollbar style or scroll range", contextName);
                    Rectangle visible = viewport.RectangleToScreen(viewport.ClientRectangle);
                    Control[] actions = Descendants(popup).Where(control => control.Visible && IsAction(control)).ToArray();
                    Check(actions.Length >= 12 && actions.Contains(Get<Control>("_exitRow")), "all main tray actions remain available", contextName);
                    foreach (Control action in actions)
                    {
                        Check(visible.Contains(action.RectangleToScreen(action.ClientRectangle)),
                            "complete action remains inside viewport: " + action.AccessibleName, contextName);
                        foreach (Label label in action.Controls.OfType<Label>().Where(label => label.Visible))
                        {
                            Size text = TextRenderer.MeasureText(label.Text, label.Font, Size.Empty,
                                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                            Check(action.ClientRectangle.Contains(label.Bounds) && label.Width >= text.Width && label.Height >= text.Height,
                                $"complete label fits: {label.Text} ({label.Size}; text {text})", contextName);
                        }
                        if (action.GetType().Name == "TrayModeButton")
                        {
                            Rectangle textBounds = (Rectangle)action.GetType().GetField("_labelBounds", Instance)!.GetValue(action)!;
                            Font font = (Font)action.GetType().GetField("_labelFont", Instance)!.GetValue(action)!;
                            Size text = TextRenderer.MeasureText(action.AccessibleName, font, Size.Empty,
                                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                            bool fits = action.ClientRectangle.Contains(textBounds) && textBounds.Width >= text.Width && textBounds.Height >= text.Height;
                            if (!fits) Capture("label-clipping");
                            Check(fits, $"complete zoom-mode label fits: {action.AccessibleName} (button {action.Size}, label {textBounds}; text {text})", contextName);
                        }
                    }
                    Check(!Screen.AllScreens.Any(monitor => monitor.Bounds.IntersectsWith(popup.Bounds)),
                        "test tray stays off-screen", contextName);
                }

                void Capture(string name)
                {
                    if (capture) captureWindow.Invoke(null, [popup, Path.Combine(output, variant + "-" + name + ".png"), null, null]);
                }

                void CheckDpiChanges()
                {
                    int initialDpi = popup.DeviceDpi;
                    foreach (int dpi in new[] { 144, 192, initialDpi })
                    {
                        var bounds = new NativeRectangle { Left = popup.Left, Top = popup.Top, Right = popup.Right, Bottom = popup.Bottom };
                        IntPtr address = Marshal.AllocHGlobal(Marshal.SizeOf<NativeRectangle>());
                        try
                        {
                            Marshal.StructureToPtr(bounds, address, false);
                            _ = SendMessage(popup.Handle, 0x02E0, new IntPtr(dpi | dpi << 16), address);
                        }
                        finally { Marshal.FreeHGlobal(address); }
                        Rectangle area = new(origin, new Size(1024, 728));
                        Arrange(area);
                        Check(popup.DeviceDpi == dpi, "native DPI change updates the tray DPI", variant);
                        Verify(area, "DPI " + dpi);
                    }
                    Console.WriteLine("PASS: off-screen tray remains fitted through native DPI-change messages.");
                }
            }
            finally { Call("CloseTrayPopup"); Set("_runtimeStopped", true); }
        }
    }

    private static bool IsAction(Control control) => control.GetType().Name is "TrayMenuRow" or "TrayModeButton";

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (Control descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void Check(bool condition, string name, string variant)
    {
        if (!condition) throw new Exception("FAIL: " + name + " [" + variant + "]");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle { internal int Left, Top, Right, Bottom; }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
}
