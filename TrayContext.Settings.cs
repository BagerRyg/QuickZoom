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
        public int StepPercent { get; set; } = 15;
        public int MaxPercent { get; set; } = 300;
        public bool FollowCursor { get; set; } = true;
        public int EnableKey { get; set; } = (int)Keys.ControlKey;
        public bool SmoothZoom { get; set; } = true;
        public bool AutoDisableAt100 { get; set; } = true;
        public int Fps { get; set; } = 60;
        public bool CenterCursor { get; set; }
        public bool AutoSwitchMonitor { get; set; } = true;
        public List<string> SelectedMonitorDeviceNames { get; set; } = new();
    }

    private void LoadSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);

            if (!File.Exists(_settingsPath))
            {
                return;
            }

            var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(_settingsPath));
            if (s == null)
            {
                return;
            }

            _stepPercent = Math.Clamp(s.StepPercent, 1, 100);
            _maxPercent = Math.Clamp(s.MaxPercent, 150, 600);
            _followCursor = s.FollowCursor;
            _autoSwitchMonitor = s.AutoSwitchMonitor;
            _enableKey = (Keys)s.EnableKey;
            _smoothZoom = s.SmoothZoom;
            _autoDisableAt100 = s.AutoDisableAt100;
            _fps = Math.Clamp(s.Fps, 5, 240);
            _centerCursor = s.CenterCursor;
            _selectedMonitorDeviceNames.Clear();
            foreach (string name in s.SelectedMonitorDeviceNames.Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                _selectedMonitorDeviceNames.Add(name);
            }
        }
        catch
        {
            // Ignore settings errors and keep defaults.
        }

        EnsureSelectedMonitorsValid();

        UpdateMenuLabels();
    }

    private void SaveSettings()
    {
        try
        {
            var s = new Settings
            {
                StepPercent = _stepPercent,
                MaxPercent = _maxPercent,
                FollowCursor = _followCursor,
                AutoSwitchMonitor = _autoSwitchMonitor,
                EnableKey = (int)_enableKey,
                SmoothZoom = _smoothZoom,
                AutoDisableAt100 = _autoDisableAt100,
                Fps = _fps,
                CenterCursor = _centerCursor,
                SelectedMonitorDeviceNames = _selectedMonitorDeviceNames.ToList()
            };

            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Ignore settings errors to keep app responsive.
        }

        UpdateMenuLabels();
    }

    private void UpdateMenuLabels()
    {
        if (_stepItem != null)
        {
            _stepItem.Text = $"Zoom Step (current: {_stepPercent}%)";
        }

        if (_maxItem != null)
        {
            _maxItem.Text = $"Max Zoom (current: {_maxPercent}%)";
        }

        if (_enableKeyItem != null)
        {
            _enableKeyItem.Text = $"Enable Key (hold): {KeyLabel(_enableKey)}";
        }

        RefreshFpsMenuChecks();
    }

    private static string KeyLabel(Keys key) => key switch
    {
        Keys.ControlKey => "Ctrl",
        Keys.Menu => "Alt",
        Keys.ShiftKey => "Shift",
        Keys.LWin or Keys.RWin => "Win",
        _ => key.ToString()
    };
}
