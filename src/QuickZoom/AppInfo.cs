using System.Diagnostics;

namespace QuickZoom;

internal static class AppInfo
{
    internal const string ReleaseVersion = "2.2.0";
    internal const int BuildNumber = 230;
    internal const string ProductVersion = "2.2.0.230";
    private static string? _versionHash;

    internal static string DisplayVersion => $"Version {ReleaseVersion}, Build {BuildNumber}";
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
