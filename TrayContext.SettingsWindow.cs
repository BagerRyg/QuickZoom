using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace QuickZoom;

internal sealed partial class TrayContext
{
    private enum SettingsPage
    {
        General,
        Display,
        Appearance,
        Zoom,
        Input,
        About
    }

    private void ShowSettingsWindow(SettingsPage initialPage = SettingsPage.General, Point? restoreLocation = null)
    {
        if (_settingsWindow != null && !_settingsWindow.IsDisposed)
        {
            _selectSettingsPageAction?.Invoke(initialPage);
            if (_settingsWindow.WindowState == FormWindowState.Minimized)
            {
                _settingsWindow.WindowState = FormWindowState.Normal;
            }

            _settingsWindow.Show();
            _settingsWindow.BringToFront();
            _settingsWindow.Activate();
            return;
        }

        ThemePalette palette = CurrentTheme;

        var form = new Form
        {
            Text = L("Settings.Title"),
            StartPosition = FormStartPosition.CenterScreen,
            ClientSize = GetSettingsClientSize(),
            FormBorderStyle = FormBorderStyle.FixedSingle,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = true,
            AutoScaleMode = AutoScaleMode.Dpi,
            BackColor = palette.MenuBackground,
            ForeColor = palette.Text,
            KeyPreview = true
        };
        _iconRef ??= LoadEmbeddedIconBySuffix("magnifier_dark.ico");
        if (_iconRef != null)
        {
            form.Icon = (Icon)_iconRef.Clone();
        }

        WindowChrome.TrySetDarkTitleBar(form, _useDarkTheme);
        _settingsWindow = form;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(20),
            Margin = new Padding(0),
            BackColor = palette.MenuBackground
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var sidebarSurface = new ModernSurfacePanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 18,
            BorderAlpha = 14,
            Margin = new Padding(0, 0, 16, 0),
            Padding = new Padding(12, 14, 12, 14),
            BackColor = _useDarkTheme ? Color.FromArgb(17, 20, 26) : palette.ControlBackground
        };

        var sidebarLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var sidebarHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 16),
            Padding = new Padding(0),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent
        };
        sidebarHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var appNameLabel = new Label
        {
            Text = L("Common.AppName"),
            AutoSize = true,
            Font = ControlDrawing.UiFont("Segoe UI Semibold", 15f, FontStyle.Bold),
            Margin = new Padding(0),
            ForeColor = palette.Text,
            BackColor = Color.Transparent
        };
        sidebarHeader.Controls.Add(appNameLabel, 0, 0);

        var navHost = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };

        var pageHost = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = palette.MenuBackground,
            AutoScroll = false
        };

        var rightLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = palette.MenuBackground
        };
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rightLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 8, 0, 0),
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };
        var closeButton = new ModernButton
        {
            Text = L("Settings.Done"),
            DialogResult = DialogResult.OK
        };
        closeButton.ApplyTheme(palette, emphasis: false);
        closeButton.SetOutlineColor(_useDarkTheme ? Color.FromArgb(96, 165, 250) : Color.FromArgb(65, 105, 170));
        _resetDefaultsButton = new ModernButton
        {
            Text = L("Settings.Reset"),
            MinimumSize = new Size(170, 38)
        };
        ApplyResetDefaultsButtonTheme();
        _resetDefaultsButton.Click += (_, _) => HandleResetDefaultsRequested();
        footer.Controls.Add(closeButton);
        footer.Controls.Add(_resetDefaultsButton);

        var pages = new Dictionary<SettingsPage, SettingsPageView>
        {
            [SettingsPage.General] = BuildGeneralSettingsPage(),
            [SettingsPage.Display] = BuildDisplaySettingsPage(),
            [SettingsPage.Appearance] = BuildAppearanceSettingsPage(),
            [SettingsPage.Zoom] = BuildZoomSettingsPage(),
            [SettingsPage.Input] = BuildInputSettingsPage(),
            [SettingsPage.About] = BuildAboutSettingsPage()
        };

        var navItems = new Dictionary<SettingsPage, SettingsSidebarItem>
        {
            [SettingsPage.General] = new SettingsSidebarItem(palette, L("Settings.General"), TrayFluentIcon.Settings),
            [SettingsPage.Display] = new SettingsSidebarItem(palette, L("Settings.Display"), TrayFluentIcon.MagnifiedDisplays),
            [SettingsPage.Appearance] = new SettingsSidebarItem(palette, L("Settings.Appearance"), TrayFluentIcon.Appearance),
            [SettingsPage.Zoom] = new SettingsSidebarItem(palette, L("Settings.Zoom"), TrayFluentIcon.Zoom),
            [SettingsPage.Input] = new SettingsSidebarItem(palette, L("Settings.Input"), TrayFluentIcon.KeyBinds),
            [SettingsPage.About] = new SettingsSidebarItem(palette, L("Settings.About"), TrayFluentIcon.About)
        };

        foreach (SettingsPageView page in pages.Values)
        {
            page.Visible = false;
            page.Dock = DockStyle.Top;
            pageHost.Controls.Add(page);
        }

        foreach ((SettingsPage pageKey, SettingsSidebarItem item) in navItems)
        {
            item.Click += (_, _) => ShowPage(pageKey);
            navHost.Controls.Add(item);
        }

        void UpdateSidebarItemWidths()
        {
            int itemWidth = Math.Max(ControlDrawing.ScaleLogical(form, 216), navHost.ClientSize.Width - 8);
            foreach (SettingsSidebarItem item in navItems.Values)
            {
                item.Width = itemWidth;
            }
        }

        void FitSettingsWindowToContent()
        {
            Rectangle area = Screen.FromControl(form).WorkingArea;
            int clientWidth = Math.Min(ControlDrawing.ScaleLogical(form, 750), area.Width - ControlDrawing.ScaleLogical(form, 96));
            clientWidth = Math.Max(ControlDrawing.ScaleLogical(form, 700), clientWidth);
            clientWidth = Math.Min(clientWidth, area.Width - ControlDrawing.ScaleLogical(form, 32));
            form.ClientSize = new Size(clientWidth, form.ClientSize.Height);
            root.PerformLayout();
            sidebarLayout.PerformLayout();
            rightLayout.PerformLayout();
            UpdateSidebarItemWidths();

            int rightWidth = Math.Min(Math.Max(ControlDrawing.ScaleLogical(form, 430), rightLayout.ClientSize.Width), ControlDrawing.ScaleLogical(form, 460));
            int maxPageHeight = 0;
            foreach (SettingsPageView page in pages.Values)
            {
                page.Width = rightWidth;
                page.PerformLayout();
                maxPageHeight = Math.Max(maxPageHeight, page.GetPreferredSize(new Size(rightWidth, 0)).Height);
            }

            int sidebarHeight = sidebarLayout.GetPreferredSize(new Size(root.GetColumnWidths()[0], 0)).Height;
            int footerHeight = footer.GetPreferredSize(Size.Empty).Height;
            int desiredHeight = root.Padding.Vertical + Math.Max(sidebarHeight, maxPageHeight + footerHeight + ControlDrawing.ScaleLogical(form, 8));
            int maxHeight = area.Height - ControlDrawing.ScaleLogical(form, 72);
            int clientHeight = Math.Min(Math.Max(ControlDrawing.ScaleLogical(form, 520), desiredHeight), maxHeight);
            form.ClientSize = new Size(clientWidth, clientHeight);
            root.PerformLayout();
            sidebarLayout.PerformLayout();
            rightLayout.PerformLayout();
            UpdateSidebarItemWidths();
        }

        void ShowPage(SettingsPage page)
        {
            _currentSettingsPage = page;
            pageHost.SuspendLayout();
            foreach ((SettingsPage key, SettingsPageView view) in pages)
            {
                view.Visible = key == page;
                if (key == page)
                {
                    view.BringToFront();
                }
            }
            pageHost.ResumeLayout(performLayout: true);
            foreach ((SettingsPage key, SettingsSidebarItem item) in navItems)
            {
                item.Selected = key == page;
            }
        }

        sidebarLayout.Controls.Add(sidebarHeader, 0, 0);
        sidebarLayout.Controls.Add(navHost, 0, 1);
        sidebarSurface.Controls.Add(sidebarLayout);

        rightLayout.Controls.Add(pageHost, 0, 0);
        rightLayout.Controls.Add(footer, 0, 1);

        root.Controls.Add(sidebarSurface, 0, 0);
        root.Controls.Add(rightLayout, 1, 0);
        form.Controls.Add(root);
        form.AcceptButton = closeButton;
        form.CancelButton = closeButton;
        closeButton.Click += (_, _) => form.Close();
        form.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                form.Close();
            }
        };
        form.FormClosed += (_, _) =>
        {
            if (_resetDefaultsConfirmTimer != null)
            {
                _resetDefaultsConfirmTimer.Stop();
                _resetDefaultsConfirmTimer.Dispose();
                _resetDefaultsConfirmTimer = null;
            }

            _resetDefaultsButton = null;
            _pendingResetDefaultsConfirmation = false;
            _settingsWindow = null;
            _selectSettingsPageAction = null;
            _displaySelectionSettingsSection = null;
        };

        _selectSettingsPageAction = page =>
        {
            ShowPage(page);
            FitSettingsWindowToContent();
        };
        ShowPage(initialPage);
        FitSettingsWindowToContent();
        if (restoreLocation.HasValue)
        {
            Rectangle area = Screen.FromPoint(restoreLocation.Value).WorkingArea;
            int x = Math.Clamp(restoreLocation.Value.X, area.Left, Math.Max(area.Left, area.Right - form.Width));
            int y = Math.Clamp(restoreLocation.Value.Y, area.Top, Math.Max(area.Top, area.Bottom - form.Height));
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(x, y);
        }

        form.Show();
        form.BringToFront();
        form.Activate();
    }

    private static Size GetSettingsClientSize()
    {
        Rectangle area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1400, 960);
        float scale = ControlDrawing.UiFontScale;
        int minWidth = (int)Math.Round(700 * Math.Min(1.16f, scale));
        int maxWidth = (int)Math.Round(760 * Math.Min(1.22f, scale));
        int minHeight = (int)Math.Round(520 * Math.Min(1.12f, scale));
        int maxHeight = (int)Math.Round(600 * Math.Min(1.20f, scale));
        int width = Math.Min(maxWidth, Math.Max(minWidth, area.Width - 128));
        int height = Math.Min(maxHeight, Math.Max(minHeight, area.Height - 128));
        return new Size(width, height);
    }

    private SettingsPageView BuildGeneralSettingsPage()
    {
        ThemePalette palette = CurrentTheme;
        var page = new SettingsPageView(palette, L("Settings.GeneralTitle"), L("Settings.GeneralDescription"));
        var section = new SettingsSection(palette, L("Settings.GeneralSection"), string.Empty);

        section.AddRow(CreateToggleRow(L("Settings.SmoothZoom"), L("Settings.SmoothZoomHelp"), _smoothZoom, value =>
        {
            _smoothZoom = value;
            SaveSettings();
        }, rightColumnWidth: 96));

        section.AddRow(CreateToggleRow(L("Settings.AutoDisableAt100"), L("Settings.AutoDisableAt100Help"), _autoDisableAt100, value =>
        {
            _autoDisableAt100 = value;
            if (_autoDisableAt100 && _zoomPercent == 100 && !_invertColors)
            {
                DisableMagAndReset();
            }

            SaveSettings();
        }, rightColumnWidth: 96));

        section.AddRow(CreateToggleRow(L("Settings.CenterCursor"), L("Settings.CenterCursorHelp"), _centerCursor, value =>
        {
            _centerCursor = value;
            SaveSettings();
        }, rightColumnWidth: 96));

        section.AddRow(CreateToggleRow(L("Settings.WiggleSpotlight"), L("Settings.WiggleSpotlightHelp"), _wiggleSpotlightEnabled, value =>
        {
            _wiggleSpotlightEnabled = value;
            if (!_wiggleSpotlightEnabled)
            {
                _recentCursorSamples.Clear();
                _cursorSpotlightVisibleUntilTick = 0;
                _cursorSpotlightOverlay?.HideSpotlight();
            }

            SaveSettings();
        }, rightColumnWidth: 96));

        page.AddSection(section);
        return page;
    }

    private SettingsPageView BuildDisplaySettingsPage()
    {
        ThemePalette palette = CurrentTheme;
        var page = new SettingsPageView(palette, L("Settings.DisplayTitle"), L("Settings.DisplayDescription"));
        var behaviorSection = new SettingsSection(palette, L("Settings.DisplaySection"), string.Empty);

        behaviorSection.AddRow(CreateToggleRow(L("Settings.AutoSwitchMonitor"), L("Settings.AutoSwitchMonitorHelp"), _autoSwitchMonitor, value =>
        {
            _autoSwitchMonitor = value;
            if (_autoSwitchMonitor)
            {
                _lockedScreen = null;
            }
            else if (GetCursorPos(out var ptLock))
            {
                _lockedScreen = Screen.FromPoint(new Point(ptLock.X, ptLock.Y));
            }

            SaveSettings();
            ApplyTransformCurrentPoint();
            RefreshMenuAndTrayUi();
        }, rightColumnWidth: 96));

        _displaySelectionSettingsSection = new SettingsSection(palette, L("Settings.DisplaySelectionSection"), string.Empty);
        PopulateDisplaySelectionSettingsSection();

        page.AddSection(behaviorSection);
        page.AddSection(_displaySelectionSettingsSection);
        return page;
    }

    private void PopulateDisplaySelectionSettingsSection()
    {
        if (_displaySelectionSettingsSection == null)
        {
            return;
        }

        _displaySelectionSettingsSection.ClearRows();
        _displaySelectionSettingsSection.AddRow(CreateDropdownRow(
            L("Settings.DisplaySelectionMode"),
            L("Settings.DisplaySelectionModeHelp"),
            BuildDisplaySelectionModeItems(),
            DisplaySelectionModeLabel(GetDisplaySelectionMode()),
            value =>
            {
                DisplaySelectionMode nextMode = ParseDisplaySelectionMode(value);
                if (nextMode != GetDisplaySelectionMode())
                {
                    SetDisplaySelectionMode(nextMode);
                }
            },
            rightColumnWidth: 320));

        if (GetDisplaySelectionMode() != DisplaySelectionMode.CustomSelection)
        {
            return;
        }

        int fallbackIndex = 1;
        foreach (Screen screen in GetOrderedScreens())
        {
            string deviceName = screen.DeviceName;
            bool selected = _selectedMonitorDeviceNames.Contains(deviceName);
            string label = GetFriendlyScreenLabel(screen, fallbackIndex++);
            _displaySelectionSettingsSection.AddRow(CreateToggleRow(
                label,
                L("Settings.DisplayCustomMonitorHelp"),
                selected,
                value =>
                {
                    bool isCurrentlySelected = _selectedMonitorDeviceNames.Contains(deviceName);
                    if (value != isCurrentlySelected)
                    {
                        SetScreenSelection(deviceName, value);
                    }
                },
                rightColumnWidth: 96));
        }
    }

    private void RefreshDisplaySettingsUi()
    {
        if (_displaySelectionSettingsSection == null || _settingsWindow == null || _settingsWindow.IsDisposed)
        {
            return;
        }

        if (_settingsWindow.InvokeRequired)
        {
            _settingsWindow.BeginInvoke((MethodInvoker)RefreshDisplaySettingsUi);
            return;
        }

        _settingsWindow.BeginInvoke((MethodInvoker)(() =>
        {
            if (_displaySelectionSettingsSection == null || _settingsWindow == null || _settingsWindow.IsDisposed)
            {
                return;
            }

            PopulateDisplaySelectionSettingsSection();
            _displaySelectionSettingsSection.PerformLayout();
        }));
    }

    private SettingsPageView BuildZoomSettingsPage()
    {
        ThemePalette palette = CurrentTheme;
        var page = new SettingsPageView(palette, L("Settings.ZoomTitle"), L("Settings.ZoomDescription"));
        var section = new SettingsSection(palette, L("Settings.ZoomSection"), string.Empty);

        section.AddRow(CreateSliderRow(L("Settings.ZoomStep"), L("Settings.ZoomStepHelp"), _stepPercent, 5, 100, 5, value => value + "%", value =>
        {
            _stepPercent = value;
            SaveSettings();
        }, rightColumnWidth: 420));

        section.AddRow(CreateSliderRow(L("Settings.MaxZoom"), L("Settings.MaxZoomHelp"), _maxPercent, 200, 500, 10, value => value + "%", value =>
        {
            _maxPercent = value;
            ClampZoom();
            SaveSettings();
        }, rightColumnWidth: 420));

        section.AddRow(CreateSliderRow(L("Settings.RefreshRate"), L("Settings.RefreshRateHelp"), _fps, 60, 360, 10, value => value + " Hz", value =>
        {
            _fps = value;
            ApplyFps();
            SaveSettings();
        }, rightColumnWidth: 420));

        page.AddSection(section);
        return page;
    }

    private SettingsPageView BuildAppearanceSettingsPage()
    {
        ThemePalette palette = CurrentTheme;
        var page = new SettingsPageView(palette, L("Settings.AppearanceTitle"), L("Settings.AppearanceDescription"));
        var themeSection = new SettingsSection(palette, L("Settings.AppearanceSection"), string.Empty);

        themeSection.AddRow(CreateDropdownRow(
            L("Settings.ThemeMode"),
            L("Settings.ThemeModeHelp"),
            BuildThemeModeItems(),
            ThemeModeLabel(_themeMode),
            value =>
            {
                ThemeMode nextMode = ParseThemeMode(value);
                if (nextMode != _themeMode)
                {
                    SetThemeMode(nextMode);
                }
            },
            rightColumnWidth: 260));

        themeSection.AddRow(CreateDropdownRow(L("Settings.Language"), L("Settings.LanguageHelp"), BuildLanguageItems(), UiText.GetLanguageDisplayName(_language, _language), value =>
        {
            _language = UiText.ParseLanguageDisplayName(_language, value);
            SaveSettings();
            RefreshMenuAndTrayUi(rebuildPopup: true);
            if (_settingsWindow != null && !_settingsWindow.IsDisposed)
            {
                _settingsWindow.BeginInvoke((MethodInvoker)(() => RefreshSettingsWindow(SettingsPage.Appearance)));
            }
            else
            {
                RefreshSettingsWindow(SettingsPage.Appearance);
            }
        }, rightColumnWidth: 260));

        themeSection.AddRow(CreateDropdownRow(L("Settings.FontSize"), L("Settings.FontSizeHelp"), BuildUiFontSizeItems(), UiFontSizeLabel(_uiFontSize), value =>
        {
            UiFontSize nextSize = ParseUiFontSize(value);
            if (nextSize == _uiFontSize)
            {
                return;
            }

            _uiFontSize = nextSize;
            ApplyUiFontScale();
            SaveSettings();
            RefreshMenuAndTrayUi(rebuildPopup: true);
            RefreshSettingsWindow(SettingsPage.Appearance);
        }, rightColumnWidth: 260));

        page.AddSection(themeSection);
        return page;
    }

    private SettingsPageView BuildInputSettingsPage()
    {
        ThemePalette palette = CurrentTheme;
        var page = new SettingsPageView(palette, L("Settings.InputTitle"), L("Settings.InputDescription"));
        var section = new SettingsSection(palette, L("Settings.InputSection"), string.Empty);

        section.AddRow(CreateDropdownRow(
            L("Settings.ShortcutMode"),
            L("Settings.ShortcutModeHelp"),
            BuildShortcutModeItems(),
            ShortcutInputModeLabel(_shortcutInputMode),
            value =>
            {
                _shortcutInputMode = ParseShortcutInputMode(value);
                _invertKeyPressed = false;
                _followCursorKeyPressed = false;
                _wheelDeltaRemainder = 0;
                SaveSettings();
                RefreshMenuAndTrayUi(rebuildPopup: true);
                RefreshSettingsWindow(SettingsPage.Input);
            },
            rightColumnWidth: 360));

        section.AddRow(CreateKeybindRow(
            L("Settings.EnableKey"),
            L("Settings.EnableKeyHelp"),
            KeyLabel(_enableKey),
            () =>
            {
                Keys? key = PromptForKey(_enableKey, L("Settings.EnableKeyDialogTitle"), L("Settings.EnableKeyDialogBody"));
                if (key != null)
                {
                    _enableKey = key.Value;
                    _enableKeyPressed = false;
                    SaveSettings();
                    RefreshMenuAndTrayUi(rebuildPopup: true);
                    RefreshSettingsWindow(SettingsPage.Input);
                }
            },
            rightColumnWidth: 360));

        section.AddRow(CreateKeybindRow(
            L("Settings.InvertActivationKey"),
            L("Settings.InvertActivationKeyHelp"),
            KeyLabel(_invertKey),
            () =>
            {
                Keys? key = PromptForKey(_invertKey, L("Settings.InvertKeyDialogTitle"), L("Settings.InvertKeyDialogBody"));
                if (key != null)
                {
                    _invertKey = key.Value;
                    _invertTrigger = InvertTriggerKind.CustomKey;
                    _invertKeyPressed = false;
                    SaveSettings();
                    RefreshMenuAndTrayUi(rebuildPopup: true);
                    RefreshSettingsWindow(SettingsPage.Input);
                }
            },
            rightColumnWidth: 360));

        section.AddRow(CreateKeybindRow(
            L("Settings.FollowCursorHotkey"),
            L("Settings.FollowCursorHotkeyHelp"),
            KeyLabel(_followCursorKey),
            () =>
            {
                Keys? key = PromptForKey(_followCursorKey, L("Settings.FollowCursorHotkeyDialogTitle"), L("Settings.FollowCursorHotkeyDialogBody"));
                if (key != null)
                {
                    _followCursorKey = key.Value;
                    _followCursorKeyPressed = false;
                    SaveSettings();
                    RefreshMenuAndTrayUi(rebuildPopup: true);
                    RefreshSettingsWindow(SettingsPage.Input);
                }
            },
            rightColumnWidth: 360));

        page.AddSection(section);
        return page;
    }

    private SettingsPageView BuildAboutSettingsPage()
    {
        ThemePalette palette = CurrentTheme;
        var page = new SettingsPageView(palette, L("Settings.AboutTitle"), L("Settings.AboutDescription"));

        string installPath = InstalledAppService.GetCurrentInstalledExecutablePath() ?? L("About.NotInstalled");
        string settingsPath = AppPaths.SettingsPath;

        var overviewSection = new SettingsSection(palette, L("Settings.AboutSection"), string.Empty);
        overviewSection.AddRow(CreateInfoRow(
            L("Settings.AboutBuildStartup"),
            L("About.VersionBuild", AppInfo.MajorVersion, AppInfo.BuildNumber),
            StartupTaskService.GetStatusLabel(_language)));
        overviewSection.AddRow(new SettingsRow(
            palette,
            L("Settings.AboutLocations"),
            L("Settings.AboutLocationsHelp"),
            CreateDualActionButtons(
                new[]
                {
                    (L("About.OpenInstallFolder"), (Action)(() => OpenFileLocation(installPath)), !string.Equals(installPath, L("About.NotInstalled"), StringComparison.OrdinalIgnoreCase)),
                    (L("About.OpenConfigFolder"), (Action)(() => OpenFileLocation(settingsPath)), true)
                },
                380),
            rightColumnWidth: 380));
        overviewSection.AddRow(CreateTextTileRow(
            L("Settings.UsageHelp"),
            L("About.HowToUseDetailed")));

        page.AddSection(overviewSection);
        return page;
    }

    private SettingsRow CreateToggleRow(string title, string description, bool initial, Action<bool> onChanged, int rightColumnWidth = 96)
    {
        var toggle = new ToggleSwitchControl(CurrentTheme)
        {
            IsOn = initial
        };
        toggle.Click += (_, _) => onChanged(toggle.IsOn);
        return new SettingsRow(CurrentTheme, title, description, toggle, rightColumnWidth);
    }

    private SettingsRow CreateSliderRow(string title, string description, int value, int min, int max, int step, Func<int, string> valueFormatter, Action<int> onChanged, int rightColumnWidth = 420)
    {
        var slider = new ModernSlider(CurrentTheme)
        {
            Minimum = min,
            Maximum = max,
            SnapStep = step,
            Value = value,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 12, 0)
        };

        var valueLabel = new Label
        {
            AutoSize = false,
            Width = 72,
            Height = 28,
            Text = valueFormatter(value),
            TextAlign = ContentAlignment.MiddleRight,
            Font = ControlDrawing.UiFont("Segoe UI Semibold", 9.5f, FontStyle.Bold),
            ForeColor = CurrentTheme.Text,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };

        slider.ValueChanged += (_, _) =>
        {
            valueLabel.Text = valueFormatter(slider.Value);
            onChanged(slider.Value);
        };

        var host = new TableLayoutPanel
        {
            AutoSize = false,
            Width = rightColumnWidth,
            Height = 34,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        host.Controls.Add(slider, 0, 0);
        host.Controls.Add(valueLabel, 1, 0);

        return new SettingsRow(CurrentTheme, title, description, host, rightColumnWidth);
    }

    private SettingsRow CreateDropdownRow(string title, string description, string[] items, string current, Action<string> onChanged, Control? actionButton = null, int rightColumnWidth = 260)
    {
        var combo = new ModernDropdown(CurrentTheme)
        {
            Width = actionButton == null ? Math.Max(220, rightColumnWidth - 24) : Math.Max(210, rightColumnWidth - 144)
        };
        combo.Items.AddRange(items);
        combo.SelectedIndex = Math.Max(0, combo.Items.IndexOf(current));
        combo.SelectedIndexChanged += (_, _) =>
        {
            if (combo.SelectedItem is string selected)
            {
                onChanged(selected);
            }
        };

        Control rightControl;
        if (actionButton == null)
        {
            rightControl = combo;
        }
        else
        {
            var row = new TableLayoutPanel
            {
                AutoSize = false,
                Width = rightColumnWidth,
                Height = Math.Max(combo.Height, actionButton.Height),
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            combo.Dock = DockStyle.Fill;
            combo.Margin = new Padding(0, 0, 10, 0);
            actionButton.Dock = DockStyle.Right;
            actionButton.Margin = new Padding(0);
            row.Controls.Add(combo, 0, 0);
            row.Controls.Add(actionButton, 1, 0);
            rightControl = row;
        }

        return new SettingsRow(CurrentTheme, title, description, rightControl, rightColumnWidth);
    }

    private SettingsRow CreateKeybindRow(string title, string description, string currentKeyLabel, Action onCustomize, int rightColumnWidth = 360)
    {
        var badge = new KeyBadgeControl(CurrentTheme, currentKeyLabel)
        {
            Width = 198,
            Height = 34,
            Dock = DockStyle.Fill
        };
        badge.ApplyTheme(CurrentTheme);
        badge.Click += (_, _) => onCustomize();

        return new SettingsRow(CurrentTheme, title, description, badge, Math.Max(198, rightColumnWidth));
    }

    private SettingsRow CreateInfoRow(string title, string value, string description, Control? actionButton = null, int rightColumnWidth = 240)
    {
        string effectiveDescription = string.IsNullOrWhiteSpace(description)
            ? value
            : value + "\n" + description;

        Control rightControl;
        if (actionButton == null)
        {
            var valueLabel = new Label
            {
                AutoSize = false,
                Width = rightColumnWidth,
                Height = 40,
                Text = value,
                TextAlign = ContentAlignment.MiddleRight,
                Font = ControlDrawing.UiFont("Segoe UI", 9.5f, FontStyle.Regular),
                ForeColor = CurrentTheme.SecondaryText,
                BackColor = Color.Transparent
            };
            rightControl = valueLabel;
        }
        else
        {
            rightControl = actionButton;
        }

        return new SettingsRow(CurrentTheme, title, effectiveDescription, rightControl, rightColumnWidth, actionButton == null ? value : null);
    }

    private SettingsRow CreateTextTileRow(string title, string description)
    {
        var spacer = new Label
        {
            AutoSize = false,
            Width = 1,
            Height = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };

        return new SettingsRow(CurrentTheme, title, description, spacer, rightColumnWidth: 96);
    }

    private Control CreateInlineActionButton(string text, Action onClick)
    {
        var button = new ModernButton
        {
            Text = text
        };
        button.ApplyTheme(CurrentTheme, emphasis: false);
        button.Click += (_, _) => onClick();
        return button;
    }

    private string[] BuildLanguageItems()
    {
        var items = new List<string>();
        foreach (UiLanguage language in Enum.GetValues<UiLanguage>())
        {
            items.Add(UiText.GetLanguageDisplayName(language, _language));
        }

        return items.ToArray();
    }

    private string[] BuildDisplaySelectionModeItems() =>
    [
        L("Settings.DisplayModeAll"),
        L("Settings.DisplayModeCursor"),
        L("Settings.DisplayModeCustom")
    ];

    private string DisplaySelectionModeLabel(DisplaySelectionMode mode) => mode switch
    {
        DisplaySelectionMode.MonitorUnderCursor => L("Settings.DisplayModeCursor"),
        DisplaySelectionMode.CustomSelection => L("Settings.DisplayModeCustom"),
        _ => L("Settings.DisplayModeAll")
    };

    private DisplaySelectionMode ParseDisplaySelectionMode(string value)
    {
        if (string.Equals(value, L("Settings.DisplayModeCursor"), StringComparison.Ordinal))
        {
            return DisplaySelectionMode.MonitorUnderCursor;
        }

        if (string.Equals(value, L("Settings.DisplayModeCustom"), StringComparison.Ordinal))
        {
            return DisplaySelectionMode.CustomSelection;
        }

        return DisplaySelectionMode.AllDisplays;
    }

    private string[] BuildThemeModeItems() => [L("Settings.ThemeAuto"), L("Settings.ThemeDark"), L("Settings.ThemeLight")];

    private string ThemeModeLabel(ThemeMode mode) => mode switch
    {
        ThemeMode.Dark => L("Settings.ThemeDark"),
        ThemeMode.Light => L("Settings.ThemeLight"),
        _ => L("Settings.ThemeAuto")
    };

    private ThemeMode ParseThemeMode(string value)
    {
        if (string.Equals(value, L("Settings.ThemeDark"), StringComparison.Ordinal))
        {
            return ThemeMode.Dark;
        }

        if (string.Equals(value, L("Settings.ThemeLight"), StringComparison.Ordinal))
        {
            return ThemeMode.Light;
        }

        return ThemeMode.AutoSystem;
    }

    private Control CreateDualActionButtons((string Text, Action OnClick, bool Enabled)[] buttons, int width)
    {
        var host = new TableLayoutPanel
        {
            AutoSize = false,
            Width = width,
            Height = 38,
            ColumnCount = buttons.Length,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        for (int i = 0; i < buttons.Length; i++)
        {
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / buttons.Length));
            var buttonSpec = buttons[i];

            var button = new ModernButton
            {
                Text = buttonSpec.Text,
                Enabled = buttonSpec.Enabled,
                Dock = DockStyle.Fill,
                Margin = new Padding(i == 0 ? 0 : 8, 0, 0, 0)
            };
            button.ApplyTheme(CurrentTheme, emphasis: false);
            button.Click += (_, _) => buttonSpec.OnClick();
            host.Controls.Add(button, i, 0);
        }

        return host;
    }

    private void RefreshSettingsWindow(SettingsPage page)
    {
        if (_settingsWindow == null || _settingsWindow.IsDisposed)
        {
            return;
        }

        if (_settingsWindow.InvokeRequired)
        {
            _settingsWindow.BeginInvoke((MethodInvoker)(() => RefreshSettingsWindow(page)));
            return;
        }

        if (_settingsWindow == null || _settingsWindow.IsDisposed)
        {
            return;
        }

        Point previousLocation = _settingsWindow.Location;
        _settingsWindow.Close();
        ShowSettingsWindow(page, previousLocation);
    }

    private void HandleResetDefaultsRequested()
    {
        if (!_pendingResetDefaultsConfirmation)
        {
            _pendingResetDefaultsConfirmation = true;
            ApplyResetDefaultsButtonTheme();

            _resetDefaultsConfirmTimer ??= new System.Windows.Forms.Timer { Interval = 5000 };
            _resetDefaultsConfirmTimer.Stop();
            _resetDefaultsConfirmTimer.Tick -= OnResetDefaultsConfirmTimeout;
            _resetDefaultsConfirmTimer.Tick += OnResetDefaultsConfirmTimeout;
            _resetDefaultsConfirmTimer.Start();
            return;
        }

        CancelResetDefaultsConfirmation();
        ResetSettingsToDefaults();
    }

    private void OnResetDefaultsConfirmTimeout(object? sender, EventArgs e)
    {
        CancelResetDefaultsConfirmation();
    }

    private void CancelResetDefaultsConfirmation()
    {
        _pendingResetDefaultsConfirmation = false;
        if (_resetDefaultsConfirmTimer != null)
        {
            _resetDefaultsConfirmTimer.Stop();
        }

        ApplyResetDefaultsButtonTheme();
    }

    private void ApplyResetDefaultsButtonTheme()
    {
        if (_resetDefaultsButton == null)
        {
            return;
        }

        _resetDefaultsButton.Text = _pendingResetDefaultsConfirmation
            ? L("Settings.ResetDefaultsConfirm")
            : L("Settings.ResetDefaults");
        _resetDefaultsButton.ApplyTheme(
            CurrentTheme,
            emphasis: false,
            destructive: _pendingResetDefaultsConfirmation,
            destructiveHoverEnabled: true);
    }
}
