using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickZoom;

internal sealed partial class TrayContext
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessage(string lpString);

    private void InitializeShellIntegration()
    {
        _taskbarCreatedMessage = unchecked((int)RegisterWindowMessage("TaskbarCreated"));
        _shellMessageWindow = new ShellMessageWindow(this, _taskbarCreatedMessage);
    }

    private void StartDeferredStartupIfNeeded()
    {
        if (IsShellReady())
        {
            CompleteStartupInitialization();
            return;
        }

        _startupTimer = new System.Windows.Forms.Timer
        {
            Interval = 1000
        };
        _startupTimer.Tick += (_, _) =>
        {
            if (!IsShellReady())
            {
                return;
            }

            _startupTimer?.Stop();
            _startupTimer?.Dispose();
            _startupTimer = null;
            CompleteStartupInitialization();
        };
        _startupTimer.Start();
    }

    private void CompleteStartupInitialization()
    {
        if (_startupInitialized)
        {
            RestoreTrayIcon();
            return;
        }

        BuildMenuAndTray();
        SubscribeThemeChanges();
        SubscribeDisplayChanges();
        InstallHook();
        InstallKeyboardHook();
        InitTimers();
        UpdateMenuLabels();
        _startupInitialized = true;
    }

    private void OnTaskbarCreated()
    {
        if (!_startupInitialized)
        {
            if (IsShellReady())
            {
                _startupTimer?.Stop();
                _startupTimer?.Dispose();
                _startupTimer = null;
                CompleteStartupInitialization();
            }

            return;
        }

        RestoreTrayIcon();
    }

    private static bool IsShellReady()
    {
        return FindWindow("Shell_TrayWnd", null) != IntPtr.Zero;
    }

    private void BuildMenuAndTray()
    {
        var menu = new ContextMenuStrip
        {
            ShowCheckMargin = true,
            ShowImageMargin = false
        };
        menu.Opening += (_, _) =>
        {
            UpdateStartupServiceStatusLabel();
            SuspendPerMonitorTracking();
        };
        menu.Closed += (_, _) => ResumePerMonitorTracking();
        _menu = menu;

        var enabledItem = new ToolStripMenuItem("Enabled") { Checked = _enabled, CheckOnClick = true };
        enabledItem.CheckedChanged += (_, _) =>
        {
            _enabled = enabledItem.Checked;
            if (!_enabled)
            {
                DisableMagAndReset();
            }

            SaveSettings();
        };

        var followItem = new ToolStripMenuItem("Follow Cursor") { Checked = _followCursor, CheckOnClick = true };
        followItem.CheckedChanged += (_, _) =>
        {
            _followCursor = followItem.Checked;
            if (_followCursor)
            {
                _followTimer.Start();
            }
            else
            {
                _followTimer.Stop();
            }

            SaveSettings();
        };

        _displayMenu = new ToolStripMenuItem("Magnified Displays");
        RebuildDisplayMenuItems();

        var autoSwitchItem = new ToolStripMenuItem("Auto-switch monitor") { Checked = _autoSwitchMonitor, CheckOnClick = true };
        autoSwitchItem.CheckedChanged += (_, _) =>
        {
            _autoSwitchMonitor = autoSwitchItem.Checked;
            if (_autoSwitchMonitor)
            {
                _lockedScreen = null;
            }
            else if (GetCursorPos(out var ptLock))
            {
                _lockedScreen = Screen.FromPoint(new Point(ptLock.X, ptLock.Y));
            }

            ApplyTransformCurrentPoint();
            SaveSettings();
        };

        var smoothItem = new ToolStripMenuItem("Smooth Zoom") { Checked = _smoothZoom, CheckOnClick = true };
        smoothItem.CheckedChanged += (_, _) => { _smoothZoom = smoothItem.Checked; SaveSettings(); };

        var centerItem = new ToolStripMenuItem("Center Cursor") { Checked = _centerCursor, CheckOnClick = true };
        centerItem.CheckedChanged += (_, _) => { _centerCursor = centerItem.Checked; SaveSettings(); };

        var autoDisableItem = new ToolStripMenuItem("Disable Magnifier at 100%") { Checked = _autoDisableAt100, CheckOnClick = true };
        autoDisableItem.CheckedChanged += (_, _) =>
        {
            _autoDisableAt100 = autoDisableItem.Checked;
            if (_autoDisableAt100 && _zoomPercent == 100)
            {
                DisableMagAndReset();
            }

            SaveSettings();
        };

        _stepItem = new ToolStripMenuItem($"Zoom Step (current: {_stepPercent}%)");
        foreach (var p in new[] { 5, 10, 12, 15, 20, 25 })
        {
            _stepItem.DropDownItems.Add($"{p}%", null, (_, _) =>
            {
                _stepPercent = p;
                UpdateMenuLabels();
                SaveSettings();
            });
        }

        _stepItem.DropDownItems.Add(new ToolStripSeparator());
        _stepItem.DropDownItems.Add("Custom...", null, (_, _) =>
        {
            var v = PromptForNumber("Enter zoom step in % (5-50):", _stepPercent, 5, 50);
            if (v != null)
            {
                _stepPercent = v.Value;
                UpdateMenuLabels();
                SaveSettings();
            }
        });

        _maxItem = new ToolStripMenuItem($"Max Zoom (current: {_maxPercent}%)");
        foreach (var p in new[] { 200, 250, 300, 350, 400 })
        {
            _maxItem.DropDownItems.Add($"{p}%", null, (_, _) =>
            {
                _maxPercent = p;
                ClampZoom();
                UpdateMenuLabels();
                SaveSettings();
            });
        }

        _maxItem.DropDownItems.Add(new ToolStripSeparator());
        _maxItem.DropDownItems.Add("Custom...", null, (_, _) =>
        {
            var v = PromptForNumber("Enter max zoom in % (150-600):", _maxPercent, 150, 600);
            if (v != null)
            {
                _maxPercent = v.Value;
                ClampZoom();
                UpdateMenuLabels();
                SaveSettings();
            }
        });

        _fpsMenu = new ToolStripMenuItem("Refresh Rate");
        foreach (var f in _fpsOptions)
        {
            var item = new ToolStripMenuItem($"{f} FPS") { Tag = f, CheckOnClick = true, Checked = f == _fps };
            item.Click += (_, _) =>
            {
                _fps = f;
                ApplyFps();
                RefreshFpsMenuChecks();
                SaveSettings();
            };
            _fpsMenu.DropDownItems.Add(item);
        }

        var enableKeyRoot = new ToolStripMenuItem("Enable Key");
        var preset = new ToolStripMenuItem("Preset");
        preset.DropDownItems.Add("Ctrl", null, (_, _) => { _enableKey = Keys.ControlKey; _enableKeyPressed = false; SaveSettings(); UpdateMenuLabels(); });
        preset.DropDownItems.Add("Alt", null, (_, _) => { _enableKey = Keys.Menu; _enableKeyPressed = false; SaveSettings(); UpdateMenuLabels(); });
        preset.DropDownItems.Add("Shift", null, (_, _) => { _enableKey = Keys.ShiftKey; _enableKeyPressed = false; SaveSettings(); UpdateMenuLabels(); });

        var customKey = new ToolStripMenuItem("Custom...");
        customKey.Click += (_, _) =>
        {
            var k = PromptForKey(_enableKey);
            if (k != null)
            {
                _enableKey = k.Value;
                _enableKeyPressed = false;
                SaveSettings();
                UpdateMenuLabels();
            }
        };

        _enableKeyItem = new ToolStripMenuItem($"Enable Key (hold): {KeyLabel(_enableKey)}");
        enableKeyRoot.DropDownItems.AddRange([_enableKeyItem, preset, customKey]);

        var resetCursorItem = new ToolStripMenuItem("Reset Cursor (fix pointer feel)");
        resetCursorItem.Click += (_, _) => { SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0); };

        _startupServiceStatusItem = new ToolStripMenuItem("Startup Service: Checking...")
        {
            Enabled = false
        };

        var aboutItem = new ToolStripMenuItem("About");
        aboutItem.Click += (_, _) => ShowAboutDialog();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitThread();

        menu.Items.Add(enabledItem);
        menu.Items.Add(followItem);
        menu.Items.Add(_displayMenu);
        menu.Items.Add(autoSwitchItem);
        menu.Items.Add(smoothItem);
        menu.Items.Add(autoDisableItem);
        menu.Items.Add(_stepItem);
        menu.Items.Add(_maxItem);
        menu.Items.Add(_fpsMenu);
        menu.Items.Add(enableKeyRoot);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(resetCursorItem);
        menu.Items.Add(_startupServiceStatusItem);
        menu.Items.Add(aboutItem);
        menu.Items.Add(exitItem);

        _iconRef = LoadEmbeddedIconBySuffix("magnifier_dark.ico")
                   ?? Icon.ExtractAssociatedIcon(Application.ExecutablePath)
                   ?? SystemIcons.Application;

        CreateNotifyIcon();

        ApplyThemeFromSystem(force: true);
        UpdateStartupServiceStatusLabel();
    }

    private void CreateNotifyIcon()
    {
        if (_menu == null)
        {
            return;
        }

        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }

        _tray = new NotifyIcon
        {
            Icon = _iconRef,
            Visible = true,
            Text = "QuickZoom",
            ContextMenuStrip = _menu
        };
    }

    private void RestoreTrayIcon()
    {
        if (_menu == null || !IsShellReady())
        {
            return;
        }

        CreateNotifyIcon();
        ApplyThemeFromSystem(force: true);
    }

    private Icon? LoadEmbeddedIconBySuffix(string suffix)
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            foreach (var name in asm.GetManifestResourceNames())
            {
                if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = asm.GetManifestResourceStream(name);
                    if (stream != null)
                    {
                        return new Icon(stream);
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private void ApplyFps()
    {
        int interval = Math.Max(5, 1000 / Math.Max(10, _fps));
        _followTimer.Interval = interval;

        if (_animTimer != null)
        {
            // Keep zoom animation updates aligned with display cadence to reduce jitter.
            _animTimer.Interval = Math.Max(8, 1000 / Math.Max(60, _fps));
        }
    }

    private void RefreshFpsMenuChecks()
    {
        if (_fpsMenu == null)
        {
            return;
        }

        foreach (ToolStripItem item in _fpsMenu.DropDownItems)
        {
            if (item is ToolStripMenuItem menuItem && int.TryParse(menuItem.Tag?.ToString(), out int f))
            {
                menuItem.Checked = f == _fps;
            }
        }
    }

    private void UpdateStartupServiceStatusLabel()
    {
        if (_startupServiceStatusItem != null)
        {
            _startupServiceStatusItem.Text = StartupTaskService.GetStatusLabel();
        }
    }

    private void InitTimers()
    {
        _followTimer = new System.Windows.Forms.Timer();
        ApplyFps();
        _followTimer.Tick += (_, _) =>
        {
            if (!_enabled || _zoomPercent <= 100)
            {
                return;
            }

            if (!_followCursor || !_magActive)
            {
                return;
            }

            UpdateShellUiTrackingState();

            if (IsPerMonitorTrackingSuspended && !_useFullscreenBackend)
            {
                return;
            }

            if (_animTimer != null && _animTimer.Enabled)
            {
                // Animation ticks already update transform continuously.
                return;
            }

            if (GetCursorPos(out var pt))
            {
                ApplyTransformAtPoint(pt, PercentToMag(_zoomPercent));
            }
        };

        if (_followCursor)
        {
            _followTimer.Start();
        }

        _animTimer = new System.Windows.Forms.Timer();
        ApplyFps();
        _animTimer.Tick += (_, _) =>
        {
            if (_zoomPercent == _animTargetPercent)
            {
                _animTimer.Stop();
                return;
            }

            _animElapsedMs += _animTimer.Interval;
            double t = Math.Min(1.0, _animElapsedMs / (double)_animDurationMs);
            double ease = t * t * (3 - 2 * t);
            _zoomPercent = (int)Math.Round(_animStartPercent + ((_animTargetPercent - _animStartPercent) * ease));
            ApplyTransformCurrentPoint();

            if (t >= 1.0)
            {
                _animTimer.Stop();
            }
        };
    }

    private void SuspendPerMonitorTracking()
    {
        if (_useFullscreenBackend)
        {
            return;
        }

        _suspendPerMonitorTrackingForMenu = true;
        SetPerMonitorWindowsVisible(false);
        _animTimer?.Stop();
    }

    private void ResumePerMonitorTracking()
    {
        if (!_suspendPerMonitorTrackingForMenu)
        {
            return;
        }

        _suspendPerMonitorTrackingForMenu = false;
        SetPerMonitorWindowsVisible(!IsPerMonitorTrackingSuspended);

        if (!IsPerMonitorTrackingSuspended && _magActive && _zoomPercent > 100)
        {
            ApplyTransformCurrentPoint();
        }
    }
}
