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
    private NotifyIcon _tray = null!;
    private Icon? _iconRef;
    private ContextMenuStrip? _menu;
    private bool _useDarkTheme;

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

    private POINT _staticCenter;
    private Keys _enableKey = Keys.ControlKey;

    // Menu refs
    private ToolStripMenuItem? _stepItem;
    private ToolStripMenuItem? _maxItem;
    private ToolStripMenuItem? _enableKeyItem;
    private ToolStripMenuItem? _displayMenu;
    private ToolStripMenuItem? _startupServiceStatusItem;

    // Refresh rate
    private int _fps = 60;
    private ToolStripMenuItem? _fpsMenu;
    private static readonly int[] _fpsOptions = [30, 40, 50, 60, 90, 120];
    private readonly HashSet<string> _selectedMonitorDeviceNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MonitorMagnifierWindow> _monitorWindows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Point> _lastAnchorByMonitor = new(StringComparer.OrdinalIgnoreCase);
    private bool _useCursorMonitorSelection;
    private bool _suspendPerMonitorTrackingForMenu;
    private bool _suspendPerMonitorTrackingForShellUi;
    private bool _monitorLayoutDirty = true;
    private bool _startupInitialized;
    private System.Windows.Forms.Timer? _startupTimer;
    private ShellMessageWindow? _shellMessageWindow;
    private int _taskbarCreatedMessage;

    // Settings
    private readonly string _settingsPath = AppPaths.SettingsPath;
    private readonly string _legacySettingsPath = AppPaths.LegacySettingsPath;

    public TrayContext()
    {
        LoadSettings();
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

            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
            }

            UnsubscribeThemeChanges();
            UnsubscribeDisplayChanges();
            _iconRef?.Dispose();
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
}
