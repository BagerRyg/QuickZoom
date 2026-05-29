using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace QuickZoom;

internal static class Program
{
    private const string SingleInstanceMutexName = @"Local\QuickZoom2.SingleInstance";
    private static Mutex? _singleInstanceMutex;
    // Per-monitor v2 gives physical pixel coordinates across mixed-DPI setups.
    private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new(-4);
    private const string StartupTaskInstallFlag = "--install-startup-task";
    private const string StartupReadyEventFlag = "--startup-ready-event";
    private const string StartupTaskUserFlag = "--startup-task-user";
    private const string CaptureUiScreenshotsFlag = "--capture-ui-screenshots";
    private const int StartupTaskPriority = 3;
    private static readonly string[] LegacyStartupTaskNames =
    [
        "QuickZoom Startup",
        "QuickZoom Startup (Legacy)",
        "QuickZoom Elevated Startup",
        "QuickZoom 2 Startup",
        "QuickZoom2 Startup"
    ];
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiFlag);

    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder text, ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

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

    private static UiLanguage StartupLanguage => UiText.GetStartupLanguage();

    private static string T(string key, params object[] args) => UiText.Get(StartupLanguage, key, args);

    [STAThread]
    private static void Main(string[] args)
    {
        string? exePath = GetExecutablePath();
        bool isElevatedLaunch = HasArg(args, ElevatedFlag);
        bool shouldInstallStartupTask = HasArg(args, StartupTaskInstallFlag);
        string? startupReadyEventName = GetArgValue(args, StartupReadyEventFlag);
        string? startupTaskUser = GetArgValue(args, StartupTaskUserFlag);
        bool shouldCaptureUiScreenshots = HasArg(args, CaptureUiScreenshotsFlag);
        bool acquiredMutex = false;

        ConfigureErrorLoggingFromSettings();

        if (!shouldCaptureUiScreenshots && !shouldInstallStartupTask && startupReadyEventName == null)
        {
            if (ReconcileOtherQuickZoomInstances(exePath) == InstanceStartupDecision.ExitCurrent)
            {
                return;
            }

            if (!TryAcquireSingleInstanceMutexWithRetry(exePath))
            {
                ShowLatestAlreadyRunningDialog();
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

        if (shouldCaptureUiScreenshots)
        {
            TrayContext.CaptureUiScreenshots(Path.Combine(Directory.GetCurrentDirectory(), "UI Screenshots", $"Build {AppInfo.BuildNumber}"));
            return;
        }

        bool isAdmin = IsRunningAsAdministrator();
        bool isManagedInstall = InstalledAppService.IsManagedInstallPath(exePath);
        bool needsSecureInstallMigration = InstalledAppService.NeedsSecureInstallMigration(exePath);

        if (!shouldInstallStartupTask)
        {
            TryCleanupLegacyUserStartupEntries(exePath);
            if (isAdmin)
            {
                TryCleanupLegacyScheduledTasks(exePath);
            }
        }

        if (shouldInstallStartupTask)
        {
            if (!isAdmin)
            {
                StartupDialogs.ShowInfo(
                    T("Common.AppName"),
                    T("Startup.AdminRequiredHeading"),
                    T("Startup.AdminRequiredBody"));
                return;
            }

            string setupTargetUser = !string.IsNullOrWhiteSpace(startupTaskUser)
                ? startupTaskUser
                : GetCurrentWindowsUserName();
            var setupStopwatch = Stopwatch.StartNew();
            (bool installed, string installedExePath, string? installError, bool taskReady, bool launchedInstalledCopy) = StartupDialogs.ShowProgress(
                T("Common.AppName"),
                T("Startup.SetupProgressHeading"),
                T("Startup.SetupProgressBody"),
                () =>
                {
                    TryCleanupLegacyUserStartupEntries(exePath);
                    TryCleanupLegacyScheduledTasks(exePath);

                    if (!TryPrepareInstalledQuickZoom(out string progressInstalledExePath, out string? progressInstallError))
                    {
                        return (false, progressInstalledExePath, progressInstallError, false, false);
                    }

                    if (!TryRegisterElevatedStartupTask(progressInstalledExePath, setupTargetUser, out progressInstallError))
                    {
                        return (false, progressInstalledExePath, progressInstallError, false, false);
                    }

                    bool progressTaskReady = StartupTaskService.WaitUntilReady(progressInstalledExePath, setupTargetUser);
                    bool progressLaunchedInstalledCopy = !PathsEqual(exePath, progressInstalledExePath) &&
                        TryLaunchInstalledCopyAndWaitUntilReady(progressInstalledExePath, timeoutMs: 8000, ElevatedFlag);
                    return (true, progressInstalledExePath, progressInstallError, progressTaskReady, progressLaunchedInstalledCopy);
                });
            ErrorLog.Write("StartupTaskInstall", $"Startup-service setup flow finished in {ErrorLog.FormatElapsed(setupStopwatch.Elapsed)}. Installed={installed}; TaskReady={taskReady}; LaunchedInstalledCopy={launchedInstalledCopy}.");

            if (!installed)
            {
                StartupDialogs.ShowWarning(
                    T("Common.AppName"),
                    T("Startup.SetupIncompleteHeading"),
                    string.IsNullOrWhiteSpace(installError)
                        ? T("Startup.SetupCopyFailedBody")
                        : installError);
            }
            else
            {
                if (!taskReady)
                {
                    StartupDialogs.ShowWarning(
                        T("Common.AppName"),
                        T("Startup.SetupIncompleteHeading"),
                        T("Startup.SetupTaskNotReadyBody", installedExePath));
                }
                else
                {
                    if (!launchedInstalledCopy)
                    {
                        StartupDialogs.ShowTimedSuccess(
                            T("Common.AppName"),
                            T("Startup.SetupSuccessHeading"),
                            T("Startup.SetupSuccessBody"),
                            8);
                    }
                }
            }

            shouldInstallStartupTask = false;
            isElevatedLaunch = true;

            if (launchedInstalledCopy)
            {
                return;
            }

            if (!PathsEqual(exePath, installedExePath) && TryLaunchInstalledCopyAndWaitUntilReady(installedExePath, timeoutMs: 8000, ElevatedFlag))
            {
                return;
            }

            if (!acquiredMutex)
            {
                if (!TryAcquireSingleInstanceMutex(clearExistingProcesses: true, currentExePath: exePath))
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

        bool shouldOfferInstallOrUpdate = !isAdmin && !isElevatedLaunch && InstalledAppService.ShouldOfferInstallOrUpdate(exePath);
        if (!shouldOfferInstallOrUpdate && ShouldYieldToNewerInstance(exePath))
        {
            ShowLatestAlreadyRunningDialog();
            ReleaseSingleInstanceMutex();
            return;
        }

        if (!isAdmin && !isElevatedLaunch)
        {
            if (shouldOfferInstallOrUpdate)
            {
                bool wantsManagedInstall = PromptToInstallPermanentStartupCopy(StartupTaskService.GetStatus() == StartupTaskStatus.Ready);
                if (wantsManagedInstall && TryRelaunchAsAdministrator(args, StartupTaskInstallFlag))
                {
                    return;
                }

                StartupDialogs.ShowWarning(
                    T("Common.AppName"),
                    T("Startup.TempLocationHeading"),
                    T("Startup.TempLocationBody"));
            }
            else
            {
                StartupTaskStatus startupTaskStatus = StartupTaskService.GetStatus();
                if (startupTaskStatus == StartupTaskStatus.Ready && TryStartElevatedScheduledTaskAndVerify(exePath))
                {
                    return;
                }

                bool wantsStartupTaskSetup = PromptToInstallPermanentStartupCopy(startupTaskStatus is StartupTaskStatus.Ready or StartupTaskStatus.Broken);
                if (wantsStartupTaskSetup && TryRelaunchAsAdministrator(args, StartupTaskInstallFlag))
                {
                    return;
                }

                StartupDialogs.ShowWarning(
                    T("Common.AppName"),
                    T("Startup.NotElevatedHeading"),
                    wantsStartupTaskSetup
                        ? T("Startup.NotElevatedAfterFailedSetupBody")
                        : T("Startup.NotElevatedBody"));
            }
        }

        if (ReconcileOtherQuickZoomInstances(exePath) == InstanceStartupDecision.ExitCurrent)
        {
            ReleaseSingleInstanceMutex();
            return;
        }

        try
        {
            Application.Run(new TrayContext(startupReadyEventName));
        }
        catch (Exception ex)
        {
            ErrorLog.WriteCrash("ApplicationRun", ex);
        }
        finally
        {
            ErrorLog.Write("Shutdown", "QuickZoom process exiting.");
            ReleaseSingleInstanceMutex();
        }
    }

    private static bool TryAcquireSingleInstanceMutex(bool clearExistingProcesses = false, string? currentExePath = null)
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
                if (clearExistingProcesses)
                {
                    TryTerminateOtherQuickZoomProcesses("StartupMutex", currentExePath);
                    Thread.Sleep(250);
                    return TryAcquireSingleInstanceMutex(clearExistingProcesses: false, currentExePath);
                }

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

    private static bool TryAcquireSingleInstanceMutexWithRetry(string? currentExePath)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            if (TryAcquireSingleInstanceMutex(clearExistingProcesses: false, currentExePath))
            {
                return true;
            }

            if (attempt == 2 &&
                ReconcileOtherQuickZoomInstances(currentExePath, showAlreadyRunningDialog: false) == InstanceStartupDecision.ExitCurrent)
            {
                return false;
            }

            Thread.Sleep(250);
        }

        return false;
    }

    private static void ReleaseSingleInstanceMutex()
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

    private enum InstanceStartupDecision
    {
        ContinueCurrent,
        ExitCurrent
    }

    private static InstanceStartupDecision ReconcileOtherQuickZoomInstances(string? currentExePath, bool showAlreadyRunningDialog = true)
    {
        if (string.IsNullOrWhiteSpace(currentExePath))
        {
            return InstanceStartupDecision.ContinueCurrent;
        }

        Process currentProcess = Process.GetCurrentProcess();
        string normalizedCurrentPath = Path.GetFullPath(currentExePath);
        bool currentIsInstalledPreferred = InstalledAppService.IsCurrentInstalledExecutablePath(normalizedCurrentPath);
        DateTime currentWriteTimeUtc = TryGetExecutableWriteTimeUtc(normalizedCurrentPath);

        foreach (Process otherProcess in Process.GetProcessesByName(currentProcess.ProcessName))
        {
            using (otherProcess)
            {
                if (!TryGetSameSessionQuickZoomProcessPath(currentProcess, otherProcess, out string? otherExePath))
                {
                    continue;
                }

                bool otherIsInstalledPreferred = InstalledAppService.IsCurrentInstalledExecutablePath(otherExePath);
                InstancePreference preference = CompareInstancePreference(
                    normalizedCurrentPath,
                    currentWriteTimeUtc,
                    currentIsInstalledPreferred,
                    currentProcess,
                    otherExePath,
                    otherIsInstalledPreferred,
                    otherProcess);

                if (preference != InstancePreference.CurrentWins)
                {
                    ErrorLog.Write("Startup", "Existing QuickZoom instance wins startup arbitration. " + DescribeProcessInstance(otherProcess, otherExePath));
                    if (showAlreadyRunningDialog)
                    {
                        ShowLatestAlreadyRunningDialog();
                    }

                    return InstanceStartupDecision.ExitCurrent;
                }

                TryTerminateOlderQuickZoom(otherProcess, otherExePath);
                Thread.Sleep(250);
            }
        }

        return InstanceStartupDecision.ContinueCurrent;
    }

    private static void ShowLatestAlreadyRunningDialog()
    {
        StartupDialogs.ShowTrayInfo(
            T("Common.AppName"),
            T("Startup.LatestAlreadyRunningHeading"),
            T("Startup.LatestAlreadyRunningBody"));
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
            T("Common.AppName"),
            isUpdate
                ? T("Startup.InstallPromptUpdateHeading")
                : T("Startup.InstallPromptInstallHeading"),
            isUpdate
                ? T("Startup.InstallPromptUpdateBody")
                : T("Startup.InstallPromptInstallBody"));
    }

    private static bool TryRelaunchAsAdministrator(string[] args, params string[] extraFlags)
    {
        string? exePath = GetExecutablePath();
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return false;
        }

        string[] effectiveExtraFlags = extraFlags;
        if (ContainsArg(extraFlags, StartupTaskInstallFlag) && !HasArg(args, StartupTaskUserFlag))
        {
            string currentUser = GetCurrentWindowsUserName();
            if (!string.IsNullOrWhiteSpace(currentUser))
            {
                effectiveExtraFlags = [.. extraFlags, StartupTaskUserFlag, currentUser];
            }
        }

        string elevatedArgs = BuildArguments(args, effectiveExtraFlags);
        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true,
            Verb = "runas",
            Arguments = elevatedArgs
        };

        try
        {
            TryTerminateOtherQuickZoomProcesses("StartupSetupRelaunch", exePath);
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
        if (StartupTaskService.IsReadyForCurrentBuild(out string? readyExePath) &&
            !string.IsNullOrWhiteSpace(readyExePath))
        {
            installedExePath = readyExePath;
            errorMessage = null;
            ErrorLog.Write("StartupTaskInstall", "Startup task already targets current build: " + readyExePath);
            return true;
        }

        if (!TryPrepareInstalledQuickZoom(out installedExePath, out errorMessage))
        {
            return false;
        }

        return TryRegisterElevatedStartupTask(installedExePath, targetUser: null, out errorMessage);
    }

    private static bool TryPrepareInstalledQuickZoom(out string installedExePath, out string? errorMessage)
    {
        string? exePath = GetExecutablePath();
        if (string.IsNullOrWhiteSpace(exePath))
        {
            installedExePath = string.Empty;
            errorMessage = T("Startup.ErrorMissingExePath");
            return false;
        }

        if (!InstalledAppService.TryPrepareInstalledPayload(exePath, out installedExePath, out errorMessage))
        {
            return false;
        }

        return true;
    }

    private static bool TryRegisterElevatedStartupTask(string installedExePath, string? targetUser, out string? errorMessage)
    {
        var stopwatch = Stopwatch.StartNew();
        errorMessage = null;

        string currentUser = !string.IsNullOrWhiteSpace(targetUser)
            ? targetUser
            : GetCurrentWindowsUserName();
        if (string.IsNullOrWhiteSpace(currentUser))
        {
            errorMessage = T("Startup.ErrorMissingUser");
            ErrorLog.Write("StartupTaskInstall", "Could not determine target startup user.");
            return false;
        }

        if (StartupTaskService.IsReadyForCurrentBuild(installedExePath, currentUser, out StartupTaskInfo readyInfo))
        {
            ErrorLog.Write("StartupTaskInstall", $"Startup task already targets current build. Check completed in {ErrorLog.FormatElapsed(stopwatch.Elapsed)}. {DescribeStartupTask(readyInfo)}");
            return true;
        }

        TryCleanupLegacyScheduledTasks(installedExePath);

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
                errorMessage = T("Startup.ErrorPowerShellLaunch");
                ErrorLog.Write("StartupTaskInstall", $"PowerShell launch failed after {ErrorLog.FormatElapsed(stopwatch.Elapsed)}.");
                return false;
            }

            if (!process.WaitForExit(5000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort.
                }

                errorMessage = T("Startup.ErrorPowerShellTimeout");
                ErrorLog.Write("StartupTaskInstall", $"{errorMessage} Elapsed={ErrorLog.FormatElapsed(stopwatch.Elapsed)}.");
                return false;
            }

            string output = process.StandardOutput.ReadToEnd().Trim();
            string error = process.StandardError.ReadToEnd().Trim();
            bool success = process.ExitCode == 0;
            if (!success)
            {
                errorMessage = string.IsNullOrWhiteSpace(error)
                    ? T("Startup.ErrorPowerShellFailed")
                    : error;
                ErrorLog.Write("StartupTaskInstall", $"Task registration failed after {ErrorLog.FormatElapsed(stopwatch.Elapsed)}. StdOut: {output} StdErr: {error}");
                return false;
            }

            StartupTaskService.InvalidateCache();
            if (!StartupTaskService.IsReadyForCurrentBuild(installedExePath, currentUser, out StartupTaskInfo verifiedInfo))
            {
                errorMessage = T("Startup.ErrorPowerShellFailed");
                ErrorLog.Write("StartupTaskInstall", $"Task registration finished but verification failed after {ErrorLog.FormatElapsed(stopwatch.Elapsed)}. ExpectedUser={currentUser}; ExpectedPath={installedExePath}; {DescribeStartupTask(verifiedInfo)}");
                return false;
            }

            TryCleanupLegacyScheduledTasks(installedExePath);
            ErrorLog.Write("StartupTaskInstall", $"Startup task registration completed and verified in {ErrorLog.FormatElapsed(stopwatch.Elapsed)}. {DescribeStartupTask(verifiedInfo)}");
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = T("Startup.ErrorUnexpected");
            ErrorLog.Write("StartupTaskInstall", $"Unexpected startup task registration error after {ErrorLog.FormatElapsed(stopwatch.Elapsed)}. {ex}");
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

    private static bool TryStartElevatedScheduledTaskAndVerify(string? currentExePath)
    {
        ReleaseSingleInstanceMutex();
        try
        {
            StartupTaskInfo taskInfo = StartupTaskService.GetStatusInfo(forceRefresh: true);
            if (!TryStartElevatedScheduledTask())
            {
                return false;
            }

            if (WaitForOtherQuickZoomInstance(taskInfo.ExecutePath, timeoutMs: 15000, pollMs: 250))
            {
                return true;
            }

            ErrorLog.Write("StartupTaskRun", "The elevated startup task was accepted by Task Scheduler, but no replacement QuickZoom process appeared.");
            return false;
        }
        finally
        {
            if (_singleInstanceMutex == null)
            {
                _ = TryAcquireSingleInstanceMutex(clearExistingProcesses: false, currentExePath);
            }
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

            if (string.Equals(args[i], StartupReadyEventFlag, StringComparison.OrdinalIgnoreCase))
            {
                i++;
                continue;
            }

            if (string.Equals(args[i], StartupTaskUserFlag, StringComparison.OrdinalIgnoreCase))
            {
                i++;
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

            sb.Append(QuoteArgument(flag));
        }

        return sb.ToString();
    }

    private static bool ContainsArg(IEnumerable<string> args, string value)
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

    private static string GetCurrentWindowsUserName()
    {
        try
        {
            return WindowsIdentity.GetCurrent().Name;
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Elevation", ex);
            return string.Empty;
        }
    }

    private static string? GetArgValue(string[] args, string key)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(args[i + 1]) ? null : args[i + 1];
            }
        }

        return null;
    }

    private static string QuoteArgument(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        if (value.IndexOfAny([' ', '\t', '\n', '\r', '"']) < 0)
        {
            return value;
        }

        var quoted = new StringBuilder();
        quoted.Append('"');
        int backslashCount = 0;
        foreach (char c in value)
        {
            if (c == '\\')
            {
                backslashCount++;
                continue;
            }

            if (c == '"')
            {
                quoted.Append('\\', (backslashCount * 2) + 1);
                quoted.Append('"');
                backslashCount = 0;
                continue;
            }

            if (backslashCount > 0)
            {
                quoted.Append('\\', backslashCount);
                backslashCount = 0;
            }

            quoted.Append(c);
        }

        if (backslashCount > 0)
        {
            quoted.Append('\\', backslashCount * 2);
        }

        quoted.Append('"');
        return quoted.ToString();
    }

    private static string ToPowerShellSingleQuoted(string value)
    {
        return "'" + value.Replace("'", "''") + "'";
    }

    private static bool TryLaunchInstalledCopyAndWaitUntilReady(string installedExePath, int timeoutMs, params string[] extraFlags)
    {
        var stopwatch = Stopwatch.StartNew();
        string readyEventName = @"Local\QuickZoom2.StartupReady." + Guid.NewGuid().ToString("N");
        try
        {
            TryTerminateOtherQuickZoomProcesses("StartupInstalledLaunch", preferredExePath: installedExePath, keepPreferredExePath: true);

            using var readyEvent = new EventWaitHandle(false, EventResetMode.ManualReset, readyEventName);
            string[] launchFlags = new string[extraFlags.Length + 2];
            Array.Copy(extraFlags, launchFlags, extraFlags.Length);
            launchFlags[^2] = StartupReadyEventFlag;
            launchFlags[^1] = readyEventName;

            var startInfo = new ProcessStartInfo
            {
                FileName = installedExePath,
                UseShellExecute = false,
                Arguments = BuildArguments(Array.Empty<string>(), launchFlags)
            };

            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                ErrorLog.Write("Startup", $"Could not launch the installed QuickZoom copy after startup-service setup. Elapsed={ErrorLog.FormatElapsed(stopwatch.Elapsed)}.");
                return false;
            }

            bool ready = readyEvent.WaitOne(timeoutMs);
            if (!ready)
            {
                ErrorLog.Write("Startup", $"Installed QuickZoom copy launched but did not signal tray readiness before timeout. Elapsed={ErrorLog.FormatElapsed(stopwatch.Elapsed)}. Path: {installedExePath}");
            }
            else
            {
                ErrorLog.Write("Startup", $"Installed QuickZoom copy launched and tray was ready in {ErrorLog.FormatElapsed(stopwatch.Elapsed)}. Path: {installedExePath}");
            }

            return ready;
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Startup", $"Installed QuickZoom launch failed after {ErrorLog.FormatElapsed(stopwatch.Elapsed)}. {ex}");
            return false;
        }
    }

    private static void TryTerminateOtherQuickZoomProcesses(string source, string? preferredExePath = null, bool keepPreferredExePath = false)
    {
        Process currentProcess = Process.GetCurrentProcess();
        string? winningExePath = !string.IsNullOrWhiteSpace(preferredExePath)
            ? Path.GetFullPath(preferredExePath)
            : GetExecutablePath();

        foreach (Process otherProcess in Process.GetProcessesByName(currentProcess.ProcessName))
        {
            using (otherProcess)
            {
                if (!TryGetSameSessionQuickZoomProcessPath(currentProcess, otherProcess, out string? otherExePath))
                {
                    continue;
                }

                if (keepPreferredExePath && PathsEqual(otherExePath, preferredExePath))
                {
                    continue;
                }

                if (!CanReplaceProcessWithPreferredExecutable(winningExePath, otherExePath))
                {
                    ErrorLog.Write(source, "Leaving existing QuickZoom process alone because it is not older or less preferred. " + DescribeProcessInstance(otherProcess, otherExePath));
                    continue;
                }

                try
                {
                    ErrorLog.Write(source, "Stopping replaceable QuickZoom process. " + DescribeProcessInstance(otherProcess, otherExePath));
                    otherProcess.Kill(entireProcessTree: true);
                    if (!otherProcess.WaitForExit(3000))
                    {
                        ErrorLog.Write(source, "Existing QuickZoom process did not exit within the timeout. " + DescribeProcessInstance(otherProcess, otherExePath));
                    }
                }
                catch (Exception ex)
                {
                    ErrorLog.Write(source, "Could not stop existing QuickZoom process at " + otherExePath + ". " + ex.Message);
                }
            }
        }
    }

    private static bool WaitForOtherQuickZoomInstance(string? expectedExePath, int timeoutMs, int pollMs)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds <= timeoutMs)
        {
            if (HasOtherQuickZoomInstance(expectedExePath))
            {
                return true;
            }

            Thread.Sleep(pollMs);
        }

        return false;
    }

    private static bool HasOtherQuickZoomInstance(string? expectedExePath)
    {
        Process currentProcess = Process.GetCurrentProcess();
        foreach (Process otherProcess in Process.GetProcessesByName(currentProcess.ProcessName))
        {
            using (otherProcess)
            {
                if (!TryGetSameSessionQuickZoomProcessPath(currentProcess, otherProcess, out string? otherExePath))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(expectedExePath) && !PathsEqual(otherExePath, expectedExePath))
                {
                    ErrorLog.Write("StartupTaskRun", "Ignoring replacement candidate because it is not the task target. " + DescribeProcessInstance(otherProcess, otherExePath));
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    private static bool TryGetSameSessionQuickZoomProcessPath(Process currentProcess, Process otherProcess, out string otherExePath)
    {
        otherExePath = string.Empty;
        if (otherProcess.Id == currentProcess.Id)
        {
            return false;
        }

        try
        {
            if (otherProcess.SessionId != currentProcess.SessionId || otherProcess.HasExited)
            {
                return false;
            }
        }
        catch
        {
            return false;
        }

        string? path = TryGetProcessExecutablePath(otherProcess);
        if (string.IsNullOrWhiteSpace(path) || !LooksLikeQuickZoomExecutable(path))
        {
            return false;
        }

        otherExePath = path;
        return true;
    }

    private static bool CanReplaceProcessWithPreferredExecutable(string? preferredExePath, string otherExePath)
    {
        if (string.IsNullOrWhiteSpace(preferredExePath))
        {
            return true;
        }

        int preferredBuild = TryGetExecutableBuildNumber(preferredExePath);
        int otherBuild = TryGetExecutableBuildNumber(otherExePath);
        if (preferredBuild > 0 && otherBuild > 0)
        {
            if (preferredBuild != otherBuild)
            {
                return preferredBuild > otherBuild;
            }

            bool preferredIsInstalled = InstalledAppService.IsCurrentInstalledExecutablePath(preferredExePath);
            bool otherIsInstalled = InstalledAppService.IsCurrentInstalledExecutablePath(otherExePath);
            return preferredIsInstalled || !otherIsInstalled;
        }

        DateTime preferredWriteTime = TryGetExecutableWriteTimeUtc(preferredExePath);
        DateTime otherWriteTime = TryGetExecutableWriteTimeUtc(otherExePath);
        if (preferredWriteTime != DateTime.MinValue && otherWriteTime != DateTime.MinValue)
        {
            return preferredWriteTime >= otherWriteTime;
        }

        return true;
    }

    private static bool ShouldYieldToNewerInstance(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return false;
        }

        string currentExePath = Path.GetFullPath(exePath);
        bool currentIsInstalledPreferred = InstalledAppService.IsCurrentInstalledExecutablePath(currentExePath);
        DateTime currentWriteTimeUtc = TryGetExecutableWriteTimeUtc(currentExePath);
        Process currentProcess = Process.GetCurrentProcess();

        foreach (Process otherProcess in Process.GetProcessesByName(currentProcess.ProcessName))
        {
            using (otherProcess)
            {
                if (!TryGetSameSessionQuickZoomProcessPath(currentProcess, otherProcess, out string? otherExePath))
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
                    ErrorLog.Write("Startup", "Yielding to a newer or preferred QuickZoom instance. " + DescribeProcessInstance(otherProcess, otherExePath));
                    return true;
                }

                if (preference == InstancePreference.CurrentWins)
                {
                    TryTerminateOlderQuickZoom(otherProcess, otherExePath);
                }
            }
        }

        return false;
    }

    private static string DescribeStartupTask(StartupTaskInfo info)
    {
        return $"Status={info.Status}; User={info.UserId ?? "<none>"}; Path={info.ExecutePath ?? "<none>"}; Args={info.Arguments ?? "<none>"}; Details={info.Details ?? "<none>"}";
    }

    private static string DescribeProcessInstance(Process process, string exePath)
    {
        string started = "<unknown>";
        try
        {
            started = process.StartTime.ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch
        {
            // Access can fail for a process exiting during inspection.
        }

        return $"PID={process.Id}; Build={TryGetExecutableBuildNumber(exePath)}; Started={started}; Path={exePath}";
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
        int otherBuildNumber = TryGetExecutableBuildNumber(otherExePath);
        if (otherBuildNumber > 0 && otherBuildNumber != AppInfo.BuildNumber)
        {
            return AppInfo.BuildNumber > otherBuildNumber
                ? InstancePreference.CurrentWins
                : InstancePreference.OtherWins;
        }

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

    private static void TryTerminateOlderQuickZoom(Process otherProcess, string otherExePath)
    {
        try
        {
            ErrorLog.Write("Startup", "Attempting to stop older QuickZoom instance. " + DescribeProcessInstance(otherProcess, otherExePath));
            otherProcess.Kill(entireProcessTree: false);
            if (!otherProcess.WaitForExit(2000))
            {
                ErrorLog.Write("Startup", "Older QuickZoom instance did not exit within the timeout. " + DescribeProcessInstance(otherProcess, otherExePath));
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Startup", "Could not stop older QuickZoom instance at " + otherExePath + ". " + ex.Message);
        }
    }

    private static bool LooksLikeQuickZoomExecutable(string exePath)
    {
        try
        {
            return string.Equals(Path.GetFileName(exePath), "QuickZoom.exe", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string? TryGetProcessExecutablePath(Process process)
    {
        try
        {
            IntPtr handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, inheritHandle: false, process.Id);
            if (handle != IntPtr.Zero)
            {
                try
                {
                    int size = 1024;
                    var buffer = new StringBuilder(size);
                    if (QueryFullProcessImageName(handle, 0, buffer, ref size) && size > 0)
                    {
                        string path = buffer.ToString(0, size);
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            return Path.GetFullPath(path);
                        }
                    }
                }
                finally
                {
                    CloseHandle(handle);
                }
            }
        }
        catch
        {
            // Fall through to the slower MainModule path.
        }

        try
        {
            if (process.MainModule?.FileName is string path && !string.IsNullOrWhiteSpace(path))
            {
                return Path.GetFullPath(path);
            }
        }
        catch
        {
            // Ignore access failures.
        }

        return null;
    }

    private static int TryGetExecutableBuildNumber(string exePath)
    {
        try
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(exePath);
            if (info.FileBuildPart > 0)
            {
                return info.FileBuildPart;
            }
        }
        catch
        {
            // Fall through to path parsing.
        }

        try
        {
            DirectoryInfo? directory = Directory.GetParent(exePath);
            while (directory != null)
            {
                if (directory.Name.StartsWith("Build ", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(directory.Name["Build ".Length..], out int buildNumber))
                {
                    return buildNumber;
                }

                directory = directory.Parent;
            }
        }
        catch
        {
            // Ignore path parsing failures.
        }

        return 0;
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
        ErrorLog.WriteCrash(source, exception);
    }

    private static void ConfigureErrorLoggingFromSettings()
    {
        bool debugLoggingEnabled = false;
        try
        {
            if (File.Exists(AppPaths.SettingsPath))
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(AppPaths.SettingsPath));
                if (document.RootElement.TryGetProperty("DebugLoggingEnabled", out JsonElement value) &&
                    value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    debugLoggingEnabled = value.GetBoolean();
                }
            }
        }
        catch
        {
            // Crash logging still works even if settings cannot be read.
        }

        ErrorLog.Configure(debugLoggingEnabled, AppInfo.VersionHash);
    }

    private static void TryCleanupLegacyUserStartupEntries(string? currentExePath)
    {
        try
        {
            RemoveLegacyRunEntries(currentExePath);
        }
        catch (Exception ex)
        {
            ErrorLog.Write("StartupCleanup.Run", ex);
        }

        try
        {
            RemoveLegacyStartupFolderEntries(currentExePath);
        }
        catch (Exception ex)
        {
            ErrorLog.Write("StartupCleanup.StartupFolder", ex);
        }
    }

    private static void RemoveLegacyRunEntries(string? currentExePath)
    {
        using RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        if (runKey == null)
        {
            return;
        }

        foreach (string valueName in runKey.GetValueNames())
        {
            string? valueData = runKey.GetValue(valueName)?.ToString();
            if (!LooksLikeQuickZoomStartupReference(valueName, valueData))
            {
                continue;
            }

            if (ReferencePointsToCurrentExecutable(valueData, currentExePath))
            {
                continue;
            }

            runKey.DeleteValue(valueName, throwOnMissingValue: false);
            ErrorLog.Write("StartupCleanup.Run", "Removed legacy HKCU Run entry: " + valueName);
        }
    }

    private static void RemoveLegacyStartupFolderEntries(string? currentExePath)
    {
        string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        if (string.IsNullOrWhiteSpace(startupFolder) || !Directory.Exists(startupFolder))
        {
            return;
        }

        foreach (string candidatePath in Directory.GetFiles(startupFolder))
        {
            if (!LooksLikeQuickZoomStartupFile(candidatePath))
            {
                continue;
            }

            if (ReferencePointsToCurrentExecutable(candidatePath, currentExePath))
            {
                continue;
            }

            File.Delete(candidatePath);
            ErrorLog.Write("StartupCleanup.StartupFolder", "Removed legacy Startup-folder entry: " + candidatePath);
        }
    }

    private static void TryCleanupLegacyScheduledTasks(string? currentExePath)
    {
        foreach (string taskName in GetQuickZoomTaskNames())
        {
            try
            {
                if (string.Equals(taskName, StartupTaskService.ElevatedStartupTaskName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!ScheduledTaskReferencesQuickZoom(taskName))
                {
                    ErrorLog.Write("StartupCleanup.Task", "Skipped scheduled task cleanup because the task does not point to QuickZoom: " + taskName);
                    continue;
                }

                if (DeleteScheduledTask(taskName))
                {
                    ErrorLog.Write("StartupCleanup.Task", "Removed legacy scheduled task: " + taskName);
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Write("StartupCleanup.Task", "Could not remove legacy scheduled task '" + taskName + "'. " + ex.Message);
            }
        }
    }

    private static IEnumerable<string> GetQuickZoomTaskNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string knownName in LegacyStartupTaskNames)
        {
            names.Add(knownName);
        }

        try
        {
            foreach (string taskName in QueryScheduledTaskNamesContainingQuickZoom())
            {
                names.Add(taskName);
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write("StartupCleanup.Task", "Could not enumerate scheduled tasks. " + ex.Message);
        }

        return names;
    }

    private static bool ScheduledTaskReferencesQuickZoom(string taskName)
    {
        StartupTaskInfo info = StartupTaskService.QueryTask(taskName);
        if (info.Status is StartupTaskStatus.Missing or StartupTaskStatus.Unknown)
        {
            return false;
        }

        return IsQuickZoomExecutableReference(info.ExecutePath) ||
               IsQuickZoomExecutableReference(info.Arguments) ||
               (!string.IsNullOrWhiteSpace(info.Details) &&
                info.Details.IndexOf("QuickZoom.exe", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static IEnumerable<string> QueryScheduledTaskNamesContainingQuickZoom()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = "/Query /FO CSV /NH",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using Process? process = Process.Start(startInfo);
        if (process == null)
        {
            yield break;
        }

        if (!process.WaitForExit(5000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort.
            }

            yield break;
        }

        string output = process.StandardOutput.ReadToEnd();
        foreach (string line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string taskName = ParseFirstCsvField(line).TrimStart('\\');
            if (taskName.IndexOf("QuickZoom", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                yield return taskName;
            }
        }
    }

    private static string ParseFirstCsvField(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        if (line[0] != '"')
        {
            int commaIndex = line.IndexOf(',');
            return commaIndex >= 0 ? line[..commaIndex] : line;
        }

        var sb = new StringBuilder();
        for (int i = 1; i < line.Length; i++)
        {
            if (line[i] == '"' && i + 1 < line.Length && line[i + 1] == '"')
            {
                sb.Append('"');
                i++;
                continue;
            }

            if (line[i] == '"')
            {
                break;
            }

            sb.Append(line[i]);
        }

        return sb.ToString();
    }

    private static bool DeleteScheduledTask(string taskName)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = "/Delete /TN " + QuoteArgument(taskName) + " /F",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using Process? process = Process.Start(startInfo);
        if (process == null)
        {
            return false;
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

            ErrorLog.Write("StartupCleanup.Task", "Timed out while deleting scheduled task '" + taskName + "'.");
            return false;
        }

        string output = process.StandardOutput.ReadToEnd().Trim();
        string error = process.StandardError.ReadToEnd().Trim();
        bool success = process.ExitCode == 0;
        if (!success && LooksLikeMissingScheduledTask(output + Environment.NewLine + error))
        {
            return false;
        }

        if (!success && !string.IsNullOrWhiteSpace(output + error))
        {
            ErrorLog.Write("StartupCleanup.Task", "Delete failed for '" + taskName + "'. StdOut: " + output + " StdErr: " + error);
        }

        return success;
    }

    private static bool LooksLikeQuickZoomStartupFile(string path)
    {
        try
        {
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
            if (!fileNameWithoutExtension.StartsWith("QuickZoom", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string extension = Path.GetExtension(path);
            return extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".url", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".exe", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool LooksLikeQuickZoomStartupReference(string name, string? value)
    {
        if (IsQuickZoomExecutableReference(value))
        {
            return true;
        }

        return string.Equals(name, "QuickZoom", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "QuickZoom2", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "QuickZoom Startup", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsQuickZoomExecutableReference(string? reference)
    {
        string? executablePath = TryExtractExecutablePath(reference);
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            return LooksLikeQuickZoomExecutable(executablePath);
        }

        return !string.IsNullOrWhiteSpace(reference) &&
               reference.IndexOf("QuickZoom.exe", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string? TryExtractExecutablePath(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        string text = Environment.ExpandEnvironmentVariables(reference.Trim());
        if (text.Length == 0)
        {
            return null;
        }

        if (text[0] == '"')
        {
            int closingQuote = text.IndexOf('"', 1);
            return closingQuote > 1 ? text[1..closingQuote] : null;
        }

        int exeIndex = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIndex < 0)
        {
            return null;
        }

        return text[..(exeIndex + 4)].Trim();
    }

    private static bool ReferencePointsToCurrentExecutable(string? reference, string? currentExePath)
    {
        if (string.IsNullOrWhiteSpace(reference) || string.IsNullOrWhiteSpace(currentExePath))
        {
            return false;
        }

        string currentFullPath = Path.GetFullPath(currentExePath);
        string? executablePath = TryExtractExecutablePath(reference);
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            return PathsEqual(executablePath, currentFullPath);
        }

        return reference.IndexOf(currentFullPath, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool LooksLikeMissingScheduledTask(string text)
    {
        return text.IndexOf("cannot find", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf("the system cannot find", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf("specified file", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf("angivne fil", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf("blev ikke fundet", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf("kan ikke finde", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
