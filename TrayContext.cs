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

    // Refresh rate
    private int _fps = 60;
    private ToolStripMenuItem? _fpsMenu;
    private static readonly int[] _fpsOptions = [30, 40, 50, 60, 90, 120];
    private readonly HashSet<string> _selectedMonitorDeviceNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MonitorMagnifierWindow> _monitorWindows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Point> _lastAnchorByMonitor = new(StringComparer.OrdinalIgnoreCase);
    private bool _monitorLayoutDirty = true;

    // Settings
    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "QuickZoom",
        "settings.json");

    public TrayContext()
    {
        LoadSettings();
        BuildMenuAndTray();
        SubscribeThemeChanges();
        SubscribeDisplayChanges();
        InstallHook();
        InstallKeyboardHook();
        InitTimers();
        UpdateMenuLabels();
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
}
