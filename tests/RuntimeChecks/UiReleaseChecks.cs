using System.Drawing;
using System.IO;
using System.Reflection;

internal static class UiReleaseChecks
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static void Run(Assembly assembly, string root)
    {
        Type slider = assembly.GetType("QuickZoom.ModernSlider", true)!;
        MethodInfo calculateBounds = slider.GetMethod("CalculateKnobBounds", BindingFlags.Static | BindingFlags.NonPublic)!;
        int cases = 0;
        foreach (int preferredSize in new[] { 24, 36, 48 })
        foreach (float ratio in new[] { 0f, 0.5f, 1f })
        for (int width = 1; width <= 80; width++)
        {
            var track = new Rectangle(12, 17, width, 6);
            var knob = (Rectangle)calculateBounds.Invoke(null, [track, preferredSize, 60, ratio])!;
            if (knob.Width <= 0 || knob.Width != knob.Height || knob.Left < track.Left || knob.Right > track.Right)
                throw new Exception("A narrow slider produced invalid knob geometry.");
            cases++;
        }
        Console.WriteLine($"PASS: slider knob geometry remains valid on narrow tracks at multiple DPI sizes ({cases} cases); no rendering.");

        object palette = assembly.GetType("QuickZoom.ThemePalettes", true)!
            .GetProperty("Light", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
        CheckResizeLayout(assembly, palette);
        Type dropdownType = assembly.GetType("QuickZoom.ModernDropdown", true)!;
        foreach (bool disposeOwnerBeforeCallback in new[] { false, true })
        {
            using var dropdown = (Control)Activator.CreateInstance(dropdownType, palette)!;
            using var menu = new ContextMenuStrip();
            _ = dropdown.Handle;
            _ = menu.Handle;
            FieldInfo activeMenu = dropdownType.GetField("_activeMenu", Instance)!;
            activeMenu.SetValue(dropdown, menu);
            dropdownType.GetMethod("ScheduleMenuDisposal", Instance)!.Invoke(dropdown, [menu]);
            if (disposeOwnerBeforeCallback) dropdown.Dispose();
            Application.DoEvents();
            if (!menu.IsDisposed || activeMenu.GetValue(dropdown) != null)
                throw new Exception("A closed dropdown menu escaped disposal when its owner was rebuilt.");
        }
        Console.WriteLine("PASS: closed dropdown menus are disposed even when their owner is destroyed before queued cleanup; no menus shown.");

        Type contextType = assembly.GetType("QuickZoom.TrayContext", true)!;
        using var context = (IDisposable)contextType.GetConstructors()[0].Invoke([null, true, false, Keys.None]);
        void Set(string name, object? value) => contextType.GetField(name, Instance)!.SetValue(context, value);
        string path = Path.Combine(root, "ui-release.json");
        Set("_settingsPath", path);
        Set("_legacySettingsPath", path);
        Set("_runtimeStopped", true);
        using var hiddenForm = new VisibilityProbeForm();
        _ = hiddenForm.Handle;
        Set("_settingsWindow", hiddenForm);
        try
        {
            MethodInfo refresh = contextType.GetMethod("RefreshSettingsWindow", Instance)!;
            object page = Enum.Parse(refresh.GetParameters()[0].ParameterType, "Appearance");
            refresh.Invoke(context, [page]);
            if (hiddenForm.ShowRequests != 0)
                throw new Exception("Refreshing hidden settings attempted to reopen the window.");
            Console.WriteLine("PASS: refreshing hidden settings never requests window activation; no windows shown.");
        }
        finally
        {
            Set("_settingsWindow", null);
            Set("_runtimeStopped", false);
        }
    }

    private static void CheckResizeLayout(Assembly assembly, object palette)
    {
        Type hostType = assembly.GetType("QuickZoom.SettingsContentHost", true)!;
        using var host = (Control)Activator.CreateInstance(hostType, palette)!;
        using var form = new ResizeProbeForm { ShowInTaskbar = false, ClientSize = new Size(600, 400) };
        MethodInfo cloak = assembly.GetType("QuickZoom.WindowChrome", true)!.GetMethod("TrySetCloaked", BindingFlags.Static | BindingFlags.NonPublic)!;
        if (!(bool)cloak.Invoke(null, [form, true])!) throw new Exception("Could not hide the resize test window.");
        host.Dock = DockStyle.Fill;
        form.Controls.Add(host);
        using var page = new UserControl();
        var content = new Panel { Dock = DockStyle.Top, Height = 1200 };
        page.Controls.Add(content);
        host.Controls.Add(page);
        form.Show();
        hostType.GetMethod("SetActivePage")!.Invoke(host, [page]);
        Application.DoEvents();
        var scrollBar = host.Controls.OfType<VScrollBar>().Single();
        hostType.GetMethod("RestoreScrollY")!.Invoke(host, [300]);
        int widthChanges = 0;
        int lastWidth = page.Width;
        page.SizeChanged += (_, _) =>
        {
            if (lastWidth != page.Width) widthChanges++;
            lastWidth = page.Width;
        };
        foreach (int width in new[] { 620, 660, 600 })
        {
            widthChanges = 0;
            form.ClientSize = new Size(width, 400);
            Application.DoEvents();
            if (widthChanges != 1 || !scrollBar.Visible || scrollBar.Value != 300 || !host.Visible ||
                page.Right > scrollBar.Left || page.Height < content.Bottom)
                throw new Exception("A scrolling page reflowed through multiple widths, lost its scroll position, or clipped content.");
        }
        content.Height = 100;
        hostType.GetMethod("RefreshActivePageLayout")!.Invoke(host, null);
        if (scrollBar.Visible || page.Width != host.ClientSize.Width || page.Top != 0)
            throw new Exception("A shorter page retained its scrollbar or reserved width.");
        Console.WriteLine("PASS: resizing uses one page width, preserves scrolling, restores painting, and removes unneeded scrollbars; no visible windows.");
    }

    private sealed class ResizeProbeForm : Form
    {
        protected override bool ShowWithoutActivation => true;
    }

    // Keep the existing form available if refresh incorrectly attempts to reopen
    // it, and suppress native visibility even when this regression test fails.
    private sealed class VisibilityProbeForm : Form
    {
        internal int ShowRequests { get; private set; }

        protected override void SetVisibleCore(bool value)
        {
            if (value) ShowRequests++;
            else base.SetVisibleCore(false);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            e.Cancel = true;
            base.OnFormClosing(e);
        }
    }
}
