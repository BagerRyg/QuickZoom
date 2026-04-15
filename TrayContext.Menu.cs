using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickZoom;

internal sealed partial class TrayContext
{
    private const int TrayContentLogicalWidth = 300;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessage(string lpString);

    private void InitializeShellIntegration()
    {
        _taskbarCreatedMessage = unchecked((int)RegisterWindowMessage("TaskbarCreated"));
        _shellMessageWindow = new ShellMessageWindow(this, _taskbarCreatedMessage);
    }

    private void InitializeCoreRuntime()
    {
        if (_coreRuntimeInitialized)
        {
            return;
        }

        ApplyThemePreference(force: true);
        SubscribeThemeChanges();
        SubscribeDisplayChanges();
        InstallHook();
        InstallKeyboardHook();
        InitTimers();
        UpdateMenuLabels();
        _coreRuntimeInitialized = true;
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
        ApplyThemePreference(force: true);
        CreateNotifyIcon();
        UpdateStartupServiceStatusLabel();
    }

    private void CreateNotifyIcon()
    {
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }

        _iconRef ??= LoadEmbeddedIconBySuffix("magnifier_dark.ico")
                    ?? Icon.ExtractAssociatedIcon(Application.ExecutablePath)
                    ?? SystemIcons.Application;

        _tray = new NotifyIcon
        {
            Icon = _iconRef,
            Visible = true,
            Text = "QuickZoom"
        };
        _tray.MouseUp += OnTrayMouseUp;
    }

    private void RestoreTrayIcon()
    {
        if (!IsShellReady())
        {
            return;
        }

        CreateNotifyIcon();
        if (_trayPopup != null && !_trayPopup.IsDisposed)
        {
            RebuildTrayPopupIfOpen();
        }
    }

    private void OnTrayMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button is not (MouseButtons.Left or MouseButtons.Right))
        {
            return;
        }

        ToggleTrayPopup(Cursor.Position);
    }

    private void ToggleTrayPopup(Point anchor)
    {
        if (_trayPopup != null && !_trayPopup.IsDisposed)
        {
            CloseTrayPopup();
            return;
        }

        ShowTrayPopup(anchor);
    }

    private void ShowTrayPopup(Point anchor)
    {
        CloseTrayPopup();
        _lastTrayPopupAnchor = anchor;

        ThemePalette palette = CurrentTheme;
        var popup = new TrayPopupWindow(palette);
        int trayContentWidth = ControlDrawing.ScaleLogical(popup, TrayContentLogicalWidth);
        popup.FormClosed += (_, _) =>
        {
            _magnifyRow = null;
            _invertRow = null;
            _followRow = null;
            _exitRow = null;
            _magnifyToggle = null;
            _invertToggle = null;
            _followToggle = null;
            _displayRow = null;
            _displayOptionsHost = null;
            _startupServiceStatusLabel = null;
            _trayPopup = null;
            _pendingExitConfirmation = false;
            ResumePerMonitorTracking();
        };

        var root = popup.ContentHost;
        root.SuspendLayout();
        root.Controls.Clear();
        root.MinimumSize = new Size(trayContentWidth, 0);
        root.MaximumSize = new Size(trayContentWidth, 0);
        root.Width = trayContentWidth;

        root.Controls.Add(CreateTrayHeader(palette, trayContentWidth));

        var quickSection = CreateTraySectionLabel(L("Tray.SectionQuick"), palette, trayContentWidth);
        root.Controls.Add(quickSection);

        var quickActions = CreateTrayStack(trayContentWidth);

        _magnifyToggle = new ToggleSwitchControl(palette) { IsOn = _enabled };
        _magnifyRow = new TrayMenuRow(palette, L("Tray.ToggleMagnify"), toggle: _magnifyToggle, icon: TrayFluentIcon.Enabled);
        _magnifyRow.Width = trayContentWidth;
        _magnifyRow.ActionRequested += (_, _) => ExecuteTrayAction(() => SetEnabledState(!_enabled));
        quickActions.Controls.Add(_magnifyRow);

        _invertToggle = new ToggleSwitchControl(palette) { IsOn = _invertEnabled };
        _invertRow = new TrayMenuRow(palette, L("Tray.ToggleInvert"), toggle: _invertToggle, icon: TrayFluentIcon.InvertColors);
        _invertRow.Width = trayContentWidth;
        _invertRow.ActionRequested += (_, _) => ExecuteTrayAction(() => SetInvertEnabledState(!_invertEnabled));
        quickActions.Controls.Add(_invertRow);

        _followToggle = new ToggleSwitchControl(palette) { IsOn = _followCursor };
        _followRow = new TrayMenuRow(palette, L("Tray.ToggleFollow"), toggle: _followToggle, icon: TrayFluentIcon.FollowCursor);
        _followRow.Width = trayContentWidth;
        _followRow.ActionRequested += (_, _) => ExecuteTrayAction(() => SetFollowCursor(!_followCursor));
        quickActions.Controls.Add(_followRow);

        root.Controls.Add(quickActions);

        var divider = new TrayMenuDivider(palette) { Dock = DockStyle.Top, Width = trayContentWidth };
        root.Controls.Add(divider);

        var actionsSection = CreateTraySectionLabel(L("Tray.SectionMenu"), palette, trayContentWidth);
        root.Controls.Add(actionsSection);

        var actions = CreateTrayStack(trayContentWidth);

        _displayRow = new TrayMenuRow(palette, L("Tray.MagnifiedDisplays"), icon: TrayFluentIcon.MagnifiedDisplays);
        _displayRow.Width = trayContentWidth;
        _displayRow.ActionRequested += (_, _) => ToggleDisplayOptions();
        actions.Controls.Add(_displayRow);

        _displayOptionsHost = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 4),
            Padding = new Padding(ControlDrawing.ScaleLogical(popup, 10), 0, 0, 0),
            BackColor = Color.Transparent,
            Width = trayContentWidth,
            MinimumSize = new Size(trayContentWidth, 0),
            Visible = false
        };
        actions.Controls.Add(_displayOptionsHost);
        PopulateDisplayOptionsHost();

        var keyBindsRow = new TrayMenuRow(palette, L("Tray.KeyBinds"), icon: TrayFluentIcon.KeyBinds);
        keyBindsRow.Width = trayContentWidth;
        keyBindsRow.ActionRequested += (_, _) => ExecuteTrayAction(() =>
        {
            CloseTrayPopup();
            ShowSettingsWindow(SettingsPage.Input);
        });
        actions.Controls.Add(keyBindsRow);

        var settingsRow = new TrayMenuRow(palette, L("Tray.Settings"), icon: TrayFluentIcon.Settings);
        settingsRow.Width = trayContentWidth;
        settingsRow.ActionRequested += (_, _) => ExecuteTrayAction(() =>
        {
            CloseTrayPopup();
            ShowSettingsWindow(SettingsPage.General);
        });
        actions.Controls.Add(settingsRow);

        var resetCursorRow = new TrayMenuRow(palette, L("Tray.ResetCursor"), icon: TrayFluentIcon.ResetCursor);
        resetCursorRow.Width = trayContentWidth;
        resetCursorRow.ActionRequested += (_, _) => ExecuteTrayAction(() =>
        {
            ResetExitConfirmation();
            SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
        });
        actions.Controls.Add(resetCursorRow);

        var aboutRow = new TrayMenuRow(palette, L("Tray.About"), icon: TrayFluentIcon.About);
        aboutRow.Width = trayContentWidth;
        aboutRow.ActionRequested += (_, _) => ExecuteTrayAction(() =>
        {
            CloseTrayPopup();
            ShowSettingsWindow(SettingsPage.About);
        });
        actions.Controls.Add(aboutRow);

        _exitRow = new TrayMenuRow(palette, L("Tray.Exit"), icon: TrayFluentIcon.Exit);
        _exitRow.Width = trayContentWidth;
        _exitRow.IsDestructive = true;
        _exitRow.ActionRequested += (_, _) => ExecuteTrayAction(HandleExitRequested);
        actions.Controls.Add(_exitRow);

        root.Controls.Add(actions);

        var footer = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Width = trayContentWidth,
            Margin = new Padding(0, 4, 0, 0),
            Padding = new Padding(8, 0, 8, 0),
            BackColor = Color.Transparent
        };
        _startupServiceStatusLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Left,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = palette.SecondaryText,
            BackColor = Color.Transparent,
            Text = StartupTaskService.GetStatusLabel(_language)
        };
        footer.Controls.Add(_startupServiceStatusLabel);
        root.Controls.Add(footer);
        root.ResumeLayout(performLayout: true);

        _trayPopup = popup;
        UpdateTrayPopupState();
        SuspendPerMonitorTracking();
        popup.ShowAnchored(anchor);
        popup.RefreshAnchoredLayout(anchor);
    }

    private Control CreateTrayHeader(ThemePalette palette, int contentWidth)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(0),
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 1,
            Width = contentWidth,
            MinimumSize = new Size(contentWidth, 0)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "QuickZoom",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 12.75f, FontStyle.Bold),
            ForeColor = palette.Text,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        panel.Controls.Add(title, 0, 0);
        return panel;
    }

    private static FlowLayoutPanel CreateTrayStack(int width)
    {
        return new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Top,
            Width = width,
            MinimumSize = new Size(width, 0),
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };
    }

    private static TrayMenuSectionLabel CreateTraySectionLabel(string text, ThemePalette palette, int width)
    {
        var label = new TrayMenuSectionLabel
        {
            Text = text,
            MaximumSize = new Size(width - 16, 0)
        };
        label.ApplyTheme(palette);
        return label;
    }

    private void ToggleDisplayOptions()
    {
        ResetExitConfirmation();
        if (_displayOptionsHost == null || _displayRow == null)
        {
            return;
        }

        _displayOptionsHost.Visible = !_displayOptionsHost.Visible;
        _displayRow.Active = _displayOptionsHost.Visible;
        _trayPopup?.RefreshAnchoredLayout(_lastTrayPopupAnchor == Point.Empty ? Cursor.Position : _lastTrayPopupAnchor);
    }

    private string GetOnOffText(bool value) => value ? L("Common.On") : L("Common.Off");

    private void RebuildTrayPopupIfOpen()
    {
        if (_trayPopup == null || _trayPopup.IsDisposed)
        {
            return;
        }

        Point anchor = _lastTrayPopupAnchor == Point.Empty ? Cursor.Position : _lastTrayPopupAnchor;
        ShowTrayPopup(anchor);
    }

    private void CloseTrayPopup()
    {
        if (_trayPopup == null || _trayPopup.IsDisposed)
        {
            return;
        }

        try
        {
            _trayPopup.Close();
            _trayPopup.Dispose();
        }
        catch
        {
            // Ignore shutdown races.
        }
        finally
        {
            _trayPopup = null;
        }
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
            _animTimer.Interval = Math.Max(8, 1000 / Math.Max(60, _fps));
        }
    }

    private void UpdateStartupServiceStatusLabel()
    {
        if (_startupServiceStatusLabel != null)
        {
            _startupServiceStatusLabel.Text = StartupTaskService.GetStatusLabel(_language);
        }
    }

    private void UpdateTrayPopupState()
    {
        if (_trayPopup == null || _trayPopup.IsDisposed)
        {
            return;
        }

        ThemePalette palette = CurrentTheme;
        if (_magnifyRow != null && _magnifyToggle != null)
        {
            _magnifyToggle.IsOn = _enabled;
            _magnifyRow.ApplyTheme(palette);
        }

        if (_invertRow != null && _invertToggle != null)
        {
            _invertToggle.IsOn = _invertEnabled;
            _invertRow.ApplyTheme(palette);
        }

        if (_followRow != null && _followToggle != null)
        {
            _followToggle.IsOn = _followCursor;
            _followRow.ApplyTheme(palette);
        }

        if (_displayRow != null)
        {
            _displayRow.ApplyTheme(palette);
        }

        if (_exitRow != null)
        {
            _exitRow.Title = _pendingExitConfirmation ? L("Tray.ExitConfirm") : L("Tray.Exit");
            _exitRow.Active = _pendingExitConfirmation;
            _exitRow.ApplyTheme(palette);
        }

        UpdateStartupServiceStatusLabel();
        PopulateDisplayOptionsHost();
        _trayPopup.RefreshAnchoredLayout(_lastTrayPopupAnchor == Point.Empty ? Cursor.Position : _lastTrayPopupAnchor);
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

        if (!IsPerMonitorTrackingSuspended && _magActive && (_zoomPercent > 100 || _invertColors))
        {
            ApplyTransformCurrentPoint();
        }
    }

    private void SetEnabledState(bool enabled)
    {
        ResetExitConfirmation();
        _enabled = enabled;
        if (!_enabled)
        {
            _zoomPercent = 100;
            _animTargetPercent = 100;
            _animTimer?.Stop();
            if (_invertColors)
            {
                ApplyTransformCurrentPoint();
            }
            else
            {
                DisableMagAndReset();
            }
        }
        else if (_invertColors || _zoomPercent > 100)
        {
            ApplyTransformCurrentPoint();
        }

        SaveSettings();
        RefreshMenuAndTrayUi();
    }

    private void SetInvertEnabledState(bool enabled)
    {
        ResetExitConfirmation();
        _invertEnabled = enabled;
        if (!_invertEnabled)
        {
            _invertColors = false;
            if (_zoomPercent > 100)
            {
                ApplyTransformCurrentPoint();
            }
            else
            {
                DisableMagAndReset();
            }
        }

        SaveSettings();
        RefreshMenuAndTrayUi();
    }

    private void SetFollowCursor(bool followCursor)
    {
        ResetExitConfirmation();
        _followCursor = followCursor;
        if (_followCursor)
        {
            _followTimer?.Start();
        }
        else
        {
            _followTimer?.Stop();
        }

        SaveSettings();
        RefreshMenuAndTrayUi();
    }

    private void RefreshMenuAndTrayUi(bool rebuildPopup = false)
    {
        if (rebuildPopup)
        {
            RebuildTrayPopupIfOpen();
            return;
        }

        UpdateTrayPopupState();
    }

    private void HandleExitRequested()
    {
        if (!_pendingExitConfirmation)
        {
            _pendingExitConfirmation = true;
            RefreshMenuAndTrayUi();
            return;
        }

        ExitThread();
    }

    private void ResetExitConfirmation()
    {
        if (!_pendingExitConfirmation)
        {
            return;
        }

        _pendingExitConfirmation = false;
        UpdateTrayPopupState();
    }
}

