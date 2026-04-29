using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace QuickZoom;

internal sealed partial class TrayContext
{
    private sealed class Settings
    {
        public int ThemeMode { get; set; } = (int)TrayContext.ThemeMode.AutoSystem;
        public int UiFontSize { get; set; } = (int)TrayContext.UiFontSize.Large;
        public int StepPercent { get; set; } = 25;
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
        public bool WiggleSpotlightEnabled { get; set; } = true;
        public bool AutoSwitchMonitor { get; set; } = true;
        public bool UseCursorMonitorSelection { get; set; }
        public List<string> SelectedMonitorDeviceNames { get; set; } = new();
    }

    private static Settings CreateDefaultSettings()
    {
        return new Settings
        {
            ThemeMode = (int)ThemeMode.AutoSystem,
            UiFontSize = (int)UiFontSize.Large,
            StepPercent = 25,
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
            WiggleSpotlightEnabled = true,
            AutoSwitchMonitor = true,
            UseCursorMonitorSelection = false,
            SelectedMonitorDeviceNames = new List<string>()
        };
    }

    private void ApplySettingsModel(Settings s)
    {
        _stepPercent = Math.Clamp(s.StepPercent, 5, 100);
        _maxPercent = Math.Clamp(s.MaxPercent, 200, 500);
        _themeMode = Enum.IsDefined(typeof(ThemeMode), s.ThemeMode)
            ? (ThemeMode)s.ThemeMode
            : ThemeMode.AutoSystem;
        _uiFontSize = Enum.IsDefined(typeof(UiFontSize), s.UiFontSize)
            ? (UiFontSize)s.UiFontSize
            : UiFontSize.Large;
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
        _fps = Math.Clamp(s.Fps, 60, 360);
        _centerCursor = s.CenterCursor;
        _wiggleSpotlightEnabled = s.WiggleSpotlightEnabled;
        _useCursorMonitorSelection = s.UseCursorMonitorSelection;
        if (!_invertEnabled)
        {
            _invertColors = false;
        }

        _enableKeyPressed = false;
        _invertKeyPressed = false;
        _followCursorKeyPressed = false;
        _wheelDeltaRemainder = 0;
        _pendingExitConfirmation = false;
        _lockedScreen = null;
        _useCursorMonitorSelection = _displaySelectionMode == DisplaySelectionMode.MonitorUnderCursor;

        _selectedMonitorDeviceNames.Clear();
        foreach (string name in s.SelectedMonitorDeviceNames.Where(n => !string.IsNullOrWhiteSpace(n)))
        {
            _selectedMonitorDeviceNames.Add(name);
        }

        EnsureSelectedMonitorsValid();
        ApplyThemePreference(force: true);
        ApplyFps();
        if (_followCursor)
        {
            _followTimer?.Start();
        }
        else
        {
            _followTimer?.Stop();
        }
    }

    private void LoadSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);

            if (!File.Exists(_settingsPath) && File.Exists(_legacySettingsPath))
            {
                File.Copy(_legacySettingsPath, _settingsPath, overwrite: false);
            }

            if (!File.Exists(_settingsPath))
            {
                return;
            }

            var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(_settingsPath));
            if (s == null)
            {
                return;
            }

            ApplySettingsModel(s);
        }
        catch (JsonException ex)
        {
            TryQuarantineCorruptSettingsFile();
            ErrorLog.Write("LoadSettings", ex);
        }
        catch (Exception ex)
        {
            ErrorLog.Write("LoadSettings", ex);
        }
        UpdateMenuLabels();
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);

            var s = new Settings
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
                WiggleSpotlightEnabled = _wiggleSpotlightEnabled,
                UseCursorMonitorSelection = _useCursorMonitorSelection,
                SelectedMonitorDeviceNames = _selectedMonitorDeviceNames.ToList()
            };

            FilePersistence.WriteAllTextAtomic(
                _settingsPath,
                JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            ErrorLog.Write("SaveSettings", ex);
        }

        UpdateMenuLabels();
    }

    private void ResetSettingsToDefaults()
    {
        ApplySettingsModel(CreateDefaultSettings());
        _animAnchorValid = false;
        _animTimer?.Stop();
        _zoomPercent = 100;
        _animTargetPercent = 100;
        DisableMagAndReset();
        SaveSettings();
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

    private string KeyLabel(Keys key) => key switch
    {
        Keys.ControlKey => L("Common.KeyCtrl"),
        Keys.Menu => L("Common.KeyAlt"),
        Keys.ShiftKey => L("Common.KeyShift"),
        Keys.LWin or Keys.RWin => L("Common.KeyWin"),
        _ => key.ToString()
    };

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
        Keys.F12
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
        InvertTriggerKind.EnableKeyPlusMiddleClick => InvertTriggerTextForCurrentEnableKey(L("Settings.Trigger.EnableMiddle")),
        InvertTriggerKind.EnableKeyPlusXButton1 => InvertTriggerTextForCurrentEnableKey(L("Settings.Trigger.EnableX1")),
        InvertTriggerKind.EnableKeyPlusXButton2 => InvertTriggerTextForCurrentEnableKey(L("Settings.Trigger.EnableX2")),
        InvertTriggerKind.CustomKey => KeyLabel(_invertKey),
        _ => L("Common.Unknown")
    };

    private string InvertTriggerTextForCurrentEnableKey(string templateKey)
    {
        return string.Format(L(templateKey), KeyLabel(_enableKey));
    }

    private void TryQuarantineCorruptSettingsFile()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return;
            }

            string backupPath = Path.Combine(
                Path.GetDirectoryName(_settingsPath)!,
                Path.GetFileNameWithoutExtension(_settingsPath) + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + Path.GetExtension(_settingsPath));

            File.Move(_settingsPath, backupPath);
        }
        catch (Exception ex)
        {
            ErrorLog.Write("LoadSettings", "Could not quarantine corrupt settings file: " + ex.Message);
        }
    }
}
