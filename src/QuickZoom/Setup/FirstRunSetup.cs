using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QuickZoom;

internal static class FirstRunSetup
{
    private const int CurrentSetupVersion = 2;
    private const int ThemeAuto = 0;
    private const int ThemeDark = 1;
    private const int ThemeLight = 2;
    private const int FontDefault = 0;
    private const int FontLarge = 1;
    private const int FontExtraLarge = 2;
    private const int FnVirtualKey = 0xFF;

    private static string StatePath => Path.Combine(
        Path.GetDirectoryName(AppPaths.SettingsPath)!,
        "first-run-setup.json");

    internal static bool ShouldRunAutomatically()
    {
        if (ReadCompletedVersion() >= CurrentSetupVersion)
        {
            return false;
        }

        // Existing installations predate this setup marker and must not receive
        // an unexpected first-run prompt after updating.
        if (File.Exists(AppPaths.SettingsPath) || File.Exists(AppPaths.LegacySettingsPath))
        {
            TryWriteCompletionState();
            return false;
        }

        return true;
    }

    internal static void Show(bool allowLivePractice)
    {
        FirstRunSetupSelection initial = ReadInitialSelection();
        FirstRunSetupSelection? selection;
        try
        {
            using var form = new FirstRunSetupForm(initial, allowLivePractice: allowLivePractice);
            selection = form.ShowStagedDialog() ? form.Selection : null;
        }
        catch (Exception ex)
        {
            ErrorLog.Write("FirstRunSetup.Show", ex);
            StartupDialogs.ShowWarning(
                UiText.Get(initial.Language, "Common.AppName"),
                UiText.Get(initial.Language, "Setup.SaveFailedTitle"),
                UiText.Get(initial.Language, "Setup.SaveFailedBody"));
            return;
        }

        if (selection is null)
        {
            return;
        }

        try
        {
            SaveSelection(selection);
            WriteCompletionState(selection.StartupServiceSkipped, selection.ViewMode);
        }
        catch (Exception ex)
        {
            ErrorLog.Write("FirstRunSetup.Save", ex);
            StartupDialogs.ShowWarning(
                UiText.Get(selection.Language, "Common.AppName"),
                UiText.Get(selection.Language, "Setup.SaveFailedTitle"),
                UiText.Get(selection.Language, "Setup.SaveFailedBody"));
        }
    }

    internal static bool ShowStartupServiceOnly(IWin32Window? owner = null)
    {
        FirstRunSetupSelection initial = ReadInitialSelection();
        try
        {
            using var form = new FirstRunSetupForm(initial, startupOnly: true);
            if (!form.ShowStagedDialog(owner))
            {
                return false;
            }

            bool ready = StartupTaskService.IsReadyForCurrentBuild(out _);
            if (ready)
            {
                try
                {
                    WriteCompletionState(startupServiceSkipped: false, form.ViewMode);
                }
                catch (Exception ex)
                {
                    ErrorLog.Write("FirstRunSetup.StartupState", ex);
                }
            }

            return ready;
        }
        catch (Exception ex)
        {
            ErrorLog.Write("FirstRunSetup.StartupOnly", ex);
            return false;
        }
    }

    internal static bool StartupServiceWasSkipped()
    {
        try
        {
            if (!File.Exists(StatePath))
            {
                return false;
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(StatePath));
            return document.RootElement.TryGetProperty("StartupServiceSkipped", out JsonElement value) &&
                value.ValueKind == JsonValueKind.True;
        }
        catch (Exception ex)
        {
            ErrorLog.WriteThrottled("FirstRunSetup.ReadStartupState", ex);
            return false;
        }
    }

    internal static void CaptureSmoke(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        bool previousFollowWindowsTextScale = ControlDrawing.FollowWindowsTextScale;
        ControlDrawing.FollowWindowsTextScale = false;
        try
        {
            using (var interactionForm = new FirstRunSetupForm(
                       new FirstRunSetupSelection(
                           UiLanguage.English,
                           ThemeAuto,
                           FontDefault,
                           Keys.Menu,
                           false,
                           SetupViewMode.Standard)))
            {
                interactionForm.ValidateViewToggleInteraction();
            }

            foreach (UiLanguage language in Enum.GetValues<UiLanguage>())
            {
                foreach (int themeMode in new[] { ThemeDark, ThemeLight })
                {
                    string languageName = LocalizationManager.GetLanguageCode(language);
                    string themeName = themeMode == ThemeDark ? "dark" : "light";
                    string variantDirectory = Path.Combine(outputDirectory, themeName, languageName);
                    Directory.CreateDirectory(variantDirectory);
                    Capture("language.png", 0);
                    Capture("appearance.png", 1);
                    Capture("shortcuts.png", 2);
                    Capture(
                        "shortcuts-capturing.png",
                        2,
                        capturingHotkey: true);
                    Capture(
                        "shortcuts-warnings.png",
                        2,
                        enableKey: Keys.LWin);
                    Capture(
                        "usage.png",
                        3,
                        enableKey: Keys.Menu);
                    Capture(
                        "startup.png",
                        4,
                        startupState: SetupStartupState.NotConfigured);
                    Capture(
                        "startup-ready.png",
                        4,
                        startupState: SetupStartupState.Ready);
                    Capture(
                        "startup-configuring.png",
                        4,
                        startupState: SetupStartupState.Installing);
                    Capture(
                        "startup-completing.png",
                        4,
                        startupState: SetupStartupState.Verifying,
                        startupProgressComplete: true);
                    Capture(
                        "startup-declined.png",
                        4,
                        startupState: SetupStartupState.Declined);
                    Capture("complete.png", 5);
                    Capture(
                        "appearance-continue-hover.png",
                        1,
                        hoverContinue: true);
                    Capture(
                        "accessible/language.png",
                        0,
                        viewMode: SetupViewMode.Accessible,
                        captureSize: new Size(1600, 900),
                        windowsTextScale: 1f);
                    Capture(
                        "accessible/appearance.png",
                        1,
                        viewMode: SetupViewMode.Accessible,
                        captureSize: new Size(1600, 900),
                        windowsTextScale: 1f);
                    Capture(
                        "accessible/shortcuts.png",
                        2,
                        viewMode: SetupViewMode.Accessible,
                        captureSize: new Size(1600, 900),
                        windowsTextScale: 1f);
                    Capture(
                        "accessible/usage.png",
                        3,
                        enableKey: Keys.Menu,
                        viewMode: SetupViewMode.Accessible,
                        captureSize: new Size(1600, 900),
                        windowsTextScale: 1f);
                    Capture(
                        "accessible/startup.png",
                        4,
                        startupState: SetupStartupState.NotConfigured,
                        viewMode: SetupViewMode.Accessible,
                        captureSize: new Size(1600, 900),
                        windowsTextScale: 1f);
                    Capture(
                        "accessible/complete.png",
                        5,
                        viewMode: SetupViewMode.Accessible,
                        captureSize: new Size(1600, 900),
                        windowsTextScale: 1f);
                    string extraLargeDirectory = Path.Combine(variantDirectory, "extra-large");
                    Directory.CreateDirectory(extraLargeDirectory);
                    Capture("extra-large/language.png", 0, fontSize: FontExtraLarge);
                    Capture("extra-large/appearance.png", 1, fontSize: FontExtraLarge);
                    Capture("extra-large/shortcuts.png", 2, fontSize: FontExtraLarge);
                    Capture(
                        "extra-large/usage.png",
                        3,
                        enableKey: Keys.Menu,
                        fontSize: FontExtraLarge);
                    Capture(
                        "extra-large/startup.png",
                        4,
                        startupState: SetupStartupState.NotConfigured,
                        fontSize: FontExtraLarge);
                    Capture("extra-large/complete.png", 5, fontSize: FontExtraLarge);

                    if (language is UiLanguage.English or UiLanguage.Finnish)
                    {
                        string[] stepNames =
                        [
                            "language",
                            "appearance",
                            "shortcuts",
                            "usage",
                            "startup",
                            "complete"
                        ];
                        foreach (Size responsiveSize in new[]
                                 {
                                     new Size(1366, 768),
                                     new Size(1920, 1080),
                                     new Size(2560, 1440)
                                 })
                        {
                            foreach (float textScale in new[] { 1f, 1.5f, 2.25f })
                            {
                                for (int responsiveStep = 0; responsiveStep < stepNames.Length; responsiveStep++)
                                {
                                    string responsivePath = Path.Combine(
                                        "responsive",
                                        "accessible",
                                        $"{responsiveSize.Width}x{responsiveSize.Height}",
                                        $"text-{(int)Math.Round(textScale * 100f)}",
                                        stepNames[responsiveStep] + ".png");
                                    Capture(
                                        responsivePath,
                                        responsiveStep,
                                        enableKey: responsiveStep == 3 ? Keys.Menu : null,
                                        startupState: responsiveStep == 4
                                            ? SetupStartupState.NotConfigured
                                            : null,
                                        viewMode: SetupViewMode.Accessible,
                                        captureSize: responsiveSize,
                                        windowsTextScale: textScale);
                                }
                            }
                        }
                    }

                    void Capture(
                        string fileName,
                        int step,
                        bool hoverContinue = false,
                        Keys? enableKey = null,
                        SetupStartupState? startupState = null,
                        bool startupProgressComplete = false,
                        bool capturingHotkey = false,
                        int fontSize = FontDefault,
                        SetupViewMode viewMode = SetupViewMode.Standard,
                        Size? captureSize = null,
                        float? windowsTextScale = null,
                        bool scrollToBottom = false)
                    {
                        string capturePath = Path.Combine(variantDirectory, fileName);
                        Directory.CreateDirectory(Path.GetDirectoryName(capturePath)!);
                        using (var form = new FirstRunSetupForm(
                                   new FirstRunSetupSelection(
                                       language,
                                       ThemeAuto,
                                       fontSize,
                                       Keys.Menu,
                                       false,
                                       viewMode),
                                   captureWindowsTextScale: windowsTextScale))
                        {
                            form.CaptureStep(
                                capturePath,
                                step,
                                themeMode,
                                hoverContinue,
                                enableKey,
                                startupState,
                                startupProgressComplete,
                                capturingHotkey,
                                viewMode,
                                captureSize,
                                windowsTextScale,
                                scrollToBottom);
                        }
                    }
                }
            }
        }
        finally
        {
            ControlDrawing.FollowWindowsTextScale = previousFollowWindowsTextScale;
        }
    }

    private static FirstRunSetupSelection ReadInitialSelection()
    {
        UiLanguage language = UiText.GetStartupLanguage();
        int fontSize = FontDefault;
        Keys enableKey = Keys.Menu;

        foreach (string path in new[] { AppPaths.SettingsPath, AppPaths.LegacySettingsPath })
        {
            try
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                JsonElement root = document.RootElement;
                if (root.TryGetProperty("Language", out JsonElement languageValue) &&
                    languageValue.TryGetInt32(out int languageNumber) &&
                    Enum.IsDefined(typeof(UiLanguage), languageNumber))
                {
                    language = (UiLanguage)languageNumber;
                }

                if (root.TryGetProperty("UiFontSize", out JsonElement fontValue) &&
                    fontValue.TryGetInt32(out int fontNumber) &&
                    fontNumber is >= FontDefault and <= FontExtraLarge)
                {
                    fontSize = fontNumber;
                }

                if (root.TryGetProperty("EnableKey", out JsonElement enableKeyValue) &&
                    enableKeyValue.TryGetInt32(out int enableKeyNumber))
                {
                    enableKey = (Keys)enableKeyNumber;
                }

                break;
            }
            catch (Exception ex)
            {
                ErrorLog.WriteThrottled("FirstRunSetup.ReadSettings", ex);
            }
        }

        // Every setup session begins by following Windows until the user makes
        // an explicit appearance choice on step two.
        return new FirstRunSetupSelection(
            language,
            ThemeAuto,
            fontSize,
            enableKey,
            StartupServiceWasSkipped(),
            ReadPreferredSetupView());
    }

    private static void SaveSelection(FirstRunSetupSelection selection)
    {
        string path = AppPaths.SettingsPath;
        JsonObject settings = ReadSettingsObject() ?? new JsonObject();
        settings["Language"] = (int)selection.Language;
        settings["ThemeMode"] = selection.ThemeMode;
        settings["UiFontSize"] = selection.FontSize;
        settings["EnableKey"] = (int)selection.EnableKey;

        WriteJsonAtomically(path, settings.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    private static JsonObject? ReadSettingsObject()
    {
        foreach (string path in new[] { AppPaths.SettingsPath, AppPaths.LegacySettingsPath })
        {
            try
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                JsonObject? settings = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
                if (settings == null)
                    continue;
                var filtered = new JsonObject();
                foreach ((string key, JsonNode? value) in settings)
                {
                    if (value is JsonValue scalar &&
                        (scalar.TryGetValue<int>(out _) || scalar.TryGetValue<bool>(out _)) &&
                        key != "DebugLoggingEnabled" && TrayContext.IsKnownSetting(key))
                        filtered[key] = value.DeepClone();
                    else if (key == "SelectedMonitorDeviceNames" && value is JsonArray names)
                    {
                        var validNames = new JsonArray();
                        foreach (JsonNode? name in names)
                            if (name is JsonValue text && text.TryGetValue<string>(out string? device) &&
                                Screen.AllScreens.Any(screen => screen.DeviceName == device))
                                validNames.Add(device);
                        filtered[key] = validNames;
                    }
                }
                return filtered;
            }
            catch (Exception ex)
            {
                ErrorLog.WriteThrottled("FirstRunSetup.ReadObject", ex);
            }
        }

        return null;
    }

    private static int ReadCompletedVersion()
    {
        try
        {
            if (!File.Exists(StatePath))
            {
                return 0;
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(StatePath));
            return document.RootElement.TryGetProperty("Version", out JsonElement value) &&
                value.TryGetInt32(out int version)
                ? version
                : 0;
        }
        catch (Exception ex)
        {
            ErrorLog.WriteThrottled("FirstRunSetup.ReadState", ex);
            return 0;
        }
    }

    private static void TryWriteCompletionState()
    {
        try
        {
            WriteCompletionState(
                StartupServiceWasSkipped(),
                SetupViewMode.Standard);
        }
        catch (Exception ex)
        {
            ErrorLog.WriteThrottled("FirstRunSetup.MigrateState", ex);
        }
    }

    private static SetupViewMode ReadPreferredSetupView()
    {
        try
        {
            if (!File.Exists(StatePath))
            {
                return SetupViewMode.Accessible;
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(StatePath));
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("PreferredSetupView", out JsonElement value) &&
                value.ValueKind == JsonValueKind.String &&
                Enum.TryParse(value.GetString(), ignoreCase: true, out SetupViewMode viewMode))
            {
                return viewMode;
            }

            // Completed setup state from an older build keeps the original
            // standard-size behavior until the user chooses a view.
            return root.TryGetProperty("Version", out JsonElement versionValue) &&
                versionValue.TryGetInt32(out int version) &&
                version >= CurrentSetupVersion
                ? SetupViewMode.Standard
                : SetupViewMode.Accessible;
        }
        catch (Exception ex)
        {
            ErrorLog.WriteThrottled("FirstRunSetup.ReadPreferredView", ex);
            return SetupViewMode.Accessible;
        }
    }

    private static void WriteCompletionState(
        bool startupServiceSkipped,
        SetupViewMode viewMode)
    {
        var state = new JsonObject
        {
            ["Version"] = CurrentSetupVersion,
            ["StartupServiceSkipped"] = startupServiceSkipped,
            ["PreferredSetupView"] = viewMode.ToString()
        };
        WriteJsonAtomically(StatePath, state.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    private static void WriteJsonAtomically(string path, string json)
    {
        FilePersistence.WriteAllTextAtomic(path, json);
    }

    private sealed record FirstRunSetupSelection(
        UiLanguage Language,
        int ThemeMode,
        int FontSize,
        Keys EnableKey,
        bool StartupServiceSkipped,
        SetupViewMode ViewMode);

    internal enum SetupViewMode
    {
        Accessible,
        Standard
    }

    private enum SetupValidationLevel
    {
        None,
        Warning,
        Error
    }

    private readonly record struct SetupValidation(
        SetupValidationLevel Level,
        string Text);

    private enum SetupIcon
    {
        None,
        Sun,
        Moon,
        System,
        EnglishLanguage,
        DanishFlag,
        SwedishFlag,
        NorwegianFlag,
        FinnishFlag
    }

    private enum SetupUsageKind
    {
        Zoom,
        Invert,
        Mode
    }

    private enum SetupStartupState
    {
        Checking,
        NotConfigured,
        AwaitingApproval,
        Installing,
        Verifying,
        Ready,
        Declined,
        Failed
    }

    private sealed class FirstRunSetupForm : Form
    {
        private const int WmNcHitTest = 0x0084;
        private const int WmNcLeftButtonDown = 0x00A1;
        private const int HtClient = 1;
        private const int HtCaption = 2;
        private const int BaseAccessibleContentWidth = 1200;
        private const int AccessibleReferenceWidth = 1600;
        private const int AccessibleReferenceHeight = 900;
        private const float AccessibleFontScale = 1.45f;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        private readonly TableLayoutPanel _root;
        private readonly Panel _header;
        private readonly Label _welcomeLabel;
        private readonly SetupViewToggle _viewSelector;
        private readonly Panel _headerDivider;
        private readonly Panel _contentHost;
        private readonly TableLayoutPanel _footer;
        private readonly SetupStepIndicator _stepIndicator;
        private readonly ModernButton _backButton;
        private readonly ModernButton _skipButton;
        private readonly ModernButton _continueButton;
        private readonly SetupWaveTransition _transitionOverlay;
        private readonly List<SetupChoiceCard> _cards = new();
        private readonly Dictionary<(float Size, FontStyle Style), Font> _ownedFonts = new();
        private Font? _fittedHeaderFont;
        private int _step;
        private UiLanguage _language;
        private int _themeMode;
        private int _fontSize;
        private Keys _enableKey;
        private SetupViewMode _viewMode;
        private bool _capturingHotkey;
        private bool _pendingControlKey;
        private bool _useDarkTheme;
        private ThemePalette _palette;
        private Label? _headingLabel;
        private Label? _descriptionLabel;
        private Label? _themeSectionLabel;
        private SetupHotkeyRow? _enableHotkeyRow;
        private readonly List<SetupUsageTile> _usageTiles = new();
        private SetupStartupServiceTile? _startupServiceTile;
        private SetupCompletionTile? _completionTile;
        private TrayContext? _practiceContext;
        private SetupStartupState _startupState = SetupStartupState.Checking;
        private readonly bool _startupOnly;
        private readonly bool _allowLivePractice;
        private bool _startupStatusChecked;
        private bool _captureMode;
        private bool _startupServiceSkipped;
        private bool _accepted;
        private Rectangle _targetWorkingArea;
        private Rectangle _standardBounds;
        private bool _updatingViewLayout;
        private float? _captureWindowsTextScale;

        internal FirstRunSetupForm(
            FirstRunSetupSelection initial,
            bool startupOnly = false,
            bool allowLivePractice = false,
            float? captureWindowsTextScale = null)
        {
            _language = initial.Language;
            _themeMode = ThemeAuto;
            _fontSize = initial.FontSize;
            _enableKey = initial.EnableKey;
            _viewMode = initial.ViewMode;
            _captureWindowsTextScale = captureWindowsTextScale;
            _startupServiceSkipped = initial.StartupServiceSkipped;
            _startupOnly = startupOnly;
            _allowLivePractice = allowLivePractice;
            _useDarkTheme = AppThemeBootstrap.ShouldUseDarkPalette(ThemeAuto);
            _palette = _useDarkTheme ? ThemePalettes.Dark : ThemePalettes.Light;

            AutoScaleMode = AutoScaleMode.Dpi;
            Rectangle workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
            _targetWorkingArea = workingArea;
            _standardBounds = GetStandardBounds(workingArea);
            Bounds = _viewMode == SetupViewMode.Accessible
                ? workingArea
                : _standardBounds;
            MinimumSize = GetMinimumWindowSize();
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            KeyPreview = true;
            DoubleBuffered = true;
            Padding = new Padding(1);
            AccessibleRole = AccessibleRole.Dialog;
            try
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                // The setup remains usable if Windows cannot read the executable icon.
            }

            _root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = GetRootPadding(),
                Margin = Padding.Empty
            };
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, LayoutHeight(70)));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, LayoutHeight(74)));

            _header = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty
            };
            _welcomeLabel = new Label
            {
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty,
                AutoEllipsis = false,
                UseCompatibleTextRendering = false,
                AccessibleRole = AccessibleRole.StaticText
            };
            _viewSelector = new SetupViewToggle
            {
                ViewMode = _viewMode,
                TabIndex = 0,
                Margin = Padding.Empty
            };
            _viewSelector.ViewModeRequested += SwitchViewMode;
            _header.Controls.Add(_welcomeLabel);
            _header.Controls.Add(_viewSelector);
            _header.Resize += (_, _) => UpdateHeaderLayout();
            _welcomeLabel.MouseDown += BeginWindowDrag;
            _header.MouseDown += BeginWindowDrag;
            _headerDivider = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty
            };
            _headerDivider.MouseDown += BeginWindowDrag;
            _root.MouseDown += BeginWindowDrag;
            _contentHost = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                AutoScroll = false
            };
            _footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 7,
                RowCount = 1,
                Padding = new Padding(0, 14, 0, 0),
                Margin = Padding.Empty
            };
            _footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
            _footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
            _footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            _footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
            _footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
            _footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _stepIndicator = new SetupStepIndicator
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                TabStop = false
            };
            _continueButton = new ModernButton
            {
                AutoSize = false,
                Size = new Size(210, LayoutHeight(42)),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Margin = Padding.Empty,
                WrapText = true,
                TabIndex = 3
            };
            _continueButton.Click += (_, _) => Continue();
            _skipButton = new ModernButton
            {
                AutoSize = false,
                Size = new Size(128, LayoutHeight(42)),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Margin = Padding.Empty,
                Visible = false,
                WrapText = true,
                TabIndex = 2
            };
            _skipButton.Click += (_, _) => SkipStartupService();
            _backButton = new ModernButton
            {
                AutoSize = false,
                Size = new Size(210, LayoutHeight(42)),
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
                Margin = Padding.Empty,
                WrapText = true,
                TabIndex = 1
            };
            _backButton.Click += (_, _) => ShowStep(_step - 1);

            _viewSelector.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _footer.Controls.Add(_backButton, 0, 0);
            _footer.Controls.Add(_stepIndicator, 3, 0);
            _footer.Controls.Add(_skipButton, 5, 0);
            _footer.Controls.Add(_continueButton, 6, 0);
            _root.Controls.Add(_header, 0, 0);
            _root.Controls.Add(_headerDivider, 0, 1);
            _root.Controls.Add(_contentHost, 0, 2);
            _root.Controls.Add(_footer, 0, 3);
            Controls.Add(_root);
            _transitionOverlay = new SetupWaveTransition
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Visible = false
            };
            Controls.Add(_transitionOverlay);
            _transitionOverlay.BringToFront();

            AcceptButton = _continueButton;
            KeyDown += HandleSetupKeyDown;
            KeyUp += HandleSetupKeyUp;
            FormClosed += (_, _) =>
            {
                StopLivePractice();
                if (!_accepted)
                {
                    Selection = null;
                }
            };
            FormClosing += (_, e) =>
            {
                if (StartupActionBlocksNavigation)
                {
                    e.Cancel = true;
                }
            };
            ClientSizeChanged += (_, _) => UpdateResponsiveLayout(rebuildContent: false);

            UpdateResponsiveLayout(rebuildContent: false);
            ShowStep(startupOnly ? 4 : 0, animate: false);
        }

        internal FirstRunSetupSelection? Selection { get; private set; }
        internal SetupViewMode ViewMode => _viewMode;

        protected override bool ShowWithoutActivation => _captureMode;

        private void BeginWindowDrag(object? sender, MouseEventArgs e)
        {
            if (_viewMode == SetupViewMode.Accessible ||
                e.Button != MouseButtons.Left ||
                sender == _root && e.Y > ControlDrawing.ScaleLogical(this, 100))
            {
                return;
            }

            _ = ReleaseCapture();
            _ = SendMessage(Handle, WmNcLeftButtonDown, new IntPtr(HtCaption), IntPtr.Zero);
        }

        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            if (_viewMode == SetupViewMode.Accessible ||
                message.Msg != WmNcHitTest ||
                message.Result != new IntPtr(HtClient))
            {
                return;
            }

            long position = message.LParam.ToInt64();
            Point clientPoint = PointToClient(new Point(
                unchecked((short)(position & 0xffff)),
                unchecked((short)((position >> 16) & 0xffff))));
            if (clientPoint.Y <= ControlDrawing.ScaleLogical(this, 84))
            {
                message.Result = new IntPtr(HtCaption);
            }
        }

        internal bool ShowStagedDialog(IWin32Window? owner = null)
        {
            Opacity = 0;
            _targetWorkingArea = owner != null && owner.Handle != IntPtr.Zero
                ? Screen.FromHandle(owner.Handle).WorkingArea
                : Screen.FromPoint(Cursor.Position).WorkingArea;
            _standardBounds = GetStandardBounds(_targetWorkingArea);
            Bounds = _viewMode == SetupViewMode.Accessible
                ? _targetWorkingArea
                : _standardBounds;
            UpdateResponsiveLayout(rebuildContent: true);
            Rectangle finalBounds = Bounds;
            Bounds = new Rectangle(
                SystemInformation.VirtualScreen.Left - Width - 200,
                SystemInformation.VirtualScreen.Top - Height - 200,
                finalBounds.Width,
                finalBounds.Height);

            _ = Handle;
            WindowChrome.TrySetDarkTitleBar(this, _useDarkTheme);
            bool cloaked = WindowChrome.TrySetCloaked(this, cloaked: true);
            Shown += (_, _) => BeginInvoke((MethodInvoker)(() =>
            {
                PrepareForReveal();
                Opacity = 1;
                PrepareForReveal();
                WindowChrome.TryFlushComposition();
                Bounds = finalBounds;
                PrepareForReveal();
                if (cloaked)
                {
                    _ = WindowChrome.TrySetCloaked(this, cloaked: false);
                }

                Activate();
                FocusCurrentStepEntry();
                WindowChrome.TryFlushComposition();
                if (_step == 4)
                {
                    BeginStartupStatusCheck();
                }
            }));

            if (owner == null)
            {
                _ = ShowDialog();
            }
            else
            {
                _ = ShowDialog(owner);
            }
            return _accepted;
        }

        private Rectangle GetStandardBounds(Rectangle workingArea)
        {
            float largeTextProgress = Math.Clamp(
                (PreferenceFontScale - 1f) / 0.28f,
                0f,
                1f);
            int maximumWidth = Math.Max(720, workingArea.Width - 32);
            int maximumHeight = Math.Max(560, workingArea.Height - 32);
            int width = Math.Min(
                1040 + (int)Math.Round(160f * largeTextProgress),
                maximumWidth);
            int height = Math.Min(
                720 + (int)Math.Round(240f * largeTextProgress),
                maximumHeight);
            return new Rectangle(
                workingArea.Left + Math.Max(0, (workingArea.Width - width) / 2),
                workingArea.Top + Math.Max(0, (workingArea.Height - height) / 2),
                width,
                height);
        }

        private Size GetMinimumWindowSize() => new(
            Math.Min(_viewMode == SetupViewMode.Accessible ? 720 : 900, _targetWorkingArea.Width),
            Math.Min(_viewMode == SetupViewMode.Accessible ? 560 : 640, _targetWorkingArea.Height));

        private Padding GetRootPadding()
        {
            int horizontal = 48;
            if (_viewMode == SetupViewMode.Accessible)
            {
                int contentWidth = Math.Min(
                    Math.Max(
                        BaseAccessibleContentWidth,
                        (int)Math.Round(BaseAccessibleContentWidth * AccessibleCanvasScale)),
                    Math.Max(1, ClientSize.Width - 96));
                horizontal = Math.Max(horizontal, (ClientSize.Width - contentWidth) / 2);
            }

            return new Padding(
                horizontal,
                _viewMode == SetupViewMode.Accessible ? 18 : 28,
                horizontal,
                _viewMode == SetupViewMode.Accessible ? 16 : 22);
        }

        private int GetViewSelectorWidth()
        {
            using Font font = new(
                "Segoe UI",
                Math.Max(8f, 9.2f * FontScale),
                FontStyle.Bold);
            int labelWidth = TextRenderer.MeasureText(
                T("Setup.ViewAccessible"),
                font,
                Size.Empty,
                TextFormatFlags.NoPadding |
                TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix).Width;
            return Math.Clamp(
                labelWidth + ControlDrawing.ScaleLogical(this, 86),
                _viewMode == SetupViewMode.Accessible ? 220 : 190,
                _viewMode == SetupViewMode.Accessible ? 360 : 280);
        }

        private int GetViewSelectorHeight() =>
            _viewMode == SetupViewMode.Accessible
                ? Math.Max(
                    ControlDrawing.ScaleLogical(this, 48),
                    LayoutHeight(42))
                : LayoutHeight(42);

        private void UpdateResponsiveLayout(bool rebuildContent)
        {
            if (_updatingViewLayout ||
                _root == null ||
                _footer == null)
            {
                return;
            }

            _updatingViewLayout = true;
            try
            {
                _root.Padding = GetRootPadding();
                int contentWidth = Math.Max(
                    1,
                    ClientSize.Width - _root.Padding.Horizontal);
                int selectorWidth = Math.Min(
                    GetViewSelectorWidth(),
                    Math.Max(1, contentWidth / 3));
                int selectorHeight = GetViewSelectorHeight();
                int headerGap = ControlDrawing.ScaleLogical(this, 18);
                int titleWidth = Math.Max(
                    1,
                    contentWidth - selectorWidth - headerGap);
                using Font headerFont = CreateFittedHeaderFont(
                    T("Setup.WelcomeTitle"),
                    titleWidth);
                int measuredHeaderHeight = TextRenderer.MeasureText(
                    T("Setup.WelcomeTitle"),
                    headerFont,
                    new Size(titleWidth, int.MaxValue),
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPrefix).Height;
                _root.RowStyles[0].Height = Math.Max(
                    Math.Max(
                        ControlDrawing.ScaleLogical(this, 58),
                        selectorHeight),
                    measuredHeaderHeight + ControlDrawing.ScaleLogical(this, 12));

                int navigationColumn = _viewMode == SetupViewMode.Accessible ? 240 : 190;
                int progressColumn = _viewMode == SetupViewMode.Accessible ? 220 : 200;
                int navigationHeight = _viewMode == SetupViewMode.Accessible
                    ? Math.Max(64, LayoutHeight(42))
                    : LayoutHeight(42);
                int footerTopPadding = _viewMode == SetupViewMode.Accessible ? 10 : 12;
                _root.RowStyles[3].Height =
                    navigationHeight + ControlDrawing.ScaleLogical(this, footerTopPadding);
                _footer.Padding = new Padding(
                    0,
                    ControlDrawing.ScaleLogical(this, footerTopPadding),
                    0,
                    0);
                int skipColumn =
                    _step == 4 && _startupState != SetupStartupState.Ready
                        ? (_viewMode == SetupViewMode.Accessible ? 140 : 118)
                        : 0;
                _footer.ColumnStyles[0].Width = navigationColumn;
                _footer.ColumnStyles[1].Width = skipColumn;
                _footer.ColumnStyles[3].Width = progressColumn;
                _footer.ColumnStyles[5].Width = skipColumn;
                _footer.ColumnStyles[6].Width = navigationColumn;
                _backButton.Size = new Size(navigationColumn - 10, navigationHeight);
                _continueButton.Size = new Size(navigationColumn - 10, navigationHeight);
                _skipButton.Size = new Size(
                    Math.Max(1, (int)_footer.ColumnStyles[5].Width - 8),
                    navigationHeight);

                MinimumSize = GetMinimumWindowSize();
                UpdateHeaderLayout();
                if (rebuildContent && _headingLabel != null)
                {
                    bool resumeHotkeyCapture = _capturingHotkey;
                    BuildStepContent();
                    ApplyVisuals();
                    if (resumeHotkeyCapture && _step == 2)
                    {
                        BeginHotkeyCapture();
                    }
                }

                UpdateStepTextRowHeights(contentWidth);
                _footer.PerformLayout();
                _root.PerformLayout();
                _footer.PerformLayout();
            }
            finally
            {
                _updatingViewLayout = false;
            }
        }

        private void UpdateStepTextRowHeights(int availableWidth)
        {
            if (_headingLabel?.Parent is not TableLayoutPanel layout ||
                _descriptionLabel?.Parent != layout)
            {
                return;
            }

            int width = Math.Max(1, availableWidth);
            layout.RowStyles[0].Height = MeasureLabelHeight(_headingLabel, width) +
                ControlDrawing.ScaleLogical(this, 4);
            layout.RowStyles[1].Height = MeasureLabelHeight(_descriptionLabel, width) +
                ControlDrawing.ScaleLogical(this, 4);
            if (_themeSectionLabel?.Parent == layout)
            {
                layout.RowStyles[3].Height = MeasureLabelHeight(_themeSectionLabel, width) +
                    ControlDrawing.ScaleLogical(this, 4);
            }
        }

        private static int MeasureLabelHeight(Label label, int availableWidth)
        {
            Size singleLine = TextRenderer.MeasureText(
                label.Text,
                label.Font,
                new Size(32767, 32767),
                TextFormatFlags.NoPadding |
                TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix);
            if (!label.Text.Contains('\n') && singleLine.Width <= availableWidth)
            {
                return singleLine.Height;
            }

            return TextRenderer.MeasureText(
                label.Text,
                label.Font,
                new Size(availableWidth, 32767),
                TextFormatFlags.NoPadding |
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoPrefix).Height;
        }

        private void UpdateHeaderLayout()
        {
            if (_header == null ||
                _welcomeLabel == null ||
                _viewSelector == null ||
                _header.Width <= 0 ||
                _header.Height <= 0)
            {
                return;
            }

            string welcomeTitle = T("Setup.WelcomeTitle");
            int selectorWidth = GetViewSelectorWidth();
            int selectorHeight = GetViewSelectorHeight();
            selectorWidth = Math.Min(
                selectorWidth,
                Math.Max(1, _header.Width / 3));
            _viewSelector.Bounds = new Rectangle(
                Math.Max(0, _header.Width - selectorWidth),
                Math.Max(0, (_header.Height - selectorHeight) / 2),
                selectorWidth,
                Math.Min(selectorHeight, _header.Height));
            int titleWidth = Math.Max(
                1,
                _viewSelector.Left - ControlDrawing.ScaleLogical(this, 18));
            Font fittedHeaderFont = CreateFittedHeaderFont(
                welcomeTitle,
                titleWidth);
            Font previousHeaderFont = _welcomeLabel.Font;
            if (previousHeaderFont.FontFamily.Name == fittedHeaderFont.FontFamily.Name &&
                previousHeaderFont.Style == fittedHeaderFont.Style &&
                Math.Abs(previousHeaderFont.Size - fittedHeaderFont.Size) < 0.01f)
            {
                fittedHeaderFont.Dispose();
            }
            else
            {
                _welcomeLabel.Font = fittedHeaderFont;
                _fittedHeaderFont?.Dispose();
                _fittedHeaderFont = fittedHeaderFont;
            }
            _welcomeLabel.AccessibleName = welcomeTitle;
            _welcomeLabel.Text = welcomeTitle;
            _welcomeLabel.Bounds = new Rectangle(
                0,
                0,
                titleWidth,
                _header.Height);
        }

        private Font CreateFittedHeaderFont(string text, int availableWidth)
        {
            Font baseFont = SetupFont(HeaderTitleFontSize, FontStyle.Bold);
            float minimumSize = Math.Max(11f, baseFont.Size * 0.7f);
            int safeWidth = Math.Max(1, (int)MathF.Floor(availableWidth * 0.9f));
            int measuredWidth = TextRenderer.MeasureText(
                text,
                baseFont,
                Size.Empty,
                TextFormatFlags.NoPadding |
                TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix).Width;
            float fittedSize = measuredWidth <= safeWidth
                ? baseFont.Size
                : Math.Max(
                    minimumSize,
                    baseFont.Size * safeWidth / Math.Max(1f, measuredWidth));
            return new Font("Segoe UI", fittedSize, FontStyle.Bold);
        }

        private void SwitchViewMode(SetupViewMode viewMode)
        {
            if (_viewMode == viewMode || StartupActionBlocksNavigation)
            {
                return;
            }

            if (_viewMode == SetupViewMode.Standard)
            {
                _standardBounds = Bounds;
                _targetWorkingArea = Screen.FromRectangle(Bounds).WorkingArea;
            }

            _viewMode = viewMode;
            _viewSelector.ViewMode = viewMode;

            _updatingViewLayout = true;
            try
            {
                if (viewMode == SetupViewMode.Accessible)
                {
                    Bounds = _targetWorkingArea;
                }
                else
                {
                    Bounds = ConstrainToWorkingArea(
                        _standardBounds,
                        _targetWorkingArea);
                }
            }
            finally
            {
                _updatingViewLayout = false;
            }

            UpdateResponsiveLayout(rebuildContent: true);
            UpdateAccessibilityText();
            if (IsHandleCreated)
            {
                AccessibilityNotifyClients(AccessibleEvents.Reorder, -1);
            }

            if (Visible && _viewSelector.Enabled)
            {
                BeginInvoke((MethodInvoker)(() => _viewSelector.Focus()));
            }
        }

        private static Rectangle ConstrainToWorkingArea(
            Rectangle bounds,
            Rectangle workingArea)
        {
            int width = Math.Min(bounds.Width, workingArea.Width);
            int height = Math.Min(bounds.Height, workingArea.Height);
            int x = Math.Clamp(
                bounds.X,
                workingArea.Left,
                Math.Max(workingArea.Left, workingArea.Right - width));
            int y = Math.Clamp(
                bounds.Y,
                workingArea.Top,
                Math.Max(workingArea.Top, workingArea.Bottom - height));
            return new Rectangle(x, y, width, height);
        }

        internal void ValidateViewToggleInteraction()
        {
            CreateControl();
            EnsureControlHandles(this);
            PerformLayoutTree(this);
            if (_viewSelector.Width < 8 || _viewSelector.Height < 8)
            {
                _viewSelector.Size = new Size(360, 48);
            }

            SetupViewMode initialMode = _viewMode;
            _viewSelector.PerformMouseClickForValidation();
            if (_viewMode == initialMode)
            {
                throw new InvalidOperationException(
                    "The setup view toggle did not respond to a mouse click.");
            }

            if (_viewMode == SetupViewMode.Accessible &&
                Bounds != _targetWorkingArea)
            {
                throw new InvalidOperationException(
                    "Large setup did not expand to the target monitor work area.");
            }

            _viewSelector.PerformMouseClickForValidation();
            if (_viewMode != initialMode)
            {
                throw new InvalidOperationException(
                    "The setup view toggle did not return to its initial mode.");
            }
        }

        internal void CaptureStep(
            string path,
            int step,
            int themeMode,
            bool hoverContinue = false,
            Keys? enableKey = null,
            SetupStartupState? startupState = null,
            bool startupProgressComplete = false,
            bool capturingHotkey = false,
            SetupViewMode? viewMode = null,
            Size? captureSize = null,
            float? windowsTextScale = null,
            bool scrollToBottom = false)
        {
            _captureMode = true;
            if (viewMode.HasValue)
            {
                _viewMode = viewMode.Value;
            }
            if (windowsTextScale.HasValue)
            {
                _captureWindowsTextScale = windowsTextScale.Value;
            }
            if (captureSize.HasValue)
            {
                _targetWorkingArea = new Rectangle(Point.Empty, captureSize.Value);
                Bounds = _viewMode == SetupViewMode.Accessible
                    ? _targetWorkingArea
                    : GetStandardBounds(_targetWorkingArea);
            }
            _viewSelector.ViewMode = _viewMode;
            UpdateResponsiveLayout(rebuildContent: true);
            _themeMode = themeMode;
            _useDarkTheme = themeMode == ThemeDark;
            _palette = _useDarkTheme ? ThemePalettes.Dark : ThemePalettes.Light;
            _enableKey = enableKey ?? _enableKey;
            if (startupState.HasValue)
            {
                _startupState = startupState.Value;
                _startupStatusChecked = true;
            }
            ShowStep(step, animate: false);
            if (capturingHotkey)
            {
                BeginHotkeyCapture();
            }
            if (startupProgressComplete)
            {
                _startupServiceTile?.SetCompletionProgressForCapture();
            }
            Opacity = 1;
            ShowInTaskbar = false;
            Rectangle virtualScreen = SystemInformation.VirtualScreen;
            Location = new Point(virtualScreen.Right + 64, virtualScreen.Bottom + 64);
            Show();
            Application.DoEvents();
            WindowChrome.TrySetDarkTitleBar(this, _useDarkTheme);
            EnsureControlHandles(this);
            PerformLayoutTree(this);
            ValidateAccessibilityTree(this);
            ValidateSetupViewport();
            _continueButton.SetInteractionStateForCapture(hoverContinue);
            WaitForCaptureUi();
            _ = scrollToBottom;
            try
            {
                using var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(BackColor);
                    using SolidBrush rootBrush = new(_root.BackColor);
                    graphics.FillRectangle(
                        rootBrush,
                        _root.Left,
                        _root.Top,
                        _root.Width,
                        _root.Height);
                    foreach (Control section in new Control[]
                             {
                                 _header,
                                 _headerDivider,
                                 _contentHost,
                                 _footer
                             })
                    {
                        if (!section.Visible || section.Width <= 0 || section.Height <= 0)
                        {
                            continue;
                        }

                        using var sectionBitmap = new Bitmap(
                            section.Width,
                            section.Height,
                            PixelFormat.Format32bppPArgb);
                        section.DrawToBitmap(
                            sectionBitmap,
                            new Rectangle(Point.Empty, sectionBitmap.Size));
                        graphics.DrawImageUnscaled(
                            sectionBitmap,
                            _root.Left + section.Left,
                            _root.Top + section.Top);
                    }
                }

                bitmap.Save(path, ImageFormat.Png);
            }
            finally
            {
                _continueButton.SetInteractionStateForCapture(hovered: false);
                Hide();
            }
        }

        private void ValidateSetupViewport()
        {
            if (_header.Bottom > _headerDivider.Top ||
                _headerDivider.Bottom > _contentHost.Top ||
                _contentHost.Bottom > _footer.Top)
            {
                throw new InvalidOperationException(
                    "Setup header, content, and footer regions must not overlap.");
            }

            if (_contentHost.AutoScroll ||
                _contentHost.VerticalScroll.Visible ||
                _contentHost.HorizontalScroll.Visible)
            {
                throw new InvalidOperationException(
                    "Setup content must fit without scrollbars.");
            }

            if (_viewSelector.Left < 0 ||
                _viewSelector.Top < 0 ||
                _viewSelector.Right > _header.ClientSize.Width ||
                _viewSelector.Bottom > _header.ClientSize.Height)
            {
                throw new InvalidOperationException(
                    "The setup view toggle extends outside the header.");
            }

            _stepIndicator.ValidatePaintBounds();

            foreach (Control child in _contentHost.Controls)
            {
                if (child.Left < 0 ||
                    child.Top < 0 ||
                    child.Right > _contentHost.ClientSize.Width + 1 ||
                    child.Bottom > _contentHost.ClientSize.Height + 1)
                {
                    throw new InvalidOperationException(
                        $"Setup content '{child.GetType().Name}' extends outside the viewport.");
                }
            }

            ValidateVisibleLabelsFit(this);
        }

        private static void ValidateVisibleLabelsFit(Control root)
        {
            if (root is Label label &&
                label.Visible &&
                !string.IsNullOrWhiteSpace(label.Text) &&
                label.ClientSize.Width > 0 &&
                label.ClientSize.Height > 0)
            {
                Size singleLine = TextRenderer.MeasureText(
                    label.Text,
                    label.Font,
                    new Size(32767, 32767),
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPrefix);
                Size measured =
                    !label.Text.Contains('\n') &&
                    singleLine.Width <= label.ClientSize.Width
                    ? singleLine
                    : TextRenderer.MeasureText(
                        label.Text,
                        label.Font,
                        new Size(label.ClientSize.Width, int.MaxValue),
                        TextFormatFlags.NoPadding |
                        TextFormatFlags.WordBreak |
                        TextFormatFlags.NoPrefix);
                if (measured.Height > label.ClientSize.Height + 2)
                {
                    throw new InvalidOperationException(
                        $"Setup label '{label.Text}' is clipped " +
                        $"({measured.Width}x{measured.Height} required; " +
                        $"{label.ClientSize.Width}x{label.ClientSize.Height} available; " +
                        $"single line {singleLine.Width}x{singleLine.Height}).");
                }
            }

            foreach (Control child in root.Controls)
            {
                ValidateVisibleLabelsFit(child);
            }
        }

        private static void ValidateAccessibilityTree(Control root)
        {
            bool requiresMetadata =
                root is FirstRunSetupForm or
                Label or
                ModernButton or
                SetupViewToggle or
                SetupChoiceCard or
                SetupHotkeyRow or
                SetupUsageTile or
                SetupStartupServiceTile or
                SetupCompletionTile or
                SetupStepIndicator;
            if (requiresMetadata &&
                root.Visible &&
                string.IsNullOrWhiteSpace(root.AccessibilityObject.Name))
            {
                throw new InvalidOperationException(
                    $"Visible setup control '{root.GetType().Name}' has no accessible name.");
            }
            if (requiresMetadata &&
                root.Visible &&
                root.AccessibilityObject.Role is AccessibleRole.Default or AccessibleRole.None)
            {
                throw new InvalidOperationException(
                    $"Visible setup control '{root.GetType().Name}' has no accessible role.");
            }

            bool interactive =
                root is ModernButton or
                SetupChoiceCard or
                SetupHotkeyRow;
            if (interactive &&
                root.Visible &&
                string.IsNullOrWhiteSpace(root.AccessibilityObject.Description))
            {
                throw new InvalidOperationException(
                    $"Interactive setup control '{root.GetType().Name}' has no accessible description.");
            }
            if (interactive && root.Visible && root.Enabled && !root.TabStop)
            {
                throw new InvalidOperationException(
                    $"Interactive setup control '{root.GetType().Name}' is missing keyboard focus.");
            }
            if (root is Label { Visible: true, AutoEllipsis: true })
            {
                throw new InvalidOperationException(
                    "Setup labels must wrap instead of using ellipses.");
            }
            if (root is ModernButton { Visible: true, WrapText: false })
            {
                throw new InvalidOperationException(
                    "Setup buttons must wrap instead of using ellipses.");
            }

            foreach (Control child in root.Controls)
            {
                ValidateAccessibilityTree(child);
            }
        }

        private static void WaitForCaptureUi()
        {
            var timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < 120)
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(10);
            }
        }

        private void PrepareForReveal()
        {
            EnsureControlHandles(this);
            PerformLayout();
            WindowChrome.RedrawNow(this);
        }

        private static void EnsureControlHandles(Control root)
        {
            _ = root.Handle;
            foreach (Control child in root.Controls)
            {
                EnsureControlHandles(child);
            }
        }

        private static void PerformLayoutTree(Control root)
        {
            root.PerformLayout();
            foreach (Control child in root.Controls)
            {
                PerformLayoutTree(child);
            }

            root.PerformLayout();
        }

        private void Continue()
        {
            if (_step == 2 && HasShortcutErrors())
            {
                UpdateHotkeyRows();
                return;
            }

            if (_step < 4)
            {
                ShowStep(_step + 1);
                return;
            }

            if (_step == 5)
            {
                CompleteSetup(_startupServiceSkipped);
                return;
            }

            if (_startupState == SetupStartupState.Ready)
            {
                if (_startupOnly)
                {
                    CompleteSetup(startupServiceSkipped: false);
                }
                else
                {
                    _startupServiceSkipped = false;
                    ShowStep(5);
                }
                return;
            }

            if (!StartupActionInProgress)
            {
                _ = ConfigureStartupServiceAsync();
            }
        }

        private void CompleteSetup(bool startupServiceSkipped)
        {
            _startupServiceSkipped = startupServiceSkipped;
            Selection = new FirstRunSetupSelection(
                _language,
                _themeMode,
                _fontSize,
                _enableKey,
                startupServiceSkipped,
                _viewMode);
            _accepted = true;
            Close();
        }

        private void SkipStartupService()
        {
            if (StartupActionInProgress)
            {
                return;
            }

            if (_startupOnly)
            {
                Close();
                return;
            }

            _startupServiceSkipped = true;
            ShowStep(5);
        }

        private void ShowStep(int step, bool animate = true)
        {
            int nextStep = Math.Clamp(step, _startupOnly ? 4 : 0, _startupOnly ? 4 : 5);
            if (_step == 3 && nextStep != 3)
            {
                StopLivePractice();
            }

            CancelHotkeyCapture();
            _step = nextStep;
            if (_step == 3 && Visible && !_captureMode)
            {
                StartLivePractice();
            }
            BuildStepContent();
            ApplyVisuals();
            _stepIndicator.Step = _step;
            if (_step == 4 && Visible && !_captureMode)
            {
                BeginStartupStatusCheck();
            }

            if (Visible && !_captureMode)
            {
                BeginInvoke((MethodInvoker)FocusCurrentStepEntry);
            }
        }

        private void FocusCurrentStepEntry()
        {
            if (!Visible || _captureMode)
            {
                return;
            }

            Control? target = _step switch
            {
                0 or 1 => _cards.FirstOrDefault(card => card.Selected) ?? _cards.FirstOrDefault(),
                2 => _enableHotkeyRow,
                _ => _continueButton
            };
            if (target is { Visible: true, Enabled: true })
            {
                target.Focus();
            }
        }

        private void StartLivePractice()
        {
            if (!_allowLivePractice || _practiceContext != null || _captureMode)
            {
                return;
            }

            try
            {
                _practiceContext = new TrayContext(
                    setupPracticeMode: true,
                    setupPracticeKey: _enableKey);
            }
            catch (Exception ex)
            {
                ErrorLog.Write("FirstRunSetup.LivePractice.Start", ex);
                _practiceContext = null;
            }
        }

        private void StopLivePractice()
        {
            if (_practiceContext == null)
            {
                return;
            }

            try
            {
                _practiceContext.StopSetupPractice();
                _practiceContext.Dispose();
            }
            catch (Exception ex)
            {
                ErrorLog.Write("FirstRunSetup.LivePractice.Stop", ex);
            }
            finally
            {
                _practiceContext = null;
            }
        }

        private void BuildStepContent()
        {
            _contentHost.SuspendLayout();
            try
            {
                while (_contentHost.Controls.Count > 0)
                {
                    Control previousContent = _contentHost.Controls[0];
                    _contentHost.Controls.RemoveAt(0);
                    previousContent.Dispose();
                }

                _cards.Clear();
                _themeSectionLabel = null;
                _enableHotkeyRow = null;
                _usageTiles.Clear();
                _startupServiceTile = null;
                _completionTile = null;
                _contentHost.AutoScroll = false;

                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    AutoSize = false,
                    ColumnCount = 1,
                    RowCount = 6,
                    Margin = Padding.Empty,
                    Padding = new Padding(
                        0,
                        LayoutHeight(_step >= 4 ? 4 : 12),
                        0,
                        0)
                };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

                _headingLabel = NewLabel();
                _descriptionLabel = NewLabel();
                _descriptionLabel.AutoEllipsis = false;
                layout.Controls.Add(_headingLabel, 0, 0);
                layout.Controls.Add(_descriptionLabel, 0, 1);
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, LayoutHeight(54)));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, LayoutHeight(38)));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, LayoutHeight(22)));

                if (_step == 0)
                {
                    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                    layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
                    layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
                    layout.Controls.Add(BuildLanguageCards(), 0, 3);
                }
                else if (_step == 1)
                {
                    _themeSectionLabel = NewLabel();
                    layout.RowStyles.Add(new RowStyle(SizeType.Absolute, LayoutHeight(40)));
                    if (_viewMode == SetupViewMode.Accessible)
                    {
                        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
                    }
                    else
                    {
                        layout.RowStyles.Add(new RowStyle(
                            SizeType.Absolute,
                            LayoutHeight(168)));
                        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                    }
                    layout.Controls.Add(_themeSectionLabel, 0, 3);
                    layout.Controls.Add(BuildThemeCards(), 0, 4);
                }
                else if (_step == 2)
                {
                    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                    layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
                    layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
                    _enableHotkeyRow = BuildHotkeyRow();
                    layout.Controls.Add(_enableHotkeyRow, 0, 3);
                }
                else if (_step == 3)
                {
                    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333f));
                    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333f));
                    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.334f));
                    layout.Controls.Add(BuildUsageTile(SetupUsageKind.Zoom), 0, 3);
                    layout.Controls.Add(BuildUsageTile(SetupUsageKind.Invert), 0, 4);
                    layout.Controls.Add(BuildUsageTile(SetupUsageKind.Mode), 0, 5);
                }
                else if (_step == 4)
                {
                    layout.RowStyles[1].SizeType = SizeType.Absolute;
                    layout.RowStyles[1].Height = LayoutHeight(72);
                    layout.RowStyles[2].SizeType = SizeType.Absolute;
                    layout.RowStyles[2].Height = LayoutHeight(6);
                    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                    layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
                    layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
                    _startupServiceTile = new SetupStartupServiceTile
                    {
                        Dock = DockStyle.Fill,
                        Margin = new Padding(0, 5, 0, 5)
                    };
                    layout.Controls.Add(_startupServiceTile, 0, 3);
                }
                else
                {
                    layout.RowStyles[1].SizeType = SizeType.Absolute;
                    layout.RowStyles[1].Height = LayoutHeight(72);
                    layout.RowStyles[2].SizeType = SizeType.Absolute;
                    layout.RowStyles[2].Height = LayoutHeight(6);
                    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                    layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
                    layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
                    _completionTile = new SetupCompletionTile
                    {
                        Dock = DockStyle.Fill,
                        Margin = new Padding(0, 5, 0, 5),
                        AnimationComplete = _captureMode
                    };
                    layout.Controls.Add(_completionTile, 0, 3);
                }

                _contentHost.Controls.Add(layout);
                UpdateText();
            }
            finally
            {
                _contentHost.ResumeLayout(performLayout: true);
            }
        }

        private Control BuildLanguageCards()
        {
            var grid = NewCardList(5);
            AddCard(
                grid,
                UiText.GetLanguageDisplayName(UiLanguage.English, _language),
                T("Setup.LanguageEnglishHint"),
                string.Empty,
                SetupIcon.EnglishLanguage,
                _language == UiLanguage.English,
                () => SelectLanguage(UiLanguage.English),
                compact: true,
                vertical: true);
            AddCard(
                grid,
                UiText.GetLanguageDisplayName(UiLanguage.Danish, _language),
                T("Setup.LanguageDanishHint"),
                string.Empty,
                SetupIcon.DanishFlag,
                _language == UiLanguage.Danish,
                () => SelectLanguage(UiLanguage.Danish),
                compact: true,
                vertical: true);
            AddCard(
                grid,
                UiText.GetLanguageDisplayName(UiLanguage.Swedish, _language),
                T("Setup.LanguageSwedishHint"),
                string.Empty,
                SetupIcon.SwedishFlag,
                _language == UiLanguage.Swedish,
                () => SelectLanguage(UiLanguage.Swedish),
                compact: true,
                vertical: true);
            AddCard(
                grid,
                UiText.GetLanguageDisplayName(UiLanguage.Norwegian, _language),
                T("Setup.LanguageNorwegianHint"),
                string.Empty,
                SetupIcon.NorwegianFlag,
                _language == UiLanguage.Norwegian,
                () => SelectLanguage(UiLanguage.Norwegian),
                compact: true,
                vertical: true);
            AddCard(
                grid,
                UiText.GetLanguageDisplayName(UiLanguage.Finnish, _language),
                T("Setup.LanguageFinnishHint"),
                string.Empty,
                SetupIcon.FinnishFlag,
                _language == UiLanguage.Finnish,
                () => SelectLanguage(UiLanguage.Finnish),
                compact: true,
                vertical: true);
            if (_viewMode != SetupViewMode.Accessible)
            {
                return grid;
            }

            int availableWidth = Math.Max(
                1,
                ClientSize.Width - GetRootPadding().Horizontal);
            int listWidth = Math.Min(
                availableWidth,
                Math.Max(
                    720,
                    (int)Math.Round(840f * AccessibleCanvasScale)));
            var centered = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            centered.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            centered.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, listWidth));
            centered.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            centered.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            centered.Controls.Add(grid, 1, 0);
            return centered;
        }

        private Control BuildThemeCards()
        {
            var grid = NewCardGrid(3);
            AddCard(
                grid,
                T("Setup.ThemeSystemLabel"),
                T("Setup.ThemeAutoHint"),
                string.Empty,
                SetupIcon.System,
                _themeMode == ThemeAuto,
                () => SelectTheme(ThemeAuto),
                compact: true,
                prominent: true);
            AddCard(
                grid,
                T("Settings.ThemeDark"),
                T("Setup.ThemeDarkHint"),
                string.Empty,
                SetupIcon.Moon,
                _themeMode == ThemeDark,
                () => SelectTheme(ThemeDark),
                compact: true,
                prominent: true);
            AddCard(
                grid,
                T("Settings.ThemeLight"),
                T("Setup.ThemeLightHint"),
                string.Empty,
                SetupIcon.Sun,
                _themeMode == ThemeLight,
                () => SelectTheme(ThemeLight),
                compact: true,
                prominent: true);
            return grid;
        }

        private SetupHotkeyRow BuildHotkeyRow()
        {
            var row = new SetupHotkeyRow
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 6, 0, 6)
            };
            row.CaptureRequested += (_, _) => BeginHotkeyCapture();
            return row;
        }

        private SetupUsageTile BuildUsageTile(SetupUsageKind kind)
        {
            string title = T(kind switch
            {
                SetupUsageKind.Zoom => "Setup.UsageZoomTitle",
                SetupUsageKind.Invert => "Setup.UsageInvertTitle",
                _ => "Setup.UsageModeTitle"
            });
            string description = T(kind switch
            {
                SetupUsageKind.Zoom => "Setup.UsageZoomBody",
                SetupUsageKind.Invert => "Setup.UsageInvertBody",
                _ => "Setup.UsageModeBody"
            });
            var tile = new SetupUsageTile(kind, title, description, KeyLabel(_enableKey), T("Setup.Or"))
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 0, 5)
            };
            tile.ShowWindowsLogo = IsWindowsKey(_enableKey);
            _usageTiles.Add(tile);
            return tile;
        }

        private void BeginHotkeyCapture()
        {
            _capturingHotkey = true;
            _pendingControlKey = false;
            UpdateHotkeyRows();
            _enableHotkeyRow?.Focus();
        }

        private void CancelHotkeyCapture()
        {
            _capturingHotkey = false;
            _pendingControlKey = false;
            UpdateHotkeyRows();
        }

        private void HandleSetupKeyDown(object? sender, KeyEventArgs e)
        {
            if (!_capturingHotkey)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    if (!StartupActionBlocksNavigation)
                    {
                        if (_step > 0 && !_startupOnly)
                        {
                            ShowStep(_step - 1);
                        }
                        else
                        {
                            Close();
                        }
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }

                return;
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
            if (e.KeyCode == Keys.Escape)
            {
                CancelHotkeyCapture();
                return;
            }

            if (e.KeyCode == Keys.ControlKey && !e.Alt)
            {
                _pendingControlKey = true;
                return;
            }

            Keys capturedKey = e.KeyCode == Keys.Menu && (e.Control || _pendingControlKey)
                ? Keys.RMenu
                : e.KeyCode;
            CompleteHotkeyCapture(capturedKey);
        }

        private void HandleSetupKeyUp(object? sender, KeyEventArgs e)
        {
            if (!_capturingHotkey ||
                !_pendingControlKey ||
                e.KeyCode != Keys.ControlKey)
            {
                return;
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
            CompleteHotkeyCapture(Keys.ControlKey);
        }

        private void CompleteHotkeyCapture(Keys key)
        {
            _enableKey = key;
            _capturingHotkey = false;
            _pendingControlKey = false;
            UpdateHotkeyRows();
        }

        private void UpdateHotkeyRows()
        {
            if (_enableHotkeyRow == null)
            {
                return;
            }

            SetupValidation enableValidation = ValidateHotkey(_enableKey);

            _enableHotkeyRow.UpdateContent(
                T("Setup.PrimaryHotkeyTitle"),
                T("Setup.PrimaryHotkeyBody"),
                KeyLabel(_enableKey),
                _capturingHotkey,
                enableValidation,
                T("Setup.ChangeKey"),
                T("Setup.PressKey"),
                IsWindowsKey(_enableKey));
            _continueButton.Enabled = _step != 2 || !HasShortcutErrors();
        }

        private SetupValidation ValidateHotkey(Keys key)
        {
            if (!IsSupportedShortcutKey(key))
            {
                return new SetupValidation(
                    SetupValidationLevel.Error,
                    T("Setup.ShortcutUnsupported"));
            }

            if (key == Keys.F)
            {
                return new SetupValidation(
                    SetupValidationLevel.Error,
                    T("Setup.ShortcutFollowCursorConflict"));
            }

            if (key is Keys.LWin or Keys.RWin or Keys.Menu or Keys.LMenu or Keys.RMenu)
            {
                return new SetupValidation(
                    SetupValidationLevel.Warning,
                    T("Setup.ShortcutPriorityWarning", KeyLabel(key)));
            }

            if (key == Keys.CapsLock)
            {
                return new SetupValidation(
                    SetupValidationLevel.Warning,
                    T("Setup.ShortcutCapsLockWarning"));
            }

            if (key is Keys.I or Keys.Z)
            {
                return new SetupValidation(
                    SetupValidationLevel.Warning,
                    T("Setup.ShortcutActionKeyWarning", KeyLabel(key)));
            }

            if (key is Keys.Enter or Keys.Return || key == (Keys)FnVirtualKey)
            {
                return new SetupValidation(
                    SetupValidationLevel.Warning,
                    T("Setup.ShortcutSystemWarning", KeyLabel(key)));
            }

            if (!IsRecommendedShortcutKey(key))
            {
                return new SetupValidation(
                    SetupValidationLevel.Warning,
                    T("Setup.ShortcutGeneralWarning"));
            }

            return new SetupValidation(SetupValidationLevel.None, string.Empty);
        }

        private bool HasShortcutErrors()
        {
            return ValidateHotkey(_enableKey).Level == SetupValidationLevel.Error;
        }

        private static bool IsSupportedShortcutKey(Keys key)
        {
            return key == (Keys)FnVirtualKey ||
                   key != Keys.None &&
                   key != Keys.KeyCode &&
                   key != Keys.Modifiers &&
                   key != Keys.ProcessKey &&
                   key != Keys.Packet;
        }

        private static bool IsRecommendedShortcutKey(Keys key)
        {
            if (key is Keys.ControlKey or Keys.LControlKey or Keys.RControlKey or
                Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey or
                Keys.Menu or Keys.LMenu or Keys.LWin or Keys.RWin or Keys.Tab)
            {
                return true;
            }

            int value = (int)key;
            return value is >= (int)Keys.A and <= (int)Keys.Z ||
                   value is >= (int)Keys.D0 and <= (int)Keys.D9 ||
                   value is >= (int)Keys.F1 and <= (int)Keys.F24;
        }

        private static bool IsWindowsKey(Keys key) => key is Keys.LWin or Keys.RWin;

        private string KeyLabel(Keys key)
        {
            if (key is >= Keys.D0 and <= Keys.D9)
            {
                return ((char)('0' + ((int)key - (int)Keys.D0))).ToString();
            }

            if (key is >= Keys.NumPad0 and <= Keys.NumPad9)
            {
                return "NUM " + ((char)('0' + ((int)key - (int)Keys.NumPad0)));
            }

            return key switch
            {
                Keys.ControlKey or Keys.LControlKey or Keys.RControlKey => "CTRL",
                Keys.Menu or Keys.LMenu => "ALT",
                Keys.RMenu => "ALTGR",
                Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey => "SHIFT",
                Keys.LWin or Keys.RWin => "WIN",
                Keys.CapsLock => "CAPS LOCK",
                Keys.Enter or Keys.Return => "ENTER",
                Keys.Escape => "ESC",
                Keys.Delete => "DEL",
                Keys.Insert => "INS",
                Keys.PageUp => "PGUP",
                Keys.PageDown => "PGDN",
                Keys.Back => "BACKSPACE",
                Keys.Space => "SPACE",
                Keys.Tab => "TAB",
                Keys.Oemplus or Keys.Add => "+",
                Keys.OemMinus or Keys.Subtract => "−",
                Keys.Left => "←",
                Keys.Right => "→",
                Keys.Up => "↑",
                Keys.Down => "↓",
                (Keys)FnVirtualKey => "FN",
                _ => key.ToString().ToUpperInvariant()
            };
        }

        private TableLayoutPanel NewCardGrid(int columns)
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = columns,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            for (int i = 0; i < columns; i++)
            {
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            }
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            return grid;
        }

        private TableLayoutPanel NewCardList(int rows)
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = rows,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int index = 0; index < rows; index++)
            {
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));
            }

            return grid;
        }

        private void AddCard(
            TableLayoutPanel grid,
            string title,
            string description,
            string badge,
            SetupIcon icon,
            bool selected,
            Action action,
            bool compact = false,
            bool vertical = false,
            bool prominent = false)
        {
            int cardIndex = grid.Controls.Count;
            var card = new SetupChoiceCard(title, description, badge, icon)
            {
                Dock = DockStyle.Fill,
                Margin = vertical
                    ? _viewMode == SetupViewMode.Accessible
                        ? new Padding(0, 2, 0, cardIndex == grid.RowCount - 1 ? 2 : 4)
                        : new Padding(0, 6, 0, cardIndex == grid.RowCount - 1 ? 6 : 8)
                    : new Padding(0, 6, cardIndex == grid.ColumnCount - 1 ? 0 : 14, 6),
                Compact = compact,
                Prominent = prominent,
                TabIndex = cardIndex
            };
            card.ApplyAccessibilityLabels(
                T("Setup.AccessibilitySelect"),
                T("Setup.AccessibilitySelected"));
            card.Selected = selected;
            card.Click += (_, _) => action();
            _cards.Add(card);
            grid.Controls.Add(card, vertical ? 0 : cardIndex, vertical ? cardIndex : 0);
        }

        private void SelectLanguage(UiLanguage language)
        {
            if (_language == language)
            {
                return;
            }

            _language = language;
            BuildStepContent();
            ApplyVisuals();
            if (Visible && !_captureMode)
            {
                BeginInvoke((MethodInvoker)FocusCurrentStepEntry);
            }
        }

        private void SelectTheme(int themeMode)
        {
            if (_themeMode == themeMode)
            {
                return;
            }

            bool nextDarkTheme = AppThemeBootstrap.ShouldUseDarkPalette(themeMode);
            void ApplySelection()
            {
                _themeMode = themeMode;
                _useDarkTheme = nextDarkTheme;
                _palette = _useDarkTheme ? ThemePalettes.Dark : ThemePalettes.Light;
                foreach (SetupChoiceCard card in _cards)
                {
                    card.Selected = card.Icon switch
                    {
                        SetupIcon.System => themeMode == ThemeAuto,
                        SetupIcon.Moon => themeMode == ThemeDark,
                        SetupIcon.Sun => themeMode == ThemeLight,
                        _ => card.Selected
                    };
                }

                ApplyVisuals();
            }

            if (nextDarkTheme != _useDarkTheme)
            {
                RunWaveTransition(ApplySelection);
            }
            else
            {
                ApplySelection();
            }
        }

        private bool StartupActionInProgress =>
            _startupState is SetupStartupState.Checking or
                SetupStartupState.AwaitingApproval or
                SetupStartupState.Installing or
                SetupStartupState.Verifying;

        private bool StartupActionBlocksNavigation =>
            _step == 4 && StartupActionInProgress;

        private async void BeginStartupStatusCheck()
        {
            if (_startupStatusChecked || _captureMode || IsDisposed)
            {
                return;
            }

            _startupStatusChecked = true;
            _startupState = SetupStartupState.Checking;
            UpdateStartupServiceContent();
            try
            {
                bool ready = await Task.Run(() => StartupTaskService.IsReadyForCurrentBuild(out _));
                if (IsDisposed || Disposing)
                {
                    return;
                }

                _startupState = ready
                    ? SetupStartupState.Ready
                    : SetupStartupState.NotConfigured;
                UpdateStartupServiceContent();
            }
            catch (Exception ex)
            {
                ErrorLog.Write("FirstRunSetup.StartupStatus", ex);
                if (!IsDisposed && !Disposing)
                {
                    _startupState = SetupStartupState.Failed;
                    UpdateStartupServiceContent();
                }
            }
        }

        private async Task ConfigureStartupServiceAsync()
        {
            if (StartupActionInProgress || IsDisposed)
            {
                return;
            }

            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath))
            {
                _startupState = SetupStartupState.Failed;
                UpdateStartupServiceContent();
                return;
            }

            _startupState = SetupStartupState.AwaitingApproval;
            UpdateStartupServiceContent();
            await Task.Yield();

            Process? helper;
            try
            {
                string currentUser = string.IsNullOrWhiteSpace(Environment.UserDomainName)
                    ? Environment.UserName
                    : Environment.UserDomainName + "\\" + Environment.UserName;
                helper = Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    Verb = "runas",
                    Arguments = Program.SetupStartupTaskInstallFlag +
                        " --startup-task-user " +
                        QuoteProcessArgument(currentUser)
                });
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                _startupState = SetupStartupState.Declined;
                UpdateStartupServiceContent();
                return;
            }
            catch (Exception ex)
            {
                ErrorLog.Write("FirstRunSetup.StartupLaunch", ex);
                _startupState = SetupStartupState.Failed;
                UpdateStartupServiceContent();
                return;
            }

            if (helper == null)
            {
                _startupState = SetupStartupState.Failed;
                UpdateStartupServiceContent();
                return;
            }

            _startupState = SetupStartupState.Installing;
            UpdateStartupServiceContent();
            using (helper)
            {
                try
                {
                    await helper.WaitForExitAsync();
                    int exitCode = helper.ExitCode;
                    if (IsDisposed || Disposing)
                    {
                        return;
                    }

                    _startupState = SetupStartupState.Verifying;
                    UpdateStartupServiceContent();
                    StartupTaskService.InvalidateCache();
                    bool ready = await Task.Run(() => StartupTaskService.IsReadyForCurrentBuild(out _));
                    if (IsDisposed || Disposing)
                    {
                        return;
                    }

                    if (exitCode == 0 && ready)
                    {
                        if (_startupServiceTile != null)
                        {
                            await _startupServiceTile.CompleteProgressAsync();
                        }
                        if (IsDisposed || Disposing)
                        {
                            return;
                        }

                        _startupState = SetupStartupState.Ready;
                        _startupServiceSkipped = false;
                    }
                    else
                    {
                        _startupState = SetupStartupState.Failed;
                    }
                }
                catch (Exception ex)
                {
                    ErrorLog.Write("FirstRunSetup.StartupWait", ex);
                    _startupState = SetupStartupState.Failed;
                }
            }

            if (!IsDisposed && !Disposing)
            {
                UpdateStartupServiceContent();
            }
        }

        private void UpdateStartupServiceContent()
        {
            if (_step != 4)
            {
                _continueButton.Text = T(_step == 5 ? "Setup.Finish" : "Setup.Continue");
                _continueButton.Enabled = true;
                _skipButton.Visible = false;
                _backButton.Enabled = true;
                UpdateViewSelectorAvailability();
                UpdateAccessibilityText();
                UpdateResponsiveLayout(rebuildContent: false);
                return;
            }

            string statusText = T(_startupState switch
            {
                SetupStartupState.Checking => "Setup.StartupStatusChecking",
                SetupStartupState.NotConfigured => "Setup.StartupStatusNotConfigured",
                SetupStartupState.AwaitingApproval => "Setup.StartupStatusAwaitingApproval",
                SetupStartupState.Installing => "Setup.StartupStatusInstalling",
                SetupStartupState.Verifying => "Setup.StartupStatusVerifying",
                SetupStartupState.Ready => "Setup.StartupStatusReady",
                SetupStartupState.Declined => "Setup.StartupStatusDeclined",
                _ => "Setup.StartupStatusFailed"
            });
            _startupServiceTile?.UpdateContent(
                T("Setup.StartupCardTitle"),
                T("Setup.StartupCardBody"),
                T("Setup.StartupBenefitAutostart"),
                T("Setup.StartupBenefitElevated"),
                T("Setup.StartupBenefitApproval"),
                statusText,
                _startupState);

            bool busy = StartupActionInProgress;
            _continueButton.Text = T(_startupState switch
            {
                SetupStartupState.Ready => "Setup.Finish",
                SetupStartupState.Declined or SetupStartupState.Failed => "Setup.StartupRetry",
                SetupStartupState.NotConfigured => "Setup.StartupConfigure",
                _ => "Setup.StartupPleaseWait"
            });
            _continueButton.Font = SetupFont(
                _viewMode == SetupViewMode.Accessible ? 9.2f : 10.5f,
                FontStyle.Bold);
            _continueButton.Enabled = !busy;
            _backButton.Enabled = !busy;
            _skipButton.Visible = !busy && _startupState != SetupStartupState.Ready;
            _skipButton.Enabled = !busy;
            UpdateViewSelectorAvailability();
            UpdateAccessibilityText();
            UpdateResponsiveLayout(rebuildContent: false);
        }

        private void UpdateViewSelectorAvailability()
        {
            _viewSelector.Enabled = !StartupActionBlocksNavigation;
        }

        private void UpdateAccessibilityText()
        {
            AccessibleName = Text;
            AccessibleDescription = string.Join(
                " ",
                new[]
                {
                    _welcomeLabel.Text,
                    _headingLabel?.Text,
                    _descriptionLabel?.Text
                }.Where(value => !string.IsNullOrWhiteSpace(value)));

            _welcomeLabel.AccessibleName = _welcomeLabel.Text;
            if (_headingLabel != null)
            {
                _headingLabel.AccessibleName = _headingLabel.Text;
            }
            if (_descriptionLabel != null)
            {
                _descriptionLabel.AccessibleName = _descriptionLabel.Text;
            }
            if (_themeSectionLabel != null)
            {
                _themeSectionLabel.AccessibleName = _themeSectionLabel.Text;
            }

            _backButton.AccessibleName = _backButton.Text;
            _backButton.AccessibleDescription = T("Setup.AccessibilityBack");
            _backButton.AccessibleDefaultActionDescription = _backButton.AccessibleDescription;
            _skipButton.AccessibleName = _skipButton.Text;
            _skipButton.AccessibleDescription = T("Setup.AccessibilitySkip");
            _skipButton.AccessibleDefaultActionDescription = _skipButton.AccessibleDescription;
            _continueButton.AccessibleName = _continueButton.Text;
            _continueButton.AccessibleDescription = T(
                _step == 5
                    ? "Setup.AccessibilityFinish"
                    : _step == 4
                        ? StartupActionInProgress
                            ? "Setup.AccessibilityStartupBusy"
                            : "Setup.AccessibilityStartupAction"
                        : "Setup.AccessibilityNext");
            _continueButton.AccessibleDefaultActionDescription = _continueButton.AccessibleDescription;
            _stepIndicator.UpdateAccessibility(
                T("Setup.AccessibilityProgress", _step + 1, 6));
            _viewSelector.AccessibleName = T("Setup.ViewSelectorLabel");
            _viewSelector.AccessibleDescription = string.Join(
                " ",
                T("Setup.ViewSelectorDescription"),
                _viewMode == SetupViewMode.Accessible
                    ? T("Setup.ViewAccessible")
                    : T("Setup.ViewStandard"));

            if (IsHandleCreated)
            {
                AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
            }
        }

        private static string QuoteProcessArgument(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private void UpdateText()
        {
            Text = T("Setup.WindowTitle");
            _welcomeLabel.Text = T("Setup.WelcomeTitle");
            _viewSelector.UpdateContent(
                T("Setup.ViewStandard"),
                T("Setup.ViewAccessible"));
            _backButton.Text = T("Setup.Back");
            _skipButton.Text = T("Setup.StartupSkip");
            _backButton.Visible = _step > 0 && !_startupOnly;
            _skipButton.Visible = _step == 4 && _startupState != SetupStartupState.Ready;

            if (_headingLabel != null)
            {
                _headingLabel.Text = T(_step switch
                {
                    0 => "Setup.LanguageTitle",
                    1 => "Setup.AppearanceTitle",
                    2 => "Setup.ShortcutsTitle",
                    3 => "Setup.UsageTitle",
                    4 => "Setup.StartupTitle",
                    _ => "Setup.CompleteTitle"
                });
            }

            if (_descriptionLabel != null)
            {
                string descriptionKey = _step switch
                {
                    0 => "Setup.LanguageBody",
                    1 => "Setup.AppearanceBody",
                    2 => "Setup.ShortcutsBody",
                    3 => _practiceContext != null ? "Setup.UsageBodyLive" : "Setup.UsageBody",
                    4 => "Setup.StartupBody",
                    _ => "Setup.CompleteBody"
                };
                _descriptionLabel.Text = _step == 3
                    ? T(descriptionKey, KeyLabel(_enableKey))
                    : T(descriptionKey);
            }

            if (_themeSectionLabel != null)
            {
                _themeSectionLabel.Text = T("Setup.ThemeSection");
            }

            UpdateStartupServiceContent();
        }

        private void ApplyVisuals()
        {
            SuspendLayout();
            try
            {
                BackColor = _palette.Border;
                ForeColor = _palette.Text;
                _root.BackColor = _palette.MenuBackground;
                _header.BackColor = _palette.MenuBackground;
                _headerDivider.BackColor = _palette.Border;
                _contentHost.BackColor = _palette.MenuBackground;
                WindowChrome.TrySetDarkScrollBars(_contentHost, _useDarkTheme);
                _footer.BackColor = _palette.MenuBackground;
                _welcomeLabel.BackColor = _palette.MenuBackground;
                _welcomeLabel.ForeColor = _palette.Text;

                ApplyLabel(_headingLabel, 15.5f, FontStyle.Bold, _palette.Text);
                ApplyLabel(_descriptionLabel, 10.2f, FontStyle.Regular, _palette.SecondaryText);
                ApplyLabel(_themeSectionLabel, 10.2f, FontStyle.Bold, _palette.Text);

                _backButton.Font = SetupFont(10.5f, FontStyle.Regular);
                _skipButton.Font = SetupFont(9.2f, FontStyle.Regular);
                _continueButton.Font = SetupFont(10.5f, FontStyle.Bold);
                _backButton.ApplyTheme(_palette);
                _skipButton.ApplyTheme(_palette);
                _continueButton.ApplyTheme(_palette, emphasis: true);
                _continueButton.SetProminentHover(
                    ControlDrawing.Blend(_palette.Accent, _palette.Text, 54),
                    ControlDrawing.Blend(_palette.Accent, _palette.Text, 118));
                _stepIndicator.ApplyTheme(_palette);
                _viewSelector.ApplyTheme(_palette, FontScale);
                foreach (SetupChoiceCard card in _cards)
                {
                    card.ApplyTheme(_palette, FontScale);
                }
                _enableHotkeyRow?.ApplyTheme(_palette, FontScale);
                foreach (SetupUsageTile tile in _usageTiles)
                {
                    tile.ApplyTheme(_palette, FontScale);
                }
                _startupServiceTile?.ApplyTheme(_palette, FontScale);
                _completionTile?.ApplyTheme(_palette, FontScale);
                _completionTile?.UpdateContent(
                    T("Setup.CompleteCardTitle"),
                    T("Setup.CompleteTrayHint"));
                UpdateHotkeyRows();
                UpdateStartupServiceContent();

                WindowChrome.TrySetDarkTitleBar(this, _useDarkTheme);
                Invalidate(true);
            }
            finally
            {
                ResumeLayout(performLayout: true);
            }

            UpdateResponsiveLayout(rebuildContent: false);
        }

        private void RunWaveTransition(Action update)
        {
            bool animate = !_captureMode &&
                AccessibilityPreferences.AnimationsEnabled &&
                Visible &&
                IsHandleCreated &&
                _root.Width > 0 &&
                _root.Height > 0;
            _transitionOverlay.Finish();
            if (!animate)
            {
                update();
                return;
            }

            Bitmap before = CaptureRootFrame();
            _transitionOverlay.Hold(before);
            update();
            _root.PerformLayout();
            Bitmap after = CaptureRootFrame();
            _transitionOverlay.Reveal(after);
        }

        private Bitmap CaptureRootFrame()
        {
            var bitmap = new Bitmap(
                Math.Max(1, _root.Width),
                Math.Max(1, _root.Height),
                PixelFormat.Format32bppPArgb);
            _root.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            return bitmap;
        }

        private void ApplyLabel(Label? label, float size, FontStyle style, Color color)
        {
            if (label == null)
            {
                return;
            }

            label.BackColor = _palette.MenuBackground;
            label.ForeColor = color;
            label.Font = SetupFont(size, style);
        }

        private Label NewLabel() => new()
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty,
            AccessibleRole = AccessibleRole.StaticText
        };

        private float PreferenceFontScale => _fontSize switch
        {
            FontLarge => 1.14f,
            FontExtraLarge => 1.28f,
            _ => 1f
        };

        private float AccessibleCanvasScale
        {
            get
            {
                if (_viewMode != SetupViewMode.Accessible)
                {
                    return 1f;
                }

                float widthScale = Math.Max(
                    1f,
                    ClientSize.Width / (float)AccessibleReferenceWidth);
                float heightScale = Math.Max(
                    1f,
                    ClientSize.Height / (float)AccessibleReferenceHeight);
                float availableScale = Math.Min(widthScale, heightScale);
                return Math.Clamp(
                    1f + ((availableScale - 1f) * 0.75f),
                    1f,
                    1.55f);
            }
        }

        private float FontScale
        {
            get
            {
                if (_viewMode != SetupViewMode.Accessible)
                {
                    return PreferenceFontScale;
                }

                float requestedScale = Math.Min(
                    2.25f,
                    Math.Max(
                        AccessibleFontScale * AccessibleCanvasScale,
                        _captureWindowsTextScale ?? AccessibilityPreferences.WindowsTextScale));
                float heightCapacity = Math.Clamp(
                    AccessibleFontScale +
                    (Math.Max(0, ClientSize.Height - 900) / 540f * 0.8f),
                    AccessibleFontScale,
                    2.25f);
                return Math.Min(requestedScale, heightCapacity);
            }
        }

        private float HeaderTitleFontSize => FontScale >= 1.8f ? 16f : 20f;

        private float LayoutScale =>
            Math.Min(1.45f, 1f + ((FontScale - 1f) * 0.75f));

        private int LayoutHeight(int logicalHeight) =>
            (int)Math.Ceiling(logicalHeight * LayoutScale);

        private Font SetupFont(float size, FontStyle style)
        {
            float scaledSize = Math.Max(8f, size * FontScale);
            var key = (scaledSize, style);
            if (_ownedFonts.TryGetValue(key, out Font? font))
            {
                return font;
            }

            font = new Font("Segoe UI", scaledSize, style);
            _ownedFonts.Add(key, font);
            return font;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopLivePractice();
                _fittedHeaderFont?.Dispose();
                _fittedHeaderFont = null;
                foreach (Font font in _ownedFonts.Values)
                {
                    font.Dispose();
                }

                _ownedFonts.Clear();
            }

            base.Dispose(disposing);
        }

        private string T(string key, params object[] args) => UiText.Get(_language, key, args);
    }

    private sealed class SetupCompletionTile : Control
    {
        private readonly System.Windows.Forms.Timer _animationTimer;
        private readonly long _animationStarted = Environment.TickCount64;
        private ThemePalette _palette;
        private float _fontScale = 1f;
        private string _title = string.Empty;
        private string _instruction = string.Empty;

        internal SetupCompletionTile()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            TabStop = false;
            AccessibleRole = AccessibleRole.StaticText;
            _animationTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _animationTimer.Tick += (_, _) =>
            {
                Invalidate();
                if (Progress >= 1f)
                {
                    _animationTimer.Stop();
                }
            };
            if (AccessibilityPreferences.AnimationsEnabled)
            {
                _animationTimer.Start();
            }
        }

        internal bool AnimationComplete { get; set; }

        private float Progress => AnimationComplete || !AccessibilityPreferences.AnimationsEnabled
            ? 1f
            : Math.Clamp((Environment.TickCount64 - _animationStarted) / 900f, 0f, 1f);

        internal void ApplyTheme(ThemePalette palette, float fontScale)
        {
            _palette = palette;
            _fontScale = fontScale;
            BackColor = palette.MenuBackground;
            ForeColor = palette.Text;
            Invalidate();
        }

        internal void UpdateContent(string title, string instruction)
        {
            bool changed = _title != title || _instruction != instruction;
            _title = title;
            _instruction = instruction;
            AccessibleName = title;
            AccessibleDescription = instruction;
            if (changed && IsHandleCreated)
            {
                AccessibilityNotifyClients(AccessibleEvents.DescriptionChange, -1);
            }
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationTimer.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using SolidBrush brush = new(ControlDrawing.EffectiveBackColor(this));
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 80 || Height < 80)
            {
                return;
            }

            GraphicsState paintState = e.Graphics.Save();
            try
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                Rectangle surface = Rectangle.Inflate(ClientRectangle, -1, -1);
                using GraphicsPath surfacePath = ControlDrawing.RoundedRect(surface, 14);
                using SolidBrush surfaceBrush = new(_palette.ControlBackground);
                using Pen surfaceBorder = new(_palette.Border, Math.Max(1f, DeviceDpi / 96f));
                e.Graphics.FillPath(surfaceBrush, surfacePath);
                e.Graphics.DrawPath(surfaceBorder, surfacePath);

                float progress = Progress;
                float renderScale = Math.Min(
                    _fontScale,
                    Math.Clamp(Height / 230f, 1f, 1.55f));
                int checkSize = Math.Clamp(Height * 24 / 100, 72, 180);
                int checkTop = Math.Max(14, Height * 5 / 100);
                Rectangle checkBounds = new(
                    (Width - checkSize) / 2,
                    checkTop,
                    checkSize,
                    checkSize);
                using SolidBrush checkBackground = new(ControlDrawing.Blend(
                    _palette.ControlBackground,
                    _palette.Accent,
                    42));
                e.Graphics.FillEllipse(checkBackground, checkBounds);
                using Pen ringPen = new(_palette.Accent, Math.Max(3f, DeviceDpi / 32f))
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                e.Graphics.DrawArc(
                    ringPen,
                    Rectangle.Inflate(checkBounds, -7, -7),
                    -90f,
                    360f * Math.Min(1f, progress / 0.62f));

                if (progress > 0.42f)
                {
                    float checkProgress = Math.Clamp((progress - 0.42f) / 0.46f, 0f, 1f);
                    PointF first = new(
                        checkBounds.Left + (checkBounds.Width * 27 / 100f),
                        checkBounds.Top + (checkBounds.Height * 52 / 100f));
                    PointF middle = new(
                        checkBounds.Left + (checkBounds.Width * 44 / 100f),
                        checkBounds.Top + (checkBounds.Height * 69 / 100f));
                    PointF last = new(
                        checkBounds.Left + (checkBounds.Width * 74 / 100f),
                        checkBounds.Top + (checkBounds.Height * 35 / 100f));
                    using Pen checkPen = new(_palette.Accent, Math.Max(4f, DeviceDpi / 24f))
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round,
                        LineJoin = LineJoin.Round
                    };
                    if (checkProgress <= 0.42f)
                    {
                        float local = checkProgress / 0.42f;
                        e.Graphics.DrawLine(
                            checkPen,
                            first,
                            new PointF(
                                first.X + ((middle.X - first.X) * local),
                                first.Y + ((middle.Y - first.Y) * local)));
                    }
                    else
                    {
                        e.Graphics.DrawLine(checkPen, first, middle);
                        float local = (checkProgress - 0.42f) / 0.58f;
                        e.Graphics.DrawLine(
                            checkPen,
                            middle,
                            new PointF(
                                middle.X + ((last.X - middle.X) * local),
                                middle.Y + ((last.Y - middle.Y) * local)));
                    }
                }

                using Font titleFont = new("Segoe UI", Math.Max(10f, 14f * renderScale), FontStyle.Bold);
                using Font bodyFont = new("Segoe UI", Math.Max(8f, 9.5f * renderScale), FontStyle.Regular);
                const TextFormatFlags textFlags =
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.Top |
                    TextFormatFlags.WordBreak |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.NoPrefix;
                int horizontalPadding = 28;
                int titleTop = checkBounds.Bottom + 18;
                int textWidth = Math.Max(1, Width - (horizontalPadding * 2));
                int titleHeight = TextRenderer.MeasureText(
                    e.Graphics,
                    _title,
                    titleFont,
                    new Size(textWidth, Height),
                    textFlags).Height + 2;
                Rectangle titleBounds = new(
                    horizontalPadding,
                    titleTop,
                    textWidth,
                    titleHeight);
                int instructionInset = 48;
                Rectangle instructionBounds = new(
                    instructionInset,
                    titleBounds.Bottom + 6,
                    Math.Max(1, Width - (instructionInset * 2)),
                    Math.Max(
                        1,
                        Height -
                        titleBounds.Bottom -
                        18));
                TextRenderer.DrawText(
                    e.Graphics,
                    _title,
                    titleFont,
                    titleBounds,
                    _palette.Text,
                    textFlags);
                TextRenderer.DrawText(
                    e.Graphics,
                    _instruction,
                    bodyFont,
                    instructionBounds,
                    _palette.SecondaryText,
                    textFlags);
            }
            finally
            {
                e.Graphics.Restore(paintState);
            }
        }
    }

    private sealed class SetupStartupServiceTile : Control
    {
        private readonly System.Windows.Forms.Timer _animationTimer;
        private ThemePalette _palette;
        private float _fontScale = 1f;
        private string _title = string.Empty;
        private string _description = string.Empty;
        private string _autostartBenefit = string.Empty;
        private string _elevatedBenefit = string.Empty;
        private string _approvalBenefit = string.Empty;
        private string _status = string.Empty;
        private SetupStartupState _state;
        private long _stateChangedAt = Environment.TickCount64;
        private float _completionProgress = -1f;

        internal SetupStartupServiceTile()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            TabStop = false;
            AccessibleRole = AccessibleRole.StaticText;
            _animationTimer = new System.Windows.Forms.Timer { Interval = 40 };
            _animationTimer.Tick += (_, _) =>
            {
                if (IsBusy)
                {
                    Invalidate();
                }
            };
        }

        private bool IsBusy =>
            _state is SetupStartupState.Checking or
                SetupStartupState.AwaitingApproval or
                SetupStartupState.Installing or
                SetupStartupState.Verifying;

        internal void ApplyTheme(ThemePalette palette, float fontScale)
        {
            _palette = palette;
            _fontScale = fontScale;
            BackColor = palette.MenuBackground;
            ForeColor = palette.Text;
            Invalidate();
        }

        internal void UpdateContent(
            string title,
            string description,
            string autostartBenefit,
            string elevatedBenefit,
            string approvalBenefit,
            string status,
            SetupStartupState state)
        {
            bool stateChanged = _state != state;
            bool statusChanged = _status != status;
            _title = title;
            _description = description;
            _autostartBenefit = autostartBenefit;
            _elevatedBenefit = elevatedBenefit;
            _approvalBenefit = approvalBenefit;
            _status = status;
            if (stateChanged)
            {
                _stateChangedAt = Environment.TickCount64;
                if (state != SetupStartupState.Ready)
                {
                    _completionProgress = -1f;
                }
            }
            _state = state;
            if (IsBusy && AccessibilityPreferences.AnimationsEnabled)
            {
                _animationTimer.Start();
            }
            else
            {
                _animationTimer.Stop();
            }
            AccessibleName = title;
            AccessibleDescription = string.Join(
                " ",
                description,
                autostartBenefit,
                elevatedBenefit,
                approvalBenefit,
                status);
            if ((stateChanged || statusChanged) && IsHandleCreated)
            {
                AccessibilityNotifyClients(AccessibleEvents.DescriptionChange, -1);
                if (state is SetupStartupState.Ready or SetupStartupState.Declined or SetupStartupState.Failed)
                {
                    AccessibilityNotifyClients(AccessibleEvents.SystemAlert, -1);
                }
            }
            Invalidate();
        }

        internal async Task CompleteProgressAsync()
        {
            float startProgress = Math.Clamp(EstimatedProgress, 0f, 0.98f);
            if (!AccessibilityPreferences.AnimationsEnabled)
            {
                _completionProgress = 1f;
                Invalidate();
                return;
            }

            const int durationMilliseconds = 680;
            long started = Environment.TickCount64;
            while (!IsDisposed)
            {
                float elapsed = Math.Clamp(
                    (Environment.TickCount64 - started) / (float)durationMilliseconds,
                    0f,
                    1f);
                float eased = 1f - MathF.Pow(1f - elapsed, 3f);
                _completionProgress = startProgress + ((1f - startProgress) * eased);
                Invalidate();
                Update();
                if (elapsed >= 1f)
                {
                    break;
                }

                await Task.Delay(16);
            }

            _completionProgress = 1f;
            Invalidate();
            Update();
            await Task.Delay(120);
        }

        internal void SetCompletionProgressForCapture()
        {
            _completionProgress = 1f;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationTimer.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using SolidBrush brush = new(ControlDrawing.EffectiveBackColor(this));
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 80 || Height < 80)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            Rectangle surface = Rectangle.Inflate(ClientRectangle, -1, -1);
            using GraphicsPath surfacePath = ControlDrawing.RoundedRect(
                surface,
                14);
            using SolidBrush surfaceBrush = new(_palette.ControlBackground);
            using Pen surfaceBorder = new(_palette.Border, Math.Max(1f, DeviceDpi / 96f));
            e.Graphics.FillPath(surfaceBrush, surfacePath);
            e.Graphics.DrawPath(surfaceBorder, surfacePath);

            float renderScale = Math.Min(
                _fontScale,
                Math.Clamp(Height / 260f, 1f, 1.45f));
            float visualScale = Math.Clamp(renderScale / 1.45f, 0.86f, 1.45f);
            int padding = (int)Math.Round(24f * visualScale);
            int iconSize = (int)Math.Round(58f * visualScale);
            Rectangle iconBounds = new(padding, padding, iconSize, iconSize);
            DrawShield(e.Graphics, iconBounds);

            int textLeft = iconBounds.Right + 20;
            int textRight = Width - padding;
            int textWidth = Math.Max(120, textRight - textLeft);
            using Font titleFont = new("Segoe UI", Math.Max(9f, 13f * renderScale), FontStyle.Bold);
            using Font bodyFont = new("Segoe UI", Math.Max(8f, 9.5f * renderScale), FontStyle.Regular);
            using Font bodyBoldFont = new("Segoe UI Semibold", Math.Max(8f, 9.5f * renderScale), FontStyle.Bold);
            using Font benefitFont = new("Segoe UI", Math.Max(8f, 9.1f * renderScale), FontStyle.Regular);
            using Font statusFont = new("Segoe UI Semibold", Math.Max(8f, 9.2f * renderScale), FontStyle.Bold);
            const TextFormatFlags titleFlags =
                TextFormatFlags.Left |
                TextFormatFlags.Top |
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoPadding |
                TextFormatFlags.NoPrefix;
            int titleTop = padding - 2;
            int titleHeight = TextRenderer.MeasureText(
                e.Graphics,
                _title,
                titleFont,
                new Size(textWidth, Height),
                titleFlags).Height + 2;
            TextRenderer.DrawText(
                e.Graphics,
                _title,
                titleFont,
                new Rectangle(textLeft, titleTop, textWidth, titleHeight),
                _palette.Text,
                titleFlags);
            int descriptionTop = titleTop + titleHeight + 2;
            int descriptionHeight = MeasureRichDescriptionHeight(
                e.Graphics,
                _description,
                textWidth,
                bodyFont,
                bodyBoldFont) + 2;
            DrawRichDescription(
                e.Graphics,
                _description,
                new Rectangle(textLeft, descriptionTop, textWidth, descriptionHeight),
                bodyFont,
                bodyBoldFont,
                _palette.SecondaryText);

            int statusHeight = Math.Max(
                52,
                TextRenderer.MeasureText(
                    e.Graphics,
                    _status,
                    statusFont,
                    new Size(Width - (padding * 2), Height),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height +
                18);
            Rectangle statusBounds = new(
                padding,
                Height - padding - statusHeight,
                Width - (padding * 2),
                statusHeight);
            int benefitsTop = Math.Max(
                iconBounds.Bottom,
                descriptionTop + descriptionHeight) +
                10;
            int benefitAreaHeight = Math.Max(
                1,
                statusBounds.Top - benefitsTop - 6);
            string[] benefits = [_autostartBenefit, _elevatedBenefit, _approvalBenefit];
            int benefitTextWidth = Math.Max(
                1,
                Width -
                (padding * 2) -
                21);
            int[] benefitHeights = benefits
                .Select(benefit => Math.Max(
                    24,
                    MeasureWrappedTextHeight(
                        e.Graphics,
                        benefit,
                        benefitFont,
                        benefitTextWidth) + 3))
                .ToArray();
            int measuredBenefitHeight = benefitHeights.Sum();
            bool useBenefitColumns =
                Width >= 1000 &&
                benefitAreaHeight < measuredBenefitHeight;
            if (useBenefitColumns)
            {
                const int columnGap = 18;
                int availableWidth = Width - (padding * 2) - (columnGap * 2);
                int columnWidth = Math.Max(1, availableWidth / 3);
                float columnFontSize = benefitFont.Size;
                while (columnFontSize > 8f)
                {
                    using Font candidateFont = new(
                        "Segoe UI",
                        columnFontSize,
                        FontStyle.Regular);
                    bool fits = benefits.All(benefit =>
                        MeasureWrappedTextHeight(
                            e.Graphics,
                            benefit,
                            candidateFont,
                            Math.Max(1, columnWidth - 21)) <= benefitAreaHeight);
                    if (fits)
                    {
                        break;
                    }

                    columnFontSize = Math.Max(8f, columnFontSize - 0.5f);
                }

                using Font columnFont = new(
                    "Segoe UI",
                    columnFontSize,
                    FontStyle.Regular);
                for (int index = 0; index < benefits.Length; index++)
                {
                    int columnLeft = padding + (index * (columnWidth + columnGap));
                    int width = index == benefits.Length - 1
                        ? Width - padding - columnLeft
                        : columnWidth;
                    DrawBenefit(
                        e.Graphics,
                        benefits[index],
                        new Rectangle(
                            columnLeft,
                            benefitsTop,
                            width,
                            benefitAreaHeight),
                        columnFont);
                }
            }
            else
            {
                int extraBenefitSpace = Math.Max(0, benefitAreaHeight - measuredBenefitHeight);
                int benefitTop = benefitsTop;
                for (int index = 0; index < benefits.Length; index++)
                {
                    int rowHeight = benefitHeights[index] +
                        (index < extraBenefitSpace ? 1 : 0);
                    DrawBenefit(e.Graphics, benefits[index], new Rectangle(
                        padding,
                        benefitTop,
                        Width - (padding * 2),
                        rowHeight),
                        benefitFont);
                    benefitTop += rowHeight;
                }
            }

            DrawStatus(e.Graphics, statusBounds, statusFont);
        }

        private void DrawShield(Graphics graphics, Rectangle bounds)
        {
            Rectangle shieldBounds = Rectangle.Inflate(bounds, -6, -4);
            using GraphicsPath shield = new();
            shield.AddPolygon(
            [
                new Point(shieldBounds.Left + 1, shieldBounds.Top + 8),
                new Point(shieldBounds.Left + (shieldBounds.Width / 2), shieldBounds.Top),
                new Point(shieldBounds.Right - 1, shieldBounds.Top + 8),
                new Point(shieldBounds.Right - 3, shieldBounds.Top + (shieldBounds.Height * 3 / 5)),
                new Point(shieldBounds.Left + (shieldBounds.Width / 2), shieldBounds.Bottom),
                new Point(shieldBounds.Left + 3, shieldBounds.Top + (shieldBounds.Height * 3 / 5))
            ]);

            GraphicsState state = graphics.Save();
            graphics.SetClip(shield);
            int middleX = shieldBounds.Left + (shieldBounds.Width / 2);
            int middleY = shieldBounds.Top + (shieldBounds.Height / 2);
            using SolidBrush blue = new(Color.FromArgb(20, 105, 218));
            using SolidBrush yellow = new(Color.FromArgb(255, 190, 24));
            graphics.FillRectangle(
                blue,
                shieldBounds.Left,
                shieldBounds.Top,
                middleX - shieldBounds.Left,
                middleY - shieldBounds.Top);
            graphics.FillRectangle(
                yellow,
                middleX,
                shieldBounds.Top,
                shieldBounds.Right - middleX,
                middleY - shieldBounds.Top);
            graphics.FillRectangle(
                yellow,
                shieldBounds.Left,
                middleY,
                middleX - shieldBounds.Left,
                shieldBounds.Bottom - middleY);
            graphics.FillRectangle(
                blue,
                middleX,
                middleY,
                shieldBounds.Right - middleX,
                shieldBounds.Bottom - middleY);
            graphics.Restore(state);

            using Pen shieldBorder = new(
                _palette.MenuBackground.GetBrightness() < 0.5f
                    ? Color.FromArgb(205, 214, 224)
                    : Color.FromArgb(142, 151, 162),
                Math.Max(2.4f, DeviceDpi / 38f))
            {
                LineJoin = LineJoin.Round
            };
            graphics.DrawPath(shieldBorder, shield);
        }

        private void DrawBenefit(Graphics graphics, string text, Rectangle bounds, Font font)
        {
            int dotSize = 9;
            int textWidth = Math.Max(1, bounds.Width - dotSize - 12);
            int textHeight = MeasureWrappedTextHeight(graphics, text, font, textWidth);
            Rectangle dot = new(
                bounds.Left,
                bounds.Top + ((bounds.Height - dotSize) / 2),
                dotSize,
                dotSize);
            using SolidBrush dotBrush = new(_palette.Accent);
            graphics.FillEllipse(dotBrush, dot);
            TextRenderer.DrawText(
                graphics,
                text,
                font,
                new Rectangle(
                    dot.Right + 12,
                    bounds.Top + Math.Max(0, (bounds.Height - textHeight) / 2),
                    textWidth,
                    textHeight),
                _palette.Text,
                TextFormatFlags.Left |
                TextFormatFlags.Top |
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoPadding |
                TextFormatFlags.NoPrefix);
        }

        private static void DrawRichDescription(
            Graphics graphics,
            string text,
            Rectangle bounds,
            Font regularFont,
            Font boldFont,
            Color color)
        {
            _ = LayoutRichDescription(
                graphics,
                text,
                bounds.Width,
                regularFont,
                boldFont,
                (word, font, point) => TextRenderer.DrawText(
                    graphics,
                    word,
                    font,
                    new Point(bounds.Left + point.X, bounds.Top + point.Y),
                    color,
                    TextFormatFlags.Left |
                    TextFormatFlags.Top |
                    TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.NoPrefix));
        }

        private static int MeasureRichDescriptionHeight(
            Graphics graphics,
            string text,
            int width,
            Font regularFont,
            Font boldFont) =>
            LayoutRichDescription(graphics, text, width, regularFont, boldFont, drawWord: null);

        private static int LayoutRichDescription(
            Graphics graphics,
            string text,
            int width,
            Font regularFont,
            Font boldFont,
            Action<string, Font, Point>? drawWord)
        {
            const TextFormatFlags flags =
                TextFormatFlags.Left |
                TextFormatFlags.Top |
                TextFormatFlags.SingleLine |
                TextFormatFlags.NoPadding |
                TextFormatFlags.NoPrefix;
            int x = 0;
            int y = 0;
            int lineHeight = Math.Max(
                TextRenderer.MeasureText(graphics, "Ag", regularFont, Size.Empty, flags).Height,
                TextRenderer.MeasureText(graphics, "Ag", boldFont, Size.Empty, flags).Height) + 3;
            int spaceWidth = TextRenderer.MeasureText(graphics, " ", regularFont, Size.Empty, flags).Width;

            string[] paragraphs = text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');
            for (int paragraphIndex = 0; paragraphIndex < paragraphs.Length; paragraphIndex++)
            {
                string[] words = paragraphs[paragraphIndex].Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries);
                foreach (string word in words)
                {
                    string comparableWord = word.TrimEnd('.', ',', ';', ':');
                    Font font = comparableWord.Equals("QuickZoom.exe", StringComparison.OrdinalIgnoreCase)
                        ? boldFont
                        : regularFont;
                    Size wordSize = TextRenderer.MeasureText(graphics, word, font, Size.Empty, flags);
                    if (x > 0 && x + wordSize.Width > width)
                    {
                        x = 0;
                        y += lineHeight;
                    }

                    drawWord?.Invoke(word, font, new Point(x, y));
                    x += wordSize.Width + spaceWidth;
                }

                if (paragraphIndex < paragraphs.Length - 1)
                {
                    x = 0;
                    y += lineHeight;
                }
            }

            return y + lineHeight;
        }

        private static int MeasureWrappedTextHeight(
            Graphics graphics,
            string text,
            Font font,
            int width)
        {
            const TextFormatFlags flags =
                TextFormatFlags.Left |
                TextFormatFlags.Top |
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoPadding |
                TextFormatFlags.NoPrefix;
            return TextRenderer.MeasureText(
                graphics,
                text,
                font,
                new Size(Math.Max(1, width), int.MaxValue),
                flags).Height;
        }

        private void DrawStatus(Graphics graphics, Rectangle bounds, Font font)
        {
            Color statusColor = _state switch
            {
                SetupStartupState.Ready => _palette.Accent,
                SetupStartupState.Failed => Color.FromArgb(239, 68, 68),
                _ => Color.FromArgb(245, 158, 11)
            };
            using GraphicsPath path = ControlDrawing.RoundedRect(
                bounds,
                10);
            using SolidBrush background = new(ControlDrawing.Blend(
                _palette.ControlBackground,
                statusColor,
                28));
            using Pen border = new(ControlDrawing.Blend(
                _palette.Border,
                statusColor,
                82),
                Math.Max(1f, DeviceDpi / 96f));
            graphics.FillPath(background, path);

            if (IsBusy)
            {
                DrawLiquidProgress(graphics, bounds, path, statusColor);
            }

            graphics.DrawPath(border, path);

            int indicatorSize = 20;
            Rectangle indicator = new(
                bounds.Left + 16,
                bounds.Top + ((bounds.Height - indicatorSize) / 2),
                indicatorSize,
                indicatorSize);
            if (IsBusy)
            {
                float angle = AccessibilityPreferences.AnimationsEnabled
                    ? (Environment.TickCount64 % 900L) * 360f / 900f
                    : 40f;
                using Pen spinner = new(statusColor, Math.Max(2f, DeviceDpi / 48f))
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                graphics.DrawArc(spinner, indicator, angle, 250f);
            }
            else if (_state == SetupStartupState.Ready)
            {
                using Pen check = new(statusColor, Math.Max(2f, DeviceDpi / 42f))
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                graphics.DrawLines(
                    check,
                    [
                        new Point(indicator.Left + 2, indicator.Top + (indicator.Height / 2)),
                        new Point(
                            indicator.Left + (indicator.Width * 2 / 5),
                            indicator.Bottom - 3),
                        new Point(indicator.Right - 1, indicator.Top + 3)
                    ]);
            }
            else
            {
                using SolidBrush dot = new(statusColor);
                graphics.FillEllipse(dot, Rectangle.Inflate(indicator, -5, -5));
            }

            TextRenderer.DrawText(
                graphics,
                _status,
                font,
                new Rectangle(
                    indicator.Right + 12,
                    bounds.Top,
                    bounds.Right - indicator.Right - 24,
                    bounds.Height),
                statusColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        }

        private float EstimatedProgress
        {
            get
            {
                if (_completionProgress >= 0f)
                {
                    return _completionProgress;
                }

                double seconds = Math.Max(0, Environment.TickCount64 - _stateChangedAt) / 1000d;
                return _state switch
                {
                    SetupStartupState.Checking =>
                        0.06f + (0.09f * (float)(1d - Math.Exp(-seconds / 0.45d))),
                    SetupStartupState.AwaitingApproval =>
                        0.16f + (0.20f * (float)(1d - Math.Exp(-seconds / 2.2d))),
                    SetupStartupState.Installing =>
                        0.42f + (0.49f * (float)(1d - Math.Exp(-seconds / 2.2d))),
                    SetupStartupState.Verifying =>
                        0.91f + (0.075f * (float)(1d - Math.Exp(-seconds / 2.8d))),
                    SetupStartupState.Ready => 1f,
                    _ => 0f
                };
            }
        }

        private void DrawLiquidProgress(
            Graphics graphics,
            Rectangle bounds,
            GraphicsPath clipPath,
            Color statusColor)
        {
            float progress = Math.Clamp(EstimatedProgress, 0f, 1f);
            float leadingX = bounds.Left + (bounds.Width * progress);
            float phase = AccessibilityPreferences.AnimationsEnabled
                ? (Environment.TickCount64 % 1800L) * MathF.Tau / 1800f
                : 0f;
            float waveAmplitude = Math.Max(2f, DeviceDpi / 32f);
            int waveStep = Math.Max(3, DeviceDpi / 32);

            GraphicsState clipState = graphics.Save();
            graphics.SetClip(clipPath);
            Color liquidStart = ControlDrawing.Blend(
                _palette.ControlBackground,
                statusColor,
                48);
            Color liquidFront = ControlDrawing.Blend(
                _palette.ControlBackground,
                statusColor,
                104);
            using LinearGradientBrush liquidBrush = new(
                bounds,
                liquidStart,
                liquidFront,
                LinearGradientMode.Horizontal);

            if (progress >= 0.999f)
            {
                graphics.FillPath(liquidBrush, clipPath);
            }
            else
            {
                using GraphicsPath backWave = CreateLiquidPath(
                    bounds,
                    leadingX - (waveAmplitude * 0.9f),
                    phase + 1.45f,
                    waveAmplitude * 0.85f,
                    waveStep);
                using SolidBrush backWaveBrush = new(Color.FromArgb(
                    30,
                    ControlDrawing.Blend(statusColor, _palette.Text, 38)));
                graphics.FillPath(backWaveBrush, backWave);

                using GraphicsPath liquidPath = CreateLiquidPath(
                    bounds,
                    leadingX,
                    phase,
                    waveAmplitude,
                    waveStep);
                graphics.FillPath(liquidBrush, liquidPath);

                using Pen surfacePen = new(
                    Color.FromArgb(
                        112,
                        ControlDrawing.Blend(statusColor, _palette.Text, 64)),
                    Math.Max(1f, DeviceDpi / 96f))
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };
                using GraphicsPath surface = CreateLiquidSurface(
                    bounds,
                    leadingX,
                    phase,
                    waveAmplitude,
                    waveStep);
                graphics.DrawPath(surfacePen, surface);
            }

            graphics.Restore(clipState);
        }

        private static GraphicsPath CreateLiquidPath(
            Rectangle bounds,
            float leadingX,
            float phase,
            float amplitude,
            int step)
        {
            GraphicsPath path = new();
            path.StartFigure();
            path.AddLine(bounds.Left, bounds.Top, leadingX, bounds.Top);
            AddLiquidSurface(path, bounds, leadingX, phase, amplitude, step);
            path.AddLine(path.GetLastPoint(), new PointF(bounds.Left, bounds.Bottom));
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath CreateLiquidSurface(
            Rectangle bounds,
            float leadingX,
            float phase,
            float amplitude,
            int step)
        {
            GraphicsPath path = new();
            path.StartFigure();
            AddLiquidSurface(path, bounds, leadingX, phase, amplitude, step);
            return path;
        }

        private static void AddLiquidSurface(
            GraphicsPath path,
            Rectangle bounds,
            float leadingX,
            float phase,
            float amplitude,
            int step)
        {
            for (int y = bounds.Top; y <= bounds.Bottom; y += step)
            {
                float vertical = y - bounds.Top;
                float wave =
                    (MathF.Sin(phase + (vertical * 0.17f)) * amplitude * 0.64f) +
                    (MathF.Sin((-phase * 0.72f) + (vertical * 0.071f)) * amplitude * 0.36f);
                PointF next = new(
                    Math.Clamp(leadingX + wave, bounds.Left, bounds.Right),
                    y);
                if (path.PointCount == 0)
                {
                    path.AddLine(next, next);
                }
                else
                {
                    path.AddLine(path.GetLastPoint(), next);
                }
            }

            if (path.GetLastPoint().Y < bounds.Bottom)
            {
                float vertical = bounds.Height;
                float wave =
                    (MathF.Sin(phase + (vertical * 0.17f)) * amplitude * 0.64f) +
                    (MathF.Sin((-phase * 0.72f) + (vertical * 0.071f)) * amplitude * 0.36f);
                path.AddLine(
                    path.GetLastPoint(),
                    new PointF(
                        Math.Clamp(leadingX + wave, bounds.Left, bounds.Right),
                        bounds.Bottom));
            }
        }
    }

    private static void DrawWindowsLogo(Graphics graphics, Color color, Rectangle bounds)
    {
        int gap = Math.Max(2, bounds.Width / 10);
        int halfWidth = (bounds.Width - gap) / 2;
        int halfHeight = (bounds.Height - gap) / 2;
        using SolidBrush brush = new(color);
        graphics.FillRectangle(brush, bounds.Left, bounds.Top + 1, halfWidth, halfHeight);
        graphics.FillRectangle(brush, bounds.Left + halfWidth + gap, bounds.Top, halfWidth, halfHeight + 1);
        graphics.FillRectangle(brush, bounds.Left, bounds.Top + halfHeight + gap, halfWidth, halfHeight);
        graphics.FillRectangle(
            brush,
            bounds.Left + halfWidth + gap,
            bounds.Top + halfHeight + gap - 1,
            halfWidth,
            halfHeight + 1);
    }

    private sealed class SetupUsageTile : Control
    {
        private readonly SetupUsageKind _kind;
        private readonly string _title;
        private readonly string _description;
        private readonly string _activationKey;
        private readonly string _orText;
        private readonly System.Windows.Forms.Timer _timer;
        private ThemePalette _palette;
        private float _fontScale = 1f;

        internal SetupUsageTile(
            SetupUsageKind kind,
            string title,
            string description,
            string activationKey,
            string orText)
        {
            _kind = kind;
            _title = title;
            _description = description;
            _activationKey = activationKey;
            _orText = orText;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            TabStop = false;
            AccessibleRole = AccessibleRole.StaticText;
            AccessibleName = title;
            AccessibleDescription = description;
            _timer = new System.Windows.Forms.Timer { Interval = 32 };
            _timer.Tick += (_, _) => Invalidate();
            if (AccessibilityPreferences.AnimationsEnabled)
            {
                _timer.Start();
            }
        }

        internal void ApplyTheme(ThemePalette palette, float fontScale)
        {
            _palette = palette;
            _fontScale = fontScale;
            BackColor = palette.MenuBackground;
            ForeColor = palette.Text;
            Invalidate();
        }

        internal bool ShowWindowsLogo { get; set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using SolidBrush brush = new(ControlDrawing.EffectiveBackColor(this));
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 40 || Height < 40)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            Rectangle surface = Rectangle.Inflate(ClientRectangle, -1, -1);
            using GraphicsPath surfacePath = ControlDrawing.RoundedRect(surface, 14);
            using SolidBrush surfaceBrush = new(_palette.ControlBackground);
            using Pen surfaceBorder = new(_palette.Border, Math.Max(1f, DeviceDpi / 96f));
            e.Graphics.FillPath(surfaceBrush, surfacePath);
            e.Graphics.DrawPath(surfaceBorder, surfacePath);

            int padding = Math.Max(18, Width / 50);
            float visualScale = Math.Min(
                Math.Clamp(_fontScale / 1.35f, 1f, 1.55f),
                Math.Clamp((Height - 20) / 68f, 0.78f, 1.55f));
            int illustrationWidth = Math.Clamp(
                Width * 30 / 100,
                (int)Math.Round(330f * visualScale),
                (int)Math.Round(520f * visualScale));
            int textWidth = Math.Max(180, Width - (padding * 3) - illustrationWidth);
            using Font titleFont = new("Segoe UI", Math.Max(8f, 10.8f * _fontScale), FontStyle.Bold);
            using Font bodyFont = new("Segoe UI", Math.Max(7.5f, 8.8f * _fontScale), FontStyle.Regular);
            using Font keyFont = new("Segoe UI", Math.Max(8f, 9.4f * _fontScale), FontStyle.Bold);
            const TextFormatFlags textFlags =
                TextFormatFlags.Left |
                TextFormatFlags.Top |
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoPadding |
                TextFormatFlags.NoPrefix;
            string balancedDescription = BalanceWrappedText(
                e.Graphics,
                _description,
                bodyFont,
                textWidth);
            int titleHeight = TextRenderer.MeasureText(
                e.Graphics,
                _title,
                titleFont,
                new Size(textWidth, Height),
                textFlags).Height + 2;
            int descriptionHeight = TextRenderer.MeasureText(
                e.Graphics,
                balancedDescription,
                bodyFont,
                new Size(textWidth, Height),
                textFlags).Height;
            int titleTop = Math.Max(
                10,
                (Height - titleHeight - descriptionHeight - 4) / 2);
            Rectangle titleBounds = new(padding, titleTop, textWidth, titleHeight);
            Rectangle descriptionBounds = new(
                padding,
                titleBounds.Bottom + 2,
                textWidth,
                Math.Max(1, descriptionHeight + 2));
            TextRenderer.DrawText(
                e.Graphics,
                _title,
                titleFont,
                titleBounds,
                _palette.Text,
                textFlags);
            TextRenderer.DrawText(
                e.Graphics,
                balancedDescription,
                bodyFont,
                descriptionBounds,
                _palette.SecondaryText,
                textFlags);

            Rectangle illustration = new(
                Width - padding - illustrationWidth,
                10,
                illustrationWidth,
                Height - 20);
            int activationKeyWidth = (int)Math.Round(100f * visualScale);
            int activationKeyHeight = (int)Math.Round(56f * visualScale);
            DrawKeycap(
                e.Graphics,
                new Rectangle(
                    illustration.Left,
                    (illustration.Height - activationKeyHeight) / 2 + illustration.Top,
                    activationKeyWidth,
                    activationKeyHeight),
                _activationKey,
                keyFont,
                ShowWindowsLogo);
            TextRenderer.DrawText(
                e.Graphics,
                "+",
                titleFont,
                new Rectangle(
                    illustration.Left + activationKeyWidth + 4,
                    illustration.Top,
                    (int)Math.Round(34f * visualScale),
                    illustration.Height),
                _palette.SecondaryText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            Rectangle mouse = new(
                illustration.Left +
                activationKeyWidth +
                (int)Math.Round(42f * visualScale),
                illustration.Top +
                ((illustration.Height - (int)Math.Round(68f * visualScale)) / 2),
                (int)Math.Round(40f * visualScale),
                (int)Math.Round(68f * visualScale));
            double phase = AccessibilityPreferences.AnimationsEnabled
                ? (Environment.TickCount64 % 1200L) / 1200d
                : 0.35d;
            float pulse = (float)((Math.Sin(phase * Math.PI * 2d) + 1d) / 2d);
            DrawMouse(e.Graphics, mouse, pulse);

            int orLeft = mouse.Right + (int)Math.Round(8f * visualScale);
            int orWidth = Math.Clamp(
                TextRenderer.MeasureText(
                    e.Graphics,
                    _orText,
                    bodyFont,
                    Size.Empty,
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width + 8,
                (int)Math.Round(34f * visualScale),
                (int)Math.Round(58f * visualScale));
            TextRenderer.DrawText(
                e.Graphics,
                _orText,
                bodyFont,
                new Rectangle(orLeft, illustration.Top, orWidth, illustration.Height),
                _palette.SecondaryText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            int keyLeft = orLeft + orWidth + (int)Math.Round(8f * visualScale);
            if (_kind == SetupUsageKind.Zoom)
            {
                int keyWidth = (int)Math.Round(44f * visualScale);
                int keyHeight = (int)Math.Round(48f * visualScale);
                int keyTop = mouse.Top + ((mouse.Height - keyHeight) / 2);
                DrawKeycap(
                    e.Graphics,
                    new Rectangle(keyLeft, keyTop, keyWidth, keyHeight),
                    "+",
                    keyFont);
                DrawKeycap(
                    e.Graphics,
                    new Rectangle(
                        keyLeft + keyWidth + (int)Math.Round(8f * visualScale),
                        keyTop,
                        keyWidth,
                        keyHeight),
                    "−",
                    keyFont);
            }
            else
            {
                DrawKeycap(
                    e.Graphics,
                    new Rectangle(
                        keyLeft,
                        mouse.Top + (int)Math.Round(7f * visualScale),
                        (int)Math.Round(56f * visualScale),
                        (int)Math.Round(52f * visualScale)),
                    _kind == SetupUsageKind.Invert ? "I" : "Z",
                    keyFont);
            }
        }

        private static string BalanceWrappedText(
            Graphics graphics,
            string text,
            Font font,
            int width)
        {
            string[] words = text.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);
            if (words.Length < 4 || width <= 1)
            {
                return text;
            }

            var lines = new List<List<string>>();
            var current = new List<string>();
            foreach (string word in words)
            {
                string candidate = string.Join(
                    " ",
                    current.Count == 0
                        ? new[] { word }
                        : current.Append(word));
                int candidateWidth = TextRenderer.MeasureText(
                    graphics,
                    candidate,
                    font,
                    Size.Empty,
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPrefix).Width;
                if (current.Count > 0 && candidateWidth > width)
                {
                    lines.Add(current);
                    current = new List<string>();
                }

                current.Add(word);
            }

            if (current.Count > 0)
            {
                lines.Add(current);
            }

            if (lines.Count > 1)
            {
                List<string> previous = lines[^2];
                List<string> last = lines[^1];
                while (last.Count < 4 && previous.Count > 4)
                {
                    string moved = previous[^1];
                    previous.RemoveAt(previous.Count - 1);
                    last.Insert(0, moved);
                }
            }

            return string.Join(
                Environment.NewLine,
                lines.Select(line => string.Join(" ", line)));
        }

        private void DrawMouse(Graphics graphics, Rectangle bounds, float pulse)
        {
            using Pen outline = new(_palette.SecondaryText, Math.Max(1.5f, DeviceDpi / 64f));
            using GraphicsPath path = ControlDrawing.RoundedRect(bounds, bounds.Width / 2);
            graphics.DrawPath(outline, path);
            int centerX = bounds.Left + (bounds.Width / 2);
            graphics.DrawLine(
                outline,
                centerX,
                bounds.Top + 2,
                centerX,
                bounds.Top + Math.Max(18, bounds.Height * 35 / 100));
            using SolidBrush accent = new(_palette.Accent);

            if (_kind == SetupUsageKind.Zoom)
            {
                int wheelWidth = Math.Max(6, bounds.Width / 6);
                int wheelHeight = Math.Max(12, bounds.Height / 5);
                int offset = (int)Math.Round((pulse - 0.5f) * bounds.Height * 0.16f);
                graphics.FillEllipse(
                    accent,
                    centerX - (wheelWidth / 2),
                    bounds.Top + (bounds.Height / 8) + offset,
                    wheelWidth,
                    wheelHeight);
                using Pen accentPen = new(_palette.Accent, Math.Max(1.4f, DeviceDpi / 72f));
                graphics.DrawLine(accentPen, centerX - 4, bounds.Top + 5, centerX, bounds.Top + 1);
                graphics.DrawLine(accentPen, centerX, bounds.Top + 1, centerX + 4, bounds.Top + 5);
                graphics.DrawLine(accentPen, centerX - 4, bounds.Bottom - 5, centerX, bounds.Bottom - 1);
                graphics.DrawLine(accentPen, centerX, bounds.Bottom - 1, centerX + 4, bounds.Bottom - 5);
            }
            else if (_kind == SetupUsageKind.Invert)
            {
                Color clickColor = ControlDrawing.Blend(
                    _palette.ControlBackground,
                    _palette.Accent,
                    80 + (int)(pulse * 160));
                using SolidBrush clickBrush = new(clickColor);
                int clickWidth = Math.Max(10, bounds.Width / 4);
                int clickHeight = Math.Max(18, bounds.Height / 3);
                graphics.FillEllipse(
                    clickBrush,
                    centerX - (clickWidth / 2),
                    bounds.Top + 5,
                    clickWidth,
                    clickHeight);
            }
            else
            {
                int alphaBlend = 100 + (int)(pulse * 140);
                using SolidBrush buttonBrush = new(ControlDrawing.Blend(
                    _palette.ControlBackground,
                    _palette.Accent,
                    alphaBlend));
                int buttonHeight = Math.Max(18, bounds.Height / 3);
                graphics.FillRectangle(
                    buttonBrush,
                    bounds.Left + 3,
                    bounds.Top + 4,
                    (bounds.Width / 2) - 4,
                    buttonHeight);
                graphics.FillRectangle(
                    buttonBrush,
                    centerX + 1,
                    bounds.Top + 4,
                    (bounds.Width / 2) - 4,
                    buttonHeight);
            }
        }

        private void DrawKeycap(
            Graphics graphics,
            Rectangle bounds,
            string text,
            Font font,
            bool showWindowsLogo = false)
        {
            Rectangle shadowBounds = new(bounds.Left, bounds.Top + 5, bounds.Width, bounds.Height - 5);
            using GraphicsPath shadowPath = ControlDrawing.RoundedRect(shadowBounds, 8);
            using SolidBrush shadowBrush = new(Color.FromArgb(90, Color.Black));
            graphics.FillPath(shadowBrush, shadowPath);

            Rectangle faceBounds = new(bounds.Left, bounds.Top, bounds.Width, bounds.Height - 7);
            using GraphicsPath path = ControlDrawing.RoundedRect(faceBounds, 8);
            using SolidBrush brush = new(_palette.ButtonBackground);
            using Pen border = new(_palette.Border, Math.Max(1.2f, DeviceDpi / 80f));
            graphics.FillPath(brush, path);
            graphics.DrawPath(border, path);
            Rectangle innerBounds = Rectangle.Inflate(faceBounds, -4, -4);
            using GraphicsPath innerPath = ControlDrawing.RoundedRect(innerBounds, 5);
            using Pen innerBorder = new(Color.FromArgb(48, _palette.Text), 1f);
            graphics.DrawPath(innerBorder, innerPath);

            if (showWindowsLogo)
            {
                int iconSize = Math.Clamp(faceBounds.Height / 2, 15, 20);
                int textWidth = TextRenderer.MeasureText(
                    graphics,
                    text,
                    font,
                    Size.Empty,
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
                int groupWidth = iconSize + 6 + textWidth;
                int groupLeft = faceBounds.Left + ((faceBounds.Width - groupWidth) / 2);
                DrawWindowsLogo(
                    graphics,
                    _palette.Text,
                    new Rectangle(
                        groupLeft,
                        faceBounds.Top + ((faceBounds.Height - iconSize) / 2),
                        iconSize,
                        iconSize));
                TextRenderer.DrawText(
                    graphics,
                    text,
                    font,
                    new Rectangle(groupLeft + iconSize + 6, faceBounds.Top, textWidth, faceBounds.Height),
                    _palette.Text,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPrefix);
            }
            else
            {
                TextRenderer.DrawText(
                    graphics,
                    text,
                    font,
                    faceBounds,
                    _palette.Text,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPrefix);
            }
        }
    }

    private sealed class SetupHotkeyRow : Control
    {
        private readonly System.Windows.Forms.Timer _animationTimer;
        private ThemePalette _palette;
        private string _title = string.Empty;
        private string _description = string.Empty;
        private string _keyLabel = string.Empty;
        private string _changeKeyText = string.Empty;
        private string _pressKeyText = string.Empty;
        private SetupValidation _validation;
        private float _fontScale = 1f;
        private bool _capturing;
        private bool _hovered;
        private bool _showWindowsLogo;
        private Rectangle _captureBounds;

        internal SetupHotkeyRow()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable |
                ControlStyles.UserPaint,
                true);
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
            _animationTimer = new System.Windows.Forms.Timer { Interval = 32 };
            _animationTimer.Tick += (_, _) => Invalidate();
        }

        internal event EventHandler? CaptureRequested;

        internal void ApplyTheme(ThemePalette palette, float fontScale)
        {
            _palette = palette;
            _fontScale = fontScale;
            BackColor = palette.MenuBackground;
            ForeColor = palette.Text;
            Invalidate();
        }

        internal void UpdateContent(
            string title,
            string description,
            string keyLabel,
            bool capturing,
            SetupValidation validation,
            string changeKeyText,
            string pressKeyText,
            bool showWindowsLogo)
        {
            bool captureChanged = _capturing != capturing;
            _title = title;
            _description = description;
            _keyLabel = keyLabel;
            _capturing = capturing;
            _validation = validation;
            _changeKeyText = changeKeyText;
            _pressKeyText = pressKeyText;
            _showWindowsLogo = showWindowsLogo;
            if (captureChanged)
            {
                if (capturing && AccessibilityPreferences.AnimationsEnabled)
                {
                    _animationTimer.Start();
                }
                else
                {
                    _animationTimer.Stop();
                }
            }
            AccessibleName = title + ": " + keyLabel;
            AccessibleDescription = string.Join(
                " ",
                description,
                capturing ? pressKeyText : changeKeyText,
                validation.Text);
            AccessibleDefaultActionDescription = capturing ? pressKeyText : changeKeyText;
            if (IsHandleCreated)
            {
                AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
                AccessibilityNotifyClients(AccessibleEvents.DescriptionChange, -1);
            }
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationTimer.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!_capturing && e.KeyCode is Keys.Enter or Keys.Space)
            {
                CaptureRequested?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool hovered = _captureBounds.Contains(e.Location);
            if (_hovered != hovered)
            {
                _hovered = hovered;
                Cursor = hovered ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && _captureBounds.Contains(e.Location))
            {
                Focus();
                CaptureRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using SolidBrush brush = new(ControlDrawing.EffectiveBackColor(this));
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 20 || Height < 20)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            int inset = ControlDrawing.ScaleLogical(this, 1);
            int radius = ControlDrawing.ScaleLogical(this, 14);
            Rectangle surface = Rectangle.Inflate(ClientRectangle, -inset, -inset);
            using GraphicsPath surfacePath = ControlDrawing.RoundedRect(surface, radius);
            using SolidBrush surfaceBrush = new(_palette.ControlBackground);
            using Pen surfaceBorder = new(_palette.Border, Math.Max(1f, DeviceDpi / 96f));
            e.Graphics.FillPath(surfaceBrush, surfacePath);
            e.Graphics.DrawPath(surfaceBorder, surfacePath);

            DrawCenteredCaptureLayout(e.Graphics);
        }

        private void DrawCenteredCaptureLayout(Graphics graphics)
        {
            float layoutScale = Math.Clamp(_fontScale, 1f, 1.65f);
            int buttonWidth = Math.Clamp(
                Width * 2 / 5,
                (int)Math.Round(280f * layoutScale),
                (int)Math.Round(360f * layoutScale));
            int buttonHeight = Math.Clamp(
                (int)Math.Round(108f * layoutScale),
                96,
                Math.Max(96, Height / 3));
            int topPadding = (int)Math.Round(20f * layoutScale);
            _captureBounds = new Rectangle(
                (Width - buttonWidth) / 2,
                topPadding,
                buttonWidth,
                buttonHeight);

            using Font titleFont = new(
                "Segoe UI",
                Math.Max(9f, 11.8f * _fontScale),
                FontStyle.Bold);
            using Font bodyFont = new(
                "Segoe UI",
                Math.Max(8f, 9.4f * _fontScale),
                FontStyle.Regular);
            using Font warningFont = new(
                "Segoe UI",
                Math.Max(7.5f, 8.1f * _fontScale),
                FontStyle.Regular);
            using Font keyFont = new(
                "Segoe UI",
                Math.Max(12f, 17f * _fontScale),
                FontStyle.Bold);

            float capturePulse = GetCapturePulse();
            Color buttonFill = _capturing
                ? ControlDrawing.Blend(
                    _palette.ControlBackground,
                    _palette.Accent,
                    68 + (int)Math.Round(capturePulse * 28))
                : _hovered
                    ? _palette.ButtonHover
                    : _palette.ButtonBackground;
            Color buttonBorder = _capturing || Focused ? _palette.Accent : _palette.Border;
            DrawCaptureGlow(
                graphics,
                _captureBounds,
                ControlDrawing.ScaleLogical(this, 14),
                capturePulse);
            using GraphicsPath buttonPath = ControlDrawing.RoundedRect(
                _captureBounds,
                ControlDrawing.ScaleLogical(this, 14));
            using SolidBrush buttonBrush = new(buttonFill);
            using Pen buttonPen = new(buttonBorder, Math.Max(1f, DeviceDpi / 64f));
            graphics.FillPath(buttonBrush, buttonPath);
            graphics.DrawPath(buttonPen, buttonPath);

            if (_capturing)
            {
                TextRenderer.DrawText(
                    graphics,
                    _pressKeyText,
                    titleFont,
                    _captureBounds,
                    _palette.Text,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.WordBreak |
                    TextFormatFlags.NoPrefix);
            }
            else
            {
                Rectangle keyBounds = new(
                    _captureBounds.Left,
                    _captureBounds.Top + (int)Math.Round(7f * layoutScale),
                    _captureBounds.Width,
                    (int)Math.Round(_captureBounds.Height * 0.56f));
                Rectangle hintBounds = new(
                    _captureBounds.Left + (int)Math.Round(12f * layoutScale),
                    keyBounds.Bottom,
                    _captureBounds.Width - (int)Math.Round(24f * layoutScale),
                    Math.Max(1, _captureBounds.Bottom - keyBounds.Bottom -
                        (int)Math.Round(7f * layoutScale)));
                DrawKeyLabel(graphics, keyBounds, keyFont);
                TextRenderer.DrawText(
                    graphics,
                    _changeKeyText,
                    bodyFont,
                    hintBounds,
                    _palette.SecondaryText,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.WordBreak |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.NoPrefix);
            }

            int textHorizontalInset = (int)Math.Round(40f * layoutScale);
            int descriptionInset = (int)Math.Round(64f * layoutScale);
            int titleTop = _captureBounds.Bottom + (int)Math.Round(14f * layoutScale);
            int titleHeight = TextRenderer.MeasureText(
                graphics,
                _title,
                titleFont,
                new Size(Math.Max(1, Width - (textHorizontalInset * 2)), Height),
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoPadding |
                TextFormatFlags.NoPrefix).Height;
            Rectangle titleBounds = new(
                textHorizontalInset,
                titleTop,
                Width - (textHorizontalInset * 2),
                titleHeight);
            int descriptionHeight = TextRenderer.MeasureText(
                graphics,
                _description,
                bodyFont,
                new Size(Math.Max(1, Width - (descriptionInset * 2)), Height),
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoPadding |
                TextFormatFlags.NoPrefix).Height;
            Rectangle descriptionBounds = new(
                descriptionInset,
                titleBounds.Bottom + (int)Math.Round(3f * layoutScale),
                Width - (descriptionInset * 2),
                descriptionHeight);
            TextRenderer.DrawText(
                graphics,
                _title,
                titleFont,
                titleBounds,
                _palette.Text,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.Top |
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoPadding |
                TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(
                graphics,
                _description,
                bodyFont,
                descriptionBounds,
                _palette.SecondaryText,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.Top |
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoPrefix);

            if (_validation.Level != SetupValidationLevel.None)
            {
                int statusTop = descriptionBounds.Bottom + (int)Math.Round(16f * layoutScale);
                int availableStatusHeight = Math.Max(
                    40,
                    Height - statusTop - (int)Math.Round(12f * layoutScale));
                int statusHeight = _validation.Level == SetupValidationLevel.Warning
                    ? Math.Min(
                        availableStatusHeight,
                        Math.Max(
                            ControlDrawing.ScaleLogical(this, 56),
                            (int)Math.Round(56f * layoutScale)))
                    : availableStatusHeight;
                Rectangle statusBounds = new(
                    descriptionInset,
                    statusTop,
                    Width - (descriptionInset * 2),
                    statusHeight);
                DrawValidationMessage(graphics, statusBounds, warningFont);
            }
        }

        private float GetCapturePulse()
        {
            if (!_capturing || !AccessibilityPreferences.AnimationsEnabled)
            {
                return 0.5f;
            }

            const double cycleMilliseconds = 1800d;
            double phase = (Environment.TickCount64 % (long)cycleMilliseconds) /
                           cycleMilliseconds *
                           Math.PI *
                           2d;
            return (float)((Math.Sin(phase) + 1d) / 2d);
        }

        private void DrawCaptureGlow(
            Graphics graphics,
            Rectangle bounds,
            int radius,
            float pulse)
        {
            if (!_capturing || !AccessibilityPreferences.AnimationsEnabled)
            {
                return;
            }

            int expansion = ControlDrawing.ScaleLogical(this, 3) +
                            (int)Math.Round(ControlDrawing.ScaleLogical(this, 3) * pulse);
            Rectangle glowBounds = Rectangle.Inflate(bounds, expansion, expansion);
            using GraphicsPath glowPath = ControlDrawing.RoundedRect(
                glowBounds,
                radius + expansion);
            using Pen glowPen = new(
                Color.FromArgb(42 + (int)Math.Round(pulse * 64), _palette.Accent),
                Math.Max(1.5f, DeviceDpi / 56f));
            graphics.DrawPath(glowPen, glowPath);
        }

        private void DrawValidationMessage(Graphics graphics, Rectangle bounds, Font font)
        {
            bool isError = _validation.Level == SetupValidationLevel.Error;
            Color accent = isError
                ? Color.FromArgb(239, 68, 68)
                : Color.FromArgb(59, 130, 246);
            Color fill = ControlDrawing.Blend(_palette.ControlBackground, accent, isError ? 24 : 18);
            Rectangle panelBounds = Rectangle.Inflate(bounds, -1, -1);
            using GraphicsPath panelPath = ControlDrawing.RoundedRect(
                panelBounds,
                ControlDrawing.ScaleLogical(this, 8));
            using SolidBrush fillBrush = new(fill);
            using Pen panelBorder = new(ControlDrawing.Blend(_palette.Border, accent, 82), 1f);
            graphics.FillPath(fillBrush, panelPath);
            graphics.DrawPath(panelBorder, panelPath);

            int iconSize = Math.Clamp(
                (int)Math.Round(22f * Math.Max(1f, _fontScale)),
                22,
                44);
            int horizontalPadding = Math.Clamp(
                (int)Math.Round(14f * Math.Max(1f, _fontScale)),
                14,
                28);
            Rectangle iconBounds = new(
                panelBounds.Left + horizontalPadding,
                panelBounds.Top + ((panelBounds.Height - iconSize) / 2),
                iconSize,
                iconSize);
            using SolidBrush iconBrush = new(accent);
            graphics.FillEllipse(iconBrush, iconBounds);
            using Font iconFont = new(
                "Segoe UI",
                Math.Max(8f, 9.2f * _fontScale),
                FontStyle.Bold);
            TextRenderer.DrawText(
                graphics,
                isError ? "!" : "i",
                iconFont,
                iconBounds,
                ControlDrawing.ContrastText(accent),
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.SingleLine);

            Rectangle textBounds = new(
                iconBounds.Right + horizontalPadding,
                panelBounds.Top + 7,
                Math.Max(
                    1,
                    panelBounds.Right -
                    iconBounds.Right -
                    (horizontalPadding * 2)),
                Math.Max(1, panelBounds.Height - 14));
            if (!isError)
            {
                DrawFittedSingleLine(
                    graphics,
                    _validation.Text,
                    font,
                    textBounds,
                    _palette.Text,
                    TextFormatFlags.Left);
                return;
            }

            TextRenderer.DrawText(
                graphics,
                _validation.Text,
                font,
                textBounds,
                _palette.Text,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoPrefix);
        }

        private void DrawKeyLabel(Graphics graphics, Rectangle bounds, Font font)
        {
            if (!_showWindowsLogo)
            {
                DrawFittedSingleLine(graphics, _keyLabel, font, bounds, _palette.Text);
                return;
            }

            int iconSize = Math.Clamp(bounds.Height / 2, 18, 28);
            int textWidth = TextRenderer.MeasureText(
                graphics,
                _keyLabel,
                font,
                Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
            int groupWidth = iconSize + 10 + textWidth;
            int groupLeft = bounds.Left + ((bounds.Width - groupWidth) / 2);
            DrawWindowsLogo(
                graphics,
                _palette.Text,
                new Rectangle(
                    groupLeft,
                    bounds.Top + ((bounds.Height - iconSize) / 2),
                    iconSize,
                    iconSize));
            TextRenderer.DrawText(
                graphics,
                _keyLabel,
                font,
                new Rectangle(groupLeft + iconSize + 10, bounds.Top, textWidth, bounds.Height),
                _palette.Text,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix);
        }

        private static void DrawFittedSingleLine(
            Graphics graphics,
            string text,
            Font font,
            Rectangle bounds,
            Color color,
            TextFormatFlags horizontalAlignment = TextFormatFlags.HorizontalCenter)
        {
            Font? fittedFont = null;
            Font activeFont = font;
            float size = font.Size;
            while (activeFont.Size > 4.5f &&
                   TextRenderer.MeasureText(
                       graphics,
                       text,
                       activeFont,
                       Size.Empty,
                       TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width > bounds.Width)
            {
                size -= 0.5f;
                fittedFont?.Dispose();
                fittedFont = new Font(font.FontFamily, size, font.Style);
                activeFont = fittedFont;
            }

            TextRenderer.DrawText(
                graphics,
                text,
                activeFont,
                bounds,
                color,
                horizontalAlignment |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix);
            fittedFont?.Dispose();
        }
    }

    private sealed class SetupViewToggle : Control
    {
        private ThemePalette _palette;
        private SetupViewMode _viewMode;
        private bool _hovered;
        private bool _pressed;
        private float _fontScale = 1f;
        private string _largeText = "Large setup";

        internal SetupViewToggle()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable |
                ControlStyles.UserPaint,
                true);
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.CheckButton;
        }

        internal event Action<SetupViewMode>? ViewModeRequested;

        internal SetupViewMode ViewMode
        {
            get => _viewMode;
            set
            {
                if (_viewMode == value)
                {
                    return;
                }

                _viewMode = value;
                if (IsHandleCreated)
                {
                    AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
                    AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
                }
                Invalidate();
            }
        }

        internal void UpdateContent(string standardText, string largeText)
        {
            _ = standardText;
            _largeText = largeText;
            Invalidate();
        }

        internal void ApplyTheme(ThemePalette palette, float fontScale)
        {
            _palette = palette;
            _fontScale = fontScale;
            BackColor = ControlDrawing.EffectiveBackColor(this);
            ForeColor = palette.Text;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            _pressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && Enabled)
            {
                Focus();
                _pressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            bool shouldToggle =
                _pressed &&
                e.Button == MouseButtons.Left &&
                ClientRectangle.Contains(e.Location);
            _pressed = false;
            Invalidate();
            base.OnMouseUp(e);
            if (shouldToggle)
            {
                RequestViewMode(
                    _viewMode == SetupViewMode.Accessible
                        ? SetupViewMode.Standard
                        : SetupViewMode.Accessible);
            }
        }

        protected override bool IsInputKey(Keys keyData) =>
            (keyData & Keys.KeyCode) is
                Keys.Left or
                Keys.Right or
                Keys.Home or
                Keys.End or
                Keys.Enter or
                Keys.Space ||
            base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            SetupViewMode? requestedMode = e.KeyCode switch
            {
                Keys.Left or Keys.Home => SetupViewMode.Standard,
                Keys.Right or Keys.End => SetupViewMode.Accessible,
                Keys.Enter or Keys.Space => _viewMode == SetupViewMode.Accessible
                    ? SetupViewMode.Standard
                    : SetupViewMode.Accessible,
                _ => null
            };
            if (requestedMode.HasValue)
            {
                RequestViewMode(requestedMode.Value);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }

            base.OnKeyDown(e);
        }

        private void RequestViewMode(SetupViewMode viewMode)
        {
            if (Enabled && viewMode != _viewMode)
            {
                ViewModeRequested?.Invoke(viewMode);
            }
        }

        internal void PerformMouseClickForValidation()
        {
            Point center = new(
                Math.Max(0, (Width - 1) / 2),
                Math.Max(0, (Height - 1) / 2));
            OnMouseDown(new MouseEventArgs(
                MouseButtons.Left,
                clicks: 1,
                center.X,
                center.Y,
                delta: 0));
            OnMouseUp(new MouseEventArgs(
                MouseButtons.Left,
                clicks: 1,
                center.X,
                center.Y,
                delta: 0));
        }

        protected override AccessibleObject CreateAccessibilityInstance() =>
            new SetupViewToggleAccessibleObject(this);

        private sealed class SetupViewToggleAccessibleObject(SetupViewToggle owner) :
            ControlAccessibleObject(owner)
        {
            public override AccessibleRole Role => AccessibleRole.CheckButton;

            public override AccessibleStates State => base.State |
                AccessibleStates.Focusable |
                (owner.ViewMode == SetupViewMode.Accessible
                    ? AccessibleStates.Checked
                    : AccessibleStates.None) |
                (owner.Enabled ? AccessibleStates.None : AccessibleStates.Unavailable);

            public override string? DefaultAction => "Toggle";

            public override void DoDefaultAction()
            {
                owner.RequestViewMode(
                    owner.ViewMode == SetupViewMode.Accessible
                        ? SetupViewMode.Standard
                        : SetupViewMode.Accessible);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using SolidBrush brush = new(ControlDrawing.EffectiveBackColor(this));
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 8 || Height < 8)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            Rectangle containerBounds = Rectangle.Inflate(ClientRectangle, -1, -1);
            using GraphicsPath containerPath = ControlDrawing.RoundedRect(
                containerBounds,
                ControlDrawing.ScaleLogical(this, 12));
            Color containerFill = ControlDrawing.Blend(
                _palette.MenuBackground,
                _palette.ControlBackground,
                168);
            using SolidBrush containerBrush = new(containerFill);
            using Pen containerBorder = new(_palette.Border, Math.Max(1f, DeviceDpi / 96f));
            e.Graphics.FillPath(containerBrush, containerPath);
            e.Graphics.DrawPath(containerBorder, containerPath);

            using Font labelFont = new(
                "Segoe UI",
                Math.Max(8f, 9.2f * _fontScale),
                FontStyle.Bold);
            int trackWidth = ControlDrawing.ScaleLogical(this, 40);
            int trackHeight = ControlDrawing.ScaleLogical(this, 22);
            int sidePadding = ControlDrawing.ScaleLogical(this, 8);
            int gap = ControlDrawing.ScaleLogical(this, 6);
            Rectangle trackBounds = new(
                Math.Max(sidePadding, Width - sidePadding - trackWidth),
                Math.Max(1, (Height - trackHeight) / 2),
                trackWidth,
                trackHeight);
            Rectangle labelBounds = new(
                sidePadding,
                0,
                Math.Max(1, trackBounds.Left - gap - sidePadding),
                Height);

            if (_hovered || Focused)
            {
                Rectangle hoverBounds = Rectangle.Inflate(containerBounds, -2, -2);
                using GraphicsPath hoverPath = ControlDrawing.RoundedRect(
                    hoverBounds,
                    ControlDrawing.ScaleLogical(this, 10));
                Color hoverColor = ControlDrawing.Blend(
                    _palette.MenuBackground,
                    _palette.ButtonHover,
                    _pressed ? 150 : 82);
                using SolidBrush hoverBrush = new(hoverColor);
                e.Graphics.FillPath(hoverBrush, hoverPath);
            }

            bool large = _viewMode == SetupViewMode.Accessible;
            TextRenderer.DrawText(
                e.Graphics,
                _largeText,
                labelFont,
                labelBounds,
                _palette.Text,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine |
                TextFormatFlags.NoPadding |
                TextFormatFlags.NoPrefix);

            int radius = trackBounds.Height / 2;
            using GraphicsPath trackPath = ControlDrawing.RoundedRect(trackBounds, radius);
            Color trackColor = large
                ? _palette.Accent
                : ControlDrawing.Blend(_palette.ControlBackground, _palette.Border, 90);
            using SolidBrush trackBrush = new(trackColor);
            using Pen trackPen = new(
                large ? _palette.Accent : _palette.Border,
                Math.Max(1f, DeviceDpi / 96f));
            e.Graphics.FillPath(trackBrush, trackPath);
            e.Graphics.DrawPath(trackPen, trackPath);

            int knobInset = ControlDrawing.ScaleLogical(this, 3);
            int knobSize = trackBounds.Height - (knobInset * 2);
            int knobX = large
                ? trackBounds.Right - knobInset - knobSize
                : trackBounds.Left + knobInset;
            Rectangle knobBounds = new(
                knobX,
                trackBounds.Top + knobInset,
                knobSize,
                knobSize);
            Color knobColor = AccessibilityPreferences.HighContrast
                ? (large ? SystemColors.HighlightText : SystemColors.WindowText)
                : Enabled
                    ? Color.FromArgb(245, 247, 250)
                    : Color.FromArgb(160, 164, 170);
            using SolidBrush knobBrush = new(knobColor);
            using Pen knobPen = new(_palette.Border, Math.Max(1f, DeviceDpi / 96f));
            e.Graphics.FillEllipse(knobBrush, knobBounds);
            e.Graphics.DrawEllipse(knobPen, knobBounds);

            if (ControlDrawing.ShouldDrawFocus(this, ShowFocusCues))
            {
                Rectangle focusBounds = Rectangle.Inflate(containerBounds, -2, -2);
                ControlDrawing.DrawFocusRing(
                    e.Graphics,
                    focusBounds,
                    ControlDrawing.ScaleLogical(this, 10),
                    _palette);
            }
        }
    }

    private sealed class SetupChoiceCard : Control
    {
        private ThemePalette _palette;
        private bool _selected;
        private bool _hovered;
        private bool _pressed;
        private float _fontScale = 1f;
        private string _selectText = "Select";
        private string _selectedText = "Selected";

        internal SetupChoiceCard(
            string title,
            string description,
            string badge,
            SetupIcon icon)
        {
            Title = title;
            Description = description;
            Badge = badge;
            Icon = icon;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable |
                ControlStyles.UserPaint,
                true);
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.RadioButton;
            AccessibleName = title;
            AccessibleDescription = description;
        }

        internal string Title { get; private set; }
        internal string Description { get; private set; }
        internal string Badge { get; }
        internal SetupIcon Icon { get; }
        internal bool Compact { get; set; }
        internal bool Prominent { get; set; }
        internal bool ShowIdentity { get; set; } = true;
        internal bool Segmented { get; set; }

        internal void UpdateContent(string title, string description)
        {
            Title = title;
            Description = description;
            AccessibleName = title;
            UpdateAccessibleSelection();
            Invalidate();
        }

        internal bool Selected
        {
            get => _selected;
            set
            {
                if (_selected == value)
                {
                    return;
                }

                _selected = value;
                UpdateAccessibleSelection();
                if (IsHandleCreated)
                {
                    AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
                    AccessibilityNotifyClients(AccessibleEvents.Selection, -1);
                }
                Invalidate();
            }
        }

        internal void ApplyAccessibilityLabels(string selectText, string selectedText)
        {
            _selectText = selectText;
            _selectedText = selectedText;
            UpdateAccessibleSelection();
        }

        private void UpdateAccessibleSelection()
        {
            AccessibleDefaultActionDescription = _selected ? _selectedText : _selectText;
            AccessibleDescription = string.Join(
                " ",
                Description,
                _selected ? _selectedText : string.Empty);
        }

        internal void ApplyTheme(ThemePalette palette, float fontScale)
        {
            _palette = palette;
            _fontScale = fontScale;
            BackColor = ControlDrawing.EffectiveBackColor(this);
            ForeColor = palette.Text;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            _pressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                Focus();
                _pressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _pressed = false;
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData) =>
            (keyData & Keys.KeyCode) is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode is Keys.Left or Keys.Up or Keys.Right or Keys.Down or Keys.Home or Keys.End)
            {
                SetupChoiceCard[] siblings = Parent?.Controls
                    .OfType<SetupChoiceCard>()
                    .OrderBy(card => card.TabIndex)
                    .ToArray() ?? [];
                int currentIndex = Array.IndexOf(siblings, this);
                if (currentIndex >= 0 && siblings.Length > 0)
                {
                    int targetIndex = e.KeyCode switch
                    {
                        Keys.Home => 0,
                        Keys.End => siblings.Length - 1,
                        Keys.Left or Keys.Up => (currentIndex - 1 + siblings.Length) % siblings.Length,
                        _ => (currentIndex + 1) % siblings.Length
                    };
                    siblings[targetIndex].Focus();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    return;
                }
            }

            if (e.KeyCode is Keys.Enter or Keys.Space)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }

            base.OnKeyDown(e);
        }

        protected override AccessibleObject CreateAccessibilityInstance() =>
            new SetupChoiceCardAccessibleObject(this);

        private sealed class SetupChoiceCardAccessibleObject(SetupChoiceCard owner) :
            ControlAccessibleObject(owner)
        {
            public override AccessibleRole Role => AccessibleRole.RadioButton;

            public override AccessibleStates State => base.State |
                AccessibleStates.Focusable |
                AccessibleStates.Selectable |
                (owner.Selected
                    ? AccessibleStates.Checked | AccessibleStates.Selected
                    : AccessibleStates.None) |
                (owner.Enabled ? AccessibleStates.None : AccessibleStates.Unavailable);

            public override string? DefaultAction =>
                owner.Selected ? owner._selectedText : owner._selectText;

            public override void DoDefaultAction()
            {
                if (owner.Enabled)
                {
                    owner.OnClick(EventArgs.Empty);
                }
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using SolidBrush brush = new(ControlDrawing.EffectiveBackColor(this));
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 8 || Height < 8)
            {
                return;
            }

            GraphicsState paintState = e.Graphics.Save();
            try
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                int inset = ControlDrawing.ScaleLogical(this, Segmented ? 1 : 2);
                int radius = ControlDrawing.ScaleLogical(this, Segmented ? 10 : 12);
                Rectangle bounds = new(
                    inset,
                    inset,
                    Width - (inset * 2) - 1,
                    Height - (inset * 2) - 1);
                using GraphicsPath path = ControlDrawing.RoundedRect(bounds, radius);
                Color normal = _selected
                    ? ControlDrawing.Blend(
                        _palette.ControlBackground,
                        _palette.Accent,
                        Segmented ? 54 : 34)
                    : _palette.ControlBackground;
                Color fill = _pressed
                    ? _palette.ButtonPressed
                    : _hovered
                    ? (_selected ? ControlDrawing.Blend(normal, _palette.Accent, 24) : _palette.ButtonHover)
                    : normal;
                using SolidBrush fillBrush = new(fill);
                e.Graphics.FillPath(fillBrush, path);
                if (!Segmented || _selected)
                {
                    using Pen outline = new(
                        _selected ? _palette.Accent : _palette.Border,
                        Math.Max(1f, DeviceDpi / 96f) * (_selected ? 1.6f : 1f));
                    e.Graphics.DrawPath(outline, path);
                }

                using Font titleFont = new(
                    "Segoe UI",
                    Math.Max(
                        8f,
                        (Prominent ? 13.2f : Compact ? 10.2f : 11.3f) *
                        _fontScale),
                    FontStyle.Bold);
                using Font descriptionFont = new(
                    "Segoe UI",
                    Math.Max(7.5f, (Compact ? 8.3f : 9.1f) * _fontScale),
                    FontStyle.Regular);

                int horizontal = ControlDrawing.ScaleLogical(this, Prominent ? 16 : 18);
                int iconSize = Prominent
                    ? Math.Clamp(
                        (int)Math.Round(Height * 0.38f),
                        ControlDrawing.ScaleLogical(this, 44),
                        ControlDrawing.ScaleLogical(this, 64))
                    : ControlDrawing.ScaleLogical(this, Compact ? 32 : 38);
                int identityGap = ControlDrawing.ScaleLogical(this, Prominent ? 8 : 14);
                if (Prominent && ShowIdentity)
                {
                    int measuredTitleWidth = TextRenderer.MeasureText(
                        Title,
                        titleFont,
                        new Size(32767, 32767),
                        TextFormatFlags.NoPadding |
                        TextFormatFlags.SingleLine |
                        TextFormatFlags.NoPrefix).Width;
                    int availableGroupWidth = Math.Max(
                        1,
                        Width - ControlDrawing.ScaleLogical(this, 28));
                    iconSize = Math.Min(
                        iconSize,
                        Math.Max(
                            ControlDrawing.ScaleLogical(this, 36),
                            availableGroupWidth - identityGap - measuredTitleWidth));
                    int groupWidth = iconSize + identityGap + measuredTitleWidth;
                    horizontal = Math.Max(
                        ControlDrawing.ScaleLogical(this, 14),
                        (Width - groupWidth) / 2);
                }

                Rectangle iconBounds = new(horizontal, (Height - iconSize) / 2, iconSize, iconSize);
                if (ShowIdentity)
                {
                    DrawIdentity(e.Graphics, iconBounds);
                }

                int textLeft = ShowIdentity
                    ? iconBounds.Right + identityGap
                    : horizontal;
                int rightInset = ControlDrawing.ScaleLogical(this, Prominent ? 14 : 18);
                int textWidth = Math.Max(1, Width - textLeft - rightInset);
                if (!ShowIdentity)
                {
                    TextRenderer.DrawText(
                        e.Graphics,
                        Title,
                        titleFont,
                        new Rectangle(
                            horizontal,
                            ControlDrawing.ScaleLogical(this, 4),
                            Math.Max(1, Width - (horizontal * 2)),
                            Math.Max(1, Height - ControlDrawing.ScaleLogical(this, 8))),
                        _palette.Text,
                        TextFormatFlags.HorizontalCenter |
                        TextFormatFlags.VerticalCenter |
                        TextFormatFlags.SingleLine |
                        TextFormatFlags.NoPadding |
                        TextFormatFlags.NoPrefix);
                    if (ControlDrawing.ShouldDrawFocus(this, ShowFocusCues))
                    {
                        ControlDrawing.DrawFocusRing(
                            e.Graphics,
                            Rectangle.Inflate(bounds, -2, -2),
                            radius - 2,
                            _palette);
                    }
                    return;
                }

                if (Compact)
                {
                    TextRenderer.DrawText(
                        e.Graphics,
                        Title,
                        titleFont,
                        new Rectangle(textLeft, 0, textWidth, Height),
                        _palette.Text,
                        TextFormatFlags.VerticalCenter |
                        TextFormatFlags.SingleLine |
                        TextFormatFlags.NoPadding |
                        TextFormatFlags.NoPrefix);
                    if (ControlDrawing.ShouldDrawFocus(this, ShowFocusCues))
                    {
                        ControlDrawing.DrawFocusRing(
                            e.Graphics,
                            Rectangle.Inflate(bounds, -2, -2),
                            radius - 2,
                            _palette);
                    }
                    return;
                }

                Size titleSize = TextRenderer.MeasureText(
                    Title,
                    titleFont,
                    new Size(textWidth, Math.Max(1, Height - 16)),
                    TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
                Size descriptionSize = TextRenderer.MeasureText(
                    Description,
                    descriptionFont,
                    new Size(textWidth, Math.Max(1, Height - 16)),
                    TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
                int gap = ControlDrawing.ScaleLogical(this, 5);
                int textHeight = titleSize.Height + gap + descriptionSize.Height;
                int titleY = Math.Max(ControlDrawing.ScaleLogical(this, 10), (Height - textHeight) / 2);
                TextRenderer.DrawText(
                    e.Graphics,
                    Title,
                    titleFont,
                    new Rectangle(textLeft, titleY, textWidth, titleSize.Height),
                    _palette.Text,
                    TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
                TextRenderer.DrawText(
                    e.Graphics,
                    Description,
                    descriptionFont,
                    new Rectangle(textLeft, titleY + titleSize.Height + gap, textWidth, Math.Max(1, Height - titleY - titleSize.Height - gap - 8)),
                    _palette.SecondaryText,
                    TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);

                if (ControlDrawing.ShouldDrawFocus(this, ShowFocusCues))
                {
                    ControlDrawing.DrawFocusRing(e.Graphics, Rectangle.Inflate(bounds, -2, -2), radius - 2, _palette);
                }
            }
            finally
            {
                e.Graphics.Restore(paintState);
            }
        }

        private void DrawIdentity(Graphics graphics, Rectangle bounds)
        {
            if (Icon == SetupIcon.EnglishLanguage)
            {
                DrawFlag(graphics, bounds, Icon);
                return;
            }

            if (Icon is SetupIcon.DanishFlag or
                SetupIcon.SwedishFlag or
                SetupIcon.NorwegianFlag or
                SetupIcon.FinnishFlag)
            {
                DrawFlag(graphics, bounds, Icon);
                return;
            }

            Color iconBackground = _selected
                ? _palette.Accent
                : ControlDrawing.Blend(_palette.ControlBackground, _palette.Text, 18);
            using SolidBrush background = new(iconBackground);
            graphics.FillEllipse(background, bounds);

            Rectangle glyphBounds = Rectangle.Inflate(bounds, -ControlDrawing.ScaleLogical(this, 8), -ControlDrawing.ScaleLogical(this, 8));
            Color glyphColor = _selected ? ControlDrawing.ContrastText(_palette.Accent) : _palette.Text;
            if (!string.IsNullOrWhiteSpace(Badge))
            {
                using Font badgeFont = new(
                    "Segoe UI",
                    Math.Max(7.5f, (Compact ? 8.5f : 9.2f) * _fontScale),
                    FontStyle.Bold);
                TextRenderer.DrawText(
                    graphics,
                    Badge,
                    badgeFont,
                    bounds,
                    glyphColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                return;
            }

            using Pen pen = new(glyphColor, Math.Max(1.4f, DeviceDpi / 72f))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            switch (Icon)
            {
                case SetupIcon.Sun:
                    DrawSun(graphics, glyphBounds, pen);
                    break;
                case SetupIcon.Moon:
                    DrawMoon(graphics, glyphBounds, glyphColor, iconBackground);
                    break;
                case SetupIcon.System:
                    DrawSystem(graphics, glyphBounds, pen);
                    break;
            }
        }

        private static void DrawFlag(Graphics graphics, Rectangle bounds, SetupIcon icon)
        {
            Rectangle flagBounds = new(
                bounds.Left,
                bounds.Top + Math.Max(2, bounds.Height / 7),
                bounds.Width,
                Math.Max(12, bounds.Height - Math.Max(4, (bounds.Height / 7) * 2)));
            using GraphicsPath flagPath = ControlDrawing.RoundedRect(flagBounds, Math.Max(2, bounds.Width / 10));
            GraphicsState state = graphics.Save();
            graphics.SetClip(flagPath);

            if (icon == SetupIcon.EnglishLanguage)
            {
                using SolidBrush blue = new(Color.FromArgb(1, 33, 105));
                graphics.FillRectangle(blue, flagBounds);
                using Pen whiteDiagonal = new(
                    Color.White,
                    Math.Max(4f, flagBounds.Height * 0.32f));
                using Pen redDiagonal = new(
                    Color.FromArgb(200, 16, 46),
                    Math.Max(2f, flagBounds.Height * 0.13f));
                graphics.DrawLine(whiteDiagonal, flagBounds.Left, flagBounds.Top, flagBounds.Right, flagBounds.Bottom);
                graphics.DrawLine(whiteDiagonal, flagBounds.Right, flagBounds.Top, flagBounds.Left, flagBounds.Bottom);
                graphics.DrawLine(redDiagonal, flagBounds.Left, flagBounds.Top, flagBounds.Right, flagBounds.Bottom);
                graphics.DrawLine(redDiagonal, flagBounds.Right, flagBounds.Top, flagBounds.Left, flagBounds.Bottom);

                using SolidBrush white = new(Color.White);
                using SolidBrush red = new(Color.FromArgb(200, 16, 46));
                int whiteVertical = Math.Max(6, flagBounds.Width / 5);
                int whiteHorizontal = Math.Max(6, flagBounds.Height / 3);
                int redVertical = Math.Max(3, whiteVertical / 2);
                int redHorizontal = Math.Max(3, whiteHorizontal / 2);
                int centerX = flagBounds.Left + (flagBounds.Width / 2);
                int centerY = flagBounds.Top + (flagBounds.Height / 2);
                graphics.FillRectangle(white, centerX - (whiteVertical / 2), flagBounds.Top, whiteVertical, flagBounds.Height);
                graphics.FillRectangle(white, flagBounds.Left, centerY - (whiteHorizontal / 2), flagBounds.Width, whiteHorizontal);
                graphics.FillRectangle(red, centerX - (redVertical / 2), flagBounds.Top, redVertical, flagBounds.Height);
                graphics.FillRectangle(red, flagBounds.Left, centerY - (redHorizontal / 2), flagBounds.Width, redHorizontal);

                graphics.Restore(state);
                using Pen englishBorder = new(Color.FromArgb(96, Color.Black), 1f);
                graphics.DrawPath(englishBorder, flagPath);
                return;
            }

            Color background;
            Color outerCross;
            Color? innerCross = null;
            switch (icon)
            {
                case SetupIcon.DanishFlag:
                    background = Color.FromArgb(198, 12, 48);
                    outerCross = Color.White;
                    break;
                case SetupIcon.SwedishFlag:
                    background = Color.FromArgb(0, 106, 167);
                    outerCross = Color.FromArgb(254, 204, 2);
                    break;
                case SetupIcon.NorwegianFlag:
                    background = Color.FromArgb(186, 12, 47);
                    outerCross = Color.White;
                    innerCross = Color.FromArgb(0, 32, 91);
                    break;
                default:
                    background = Color.White;
                    outerCross = Color.FromArgb(0, 53, 128);
                    break;
            }

            using SolidBrush backgroundBrush = new(background);
            using SolidBrush outerCrossBrush = new(outerCross);
            graphics.FillRectangle(backgroundBrush, flagBounds);
            int outerVertical = Math.Max(4, flagBounds.Width / 7);
            int outerHorizontal = Math.Max(4, flagBounds.Height / 5);
            int crossX = flagBounds.Left + (flagBounds.Width * 2 / 5);
            int crossY = flagBounds.Top + (flagBounds.Height / 2);
            graphics.FillRectangle(outerCrossBrush, crossX - (outerVertical / 2), flagBounds.Top, outerVertical, flagBounds.Height);
            graphics.FillRectangle(outerCrossBrush, flagBounds.Left, crossY - (outerHorizontal / 2), flagBounds.Width, outerHorizontal);

            if (innerCross.HasValue)
            {
                using SolidBrush innerCrossBrush = new(innerCross.Value);
                int innerVertical = Math.Max(2, outerVertical / 2);
                int innerHorizontal = Math.Max(2, outerHorizontal / 2);
                graphics.FillRectangle(innerCrossBrush, crossX - (innerVertical / 2), flagBounds.Top, innerVertical, flagBounds.Height);
                graphics.FillRectangle(innerCrossBrush, flagBounds.Left, crossY - (innerHorizontal / 2), flagBounds.Width, innerHorizontal);
            }

            graphics.Restore(state);
            using Pen border = new(Color.FromArgb(96, Color.Black), 1f);
            graphics.DrawPath(border, flagPath);
        }

        private static void DrawSun(Graphics graphics, Rectangle bounds, Pen pen)
        {
            Rectangle center = Rectangle.Inflate(bounds, -bounds.Width / 4, -bounds.Height / 4);
            graphics.DrawEllipse(pen, center);
            Point midpoint = new(bounds.Left + (bounds.Width / 2), bounds.Top + (bounds.Height / 2));
            for (int i = 0; i < 8; i++)
            {
                double angle = (Math.PI * 2 * i) / 8;
                Point start = new(
                    midpoint.X + (int)(Math.Cos(angle) * bounds.Width * 0.34),
                    midpoint.Y + (int)(Math.Sin(angle) * bounds.Height * 0.34));
                Point end = new(
                    midpoint.X + (int)(Math.Cos(angle) * bounds.Width * 0.49),
                    midpoint.Y + (int)(Math.Sin(angle) * bounds.Height * 0.49));
                graphics.DrawLine(pen, start, end);
            }
        }

        private static void DrawMoon(Graphics graphics, Rectangle bounds, Color color, Color cutout)
        {
            using SolidBrush moon = new(color);
            using SolidBrush mask = new(cutout);
            graphics.FillEllipse(moon, bounds);
            Rectangle cutoutBounds = new(
                bounds.Left + (bounds.Width / 3),
                bounds.Top - (bounds.Height / 8),
                bounds.Width,
                bounds.Height);
            graphics.FillEllipse(mask, cutoutBounds);
        }

        private static void DrawSystem(Graphics graphics, Rectangle bounds, Pen pen)
        {
            Rectangle screen = new(bounds.Left, bounds.Top, bounds.Width, Math.Max(8, bounds.Height * 3 / 4));
            using GraphicsPath screenPath = ControlDrawing.RoundedRect(screen, Math.Max(2, bounds.Width / 8));
            graphics.DrawPath(pen, screenPath);
            int centerX = bounds.Left + (bounds.Width / 2);
            int standTop = screen.Bottom + 1;
            graphics.DrawLine(pen, centerX, standTop, centerX, bounds.Bottom - 1);
            graphics.DrawLine(
                pen,
                bounds.Left + (bounds.Width / 4),
                bounds.Bottom - 1,
                bounds.Right - (bounds.Width / 4),
                bounds.Bottom - 1);
        }
    }

    private sealed class SetupWaveTransition : Control
    {
        private const float DurationMilliseconds = 460f;
        private readonly System.Windows.Forms.Timer _timer;
        private Bitmap? _before;
        private Bitmap? _after;
        private long _started;
        private float _progress = 1f;

        internal SetupWaveTransition()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            TabStop = false;
            _timer = new System.Windows.Forms.Timer { Interval = 16 };
            _timer.Tick += (_, _) => Advance();
        }

        internal void Hold(Bitmap before)
        {
            Finish();
            _before = before;
            _started = Environment.TickCount64;
            _progress = 0f;
            Enabled = true;
            Visible = true;
            BringToFront();
            Invalidate();
            Update();
        }

        internal void Reveal(Bitmap after)
        {
            if (_before == null)
            {
                after.Dispose();
                return;
            }

            _after?.Dispose();
            _after = after;
            _started = Environment.TickCount64;
            _progress = 0f;
            _timer.Start();
            Invalidate();
        }

        internal void Finish()
        {
            _timer.Stop();
            Visible = false;
            Enabled = false;
            _before?.Dispose();
            _after?.Dispose();
            _before = null;
            _after = null;
            _progress = 1f;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Finish();
                _timer.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (_before == null)
            {
                using SolidBrush brush = new(ControlDrawing.EffectiveBackColor(this));
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_before == null || Width < 2 || Height < 2)
            {
                return;
            }

            e.Graphics.CompositingMode = CompositingMode.SourceCopy;
            e.Graphics.DrawImage(_before, ClientRectangle);
            if (_after == null)
            {
                return;
            }

            e.Graphics.CompositingMode = CompositingMode.SourceOver;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            float eased = _progress * _progress * (3f - (2f * _progress));
            float amplitude = ControlDrawing.ScaleLogical(this, 24);
            float travel = (eased * ((Width * 2f) + (amplitude * 2f))) - amplitude;
            float diagonalScale = Width / Math.Max(1f, Height);
            using GraphicsPath reveal = new();
            reveal.StartFigure();
            reveal.AddLine(-Width, -Height, WaveX(-Height, travel, amplitude, diagonalScale), -Height);
            PointF previous = new(WaveX(-Height, travel, amplitude, diagonalScale), -Height);
            int step = Math.Max(4, ControlDrawing.ScaleLogical(this, 6));
            for (int y = -Height + step; y < Height * 2; y += step)
            {
                PointF next = new(WaveX(y, travel, amplitude, diagonalScale), y);
                reveal.AddLine(previous, next);
                previous = next;
            }

            PointF bottom = new(WaveX(Height * 2, travel, amplitude, diagonalScale), Height * 2);
            reveal.AddLine(previous, bottom);
            reveal.AddLine(bottom, new PointF(-Width, Height * 2));
            reveal.CloseFigure();

            GraphicsState state = e.Graphics.Save();
            e.Graphics.SetClip(reveal);
            e.Graphics.DrawImage(_after, ClientRectangle);
            e.Graphics.Restore(state);
        }

        private float WaveX(int y, float travel, float amplitude, float diagonalScale)
        {
            float vertical = y / Math.Max(1f, Height);
            return travel -
                (diagonalScale * y) +
                (MathF.Sin((vertical * MathF.PI * 2.8f) - (_progress * MathF.PI)) * amplitude);
        }

        private void Advance()
        {
            _progress = Math.Clamp((Environment.TickCount64 - _started) / DurationMilliseconds, 0f, 1f);
            Invalidate();
            if (_progress >= 1f)
            {
                Finish();
            }
        }
    }

    private sealed class SetupStepIndicator : Control
    {
        private const int StepCount = 6;
        private readonly System.Windows.Forms.Timer _pulseTimer;
        private ThemePalette _palette;
        private int _step;

        internal SetupStepIndicator()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            TabStop = false;
            AccessibleRole = AccessibleRole.ProgressBar;
            _pulseTimer = new System.Windows.Forms.Timer { Interval = 33 };
            _pulseTimer.Tick += (_, _) => Invalidate();
            if (AccessibilityPreferences.AnimationsEnabled)
            {
                _pulseTimer.Start();
            }
        }

        internal int Step
        {
            get => _step;
            set
            {
                _step = Math.Clamp(value, 0, 5);
                if (IsHandleCreated)
                {
                    AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
                }
                Invalidate();
            }
        }

        internal void UpdateAccessibility(string name)
        {
            if (AccessibleName == name)
            {
                return;
            }

            AccessibleName = name;
            AccessibleDescription = name;
            if (IsHandleCreated)
            {
                AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
                AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            }
        }

        internal void ApplyTheme(ThemePalette palette)
        {
            _palette = palette;
            BackColor = palette.MenuBackground;
            Invalidate();
        }

        internal void ValidatePaintBounds()
        {
            if (Width < 8 || Height < 8)
            {
                return;
            }

            (PointF[] centers, float haloDiameter) = GetPaintGeometry();
            float radius = haloDiameter / 2f;
            foreach (PointF center in centers)
            {
                if (center.X - radius < 0f ||
                    center.X + radius > Width ||
                    center.Y - radius < 0f ||
                    center.Y + radius > Height)
                {
                    throw new InvalidOperationException(
                        "The setup progress animation extends outside its control.");
                }
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using SolidBrush brush = new(ControlDrawing.EffectiveBackColor(this));
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 8 || Height < 8)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            int diameter = 12;
            int activeDiameter = 20;
            (PointF[] centers, _) = GetPaintGeometry();
            float centerY = Height / 2f;

            for (int index = 0; index < centers.Length - 1; index++)
            {
                using Pen connector = new(
                    _step > index ? _palette.Accent : _palette.Border,
                    Math.Max(2f, DeviceDpi / 48f));
                e.Graphics.DrawLine(
                    connector,
                    centers[index].X + (diameter / 2f),
                    centerY,
                    centers[index + 1].X - (diameter / 2f),
                    centerY);
            }

            for (int index = 0; index < centers.Length; index++)
            {
                bool current = index == _step;
                bool completed = index < _step;
                int size = current ? activeDiameter : diameter;
                PointF center = centers[index];
                if (current)
                {
                    float pulse = AccessibilityPreferences.AnimationsEnabled
                        ? (MathF.Sin((Environment.TickCount64 % 1400L) * MathF.Tau / 1400f) + 1f) / 2f
                        : 0.45f;
                    float haloSize =
                        activeDiameter +
                        10f +
                        MathF.Round(pulse * 8f);
                    RectangleF halo = new(
                        center.X - (haloSize / 2),
                        center.Y - (haloSize / 2),
                        haloSize,
                        haloSize);
                    using SolidBrush haloBrush = new(Color.FromArgb(
                        22 + (int)(pulse * 30f),
                        _palette.Accent));
                    using Pen haloRing = new(
                        Color.FromArgb(50 + (int)(pulse * 45f), _palette.Accent),
                        Math.Max(1f, DeviceDpi / 96f));
                    e.Graphics.FillEllipse(haloBrush, halo);
                    e.Graphics.DrawEllipse(haloRing, halo);
                }

                RectangleF circle = new(
                    center.X - (size / 2),
                    center.Y - (size / 2),
                    size,
                    size);
                Color fill = current || completed ? _palette.Accent : _palette.MenuBackground;
                using SolidBrush fillBrush = new(fill);
                using Pen outline = new(current || completed ? _palette.Accent : _palette.SecondaryText, Math.Max(1f, DeviceDpi / 96f));
                e.Graphics.FillEllipse(fillBrush, circle);
                e.Graphics.DrawEllipse(outline, circle);
            }
        }

        private (PointF[] Centers, float HaloDiameter) GetPaintGeometry()
        {
            const float activeDiameter = 20f;
            const float haloDiameter = activeDiameter + 18f;
            const float edgeInset = (haloDiameter / 2f) + 1f;
            float usableWidth = Math.Max(0f, Width - (edgeInset * 2f));
            float spacing = usableWidth / (StepCount - 1);
            float centerY = Height / 2f;
            var centers = new PointF[StepCount];
            for (int index = 0; index < centers.Length; index++)
            {
                centers[index] = new PointF(edgeInset + (spacing * index), centerY);
            }

            return (centers, haloDiameter);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _pulseTimer.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
