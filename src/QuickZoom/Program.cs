using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Win32;

namespace QuickZoom;

internal static class Program
{
    private const string SingleInstanceMutexName = @"Local\QuickZoom2.SingleInstance";
    private static Mutex? _singleInstanceMutex;
    private static IDisposable? _startupYieldingMarker;
    // Per-monitor v2 gives physical pixel coordinates across mixed-DPI setups.
    private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new(-4);
    // Keep older shortcuts compatible, but always open the current wizard.
    private const string StartupSetupFlag = "--install-startup-task";
    internal const string SetupStartupTaskInstallFlag = "--setup-install-startup-task";
    private const string StartupReadyEventFlag = "--startup-ready-event";
    private const string StartupTaskUserFlag = "--startup-task-user";
    private const string CaptureUiScreenshotsFlag = "--capture-ui-screenshots";
    private const string CaptureSettingsSmokeFlag = "--capture-settings-smoke";
    private const string CaptureSetupSmokeFlag = "--capture-setup-smoke";
    private const string SetupFlag = "-setup";
    private const string LongSetupFlag = "--setup";
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

    private const string ElevatedFlag = "--quickzoom-elevated";

    private static UiLanguage StartupLanguage => UiText.GetStartupLanguage();

    private static string T(string key, params object[] args) => UiText.Get(StartupLanguage, key, args);

    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            LocalStorage.RequireLocalPath(AppContext.BaseDirectory);
            LocalStorage.RequireLocalPath(AppPaths.SettingsPath);
        }
        catch
        {
            MessageBox.Show("QuickZoom requires local, non-redirected application and settings paths.", "QuickZoom", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.ExitCode = 1;
            return;
        }
        string? exePath = GetExecutablePath();
        bool isElevatedLaunch = HasArg(args, ElevatedFlag);
        bool shouldRunStartupSetup = HasArg(args, StartupSetupFlag);
        bool shouldInstallSetupStartupTask = HasArg(args, SetupStartupTaskInstallFlag);
        string? startupReadyEventName = GetArgValue(args, StartupReadyEventFlag);
        string? startupTaskUser = GetArgValue(args, StartupTaskUserFlag);
        bool shouldCaptureUiScreenshots = HasArg(args, CaptureUiScreenshotsFlag);
        bool shouldCaptureSettingsSmoke = HasArg(args, CaptureSettingsSmokeFlag);
        bool shouldCaptureSetupSmoke = HasArg(args, CaptureSetupSmokeFlag);
        AccessibilityPreferences.CaptureHighContrast = HasArg(args, "--capture-high-contrast") &&
            (shouldCaptureSetupSmoke || shouldCaptureUiScreenshots || shouldCaptureSettingsSmoke);
        bool shouldRunSetup = HasArg(args, SetupFlag) || HasArg(args, LongSetupFlag);
        bool setupFlowWasShown = false;

        EnablePerMonitorDpiAwareness();
        try { Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => LogFatalException("UI thread", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogFatalException("AppDomain", e.ExceptionObject as Exception);
        bool internalStartupMode = shouldCaptureUiScreenshots ||
            shouldCaptureSettingsSmoke ||
            shouldCaptureSetupSmoke ||
            shouldRunStartupSetup ||
            shouldInstallSetupStartupTask ||
            startupReadyEventName != null;
        if (shouldRunSetup || (!internalStartupMode && FirstRunSetup.ShouldRunAutomatically()))
        {
            setupFlowWasShown = true;
            if (ShowSetupAndStartRuntime(exePath)) return;
        }

        _ = AppThemeBootstrap.TryApplyNativeColorMode(AppThemeBootstrap.ReadPersistedThemeMode());

        if (!shouldCaptureUiScreenshots &&
            !shouldCaptureSettingsSmoke &&
            !shouldCaptureSetupSmoke &&
            !shouldInstallSetupStartupTask &&
            startupReadyEventName == null)
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
        }

        ErrorLog.WriteAlways("Startup", $"Launching {AppInfo.DisplayVersion} from {AppContext.BaseDirectory}");

        if (shouldCaptureUiScreenshots)
        {
            string workingDirectory = Directory.GetCurrentDirectory();
            string screenshotsRoot = GetArgValue(args, "--capture-output") ?? Path.Combine(
                workingDirectory, "test-validation", $"Build {AppInfo.BuildNumber}", "Interface");
            try { TrayContext.CaptureUiScreenshots(screenshotsRoot, GetArgValue(args, "--capture-language"), GetArgValue(args, "--capture-font")); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }

        if (shouldCaptureSettingsSmoke)
        {
            string workingDirectory = Directory.GetCurrentDirectory();
            try
            {
                TrayContext.CaptureSettingsSmoke(GetArgValue(args, "--capture-output") ?? Path.Combine(
                    workingDirectory, "test-validation", $"Build {AppInfo.BuildNumber}", "Settings"));
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }

        if (shouldCaptureSetupSmoke)
        {
            try
            {
                string workingDirectory = Directory.GetCurrentDirectory();
                string captureRoot = GetArgValue(args, "--capture-output") ?? Path.Combine(
                    workingDirectory, "test-validation", $"Build {AppInfo.BuildNumber}", "Setup");
                FirstRunSetup.CaptureSmoke(captureRoot, GetArgValue(args, "--capture-language"));
                StartupDialogs.CaptureSmoke(Path.Combine(captureRoot, "startup-messages"), GetArgValue(args, "--capture-language"));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                ErrorLog.WriteAlways("Program.CaptureSetupSmoke", ex.ToString());
                Environment.ExitCode = 1;
            }
            return;
        }

        bool isAdmin = IsRunningAsAdministrator();

        if (shouldInstallSetupStartupTask)
        {
            Environment.ExitCode = TryInstallStartupTaskForSetup(exePath, startupTaskUser) ? 0 : 1;
            return;
        }

        InitializeStartupMaintenance(exePath);

        bool shouldOfferInstallOrUpdate = !isAdmin && !isElevatedLaunch && InstalledAppService.ShouldOfferInstallOrUpdate(exePath);
        if (!shouldOfferInstallOrUpdate && ShouldYieldToNewerInstance(exePath))
        {
            ShowLatestAlreadyRunningDialog();
            ReleaseSingleInstanceMutex();
            return;
        }

        if (!isAdmin &&
            !isElevatedLaunch &&
            setupFlowWasShown &&
            StartupTaskService.GetStatus() == StartupTaskStatus.Ready &&
            StartElevatedScheduledTaskAndVerify() != StartupTaskLaunchResult.Failed)
        {
            return;
        }

        bool suppressStartupSetup =
            FirstRunSetup.StartupServiceWasSkipped() ||
            setupFlowWasShown;
        if ((shouldRunStartupSetup && !setupFlowWasShown) ||
            (!isAdmin && !isElevatedLaunch && !suppressStartupSetup))
        {
            if (!shouldRunStartupSetup &&
                !shouldOfferInstallOrUpdate &&
                StartupTaskService.GetStatus() == StartupTaskStatus.Ready &&
                StartElevatedScheduledTaskAndVerify() != StartupTaskLaunchResult.Failed)
            {
                return;
            }

            // A setup offered at launch always starts at the language screen.
            // Only the explicit autostart action in Settings uses the short flow.
            if (ShowSetupAndStartRuntime(exePath, startupMaintenanceComplete: true)) return;
            if (!isAdmin &&
                StartupTaskService.IsReadyForCurrentBuild(out _) &&
                StartElevatedScheduledTaskAndVerify() != StartupTaskLaunchResult.Failed)
            {
                return;
            }
        }

        if (ReconcileOtherQuickZoomInstances(exePath) == InstanceStartupDecision.ExitCurrent)
        {
            ReleaseSingleInstanceMutex();
            return;
        }

        try
        {
            using var context = new TrayContext(startupReadyEventName);
            Application.Run(context);
        }
        catch (Exception ex)
        {
            ErrorLog.WriteCrash("ApplicationRun", ex);
            Environment.ExitCode = 1;
            MessageBox.Show(T("Error.MagnifierInit"), T("Common.AppName"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            ErrorLog.WriteAlways("Shutdown", "QuickZoom process exiting.");
            ReleaseSingleInstanceMutex();
        }
    }

    private static void InitializeStartupMaintenance(string? exePath)
    {
        bool isAdmin = IsRunningAsAdministrator();
        _ = Task.Run(() =>
        {
            if (!StartupTaskService.IsReadyForCurrentBuild(out _)) return;
            TryCleanupLegacyUserStartupEntries(exePath);
            if (isAdmin) TryCleanupLegacyScheduledTasks(exePath);
        });

        if (isAdmin && InstalledAppService.IsManagedInstallPath(exePath) &&
            InstalledAppService.NeedsSecureInstallMigration(exePath))
        {
            if (TryInstallElevatedScheduledTask(out string migratedExePath, out string? migrationError))
                ErrorLog.Write("StartupMigration", "Migrated elevated startup payload to secured install path: " + migratedExePath);
            else
                ErrorLog.Write("StartupMigration", "Could not migrate the legacy startup install to the secured install path. " + (migrationError ?? string.Empty));
        }
    }

    private static bool ShowSetupAndStartRuntime(string? exePath, bool startupMaintenanceComplete = false)
    {
        TrayContext? context = null;
        bool runtimeReady = false;
        bool preparationStarted = false;
        EventHandler closeSetup = (_, _) => Application.Exit();
        try
        {
            FirstRunSetup.Show(allowLivePractice: _singleInstanceMutex != null || !IsSingleInstanceActive(), async skipped =>
            {
                preparationStarted = true;
                if (runtimeReady) return;
                if (context == null && await Task.Run(() =>
                    ReconcileOtherQuickZoomInstances(exePath, showAlreadyRunningDialog: false)) == InstanceStartupDecision.ExitCurrent)
                {
                    if (!HasOtherQuickZoomInstance(null, requireReady: true))
                        throw new InvalidOperationException("The existing QuickZoom instance has not reported readiness yet.");
                    runtimeReady = true;
                    return;
                }

                // Keep mutex acquisition/release on this UI thread. Waiting for
                // Task Scheduler must leave the setup window responsive.
                if (_singleInstanceMutex == null && !TryAcquireSingleInstanceMutex())
                    throw new InvalidOperationException("Another QuickZoom instance is still starting. Retry when it is ready.");
                _startupYieldingMarker?.Dispose();
                _startupYieldingMarker = null;

                if (context == null && !skipped && !IsRunningAsAdministrator())
                {
                    StartupTaskLaunchResult result = await StartElevatedScheduledTaskForSetupAsync();
                    if (result == StartupTaskLaunchResult.Ready)
                    {
                        runtimeReady = true;
                        return;
                    }
                    if (result == StartupTaskLaunchResult.Pending)
                        throw new TimeoutException("The elevated QuickZoom instance has not reported readiness. Retry to check it again.");
                }

                if (context == null)
                {
                    if (!startupMaintenanceComplete)
                    {
                        await Task.Run(() => InitializeStartupMaintenance(exePath));
                        startupMaintenanceComplete = true;
                    }
                    ErrorLog.WriteAlways("Startup", $"Starting {AppInfo.DisplayVersion} before setup completion from {exePath}");
                    context = new TrayContext();
                    context.ThreadExit += closeSetup;
                }
                await context.WaitForStartupReadyAsync();
                runtimeReady = true;
            });

            if (!runtimeReady) return preparationStarted;
            // The wizard's modal loop has already been serving the hooks and
            // tray. Continue with the same runtime, without restarting it.
            if (context is { IsStartupReady: true })
            {
                context.ThreadExit -= closeSetup;
                Application.Run(context);
            }
            return true;
        }
        catch (Exception ex)
        {
            ErrorLog.WriteCrash("Setup.ApplicationRun", ex);
            Environment.ExitCode = 1;
            MessageBox.Show(T("Error.MagnifierInit"), T("Common.AppName"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            return true;
        }
        finally
        {
            context?.Dispose();
            if (preparationStarted) ReleaseSingleInstanceMutex();
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
            ErrorLog.Write("Startup", "Could not acquire the single-instance mutex. Startup was stopped to avoid duplicate runtimes.");
            return false;
        }
    }

    private static bool IsSingleInstanceActive()
    {
        try
        {
            if (!Mutex.TryOpenExisting(SingleInstanceMutexName, out Mutex? existing))
            {
                return false;
            }

            existing.Dispose();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryAcquireSingleInstanceMutexWithRetry(string? currentExePath)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            if (TryAcquireSingleInstanceMutex())
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

                if (StartupHandoff.IsYielding(otherProcess)) continue;

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
        StartupDialogs.ShowAlreadyRunning();
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

    private static bool TryInstallElevatedScheduledTask(out string installedExePath, out string? errorMessage)
    {
        installedExePath = GetExecutablePath() ?? string.Empty;
        using IDisposable? transaction = InstalledAppService.TryAcquireInstallTransaction(out errorMessage);
        if (transaction == null) return false;
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

        if (!TryRegisterElevatedStartupTask(installedExePath, targetUser: null, out errorMessage) ||
            !InstalledAppService.TryCommitInstalledPayload(installedExePath, out errorMessage)) return false;
        TryCleanupLegacyScheduledTasks(installedExePath);
        return true;
    }

    private static bool TryInstallStartupTaskForSetup(string? sourceExePath, string? targetUser)
    {
        if (!IsRunningAsAdministrator())
        {
            ErrorLog.Write("FirstRunSetup.Startup", "The startup-service helper was not elevated.");
            return false;
        }

        try
        {
            using IDisposable? transaction = InstalledAppService.TryAcquireInstallTransaction(out string? transactionError);
            if (transaction == null)
            {
                ErrorLog.Write("FirstRunSetup.Startup", transactionError ?? "Could not acquire the installation lock.");
                return false;
            }
            if (!TryPrepareInstalledQuickZoom(out string installedExePath, out string? errorMessage))
            {
                ErrorLog.Write("FirstRunSetup.Startup", errorMessage ?? "Could not prepare the managed install.");
                return false;
            }

            if (!TryRegisterElevatedStartupTask(installedExePath, targetUser, out errorMessage))
            {
                ErrorLog.Write("FirstRunSetup.Startup", errorMessage ?? "Could not register the startup task.");
                return false;
            }

            if (!InstalledAppService.TryCommitInstalledPayload(installedExePath, out errorMessage))
            {
                ErrorLog.Write("FirstRunSetup.Startup", errorMessage ?? "Could not commit the managed install.");
                return false;
            }

            TryCleanupLegacyUserStartupEntries(sourceExePath);
            TryCleanupLegacyScheduledTasks(installedExePath);

            bool ready = StartupTaskService.WaitUntilReady(installedExePath, targetUser);
            ErrorLog.Write(
                "FirstRunSetup.Startup",
                ready
                    ? "The startup service was created and verified."
                    : "The startup task was created, but verification did not complete.");
            return ready;
        }
        catch (Exception ex)
        {
            ErrorLog.Write("FirstRunSetup.Startup", ex);
            return false;
        }
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
            errorMessage = T("Startup.SetupCopyFailedBody");
            return false;
        }

        return true;
    }

    private static bool TryRegisterElevatedStartupTask(string installedExePath, string? targetUser, out string? errorMessage)
    {
        var stopwatch = Stopwatch.StartNew();
        string? taskDefinitionPath = null;
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

        try
        {
            if (!InstalledAppService.IsSecureInstallPath(installedExePath))
                throw new InvalidOperationException("The startup executable is not in a protected managed install.");
            taskDefinitionPath = Path.Combine(Path.GetDirectoryName(installedExePath)!, Guid.NewGuid().ToString("N") + ".task.xml");
            WriteStartupTaskDefinition(taskDefinitionPath, installedExePath, currentUser);

            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
                Arguments = "/Create /TN " + QuoteArgument(StartupTaskService.ElevatedStartupTaskName) +
                    " /XML " + QuoteArgument(taskDefinitionPath) + " /F",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                errorMessage = T("Startup.ErrorTaskRegistrationLaunch");
                ErrorLog.Write("StartupTaskInstall", $"schtasks.exe launch failed after {ErrorLog.FormatElapsed(stopwatch.Elapsed)}.");
                return false;
            }

            if (!ProcessOutput.TryRead(process, 15000, out string output, out string error))
            {
                errorMessage = T("Startup.ErrorTaskRegistrationTimeout");
                ErrorLog.Write("StartupTaskInstall", $"{errorMessage} Elapsed={ErrorLog.FormatElapsed(stopwatch.Elapsed)}.");
                return false;
            }

            bool success = process.ExitCode == 0;
            if (!success)
            {
                errorMessage = T("Startup.ErrorTaskRegistrationFailed");
                ErrorLog.Write("StartupTaskInstall", $"Task registration failed after {ErrorLog.FormatElapsed(stopwatch.Elapsed)}. StdOut: {output} StdErr: {error}");
                return false;
            }

            StartupTaskService.InvalidateCache();
            if (!StartupTaskService.IsReadyForCurrentBuild(installedExePath, currentUser, out StartupTaskInfo verifiedInfo))
            {
                errorMessage = T("Startup.ErrorTaskRegistrationFailed");
                ErrorLog.Write("StartupTaskInstall", $"Task registration finished but verification failed after {ErrorLog.FormatElapsed(stopwatch.Elapsed)}. ExpectedUser={currentUser}; ExpectedPath={installedExePath}; {DescribeStartupTask(verifiedInfo)}");
                return false;
            }

            ErrorLog.Write("StartupTaskInstall", $"Startup task registration completed and verified in {ErrorLog.FormatElapsed(stopwatch.Elapsed)}. {DescribeStartupTask(verifiedInfo)}");
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = T("Startup.ErrorUnexpected");
            ErrorLog.Write("StartupTaskInstall", $"Unexpected startup task registration error after {ErrorLog.FormatElapsed(stopwatch.Elapsed)}. {ex}");
            return false;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(taskDefinitionPath))
            {
                try
                {
                    File.Delete(taskDefinitionPath);
                }
                catch
                {
                    // Best effort.
                }
            }
        }
    }

    private static void WriteStartupTaskDefinition(string path, string installedExePath, string currentUser)
    {
        // Finish and close the writer before schtasks.exe opens the XML. A live
        // write handle causes ERROR_SHARING_VIOLATION even with FileShare.Read.
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var settings = new XmlWriterSettings { Encoding = Encoding.Unicode, Indent = true };
        using (XmlWriter writer = XmlWriter.Create(stream, settings))
        {
            CreateStartupTaskDefinition(installedExePath, currentUser).Save(writer);
        }
        stream.Flush(flushToDisk: true);
    }

    private static XDocument CreateStartupTaskDefinition(string installedExePath, string currentUser)
    {
        XNamespace taskNamespace = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        return new XDocument(
            new XDeclaration("1.0", "utf-16", null),
            new XElement(taskNamespace + "Task",
                new XAttribute("version", "1.3"),
                new XElement(taskNamespace + "RegistrationInfo",
                    new XElement(taskNamespace + "Description", "Launch QuickZoom at user logon with highest privileges.")),
                new XElement(taskNamespace + "Triggers",
                    new XElement(taskNamespace + "LogonTrigger",
                        new XElement(taskNamespace + "Enabled", true),
                        new XElement(taskNamespace + "UserId", currentUser))),
                new XElement(taskNamespace + "Principals",
                    new XElement(taskNamespace + "Principal",
                        new XAttribute("id", "Author"),
                        new XElement(taskNamespace + "UserId", currentUser),
                        new XElement(taskNamespace + "LogonType", "InteractiveToken"),
                        new XElement(taskNamespace + "RunLevel", "HighestAvailable"))),
                new XElement(taskNamespace + "Settings",
                    new XElement(taskNamespace + "MultipleInstancesPolicy", "IgnoreNew"),
                    new XElement(taskNamespace + "DisallowStartIfOnBatteries", false),
                    new XElement(taskNamespace + "StopIfGoingOnBatteries", false),
                    new XElement(taskNamespace + "StartWhenAvailable", true),
                    new XElement(taskNamespace + "ExecutionTimeLimit", "PT0S"),
                    new XElement(taskNamespace + "Priority", StartupTaskPriority)),
                new XElement(taskNamespace + "Actions",
                    new XAttribute("Context", "Author"),
                    new XElement(taskNamespace + "Exec",
                        new XElement(taskNamespace + "Command", installedExePath),
                        new XElement(taskNamespace + "Arguments", ElevatedFlag)))));
    }

    private static bool TryStartElevatedScheduledTask()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
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

            if (!ProcessOutput.TryRead(process, 3000, out string output, out string error))
            {
                ErrorLog.Write("StartupTaskRun", "Timed out while starting the elevated scheduled task.");
                return false;
            }

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

    private enum StartupTaskLaunchResult
    {
        Failed,
        Ready,
        Pending
    }

    private static StartupTaskLaunchResult StartElevatedScheduledTaskAndVerify()
    {
        if (!StartupTaskService.IsReadyForCurrentBuild(out string? targetExePath)) return StartupTaskLaunchResult.Failed;
        IDisposable? yielding = StartupHandoff.MarkYielding();
        if (yielding == null) return StartupTaskLaunchResult.Failed;
        bool replacementReady = false;
        bool launcherOwnsMutex = false;
        ReleaseSingleInstanceMutex();
        try
        {
            replacementReady = TryStartElevatedScheduledTask() &&
                WaitForOtherQuickZoomInstance(targetExePath, timeoutMs: 15000, pollMs: 250);
            if (!replacementReady)
                ErrorLog.Write("StartupTaskRun", "The startup task did not signal a ready replacement within the launch deadline.");
        }
        catch (Exception ex)
        {
            ErrorLog.Write("StartupTaskRun", ex);
        }
        finally
        {
            if (!replacementReady && _singleInstanceMutex == null)
            {
                launcherOwnsMutex = TryAcquireSingleInstanceMutex();
            }
            CompleteStartupYielding(yielding, replacementReady, launcherOwnsMutex);
        }

        StartupTaskLaunchResult result = GetStartupTaskLaunchResult(replacementReady, launcherOwnsMutex);
        if (result == StartupTaskLaunchResult.Pending)
            ErrorLog.Write("StartupTaskRun", "Another QuickZoom process still owns startup. The launcher is exiting without starting a duplicate runtime.");
        return result;
    }

    private static async Task<StartupTaskLaunchResult> StartElevatedScheduledTaskForSetupAsync()
    {
        string? targetExePath = null;
        if (!await Task.Run(() => StartupTaskService.IsReadyForCurrentBuild(out targetExePath)))
            return StartupTaskLaunchResult.Failed;
        IDisposable? yielding = StartupHandoff.MarkYielding();
        if (yielding == null) return StartupTaskLaunchResult.Failed;
        bool replacementReady = false;
        bool launcherOwnsMutex = false;
        ReleaseSingleInstanceMutex();
        try
        {
            // Only the blocking process operations run in the background; the
            // mutex belongs to the setup UI thread before and after this await.
            replacementReady = await Task.Run(() => TryStartElevatedScheduledTask() &&
                WaitForOtherQuickZoomInstance(targetExePath, timeoutMs: 15000, pollMs: 100));
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Setup.StartupTaskRun", ex);
        }
        finally
        {
            if (!replacementReady) launcherOwnsMutex = TryAcquireSingleInstanceMutex();
            CompleteStartupYielding(yielding, replacementReady, launcherOwnsMutex);
        }
        return GetStartupTaskLaunchResult(replacementReady, launcherOwnsMutex);
    }

    private static StartupTaskLaunchResult GetStartupTaskLaunchResult(bool replacementReady, bool launcherOwnsMutex)
        => replacementReady ? StartupTaskLaunchResult.Ready :
            launcherOwnsMutex ? StartupTaskLaunchResult.Failed : StartupTaskLaunchResult.Pending;

    private static void CompleteStartupYielding(IDisposable marker, bool replacementReady, bool launcherOwnsMutex)
    {
        if (replacementReady || launcherOwnsMutex)
        {
            marker.Dispose();
            return;
        }

        // The pending child may still be approaching its last arbitration check.
        // Keep this launcher marked as yielding until Windows closes the handle
        // on process exit, so neither process can mistake it for a runtime owner.
        _startupYieldingMarker = marker;
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

    private static bool WaitForOtherQuickZoomInstance(string? expectedExePath, int timeoutMs, int pollMs)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds <= timeoutMs)
        {
            if (HasOtherQuickZoomInstance(expectedExePath, requireReady: true))
            {
                return true;
            }

            Thread.Sleep(pollMs);
        }

        return false;
    }

    private static bool HasOtherQuickZoomInstance(string? expectedExePath, bool requireReady = false)
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

                if (!requireReady || StartupHandoff.IsReady(otherProcess)) return true;
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

                if (StartupHandoff.IsYielding(otherProcess)) continue;

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
        int currentBuildNumber = TryGetExecutableBuildNumber(currentExePath);
        if (currentBuildNumber <= 0)
        {
            currentBuildNumber = AppInfo.BuildNumber;
        }

        int otherBuildNumber = TryGetExecutableBuildNumber(otherExePath);
        if (currentBuildNumber > 0 && otherBuildNumber > 0 && currentBuildNumber != otherBuildNumber)
        {
            return currentBuildNumber > otherBuildNumber
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
        int fileBuildNumber = 0;
        int pathBuildNumber = 0;
        try
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(exePath);
            int metadataBuildNumber = Math.Max(info.FileBuildPart, info.FilePrivatePart);
            if (metadataBuildNumber > 0)
            {
                fileBuildNumber = metadataBuildNumber;
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
                    pathBuildNumber = buildNumber;
                    break;
                }

                directory = directory.Parent;
            }
        }
        catch
        {
            // Ignore path parsing failures.
        }

        return Math.Max(fileBuildNumber, pathBuildNumber);
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
            FileName = Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
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

        if (!ProcessOutput.TryRead(process, 5000, out string output, out _))
        {
            yield break;
        }

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
            FileName = Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
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

        if (!ProcessOutput.TryRead(process, 4000, out string output, out string error))
        {
            ErrorLog.Write("StartupCleanup.Task", "Timed out while deleting scheduled task '" + taskName + "'.");
            return false;
        }

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
