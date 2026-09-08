using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace QuickZoom;

internal sealed partial class TrayContext
{
    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetKeyNameText(int lParam, StringBuilder lpString, int nSize);

    private sealed class Settings
    {
        public int ThemeMode { get; set; } = (int)TrayContext.ThemeMode.AutoSystem;
        public int UiFontSize { get; set; } = (int)TrayContext.UiFontSize.Default;
        public int StepPercent { get; set; } = 30;
        public int MaxPercent { get; set; } = 400;
        public bool MagnificationEnabled { get; set; } = true;
        public bool InvertEnabled { get; set; }
        public bool FollowCursor { get; set; } = true;
        public int DisplaySelectionMode { get; set; } = (int)TrayContext.DisplaySelectionMode.AllDisplays;
        public int ShortcutInputMode { get; set; } = (int)TrayContext.ShortcutInputMode.Both;
        public int EnableKey { get; set; } = (int)Keys.Menu;
        public int Language { get; set; } = (int)UiText.GetStartupLanguage();
        public bool InvertColors { get; set; }
        public int InvertKey { get; set; } = (int)Keys.I;
        public int FollowCursorKey { get; set; } = (int)Keys.F;
        public int InvertTrigger { get; set; } = (int)InvertTriggerKind.EnableKeyPlusMiddleClick;
        public bool SmoothZoom { get; set; } = true;
        public bool AutoDisableAt100 { get; set; } = true;
        public int Fps { get; set; } = 120;
        public bool CenterCursor { get; set; }
        public bool SuppressShortcutKeystrokes { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool SuppressAltKeyInOfficeApps { get; set; }
        [JsonIgnore]
        public bool DebugLoggingEnabled { get; set; }
        public bool WiggleSpotlightEnabled { get; set; } = true;
        public bool CursorEnhancementEnabled { get; set; }
        public int CursorScale { get; set; } = 100;
        public int CursorFillColor { get; set; } = unchecked((int)0xFFFFFFFF);
        public int CursorBorderColor { get; set; } = unchecked((int)0xFF000000);
        public bool AutoSwitchMonitor { get; set; } = true;
        public bool UseCursorMonitorSelection { get; set; }
        public List<string> SelectedMonitorDeviceNames { get; set; } = new();
        public int ZoomMode { get; set; } = (int)TrayContext.ZoomMode.Fullscreen;
        public int LensSize { get; set; } = 360;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int LensWidth { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int LensHeight { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int LensZoomPercent { get; set; }
        public int LensShape { get; set; } = (int)TrayContext.LensShape.Rectangle;
        public int DockPosition { get; set; } = (int)TrayContext.DockPosition.Top;
        public int DockSizePercent { get; set; } = 25;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int DockZoomPercent { get; set; }
        public int TrackingSource { get; set; } = (int)TrayContext.TrackingSource.MouseCursor;
    }

    internal static bool IsKnownSetting(string name) => typeof(Settings).GetProperty(name) != null;

    private static Settings CreateDefaultSettings()
    {
        return new Settings
        {
            ThemeMode = (int)ThemeMode.AutoSystem,
            UiFontSize = (int)UiFontSize.Default,
            StepPercent = 30,
            MaxPercent = 400,
            MagnificationEnabled = true,
            InvertEnabled = false,
            FollowCursor = true,
            DisplaySelectionMode = (int)DisplaySelectionMode.AllDisplays,
            ShortcutInputMode = (int)ShortcutInputMode.Both,
            EnableKey = (int)Keys.Menu,
            Language = (int)UiText.GetDefaultLanguage(),
            InvertColors = false,
            InvertKey = (int)Keys.I,
            FollowCursorKey = (int)Keys.F,
            InvertTrigger = (int)InvertTriggerKind.EnableKeyPlusMiddleClick,
            SmoothZoom = true,
            AutoDisableAt100 = true,
            Fps = 120,
            CenterCursor = false,
            SuppressShortcutKeystrokes = false,
            DebugLoggingEnabled = false,
            WiggleSpotlightEnabled = true,
            CursorEnhancementEnabled = false,
            CursorScale = 100,
            CursorFillColor = Color.White.ToArgb(),
            CursorBorderColor = Color.Black.ToArgb(),
            AutoSwitchMonitor = true,
            UseCursorMonitorSelection = false,
            SelectedMonitorDeviceNames = new List<string>(),
            ZoomMode = (int)ZoomMode.Fullscreen,
            LensSize = 360,
            LensShape = (int)LensShape.Rectangle,
            DockPosition = (int)DockPosition.Top,
            DockSizePercent = 25,
            TrackingSource = (int)TrackingSource.MouseCursor
        };
    }

    private void ApplySettingsModel(Settings s)
    {
        _stepPercent = Math.Clamp(s.StepPercent, 1, 200);
        _maxPercent = Math.Clamp(s.MaxPercent, 150, 750);
        _themeMode = Enum.IsDefined(typeof(ThemeMode), s.ThemeMode)
            ? (ThemeMode)s.ThemeMode
            : ThemeMode.AutoSystem;
        _uiFontSize = Enum.IsDefined(typeof(UiFontSize), s.UiFontSize)
            ? (UiFontSize)s.UiFontSize
            : UiFontSize.Default;
        ApplyUiFontScale();
        _enabled = s.MagnificationEnabled;
        _invertEnabled = s.InvertEnabled;
        _followCursor = s.FollowCursor;
        _displaySelectionMode = Enum.IsDefined(typeof(DisplaySelectionMode), s.DisplaySelectionMode)
            ? (DisplaySelectionMode)s.DisplaySelectionMode
            : DisplaySelectionMode.AllDisplays;
        _autoSwitchMonitor = s.AutoSwitchMonitor;
        _shortcutInputMode = Enum.IsDefined(typeof(ShortcutInputMode), s.ShortcutInputMode)
            ? (ShortcutInputMode)s.ShortcutInputMode
            : ShortcutInputMode.Both;
        _enableKey = (Keys)s.EnableKey;
        _language = Enum.IsDefined(typeof(UiLanguage), s.Language)
            ? (UiLanguage)s.Language
            : UiText.GetDefaultLanguage();
        _invertColors = s.InvertColors;
        _invertKey = (Keys)s.InvertKey;
        _followCursorKey = s.FollowCursorKey == 0 ? Keys.F : (Keys)s.FollowCursorKey;
        _invertTrigger = Enum.IsDefined(typeof(InvertTriggerKind), s.InvertTrigger)
            ? (InvertTriggerKind)s.InvertTrigger
            : InvertTriggerKind.EnableKeyPlusMiddleClick;
        _smoothZoom = s.SmoothZoom;
        _autoDisableAt100 = s.AutoDisableAt100;
        _fps = NormalizeFpsSetting(s.Fps);
        _centerCursor = s.CenterCursor;
        _suppressShortcutKeystrokes = s.SuppressShortcutKeystrokes || s.SuppressAltKeyInOfficeApps;
        _debugLoggingEnabled = false;
        ErrorLog.Configure(_debugLoggingEnabled, AppInfo.VersionHash);
        _wiggleSpotlightEnabled = s.WiggleSpotlightEnabled;
        _cursorEnhancementEnabled = s.CursorEnhancementEnabled;
        _cursorScale = NormalizeCursorScale(s.CursorScale);
        _cursorFillColorArgb = NormalizeCursorColor(s.CursorFillColor, Color.White);
        _cursorBorderColorArgb = NormalizeCursorColor(s.CursorBorderColor, Color.Black);
        _useCursorMonitorSelection = s.UseCursorMonitorSelection;
        _zoomMode = Enum.IsDefined(typeof(ZoomMode), s.ZoomMode) ? (ZoomMode)s.ZoomMode : ZoomMode.Fullscreen;
        _lensSize = NormalizeLensSize(s.LensSize > 0 ? s.LensSize : s.LensWidth);
        _lensShape = Enum.IsDefined(typeof(LensShape), s.LensShape) ? (LensShape)s.LensShape : LensShape.Rectangle;
        _dockPosition = Enum.IsDefined(typeof(DockPosition), s.DockPosition) ? (DockPosition)s.DockPosition : DockPosition.Top;
        _dockSizePercent = NormalizeDockSizePercent(s.DockSizePercent);
        _trackingSource = Enum.IsDefined(typeof(TrackingSource), s.TrackingSource) ? (TrackingSource)s.TrackingSource : TrackingSource.MouseCursor;
        if (!_invertEnabled)
        {
            _invertColors = false;
        }

        _enableKeyPressed = false;
        _invertKeyPressed = false;
        _followCursorKeyPressed = false;
        _controlKeyPressed = false;
        _altGrPressed = false;
        ResetEnableKeySuppressionState();
        _suppressedShortcutKeyUps.Clear();
        _wheelDeltaRemainder = 0;
        _pendingExitConfirmation = false;
        _lockedScreen = null;
        _useCursorMonitorSelection = _displaySelectionMode == DisplaySelectionMode.MonitorUnderCursor;

        _selectedMonitorDeviceNames.Clear();
        foreach (string name in (s.SelectedMonitorDeviceNames ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrWhiteSpace(n)))
        {
            _selectedMonitorDeviceNames.Add(name);
        }

        EnsureSelectedMonitorsValid();
        ApplyThemePreference(force: true);
        ApplyFps();
        UpdateFollowTimerState();
    }

    private void LoadSettings()
    {
        try
        {
            if (!_screenshotMode)
            {
                LocalStorage.RunAsUser(() =>
                {
                    LocalStorage.RequireLocalPath(_settingsPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
                });

                if (!File.Exists(_settingsPath) && File.Exists(_legacySettingsPath))
                {
                    File.Copy(_legacySettingsPath, _settingsPath, overwrite: false);
                }
            }

            string settingsPath = File.Exists(_settingsPath)
                ? _settingsPath
                : _screenshotMode && File.Exists(_legacySettingsPath)
                    ? _legacySettingsPath
                    : _settingsPath;
            if (!File.Exists(settingsPath))
            {
                return;
            }

            var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsPath));
            if (s == null)
            {
                return;
            }

            ApplySettingsModel(s);
        }
        catch (JsonException ex)
        {
            if (!_screenshotMode)
            {
                TryQuarantineCorruptSettingsFile();
            }
            ErrorLog.Write("LoadSettings", ex);
            TryApplyDefaultSettingsAfterLoadFailure();
        }
        catch (Exception ex)
        {
            ErrorLog.Write("LoadSettings", ex);
            TryApplyDefaultSettingsAfterLoadFailure();
        }
        UpdateMenuLabels();
    }

    private void TryApplyDefaultSettingsAfterLoadFailure()
    {
        try
        {
            ApplySettingsModel(CreateDefaultSettings());
        }
        catch (Exception ex)
        {
            ErrorLog.WriteThrottled("LoadSettings.DefaultFallback", ex);
        }
    }

    private void SaveSettings()
    {
        if (_screenshotMode || _setupPracticeMode)
        {
            UpdateMenuLabels();
            return;
        }

        Settings snapshot = CreateSettingsSnapshot();
        lock (_settingsSaveSync)
        {
            _pendingSettingsSave = snapshot;
        }

        void ScheduleSave()
        {
            if (_settingsSaveTimer == null)
            {
                _settingsSaveTimer = new System.Windows.Forms.Timer { Interval = 400 };
                _settingsSaveTimer.Tick += OnSettingsSaveTimerTick;
            }

            _settingsSaveTimer.Stop();
            _settingsSaveTimer.Start();
        }

        if (_uiInvoker != null && !_uiInvoker.IsDisposed && _uiInvoker.InvokeRequired)
        {
            _uiInvoker.BeginInvoke((MethodInvoker)ScheduleSave);
        }
        else
        {
            ScheduleSave();
        }

        UpdateMenuLabels();
    }

    private void OnSettingsSaveTimerTick(object? sender, EventArgs e)
    {
        _settingsSaveTimer?.Stop();
        Settings? snapshot;
        lock (_settingsSaveSync)
        {
            snapshot = _pendingSettingsSave;
            _pendingSettingsSave = null;
        }

        if (snapshot == null)
        {
            return;
        }

        _settingsSaveTask = Task.Run(() => WriteSettingsSnapshot(snapshot));
    }

    private void FlushSettingsSave()
    {
        if (_screenshotMode || _setupPracticeMode)
        {
            lock (_settingsSaveSync)
            {
                _pendingSettingsSave = null;
            }
            _settingsSaveTimer?.Stop();
            return;
        }

        Settings? snapshot;
        lock (_settingsSaveSync)
        {
            snapshot = _pendingSettingsSave;
            _pendingSettingsSave = null;
        }

        _settingsSaveTimer?.Stop();
        try
        {
            _settingsSaveTask?.Wait(1000);
        }
        catch (Exception ex)
        {
            ErrorLog.WriteThrottled("SaveSettings.FlushWait", ex);
        }

        if (snapshot != null)
        {
            WriteSettingsSnapshot(snapshot);
        }
    }

    private Settings CreateSettingsSnapshot()
    {
        return new Settings
        {
            ThemeMode = (int)_themeMode,
            UiFontSize = (int)_uiFontSize,
            StepPercent = _stepPercent,
            MaxPercent = _maxPercent,
            MagnificationEnabled = _enabled,
            InvertEnabled = _invertEnabled,
            FollowCursor = _followCursor,
            DisplaySelectionMode = (int)_displaySelectionMode,
            AutoSwitchMonitor = _autoSwitchMonitor,
            ShortcutInputMode = (int)_shortcutInputMode,
            EnableKey = (int)_enableKey,
            Language = (int)_language,
            InvertColors = _invertColors,
            InvertKey = (int)_invertKey,
            FollowCursorKey = (int)_followCursorKey,
            InvertTrigger = (int)_invertTrigger,
            SmoothZoom = _smoothZoom,
            AutoDisableAt100 = _autoDisableAt100,
            Fps = _fps,
            CenterCursor = _centerCursor,
            SuppressShortcutKeystrokes = _suppressShortcutKeystrokes,
            DebugLoggingEnabled = _debugLoggingEnabled,
            WiggleSpotlightEnabled = _wiggleSpotlightEnabled,
            CursorEnhancementEnabled = _cursorEnhancementEnabled,
            CursorScale = _cursorScale,
            CursorFillColor = _cursorFillColorArgb,
            CursorBorderColor = _cursorBorderColorArgb,
            UseCursorMonitorSelection = _useCursorMonitorSelection,
            SelectedMonitorDeviceNames = _selectedMonitorDeviceNames.ToList(),
            ZoomMode = (int)_zoomMode,
            LensSize = _lensSize,
            LensShape = (int)_lensShape,
            DockPosition = (int)_dockPosition,
            DockSizePercent = _dockSizePercent,
            TrackingSource = (int)_trackingSource
        };
    }

    private static int NormalizeFpsSetting(int fps)
    {
        if (fps == UnlimitedFps || fps > _fpsOptions[^1])
        {
            return UnlimitedFps;
        }

        return _fpsOptions.Contains(fps) ? fps : _fpsOptions[0];
    }

    private static int NormalizeLensSize(int size)
    {
        int clamped = Math.Clamp(size, 100, 1400);
        if (clamped >= 1400)
        {
            return 1400;
        }

        return 100 + (int)Math.Round((clamped - 100) / 40.0, MidpointRounding.AwayFromZero) * 40;
    }

    private static int NormalizeDockSizePercent(int sizePercent)
    {
        int clamped = Math.Clamp(sizePercent, 10, 50);
        return 10 + (int)Math.Round((clamped - 10) / 5.0) * 5;
    }

    private void WriteSettingsSnapshot(Settings s)
    {
        if (_screenshotMode)
        {
            return;
        }

        try
        {
            lock (_settingsWriteSync)
            {
                FilePersistence.WriteAllTextAtomic(
                    _settingsPath,
                    JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write("SaveSettings", ex);
        }

    }

    private void ResetSettingsToDefaults()
    {
        ApplySettingsModel(CreateDefaultSettings());
        _animAnchorValid = false;
        _animTimer?.Stop();
        _zoomPercent = 100;
        _animTargetPercent = 100;
        DisableMagAndReset();
        ApplyCursorEnhancementIfNeeded();
        SaveSettings();
        FlushSettingsSave();
        RefreshMenuAndTrayUi(rebuildPopup: true);

        if (_settingsWindow != null && !_settingsWindow.IsDisposed)
        {
            RefreshSettingsWindow(SettingsPage.General);
        }
    }

    private void UpdateMenuLabels()
    {
        // Reserved for future live-menu label updates. The current tray UI is rebuilt instead.
    }

    private static int NormalizeCursorColor(int argb, Color fallback)
    {
        Color color = Color.FromArgb(argb);
        return color.A == 0 ? fallback.ToArgb() : Color.FromArgb(255, color).ToArgb();
    }

    private static int NormalizeCursorScale(int scale)
    {
        if (scale >= 1 && scale <= 5)
        {
            scale *= 100;
        }

        return Math.Clamp(scale, CursorScaleMinimum, CursorScaleMaximum);
    }

    private string KeyLabel(Keys key)
    {
        return key switch
        {
            Keys.ControlKey => L("Common.KeyCtrl"),
            Keys.Menu => L("Common.KeyAlt"),
            Keys.RMenu => L("Common.KeyAltGr"),
            Keys.ShiftKey => L("Common.KeyShift"),
            Keys.LWin or Keys.RWin => L("Common.KeyWin"),
            (Keys)FnVirtualKey => L("Common.KeyFn"),
            Keys.Enter or Keys.Return => L("Common.KeyEnter"),
            Keys.Oemcomma => ",",
            Keys.OemPeriod => ".",
            Keys.OemMinus or Keys.Subtract => "-",
            _ => TryGetNativeKeyLabel(key) ?? key.ToString()
        };
    }

    private static string? TryGetNativeKeyLabel(Keys key)
    {
        if (!IsOemKey(key))
        {
            return null;
        }

        uint scanCode = MapVirtualKey((uint)key, 0);
        if (scanCode == 0)
        {
            return null;
        }

        var buffer = new StringBuilder(32);
        int length = GetKeyNameText((int)(scanCode << 16), buffer, buffer.Capacity);
        string label = length > 0 ? buffer.ToString() : string.Empty;
        return string.IsNullOrWhiteSpace(label) ? null : label;
    }

    private static bool IsOemKey(Keys key)
    {
        return key is Keys.Oem1 or Keys.Oem3 or Keys.Oem4 or Keys.Oem5 or Keys.Oem6 or Keys.Oem7 or Keys.Oem8 or Keys.Oem102;
    }

    private string ShortcutInputModeLabel(ShortcutInputMode mode) => mode switch
    {
        ShortcutInputMode.KeyboardOnly => L("Settings.ShortcutModeKeyboardOnly"),
        ShortcutInputMode.MouseOnly => L("Settings.ShortcutModeMouseOnly"),
        _ => L("Settings.ShortcutModeBoth")
    };

    private void ApplyUiFontScale()
    {
        ControlDrawing.UiFontScale = _uiFontSize switch
        {
            UiFontSize.Large => 1.14f,
            UiFontSize.ExtraLarge => 1.28f,
            _ => 1f
        };
    }

    private string UiFontSizeLabel(UiFontSize size) => size switch
    {
        UiFontSize.Large => L("Settings.FontSizeLarge"),
        UiFontSize.ExtraLarge => L("Settings.FontSizeExtraLarge"),
        _ => L("Settings.FontSizeDefault")
    };

    private string[] BuildUiFontSizeItems() =>
    [
        L("Settings.FontSizeDefault"),
        L("Settings.FontSizeLarge"),
        L("Settings.FontSizeExtraLarge")
    ];

    private UiFontSize ParseUiFontSize(string value)
    {
        if (string.Equals(value, L("Settings.FontSizeLarge"), StringComparison.Ordinal))
        {
            return UiFontSize.Large;
        }

        if (string.Equals(value, L("Settings.FontSizeExtraLarge"), StringComparison.Ordinal))
        {
            return UiFontSize.ExtraLarge;
        }

        return UiFontSize.Default;
    }

    private ShortcutInputMode ParseShortcutInputMode(string value)
    {
        if (string.Equals(value, L("Settings.ShortcutModeKeyboardOnly"), StringComparison.Ordinal))
        {
            return ShortcutInputMode.KeyboardOnly;
        }

        if (string.Equals(value, L("Settings.ShortcutModeMouseOnly"), StringComparison.Ordinal))
        {
            return ShortcutInputMode.MouseOnly;
        }

        return ShortcutInputMode.Both;
    }

    private bool KeyboardShortcutsAllowed() => _shortcutInputMode != ShortcutInputMode.MouseOnly;

    private bool MouseShortcutsAllowed() => _shortcutInputMode != ShortcutInputMode.KeyboardOnly;

    private string[] BuildShortcutModeItems() =>
    [
        L("Settings.ShortcutModeBoth"),
        L("Settings.ShortcutModeKeyboardOnly"),
        L("Settings.ShortcutModeMouseOnly")
    ];

    private string[] BuildPrimaryKeyItems() => BuildKeyItemLabels(new[]
    {
        Keys.Menu,
        Keys.ControlKey,
        Keys.ShiftKey,
        Keys.LWin,
        Keys.A,
        Keys.Q,
        Keys.Z,
        Keys.F1,
        Keys.F2,
        Keys.F3,
        Keys.F4,
        Keys.F5,
        Keys.F6,
        Keys.F7,
        Keys.F8,
        Keys.F9,
        Keys.F10,
        Keys.F11,
        Keys.F12,
        Keys.Oem1,
        Keys.Oem7,
        Keys.Oem4,
        Keys.Space
    }, _enableKey);

    private string[] BuildSecondaryKeyItems(Keys current) => BuildKeyItemLabels(new[]
    {
        Keys.I,
        Keys.F,
        Keys.C,
        Keys.X,
        Keys.Z,
        Keys.Q,
        Keys.E,
        Keys.R,
        Keys.T,
        Keys.G,
        Keys.F1,
        Keys.F2,
        Keys.F3,
        Keys.F4,
        Keys.F5,
        Keys.F6,
        Keys.F7,
        Keys.F8,
        Keys.F9,
        Keys.F10,
        Keys.F11,
        Keys.F12,
        Keys.Oem1,
        Keys.Oem7,
        Keys.Oem4
    }, current);

    private string[] BuildKeyItemLabels(IEnumerable<Keys> defaults, Keys current)
    {
        var items = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void AddKey(Keys key)
        {
            string label = KeyLabel(key);
            if (seen.Add(label))
            {
                items.Add(label);
            }
        }

        foreach (Keys key in defaults)
        {
            AddKey(key);
        }

        AddKey(current);
        return items.ToArray();
    }

    private Keys ParseKeySelection(string value, Keys fallback, IEnumerable<Keys> candidates)
    {
        foreach (Keys key in candidates)
        {
            if (string.Equals(value, KeyLabel(key), StringComparison.Ordinal))
            {
                return key;
            }
        }

        return fallback;
    }

    private string InvertTriggerLabel() => _invertTrigger switch
    {
        InvertTriggerKind.EnableKeyPlusMiddleClick => L("Settings.Trigger.EnableMiddle", KeyLabel(_enableKey)),
        InvertTriggerKind.EnableKeyPlusXButton1 => L("Settings.Trigger.EnableX1", KeyLabel(_enableKey)),
        InvertTriggerKind.EnableKeyPlusXButton2 => L("Settings.Trigger.EnableX2", KeyLabel(_enableKey)),
        InvertTriggerKind.CustomKey => KeyLabel(_invertKey),
        _ => L("Common.Unknown")
    };

    private void TryQuarantineCorruptSettingsFile()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return;
            }

            // Replace invalid data without retaining an unbounded copy of its contents.
            FilePersistence.WriteAllTextAtomic(_settingsPath, "{}");
        }
        catch (Exception ex)
        {
            ErrorLog.Write("LoadSettings", "Could not quarantine corrupt settings file: " + ex.Message);
        }
    }
}
