using System;
using Microsoft.Win32;

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

        RunOnUiThread(() => ApplyThemePreference(force: false));
    }

    private void ApplyThemePreference(bool force)
    {
        bool shouldUseDark = _themeMode switch
        {
            ThemeMode.Dark => true,
            ThemeMode.Light => false,
            _ => GetWindowsAppsUseDarkMode()
        };

        if (!force && shouldUseDark == _useDarkTheme)
        {
            return;
        }

        _useDarkTheme = shouldUseDark;
        RefreshMenuAndTrayUi(rebuildPopup: true);
        RefreshSettingsWindow(_currentSettingsPage);
    }

    private void SetThemeMode(ThemeMode mode)
    {
        _themeMode = mode;
        SaveSettings();
        ApplyThemePreference(force: true);
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
