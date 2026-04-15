using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;

namespace QuickZoom;

internal sealed partial class TrayContext : ApplicationContext
{
    private enum ThemeMode
    {
        AutoSystem = 0,
        Dark = 1,
        Light = 2
    }

    private enum InvertTriggerKind
    {
        EnableKeyPlusMiddleClick = 0,
        EnableKeyPlusXButton1 = 1,
        EnableKeyPlusXButton2 = 2,
        CustomKey = 3
    }

    private NotifyIcon _tray = null!;
    private Icon? _iconRef;
    private Control _uiInvoker = null!;
    private TrayPopupWindow? _trayPopup;
    private bool _useDarkTheme;
    private ThemeMode _themeMode = ThemeMode.AutoSystem;
    private UiLanguage _language = UiText.GetDefaultLanguage();

    // Hook + timers
    private IntPtr _hook = IntPtr.Zero;
    private IntPtr _kbdHook = IntPtr.Zero;
    private LowLevelMouseProc _proc = null!;
    private LowLevelKeyboardProc _kbdProc = null!;
    private System.Windows.Forms.Timer _followTimer = null!;
    private System.Windows.Forms.Timer _animTimer = null!;

    // Zoom state
    private int _zoomPercent = 100;
    private const int MinPercent = 100;
    private int _maxPercent = 300;
    private int _stepPercent = 15;
    private bool _enabled = true;
    private bool _followCursor = true;
    private bool _autoSwitchMonitor = true;
    private Screen? _lockedScreen;
    private bool _smoothZoom = true;
    private bool _autoDisableAt100 = true;
    private bool _invertEnabled = true;
    private bool _invertColors;
    private bool _magActive;
    private bool _useFullscreenBackend;
    private bool _centerCursor;

    // Animation
    private int _animDurationMs = 140;
    private int _animElapsedMs;
    private int _animStartPercent = 100;
    private int _animTargetPercent = 100;
    private int _wheelDeltaRemainder;
    private bool _enableKeyPressed;
    private bool _invertKeyPressed;
    private bool _pendingExitConfirmation;

    private POINT _staticCenter;
    private Keys _enableKey = Keys.Menu;
    private Keys _invertKey = Keys.Menu;
    private InvertTriggerKind _invertTrigger = InvertTriggerKind.EnableKeyPlusMiddleClick;

    // Tray popup refs
    private TrayMenuRow? _magnifyRow;
    private TrayMenuRow? _invertRow;
    private TrayMenuRow? _followRow;
    private TrayMenuRow? _exitRow;
    private ToggleSwitchControl? _magnifyToggle;
    private ToggleSwitchControl? _invertToggle;
    private ToggleSwitchControl? _followToggle;
    private TrayMenuRow? _displayRow;
    private FlowLayoutPanel? _displayOptionsHost;
    private Label? _startupServiceStatusLabel;
    private Point _lastTrayPopupAnchor;
    private Form? _settingsWindow;
    private Action<SettingsPage>? _selectSettingsPageAction;
    private SettingsPage _currentSettingsPage = SettingsPage.General;

    // Refresh rate
    private int _fps = 120;
    private static readonly int[] _fpsOptions = [30, 40, 50, 60, 90, 120];
    private readonly HashSet<string> _selectedMonitorDeviceNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MonitorMagnifierWindow> _monitorWindows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Point> _lastAnchorByMonitor = new(StringComparer.OrdinalIgnoreCase);
    private bool _useCursorMonitorSelection;
    private bool _suspendPerMonitorTrackingForMenu;
    private bool _suspendPerMonitorTrackingForShellUi;
    private bool _monitorLayoutDirty = true;
    private bool _coreRuntimeInitialized;
    private bool _startupInitialized;
    private bool _magInitializationFailureLogged;
    private System.Windows.Forms.Timer? _startupTimer;
    private ShellMessageWindow? _shellMessageWindow;
    private int _taskbarCreatedMessage;

    // Settings
    private readonly string _settingsPath = AppPaths.SettingsPath;
    private readonly string _legacySettingsPath = AppPaths.LegacySettingsPath;

    public TrayContext()
    {
        LoadSettings();
        _uiInvoker = new Control();
        _uiInvoker.CreateControl();
        InitializeCoreRuntime();
        InitializeShellIntegration();
        StartDeferredStartupIfNeeded();
    }

    protected override void ExitThreadCore()
    {
        try
        {
            DisableMagAndReset();

            if (_hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hook);
            }

            if (_kbdHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_kbdHook);
            }

            _followTimer?.Stop();
            _animTimer?.Stop();
            _startupTimer?.Stop();
            _startupTimer?.Dispose();
            _shellMessageWindow?.Dispose();
            CloseTrayPopup();
            if (_settingsWindow != null && !_settingsWindow.IsDisposed)
            {
                _settingsWindow.Close();
                _settingsWindow.Dispose();
            }

            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
            }

            UnsubscribeThemeChanges();
            UnsubscribeDisplayChanges();
            _iconRef?.Dispose();
            _uiInvoker.Dispose();
        }
        finally
        {
            base.ExitThreadCore();
        }
    }

    private sealed class ShellMessageWindow : NativeWindow, IDisposable
    {
        private readonly TrayContext _owner;
        private readonly int _taskbarCreatedMessage;

        public ShellMessageWindow(TrayContext owner, int taskbarCreatedMessage)
        {
            _owner = owner;
            _taskbarCreatedMessage = taskbarCreatedMessage;
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == _taskbarCreatedMessage)
            {
                _owner.OnTaskbarCreated();
            }

            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                DestroyHandle();
            }
        }
    }

    private string L(string key) => UiText.Get(_language, key);

    private void ExecuteTrayAction(Action action)
    {
        try
        {
            if (_uiInvoker.IsDisposed)
            {
                return;
            }

            void RunSafely()
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    LogTrayFailure(ex);
                }
            }

            if (_uiInvoker.InvokeRequired)
            {
                _uiInvoker.BeginInvoke((MethodInvoker)RunSafely);
            }
            else
            {
                RunSafely();
            }
        }
        catch (Exception ex)
        {
            LogTrayFailure(ex);
        }
    }

    private void LogTrayFailure(Exception ex)
    {
        ErrorLog.Write("TrayContext", ex);
    }

    private void RunOnUiThread(Action action)
    {
        try
        {
            if (_uiInvoker.IsDisposed)
            {
                return;
            }

            if (_uiInvoker.InvokeRequired)
            {
                _uiInvoker.BeginInvoke((MethodInvoker)(() => action()));
            }
            else
            {
                action();
            }
        }
        catch
        {
            // Ignore shutdown races.
        }
    }
}
