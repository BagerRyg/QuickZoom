using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

internal static class StartupTaskService
{
    private const string ElevatedLaunchFlag = "--quickzoom-elevated";
    private static readonly object CacheSync = new();
    private static StartupTaskInfo? _cachedInfo;
    private static DateTime _cachedInfoAtUtc;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);

    internal const string ElevatedStartupTaskName = "QuickZoom Startup (Elevated)";

    private sealed class StartupTaskInfo
    {
        public StartupTaskStatus Status { get; init; }
        public string? ExecutePath { get; init; }
        public string? Arguments { get; init; }
        public string? Details { get; init; }
    }

    internal static StartupTaskStatus GetStatus() => GetStatusInfo().Status;

    internal static void InvalidateCache()
    {
        lock (CacheSync)
        {
            _cachedInfo = null;
            _cachedInfoAtUtc = DateTime.MinValue;
        }
    }

    internal static bool WaitUntilReady(int timeoutMs = 10000, int pollMs = 500)
    {
        int elapsed = 0;
        while (elapsed <= timeoutMs)
        {
            if (GetStatusInfo(forceRefresh: true).Status == StartupTaskStatus.Ready)
            {
                return true;
            }

            Thread.Sleep(pollMs);
            elapsed += pollMs;
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

    private static StartupTaskInfo GetStatusInfo(bool forceRefresh = false)
    {
        lock (CacheSync)
        {
            if (!forceRefresh &&
                _cachedInfo != null &&
                (DateTime.UtcNow - _cachedInfoAtUtc) < CacheDuration)
            {
                return _cachedInfo;
            }

            StartupTaskInfo info = QueryStatusInfo();
            _cachedInfo = info;
            _cachedInfoAtUtc = DateTime.UtcNow;
            return info;
        }
    }

    private static StartupTaskInfo QueryStatusInfo()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = "/Query /TN \"" + ElevatedStartupTaskName + "\" /XML",
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

            if (string.IsNullOrWhiteSpace(executePath))
            {
                return new StartupTaskInfo
                {
                    Status = StartupTaskStatus.Broken,
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
                    Details = "The startup task does not launch QuickZoom with the expected elevated flag."
                };
            }

            return new StartupTaskInfo
            {
                Status = StartupTaskStatus.Ready,
                ExecutePath = executePath,
                Arguments = arguments
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
               combined.IndexOf("sti blev ikke fundet", StringComparison.OrdinalIgnoreCase) >= 0 ||
               combined.IndexOf("taskname", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }
}
