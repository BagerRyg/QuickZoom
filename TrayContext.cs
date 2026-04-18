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
    [DllImport("user32.dll")]
    private static extern int ShowCursor(bool bShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetSystemCursor(IntPtr hcur, uint id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateIconIndirect(ref ICONINFO piconinfo);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateBitmap(int nWidth, int nHeight, uint cPlanes, uint cBitsPerPel, IntPtr lpvBits);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    private static readonly uint[] CursorSystemIds =
    [
        32512, // OCR_NORMAL
        32513, // OCR_IBEAM
        32514, // OCR_WAIT
        32515, // OCR_CROSS
        32516, // OCR_UP
        32640, // OCR_SIZE
        32641, // OCR_ICON
        32642, // OCR_SIZENWSE
        32643, // OCR_SIZENESW
        32644, // OCR_SIZEWE
        32645, // OCR_SIZENS
        32646, // OCR_SIZEALL
        32648, // OCR_NO
        32649, // OCR_HAND
        32650, // OCR_APPSTARTING
        32651  // OCR_HELP
    ];

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
    private UiLanguage _language = UiText.GetStartupLanguage();

    // Hook + timers
    private IntPtr _hook = IntPtr.Zero;
    private IntPtr _kbdHook = IntPtr.Zero;
    private LowLevelMouseProc _proc = null!;
    private LowLevelKeyboardProc _kbdProc = null!;
    private System.Windows.Forms.Timer _followTimer = null!;
    private System.Windows.Forms.Timer _animTimer = null!;
    private System.Windows.Forms.Timer _cursorSpotlightTimer = null!;

    // Zoom state
    private int _zoomPercent = 100;
    private const int MinPercent = 100;
    private int _maxPercent = 400;
    private int _stepPercent = 25;
    private bool _enabled = true;
    private bool _followCursor = true;
    private bool _autoSwitchMonitor = true;
    private Screen? _lockedScreen;
    private bool _smoothZoom = true;
    private bool _autoDisableAt100 = true;
    private bool _invertEnabled;
    private bool _invertColors;
    private bool _magActive;
    private bool _useFullscreenBackend;
    private bool _centerCursor;
    private bool _wiggleSpotlightEnabled = true;

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
    private Keys _invertKey = Keys.I;
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
    private ModernButton? _resetDefaultsButton;
    private System.Windows.Forms.Timer? _resetDefaultsConfirmTimer;
    private bool _pendingResetDefaultsConfirmation;

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
    private CursorSpotlightOverlay? _cursorSpotlightOverlay;
    private readonly List<(long Tick, Point Point)> _recentCursorSamples = new();
    private long _lastCursorSpotlightTriggerTick;
    private long _cursorSpotlightVisibleUntilTick;
    private bool _cursorSpotlightHidesSystemCursor;
    private bool _cursorSpotlightOverridesSystemCursors;

    // Settings
    private readonly string _settingsPath = AppPaths.SettingsPath;
    private readonly string _legacySettingsPath = AppPaths.LegacySettingsPath;

    public TrayContext()
    {
        LoadSettings();
        RestoreSystemCursorScheme();
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
            _followTimer?.Dispose();
            _animTimer?.Stop();
            _animTimer?.Dispose();
            _cursorSpotlightTimer?.Stop();
            _cursorSpotlightTimer?.Dispose();
            _startupTimer?.Stop();
            _startupTimer?.Dispose();
            _shellMessageWindow?.Dispose();
            RestoreSystemCursorVisibility();
            _cursorSpotlightOverlay?.HideSpotlight();
            _cursorSpotlightOverlay?.Dispose();
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

    private string L(string key, params object[] args) => UiText.Get(_language, key, args);

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
