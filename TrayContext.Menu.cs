using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace QuickZoom;

internal sealed partial class TrayContext
{
    private void BuildMenuAndTray()
    {
        var menu = new ContextMenuStrip
        {
            ShowCheckMargin = true,
            ShowImageMargin = false
        };
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
        preset.DropDownItems.Add("Ctrl", null, (_, _) => { _enableKey = Keys.ControlKey; SaveSettings(); UpdateMenuLabels(); });
        preset.DropDownItems.Add("Alt", null, (_, _) => { _enableKey = Keys.Menu; SaveSettings(); UpdateMenuLabels(); });
        preset.DropDownItems.Add("Shift", null, (_, _) => { _enableKey = Keys.ShiftKey; SaveSettings(); UpdateMenuLabels(); });

        var customKey = new ToolStripMenuItem("Custom...");
        customKey.Click += (_, _) =>
        {
            var k = PromptForKey(_enableKey);
            if (k != null)
            {
                _enableKey = k.Value;
                SaveSettings();
                UpdateMenuLabels();
            }
        };

        _enableKeyItem = new ToolStripMenuItem($"Enable Key (hold): {KeyLabel(_enableKey)}");
        enableKeyRoot.DropDownItems.AddRange([_enableKeyItem, preset, customKey]);

        var resetCursorItem = new ToolStripMenuItem("Reset Cursor (fix pointer feel)");
        resetCursorItem.Click += (_, _) => { SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0); };

        var aboutItem = new ToolStripMenuItem("About");
        aboutItem.Click += (_, _) => ShowAboutDialog();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitThread();

        menu.Items.Add(enabledItem);
        menu.Items.Add(followItem);
        menu.Items.Add(autoSwitchItem);
        menu.Items.Add(smoothItem);
        menu.Items.Add(autoDisableItem);
        menu.Items.Add(_stepItem);
        menu.Items.Add(_maxItem);
        menu.Items.Add(_fpsMenu);
        menu.Items.Add(enableKeyRoot);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(resetCursorItem);
        menu.Items.Add(aboutItem);
        menu.Items.Add(exitItem);

        _iconRef = LoadEmbeddedIconBySuffix("magnifier_dark.ico")
                   ?? Icon.ExtractAssociatedIcon(Application.ExecutablePath)
                   ?? SystemIcons.Application;

        _tray = new NotifyIcon
        {
            Icon = _iconRef,
            Visible = true,
            Text = "QuickZoom",
            ContextMenuStrip = menu
        };

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

            if (GetCursorPos(out var pt))
            {
                ApplyTransformAtPoint(pt, PercentToMag(_zoomPercent));
            }
        };

        if (_followCursor)
        {
            _followTimer.Start();
        }

        _animTimer = new System.Windows.Forms.Timer { Interval = 10 };
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
}
