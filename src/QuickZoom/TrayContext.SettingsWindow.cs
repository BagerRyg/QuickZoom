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

    private TrayModeButton[] _settingsZoomModeButtons = [];
    private SettingsSection? _settingsZoomModeSection;
    private Control? _settingsLensSizeRow;
    private Control? _settingsLensShapeRow;
    private Control? _settingsDockPositionRow;
    private Control? _settingsDockSizeRow;
    private System.Windows.Forms.Timer? _settingsZoomModeApplyTimer;

    private void ShowSettingsWindow(
        SettingsPage initialPage = SettingsPage.General,
        Point? restoreLocation = null,
        SettingsUiState? restoreUiState = null)
    {
        if (_settingsWindow != null && !_settingsWindow.IsDisposed)
        {
            _selectSettingsPageAction?.Invoke(initialPage);
            if (_settingsWindow.WindowState == FormWindowState.Minimized)
            {
                _settingsWindow.WindowState = FormWindowState.Normal;
            }

            if (!_settingsWindow.Visible &&
                _settingsWindow is SettingsForm existingForm &&
                existingForm.BackColor.GetBrightness() < 0.5f)
            {
                ShowStagedDarkSettingsWindow(existingForm, existingForm.Location);
                return;
            }

            if (!_settingsWindow.Visible && _settingsWindow is SettingsForm hiddenForm)
            {
                hiddenForm.PrepareForKeyboardEntry();
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
            _resetDefaultsButton,
            BuildSettingsPageDefinitions(),
            BuildSettingsSearchEntries(),
            L("Settings.SearchPlaceholder"),
            L("Settings.SearchNoResults"),
            L("Settings.SearchAccessibleDescription"));
        bool rightToLeft = UiText.IsRightToLeft(_language);
        form.RightToLeft = rightToLeft ? RightToLeft.Yes : RightToLeft.No;
        form.RightToLeftLayout = rightToLeft;
        if (shouldStageDarkOpen)
        {
            form.Opacity = 0;
        }

        _iconRef ??= LoadEmbeddedIconBySuffix("magnifier-dark.ico");
        if (_iconRef != null)
        {
            form.Icon = (Icon)_iconRef.Clone();
        }

        WindowChrome.TrySetDarkTitleBar(form, shouldStageDarkOpen);
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
        if (restoreUiState is SettingsUiState uiState)
        {
            form.RestoreUiState(uiState);
        }

        CenterSettingsWindow(form, restoreLocation);
        Point finalLocation = form.Location;
        if (shouldStageDarkOpen)
        {
            ShowStagedDarkSettingsWindow(form, finalLocation);
            return;
        }

        form.PrepareForKeyboardEntry();
        form.Show();
        form.BringToFront();
        form.Activate();
    }

    private static void ShowStagedDarkSettingsWindow(SettingsForm form, Point finalLocation)
    {
        form.PrepareForKeyboardEntry();
        form.Opacity = 0;
        form.Location = new Point(
            SystemInformation.VirtualScreen.Left - form.Width - 200,
            SystemInformation.VirtualScreen.Top - form.Height - 200);
        bool cloaked = WindowChrome.TrySetCloaked(form, cloaked: true);

        form.Show();
        form.RenderBeforeReveal();

        // Remove the layered-window transparency while the fully rendered form
        // is still off-screen (and cloaked when DWM supports it).
        form.Opacity = 1;
        form.RenderBeforeReveal();
        WindowChrome.TryFlushComposition();

        form.Location = finalLocation;
        form.RenderBeforeReveal();
        WindowChrome.TryFlushComposition();
        if (cloaked)
        {
            _ = WindowChrome.TrySetCloaked(form, cloaked: false);
        }

        form.BringToFront();
        form.Activate();
        WindowChrome.TryFlushComposition();
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

    private IReadOnlyList<SettingsSearchEntry> BuildSettingsSearchEntries()
    {
        var entries = new List<SettingsSearchEntry>();

        IEnumerable<string> LocalizedText(params string[] keys)
        {
            foreach (UiLanguage language in Enum.GetValues<UiLanguage>())
            {
                foreach (string key in keys)
                {
                    yield return UiText.Get(language, key);
                }
            }
        }

        string[] PageTextKeys(SettingsPage page) => page switch
        {
            SettingsPage.Display =>
            [
                "Settings.Display", "Settings.DisplayTitle", "Settings.DisplayDescription",
                "Settings.DisplaySection", "Settings.DisplaySelectionSection", "Settings.DisplaySectionHint"
            ],
            SettingsPage.Appearance =>
            [
                "Settings.Appearance", "Settings.AppearanceTitle", "Settings.AppearanceDescription",
                "Settings.AppearanceSection", "Settings.AppearanceSectionHint"
            ],
            SettingsPage.Cursor =>
            [
                "Settings.Cursor", "Settings.CursorTitle", "Settings.CursorDescription", "Settings.CursorSection"
            ],
            SettingsPage.Zoom =>
            [
                "Settings.Zoom", "Settings.ZoomTitle", "Settings.ZoomDescription",
                "Settings.ZoomModeSection", "Settings.ZoomSection", "Settings.ZoomSectionHint"
            ],
            SettingsPage.Input =>
            [
                "Settings.Input", "Settings.InputTitle", "Settings.InputDescription",
                "Settings.InputSection", "Settings.InputSectionHint"
            ],
            SettingsPage.About =>
            [
                "Settings.About", "Settings.AboutTitle", "Settings.AboutDescription", "Settings.AboutSection"
            ],
            _ =>
            [
                "Settings.General", "Settings.GeneralTitle", "Settings.GeneralDescription",
                "Settings.GeneralSection", "Settings.GeneralSectionHint"
            ]
        };

        IEnumerable<string> LocalizedFpsValues()
        {
            int[] values = [60, 90, 120, 180, 240];
            foreach (UiLanguage language in Enum.GetValues<UiLanguage>())
            {
                foreach (int fps in values)
                {
                    yield return UiText.Get(language, "Common.HertzValue", fps);
                }

                yield return UiText.Get(language, "Settings.FpsUnlimited");
            }
        }

        IEnumerable<string> LanguageNames()
        {
            foreach (UiLanguage language in Enum.GetValues<UiLanguage>())
            {
                yield return UiText.GetLanguageDisplayName(language, _language);
            }
        }

        void Add(SettingsPage page, string titleKey, string descriptionKey, params IEnumerable<string>[] keywordGroups)
        {
            string title = L(titleKey);
            string aliasKey = titleKey.Replace("Settings.", "Settings.SearchAliases.", StringComparison.Ordinal);
            IEnumerable<string> localizedKeywords = LocalizedText(
                PageTextKeys(page)
                    .Append(titleKey)
                    .Append(descriptionKey)
                    .Append(aliasKey)
                    .ToArray());
            entries.Add(new SettingsSearchEntry(
                GetSettingsPageType(page),
                GetSettingsPageLabel(page),
                title,
                L(descriptionKey),
                string.Join(" ", localizedKeywords.Concat(keywordGroups.SelectMany(group => group)))));
        }

        Add(SettingsPage.General, "Settings.SmoothZoom", "Settings.SmoothZoomHelp");
        Add(SettingsPage.General, "Settings.AutoDisableAt100", "Settings.AutoDisableAt100Help");
        Add(SettingsPage.General, "Settings.CenterCursor", "Settings.CenterCursorHelp");

        Add(SettingsPage.Display, "Settings.AutoSwitchMonitor", "Settings.AutoSwitchMonitorHelp");
        Add(SettingsPage.Display, "Settings.DisplaySelectionMode", "Settings.DisplaySelectionModeHelp", LocalizedText(
            "Settings.DisplayModeAll", "Settings.DisplayModeCursor", "Settings.DisplayModeCustom"));

        Add(SettingsPage.Zoom, "Settings.ZoomMode", "Settings.ZoomModeHelp", LocalizedText(
            "Settings.ZoomModeFullscreen", "Settings.ZoomModeLens", "Settings.ZoomModeDocked"));
        Add(SettingsPage.Zoom, "Settings.LensSize", "Settings.LensSizeHelp");
        Add(SettingsPage.Zoom, "Settings.LensShape", "Settings.LensShapeHelp", LocalizedText(
            "Settings.LensShapeRectangle", "Settings.LensShapeSquare", "Settings.LensShapeCircle"));
        Add(SettingsPage.Zoom, "Settings.DockPosition", "Settings.DockPositionHelp", LocalizedText(
            "Settings.DockTop", "Settings.DockBottom", "Settings.DockLeft", "Settings.DockRight"));
        Add(SettingsPage.Zoom, "Settings.DockSize", "Settings.DockSizeHelp");
        Add(SettingsPage.Zoom, "Settings.ZoomStep", "Settings.ZoomStepHelp");
        Add(SettingsPage.Zoom, "Settings.MaxZoom", "Settings.MaxZoomHelp");
        Add(SettingsPage.Zoom, "Settings.RefreshRate", "Settings.RefreshRateHelp", LocalizedFpsValues());

        Add(SettingsPage.Cursor, "Settings.WiggleSpotlight", "Settings.WiggleSpotlightHelp");
        Add(SettingsPage.Cursor, "Settings.CursorEnhancement", "Settings.CursorEnhancementHelp");
        Add(SettingsPage.Cursor, "Settings.CursorSize", "Settings.CursorSizeHelp");
        Add(SettingsPage.Cursor, "Settings.CursorFillColor", "Settings.CursorFillColorHelp", LocalizedText(CursorColorNameKeys));
        Add(SettingsPage.Cursor, "Settings.CursorBorderColor", "Settings.CursorBorderColorHelp", LocalizedText(CursorColorNameKeys));
        Add(SettingsPage.Cursor, "Settings.CursorPreview", "Settings.CursorPreviewHelp");

        Add(SettingsPage.Appearance, "Settings.ThemeMode", "Settings.ThemeModeHelp", LocalizedText(
            "Settings.ThemeAuto", "Settings.ThemeDark", "Settings.ThemeLight"));
        Add(SettingsPage.Appearance, "Settings.Language", "Settings.LanguageHelp", LanguageNames());
        Add(SettingsPage.Appearance, "Settings.FontSize", "Settings.FontSizeHelp", LocalizedText(
            "Settings.FontSizeDefault", "Settings.FontSizeLarge", "Settings.FontSizeExtraLarge"));

        Add(SettingsPage.Input, "Settings.ShortcutMode", "Settings.ShortcutModeHelp", LocalizedText(
            "Settings.ShortcutModeBoth", "Settings.ShortcutModeKeyboardOnly", "Settings.ShortcutModeMouseOnly"));
        Add(SettingsPage.Input, "Settings.EnableKey", "Settings.EnableKeyHelp");
        Add(SettingsPage.Input, "Settings.InvertActivationKey", "Settings.InvertActivationKeyHelp");
        Add(SettingsPage.Input, "Settings.FollowCursorHotkey", "Settings.FollowCursorHotkeyHelp");
        Add(SettingsPage.Input, "Settings.SuppressShortcutKeystrokes", "Settings.SuppressShortcutKeystrokesHelp");

        Add(SettingsPage.About, "Settings.AboutBuildStartup", "Settings.AboutDescription");
        Add(SettingsPage.About, "Settings.AboutLocations", "Settings.AboutLocationsHelp");
        Add(SettingsPage.About, "Settings.UsageHelp", "About.HowToUseDetailed");

        return entries;
    }

    private string GetSettingsPageLabel(SettingsPage page) => page switch
    {
        SettingsPage.Display => L("Settings.Display"),
        SettingsPage.Appearance => L("Settings.Appearance"),
        SettingsPage.Cursor => L("Settings.Cursor"),
        SettingsPage.Zoom => L("Settings.Zoom"),
        SettingsPage.Input => L("Settings.Input"),
        SettingsPage.About => L("Settings.About"),
        _ => L("Settings.General")
    };

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
        double largeTextProgress = Math.Clamp(
            (ControlDrawing.UiFontScale - 1f) / 0.28f,
            0f,
            1f);
        double widthRatio = 0.68d + (0.08d * largeTextProgress);
        double heightRatio = 0.80d + (0.05d * largeTextProgress);
        int minimumWidth = 1040 + (int)Math.Round(120d * largeTextProgress);
        int maximumWidth = 1520 + (int)Math.Round(240d * largeTextProgress);
        int width = Math.Clamp(
            (int)Math.Round(area.Width * widthRatio),
            minimumWidth,
            maximumWidth);
        int height = Math.Max(640, (int)Math.Round((area.Height - 32) * heightRatio));
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

        EventHandler? handleCreated = null;
        handleCreated = (_, _) =>
        {
            owner.HandleCreated -= handleCreated;
            _ = LoadDisplaySelectionSettingsAsync(owner);
        };
        owner.HandleCreated += handleCreated;
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

            _displaySelectionSettingsSection.BeginRowsUpdate();
            try
            {
                PopulateDisplaySelectionSettingsSection(monitors);
            }
            finally
            {
                _displaySelectionSettingsSection.EndRowsUpdate();
            }

            if (_currentSettingsPage == SettingsPage.Display && owner.FindForm() is SettingsForm settingsForm)
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
        var modeSection = new SettingsSection(palette, L("Settings.ZoomModeSection"), string.Empty);
        _settingsZoomModeSection = modeSection;
        modeSection.AddRow(CreateZoomModeButtonRow());

        _settingsLensSizeRow = CreateSliderRow(L("Settings.LensSize"), L("Settings.LensSizeHelp"), _lensSize, 100, 1400, 40, value => L("Common.PixelValue", value), value =>
        {
            _lensSize = NormalizeLensSize(value);
            SaveSettings();
            ApplyTransformCurrentPoint();
        }, rightColumnWidth: 420);
        _settingsLensShapeRow = CreateDropdownRow(L("Settings.LensShape"), L("Settings.LensShapeHelp"), BuildLensShapeItems(), LensShapeLabel(_lensShape), value =>
        {
            _lensShape = ParseLensShape(value);
            SaveSettings();
            ApplyTransformCurrentPoint();
        }, rightColumnWidth: 360);
        _settingsDockPositionRow = CreateDropdownRow(L("Settings.DockPosition"), L("Settings.DockPositionHelp"), BuildDockPositionItems(), DockPositionLabel(_dockPosition), value =>
        {
            _dockPosition = ParseDockPosition(value);
            SaveSettings();
            ApplyTransformCurrentPoint();
        }, rightColumnWidth: 360);
        _settingsDockSizeRow = CreateSliderRow(L("Settings.DockSize"), L("Settings.DockSizeHelp"), _dockSizePercent, 10, 50, 5, value => L("Common.PercentValue", value), value =>
        {
            _dockSizePercent = NormalizeDockSizePercent(value);
            SaveSettings();
            ApplyTransformCurrentPoint();
        }, rightColumnWidth: 420);

        modeSection.AddRow(_settingsLensSizeRow);
        modeSection.AddRow(_settingsLensShapeRow);
        modeSection.AddRow(_settingsDockPositionRow);
        modeSection.AddRow(_settingsDockSizeRow);
        UpdateSettingsZoomModeUi();

        var section = new SettingsSection(palette, L("Settings.ZoomSection"), string.Empty);

        section.AddRow(CreateSliderRow(L("Settings.ZoomStep"), L("Settings.ZoomStepHelp"), _stepPercent, 1, 200, 5, value => L("Common.PercentValue", value), value =>
        {
            _stepPercent = value;
            SaveSettings();
        }, rightColumnWidth: 420));

        section.AddRow(CreateSliderRow(L("Settings.MaxZoom"), L("Settings.MaxZoomHelp"), _maxPercent, 150, 750, 10, value => L("Common.PercentValue", value), value =>
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

        page.AddSection(modeSection);
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
        SettingsRow? fillColorRow = null;
        SettingsRow? borderColorRow = null;

        void UpdateCursorContrastWarning()
        {
            bool lowContrast = GetContrastRatio(
                Color.FromArgb(_cursorFillColorArgb),
                Color.FromArgb(_cursorBorderColorArgb)) < 3d;
            string? warning = lowContrast ? L("Accessibility.CursorContrastWarning") : null;
            Color warningColor = ShortcutWarningColor();
            fillColorRow?.SetStatus(warning, warningColor);
            borderColorRow?.SetStatus(warning, warningColor);
        }

        section.AddRow(CreateToggleRow(L("Settings.WiggleSpotlight"), L("Settings.WiggleSpotlightHelp"), _wiggleSpotlightEnabled, value =>
        {
            _wiggleSpotlightEnabled = value;
            if (!_wiggleSpotlightEnabled)
            {
                _cursorSpotlightTimer?.Stop();
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
            value => L("Common.PercentValue", value),
            value =>
            {
                _cursorScale = value;
                preview.ScalePercent = value;
                SaveSettings();
                ScheduleCursorEnhancementApply();
            },
            rightColumnWidth: 420));

        fillColorRow = CreateColorPaletteRow(
            L("Settings.CursorFillColor"),
            L("Settings.CursorFillColorHelp"),
            Color.FromArgb(_cursorFillColorArgb),
            color =>
            {
                _cursorFillColorArgb = color.ToArgb();
                preview.FillColor = color;
                ScheduleCursorEnhancementApply();
                SaveSettings();
                UpdateCursorContrastWarning();
            });
        section.AddRow(fillColorRow);

        borderColorRow = CreateColorPaletteRow(
            L("Settings.CursorBorderColor"),
            L("Settings.CursorBorderColorHelp"),
            Color.FromArgb(_cursorBorderColorArgb),
            color =>
            {
                _cursorBorderColorArgb = color.ToArgb();
                preview.BorderColor = color;
                ScheduleCursorEnhancementApply();
                SaveSettings();
                UpdateCursorContrastWarning();
            });
        section.AddRow(borderColorRow);
        UpdateCursorContrastWarning();

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

        page.AddSection(themeSection);
        return page;
    }

    private SettingsPageView BuildInputSettingsPage()
    {
        ThemePalette palette = CurrentTheme;
        var page = new ShortcutsSettingsPageView(palette, L("Settings.InputTitle"), L("Settings.InputDescription"));
        var section = new SettingsSection(palette, L("Settings.InputSection"), string.Empty);
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
                _enableKeyPressed = false;
                ResetEnableKeySuppressionState();
                _suppressedShortcutKeyUps.Clear();
                SaveSettings();
                RefreshMenuAndTrayUi(rebuildPopup: true);
                UpdateShortcutValidationRows();

                return KeyBadgeLabel(_enableKey);
            },
            rightColumnWidth: 170,
            compact: true,
            showWindowsLogo: () => IsWindowsKey(_enableKey));
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
            rightColumnWidth: 170,
            compact: true,
            showWindowsLogo: () => IsWindowsKey(_invertKey));
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
            rightColumnWidth: 170,
            compact: true,
            showWindowsLogo: () => IsWindowsKey(_followCursorKey));
        section.AddRow(followCursorKeyRow);
        UpdateShortcutValidationRows();

        section.AddRow(CreateToggleRow(
            L("Settings.SuppressShortcutKeystrokes"),
            L("Settings.SuppressShortcutKeystrokesHelp"),
            _suppressShortcutKeystrokes,
            value =>
            {
                _suppressShortcutKeystrokes = value;
                ResetEnableKeySuppressionState();
                _suppressedShortcutKeyUps.Clear();
                SaveSettings();
            },
            rightColumnWidth: 96,
            compact: true));

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
        if (_debugLoggingEnabled)
        {
            overviewSection.AddRow(CreateInfoRow(
                L("About.ThemeEngine"),
                ThemeEngineStatusText(),
                string.Empty));
        }
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

        EventHandler? handleCreated = null;
        handleCreated = (_, _) =>
        {
            owner.HandleCreated -= handleCreated;
            _ = LoadAboutSettingsAsync(owner, overviewSection);
        };
        owner.HandleCreated += handleCreated;
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
            overviewSection.BeginRowsUpdate();
            try
            {
                overviewSection.ClearRows();
                overviewSection.AddRow(CreateInfoRow(
                    L("Settings.AboutBuildStartup"),
                    L("About.VersionBuild", AppInfo.ReleaseVersion, AppInfo.BuildNumber),
                    details.StartupStatus,
                    details.Status != StartupTaskStatus.Ready
                        ? CreateInlineActionButton(L("About.ConfigureStartup"), ConfigureStartupServiceNow)
                        : null,
                    details.Status != StartupTaskStatus.Ready ? 180 : 240));
                if (_debugLoggingEnabled)
                {
                    overviewSection.AddRow(CreateInfoRow(
                        L("About.ThemeEngine"),
                        ThemeEngineStatusText(),
                        string.Empty));
                }
                int locationActionsWidth = ControlDrawing.ScaleLogical(owner, 272);
                Control locationActions = CreateDualActionButtons(
                    new[]
                    {
                        (L("About.OpenInstallFolder"), (Action)(() => OpenFileLocation(details.InstallPath)), !string.Equals(details.InstallPath, L("About.NotInstalled"), StringComparison.OrdinalIgnoreCase)),
                        (L("About.OpenConfigFolder"), (Action)(() => OpenFileLocation(settingsPath)), true)
                    },
                    locationActionsWidth);
                var locationsRow = new SettingsRow(
                    CurrentTheme,
                    L("Settings.AboutLocations"),
                    L("Settings.AboutLocationsHelp"),
                    locationActions,
                    rightColumnWidth: locationActionsWidth);
                locationActions.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                locationActions.Margin = new Padding(0);
                if (locationActions.Parent is TableLayoutPanel locationActionsHost)
                {
                    locationActionsHost.AutoSize = false;
                    bool aligningLocationActions = false;
                    void AlignLocationActions()
                    {
                        if (aligningLocationActions)
                        {
                            return;
                        }

                        Point target = new(
                            Math.Max(0, locationActionsHost.ClientSize.Width - locationActions.Width),
                            locationActionsHost.Padding.Top);
                        if (locationActions.Location != target)
                        {
                            aligningLocationActions = true;
                            try
                            {
                                locationActions.Location = target;
                            }
                            finally
                            {
                                aligningLocationActions = false;
                            }
                        }
                    }

                    locationActionsHost.Layout += (_, _) => AlignLocationActions();
                    locationActionsHost.SizeChanged += (_, _) => AlignLocationActions();
                    locationActions.LocationChanged += (_, _) => AlignLocationActions();
                    locationActions.SizeChanged += (_, _) => AlignLocationActions();
                    locationActionsHost.PerformLayout();
                    AlignLocationActions();
                }
                overviewSection.AddRow(locationsRow);
                overviewSection.AddRow(CreateTextTileRow(
                    L("Settings.UsageHelp"),
                    L("About.HowToUseDetailed")));
            }
            finally
            {
                overviewSection.EndRowsUpdate();
            }

            if (_currentSettingsPage == SettingsPage.About && owner.FindForm() is SettingsForm settingsForm)
            {
                settingsForm.FitToCurrentPage();
            }
        }));
    }

    private string ThemeEngineStatusText() => AppThemeBootstrap.NativeColorModeActive
        ? L("About.ThemeEngineNative")
        : L("About.ThemeEngineFallback");

    private SettingsRow CreateToggleRow(string title, string description, bool initial, Action<bool> onChanged, int rightColumnWidth = 96, bool enabled = true, bool compact = false, Action<ToggleSwitchControl, SettingsRow>? onCreated = null)
    {
        var toggle = new ToggleSwitchControl(CurrentTheme)
        {
            IsOn = initial,
            Enabled = enabled,
            ShowStateText = true,
            OnText = L("Common.On"),
            OffText = L("Common.Off"),
            AccessibleName = title,
            AccessibleDescription = string.IsNullOrWhiteSpace(description)
                ? L("Accessibility.ToggleInstruction")
                : description + " " + L("Accessibility.ToggleInstruction")
        };
        toggle.Click += (_, _) => onChanged(toggle.IsOn);
        var row = new SettingsRow(CurrentTheme, title, description, toggle, rightColumnWidth, compactDescription: compact)
        {
            Enabled = enabled
        };
        onCreated?.Invoke(toggle, row);
        return row;
    }

    private Control CreateZoomModeButtonRow()
    {
        ThemePalette palette = CurrentTheme;
        Control scaleOwner = _settingsWindow != null && !_settingsWindow.IsDisposed ? _settingsWindow : _uiInvoker;
        var surface = new ModernSurfacePanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            CornerRadius = 9,
            BorderAlpha = 20,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(14, 12, 14, 12),
            BackColor = palette.ControlBackground,
            AccessibleName = L("Settings.ZoomMode"),
            AccessibleDescription = L("Settings.ZoomModeHelp"),
            AccessibleRole = AccessibleRole.Grouping
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            Text = L("Settings.ZoomMode"),
            AutoSize = true,
            Font = ControlDrawing.UiFont("Segoe UI Semibold", 10f, FontStyle.Bold),
            Margin = new Padding(0),
            BackColor = Color.Transparent,
            ForeColor = palette.Text
        };

        var descriptionLabel = new Label
        {
            Text = L("Settings.ZoomModeHelp"),
            AutoSize = true,
            Font = ControlDrawing.UiFont("Segoe UI", 8.8f, FontStyle.Regular),
            Margin = new Padding(0, 4, 0, 0),
            BackColor = Color.Transparent,
            ForeColor = palette.SecondaryText
        };

        var buttonsHost = new FlowLayoutPanel
        {
            AutoSize = false,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, ControlDrawing.ScaleLogical(scaleOwner, 12), 0, 0),
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };

        TrayModeButton[] buttons =
        [
            CreateSettingsZoomModeButton(palette, ZoomMode.Fullscreen, TrayFluentIcon.ZoomModeFullscreen, L("Settings.ZoomModeFullscreen"), L("Settings.ZoomModeFullscreenDescription")),
            CreateSettingsZoomModeButton(palette, ZoomMode.Lens, TrayFluentIcon.ZoomModeLens, L("Settings.ZoomModeLens"), L("Settings.ZoomModeLensDescription")),
            CreateSettingsZoomModeButton(palette, ZoomMode.Docked, TrayFluentIcon.ZoomModeDocked, L("Settings.ZoomModeDocked"), L("Settings.ZoomModeDockedDescription"))
        ];
        _settingsZoomModeButtons = buttons;

        foreach (TrayModeButton button in buttons)
        {
            buttonsHost.Controls.Add(button);
        }

        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(descriptionLabel, 0, 1);
        layout.Controls.Add(buttonsHost, 0, 2);
        surface.Controls.Add(layout);

        void ArrangeButtons()
        {
            int innerWidth = Math.Max(ControlDrawing.ScaleLogical(surface, 360), surface.ClientSize.Width - surface.Padding.Horizontal);
            int maxTextWidth = Math.Min(innerWidth, ControlDrawing.ScaleLogical(surface, 620));
            titleLabel.MaximumSize = new Size(innerWidth, 0);
            descriptionLabel.MaximumSize = new Size(maxTextWidth, 0);
            layout.Width = innerWidth;
            buttonsHost.Width = innerWidth;

            int gap = ControlDrawing.ScaleLogical(surface, 8);
            int rowHeight = 0;
            int widestPreferredButton = 0;
            foreach (TrayModeButton button in buttons)
            {
                Size preferred = button.GetPreferredSizeForOwner(surface);
                rowHeight = Math.Max(rowHeight, preferred.Height);
                widestPreferredButton = Math.Max(widestPreferredButton, preferred.Width);
            }

            int equalWidth = Math.Max(1, (innerWidth - (gap * 2)) / 3);
            bool stackButtons = equalWidth < widestPreferredButton;
            buttonsHost.FlowDirection = stackButtons ? FlowDirection.TopDown : FlowDirection.LeftToRight;
            buttonsHost.Height = stackButtons
                ? (rowHeight * buttons.Length) + (gap * (buttons.Length - 1))
                : rowHeight;

            for (int i = 0; i < buttons.Length; i++)
            {
                TrayModeButton button = buttons[i];
                button.Size = stackButtons
                    ? new Size(innerWidth, rowHeight)
                    : new Size(equalWidth, rowHeight);
                button.Margin = stackButtons
                    ? new Padding(0, 0, 0, i == buttons.Length - 1 ? 0 : gap)
                    : new Padding(0, 0, i == buttons.Length - 1 ? 0 : gap, 0);
            }

            layout.PerformLayout();
            surface.PerformLayout();
        }

        surface.Resize += (_, _) => ArrangeButtons();
        surface.HandleCreated += (_, _) => ArrangeButtons();
        ArrangeButtons();
        return surface;
    }

    private TrayModeButton CreateSettingsZoomModeButton(ThemePalette palette, ZoomMode mode, TrayFluentIcon icon, string label, string description)
    {
        var button = new TrayModeButton(palette, icon, label, description)
        {
            Selected = _zoomMode == mode,
            Margin = new Padding(0),
            Tag = mode
        };
        button.Click += (_, _) => SetZoomModeFromSettings(mode);
        button.NavigationExitRequested += (_, _) =>
        {
            if (_settingsWindow is SettingsForm form)
            {
                _ = form.FocusSidebarFromContent();
            }
        };
        return button;
    }

    private void SetZoomModeFromSettings(ZoomMode nextMode)
    {
        if (nextMode == _zoomMode)
        {
            return;
        }

        _zoomMode = nextMode;
        _monitorLayoutDirty = true;

        UpdateSettingsZoomModeUi();
        SaveSettings();
        ScheduleSettingsZoomModeApply();
        UpdateTrayPopupState();
    }

    private void UpdateSettingsZoomModeUi()
    {
        SettingsForm? settingsForm = _settingsWindow is SettingsForm { IsDisposed: false } form ? form : null;
        settingsForm?.BeginAtomicUpdate();
        try
        {
            foreach (TrayModeButton button in _settingsZoomModeButtons)
            {
                if (!button.IsDisposed && button.Tag is ZoomMode mode)
                {
                    button.Selected = mode == _zoomMode;
                }
            }

            if (_settingsZoomModeSection is not { IsDisposed: false } modeSection)
            {
                return;
            }

            modeSection.BeginRowsUpdate();
            try
            {
                SetSettingsRowVisible(_settingsLensSizeRow, _zoomMode == ZoomMode.Lens);
                SetSettingsRowVisible(_settingsLensShapeRow, _zoomMode == ZoomMode.Lens);
                SetSettingsRowVisible(_settingsDockPositionRow, _zoomMode == ZoomMode.Docked);
                SetSettingsRowVisible(_settingsDockSizeRow, _zoomMode == ZoomMode.Docked);
            }
            finally
            {
                modeSection.EndRowsUpdate();
            }
        }
        finally
        {
            settingsForm?.EndAtomicUpdate();
        }
    }

    private static void SetSettingsRowVisible(Control? row, bool visible)
    {
        if (row is { IsDisposed: false } && row.Visible != visible)
        {
            row.Visible = visible;
        }
    }

    private void ScheduleSettingsZoomModeApply()
    {
        if (_zoomPercent <= 100 && !_invertColors)
        {
            return;
        }

        if (_settingsZoomModeApplyTimer == null)
        {
            _settingsZoomModeApplyTimer = new System.Windows.Forms.Timer { Interval = 120 };
            _settingsZoomModeApplyTimer.Tick += (_, _) =>
            {
                _settingsZoomModeApplyTimer.Stop();
                RunGuarded("Settings.ApplyZoomMode", ApplyTransformCurrentPoint);
            };
        }

        _settingsZoomModeApplyTimer.Stop();
        _settingsZoomModeApplyTimer.Start();
    }


    private void ConfigureStartupServiceNow()
    {
        try
        {
            if (_settingsWindow == null || _settingsWindow.IsDisposed)
            {
                return;
            }

            _ = FirstRunSetup.ShowStartupServiceOnly(_settingsWindow);
            StartupTaskService.InvalidateCache();
            RefreshSettingsWindow(SettingsPage.About);
        }
        catch (Exception ex)
        {
            ErrorLog.Write("StartupRepair", ex);
        }
    }

    private SettingsRow CreateSliderRow(string title, string description, int value, int min, int max, int step, Func<int, string> valueFormatter, Action<int> onChanged, int rightColumnWidth = 420)
    {
        bool updatingFromSlider = false;
        bool updatingFromInput = false;
        bool showingPlaceholder = false;
        string placeholderText = valueFormatter(value);
        Color normalTextColor = CurrentTheme.Text;
        Color warningTextColor = ShortcutErrorColor();
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
            Margin = new Padding(0, 4, 12, 0),
            AccessibleName = title,
            AccessibleDescription = string.IsNullOrWhiteSpace(description)
                ? L("Accessibility.SliderInstruction")
                : description + " " + L("Accessibility.SliderInstruction")
        };
        slider.SetExactValue(value);

        Font valueInputFont = ControlDrawing.UiFont("Segoe UI Semibold", 9f, FontStyle.Bold);
        string[] representativeValues = [valueFormatter(min), valueFormatter(max), valueFormatter(value)];
        int widestValue = representativeValues.Max(formattedValue =>
            TextRenderer.MeasureText(
                formattedValue,
                valueInputFont,
                Size.Empty,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width);
        int valueInputFrameWidth = Math.Clamp(widestValue + 18, 62, 92);
        const int valueInputFrameHeight = 34;
        var valueInput = new CompactNumericTextBox
        {
            AutoSize = false,
            Text = placeholderText,
            TextAlign = HorizontalAlignment.Center,
            Font = valueInputFont,
            ForeColor = normalTextColor,
            BackColor = inputBackColor,
            BorderStyle = BorderStyle.None,
            MinimumValue = min,
            MaximumValue = max,
            ValueFormatter = valueFormatter,
            MaxLength = representativeValues.Max(formattedValue => formattedValue.Length),
            AccessibleName = title,
            AccessibleDescription = string.IsNullOrWhiteSpace(description)
                ? L("Accessibility.SliderInstruction")
                : description + " " + L("Accessibility.SliderInstruction"),
            AccessibleRole = AccessibleRole.SpinButton
        };
        slider.CaptureValueChanged = captureValue =>
        {
            placeholderText = valueFormatter(captureValue);
            showingPlaceholder = false;
            valueInput.ForeColor = normalTextColor;
            valueInput.Text = placeholderText;
        };
        var valueInputFrame = new Panel
        {
            Width = valueInputFrameWidth,
            Height = valueInputFrameHeight,
            BackColor = inputBorderColor,
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            Margin = new Padding(0, (48 - valueInputFrameHeight) / 2, 0, 0),
            Padding = new Padding(1)
        };
        valueInputFrame.Controls.Add(valueInput);
        valueInputFrame.Click += (_, _) => valueInput.Focus();

        void LayoutValueInput()
        {
            int inputHeight = Math.Min(
                Math.Max(valueInput.PreferredHeight, valueInput.Font.Height + 2),
                Math.Max(1, valueInputFrame.ClientSize.Height - valueInputFrame.Padding.Vertical));
            valueInput.Bounds = new Rectangle(
                valueInputFrame.Padding.Left,
                Math.Max(valueInputFrame.Padding.Top, (valueInputFrame.ClientSize.Height - inputHeight) / 2),
                Math.Max(1, valueInputFrame.ClientSize.Width - valueInputFrame.Padding.Horizontal),
                inputHeight);
        }

        valueInputFrame.Resize += (_, _) => LayoutValueInput();
        LayoutValueInput();

        void UpdateInputBorder(bool invalid = false)
        {
            valueInputFrame.BackColor = invalid
                ? warningTextColor
                : valueInput.Focused || ReferenceEquals(ControlDrawing.FocusCaptureTarget, valueInput)
                    ? ControlDrawing.FocusColor(CurrentTheme)
                    : inputBorderColor;
        }

        slider.ValueChanged += (_, _) =>
        {
            if (updatingFromInput)
            {
                return;
            }

            updatingFromSlider = true;
            valueInput.ForeColor = normalTextColor;
            placeholderText = valueFormatter(slider.Value);
            showingPlaceholder = false;
            valueInput.Text = placeholderText;
            row?.SetStatus(null, normalTextColor);
            updatingFromSlider = false;
            onChanged(slider.Value);
        };

        void ValidateInputText()
        {
            if (updatingFromSlider || updatingFromInput)
            {
                return;
            }

            if (showingPlaceholder || string.IsNullOrWhiteSpace(valueInput.Text))
            {
                valueInput.ForeColor = showingPlaceholder ? placeholderTextColor : normalTextColor;
                UpdateInputBorder();
                row?.SetStatus(null, normalTextColor);
            }
            else if (valueInput.TryGetNumericValue(out _))
            {
                valueInput.ForeColor = normalTextColor;
                UpdateInputBorder();
                row?.SetStatus(null, normalTextColor);
            }
            else
            {
                valueInput.ForeColor = warningTextColor;
                UpdateInputBorder(invalid: true);
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

            if (!valueInput.TryGetNumericValue(out int entered))
            {
                updatingFromInput = true;
                valueInput.Text = placeholderText;
                valueInput.ForeColor = normalTextColor;
                UpdateInputBorder(invalid: true);
                row?.SetStatus(L("Settings.SliderRangeWarning", min, max), warningTextColor);
                updatingFromInput = false;
                valueInput.SelectAll();
                return;
            }

            int previousValue = slider.Value;
            updatingFromInput = true;
            slider.SetExactValue(entered);
            placeholderText = valueFormatter(entered);
            showingPlaceholder = false;
            valueInput.ForeColor = normalTextColor;
            valueInput.Text = placeholderText;
            UpdateInputBorder();
            row?.SetStatus(null, normalTextColor);
            updatingFromInput = false;
            valueInput.SelectAll();
            if (entered != previousValue)
            {
                onChanged(entered);
            }
        }

        void ShowPlaceholder()
        {
            showingPlaceholder = false;
            valueInput.ForeColor = normalTextColor;
            valueInput.Text = placeholderText;
            UpdateInputBorder();
            valueInput.SelectAll();
        }

        valueInput.Enter += (_, _) =>
        {
            showingPlaceholder = false;
            valueInput.ForeColor = normalTextColor;
            UpdateInputBorder();
            valueInput.SelectAll();
            row?.SetStatus(null, normalTextColor);
        };
        valueInput.TextChanged += (_, _) => ValidateInputText();
        valueInput.CommitRequested += (_, _) => CommitInput();
        valueInput.CancelRequested += (_, _) =>
        {
            ShowPlaceholder();
            row?.SetStatus(null, normalTextColor);
        };
        valueInput.Leave += (_, _) =>
        {
            CommitInput();
            UpdateInputBorder();
        };
        valueInput.KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown)
            {
                int direction = e.KeyCode is Keys.Up or Keys.PageUp ? 1 : -1;
                int multiplier = e.KeyCode is Keys.PageUp or Keys.PageDown ? 5 : 1;
                slider.SetExactValue(Math.Clamp(slider.Value + (direction * step * multiplier), min, max));
                valueInput.SelectAll();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };

        var host = new TableLayoutPanel
        {
            AutoSize = false,
            Width = rightColumnWidth,
            Height = 48,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, valueInputFrameWidth));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        host.Controls.Add(slider, 0, 0);
        host.Controls.Add(valueInputFrame, 1, 0);

        string effectiveDescription = string.IsNullOrWhiteSpace(description)
            ? L("Settings.SliderManualHint")
            : description + " " + L("Settings.SliderManualHint");
        row = new SettingsRow(CurrentTheme, title, effectiveDescription, host, rightColumnWidth);
        return row;
    }

    private SettingsRow CreateDropdownRow(string title, string description, string[] items, string current, Action<string> onChanged, Control? actionButton = null, int rightColumnWidth = 260, bool compact = false)
    {
        var combo = new ModernDropdown(CurrentTheme)
        {
            AccessibleName = title,
            AccessibleDescription = description
        };
        combo.Items.AddRange(items);
        combo.Width = combo.GetPreferredSize(Size.Empty).Width;
        combo.SelectedIndex = Math.Max(0, combo.Items.IndexOf(current));
        combo.SelectedIndexChanged += (_, _) =>
        {
            if (combo.SelectedItem is string selected)
            {
                onChanged(selected);
            }
        };

        Control rightControl;
        int effectiveRightColumnWidth;
        if (actionButton == null)
        {
            rightControl = combo;
            effectiveRightColumnWidth = Math.Max(
                rightColumnWidth,
                combo.Width + ControlDrawing.ScaleLogical(combo, 24));
        }
        else
        {
            int actionGap = ControlDrawing.ScaleLogical(combo, 10);
            effectiveRightColumnWidth = Math.Max(
                rightColumnWidth,
                combo.Width + actionButton.Width + actionGap);
            var row = new TableLayoutPanel
            {
                AutoSize = false,
                Width = effectiveRightColumnWidth,
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

        return new SettingsRow(CurrentTheme, title, description, rightControl, effectiveRightColumnWidth, compactDescription: compact);
    }

    private SettingsRow CreateColorPaletteRow(string title, string description, Color selectedColor, Action<Color> onChanged)
    {
        var paletteControl = new ColorPaletteControl(CurrentTheme, BuildCursorColorPalette(), selectedColor)
        {
            Width = 600,
            AccessibleName = title,
            AccessibleDescription = description,
            AccessibleColorNames = BuildCursorColorAccessibleNames()
        };
        paletteControl.ColorSelected += (_, color) => onChanged(Color.FromArgb(255, color));
        return new SettingsRow(CurrentTheme, title, description, paletteControl, rightColumnWidth: 620);
    }

    private SettingsRow CreateKeybindRow(
        string title,
        string description,
        string currentKeyLabel,
        Func<string?> onCustomize,
        int rightColumnWidth = 170,
        bool compact = false,
        Func<bool>? showWindowsLogo = null)
    {
        var badge = new KeyBadgeControl(CurrentTheme, currentKeyLabel)
        {
            Width = 150,
            Height = 92,
            Dock = DockStyle.Fill,
            AccessibleName = title,
            AccessibleDescription = description,
            ShowWindowsLogo = showWindowsLogo?.Invoke() == true
        };
        badge.ApplyTheme(CurrentTheme);
        badge.Click += (_, _) =>
        {
            string? nextLabel = onCustomize();
            if (!string.IsNullOrWhiteSpace(nextLabel))
            {
                badge.Text = nextLabel;
                badge.ShowWindowsLogo = showWindowsLogo?.Invoke() == true;
            }
        };

        return new SettingsRow(CurrentTheme, title, description, badge, Math.Max(160, rightColumnWidth), compactDescription: compact);
    }

    private string KeyBadgeLabel(Keys key)
    {
        return key is Keys.LWin or Keys.RWin ? L("Common.KeyWinShort") : KeyLabel(key);
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
            key is Keys.LWin or Keys.RWin ||
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

    private string[] BuildZoomModeItems() =>
    [
        L("Settings.ZoomModeFullscreen"),
        L("Settings.ZoomModeLens"),
        L("Settings.ZoomModeDocked")
    ];

    private string ZoomModeLabel(ZoomMode mode) => mode switch
    {
        ZoomMode.Lens => L("Settings.ZoomModeLens"),
        ZoomMode.Docked => L("Settings.ZoomModeDocked"),
        _ => L("Settings.ZoomModeFullscreen")
    };

    private ZoomMode ParseZoomMode(string value)
    {
        if (string.Equals(value, L("Settings.ZoomModeLens"), StringComparison.Ordinal))
        {
            return ZoomMode.Lens;
        }

        if (string.Equals(value, L("Settings.ZoomModeDocked"), StringComparison.Ordinal))
        {
            return ZoomMode.Docked;
        }

        return ZoomMode.Fullscreen;
    }

    private string[] BuildLensShapeItems() =>
    [
        L("Settings.LensShapeRectangle"),
        L("Settings.LensShapeSquare"),
        L("Settings.LensShapeCircle")
    ];

    private string LensShapeLabel(LensShape shape) => shape switch
    {
        LensShape.Square => L("Settings.LensShapeSquare"),
        LensShape.Circle => L("Settings.LensShapeCircle"),
        _ => L("Settings.LensShapeRectangle")
    };

    private LensShape ParseLensShape(string value)
    {
        if (string.Equals(value, L("Settings.LensShapeSquare"), StringComparison.Ordinal))
        {
            return LensShape.Square;
        }

        if (string.Equals(value, L("Settings.LensShapeCircle"), StringComparison.Ordinal))
        {
            return LensShape.Circle;
        }

        return LensShape.Rectangle;
    }

    private string[] BuildDockPositionItems() =>
    [
        L("Settings.DockTop"),
        L("Settings.DockBottom"),
        L("Settings.DockLeft"),
        L("Settings.DockRight")
    ];

    private string DockPositionLabel(DockPosition position) => position switch
    {
        DockPosition.Bottom => L("Settings.DockBottom"),
        DockPosition.Left => L("Settings.DockLeft"),
        DockPosition.Right => L("Settings.DockRight"),
        _ => L("Settings.DockTop")
    };

    private DockPosition ParseDockPosition(string value)
    {
        if (string.Equals(value, L("Settings.DockBottom"), StringComparison.Ordinal))
        {
            return DockPosition.Bottom;
        }

        if (string.Equals(value, L("Settings.DockLeft"), StringComparison.Ordinal))
        {
            return DockPosition.Left;
        }

        if (string.Equals(value, L("Settings.DockRight"), StringComparison.Ordinal))
        {
            return DockPosition.Right;
        }

        return DockPosition.Top;
    }

    private string[] BuildTrackingSourceItems() =>
    [
        L("Settings.TrackingMouse"),
        L("Settings.TrackingFocus"),
        L("Settings.TrackingCaret"),
        L("Settings.TrackingSelection")
    ];

    private string TrackingSourceLabel(TrackingSource source) => source switch
    {
        TrackingSource.KeyboardFocus => L("Settings.TrackingFocus"),
        TrackingSource.TextCaret => L("Settings.TrackingCaret"),
        TrackingSource.SelectedElement => L("Settings.TrackingSelection"),
        _ => L("Settings.TrackingMouse")
    };

    private TrackingSource ParseTrackingSource(string value)
    {
        if (string.Equals(value, L("Settings.TrackingFocus"), StringComparison.Ordinal))
        {
            return TrackingSource.KeyboardFocus;
        }

        if (string.Equals(value, L("Settings.TrackingCaret"), StringComparison.Ordinal))
        {
            return TrackingSource.TextCaret;
        }

        if (string.Equals(value, L("Settings.TrackingSelection"), StringComparison.Ordinal))
        {
            return TrackingSource.SelectedElement;
        }

        return TrackingSource.MouseCursor;
    }

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
        L("Common.HertzValue", 60),
        L("Common.HertzValue", 90),
        L("Common.HertzValue", 120),
        L("Common.HertzValue", 180),
        L("Common.HertzValue", 240),
        FpsLabel(UnlimitedFps)
    ];

    private string FpsLabel(int fps) => fps == UnlimitedFps ? L("Settings.FpsUnlimited") : L("Common.HertzValue", fps);

    private int ParseFpsLabel(string value)
    {
        if (string.Equals(value, L("Settings.FpsUnlimited"), StringComparison.OrdinalIgnoreCase))
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

    private static double GetContrastRatio(Color first, Color second)
    {
        static double Channel(byte value)
        {
            double normalized = value / 255d;
            return normalized <= 0.04045d
                ? normalized / 12.92d
                : Math.Pow((normalized + 0.055d) / 1.055d, 2.4d);
        }

        static double Luminance(Color color) =>
            (0.2126d * Channel(color.R)) +
            (0.7152d * Channel(color.G)) +
            (0.0722d * Channel(color.B));

        double lighter = Math.Max(Luminance(first), Luminance(second));
        double darker = Math.Min(Luminance(first), Luminance(second));
        return (lighter + 0.05d) / (darker + 0.05d);
    }

    private static readonly string[] CursorColorNameKeys =
    [
        "Accessibility.ColorName.White",
        "Accessibility.ColorName.Black",
        "Accessibility.ColorName.SoftWhite",
        "Accessibility.ColorName.Gray",
        "Accessibility.ColorName.Charcoal",
        "Accessibility.ColorName.Red",
        "Accessibility.ColorName.DarkRed",
        "Accessibility.ColorName.Orange",
        "Accessibility.ColorName.Amber",
        "Accessibility.ColorName.GoldenYellow",
        "Accessibility.ColorName.Yellow",
        "Accessibility.ColorName.Lime",
        "Accessibility.ColorName.Green",
        "Accessibility.ColorName.Emerald",
        "Accessibility.ColorName.Teal",
        "Accessibility.ColorName.Cyan",
        "Accessibility.ColorName.SkyBlue",
        "Accessibility.ColorName.Blue",
        "Accessibility.ColorName.DarkBlue",
        "Accessibility.ColorName.Indigo",
        "Accessibility.ColorName.Violet",
        "Accessibility.ColorName.Purple",
        "Accessibility.ColorName.Magenta",
        "Accessibility.ColorName.Pink",
        "Accessibility.ColorName.Rose",
        "Accessibility.ColorName.LightRed",
        "Accessibility.ColorName.Peach",
        "Accessibility.ColorName.LightYellow",
        "Accessibility.ColorName.Mint",
        "Accessibility.ColorName.LightCyan",
        "Accessibility.ColorName.LightBlue",
        "Accessibility.ColorName.Lavender",
        "Accessibility.ColorName.LightPink",
        "Accessibility.ColorName.PaleRed",
        "Accessibility.ColorName.LightGray",
        "Accessibility.ColorName.StoneGray"
    ];

    private string[] BuildCursorColorAccessibleNames()
    {
        return CursorColorNameKeys
            .Select((key, index) => L("Accessibility.ColorOption", index + 1, CursorColorNameKeys.Length, L(key)))
            .ToArray();
    }

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
        Control scaleOwner = _settingsWindow != null && !_settingsWindow.IsDisposed ? _settingsWindow : _uiInvoker;
        int gap = ControlDrawing.ScaleLogical(scaleOwner, 8);
        int buttonWidth = Math.Max(1, (width - (gap * Math.Max(0, buttons.Length - 1))) / Math.Max(1, buttons.Length));
        Size actionButtonSize = new(buttonWidth, ControlDrawing.ScaleLogical(scaleOwner, 32));
        var host = new TableLayoutPanel
        {
            AutoSize = false,
            ColumnCount = Math.Max(1, buttons.Length),
            RowCount = 1,
            Size = new Size(width, actionButtonSize.Height),
            MinimumSize = new Size(width, actionButtonSize.Height),
            MaximumSize = new Size(width, actionButtonSize.Height),
            BackColor = CurrentTheme.ControlBackground,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (int i = 0; i < Math.Max(1, buttons.Length); i++)
        {
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / Math.Max(1, buttons.Length)));
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            var buttonSpec = buttons[i];

            var button = new ModernButton
            {
                Text = buttonSpec.Text,
                Enabled = buttonSpec.Enabled,
                AutoSize = false,
                Dock = DockStyle.Fill,
                MinimumSize = Size.Empty,
                MaximumSize = Size.Empty,
                Margin = new Padding(i == 0 ? 0 : gap / 2, 0, i == buttons.Length - 1 ? 0 : gap - (gap / 2), 0),
                AccessibleName = buttonSpec.Text
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
        SettingsUiState? previousUiState = (_settingsWindow as SettingsForm)?.CaptureUiState();
        if (_settingsWindow is SettingsForm settingsForm)
        {
            settingsForm.ClosePermanently();
        }
        else
        {
            _settingsWindow.Close();
        }
        ShowSettingsWindow(page, previousCenter, previousUiState);
    }

    private void RebuildSettingsPage(SettingsPage page)
    {
        if (_settingsWindow == null || _settingsWindow.IsDisposed)
        {
            return;
        }

        if (_settingsWindow.InvokeRequired)
        {
            _settingsWindow.BeginInvoke((MethodInvoker)(() => RunGuarded("Settings.RebuildPage.Invoke", () => RebuildSettingsPage(page))));
            return;
        }

        if (_settingsWindow is SettingsForm settingsForm)
        {
            settingsForm.RebuildPage(GetSettingsPageType(page));
        }
    }

    private void HandleResetDefaultsRequested()
    {
        if (!_pendingResetDefaultsConfirmation)
        {
            _pendingResetDefaultsConfirmation = true;
            ApplyResetDefaultsButtonTheme();

            if (_resetDefaultsConfirmTimer == null)
            {
                _resetDefaultsConfirmTimer = new System.Windows.Forms.Timer { Interval = 10000 };
                _resetDefaultsConfirmTimer.Tick += OnResetDefaultsConfirmTimeout;
            }

            _resetDefaultsConfirmTimer.Stop();
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
