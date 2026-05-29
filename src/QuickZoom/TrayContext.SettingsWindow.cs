using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
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

    private enum ShortcutKeyRole
    {
        Enable,
        Invert,
        FollowCursor
    }

    private enum ShortcutValidationLevel
    {
        None,
        Warning,
        Error
    }

    private readonly record struct ShortcutValidation(ShortcutValidationLevel Level, string Text);

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
        bool shouldStageDarkOpen = palette.MenuBackground.GetBrightness() < 0.5f;
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
            _colourblindMode,
            _resetDefaultsButton,
            BuildSettingsPageDefinitions());
        if (shouldStageDarkOpen)
        {
            form.Opacity = 0;
        }

        _iconRef ??= LoadEmbeddedIconBySuffix("magnifier_dark.ico");
        if (_iconRef != null)
        {
            form.Icon = (Icon)_iconRef.Clone();
        }

        WindowChrome.TrySetDarkTitleBar(form, shouldStageDarkOpen);
        if (shouldStageDarkOpen)
        {
            _ = form.Handle;
            WindowChrome.TrySetDarkTitleBar(form, enabled: true);
        }

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
        Point finalLocation = form.Location;
        if (shouldStageDarkOpen)
        {
            form.Location = new Point(
                SystemInformation.VirtualScreen.Left - form.Width - 200,
                SystemInformation.VirtualScreen.Top - form.Height - 200);
        }

        form.Show();
        if (shouldStageDarkOpen)
        {
            form.BeginInvoke((MethodInvoker)(() =>
            {
                if (!form.IsDisposed)
                {
                    form.Update();
                    form.Location = finalLocation;
                    form.Opacity = 1;
                    form.BringToFront();
                    form.Activate();
                }
            }));
            return;
        }

        form.BringToFront();
        form.Activate();
    }

    private IReadOnlyList<SettingsPageDefinition> BuildSettingsPageDefinitions() =>
    [
        new(typeof(GeneralSettingsPageView), L("Settings.General"), TrayFluentIcon.Settings, BuildGeneralSettingsPage),
        new(typeof(ZoomSettingsPageView), L("Settings.Zoom"), TrayFluentIcon.Zoom, BuildZoomSettingsPage),
        new(typeof(DisplaySettingsPageView), L("Settings.Display"), TrayFluentIcon.MagnifiedDisplays, BuildDisplaySettingsPage),
        new(typeof(CursorSettingsPageView), L("Settings.Cursor"), TrayFluentIcon.Cursor, BuildCursorSettingsPage),
        new(typeof(ShortcutsSettingsPageView), L("Settings.Input"), TrayFluentIcon.KeyBinds, BuildInputSettingsPage),
        new(typeof(AppearanceSettingsPageView), L("Settings.Appearance"), TrayFluentIcon.Appearance, BuildAppearanceSettingsPage),
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
            rightColumnWidth: 420));

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

        section.AddRow(CreateSliderRow(L("Settings.ZoomStep"), L("Settings.ZoomStepHelp"), _stepPercent, 1, 200, 5, value => value + "%", value =>
        {
            _stepPercent = value;
            SaveSettings();
        }, rightColumnWidth: 420));

        section.AddRow(CreateSliderRow(L("Settings.MaxZoom"), L("Settings.MaxZoomHelp"), _maxPercent, 150, 750, 10, value => value + "%", value =>
        {
            _maxPercent = value;
            ClampZoom();
            SaveSettings();
        }, rightColumnWidth: 420));

        section.AddRow(CreateDropdownRow(L("Settings.RefreshRate"), L("Settings.RefreshRateHelp"), BuildFpsItems(), FpsLabel(_fps), value =>
        {
            _fps = ParseFpsLabel(value);
            ApplyFps();
            SaveSettings();
        }, rightColumnWidth: 360));

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
            5,
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
            rightColumnWidth: 560));

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

        themeSection.AddRow(CreateToggleRow(L("Settings.ColourblindMode"), L("Settings.ColourblindModeHelp"), _colourblindMode, value =>
        {
            _colourblindMode = value;
            SaveSettings();
            RefreshMenuAndTrayUi(rebuildPopup: true);
            RefreshSettingsWindow(SettingsPage.Appearance);
        }, rightColumnWidth: 106));

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
        SettingsRow? enableKeyRow = null;
        SettingsRow? invertKeyRow = null;
        SettingsRow? followCursorKeyRow = null;

        void UpdateShortcutValidationRows()
        {
            ApplyShortcutValidation(enableKeyRow, GetShortcutValidation(ShortcutKeyRole.Enable));
            ApplyShortcutValidation(invertKeyRow, GetShortcutValidation(ShortcutKeyRole.Invert));
            ApplyShortcutValidation(followCursorKeyRow, GetShortcutValidation(ShortcutKeyRole.FollowCursor));
        }

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
            rightColumnWidth: 260,
            compact: true));

        enableKeyRow = CreateKeybindRow(
            L("Settings.EnableKey"),
            L("Settings.EnableKeyHelp"),
            KeyBadgeLabel(_enableKey),
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
                UpdateShortcutValidationRows();

                if (officeAltToggle != null)
                {
                    officeAltToggle.IsOn = _suppressAltKeyInOfficeApps && altEnableKey;
                    officeAltToggle.Enabled = altEnableKey;
                }

                if (officeAltRow != null)
                {
                    officeAltRow.Enabled = altEnableKey;
                }

                return KeyBadgeLabel(_enableKey);
            },
            rightColumnWidth: 210,
            compact: true);
        section.AddRow(enableKeyRow);

        invertKeyRow = CreateKeybindRow(
            L("Settings.InvertActivationKey"),
            L("Settings.InvertActivationKeyHelp"),
            KeyBadgeLabel(_invertKey),
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
                UpdateShortcutValidationRows();
                return KeyBadgeLabel(_invertKey);
            },
            rightColumnWidth: 210,
            compact: true);
        section.AddRow(invertKeyRow);

        followCursorKeyRow = CreateKeybindRow(
            L("Settings.FollowCursorHotkey"),
            L("Settings.FollowCursorHotkeyHelp"),
            KeyBadgeLabel(_followCursorKey),
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
                UpdateShortcutValidationRows();
                return KeyBadgeLabel(_followCursorKey);
            },
            rightColumnWidth: 210,
            compact: true);
        section.AddRow(followCursorKeyRow);
        UpdateShortcutValidationRows();

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

        var overviewSection = new SettingsSection(palette, string.Empty, string.Empty);
        overviewSection.AddRow(CreateInfoRow(
            L("Settings.AboutBuildStartup"),
            L("About.VersionBuild", AppInfo.ReleaseVersion, AppInfo.BuildNumber),
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
        (string InstallPath, string StartupStatus, StartupTaskStatus Status) details = await Task.Run(() =>
        {
            string installPath = InstalledAppService.GetCurrentInstalledExecutablePath() ?? notInstalled;
            StartupTaskInfo startupTaskInfo = StartupTaskService.GetStatusInfo(forceRefresh: true);
            StartupTaskStatus startupTaskStatus = startupTaskInfo.Status;
            string startupStatus = StartupTaskService.GetStatusLabel(language);
            return (installPath, startupStatus, startupTaskStatus);
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
                L("About.VersionBuild", AppInfo.ReleaseVersion, AppInfo.BuildNumber),
                details.StartupStatus,
                details.Status == StartupTaskStatus.Broken
                    ? CreateInlineActionButton(L("About.FixStartup"), RepairStartupServiceNow)
                    : null,
                details.Status == StartupTaskStatus.Broken ? 140 : 240));
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
            var debugLoggingRow = new SettingsRow(
                CurrentTheme,
                L("Settings.DebugLogging"),
                L("Settings.DebugLoggingHelp"),
                CreateDebugLoggingControls(),
                rightColumnWidth: 300)
            {
                MinimumSize = new Size(0, 92)
            };
            overviewSection.AddRow(debugLoggingRow);
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
            Enabled = enabled,
            ShowStateText = _colourblindMode
        };
        toggle.Click += (_, _) => onChanged(toggle.IsOn);
        var row = new SettingsRow(CurrentTheme, title, description, toggle, rightColumnWidth, compactDescription: compact)
        {
            Enabled = enabled
        };
        onCreated?.Invoke(toggle, row);
        return row;
    }

    private Control CreateDebugLoggingControls()
    {
        var host = new TableLayoutPanel
        {
            AutoSize = false,
            Width = 300,
            Height = 44,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 188));
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));

        var openLogButton = new ModernButton
        {
            Text = L("About.OpenLog"),
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 2, 8, 2)
        };
        openLogButton.ApplyTheme(CurrentTheme, emphasis: false);
        openLogButton.Click += (_, _) => OpenLogFile();
        host.Controls.Add(openLogButton, 0, 0);

        var toggle = new ToggleSwitchControl(CurrentTheme)
        {
            IsOn = _debugLoggingEnabled,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 2, 0, 2),
            ShowStateText = _colourblindMode
        };
        toggle.Click += (_, _) =>
        {
            _debugLoggingEnabled = toggle.IsOn;
            ErrorLog.Configure(_debugLoggingEnabled, AppInfo.VersionHash);
            SaveSettings();
        };
        host.Controls.Add(toggle, 1, 0);

        return host;
    }

    private static void OpenLogFile()
    {
        string logPath = AppPaths.AppDataLogPath;
        try
        {
            string? directory = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            ErrorLog.EnsureLogFileExists();

            Process.Start(new ProcessStartInfo
            {
                FileName = logPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ErrorLog.Write("OpenLog", ex);
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = "\"" + logPath + "\"",
                    UseShellExecute = false
                });
            }
            catch
            {
                // Best effort.
            }
        }
    }

    private void RepairStartupServiceNow()
    {
        try
        {
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath))
            {
                return;
            }

            Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas",
                Arguments = "--install-startup-task --startup-task-user " + QuoteProcessArgument(Environment.UserDomainName + "\\" + Environment.UserName)
            });
            ScheduleStartupStatusRefresh(3500);
            ScheduleStartupStatusRefresh(9000);
            if (process != null)
            {
                process.EnableRaisingEvents = true;
                process.Exited += (_, _) => ScheduleStartupStatusRefresh(800);
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write("StartupRepair", ex);
        }
    }

    private static string QuoteProcessArgument(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        if (value.IndexOfAny([' ', '\t', '\n', '\r', '"']) < 0)
        {
            return value;
        }

        var quoted = new StringBuilder();
        quoted.Append('"');
        int backslashCount = 0;
        foreach (char c in value)
        {
            if (c == '\\')
            {
                backslashCount++;
                continue;
            }

            if (c == '"')
            {
                quoted.Append('\\', (backslashCount * 2) + 1);
                quoted.Append('"');
                backslashCount = 0;
                continue;
            }

            if (backslashCount > 0)
            {
                quoted.Append('\\', backslashCount);
                backslashCount = 0;
            }

            quoted.Append(c);
        }

        if (backslashCount > 0)
        {
            quoted.Append('\\', backslashCount * 2);
        }

        quoted.Append('"');
        return quoted.ToString();
    }

    private void ScheduleStartupStatusRefresh(int delayMs)
    {
        if (_settingsWindow == null || _settingsWindow.IsDisposed)
        {
            return;
        }

        var timer = new System.Windows.Forms.Timer { Interval = delayMs };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            if (_settingsWindow == null || _settingsWindow.IsDisposed)
            {
                return;
            }

            StartupTaskService.InvalidateCache();
            RefreshSettingsWindow(SettingsPage.About);
        };
        timer.Start();
    }

    private SettingsRow CreateSliderRow(string title, string description, int value, int min, int max, int step, Func<int, string> valueFormatter, Action<int> onChanged, int rightColumnWidth = 420)
    {
        bool updatingFromSlider = false;
        bool updatingFromInput = false;
        bool showingPlaceholder = true;
        string placeholderText = valueFormatter(value);
        Color normalTextColor = CurrentTheme.Text;
        Color warningTextColor = _colourblindMode ? ShortcutWarningColor() : ShortcutErrorColor();
        Color placeholderTextColor = CurrentTheme.SecondaryText;
        Color inputBorderColor = ControlContrast.FieldBorder(CurrentTheme);
        Color inputBackColor = ControlContrast.FieldBackground(CurrentTheme);
        SettingsRow? row = null;
        var slider = new ModernSlider(CurrentTheme)
        {
            Minimum = min,
            Maximum = max,
            SnapStep = step,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 12, 0)
        };
        slider.SetExactValue(value);

        var valueInput = new TextBox
        {
            AutoSize = false,
            Width = 86,
            Height = 28,
            Text = placeholderText,
            TextAlign = HorizontalAlignment.Center,
            Font = ControlDrawing.UiFont("Segoe UI Semibold", 9f, FontStyle.Bold),
            ForeColor = placeholderTextColor,
            BackColor = inputBackColor,
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 3, 0, 0)
        };
        var valueInputFrame = new Panel
        {
            Width = 86,
            Height = 32,
            BackColor = inputBorderColor,
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            Margin = new Padding(0, 2, 0, 0),
            Padding = new Padding(1)
        };
        valueInputFrame.Controls.Add(valueInput);
        valueInputFrame.Click += (_, _) => valueInput.Focus();

        slider.ValueChanged += (_, _) =>
        {
            if (updatingFromInput)
            {
                return;
            }

            updatingFromSlider = true;
            valueInput.ForeColor = normalTextColor;
            placeholderText = valueFormatter(slider.Value);
            showingPlaceholder = true;
            valueInput.Text = placeholderText;
            row?.SetStatus(null, normalTextColor);
            updatingFromSlider = false;
            onChanged(slider.Value);
        };

        void ValidateInputText()
        {
            if (updatingFromSlider)
            {
                return;
            }

            if (showingPlaceholder || string.IsNullOrWhiteSpace(valueInput.Text))
            {
                valueInput.ForeColor = showingPlaceholder ? placeholderTextColor : normalTextColor;
                valueInputFrame.BackColor = inputBorderColor;
                row?.SetStatus(null, normalTextColor);
            }
            else if (TryParseSliderInput(valueInput.Text, out int entered) && entered >= min && entered <= max)
            {
                valueInput.ForeColor = normalTextColor;
                valueInputFrame.BackColor = inputBorderColor;
                row?.SetStatus(null, normalTextColor);
            }
            else
            {
                valueInput.ForeColor = warningTextColor;
                valueInputFrame.BackColor = warningTextColor;
                row?.SetStatus(L("Settings.SliderRangeWarning", min, max), warningTextColor);
            }
        }

        void CommitInput()
        {
            if (showingPlaceholder || string.IsNullOrWhiteSpace(valueInput.Text))
            {
                ShowPlaceholder();
                return;
            }

            if (!TryParseSliderInput(valueInput.Text, out int entered) || entered < min || entered > max)
            {
                ValidateInputText();
                return;
            }

            updatingFromInput = true;
            slider.SetExactValue(entered);
            placeholderText = valueFormatter(entered);
            showingPlaceholder = true;
            valueInput.ForeColor = normalTextColor;
            valueInput.Text = placeholderText;
            valueInputFrame.BackColor = inputBorderColor;
            row?.SetStatus(null, normalTextColor);
            updatingFromInput = false;
            onChanged(entered);
        }

        void ShowPlaceholder()
        {
            showingPlaceholder = true;
            valueInput.ForeColor = placeholderTextColor;
            valueInput.Text = placeholderText;
            valueInputFrame.BackColor = inputBorderColor;
            valueInput.SelectionStart = 0;
            valueInput.SelectionLength = 0;
        }

        valueInput.Enter += (_, _) =>
        {
            showingPlaceholder = false;
            valueInput.ForeColor = normalTextColor;
            valueInput.Text = string.Empty;
            row?.SetStatus(null, normalTextColor);
        };
        valueInput.MouseDown += (_, _) =>
        {
            if (showingPlaceholder)
            {
                valueInput.SelectionStart = 0;
                valueInput.SelectionLength = 0;
            }
        };
        valueInput.TextChanged += (_, _) => ValidateInputText();
        valueInput.Leave += (_, _) => CommitInput();
        valueInput.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                CommitInput();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                ShowPlaceholder();
                row?.SetStatus(null, normalTextColor);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };

        var host = new TableLayoutPanel
        {
            AutoSize = false,
            Width = rightColumnWidth,
            Height = 38,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        host.Controls.Add(slider, 0, 0);
        host.Controls.Add(valueInputFrame, 1, 0);

        string effectiveDescription = string.IsNullOrWhiteSpace(description)
            ? L("Settings.SliderManualHint")
            : description + " " + L("Settings.SliderManualHint");
        row = new SettingsRow(CurrentTheme, title, effectiveDescription, host, rightColumnWidth);
        return row;
    }

    private static bool TryParseSliderInput(string text, out int value)
    {
        string digits = new(text.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out value);
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
            Width = 180,
            Height = 74,
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

        return new SettingsRow(CurrentTheme, title, description, badge, Math.Max(180, rightColumnWidth), compactDescription: compact);
    }

    private string KeyBadgeLabel(Keys key)
    {
        return key is Keys.LWin or Keys.RWin ? "Win" : KeyLabel(key);
    }

    private void ApplyShortcutValidation(SettingsRow? row, ShortcutValidation validation)
    {
        if (row == null)
        {
            return;
        }

        Color color = validation.Level == ShortcutValidationLevel.Error
            ? ShortcutErrorColor()
            : ShortcutWarningColor();
        row.SetStatus(validation.Text, color);
    }

    private ShortcutValidation GetShortcutValidation(ShortcutKeyRole role)
    {
        Keys key = GetShortcutKey(role);
        if (!IsSupportedShortcutKey(key))
        {
            return new ShortcutValidation(ShortcutValidationLevel.Error, L("Settings.ShortcutErrorUnsupported"));
        }

        string conflictNames = GetShortcutConflictNames(role, key);
        if (!string.IsNullOrWhiteSpace(conflictNames))
        {
            return new ShortcutValidation(ShortcutValidationLevel.Error, L("Settings.ShortcutErrorConflictWith", conflictNames));
        }

        if (role == ShortcutKeyRole.Enable && IsWindowsKey(key))
        {
            return new ShortcutValidation(ShortcutValidationLevel.Warning, L("Settings.ShortcutWarningWindowsKey"));
        }

        if (role == ShortcutKeyRole.Enable && IsNotRecommendedEnableKey(key))
        {
            return new ShortcutValidation(ShortcutValidationLevel.Warning, L("Settings.ShortcutWarningEnableKey", KeyLabel(key)));
        }

        if (!IsRecommendedShortcutKey(key))
        {
            return new ShortcutValidation(ShortcutValidationLevel.Warning, L("Settings.ShortcutWarningNotRecommended"));
        }

        return new ShortcutValidation(ShortcutValidationLevel.None, string.Empty);
    }

    private Keys GetShortcutKey(ShortcutKeyRole role) => role switch
    {
        ShortcutKeyRole.Invert => _invertKey,
        ShortcutKeyRole.FollowCursor => _followCursorKey,
        _ => _enableKey
    };

    private string GetShortcutConflictNames(ShortcutKeyRole role, Keys key)
    {
        var conflicts = new List<string>();
        foreach (ShortcutKeyRole otherRole in Enum.GetValues<ShortcutKeyRole>())
        {
            if (otherRole != role && ShortcutKeyConflictId(GetShortcutKey(otherRole)) == ShortcutKeyConflictId(key))
            {
                conflicts.Add(GetShortcutRoleLabel(otherRole));
            }
        }

        return string.Join(", ", conflicts);
    }

    private static string ShortcutKeyConflictId(Keys key)
    {
        return key switch
        {
            Keys.ControlKey or Keys.LControlKey or Keys.RControlKey => "Ctrl",
            Keys.Menu or Keys.LMenu or Keys.RMenu => "Alt",
            Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey => "Shift",
            Keys.LWin or Keys.RWin => "Win",
            (Keys)FnVirtualKey => "Fn",
            _ => ((int)key).ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    private string GetShortcutRoleLabel(ShortcutKeyRole role) => role switch
    {
        ShortcutKeyRole.Invert => L("Settings.InvertActivationKey"),
        ShortcutKeyRole.FollowCursor => L("Settings.FollowCursorHotkey"),
        _ => L("Settings.EnableKey")
    };

    private static bool IsSupportedShortcutKey(Keys key)
    {
        if (key == (Keys)FnVirtualKey)
        {
            return true;
        }

        return key != Keys.None &&
               key != Keys.KeyCode &&
               key != Keys.Modifiers &&
               key != Keys.ProcessKey &&
               key != Keys.Packet;
    }

    private static bool IsNotRecommendedEnableKey(Keys key)
    {
        return key is Keys.RMenu or Keys.Enter or Keys.Return ||
               key == (Keys)FnVirtualKey;
    }

    private static bool IsWindowsKey(Keys key)
    {
        return key is Keys.LWin or Keys.RWin;
    }

    private static bool IsRecommendedShortcutKey(Keys key)
    {
        if (key is Keys.ControlKey or Keys.LControlKey or Keys.RControlKey ||
            key is Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey ||
            key is Keys.Menu or Keys.LMenu ||
            key == Keys.Tab)
        {
            return true;
        }

        if (key is Keys.Oemcomma or Keys.OemPeriod or Keys.OemMinus or Keys.Subtract)
        {
            return false;
        }

        return IsLetterKey(key) || IsNumberKey(key) || IsFunctionKey(key) || IsNonNordicPunctuationKey(key);
    }

    private static bool IsLetterKey(Keys key)
    {
        return (key >= Keys.A && key <= Keys.Z) ||
               key is Keys.Oem1 or Keys.Oem3 or Keys.Oem4 or Keys.Oem6 or Keys.Oem7 or Keys.Oem102;
    }

    private static bool IsFunctionKey(Keys key)
    {
        return key >= Keys.F1 && key <= Keys.F12;
    }

    private static bool IsNonNordicPunctuationKey(Keys key)
    {
        return key is Keys.Oem1 or Keys.Oem4 or Keys.Oem7;
    }

    private static bool IsNumberKey(Keys key)
    {
        return (key >= Keys.D0 && key <= Keys.D9) ||
               (key >= Keys.NumPad0 && key <= Keys.NumPad9);
    }

    private Color ShortcutWarningColor()
    {
        return _useDarkTheme ? Color.FromArgb(250, 204, 21) : Color.FromArgb(202, 138, 4);
    }

    private Color ShortcutErrorColor()
    {
        return _useDarkTheme ? Color.FromArgb(248, 113, 113) : Color.FromArgb(220, 38, 38);
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

    private string[] BuildFpsItems() =>
    [
        "60 Hz",
        "90 Hz",
        "120 Hz",
        "180 Hz",
        "240 Hz",
        FpsLabel(UnlimitedFps)
    ];

    private static string FpsLabel(int fps) => fps == UnlimitedFps ? "Unlimited" : fps + " Hz";

    private static int ParseFpsLabel(string value)
    {
        if (value.StartsWith("Unlimited", StringComparison.OrdinalIgnoreCase))
        {
            return UnlimitedFps;
        }

        foreach (int fps in _fpsOptions)
        {
            if (value.StartsWith(fps.ToString(), StringComparison.Ordinal))
            {
                return fps;
            }
        }

        return _fpsOptions[0];
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
            Height = 44,
            ColumnCount = buttons.Length,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        for (int i = 0; i < buttons.Length; i++)
        {
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / buttons.Length));
            var buttonSpec = buttons[i];

            var button = new ModernButton
            {
                Text = buttonSpec.Text,
                Enabled = buttonSpec.Enabled,
                Dock = DockStyle.Fill,
                Margin = new Padding(i == 0 ? 0 : 8, 2, 0, 2)
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
        if (_colourblindMode)
        {
            _resetDefaultsButton.ApplyWarningOutlineTheme(CurrentTheme, _pendingResetDefaultsConfirmation);
        }
        else
        {
            _resetDefaultsButton.ApplyTheme(
                CurrentTheme,
                emphasis: false,
                destructive: _pendingResetDefaultsConfirmation,
                destructiveHoverEnabled: true);
        }
    }
}
