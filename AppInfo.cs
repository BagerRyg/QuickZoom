using System.Diagnostics;

namespace QuickZoom;

internal static class AppInfo
{
    internal const int MajorVersion = 2;
    internal const int BuildNumber = 203;
    internal const string ProductVersion = "2.0.203.0";
    private static string? _versionHash;

    internal static string DisplayVersion => $"Version {MajorVersion}, Build {BuildNumber}";
    internal static string VersionHash => _versionHash ??= CreateVersionHash();

    private static string CreateVersionHash()
    {
        try
        {
            string? productVersion = FileVersionInfo.GetVersionInfo(Environment.ProcessPath ?? string.Empty).ProductVersion;
            return string.IsNullOrWhiteSpace(productVersion) ? ProductVersion : productVersion;
        }
        catch
        {
            return ProductVersion;
        }
    }
}
