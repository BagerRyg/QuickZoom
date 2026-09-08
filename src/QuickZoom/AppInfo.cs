using System.Diagnostics;

namespace QuickZoom;

internal static class AppInfo
{
    internal const string ReleaseVersion = "3.0";
    internal const int BuildNumber = 333;
    internal const string ProductVersion = "3.0.0.333";
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
