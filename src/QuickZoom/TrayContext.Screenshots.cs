using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace QuickZoom;

internal sealed partial class TrayContext
{
    internal static void CaptureUiScreenshots(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        bool previousFollowWindowsTextScale = ControlDrawing.FollowWindowsTextScale;
        ControlDrawing.FollowWindowsTextScale = false;
        float previousUiFontScale = ControlDrawing.UiFontScale;

        try
        {
            using var context = new TrayContext(screenshotMode: true);
            context.ValidateSettingsSearchForCapture();
            foreach (bool useDarkTheme in new[] { true, false })
            {
                foreach (UiLanguage language in Enum.GetValues<UiLanguage>())
                {
                    foreach (UiFontSize fontSize in Enum.GetValues<UiFontSize>())
                    {
                        context.CaptureUiScreenshotSet(outputDirectory, language, useDarkTheme, fontSize);
                    }
                }
            }
        }
        finally
        {
            ControlDrawing.UiFontScale = previousUiFontScale;
            ControlDrawing.FollowWindowsTextScale = previousFollowWindowsTextScale;
        }
    }

    internal static void CaptureSettingsSmoke(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        bool previousFollowWindowsTextScale = ControlDrawing.FollowWindowsTextScale;
        float previousUiFontScale = ControlDrawing.UiFontScale;
        ControlDrawing.FollowWindowsTextScale = false;

        try
        {
            using var context = new TrayContext(screenshotMode: true);
            foreach (bool useDarkTheme in new[] { true, false })
            {
                _ = AppThemeBootstrap.TryApplyNativeColorMode(useDarkTheme ? AppThemeBootstrap.Dark : AppThemeBootstrap.Light);
                foreach (UiLanguage language in Enum.GetValues<UiLanguage>())
                {
                    context._language = language;
                    context._themeMode = useDarkTheme ? ThemeMode.Dark : ThemeMode.Light;
                    context._useDarkTheme = useDarkTheme;
                    context._uiFontSize = UiFontSize.Default;
                    context.ApplyUiFontScale();

                    string themeName = useDarkTheme ? "dark" : "light";
                    string languageCode = LocalizationManager.GetLanguageCode(language);
                    string variantDirectory = Path.Combine(outputDirectory, themeName, languageCode);
                    context.CaptureSettingsPages(variantDirectory);
                    context.CaptureTrayMenu(variantDirectory);
                }
            }
        }
        finally
        {
            ControlDrawing.UiFontScale = previousUiFontScale;
            ControlDrawing.FollowWindowsTextScale = previousFollowWindowsTextScale;
        }
    }

    private void CaptureUiScreenshotSet(
        string outputDirectory,
        UiLanguage language,
        bool useDarkTheme,
        UiFontSize fontSize)
    {
        _ = AppThemeBootstrap.TryApplyNativeColorMode(useDarkTheme ? AppThemeBootstrap.Dark : AppThemeBootstrap.Light);
        _language = language;
        _themeMode = useDarkTheme ? ThemeMode.Dark : ThemeMode.Light;
        _useDarkTheme = useDarkTheme;
        _uiFontSize = fontSize;
        ApplyUiFontScale();

        string languageCode = LocalizationManager.GetLanguageCode(language);
        string themeName = useDarkTheme ? "dark" : "light";
        string legacyVariantDirectory = Path.Combine(outputDirectory, themeName, languageCode);
        string variantDirectory = Path.Combine(legacyVariantDirectory, GetUiFontSizeDirectoryName(fontSize));
        Directory.CreateDirectory(variantDirectory);

        CaptureSettingsPages(variantDirectory);
        CaptureTrayMenu(variantDirectory);

        // Keep the original theme/language paths as the stable baseline used by
        // existing documentation and visual-review tooling.
        if (fontSize == UiFontSize.Default)
        {
            MirrorScreenshotSet(variantDirectory, legacyVariantDirectory);
        }
    }

    private void CaptureSettingsPages(string outputDirectory)
    {
        SettingsForm? form = null;
        bool previousDebugLoggingEnabled = _debugLoggingEnabled;
        try
        {
            _resetDefaultsButton = new ModernButton
            {
                Text = L("Settings.Reset"),
                MinimumSize = new Size(170, 38)
            };
            ApplyResetDefaultsButtonTheme();

            form = new SettingsForm(
                CurrentTheme,
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

            form.CaptureMode = true;
            form.ShowInTaskbar = false;
            _settingsWindow = form;
            WindowChrome.TrySetDarkTitleBar(form, _useDarkTheme);
            PlaceCaptureWindow(form);
            form.Show();
            form.PrepareForCapture();
            WaitForUi();
            ValidateRoundedSurfaceComposition(form);
            CaptureKeyboardNavigationAudit(form, outputDirectory);

            foreach (SettingsPage page in Enum.GetValues<SettingsPage>())
            {
                form.ShowPage(GetSettingsPageType(page));
                form.PrepareForCapture();
                WaitForUi(page is SettingsPage.Display or SettingsPage.About ? 900 : 250);
                form.ValidateNumericInputsForCapture();
                form.ValidateDropdownWidthsForCapture();
                CaptureWindow(form, Path.Combine(outputDirectory, "settings-" + GetSettingsPageFileName(page) + ".png"));
            }

            CaptureZoomModeTransitionAudit(form, outputDirectory);
            CaptureResponsiveResizeAudit(form, outputDirectory);

            form.ShowPage(typeof(GeneralSettingsPageView));
            form.PrepareInteractionCapture("zoom");
            WaitForUi(250);
            CaptureWindow(form, Path.Combine(outputDirectory, "settings-interactions.png"));

            form.PrepareInteractionCapture("frame");
            WaitForUi(250);
            CaptureWindow(form, Path.Combine(outputDirectory, "settings-search-alias-frame.png"));

            form.PrepareInteractionCapture(string.Empty);
            form.ActiveControl = null;
            foreach (bool debugLoggingEnabled in new[] { false, true })
            {
                _debugLoggingEnabled = debugLoggingEnabled;
                form.RebuildPage(typeof(AboutSettingsPageView));
                form.ShowPage(typeof(AboutSettingsPageView));
                WaitForUi(900);
                CaptureWindow(
                    form,
                    Path.Combine(outputDirectory, debugLoggingEnabled ? "settings-about-debug-on.png" : "settings-about-debug-off.png"));
            }

            ModernButton? hoveredAboutButton = FindControl<ModernButton>(form, L("About.OpenLog"));
            if (hoveredAboutButton != null)
            {
                hoveredAboutButton.SetInteractionStateForCapture(hovered: true);
                hoveredAboutButton.Parent?.Invalidate(true);
                WaitForUi(180);
                CaptureWindow(form, Path.Combine(outputDirectory, "settings-about-button-hover.png"));
                hoveredAboutButton.SetInteractionStateForCapture(hovered: false);
            }

            CaptureDropdownAudit(form, outputDirectory);
            CaptureControlStateAudit(form, outputDirectory);
            CaptureFocusAudit(form, outputDirectory);
        }
        finally
        {
            ControlDrawing.FocusCaptureTarget = null;
            _debugLoggingEnabled = previousDebugLoggingEnabled;
            if (form != null)
            {
                form.Hide();
                form.Dispose();
            }

            _settingsWindow = null;
            _resetDefaultsButton = null;
            _displaySelectionSettingsSection = null;
        }
    }

    private void ValidateSettingsSearchForCapture()
    {
        UiLanguage previousLanguage = _language;
        try
        {
            var catalogs = new Dictionary<UiLanguage, IReadOnlyList<SettingsSearchEntry>>();
            foreach (UiLanguage language in Enum.GetValues<UiLanguage>())
            {
                _language = language;
                catalogs[language] = BuildSettingsSearchEntries();
            }

            foreach ((UiLanguage currentLanguage, IReadOnlyList<SettingsSearchEntry> entries) in catalogs)
            {
                foreach (IReadOnlyList<SettingsSearchEntry> translatedEntries in catalogs.Values)
                {
                    if (translatedEntries.Count != entries.Count)
                    {
                        throw new InvalidOperationException("Settings search catalogs do not contain the same entries in every language.");
                    }

                    for (int index = 0; index < entries.Count; index++)
                    {
                        AssertSearchFinds(entries, translatedEntries[index].Title, entries[index], currentLanguage);
                        AssertSearchFinds(entries, translatedEntries[index].Description, entries[index], currentLanguage);
                    }
                }

                SettingsSearchEntry refreshRate = entries.Single(entry =>
                    entry.Title == UiText.Get(currentLanguage, "Settings.RefreshRate"));
                foreach (string query in new[]
                {
                    "frame", "frame-rate", "FPS", "update rate", "change the frame rate setting",
                    "billedhastighed", "opdateringsfrekvens"
                })
                {
                    AssertSearchFinds(entries, query, refreshRate, currentLanguage);
                }

            }
        }
        finally
        {
            _language = previousLanguage;
        }

        static void AssertSearchFinds(
            IReadOnlyList<SettingsSearchEntry> entries,
            string query,
            SettingsSearchEntry expected,
            UiLanguage currentLanguage)
        {
            IReadOnlyList<SettingsSearchEntry> matches = SettingsSearchControl.FindMatches(entries, query);
            if (!matches.Contains(expected))
            {
                throw new InvalidOperationException(
                    $"Settings search query '{query}' did not find '{expected.Title}' in {currentLanguage}.");
            }
        }
    }

    private void CaptureDropdownAudit(SettingsForm form, string outputDirectory)
    {
        ZoomMode previousZoomMode = _zoomMode;
        Point previousLocation = form.Location;
        try
        {
            CaptureDropdown(
                form,
                typeof(DisplaySettingsPageView),
                L("Settings.DisplaySelectionMode"),
                Path.Combine(outputDirectory, "dropdown-display-selection.png"),
                waitMilliseconds: 900);

            _zoomMode = ZoomMode.Fullscreen;
            form.RebuildPage(typeof(ZoomSettingsPageView));
            CaptureDropdown(
                form,
                typeof(ZoomSettingsPageView),
                L("Settings.RefreshRate"),
                Path.Combine(outputDirectory, "dropdown-refresh-rate-upward.png"),
                requireUpwardPlacement: true);

            _zoomMode = ZoomMode.Lens;
            form.RebuildPage(typeof(ZoomSettingsPageView));
            CaptureZoomModeControls(
                form,
                Path.Combine(outputDirectory, "settings-zoom-lens-controls.png"));
            CaptureDropdown(
                form,
                typeof(ZoomSettingsPageView),
                L("Settings.LensShape"),
                Path.Combine(outputDirectory, "dropdown-lens-shape.png"));

            _zoomMode = ZoomMode.Docked;
            form.RebuildPage(typeof(ZoomSettingsPageView));
            CaptureZoomModeControls(
                form,
                Path.Combine(outputDirectory, "settings-zoom-docked-controls.png"));
            CaptureDropdown(
                form,
                typeof(ZoomSettingsPageView),
                L("Settings.DockPosition"),
                Path.Combine(outputDirectory, "dropdown-dock-position.png"));
            CaptureModeAndSliderKeyboardAudit(form, outputDirectory);

            CaptureDropdown(
                form,
                typeof(AppearanceSettingsPageView),
                L("Settings.ThemeMode"),
                Path.Combine(outputDirectory, "dropdown-theme.png"));
            CaptureDropdown(
                form,
                typeof(AppearanceSettingsPageView),
                L("Settings.Language"),
                Path.Combine(outputDirectory, "dropdown-language.png"));
            CaptureDropdown(
                form,
                typeof(AppearanceSettingsPageView),
                L("Settings.FontSize"),
                Path.Combine(outputDirectory, "dropdown-font-size.png"));
            CaptureDropdown(
                form,
                typeof(ShortcutsSettingsPageView),
                L("Settings.ShortcutMode"),
                Path.Combine(outputDirectory, "dropdown-shortcut-mode.png"));
        }
        finally
        {
            _zoomMode = previousZoomMode;
            form.RebuildPage(typeof(ZoomSettingsPageView));
            form.Location = previousLocation;
        }
    }

    private void CaptureModeAndSliderKeyboardAudit(SettingsForm form, string outputDirectory)
    {
        form.ShowPage(typeof(ZoomSettingsPageView));
        form.PrepareForCapture();
        WaitForUi(250);

        Control lensTarget = form.PrepareModeLeftArrowCapture(
            L("Settings.ZoomModeDocked"),
            L("Settings.ZoomModeLens"));
        CaptureKeyboardTarget(
            form,
            lensTarget,
            Path.Combine(outputDirectory, "keyboard-mode-left-focus-lens.png"));

        Control fullscreenTarget = form.PrepareModeLeftArrowCapture(
            L("Settings.ZoomModeLens"),
            L("Settings.ZoomModeFullscreen"));
        CaptureKeyboardTarget(
            form,
            fullscreenTarget,
            Path.Combine(outputDirectory, "keyboard-mode-left-focus-fullscreen.png"));

        ModernSlider slider = form.PrepareSliderArrowCapture(out int originalValue);
        try
        {
            form.EnsureControlVisibleForCapture(slider);
            CaptureKeyboardTarget(
                form,
                slider,
                Path.Combine(outputDirectory, "keyboard-slider-arrows.png"));
        }
        finally
        {
            slider.SetExactValue(originalValue);
        }
    }

    private void CaptureZoomModeTransitionAudit(SettingsForm form, string outputDirectory)
    {
        ZoomMode previousZoomMode = _zoomMode;
        try
        {
            _zoomMode = ZoomMode.Fullscreen;
            form.RebuildPage(typeof(ZoomSettingsPageView));
            form.ShowPage(typeof(ZoomSettingsPageView));
            form.PrepareForCapture();
            WaitForUi(250);

            CaptureTransition(ZoomMode.Lens, "settings-zoom-transition-lens.png");
            CaptureTransition(ZoomMode.Docked, "settings-zoom-transition-docked.png");
            CaptureTransition(ZoomMode.Fullscreen, "settings-zoom-transition-fullscreen.png");
        }
        finally
        {
            _zoomMode = previousZoomMode;
            form.RebuildPage(typeof(ZoomSettingsPageView));
        }

        void CaptureTransition(ZoomMode mode, string fileName)
        {
            _zoomMode = mode;
            UpdateSettingsZoomModeUi();
            ValidateTransitionRows(mode);
            CaptureWindow(form, Path.Combine(outputDirectory, fileName));
        }

        void ValidateTransitionRows(ZoomMode mode)
        {
            AssertRowVisibility(L("Settings.LensSize"), mode == ZoomMode.Lens);
            AssertRowVisibility(L("Settings.LensShape"), mode == ZoomMode.Lens);
            AssertRowVisibility(L("Settings.DockPosition"), mode == ZoomMode.Docked);
            AssertRowVisibility(L("Settings.DockSize"), mode == ZoomMode.Docked);
        }

        void AssertRowVisibility(string accessibleName, bool expected)
        {
            SettingsRow? row = FindControl<SettingsRow>(form, accessibleName);
            if (row == null || row.Visible != expected)
            {
                throw new InvalidOperationException(
                    $"Zoom mode transition row '{accessibleName}' visibility was {row?.Visible.ToString() ?? "missing"}; expected {expected}.");
            }
        }
    }

    private void CaptureResponsiveResizeAudit(SettingsForm form, string outputDirectory)
    {
        Size previousSize = form.Size;
        try
        {
            form.ShowPage(typeof(GeneralSettingsPageView));
            form.PrepareForCapture();
            WaitForUi(180);

            Rectangle workingArea = Screen.FromControl(form).WorkingArea;
            Size minimumSize = form.MinimumSize;
            Size wideSize = new(
                Math.Min(workingArea.Width - ControlDrawing.ScaleLogical(form, 24), minimumSize.Width + ControlDrawing.ScaleLogical(form, 260)),
                Math.Min(workingArea.Height - ControlDrawing.ScaleLogical(form, 24), minimumSize.Height + ControlDrawing.ScaleLogical(form, 90)));

            ResizeAndCapture(minimumSize, "settings-resize-live-minimum.png");
            foreach (SettingsPage page in Enum.GetValues<SettingsPage>())
            {
                form.ShowPage(GetSettingsPageType(page));
                ValidateVisibleControlBounds();
                if (page is SettingsPage.Zoom or SettingsPage.About)
                {
                    CaptureWindow(
                        form,
                        Path.Combine(outputDirectory, $"settings-resize-live-minimum-{GetSettingsPageFileName(page)}.png"));
                }
            }

            form.ShowPage(typeof(GeneralSettingsPageView));
            ResizeAndCapture(wideSize, "settings-resize-live-wide.png");
        }
        finally
        {
            form.Size = previousSize;
            form.PrepareForCapture();
        }

        void ResizeAndCapture(Size size, string fileName)
        {
            form.Size = size;
            ValidateVisibleControlBounds();
            CaptureWindow(form, Path.Combine(outputDirectory, fileName));
        }

        void ValidateVisibleControlBounds()
        {
            int rightLimit = form.ClientSize.Width + ControlDrawing.ScaleLogical(form, 1);
            foreach (Control control in EnumerateDescendants(form))
            {
                if (!control.Visible || control is not (SettingsRow or ToggleSwitchControl or ModernDropdown or ModernSlider))
                {
                    continue;
                }

                Rectangle bounds = form.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
                if (bounds.Right > rightLimit)
                {
                    throw new InvalidOperationException(
                        $"Live resize left '{control.AccessibleName}' outside the client area: right={bounds.Right}, limit={rightLimit}.");
                }
            }
        }

        static IEnumerable<Control> EnumerateDescendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (Control descendant in EnumerateDescendants(child))
                {
                    yield return descendant;
                }
            }
        }
    }

    private static void CaptureZoomModeControls(SettingsForm form, string path)
    {
        form.ShowPage(typeof(ZoomSettingsPageView));
        form.PrepareForCapture();
        WaitForUi(250);
        form.ValidateNumericInputsForCapture();
        CaptureWindow(form, path);
    }

    private static void CaptureControlStateAudit(SettingsForm form, string outputDirectory)
    {
        foreach (SettingsPage page in Enum.GetValues<SettingsPage>())
        {
            if (page == SettingsPage.About)
            {
                continue;
            }

            Type pageType = GetSettingsPageType(page);
            form.RebuildPage(pageType);
            form.ShowPage(pageType);
            form.PrepareForCapture();
            WaitForUi(page is SettingsPage.Display ? 900 : 250);
            if (!form.PrepareControlStateCapture())
            {
                continue;
            }

            form.ValidateNumericInputsForCapture();
            WaitForUi(220);
            CaptureWindow(
                form,
                Path.Combine(outputDirectory, "settings-" + GetSettingsPageFileName(page) + "-control-states.png"));
        }
    }

    private void CaptureFocusAudit(SettingsForm form, string outputDirectory)
    {
        CaptureFocusState<SettingsSidebarItem>(
            form,
            typeof(GeneralSettingsPageView),
            L("Settings.General"),
            Path.Combine(outputDirectory, "focus-sidebar-keyboard.png"));
        CaptureFocusState<TextBox>(
            form,
            typeof(GeneralSettingsPageView),
            L("Settings.SearchPlaceholder"),
            Path.Combine(outputDirectory, "focus-search-field.png"));
        CaptureFocusState<ToggleSwitchControl>(
            form,
            typeof(GeneralSettingsPageView),
            L("Settings.SmoothZoom"),
            Path.Combine(outputDirectory, "focus-toggle.png"));
        CaptureFocusState<TrayModeButton>(
            form,
            typeof(ZoomSettingsPageView),
            L("Settings.ZoomModeFullscreen"),
            Path.Combine(outputDirectory, "focus-mode-option.png"));
        CaptureFocusState<ModernSlider>(
            form,
            typeof(ZoomSettingsPageView),
            L("Settings.ZoomStep"),
            Path.Combine(outputDirectory, "focus-slider.png"));
        CaptureFocusState<CompactNumericTextBox>(
            form,
            typeof(ZoomSettingsPageView),
            L("Settings.ZoomStep"),
            Path.Combine(outputDirectory, "focus-numeric-input.png"));
        CaptureFocusState<ModernDropdown>(
            form,
            typeof(ZoomSettingsPageView),
            L("Settings.RefreshRate"),
            Path.Combine(outputDirectory, "focus-dropdown.png"));
        CaptureFocusState<ColorPaletteControl>(
            form,
            typeof(CursorSettingsPageView),
            L("Settings.CursorFillColor"),
            Path.Combine(outputDirectory, "focus-colour-option.png"));
        CaptureFocusState<KeyBadgeControl>(
            form,
            typeof(ShortcutsSettingsPageView),
            L("Settings.EnableKey"),
            Path.Combine(outputDirectory, "focus-key-button.png"));
        CaptureFocusState<ModernButton>(
            form,
            typeof(AboutSettingsPageView),
            L("About.OpenConfigFolder"),
            Path.Combine(outputDirectory, "focus-button.png"));
    }

    private static void CaptureKeyboardNavigationAudit(SettingsForm form, string outputDirectory)
    {
        form.RebuildPage(typeof(ZoomSettingsPageView));
        form.ShowPage(typeof(ZoomSettingsPageView));
        form.PrepareForCapture();
        WaitForUi(250);

        Control sidebarTarget = form.PrepareFirstTabCapture();
        CaptureKeyboardTarget(
            form,
            sidebarTarget,
            Path.Combine(outputDirectory, "keyboard-first-tab.png"));

        Control contentTarget = form.PrepareSidebarContentEntryCapture();
        form.EnsureControlVisibleForCapture(contentTarget);
        CaptureKeyboardTarget(
            form,
            contentTarget,
            Path.Combine(outputDirectory, "keyboard-right-enters-page.png"));

        Control returnedTarget = form.PrepareSidebarReturnCapture();
        CaptureKeyboardTarget(
            form,
            returnedTarget,
            Path.Combine(outputDirectory, "keyboard-left-returns-sidebar.png"));
        form.ActiveControl = null;
    }

    private static void CaptureKeyboardTarget(SettingsForm form, Control target, string path)
    {
        ControlDrawing.FocusCaptureTarget = target;
        target.Invalidate();
        target.Parent?.Invalidate(true);
        form.Invalidate(true);
        WaitForUi(180);
        CaptureWindow(form, path);
        ControlDrawing.FocusCaptureTarget = null;
    }

    private static void CaptureFocusState<TControl>(
        SettingsForm form,
        Type pageType,
        string accessibleName,
        string path)
        where TControl : Control
    {
        ControlDrawing.FocusCaptureTarget = null;
        form.RebuildPage(pageType);
        form.ShowPage(pageType);
        form.PrepareForCapture();
        form.ActiveControl = null;
        WaitForUi(pageType == typeof(DisplaySettingsPageView) || pageType == typeof(AboutSettingsPageView) ? 900 : 250);

        TControl? target = FindControl<TControl>(form, accessibleName);
        if (target == null)
        {
            throw new InvalidOperationException(
                $"Could not find {typeof(TControl).Name} focus target '{accessibleName}'.");
        }

        form.EnsureControlVisibleForCapture(target);
        ControlDrawing.FocusCaptureTarget = target;
        if (target is CompactNumericTextBox && target.Parent != null)
        {
            target.Parent.BackColor = target.ForeColor;
        }

        target.Invalidate();
        target.Parent?.Invalidate(true);
        form.Invalidate(true);
        WaitForUi(180);
        CaptureWindow(form, path);
        ControlDrawing.FocusCaptureTarget = null;
    }

    private static TControl? FindControl<TControl>(Control root, string accessibleName)
        where TControl : Control
    {
        if (root is TControl match &&
            string.Equals(root.AccessibleName, accessibleName, StringComparison.CurrentCulture))
        {
            return match;
        }

        foreach (Control child in root.Controls)
        {
            TControl? descendant = FindControl<TControl>(child, accessibleName);
            if (descendant != null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static void ValidateRoundedSurfaceComposition(Control root)
    {
        if (root is ModernButton button)
        {
            if (button.Region != null)
            {
                throw new InvalidOperationException($"Button '{button.AccessibleName}' still uses a clipping region.");
            }

            if (button.Parent == null || button.Parent.BackColor.A != 255)
            {
                throw new InvalidOperationException($"Button '{button.AccessibleName}' does not have an opaque repaint host.");
            }
        }
        else if (root is ModernSurfacePanel or SettingsSidebarItem)
        {
            if (root.Region != null)
            {
                throw new InvalidOperationException($"Rounded surface '{root.AccessibleName}' still uses a clipping region.");
            }
        }

        foreach (Control child in root.Controls)
        {
            ValidateRoundedSurfaceComposition(child);
        }
    }

    private static void CaptureDropdown(
        SettingsForm form,
        Type pageType,
        string accessibleName,
        string path,
        bool requireUpwardPlacement = false,
        int waitMilliseconds = 250)
    {
        form.ShowPage(pageType);
        form.PrepareForCapture();
        WaitForUi(waitMilliseconds);
        ModernDropdown? dropdown = FindDropdown(form, accessibleName);
        if (dropdown == null)
        {
            throw new InvalidOperationException($"Could not find dropdown '{accessibleName}'.");
        }

        Size previousFormSize = form.Size;
        if (requireUpwardPlacement && form.Height > form.MinimumSize.Height)
        {
            form.Height = form.MinimumSize.Height;
        }

        form.EnsureControlVisibleForCapture(dropdown);
        WaitForUi(180);
        SimulatedCaptureLayout simulatedLayout = GetSimulatedCaptureLayout(form, dropdown, requireUpwardPlacement);

        try
        {
            using ModernDropdown.MenuCapture menuCapture = dropdown.RenderMenuForCapture(
                simulatedLayout.ControlBounds,
                simulatedLayout.WorkingArea);
            Rectangle menuBounds = menuCapture.ScreenBounds;
            Rectangle dropdownBounds = simulatedLayout.ControlBounds;
            Rectangle workingArea = simulatedLayout.WorkingArea;
            int tolerance = Math.Max(2, ControlDrawing.ScaleLogical(dropdown, 2));
            int anchoredEdge = menuCapture.OpenedAbove ? menuBounds.Bottom : menuBounds.Top;
            int expectedEdge = menuCapture.OpenedAbove ? dropdownBounds.Top : dropdownBounds.Bottom;
            bool horizontallyAnchored = Math.Abs(menuBounds.Left - dropdownBounds.Left) <= tolerance ||
                Math.Abs(menuBounds.Right - dropdownBounds.Right) <= tolerance;

            if (Math.Abs(anchoredEdge - expectedEdge) > tolerance || !horizontallyAnchored)
            {
                throw new InvalidOperationException($"Dropdown '{accessibleName}' is not anchored to its field.");
            }

            if (requireUpwardPlacement && !menuCapture.OpenedAbove)
            {
                throw new InvalidOperationException($"Dropdown '{accessibleName}' did not use its upward fallback.");
            }

            if (!workingArea.Contains(menuBounds))
            {
                throw new InvalidOperationException($"Dropdown '{accessibleName}' rendered outside the working area.");
            }

            if (dropdown.Height > ControlDrawing.ScaleLogical(dropdown, 36) ||
                menuCapture.ItemHeight > ControlDrawing.ScaleLogical(dropdown, 30))
            {
                throw new InvalidOperationException($"Dropdown '{accessibleName}' exceeded compact sizing limits.");
            }

            CaptureWindow(form, path, menuCapture, simulatedLayout.TargetBounds);
        }
        finally
        {
            if (form.Size != previousFormSize)
            {
                form.Size = previousFormSize;
            }
        }
    }

    private static ModernDropdown? FindDropdown(Control root, string accessibleName)
    {
        foreach (Control child in root.Controls)
        {
            if (child is ModernDropdown dropdown &&
                string.Equals(dropdown.AccessibleName, accessibleName, StringComparison.CurrentCulture))
            {
                return dropdown;
            }

            ModernDropdown? match = FindDropdown(child, accessibleName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private readonly record struct SimulatedCaptureLayout(
        Rectangle TargetBounds,
        Rectangle ControlBounds,
        Rectangle WorkingArea);

    private static SimulatedCaptureLayout GetSimulatedCaptureLayout(Form form, Control control, bool placeAtBottom)
    {
        Rectangle area = Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.VirtualScreen;
        Point simulatedFormLocation = new(
            area.Left + Math.Max(0, (area.Width - form.Width) / 2),
            placeAtBottom
                ? Math.Max(area.Top, area.Bottom - form.Height)
                : area.Top + Math.Max(0, (area.Height - form.Height) / 2));
        int offsetX = simulatedFormLocation.X - form.Left;
        int offsetY = simulatedFormLocation.Y - form.Top;
        Control captureTarget = form.Controls.Count > 0 ? form.Controls[0] : form;
        Rectangle targetBounds = captureTarget.RectangleToScreen(captureTarget.ClientRectangle);
        Rectangle controlBounds = control.RectangleToScreen(control.ClientRectangle);
        targetBounds.Offset(offsetX, offsetY);
        controlBounds.Offset(offsetX, offsetY);
        return new SimulatedCaptureLayout(targetBounds, controlBounds, area);
    }

    private void CaptureTrayMenu(string outputDirectory)
    {
        try
        {
            Rectangle area = Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.VirtualScreen;
            Point anchor = new(area.Right - 24, area.Bottom - 24);
            ShowTrayPopup(anchor, showWindow: false);
            if (_trayPopup == null)
            {
                return;
            }

            WaitForUi(250);
            CaptureWindow(_trayPopup, Path.Combine(outputDirectory, "tray-menu.png"));
            if (_magnifyRow != null)
            {
                CaptureTrayFocusTarget(
                    _trayPopup,
                    _magnifyRow,
                    Path.Combine(outputDirectory, "tray-menu-focus-toggle.png"));
            }

            if (_displayRow != null)
            {
                CaptureTrayFocusTarget(
                    _trayPopup,
                    _displayRow,
                    Path.Combine(outputDirectory, "tray-menu-focus-row.png"));
            }

            if (_fullscreenModeButton != null)
            {
                CaptureTrayFocusTarget(
                    _trayPopup,
                    _fullscreenModeButton,
                    Path.Combine(outputDirectory, "tray-menu-focus-mode.png"));
            }
        }
        finally
        {
            ControlDrawing.FocusCaptureTarget = null;
            CloseTrayPopup();
        }
    }

    private static void CaptureTrayFocusTarget(TrayPopupWindow popup, Control target, string path)
    {
        ControlDrawing.FocusCaptureTarget = target;
        target.Invalidate(true);
        popup.ContentHost.Invalidate(true);
        popup.Invalidate(true);
        WaitForUi(180);
        CaptureWindow(popup, path);
        ControlDrawing.FocusCaptureTarget = null;
    }

    private static string GetSettingsPageFileName(SettingsPage page) => page switch
    {
        SettingsPage.Display => "display",
        SettingsPage.Appearance => "appearance",
        SettingsPage.Cursor => "cursor",
        SettingsPage.Zoom => "zoom",
        SettingsPage.Input => "shortcuts",
        SettingsPage.About => "about",
        _ => "general"
    };

    private static string GetUiFontSizeDirectoryName(UiFontSize fontSize) => fontSize switch
    {
        UiFontSize.Large => "font-large",
        UiFontSize.ExtraLarge => "font-extra-large",
        _ => "font-default"
    };

    private static void MirrorScreenshotSet(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (string sourcePath in Directory.GetFiles(sourceDirectory, "*.png", SearchOption.TopDirectoryOnly))
        {
            string destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(sourcePath));
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }
    }

    private static void PlaceCaptureWindow(Form form)
    {
        Rectangle area = SystemInformation.VirtualScreen;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(area.Right + 64, area.Bottom + 64);
    }

    private static void CaptureWindow(
        Form form,
        string path,
        ModernDropdown.MenuCapture? overlay = null,
        Rectangle? targetBoundsOverride = null)
    {
        form.WindowState = FormWindowState.Normal;
        form.PerformLayout();
        WaitForUi(180);

        Control captureTarget = form.Controls.Count > 0 ? form.Controls[0] : form;
        Rectangle targetScreenBounds = targetBoundsOverride ?? captureTarget.RectangleToScreen(captureTarget.ClientRectangle);
        Rectangle captureBounds = overlay == null
            ? targetScreenBounds
            : Rectangle.Union(targetScreenBounds, overlay.ScreenBounds);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        Exception? lastFailure = null;

        try
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }

                    captureTarget.PerformLayout();
                    WaitForUi(80 + (attempt * 80));

                    using var bitmap = new Bitmap(
                        Math.Max(1, captureBounds.Width),
                        Math.Max(1, captureBounds.Height),
                        PixelFormat.Format32bppPArgb);
                    using (Graphics graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.Clear(form.BackColor);
                        using var windowBitmap = new Bitmap(
                            Math.Max(1, captureTarget.Width),
                            Math.Max(1, captureTarget.Height),
                            PixelFormat.Format32bppPArgb);
                        captureTarget.DrawToBitmap(windowBitmap, new Rectangle(Point.Empty, windowBitmap.Size));
                        ValidateRenderedContent(form, windowBitmap, path);
                        graphics.DrawImageUnscaled(
                            windowBitmap,
                            targetScreenBounds.X - captureBounds.X,
                            targetScreenBounds.Y - captureBounds.Y);

                        if (overlay != null)
                        {
                            graphics.DrawImageUnscaled(
                                overlay.Bitmap,
                                overlay.ScreenBounds.X - captureBounds.X,
                                overlay.ScreenBounds.Y - captureBounds.Y);
                        }
                    }

                    bitmap.Save(temporaryPath, ImageFormat.Png);
                    File.Move(temporaryPath, path, overwrite: true);
                    return;
                }
                catch (Exception ex)
                {
                    lastFailure = ex;
                    if (attempt >= 2)
                    {
                        break;
                    }
                }
            }

            throw new IOException("Could not capture the UI window after three attempts.", lastFailure);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void ValidateRenderedContent(Form form, Bitmap bitmap, string path)
    {
        Rectangle sampleBounds = form is SettingsForm
            ? new Rectangle(
                bitmap.Width / 4,
                bitmap.Height / 9,
                Math.Max(1, (bitmap.Width * 3 / 4) - 12),
                Math.Max(1, (bitmap.Height * 3 / 4) - 12))
            : Rectangle.Inflate(new Rectangle(Point.Empty, bitmap.Size), -12, -12);
        sampleBounds.Intersect(new Rectangle(Point.Empty, bitmap.Size));
        if (sampleBounds.Width <= 0 || sampleBounds.Height <= 0)
        {
            throw new InvalidOperationException($"Capture '{Path.GetFileName(path)}' has no drawable content area.");
        }

        Color baseline = bitmap.GetPixel(sampleBounds.Left, sampleBounds.Top);
        int variedSamples = 0;
        int totalSamples = 0;
        const int sampleStep = 4;
        for (int y = sampleBounds.Top; y < sampleBounds.Bottom; y += sampleStep)
        {
            for (int x = sampleBounds.Left; x < sampleBounds.Right; x += sampleStep)
            {
                Color pixel = bitmap.GetPixel(x, y);
                int difference = Math.Abs(pixel.R - baseline.R) +
                    Math.Abs(pixel.G - baseline.G) +
                    Math.Abs(pixel.B - baseline.B);
                if (difference >= 30)
                {
                    variedSamples++;
                }

                totalSamples++;
            }
        }

        if (variedSamples < Math.Max(80, totalSamples / 100))
        {
            throw new InvalidOperationException(
                $"Capture '{Path.GetFileName(path)}' is missing rendered UI content.");
        }
    }

    private static void WaitForUi(int delayMs = 150)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(delayMs);
        do
        {
            Application.DoEvents();
            Thread.Sleep(15);
        }
        while (DateTime.UtcNow < deadline);

        Application.DoEvents();
    }
}
