using System;
using Microsoft.Win32;
using System.Windows.Forms;

namespace QuickZoom;

internal sealed partial class TrayContext
{
    private ThemePalette CurrentTheme => _useDarkTheme ? ThemePalettes.Dark : ThemePalettes.Light;

    private void SubscribeThemeChanges()
    {
        try
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }
        catch
        {
            // Best effort.
        }
    }

    private void UnsubscribeThemeChanges()
    {
        try
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        }
        catch
        {
            // Best effort.
        }
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.Color or UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle))
        {
            return;
        }

        if (_menu == null || _menu.IsDisposed)
        {
            return;
        }

        try
        {
            _menu.BeginInvoke((MethodInvoker)(() => ApplyThemeFromSystem(force: false)));
        }
        catch
        {
            // Ignore if menu handle is gone during shutdown.
        }
    }

    private void ApplyThemeFromSystem(bool force)
    {
        bool shouldUseDark = GetWindowsAppsUseDarkMode();
        if (!force && shouldUseDark == _useDarkTheme)
        {
            return;
        }

        _useDarkTheme = shouldUseDark;
        var palette = CurrentTheme;

        if (_menu != null)
        {
            _menu.Renderer = new TrayMenuRenderer(palette);
            _menu.BackColor = palette.MenuBackground;
            _menu.ForeColor = palette.Text;
            ApplyMenuThemeRecursive(_menu.Items, palette);
            _menu.Invalidate();
        }
    }

    private static void ApplyMenuThemeRecursive(ToolStripItemCollection items, ThemePalette palette)
    {
        foreach (ToolStripItem item in items)
        {
            item.BackColor = palette.MenuBackground;
            item.ForeColor = palette.Text;

            if (item is ToolStripMenuItem menuItem)
            {
                ApplyMenuThemeRecursive(menuItem.DropDownItems, palette);
            }
        }
    }

    private static bool GetWindowsAppsUseDarkMode()
    {
        const string personalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        const string valueName = "AppsUseLightTheme";

        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(personalizeKey);
            if (key?.GetValue(valueName) is int intValue)
            {
                return intValue == 0;
            }

            if (key?.GetValue(valueName) is long longValue)
            {
                return longValue == 0;
            }
        }
        catch
        {
            // Fall back to light mode if registry cannot be read.
        }

        return false;
    }
}
