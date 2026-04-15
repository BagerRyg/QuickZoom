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

    private void ShowSettingsWindow(SettingsPage initialPage = SettingsPage.General)
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
        WindowChrome.TrySetDarkTitleBar(form, _useDarkTheme);
        _settingsWindow = form;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(24),
            Margin = new Padding(0),
            BackColor = palette.MenuBackground
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 18),
            Padding = new Padding(0),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent
        };
        var title = new Label
        {
            Text = L("Settings.Title"),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold),
            Margin = new Padding(0),
            ForeColor = palette.Text,
            BackColor = Color.Transparent
        };
        var subtitle = new Label
        {
            Text = L("Settings.Subtitle"),
            AutoSize = true,
            MaximumSize = new Size(860, 0),
            Font = new Font("Segoe UI", 10f, FontStyle.Regular),
            Margin = new Padding(0, 8, 0, 0),
            ForeColor = palette.SecondaryText,
            BackColor = Color.Transparent
        };
        header.Controls.Add(title, 0, 0);
        header.Controls.Add(subtitle, 0, 1);

        var tabBar = new ModernTabBar(palette)
        {
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 20)
        };
        tabBar.SetTabs(new[]
        {
            (SettingsPage.General.ToString(), L("Settings.General")),
            (SettingsPage.Display.ToString(), L("Settings.Display")),
            (SettingsPage.Appearance.ToString(), L("Settings.Appearance")),
            (SettingsPage.Zoom.ToString(), L("Settings.Zoom")),
            (SettingsPage.Input.ToString(), L("Settings.Input")),
            (SettingsPage.About.ToString(), L("Settings.About"))
        });

        var pageHost = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = palette.MenuBackground,
            AutoScroll = false
        };

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 16, 0, 0),
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };
        var closeButton = new ModernButton
        {
            Text = L("Settings.CloseEsc"),
            DialogResult = DialogResult.OK
        };
        closeButton.ApplyTheme(palette, emphasis: false);
        footer.Controls.Add(closeButton);

        var pages = new Dictionary<SettingsPage, SettingsPageView>
        {
            [SettingsPage.General] = BuildGeneralSettingsPage(),
            [SettingsPage.Display] = BuildDisplaySettingsPage(),
            [SettingsPage.Appearance] = BuildAppearanceSettingsPage(),
            [SettingsPage.Zoom] = BuildZoomSettingsPage(),
            [SettingsPage.Input] = BuildInputSettingsPage(),
            [SettingsPage.About] = BuildAboutSettingsPage()
        };

        foreach (SettingsPageView page in pages.Values)
        {
            page.Visible = false;
            page.Dock = DockStyle.Fill;
            pageHost.Controls.Add(page);
        }

        void ShowPage(SettingsPage page)
        {
            _currentSettingsPage = page;
            pageHost.SuspendLayout();
            foreach ((SettingsPage key, SettingsPageView view) in pages)
            {
                view.Visible = key == page;
            }

            pageHost.PerformLayout();
            pageHost.ResumeLayout(performLayout: true);
            tabBar.SelectTab(page.ToString(), notify: false);
        }

        tabBar.SelectionChanged += key =>
        {
            if (Enum.TryParse(key, out SettingsPage page))
            {
                ShowPage(page);
            }
        };

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(tabBar, 0, 1);
        root.Controls.Add(pageHost, 0, 2);
        root.Controls.Add(footer, 0, 3);
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
            _settingsWindow = null;
            _selectSettingsPageAction = null;
        };

        _selectSettingsPageAction = ShowPage;
        ShowPage(initialPage);
        form.Show();
        form.BringToFront();
        form.Activate();
    }

    private static Size GetSettingsClientSize()
    {
        Rectangle area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1400, 960);
        int width = Math.Min(1280, Math.Max(1100, area.Width - 120));
        int height = Math.Min(980, Math.Max(880, area.Height - 100));
        return new Size(width, height);
    }

    private SettingsPageView BuildGeneralSettingsPage()
    {
        ThemePalette palette = CurrentTheme;
        var page = new SettingsPageView(palette, L("Settings.GeneralTitle"), L("Settings.SectionHintGeneral"));
        var section = new SettingsSection(palette, L("Settings.GeneralSection"), L("Settings.GeneralSectionHint"));

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
        var page = new SettingsPageView(palette, L("Settings.DisplayTitle"), L("Settings.SectionHintDisplay"));
        var section = new SettingsSection(palette, L("Settings.DisplaySection"), L("Settings.DisplaySectionHint"));

        section.AddRow(CreateToggleRow(L("Settings.AutoSwitchMonitor"), L("Settings.AutoSwitchMonitorHelp"), _autoSwitchMonitor, value =>
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

        var openTrayButton = new ModernButton
        {
            Text = L("Settings.OpenTrayButton")
        };
        openTrayButton.ApplyTheme(palette, emphasis: false);
        openTrayButton.Click += (_, _) => ShowTrayPopup(Cursor.Position);
        section.AddRow(new SettingsRow(palette, L("Settings.DisplayTrayRow"), L("Settings.DisplayHint"), openTrayButton, rightColumnWidth: 200));

        page.AddSection(section);
        return page;
    }

    private SettingsPageView BuildZoomSettingsPage()
    {
        ThemePalette palette = CurrentTheme;
        var page = new SettingsPageView(palette, L("Settings.ZoomTitle"), L("Settings.SectionHintZoom"));
        var section = new SettingsSection(palette, L("Settings.ZoomSection"), L("Settings.ZoomSectionHint"));

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
        var page = new SettingsPageView(palette, L("Settings.AppearanceTitle"), L("Settings.SectionHintAppearance"));
        var themeSection = new SettingsSection(palette, L("Settings.AppearanceSection"), L("Settings.AppearanceSectionHint"));

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
            rightColumnWidth: 220));

        var languageSection = new SettingsSection(palette, L("Settings.LanguageSection"), L("Settings.LanguageSectionHint"));
        languageSection.AddRow(CreateDropdownRow(L("Settings.Language"), L("Settings.LanguageHelp"), BuildLanguageItems(), _language == UiLanguage.Danish ? L("Settings.Danish") : L("Settings.English"), value =>
        {
            _language = value == L("Settings.Danish") ? UiLanguage.Danish : UiLanguage.English;
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
        }, rightColumnWidth: 220));

        page.AddSection(themeSection);
        page.AddSection(languageSection);
        return page;
    }

    private SettingsPageView BuildInputSettingsPage()
    {
        ThemePalette palette = CurrentTheme;
        var page = new SettingsPageView(palette, L("Settings.InputTitle"), L("Settings.SectionHintInput"));
        var section = new SettingsSection(palette, L("Settings.InputSection"), L("Settings.InputSectionHint"));

        section.AddRow(CreateDropdownRow(
            L("Settings.EnableKey"),
            L("Settings.EnableKeyHelp"),
            BuildEnableKeyItems(),
            KeyLabel(_enableKey),
            value =>
            {
                _enableKey = value switch
                {
                    "Ctrl" => Keys.ControlKey,
                    "Alt" => Keys.Menu,
                    "Shift" => Keys.ShiftKey,
                    _ => _enableKey
                };
                _enableKeyPressed = false;
                SaveSettings();
                RefreshMenuAndTrayUi(rebuildPopup: true);
            },
            CreateInlineActionButton(L("Settings.Customize"), () =>
            {
                Keys? key = PromptForKey(_enableKey, L("Settings.EnableKeyDialogTitle"), L("Settings.EnableKeyDialogBody"));
                if (key != null)
                {
                    _enableKey = key.Value;
                    _enableKeyPressed = false;
                    SaveSettings();
                    RefreshMenuAndTrayUi(rebuildPopup: true);
                }
            }),
            rightColumnWidth: 360));

        section.AddRow(CreateDropdownRow(
            L("Settings.InvertHotkey"),
            L("Settings.InvertHotkeyHelp"),
            BuildInvertHotkeyItems(),
            InvertTriggerLabel(),
            value =>
            {
                switch (value)
                {
                    case var _ when value == InvertTriggerTextForCurrentEnableKey(L("Settings.Trigger.EnableMiddle")):
                        _invertTrigger = InvertTriggerKind.EnableKeyPlusMiddleClick;
                        break;
                    case var _ when value == InvertTriggerTextForCurrentEnableKey(L("Settings.Trigger.EnableX1")):
                        _invertTrigger = InvertTriggerKind.EnableKeyPlusXButton1;
                        break;
                    case var _ when value == InvertTriggerTextForCurrentEnableKey(L("Settings.Trigger.EnableX2")):
                        _invertTrigger = InvertTriggerKind.EnableKeyPlusXButton2;
                        break;
                    default:
                        _invertTrigger = InvertTriggerKind.CustomKey;
                        break;
                }

                _invertKeyPressed = false;
                SaveSettings();
                RefreshMenuAndTrayUi(rebuildPopup: true);
            },
            CreateInlineActionButton(L("Settings.Customize"), () =>
            {
                Keys? key = PromptForKey(_invertKey, L("Settings.InvertKeyDialogTitle"), L("Settings.InvertKeyDialogBody"));
                if (key != null)
                {
                    _invertKey = key.Value;
                    _invertTrigger = InvertTriggerKind.CustomKey;
                    _invertKeyPressed = false;
                    SaveSettings();
                    RefreshMenuAndTrayUi(rebuildPopup: true);
                }
            }),
            rightColumnWidth: 360));

        page.AddSection(section);
        return page;
    }

    private SettingsPageView BuildAboutSettingsPage()
    {
        ThemePalette palette = CurrentTheme;
        var page = new SettingsPageView(palette, L("Settings.AboutTitle"), string.Empty);

        string installPath = InstalledAppService.GetCurrentInstalledExecutablePath() ?? L("About.NotInstalled");
        string settingsPath = AppPaths.SettingsPath;

        var overviewSection = new SettingsSection(palette, L("Settings.AboutSection"), string.Empty);
        overviewSection.AddRow(CreateInfoRow(
            L("Settings.AboutBuildStartup"),
            AppInfo.DisplayVersion,
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

    private SettingsRow CreateNumericRow(string title, string description, int value, int min, int max, Action<int> onChanged, int rightColumnWidth = 180)
    {
        var numeric = new ModernNumberInput(CurrentTheme)
        {
            Minimum = min,
            Maximum = max,
            Value = value
        };
        numeric.ValueChanged += (_, _) => onChanged((int)numeric.Value);
        return new SettingsRow(CurrentTheme, title, description, numeric, rightColumnWidth);
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
            Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
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
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
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

    private string[] BuildEnableKeyItems() => ["Ctrl", "Alt", "Shift"];

    private string[] BuildInvertHotkeyItems() =>
    [
        InvertTriggerTextForCurrentEnableKey(L("Settings.Trigger.EnableMiddle")),
        InvertTriggerTextForCurrentEnableKey(L("Settings.Trigger.EnableX1")),
        InvertTriggerTextForCurrentEnableKey(L("Settings.Trigger.EnableX2")),
        L("Settings.Trigger.Custom")
    ];

    private string[] BuildLanguageItems() => [L("Settings.English"), L("Settings.Danish")];

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

        _settingsWindow.Close();
        ShowSettingsWindow(page);
    }
}
