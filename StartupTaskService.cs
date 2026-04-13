using System;
using System.Diagnostics;
using System.Threading;

namespace QuickZoom;

internal enum StartupTaskStatus
{
    Ready,
    Missing,
    Unknown
}

internal static class StartupTaskService
{
    internal const string ElevatedStartupTaskName = "QuickZoom Startup (Elevated)";

    internal static StartupTaskStatus GetStatus()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = "/Query /TN \"" + ElevatedStartupTaskName + "\"",
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
                return StartupTaskStatus.Unknown;
            }

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit(3000);

            if (process.ExitCode == 0)
            {
                return StartupTaskStatus.Ready;
            }

            string combined = (output + "\n" + error).Trim();
            return combined.IndexOf("cannot find", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   combined.IndexOf("kan ikke finde", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   combined.IndexOf("cannot find the file", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   combined.IndexOf("ERROR:", StringComparison.OrdinalIgnoreCase) >= 0
                ? StartupTaskStatus.Missing
                : StartupTaskStatus.Unknown;
        }
        catch
        {
            return StartupTaskStatus.Unknown;
        }
    }

    internal static bool WaitUntilReady(int timeoutMs = 10000, int pollMs = 500)
    {
        int elapsed = 0;
        while (elapsed <= timeoutMs)
        {
            if (GetStatus() == StartupTaskStatus.Ready)
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
            _ => UiText.Get(language, "Tray.StartupUnknown")
        };
    }
}
