using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace QuickZoom;

internal static class Program
{
    private const string SingleInstanceMutexName = @"Local\QuickZoom2.SingleInstance";
    private static Mutex? _singleInstanceMutex;
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
        string? exePath = GetExecutablePath();
        bool isElevatedLaunch = HasArg(args, ElevatedFlag);
        bool shouldInstallStartupTask = HasArg(args, StartupTaskInstallFlag);
        bool acquiredMutex = false;

        if (!shouldInstallStartupTask)
        {
            if (!TryAcquireSingleInstanceMutex())
            {
                ErrorLog.Write("Startup", "Another QuickZoom instance is already running. Exiting duplicate launch.");
                return;
            }

            acquiredMutex = true;
        }

        EnablePerMonitorDpiAwareness();
        try { Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => LogFatalException("UI thread", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogFatalException("AppDomain", e.ExceptionObject as Exception);
        ErrorLog.Write("Startup", $"Launching {AppInfo.DisplayVersion} from {AppContext.BaseDirectory}");

        bool isAdmin = IsRunningAsAdministrator();
        bool isManagedInstall = InstalledAppService.IsManagedInstallPath(exePath);
        bool needsSecureInstallMigration = InstalledAppService.NeedsSecureInstallMigration(exePath);

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

            shouldInstallStartupTask = false;
            isElevatedLaunch = true;

            if (!PathsEqual(exePath, installedExePath) && TryLaunchInstalledCopy(installedExePath, ElevatedFlag))
            {
                return;
            }

            if (!acquiredMutex)
            {
                if (!TryAcquireSingleInstanceMutex())
                {
                    ErrorLog.Write("Startup", "The elevated startup-service helper finished setup, but another QuickZoom instance was already active. Exiting helper process.");
                    return;
                }

                acquiredMutex = true;
            }
        }

        if (isAdmin && isManagedInstall && needsSecureInstallMigration && !shouldInstallStartupTask)
        {
            if (TryInstallElevatedScheduledTask(out string migratedExePath, out string? migrationError))
            {
                ErrorLog.Write("StartupMigration", "Migrated elevated startup payload to secured install path: " + migratedExePath);
            }
            else
            {
                ErrorLog.Write("StartupMigration", "Could not migrate the legacy startup install to the secured install path. " + (migrationError ?? string.Empty));
            }
        }

        if (ShouldYieldToNewerInstance(exePath))
        {
            return;
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
                StartupTaskStatus startupTaskStatus = StartupTaskService.GetStatus();
                if (startupTaskStatus == StartupTaskStatus.Ready && TryStartElevatedScheduledTask())
                {
                    return;
                }

                bool wantsStartupTaskSetup = PromptToInstallPermanentStartupCopy(startupTaskStatus is StartupTaskStatus.Ready or StartupTaskStatus.Broken);
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
        try
        {
            Application.Run(new TrayContext());
        }
        finally
        {
            try
            {
                _singleInstanceMutex?.ReleaseMutex();
            }
            catch
            {
                // Ignore shutdown races.
            }

            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;
        }
    }

    private static bool TryAcquireSingleInstanceMutex()
    {
        try
        {
            bool createdNew;
            var mutex = new Mutex(initiallyOwned: true, name: SingleInstanceMutexName, createdNew: out createdNew);
            _singleInstanceMutex = mutex;
            if (!createdNew)
            {
                mutex.Dispose();
                _singleInstanceMutex = null;
                return false;
            }

            return true;
        }
        catch
        {
            // If mutex creation fails, do not block startup.
            ErrorLog.Write("Startup", "Could not create the single-instance mutex. Continuing without duplicate-instance protection.");
            return true;
        }
    }

    private static bool IsRunningAsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Elevation", ex);
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
        catch (Exception ex)
        {
            ErrorLog.Write("Elevation", ex);
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
        catch (Exception ex)
        {
            errorMessage = "QuickZoom could not determine the current Windows user.";
            ErrorLog.Write("StartupTaskInstall", ex);
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
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                errorMessage = "QuickZoom could not start PowerShell to register the startup task.";
                return false;
            }

            if (!process.WaitForExit(8000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort.
                }

                errorMessage = "PowerShell timed out while registering the elevated startup task.";
                ErrorLog.Write("StartupTaskInstall", errorMessage);
                return false;
            }

            string output = process.StandardOutput.ReadToEnd().Trim();
            string error = process.StandardError.ReadToEnd().Trim();
            bool success = process.ExitCode == 0;
            if (!success)
            {
                errorMessage = string.IsNullOrWhiteSpace(error)
                    ? "PowerShell failed while registering the elevated startup task."
                    : error;
                ErrorLog.Write("StartupTaskInstall", "Task registration failed. StdOut: " + output + " StdErr: " + error);
                return false;
            }

            StartupTaskService.InvalidateCache();
            return success;
        }
        catch (Exception ex)
        {
            errorMessage = "QuickZoom encountered an unexpected error while registering the startup task.";
            ErrorLog.Write("StartupTaskInstall", ex);
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
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            if (!process.WaitForExit(3000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort.
                }

                ErrorLog.Write("StartupTaskRun", "Timed out while starting the elevated scheduled task.");
                return false;
            }

            string output = process.StandardOutput.ReadToEnd().Trim();
            string error = process.StandardError.ReadToEnd().Trim();
            bool success = process.ExitCode == 0;
            if (!success)
            {
                ErrorLog.Write("StartupTaskRun", "Could not start the elevated scheduled task. StdOut: " + output + " StdErr: " + error);
            }

            return success;
        }
        catch (Exception ex)
        {
            ErrorLog.Write("StartupTaskRun", ex);
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

    private static bool TryLaunchInstalledCopy(string installedExePath, params string[] extraFlags)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = installedExePath,
                UseShellExecute = false,
                Arguments = BuildArguments(Array.Empty<string>(), extraFlags)
            };

            using Process? process = Process.Start(startInfo);
            bool success = process != null;
            if (!success)
            {
                ErrorLog.Write("Startup", "Could not launch the installed QuickZoom copy after startup-service setup.");
            }

            return success;
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Startup", ex);
            return false;
        }
    }

    private static bool ShouldYieldToNewerInstance(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return false;
        }

        string currentExePath = Path.GetFullPath(exePath);
        string? currentInstalledExePath = InstalledAppService.GetCurrentInstalledExecutablePath();
        bool currentIsInstalledPreferred = InstalledAppService.IsCurrentInstalledExecutablePath(currentExePath);
        DateTime currentWriteTimeUtc = TryGetExecutableWriteTimeUtc(currentExePath);
        Process currentProcess = Process.GetCurrentProcess();

        foreach (Process otherProcess in Process.GetProcessesByName(currentProcess.ProcessName))
        {
            using (otherProcess)
            {
                if (otherProcess.Id == currentProcess.Id || otherProcess.SessionId != currentProcess.SessionId)
                {
                    continue;
                }

                string? otherExePath = TryGetProcessExecutablePath(otherProcess);
                if (string.IsNullOrWhiteSpace(otherExePath))
                {
                    continue;
                }

                bool otherIsInstalledPreferred = InstalledAppService.IsCurrentInstalledExecutablePath(otherExePath);
                InstancePreference preference = CompareInstancePreference(
                    currentExePath,
                    currentWriteTimeUtc,
                    currentIsInstalledPreferred,
                    currentProcess,
                    otherExePath,
                    otherIsInstalledPreferred,
                    otherProcess);

                if (preference == InstancePreference.OtherWins)
                {
                    ErrorLog.Write("Startup", "Yielding to a newer or preferred QuickZoom instance at " + otherExePath);
                    return true;
                }

                if (preference == InstancePreference.CurrentWins)
                {
                    TryTerminateOlderQuickZoom(otherProcess, otherExePath, currentInstalledExePath);
                }
            }
        }

        return false;
    }

    private enum InstancePreference
    {
        Undetermined,
        CurrentWins,
        OtherWins
    }

    private static InstancePreference CompareInstancePreference(
        string currentExePath,
        DateTime currentWriteTimeUtc,
        bool currentIsInstalledPreferred,
        Process currentProcess,
        string otherExePath,
        bool otherIsInstalledPreferred,
        Process otherProcess)
    {
        if (currentIsInstalledPreferred != otherIsInstalledPreferred)
        {
            return currentIsInstalledPreferred ? InstancePreference.CurrentWins : InstancePreference.OtherWins;
        }

        DateTime otherWriteTimeUtc = TryGetExecutableWriteTimeUtc(otherExePath);
        if (currentWriteTimeUtc != DateTime.MinValue &&
            otherWriteTimeUtc != DateTime.MinValue &&
            currentWriteTimeUtc != otherWriteTimeUtc)
        {
            return currentWriteTimeUtc > otherWriteTimeUtc
                ? InstancePreference.CurrentWins
                : InstancePreference.OtherWins;
        }

        if (PathsEqual(currentExePath, otherExePath))
        {
            return currentProcess.StartTime <= otherProcess.StartTime
                ? InstancePreference.CurrentWins
                : InstancePreference.OtherWins;
        }

        try
        {
            return currentProcess.StartTime <= otherProcess.StartTime
                ? InstancePreference.CurrentWins
                : InstancePreference.OtherWins;
        }
        catch
        {
            return InstancePreference.Undetermined;
        }
    }

    private static void TryTerminateOlderQuickZoom(Process otherProcess, string otherExePath, string? currentInstalledExePath)
    {
        try
        {
            if (currentInstalledExePath != null && PathsEqual(otherExePath, currentInstalledExePath))
            {
                return;
            }

            ErrorLog.Write("Startup", "Attempting to stop an older QuickZoom instance at " + otherExePath);
            otherProcess.Kill(entireProcessTree: false);
            otherProcess.WaitForExit(2000);
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Startup", "Could not stop older QuickZoom instance at " + otherExePath + ". " + ex.Message);
        }
    }

    private static string? TryGetProcessExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName is string path && !string.IsNullOrWhiteSpace(path)
                ? Path.GetFullPath(path)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static DateTime TryGetExecutableWriteTimeUtc(string exePath)
    {
        try
        {
            return File.GetLastWriteTimeUtc(exePath);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void LogFatalException(string source, Exception? exception)
    {
        ErrorLog.Write(source, exception);
    }
}
