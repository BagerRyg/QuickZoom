using System;
using System.IO;

namespace QuickZoom;

internal static class AppPaths
{
    internal static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QuickZoom",
        "settings.json");

    internal static string LegacySettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "QuickZoom",
        "settings.json");
}
