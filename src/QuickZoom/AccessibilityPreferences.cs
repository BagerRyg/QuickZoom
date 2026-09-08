using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace QuickZoom;

internal static class AccessibilityPreferences
{
    private const uint SpiGetClientAreaAnimation = 0x1042;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, out bool pvParam, uint fWinIni);

    internal static bool HighContrast => SystemInformation.HighContrast;

    internal static bool AnimationsEnabled
    {
        get
        {
            if (HighContrast)
            {
                return false;
            }

            try
            {
                return !SystemParametersInfo(SpiGetClientAreaAnimation, 0, out bool enabled, 0) || enabled;
            }
            catch
            {
                return true;
            }
        }
    }

    internal static float WindowsTextScale
    {
        get
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Accessibility");
                object? value = key?.GetValue("TextScaleFactor");
                int percent = value switch
                {
                    int intValue => intValue,
                    long longValue => (int)longValue,
                    string text when int.TryParse(text, out int parsed) => parsed,
                    _ => 100
                };
                return Math.Clamp(percent, 100, 225) / 100f;
            }
            catch
            {
                return 1f;
            }
        }
    }
}
