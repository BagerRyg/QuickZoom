using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Xml.Linq;

namespace QuickZoom;

internal enum StartupTaskStatus
{
    Ready,
    Missing,
    Broken,
    Unknown
}

internal sealed class StartupTaskInfo
{
    public StartupTaskStatus Status { get; init; }
    public string? ExecutePath { get; init; }
    public string? Arguments { get; init; }
    public string? UserId { get; init; }
    public string? Details { get; init; }
}

internal static class StartupTaskService
{
    private const string ElevatedLaunchFlag = "--quickzoom-elevated";
    private static readonly object CacheSync = new();
    private static StartupTaskInfo? _cachedInfo;
    private static DateTime _cachedInfoAtUtc;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);

    internal const string ElevatedStartupTaskName = "QuickZoom Startup (Elevated)";

    internal static StartupTaskStatus GetStatus() => GetStatusInfo().Status;

    internal static StartupTaskInfo GetStatusInfo(bool forceRefresh = false)
    {
        lock (CacheSync)
        {
            if (!forceRefresh &&
                _cachedInfo != null &&
                (DateTime.UtcNow - _cachedInfoAtUtc) < CacheDuration)
            {
                return _cachedInfo;
            }

            StartupTaskInfo info = QueryStatusInfo(ElevatedStartupTaskName);
            _cachedInfo = info;
            _cachedInfoAtUtc = DateTime.UtcNow;
            return info;
        }
    }

    internal static bool IsReadyForCurrentBuild(out string? executePath)
    {
        StartupTaskInfo info = GetStatusInfo(forceRefresh: true);
        executePath = info.ExecutePath;
        return info.Status == StartupTaskStatus.Ready &&
               !string.IsNullOrWhiteSpace(info.ExecutePath) &&
               GetExecutableBuildNumber(info.ExecutePath) >= AppInfo.BuildNumber;
    }

    internal static bool IsReadyForCurrentBuild(string expectedExePath, string? expectedUser, out StartupTaskInfo info)
    {
        info = GetStatusInfo(forceRefresh: true);
        return info.Status == StartupTaskStatus.Ready &&
               !string.IsNullOrWhiteSpace(info.ExecutePath) &&
               PathsEqual(info.ExecutePath, expectedExePath) &&
               UserMatches(info.UserId, expectedUser) &&
               GetExecutableBuildNumber(info.ExecutePath) >= AppInfo.BuildNumber;
    }

    internal static StartupTaskInfo QueryTask(string taskName)
    {
        return QueryStatusInfo(taskName);
    }

    internal static void InvalidateCache()
    {
        lock (CacheSync)
        {
            _cachedInfo = null;
            _cachedInfoAtUtc = DateTime.MinValue;
        }
    }

    internal static bool WaitUntilReady(string? expectedExePath = null, string? expectedUser = null, int timeoutMs = 10000, int pollMs = 500)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds <= timeoutMs)
        {
            StartupTaskInfo info = GetStatusInfo(forceRefresh: true);
            bool ready = info.Status == StartupTaskStatus.Ready;
            if (!string.IsNullOrWhiteSpace(expectedExePath))
            {
                ready = ready &&
                    !string.IsNullOrWhiteSpace(info.ExecutePath) &&
                    PathsEqual(info.ExecutePath, expectedExePath);
            }

            if (!string.IsNullOrWhiteSpace(expectedUser))
            {
                ready = ready && UserMatches(info.UserId, expectedUser);
            }

            if (ready)
            {
                return true;
            }

            Thread.Sleep(pollMs);
        }

        return false;
    }

    internal static string GetStatusLabel(UiLanguage language)
    {
        return GetStatus() switch
        {
            StartupTaskStatus.Ready => UiText.Get(language, "Tray.StartupConfigured"),
            StartupTaskStatus.Missing => UiText.Get(language, "Tray.StartupMissing"),
            StartupTaskStatus.Broken => UiText.Get(language, "Tray.StartupBroken"),
            _ => UiText.Get(language, "Tray.StartupUnknown")
        };
    }

    private static StartupTaskInfo QueryStatusInfo(string taskName)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = "/Query /TN \"" + taskName + "\" /XML",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return new StartupTaskInfo
                {
                    Status = StartupTaskStatus.Unknown,
                    Details = "Could not start schtasks.exe."
                };
            }

            if (!process.WaitForExit(4000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort.
                }

                ErrorLog.Write("StartupTaskService", "Timed out while querying the scheduled startup task.");
                return new StartupTaskInfo
                {
                    Status = StartupTaskStatus.Unknown,
                    Details = "The startup task query timed out."
                };
            }

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            if (process.ExitCode == 0)
            {
                return ParseTaskXml(output);
            }

            string combined = (output + Environment.NewLine + error).Trim();
            if (LooksLikeMissingTask(combined))
            {
                return new StartupTaskInfo
                {
                    Status = StartupTaskStatus.Missing,
                    Details = combined
                };
            }

            ErrorLog.Write("StartupTaskService", "Unexpected startup task query failure: " + combined);
            return new StartupTaskInfo
            {
                Status = StartupTaskStatus.Unknown,
                Details = combined
            };
        }
        catch (Exception ex)
        {
            ErrorLog.Write("StartupTaskService", ex);
            return new StartupTaskInfo
            {
                Status = StartupTaskStatus.Unknown,
                Details = ex.Message
            };
        }
    }

    private static StartupTaskInfo ParseTaskXml(string xml)
    {
        try
        {
            XDocument document = XDocument.Parse(xml);
            string? executePath = document.Descendants().FirstOrDefault(node => node.Name.LocalName == "Command")?.Value?.Trim();
            string? arguments = document.Descendants().FirstOrDefault(node => node.Name.LocalName == "Arguments")?.Value?.Trim();
            string? userId = document.Descendants().FirstOrDefault(node => node.Name.LocalName == "UserId")?.Value?.Trim();

            if (string.IsNullOrWhiteSpace(executePath))
            {
                return new StartupTaskInfo
                {
                    Status = StartupTaskStatus.Broken,
                    UserId = userId,
                    Details = "The startup task has no executable configured."
                };
            }

            executePath = Environment.ExpandEnvironmentVariables(executePath);
            if (!Path.IsPathRooted(executePath))
            {
                executePath = Path.GetFullPath(executePath);
            }

            if (!File.Exists(executePath))
            {
                return new StartupTaskInfo
                {
                    Status = StartupTaskStatus.Broken,
                    ExecutePath = executePath,
                    Arguments = arguments,
                    UserId = userId,
                    Details = "The startup task points to an executable that no longer exists."
                };
            }

            string? currentInstalledExePath = InstalledAppService.GetCurrentInstalledExecutablePath();
            if (!string.IsNullOrWhiteSpace(currentInstalledExePath) &&
                !PathsEqual(executePath, currentInstalledExePath))
            {
                return new StartupTaskInfo
                {
                    Status = StartupTaskStatus.Broken,
                    ExecutePath = executePath,
                    Arguments = arguments,
                    UserId = userId,
                    Details = "The startup task points to an older QuickZoom install instead of the current managed build."
                };
            }

            if (string.IsNullOrWhiteSpace(arguments) ||
                arguments.IndexOf(ElevatedLaunchFlag, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return new StartupTaskInfo
                {
                    Status = StartupTaskStatus.Broken,
                    ExecutePath = executePath,
                    Arguments = arguments,
                    UserId = userId,
                    Details = "The startup task does not launch QuickZoom with the expected elevated flag."
                };
            }

            return new StartupTaskInfo
            {
                Status = StartupTaskStatus.Ready,
                ExecutePath = executePath,
                Arguments = arguments,
                UserId = userId
            };
        }
        catch (Exception ex)
        {
            ErrorLog.Write("StartupTaskService", ex);
            return new StartupTaskInfo
            {
                Status = StartupTaskStatus.Unknown,
                Details = ex.Message
            };
        }
    }

    private static bool LooksLikeMissingTask(string combined)
    {
        return combined.IndexOf("cannot find", StringComparison.OrdinalIgnoreCase) >= 0 ||
               combined.IndexOf("cannot find the file", StringComparison.OrdinalIgnoreCase) >= 0 ||
               combined.IndexOf("cannot find the path", StringComparison.OrdinalIgnoreCase) >= 0 ||
               combined.IndexOf("the system cannot find the path specified", StringComparison.OrdinalIgnoreCase) >= 0 ||
               combined.IndexOf("kan ikke finde", StringComparison.OrdinalIgnoreCase) >= 0 ||
               combined.IndexOf("den angivne fil blev ikke fundet", StringComparison.OrdinalIgnoreCase) >= 0 ||
               combined.IndexOf("sti blev ikke fundet", StringComparison.OrdinalIgnoreCase) >= 0 ||
               combined.IndexOf("opgaven findes ikke", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool UserMatches(string? taskUser, string? expectedUser)
    {
        if (string.IsNullOrWhiteSpace(expectedUser))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(taskUser))
        {
            return false;
        }

        string normalizedTaskUser = NormalizeUserName(taskUser);
        string normalizedExpectedUser = NormalizeUserName(expectedUser);
        if (string.Equals(normalizedTaskUser, normalizedExpectedUser, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        SecurityIdentifier? taskSid = TryResolveSid(normalizedTaskUser);
        SecurityIdentifier? expectedSid = TryResolveSid(normalizedExpectedUser) ??
                                          GetCurrentUserSidIfMatches(normalizedExpectedUser);
        return taskSid != null && expectedSid != null && taskSid.Equals(expectedSid);
    }

    private static SecurityIdentifier? GetCurrentUserSidIfMatches(string expectedUser)
    {
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            string currentUser = NormalizeUserName(identity.Name);
            if (!string.Equals(currentUser, expectedUser, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return identity.User;
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeUserName(string userName)
    {
        return userName.Trim().Replace('/', '\\');
    }

    private static SecurityIdentifier? TryResolveSid(string userName)
    {
        try
        {
            if (userName.StartsWith("S-1-", StringComparison.OrdinalIgnoreCase))
            {
                return new SecurityIdentifier(userName);
            }

            return new NTAccount(userName).Translate(typeof(SecurityIdentifier)) as SecurityIdentifier;
        }
        catch
        {
            return null;
        }
    }

    private static int GetExecutableBuildNumber(string exePath)
    {
        try
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(exePath);
            return Math.Max(info.FileBuildPart, info.FilePrivatePart);
        }
        catch
        {
            return 0;
        }
    }
}
