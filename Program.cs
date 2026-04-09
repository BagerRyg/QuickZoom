using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickZoom;

internal static class Program
{
    // Per-monitor v2 gives physical pixel coordinates across mixed-DPI setups.
    private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new(-4);
    private const string StartupTaskInstallFlag = "--install-startup-task";
    private const int StartupTaskPriority = 3;

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiFlag);

    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    private static void EnablePerMonitorDpiAwareness()
    {
        try
        {
            if (!SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
            {
                SetProcessDPIAware();
            }
        }
        catch
        {
            // Best effort.
        }
    }

    private const int ERROR_CANCELLED = 1223;
    private const string ElevatedFlag = "--quickzoom-elevated";

    [STAThread]
    private static void Main(string[] args)
    {
        EnablePerMonitorDpiAwareness();
        try { Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        string? exePath = GetExecutablePath();
        bool isAdmin = IsRunningAsAdministrator();
        bool isElevatedLaunch = HasArg(args, ElevatedFlag);
        bool shouldInstallStartupTask = HasArg(args, StartupTaskInstallFlag);
        bool isManagedInstall = InstalledAppService.IsManagedInstallPath(exePath);

        if (shouldInstallStartupTask)
        {
            if (!isAdmin)
            {
                StartupDialogs.ShowInfo(
                    "QuickZoom",
                    "Administrator permission is needed once",
                    "QuickZoom needs one Administrator approval to create the elevated startup task.");
                return;
            }

            if (!TryInstallElevatedScheduledTask(out string installedExePath, out string? installError))
            {
                StartupDialogs.ShowWarning(
                    "QuickZoom",
                    "Automatic setup did not complete",
                    "QuickZoom could not copy itself into its permanent startup location.\n\n" +
                    (string.IsNullOrWhiteSpace(installError) ? "Please try again." : installError));
            }
            else
            {
                if (!StartupTaskService.WaitUntilReady())
                {
                    StartupDialogs.ShowWarning(
                        "QuickZoom",
                        "Automatic setup did not complete",
                        "QuickZoom copied the app to its permanent startup location, but could not confirm that the elevated startup task is ready yet.\n\n" +
                        "Installed path:\n" + installedExePath);
                }
                else
                {
                    StartupDialogs.ShowTimedSuccess(
                        "QuickZoom",
                        "Startup service created successfully",
                        "QuickZoom was installed to a permanent startup location and will now be able to launch elevated automatically at sign-in without prompting each boot.",
                        15);
                }
            }
        }

        if (!isAdmin && !isElevatedLaunch)
        {
            if (!isManagedInstall && InstalledAppService.ShouldOfferInstallOrUpdate(exePath))
            {
                bool wantsManagedInstall = PromptToInstallPermanentStartupCopy(StartupTaskService.GetStatus() == StartupTaskStatus.Ready);
                if (wantsManagedInstall && TryRelaunchAsAdministrator(args, StartupTaskInstallFlag))
                {
                    return;
                }

                StartupDialogs.ShowWarning(
                    "QuickZoom",
                    "QuickZoom is running from a temporary location",
                    "QuickZoom will continue in normal user mode from this EXE.\n\n" +
                    "If you want startup support that keeps working after the file is moved or deleted, set up the permanent startup copy.");
            }
            else
            {
                if (TryStartElevatedScheduledTask())
                {
                    return;
                }

                bool wantsStartupTaskSetup = PromptToInstallPermanentStartupCopy(false);
                if (wantsStartupTaskSetup && TryRelaunchAsAdministrator(args, StartupTaskInstallFlag))
                {
                    return;
                }

                StartupDialogs.ShowWarning(
                    "QuickZoom",
                    "QuickZoom is not elevated",
                    wantsStartupTaskSetup
                        ? "QuickZoom could not complete elevated startup setup.\n\n" +
                          "It will continue in normal user mode, but zoom hotkeys may not work while an Administrator app is focused."
                        : "QuickZoom will continue in normal user mode.\n\n" +
                          "Zoom hotkeys may not work while an Administrator app is focused until you set up elevated startup.");
            }
        }
        Application.Run(new TrayContext());
    }

    private static bool IsRunningAsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static bool PromptToInstallPermanentStartupCopy(bool isUpdate)
    {
        return StartupDialogs.ShowYesNo(
            "QuickZoom",
            isUpdate
                ? "Update the installed QuickZoom startup copy?"
                : "Install QuickZoom to a permanent startup location?",
            isUpdate
                ? "QuickZoom will copy this build into its permanent folder and update the startup service to use it.\n\n" +
                  "That keeps startup working even if this downloaded EXE is moved or deleted."
                : "QuickZoom will copy itself into a permanent folder in your user profile and create the elevated startup service there.\n\n" +
                  "That keeps startup working even if this EXE is moved or deleted.");
    }

    private static bool TryRelaunchAsAdministrator(string[] args, params string[] extraFlags)
    {
        string? exePath = GetExecutablePath();
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return false;
        }

        string elevatedArgs = BuildArguments(args, extraFlags);
        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true,
            Verb = "runas",
            Arguments = elevatedArgs
        };

        try
        {
            Process.Start(startInfo);
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ERROR_CANCELLED)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryInstallElevatedScheduledTask(out string installedExePath, out string? errorMessage)
    {
        string? exePath = GetExecutablePath();
        if (string.IsNullOrWhiteSpace(exePath))
        {
            installedExePath = string.Empty;
            errorMessage = "QuickZoom could not determine its current executable path.";
            return false;
        }

        if (!InstalledAppService.TryPrepareInstalledPayload(exePath, out installedExePath, out errorMessage))
        {
            return false;
        }

        string currentUser;
        try
        {
            currentUser = WindowsIdentity.GetCurrent().Name;
        }
        catch
        {
            errorMessage = "QuickZoom could not determine the current Windows user.";
            return false;
        }

        string psExe = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell\\v1.0\\powershell.exe");

        string command =
            "$action = New-ScheduledTaskAction -Execute " + ToPowerShellSingleQuoted(installedExePath) + " -Argument " + ToPowerShellSingleQuoted(ElevatedFlag) + "; " +
            "$trigger = New-ScheduledTaskTrigger -AtLogOn -User " + ToPowerShellSingleQuoted(currentUser) + " -RandomDelay (New-TimeSpan -Seconds 0); " +
            "$principal = New-ScheduledTaskPrincipal -UserId " + ToPowerShellSingleQuoted(currentUser) + " -LogonType Interactive -RunLevel Highest; " +
            "$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -MultipleInstances IgnoreNew -Priority " + StartupTaskPriority.ToString() + "; " +
            "Register-ScheduledTask -TaskName " + ToPowerShellSingleQuoted(StartupTaskService.ElevatedStartupTaskName) +
            " -Description " + ToPowerShellSingleQuoted("Launch QuickZoom at user logon with highest privileges.") +
            " -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null";

        var startInfo = new ProcessStartInfo
        {
            FileName = psExe,
            Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " + QuoteArgument(command),
            CreateNoWindow = true,
            UseShellExecute = false
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                errorMessage = "QuickZoom could not start PowerShell to register the startup task.";
                return false;
            }

            process.WaitForExit(5000);
            bool success = process.ExitCode == 0;
            if (!success)
            {
                errorMessage = "PowerShell failed while registering the elevated startup task.";
            }

            return success;
        }
        catch
        {
            errorMessage = "QuickZoom encountered an unexpected error while registering the startup task.";
            return false;
        }
    }

    private static bool TryStartElevatedScheduledTask()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = "/Run /TN \"" + StartupTaskService.ElevatedStartupTaskName + "\"",
            CreateNoWindow = true,
            UseShellExecute = false
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            process.WaitForExit(1500);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetExecutablePath()
    {
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            exePath = Application.ExecutablePath;
        }

        return string.IsNullOrWhiteSpace(exePath) ? null : exePath;
    }

    private static string BuildArguments(string[] args, params string[] extraFlags)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], ElevatedFlag, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(args[i], StartupTaskInstallFlag, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (sb.Length > 0)
            {
                sb.Append(' ');
            }

            sb.Append(QuoteArgument(args[i]));
        }

        foreach (string flag in extraFlags)
        {
            if (string.IsNullOrWhiteSpace(flag))
            {
                continue;
            }

            if (sb.Length > 0)
            {
                sb.Append(' ');
            }

            sb.Append(flag);
        }

        return sb.ToString();
    }

    private static bool HasArg(string[] args, string value)
    {
        foreach (string arg in args)
        {
            if (string.Equals(arg, value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string QuoteArgument(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        if (!value.Contains(' ') && !value.Contains('"'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static string ToPowerShellSingleQuoted(string value)
    {
        return "'" + value.Replace("'", "''") + "'";
    }
}
