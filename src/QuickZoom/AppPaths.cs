using System;
using System.IO;

namespace QuickZoom;

internal static class AppPaths
{
    internal static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QuickZoom",
        "settings.json");

    // Do not consult roaming profiles: they may be backed by a remote server.
    internal static string LegacySettingsPath => SettingsPath;

}
