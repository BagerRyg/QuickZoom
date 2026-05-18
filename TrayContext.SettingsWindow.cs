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
        Cursor,
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
        _resetDefaultsButton = new ModernButton
        {
            Text = L("Settings.Reset"),
            MinimumSize = new Size(170, 38)
        };
        ApplyResetDefaultsButtonTheme();
        _resetDefaultsButton.Click += (_, _) => HandleResetDefaultsRequested();

        var form = new SettingsForm(
            palette,
            _useDarkTheme,
            L("Settings.Title"),
            GetSettingsClientSize(),
            L("Common.AppName"),
            L("Settings.Done"),
            _resetDefaultsButton,
            BuildSettingsPageDefinitions());

        _iconRef ??= LoadEmbeddedIconBySuffix("magnifier_dark.ico");
        if (_iconRef != null)
        {
            form.Icon = (Icon)_iconRef.Clone();
        }

        WindowChrome.TrySetDarkTitleBar(form, _useDarkTheme);
        _settingsWindow = form;
        form.FormClosed += (_, _) =>
        {
            FlushSettingsSave();
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
            form.ShowPage(GetSettingsPageType(page));
            _currentSettingsPage = page;
        };
        form.PageShown += (_, pageType) => _currentSettingsPage = GetSettingsPage(pageType);
        form.ShowPage(GetSettingsPageType(initialPage));
        CenterSettingsWindow(form, restoreLocation);
        form.Show();
        form.BringToFront();
        form.Activate();
    }

    private IReadOnlyList<SettingsPageDefinition> BuildSettingsPageDefinitions() =>
    [
        new(typeof(GeneralSettingsPageView), L("Settings.General"), TrayFluentIcon.Settings, BuildGeneralSettingsPage),
        new(typeof(DisplaySettingsPageView), L("Settings.Display"), TrayFluentIcon.MagnifiedDisplays, BuildDisplaySettingsPage),
        new(typeof(AppearanceSettingsPageView), L("Settings.Appearance"), TrayFluentIcon.Appearance, BuildAppearanceSettingsPage),
        new(typeof(CursorSettingsPageView), L("Settings.Cursor"), TrayFluentIcon.Cursor, BuildCursorSettingsPage),
        new(typeof(ZoomSettingsPageView), L("Settings.Zoom"), TrayFluentIcon.Zoom, BuildZoomSettingsPage),
        new(typeof(ShortcutsSettingsPageView), L("Settings.Input"), TrayFluentIcon.KeyBinds, BuildInputSettingsPage),
        new(typeof(AboutSettingsPageView), L("Settings.About"), TrayFluentIcon.About, BuildAboutSettingsPage)
    ];

    private static Type GetSettingsPageType(SettingsPage page) => page switch
    {
        SettingsPage.Display => typeof(DisplaySettingsPageView),
        SettingsPage.Appearance => typeof(AppearanceSettingsPageView),
        SettingsPage.Cursor => typeof(CursorSettingsPageView),
        SettingsPage.Zoom => typeof(ZoomSettingsPageView),
        SettingsPage.Input => typeof(ShortcutsSettingsPageView),
        SettingsPage.About => typeof(AboutSettingsPageView),
        _ => typeof(GeneralSettingsPageView)
    };

    private static SettingsPage GetSettingsPage(Type pageType)
    {
        if (pageType == typeof(DisplaySettingsPageView))
        {
            return SettingsPage.Display;
        }

        if (pageType == typeof(AppearanceSettingsPageView))
        {
            return SettingsPage.Appearance;
        }

        if (pageType == typeof(CursorSettingsPageView))
        {
            return SettingsPage.Cursor;
        }

        if (pageType == typeof(ZoomSettingsPageView))
        {
            return SettingsPage.Zoom;
        }

        if (pageType == typeof(ShortcutsSettingsPageView))
        {
            return SettingsPage.Input;
        }

        if (pageType == typeof(AboutSettingsPageView))
        {
            return SettingsPage.About;
        }

        return SettingsPage.General;
    }

    private static Size GetSettingsClientSize()
    {
        Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
        int width = Math.Min(1280, Math.Max(1040, area.Width - 256));
        int height = Math.Max(640, (int)Math.Round((area.Height - 32) * 0.8));
        width = Math.Min(width, area.Width - 32);
        height = Math.Min(height, area.Height - 16);
        return new Size(width, height);
    }

    private static void CenterSettingsWindow(Form form, Point? displayPoint = null)
    {
        Screen screen = displayPoint.HasValue
            ? Screen.FromPoint(displayPoint.Value)
            : Screen.FromPoint(Cursor.Position);
        Rectangle area = screen.WorkingArea;
        int x = area.Left + (area.Width - form.Width) / 2;
        int y = area.Top + (area.Height - form.Height) / 2;
        form.Location = new Point(
            Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - form.Width)),
            Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - form.Height)));
    }

    private SettingsPageView BuildGeneralSettingsPage()
    {
        ThemePalette palette = CurrentTheme;
        var page = new GeneralSettingsPageView(palette, L("Settings.GeneralTitle"), L("Settings.GeneralDescription"));
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

        page.AddSection(section);
        return page;
    }

    private SettingsPageView BuildDisplaySettingsPage()
    {
        ThemePalette palette = CurrentTheme;
        var page = new DisplaySettingsPageView(palette, L("Settings.DisplayTitle"), L("Settings.DisplayDescription"));
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
        _displaySelectionSettingsSection.AddRow(CreateTextTileRow(L("Settings.Loading"), string.Empty));
        StartDisplaySelectionLoad(page);

        page.AddSection(behaviorSection);
        page.AddSection(_displaySelectionSettingsSection);
        return page;
    }

    private void StartDisplaySelectionLoad(Control owner)
    {
        if (owner.IsHandleCreated)
        {
            _ = LoadDisplaySelectionSettingsAsync(owner);
            return;
        }

        owner.HandleCreated += (_, _) => _ = LoadDisplaySelectionSettingsAsync(owner);
    }

    private async Task LoadDisplaySelectionSettingsAsync(Control owner)
    {
        List<DisplayMonitorSettingsInfo> monitors = await Task.Run(GetDisplayMonitorSettingsInfos);
        if (owner.IsDisposed || _settingsWindow == null || _settingsWindow.IsDisposed)
        {
            return;
        }

        owner.BeginInvoke((MethodInvoker)(() =>
        {
            if (owner.IsDisposed || _displaySelectionSettingsSection == null)
            {
                return;
            }

            PopulateDisplaySelectionSettingsSection(monitors);
            _displaySelectionSettingsSection.PerformLayout();
            if (owner.FindForm() is SettingsForm settingsForm)
            {
                settingsForm.FitToCurrentPage();
            }
        }));
    }

    private static List<DisplayMonitorSettingsInfo> GetDisplayMonitorSettingsInfos()
    {
        return Screen.AllScreens
            .OrderByDescending(screen => screen.Primary)
            .ThenBy(screen => screen.DeviceName, StringComparer.OrdinalIgnoreCase)
            .Select(screen => new DisplayMonitorSettingsInfo(screen.DeviceName))
            .ToList();
    }

    private sealed class DisplayMonitorSettingsInfo
    {
        public DisplayMonitorSettingsInfo(string deviceName)
        {
            DeviceName = deviceName;
        }

        public string DeviceName { get; }
    }

    private void PopulateDisplaySelectionSettingsSection(IReadOnlyList<DisplayMonitorSettingsInfo>? monitors = null)
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
        IReadOnlyList<DisplayMonitorSettingsInfo> monitorItems = monitors ?? GetDisplayMonitorSettingsInfos();
        foreach (DisplayMonitorSettingsInfo screen in monitorItems)
        {
            string deviceName = screen.DeviceName;
            bool selected = _selectedMonitorDeviceNames.Contains(deviceName);
            string label = GetFriendlyScreenLabel(deviceName, fallbackIndex++);
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
            _settingsWindow.BeginInvoke((MethodInvoker)(() => RunGuarded("Settings.RefreshDisplay.Invoke", RefreshDisplaySettingsUi)));
            return;
        }

        _settingsWindow.BeginInvoke((MethodInvoker)(() =>
        {
            RunGuarded("Settings.RefreshDisplay", () =>
            {
                if (_displaySelectionSettingsSection == null || _settingsWindow == null || _settingsWindow.IsDisposed)
                {
                    return;
                }

                if (_displaySelectionSettingsSection != null)
                {
                    _displaySelectionSettingsSection.ClearRows();
                    _displaySelectionSettingsSection.AddRow(CreateTextTileRow(L("Settings.Loading"), string.Empty));
                    _ = LoadDisplaySelectionSettingsAsync(_displaySelectionSettingsSection);
                }
            });
        }));
    }

    private string GetFriendlyScreenLabel(string deviceName, int fallbackIndex)
    {
        int displayNumber = TryGetDisplayNumber(deviceName) ?? fallbackIndex;
        return displayNumber switch
        {
            1 => L("Tray.PrimaryDisplay"),
            2 => L("Tray.SecondaryDisplay"),
            _ => L("Tray.MonitorNumber", displayNumber)
        };
    }

    private SettingsPageView BuildZoomSettingsPage()
    {
        ThemePalette palette = CurrentTheme;
        var page = new ZoomSettingsPageView(palette, L("Settings.ZoomTitle"), L("Settings.ZoomDescription"));
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

    private SettingsPageView BuildCursorSettingsPage()
    {
        ThemePalette palette = CurrentTheme;
        var page = new CursorSettingsPageView(palette, L("Settings.CursorTitle"), L("Settings.CursorDescription"));
        var section = new SettingsSection(palette, string.Empty, string.Empty);
        var preview = new CursorPreviewControl(
            palette,
            Color.FromArgb(_cursorFillColorArgb),
            Color.FromArgb(_cursorBorderColorArgb),
            _cursorScale);

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

        section.AddRow(CreateToggleRow(L("Settings.CursorEnhancement"), L("Settings.CursorEnhancementHelp"), _cursorEnhancementEnabled, value =>
        {
            _cursorEnhancementEnabled = value;
            ApplyCursorEnhancementIfNeeded();
            SaveSettings();
        }, rightColumnWidth: 96));

        section.AddRow(CreateSliderRow(
            L("Settings.CursorSize"),
            L("Settings.CursorSizeHelp"),
            _cursorScale,
            CursorScaleMinimum,
            CursorScaleMaximum,
            10,
            value => value + "%",
            value =>
            {
                _cursorScale = value;
                preview.ScalePercent = value;
                preview.Invalidate();
                SaveSettings();
                ScheduleCursorScaleApply();
            },
            rightColumnWidth: 420));

        section.AddRow(CreateColorPaletteRow(
            L("Settings.CursorFillColor"),
            L("Settings.CursorFillColorHelp"),
            Color.FromArgb(_cursorFillColorArgb),
            color =>
            {
                _cursorFillColorArgb = color.ToArgb();
                preview.FillColor = color;
                preview.Invalidate();
                ApplyCursorEnhancementIfNeeded();
                SaveSettings();
            }));

        section.AddRow(CreateColorPaletteRow(
            L("Settings.CursorBorderColor"),
            L("Settings.CursorBorderColorHelp"),
            Color.FromArgb(_cursorBorderColorArgb),
            color =>
            {
                _cursorBorderColorArgb = color.ToArgb();
                preview.BorderColor = color;
                preview.Invalidate();
                ApplyCursorEnhancementIfNeeded();
                SaveSettings();
            }));

        section.AddRow(new SettingsRow(
            palette,
            L("Settings.CursorPreview"),
            L("Settings.CursorPreviewHelp"),
            preview,
            rightColumnWidth: 260));

        page.AddSection(section);
        return page;
    }

    private SettingsPageView BuildAppearanceSettingsPage()
    {
        ThemePalette palette = CurrentTheme;
        var page = new AppearanceSettingsPageView(palette, L("Settings.AppearanceTitle"), L("Settings.AppearanceDescription"));
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
            FlushSettingsSave();
            RefreshMenuAndTrayUi(rebuildPopup: true);
            if (_settingsWindow != null && !_settingsWindow.IsDisposed)
            {
                _settingsWindow.BeginInvoke((MethodInvoker)(() => RunGuarded("Settings.LanguageRefresh", () => RefreshSettingsWindow(SettingsPage.Appearance))));
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
            FlushSettingsSave();
            RefreshMenuAndTrayUi(rebuildPopup: true);
            RefreshSettingsWindow(SettingsPage.Appearance);
        }, rightColumnWidth: 260));

        page.AddSection(themeSection);
        return page;
    }

    private SettingsPageView BuildInputSettingsPage()
    {
        ThemePalette palette = CurrentTheme;
        var page = new ShortcutsSettingsPageView(palette, L("Settings.InputTitle"), L("Settings.InputDescription"));
        var section = new SettingsSection(palette, L("Settings.InputSection"), string.Empty);
        ToggleSwitchControl? officeAltToggle = null;
        SettingsRow? officeAltRow = null;

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
            },
            rightColumnWidth: 360,
            compact: true));

        section.AddRow(CreateKeybindRow(
            L("Settings.EnableKey"),
            L("Settings.EnableKeyHelp"),
            KeyLabel(_enableKey),
            () =>
            {
                Keys? key = PromptForKey(_enableKey, L("Settings.EnableKeyDialogTitle"), L("Settings.EnableKeyDialogBody"));
                if (key == null)
                {
                    return null;
                }

                _enableKey = key.Value;
                bool altEnableKey = IsAltEnableKey();
                if (!altEnableKey)
                {
                    _suppressAltKeyInOfficeApps = false;
                }

                _enableKeyPressed = false;
                SaveSettings();
                RefreshMenuAndTrayUi(rebuildPopup: true);

                if (officeAltToggle != null)
                {
                    officeAltToggle.IsOn = _suppressAltKeyInOfficeApps && altEnableKey;
                    officeAltToggle.Enabled = altEnableKey;
                }

                if (officeAltRow != null)
                {
                    officeAltRow.Enabled = altEnableKey;
                }

                return KeyLabel(_enableKey);
            },
            rightColumnWidth: 360,
            compact: true));

        section.AddRow(CreateKeybindRow(
            L("Settings.InvertActivationKey"),
            L("Settings.InvertActivationKeyHelp"),
            KeyLabel(_invertKey),
            () =>
            {
                Keys? key = PromptForKey(_invertKey, L("Settings.InvertKeyDialogTitle"), L("Settings.InvertKeyDialogBody"));
                if (key == null)
                {
                    return null;
                }

                _invertKey = key.Value;
                _invertTrigger = InvertTriggerKind.CustomKey;
                _invertKeyPressed = false;
                SaveSettings();
                RefreshMenuAndTrayUi(rebuildPopup: true);
                return KeyLabel(_invertKey);
            },
            rightColumnWidth: 360,
            compact: true));

        section.AddRow(CreateKeybindRow(
            L("Settings.FollowCursorHotkey"),
            L("Settings.FollowCursorHotkeyHelp"),
            KeyLabel(_followCursorKey),
            () =>
            {
                Keys? key = PromptForKey(_followCursorKey, L("Settings.FollowCursorHotkeyDialogTitle"), L("Settings.FollowCursorHotkeyDialogBody"));
                if (key == null)
                {
                    return null;
                }

                _followCursorKey = key.Value;
                _followCursorKeyPressed = false;
                SaveSettings();
                RefreshMenuAndTrayUi(rebuildPopup: true);
                return KeyLabel(_followCursorKey);
            },
            rightColumnWidth: 360,
            compact: true));

        section.AddRow(CreateToggleRow(
            L("Settings.SuppressAltKeyInOfficeApps"),
            L("Settings.SuppressAltKeyInOfficeAppsHelp"),
            _suppressAltKeyInOfficeApps && IsAltEnableKey(),
            value =>
            {
                _suppressAltKeyInOfficeApps = value;
                SaveSettings();
            },
            rightColumnWidth: 96,
            enabled: IsAltEnableKey(),
            compact: true,
            onCreated: (toggle, row) =>
            {
                officeAltToggle = toggle;
                officeAltRow = row;
            }));

        page.AddSection(section);
        return page;
    }

    private SettingsPageView BuildAboutSettingsPage()
    {
        ThemePalette palette = CurrentTheme;
        var page = new AboutSettingsPageView(palette, L("Settings.AboutTitle"), L("Settings.AboutDescription"));

        var overviewSection = new SettingsSection(palette, L("Settings.AboutSection"), string.Empty);
        overviewSection.AddRow(CreateInfoRow(
            L("Settings.AboutBuildStartup"),
            L("About.VersionBuild", AppInfo.MajorVersion, AppInfo.BuildNumber),
            L("Settings.Loading")));
        overviewSection.AddRow(CreateTextTileRow(
            L("Settings.UsageHelp"),
            L("About.HowToUseDetailed")));

        page.AddSection(overviewSection);
        StartAboutLoad(page, overviewSection);
        return page;
    }

    private void StartAboutLoad(Control owner, SettingsSection overviewSection)
    {
        if (owner.IsHandleCreated)
        {
            _ = LoadAboutSettingsAsync(owner, overviewSection);
            return;
        }

        owner.HandleCreated += (_, _) => _ = LoadAboutSettingsAsync(owner, overviewSection);
    }

    private async Task LoadAboutSettingsAsync(Control owner, SettingsSection overviewSection)
    {
        string notInstalled = L("About.NotInstalled");
        UiLanguage language = _language;
        (string InstallPath, string StartupStatus) details = await Task.Run(() =>
        {
            string installPath = InstalledAppService.GetCurrentInstalledExecutablePath() ?? notInstalled;
            string startupStatus = StartupTaskService.GetStatusLabel(language);
            return (installPath, startupStatus);
        });

        if (owner.IsDisposed || _settingsWindow == null || _settingsWindow.IsDisposed)
        {
            return;
        }

        owner.BeginInvoke((MethodInvoker)(() =>
        {
            if (owner.IsDisposed || overviewSection.IsDisposed)
            {
                return;
            }

            string settingsPath = AppPaths.SettingsPath;
            overviewSection.ClearRows();
            overviewSection.AddRow(CreateInfoRow(
                L("Settings.AboutBuildStartup"),
                L("About.VersionBuild", AppInfo.MajorVersion, AppInfo.BuildNumber),
                details.StartupStatus));
            overviewSection.AddRow(new SettingsRow(
                CurrentTheme,
                L("Settings.AboutLocations"),
                L("Settings.AboutLocationsHelp"),
                CreateDualActionButtons(
                    new[]
                    {
                        (L("About.OpenInstallFolder"), (Action)(() => OpenFileLocation(details.InstallPath)), !string.Equals(details.InstallPath, L("About.NotInstalled"), StringComparison.OrdinalIgnoreCase)),
                        (L("About.OpenConfigFolder"), (Action)(() => OpenFileLocation(settingsPath)), true)
                    },
                    380),
                rightColumnWidth: 380));
            overviewSection.AddRow(CreateTextTileRow(
                L("Settings.UsageHelp"),
                L("About.HowToUseDetailed")));
            overviewSection.PerformLayout();
            if (owner.FindForm() is SettingsForm settingsForm)
            {
                settingsForm.FitToCurrentPage();
            }
        }));
    }

    private SettingsRow CreateToggleRow(string title, string description, bool initial, Action<bool> onChanged, int rightColumnWidth = 96, bool enabled = true, bool compact = false, Action<ToggleSwitchControl, SettingsRow>? onCreated = null)
    {
        var toggle = new ToggleSwitchControl(CurrentTheme)
        {
            IsOn = initial,
            Enabled = enabled
        };
        toggle.Click += (_, _) => onChanged(toggle.IsOn);
        var row = new SettingsRow(CurrentTheme, title, description, toggle, rightColumnWidth, compactDescription: compact)
        {
            Enabled = enabled
        };
        onCreated?.Invoke(toggle, row);
        return row;
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

    private SettingsRow CreateDropdownRow(string title, string description, string[] items, string current, Action<string> onChanged, Control? actionButton = null, int rightColumnWidth = 260, bool compact = false)
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

        return new SettingsRow(CurrentTheme, title, description, rightControl, rightColumnWidth, compactDescription: compact);
    }

    private SettingsRow CreateColorPaletteRow(string title, string description, Color selectedColor, Action<Color> onChanged)
    {
        var paletteControl = new ColorPaletteControl(CurrentTheme, BuildCursorColorPalette(), selectedColor)
        {
            Width = 264
        };
        paletteControl.ColorSelected += (_, color) => onChanged(Color.FromArgb(255, color));
        return new SettingsRow(CurrentTheme, title, description, paletteControl, rightColumnWidth: 280);
    }

    private SettingsRow CreateKeybindRow(string title, string description, string currentKeyLabel, Func<string?> onCustomize, int rightColumnWidth = 360, bool compact = false)
    {
        var badge = new KeyBadgeControl(CurrentTheme, currentKeyLabel)
        {
            Width = 198,
            Height = 34,
            Dock = DockStyle.Fill
        };
        badge.ApplyTheme(CurrentTheme);
        badge.Click += (_, _) =>
        {
            string? nextLabel = onCustomize();
            if (!string.IsNullOrWhiteSpace(nextLabel))
            {
                badge.Text = nextLabel;
            }
        };

        return new SettingsRow(CurrentTheme, title, description, badge, Math.Max(198, rightColumnWidth), compactDescription: compact);
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

    private static Color[] BuildCursorColorPalette() =>
    [
        Color.White,
        Color.Black,
        Color.FromArgb(248, 250, 252),
        Color.FromArgb(107, 114, 128),
        Color.FromArgb(31, 41, 55),
        Color.FromArgb(239, 68, 68),
        Color.FromArgb(220, 38, 38),
        Color.FromArgb(249, 115, 22),
        Color.FromArgb(245, 158, 11),
        Color.FromArgb(234, 179, 8),
        Color.FromArgb(250, 204, 21),
        Color.FromArgb(132, 204, 22),
        Color.FromArgb(34, 197, 94),
        Color.FromArgb(16, 185, 129),
        Color.FromArgb(20, 184, 166),
        Color.FromArgb(6, 182, 212),
        Color.FromArgb(14, 165, 233),
        Color.FromArgb(59, 130, 246),
        Color.FromArgb(37, 99, 235),
        Color.FromArgb(99, 102, 241),
        Color.FromArgb(139, 92, 246),
        Color.FromArgb(168, 85, 247),
        Color.FromArgb(217, 70, 239),
        Color.FromArgb(236, 72, 153),
        Color.FromArgb(244, 63, 94),
        Color.FromArgb(252, 165, 165),
        Color.FromArgb(253, 186, 116),
        Color.FromArgb(253, 224, 71),
        Color.FromArgb(134, 239, 172),
        Color.FromArgb(103, 232, 249),
        Color.FromArgb(147, 197, 253),
        Color.FromArgb(216, 180, 254),
        Color.FromArgb(251, 207, 232),
        Color.FromArgb(254, 202, 202),
        Color.FromArgb(209, 213, 219),
        Color.FromArgb(120, 113, 108)
    ];

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
            _settingsWindow.BeginInvoke((MethodInvoker)(() => RunGuarded("Settings.RefreshWindow.Invoke", () => RefreshSettingsWindow(page))));
            return;
        }

        if (_settingsWindow == null || _settingsWindow.IsDisposed)
        {
            return;
        }

        Point previousCenter = new(_settingsWindow.Left + _settingsWindow.Width / 2, _settingsWindow.Top + _settingsWindow.Height / 2);
        _settingsWindow.Close();
        ShowSettingsWindow(page, previousCenter);
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
        RunGuarded("Settings.ResetDefaultsConfirmTimer", CancelResetDefaultsConfirmation);
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
