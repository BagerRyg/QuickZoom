using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
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
        SignalStartupReady();
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
            Text = L("Common.AppName")
        };
        _tray.MouseUp += OnTrayMouseUp;
    }

    private void SignalStartupReady()
    {
        if (string.IsNullOrWhiteSpace(_startupReadyEventName))
        {
            return;
        }

        try
        {
            using EventWaitHandle readyEvent = EventWaitHandle.OpenExisting(_startupReadyEventName);
            readyEvent.Set();
            ErrorLog.Write("Startup", "Signaled startup readiness.");
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Startup", "Could not signal startup readiness. " + ex.Message);
        }
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
        float fontScale = ControlDrawing.UiFontScale;
        int trayLogicalWidth = TrayContentLogicalWidth + (int)Math.Round((fontScale - 1f) * 220f);
        int trayContentWidth = ControlDrawing.ScaleLogical(popup, trayLogicalWidth);
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
            ShowTrayRowDone(resetCursorRow, L("Tray.ResetCursor"));
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
            Font = ControlDrawing.UiFont("Segoe UI", 8.5f, FontStyle.Regular),
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
            Text = L("Common.AppName"),
            AutoSize = true,
            Font = ControlDrawing.UiFont("Segoe UI Semibold", 12.75f, FontStyle.Bold),
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

    private void ShowTrayRowDone(TrayMenuRow row, string defaultTitle)
    {
        row.Title = "Done.";
        row.IsSuccess = true;
        var timer = new System.Windows.Forms.Timer { Interval = 2500 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            if (row.IsDisposed)
            {
                return;
            }

            row.Title = defaultTitle;
            row.IsSuccess = false;
        };
        timer.Start();
    }

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
        int effectiveFps = GetEffectiveRenderingFps();
        int interval = Math.Max(5, 1000 / Math.Max(10, effectiveFps));
        _followTimer.Interval = interval;

        if (_animTimer != null)
        {
            _animTimer.Interval = Math.Max(8, 1000 / Math.Max(60, effectiveFps));
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

    private void UpdateTrayQuickToggleState()
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
                _animAnchorValid = false;
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
                _animAnchorValid = false;
                _animTimer.Stop();
            }
        };

        _cursorSpotlightTimer = new System.Windows.Forms.Timer
        {
            Interval = 20
        };
        _cursorSpotlightTimer.Tick += (_, _) => HandleCursorSpotlightTick();
        _cursorSpotlightTimer.Start();
    }

    private void HandleCursorSpotlightTick()
    {
        const int spotlightHoldDurationMs = 2000;
        const int spotlightShrinkDurationMs = 220;
        const int spotlightTotalDurationMs = spotlightHoldDurationMs + spotlightShrinkDurationMs;
        long now = Environment.TickCount64;
        bool isVisible = _cursorSpotlightVisibleUntilTick > now;

        if (!GetCursorPos(out var pt))
        {
            RestoreSystemCursorVisibility();
            if (_trayPopup != null && !_trayPopup.IsDisposed)
            {
                _trayPopup.IgnoreDeactivateClose = false;
            }
            if (!isVisible)
            {
                _cursorSpotlightOverlay?.HideSpotlight();
            }

            return;
        }

        Point currentPoint = new(pt.X, pt.Y);
        PruneCursorSamples(now);

        if (_wiggleSpotlightEnabled)
        {
            AddCursorSample(now, currentPoint);
            if (!isVisible &&
                now - _lastCursorSpotlightTriggerTick >= 1500 &&
                ShouldTriggerCursorSpotlight())
            {
                _lastCursorSpotlightTriggerTick = now;
                _cursorSpotlightVisibleUntilTick = now + spotlightTotalDurationMs;
                isVisible = true;
            }
        }
        else
        {
            _recentCursorSamples.Clear();
            _cursorSpotlightVisibleUntilTick = 0;
            isVisible = false;
        }

        if (!isVisible)
        {
            RestoreSystemCursorVisibility();
            if (_trayPopup != null && !_trayPopup.IsDisposed)
            {
                _trayPopup.IgnoreDeactivateClose = false;
            }
            _cursorSpotlightOverlay?.HideSpotlight();
            return;
        }

        if (_trayPopup != null && !_trayPopup.IsDisposed)
        {
            _trayPopup.IgnoreDeactivateClose = true;
        }

        long remainingMs = _cursorSpotlightVisibleUntilTick - now;
        double progress = remainingMs > spotlightShrinkDurationMs
            ? 0d
            : 1d - (remainingMs / (double)spotlightShrinkDurationMs);
        _cursorSpotlightOverlay ??= new CursorSpotlightOverlay();
        bool overlayVisible = _cursorSpotlightOverlay.UpdateSpotlight(currentPoint, progress);
        if (overlayVisible)
        {
            EnsureSystemCursorHidden();
        }
        else
        {
            RestoreSystemCursorVisibility();
        }
    }

    private void AddCursorSample(long now, Point point)
    {
        if (_recentCursorSamples.Count > 0)
        {
            Point last = _recentCursorSamples[_recentCursorSamples.Count - 1].Point;
            if (last == point)
            {
                return;
            }
        }

        _recentCursorSamples.Add((now, point));
    }

    private void PruneCursorSamples(long now)
    {
        while (_recentCursorSamples.Count > 0 && now - _recentCursorSamples[0].Tick > 1000)
        {
            _recentCursorSamples.RemoveAt(0);
        }
    }

    private bool ShouldTriggerCursorSpotlight()
    {
        if (_recentCursorSamples.Count < 5)
        {
            return false;
        }

        long durationMs = _recentCursorSamples[_recentCursorSamples.Count - 1].Tick - _recentCursorSamples[0].Tick;
        if (durationMs < 550)
        {
            return false;
        }

        double pathLength = 0d;
        int minX = _recentCursorSamples[0].Point.X;
        int maxX = minX;
        int minY = _recentCursorSamples[0].Point.Y;
        int maxY = minY;
        int xChanges = 0;
        int yChanges = 0;
        int prevDirX = 0;
        int prevDirY = 0;

        for (int i = 1; i < _recentCursorSamples.Count; i++)
        {
            Point prev = _recentCursorSamples[i - 1].Point;
            Point current = _recentCursorSamples[i].Point;
            int dx = current.X - prev.X;
            int dy = current.Y - prev.Y;
            pathLength += Math.Sqrt((dx * dx) + (dy * dy));

            minX = Math.Min(minX, current.X);
            maxX = Math.Max(maxX, current.X);
            minY = Math.Min(minY, current.Y);
            maxY = Math.Max(maxY, current.Y);

            int dirX = Math.Abs(dx) >= 10 ? Math.Sign(dx) : 0;
            int dirY = Math.Abs(dy) >= 10 ? Math.Sign(dy) : 0;

            if (dirX != 0)
            {
                if (prevDirX != 0 && dirX != prevDirX)
                {
                    xChanges++;
                }

                prevDirX = dirX;
            }

            if (dirY != 0)
            {
                if (prevDirY != 0 && dirY != prevDirY)
                {
                    yChanges++;
                }

                prevDirY = dirY;
            }
        }

        int width = maxX - minX;
        int height = maxY - minY;
        int directionChanges = xChanges + yChanges;

        return pathLength >= 760d &&
            width <= 280 &&
            height <= 280 &&
            directionChanges >= 6;
    }

    private void EnsureSystemCursorHidden()
    {
        if (_cursorSpotlightHidesSystemCursor)
        {
            return;
        }

        TryApplyTransparentSystemCursors();
        _cursorSpotlightHidesSystemCursor = _cursorSpotlightOverridesSystemCursors;
    }

    private void RestoreSystemCursorVisibility()
    {
        if (!_cursorSpotlightHidesSystemCursor)
        {
            return;
        }

        RestoreSystemCursorScheme();
        _cursorSpotlightHidesSystemCursor = false;
    }

    private void TryApplyTransparentSystemCursors()
    {
        if (_cursorSpotlightOverridesSystemCursors)
        {
            return;
        }

        try
        {
            foreach (uint cursorId in CursorSystemIds)
            {
                IntPtr blankCursor = CreateTransparentCursor(32, 32);
                if (blankCursor == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Could not create a transparent cursor for system override.");
                }

                if (!SetSystemCursor(blankCursor, cursorId))
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new InvalidOperationException($"SetSystemCursor failed for OCR value {cursorId} with Win32 error {error}.");
                }
            }

            _cursorSpotlightOverridesSystemCursors = true;
        }
        catch (Exception ex)
        {
            ErrorLog.Write("CursorSpotlightOverride", ex);
            RestoreSystemCursorScheme();
        }
    }

    private void RestoreSystemCursorScheme()
    {
        _ = SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
        _cursorSpotlightOverridesSystemCursors = false;
    }

    private static IntPtr CreateTransparentCursor(int width, int height)
    {
        using var colorBitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(colorBitmap);
        graphics.Clear(Color.Transparent);

        IntPtr hbmColor = colorBitmap.GetHbitmap(Color.FromArgb(0));
        IntPtr hbmMask = CreateBitmap(width, height, 1, 1, IntPtr.Zero);

        if (hbmColor == IntPtr.Zero || hbmMask == IntPtr.Zero)
        {
            if (hbmColor != IntPtr.Zero)
            {
                _ = DeleteObject(hbmColor);
            }

            if (hbmMask != IntPtr.Zero)
            {
                _ = DeleteObject(hbmMask);
            }

            return IntPtr.Zero;
        }

        try
        {
            var iconInfo = new ICONINFO
            {
                fIcon = false,
                xHotspot = 0,
                yHotspot = 0,
                hbmMask = hbmMask,
                hbmColor = hbmColor
            };

            return CreateIconIndirect(ref iconInfo);
        }
        finally
        {
            _ = DeleteObject(hbmColor);
            _ = DeleteObject(hbmMask);
        }
    }

    private void SuspendPerMonitorTracking()
    {
        if (_useFullscreenBackend)
        {
            return;
        }

        _suspendPerMonitorTrackingForMenu = true;
        SetPerMonitorWindowsVisible(false);
        _animAnchorValid = false;
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
            _animAnchorValid = false;
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
        UpdateTrayQuickToggleState();
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
        UpdateTrayQuickToggleState();
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
        UpdateTrayQuickToggleState();
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

