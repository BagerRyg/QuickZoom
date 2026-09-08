using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Win32;

namespace QuickZoom;

internal static class AppThemeBootstrap
{
    internal const int AutoSystem = 0;
    internal const int Dark = 1;
    internal const int Light = 2;

    internal static bool NativeColorModeActive { get; private set; }

    internal static int ReadPersistedThemeMode()
    {
        foreach (string path in new[] { AppPaths.SettingsPath, AppPaths.LegacySettingsPath })
        {
            try
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                if (document.RootElement.TryGetProperty("ThemeMode", out JsonElement value) &&
                    value.ValueKind == JsonValueKind.Number &&
                    value.TryGetInt32(out int themeMode) &&
                    themeMode is >= AutoSystem and <= Light)
                {
                    return themeMode;
                }
            }
            catch (Exception ex)
            {
                ErrorLog.WriteThrottled("ThemeBootstrap.Read", ex);
            }
        }

        return AutoSystem;
    }

    internal static bool ShouldUseDarkPalette(int themeMode)
    {
        return themeMode switch
        {
            Dark => true,
            Light => false,
            _ => GetWindowsAppsUseDarkMode()
        };
    }

    internal static bool TryApplyNativeColorMode(int themeMode)
    {
        try
        {
            SystemColorMode colorMode = themeMode switch
            {
                Dark => SystemColorMode.Dark,
                Light => SystemColorMode.Classic,
                _ => SystemColorMode.System
            };
            Application.SetColorMode(colorMode);
            NativeColorModeActive = true;
            ErrorLog.WriteAlways(
                "ThemeEngine",
                $"Using .NET 10 native colour mode ({colorMode}) with QuickZoom compatibility safeguards.");
            return true;
        }
        catch (Exception ex)
        {
            NativeColorModeActive = false;
            // Manual palettes, DWM title-bar theming, and staged reveal remain
            // active when native color mode is unavailable or rejects a change.
            ErrorLog.WriteThrottled("ThemeBootstrap.NativeColorMode", ex);
            ErrorLog.WriteAlways("ThemeEngine", "Using QuickZoom compatibility fallback.");
            return false;
        }
    }

    private static bool GetWindowsAppsUseDarkMode()
    {
        const string personalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        const string valueName = "AppsUseLightTheme";

        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(personalizeKey);
            return key?.GetValue(valueName) switch
            {
                int intValue => intValue == 0,
                long longValue => longValue == 0,
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }
}
