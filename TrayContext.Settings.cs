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
        public int StepPercent { get; set; } = 25;
        public int MaxPercent { get; set; } = 400;
        public bool MagnificationEnabled { get; set; } = true;
        public bool InvertEnabled { get; set; }
        public bool FollowCursor { get; set; } = true;
        public int EnableKey { get; set; } = (int)Keys.Menu;
        public int Language { get; set; } = (int)UiText.GetDefaultLanguage();
        public bool InvertColors { get; set; }
        public int InvertKey { get; set; } = (int)Keys.I;
        public int InvertTrigger { get; set; } = (int)InvertTriggerKind.EnableKeyPlusMiddleClick;
        public bool SmoothZoom { get; set; } = true;
        public bool AutoDisableAt100 { get; set; } = true;
        public int Fps { get; set; } = 120;
        public bool CenterCursor { get; set; }
        public bool AutoSwitchMonitor { get; set; } = true;
        public bool UseCursorMonitorSelection { get; set; }
        public List<string> SelectedMonitorDeviceNames { get; set; } = new();
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

            _stepPercent = Math.Clamp(s.StepPercent, 5, 100);
            _maxPercent = Math.Clamp(s.MaxPercent, 200, 500);
            _themeMode = Enum.IsDefined(typeof(ThemeMode), s.ThemeMode)
                ? (ThemeMode)s.ThemeMode
                : ThemeMode.AutoSystem;
            _enabled = s.MagnificationEnabled;
            _invertEnabled = s.InvertEnabled;
            _followCursor = s.FollowCursor;
            _autoSwitchMonitor = s.AutoSwitchMonitor;
            _enableKey = (Keys)s.EnableKey;
            _language = Enum.IsDefined(typeof(UiLanguage), s.Language)
                ? (UiLanguage)s.Language
                : UiText.GetDefaultLanguage();
            _invertColors = s.InvertColors;
            _invertKey = (Keys)s.InvertKey;
            _invertTrigger = Enum.IsDefined(typeof(InvertTriggerKind), s.InvertTrigger)
                ? (InvertTriggerKind)s.InvertTrigger
                : InvertTriggerKind.EnableKeyPlusMiddleClick;
            _smoothZoom = s.SmoothZoom;
            _autoDisableAt100 = s.AutoDisableAt100;
            _fps = Math.Clamp(s.Fps, 60, 360);
            _centerCursor = s.CenterCursor;
            _useCursorMonitorSelection = s.UseCursorMonitorSelection;
            if (!_invertEnabled)
            {
                _invertColors = false;
            }
            _selectedMonitorDeviceNames.Clear();
            foreach (string name in s.SelectedMonitorDeviceNames.Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                _selectedMonitorDeviceNames.Add(name);
            }
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

        EnsureSelectedMonitorsValid();

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
                StepPercent = _stepPercent,
                MaxPercent = _maxPercent,
                MagnificationEnabled = _enabled,
                InvertEnabled = _invertEnabled,
                FollowCursor = _followCursor,
                AutoSwitchMonitor = _autoSwitchMonitor,
                EnableKey = (int)_enableKey,
                Language = (int)_language,
                InvertColors = _invertColors,
                InvertKey = (int)_invertKey,
                InvertTrigger = (int)_invertTrigger,
                SmoothZoom = _smoothZoom,
                AutoDisableAt100 = _autoDisableAt100,
                Fps = _fps,
                CenterCursor = _centerCursor,
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

    private void UpdateMenuLabels()
    {
        // Reserved for future live-menu label updates. The current tray UI is rebuilt instead.
    }

    private static string KeyLabel(Keys key) => key switch
    {
        Keys.ControlKey => "Ctrl",
        Keys.Menu => "Alt",
        Keys.ShiftKey => "Shift",
        Keys.LWin or Keys.RWin => "Win",
        _ => key.ToString()
    };

    private string InvertTriggerLabel() => _invertTrigger switch
    {
        InvertTriggerKind.EnableKeyPlusMiddleClick => InvertTriggerTextForCurrentEnableKey(L("Settings.Trigger.EnableMiddle")),
        InvertTriggerKind.EnableKeyPlusXButton1 => InvertTriggerTextForCurrentEnableKey(L("Settings.Trigger.EnableX1")),
        InvertTriggerKind.EnableKeyPlusXButton2 => InvertTriggerTextForCurrentEnableKey(L("Settings.Trigger.EnableX2")),
        InvertTriggerKind.CustomKey => KeyLabel(_invertKey),
        _ => "Unknown"
    };

    private string InvertTriggerTextForCurrentEnableKey(string template)
    {
        return template
            .Replace("Enable Key", KeyLabel(_enableKey), StringComparison.Ordinal)
            .Replace("Aktiveringstast", KeyLabel(_enableKey), StringComparison.Ordinal);
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
