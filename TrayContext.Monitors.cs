using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace QuickZoom;

internal sealed partial class TrayContext
{
    private enum DisplaySelectionMode
    {
        AllDisplays = 0,
        MonitorUnderCursor = 1,
        CustomSelection = 2
    }

    private void SubscribeDisplayChanges()
    {
        try
        {
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }
        catch
        {
            // Best effort.
        }
    }

    private void UnsubscribeDisplayChanges()
    {
        try
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        }
        catch
        {
            // Best effort.
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        RunOnUiThread(() =>
        {
            EnsureSelectedMonitorsValid();
            EnsureLockedScreenStillValid();
            _monitorLayoutDirty = true;
            PopulateDisplayOptionsHost();
            UpdateTrayPopupState();
            RefreshDisplaySettingsUi();

            if (_magActive)
            {
                ApplyTransformCurrentPoint();
            }
        });
    }

    private void EnsureSelectedMonitorsValid()
    {
        var availableNames = new HashSet<string>(
            Screen.AllScreens.Select(s => s.DeviceName),
            StringComparer.OrdinalIgnoreCase);

        _selectedMonitorDeviceNames.RemoveWhere(name => !availableNames.Contains(name));

        if (_selectedMonitorDeviceNames.Count == 0)
        {
            foreach (Screen screen in Screen.AllScreens)
            {
                _selectedMonitorDeviceNames.Add(screen.DeviceName);
            }
        }

        if (_displaySelectionMode == DisplaySelectionMode.MonitorUnderCursor)
        {
            _useCursorMonitorSelection = true;
        }
        else
        {
            _useCursorMonitorSelection = false;
        }
    }

    private void EnsureLockedScreenStillValid()
    {
        if (_lockedScreen == null)
        {
            return;
        }

        bool exists = Screen.AllScreens.Any(screen =>
            string.Equals(screen.DeviceName, _lockedScreen.DeviceName, StringComparison.OrdinalIgnoreCase));

        if (!exists)
        {
            _lockedScreen = null;
        }
    }

    private void PopulateDisplayOptionsHost()
    {
        if (_displayOptionsHost == null)
        {
            return;
        }

        ThemePalette palette = CurrentTheme;
        EnsureSelectedMonitorsValid();
        _displayOptionsHost.SuspendLayout();
        _displayOptionsHost.Controls.Clear();
        int rowWidth = Math.Max(
            ControlDrawing.ScaleLogical(_displayOptionsHost, 180),
            (_displayOptionsHost.Width > 0 ? _displayOptionsHost.Width : _displayOptionsHost.MinimumSize.Width) - _displayOptionsHost.Padding.Horizontal);

        var cursorRow = new TrayMenuRow(palette, L("Tray.MonitorUnderCursor"), _useCursorMonitorSelection ? L("Tray.DisplaySelected") : string.Empty)
        {
            Active = _displaySelectionMode == DisplaySelectionMode.MonitorUnderCursor,
            Width = rowWidth
        };
        cursorRow.ActionRequested += (_, _) =>
        {
            _displaySelectionMode = DisplaySelectionMode.MonitorUnderCursor;
            _useCursorMonitorSelection = true;
            _lockedScreen = null;
            _monitorLayoutDirty = true;
            SaveSettings();
            UpdateTrayPopupState();
            RefreshDisplaySettingsUi();
            ApplyTransformCurrentPoint();
        };
        _displayOptionsHost.Controls.Add(cursorRow);

        List<Screen> screens = GetOrderedScreens();
        bool allDisplaysSelected = _displaySelectionMode == DisplaySelectionMode.AllDisplays;

        var allRow = new TrayMenuRow(palette, L("Tray.AllDisplays"), allDisplaysSelected ? L("Tray.DisplaySelected") : string.Empty)
        {
            Active = allDisplaysSelected,
            Width = rowWidth
        };
        allRow.ActionRequested += (_, _) =>
        {
            _displaySelectionMode = DisplaySelectionMode.AllDisplays;
            _useCursorMonitorSelection = false;
            foreach (Screen screen in screens)
            {
                _selectedMonitorDeviceNames.Add(screen.DeviceName);
            }

            _monitorLayoutDirty = true;
            SaveSettings();
            UpdateTrayPopupState();
            RefreshDisplaySettingsUi();
            ApplyTransformCurrentPoint();
        };
        _displayOptionsHost.Controls.Add(allRow);

        int index = 1;
        foreach (Screen screen in screens)
        {
            string label = GetFriendlyScreenLabel(screen, index);

            bool selected = _displaySelectionMode == DisplaySelectionMode.AllDisplays ||
                            (_displaySelectionMode == DisplaySelectionMode.CustomSelection && _selectedMonitorDeviceNames.Contains(screen.DeviceName));
            var screenRow = new TrayMenuRow(palette, label, selected ? L("Tray.DisplayIncluded") : string.Empty)
            {
                Active = _displaySelectionMode == DisplaySelectionMode.CustomSelection && _selectedMonitorDeviceNames.Contains(screen.DeviceName),
                Width = rowWidth
            };
            string deviceName = screen.DeviceName;
            screenRow.ActionRequested += (_, _) => ToggleScreenSelection(deviceName);
            _displayOptionsHost.Controls.Add(screenRow);
            index++;
        }
        _displayOptionsHost.ResumeLayout(performLayout: true);
    }

    private void ToggleScreenSelection(string deviceName)
    {
        SetScreenSelection(deviceName, !_selectedMonitorDeviceNames.Contains(deviceName));
    }

    private void SetScreenSelection(string deviceName, bool included)
    {
        ResetExitConfirmation();
        _displaySelectionMode = DisplaySelectionMode.CustomSelection;
        _useCursorMonitorSelection = false;
        if (included)
        {
            _selectedMonitorDeviceNames.Add(deviceName);
        }
        else
        {
            if (_selectedMonitorDeviceNames.Count == 1)
            {
                return;
            }

            _selectedMonitorDeviceNames.Remove(deviceName);
        }

        _monitorLayoutDirty = true;
        SaveSettings();
        UpdateTrayPopupState();
        RefreshDisplaySettingsUi();
        ApplyTransformCurrentPoint();
    }

    private DisplaySelectionMode GetDisplaySelectionMode()
    {
        return _displaySelectionMode;
    }

    private void SetDisplaySelectionMode(DisplaySelectionMode mode)
    {
        ResetExitConfirmation();

        switch (mode)
        {
            case DisplaySelectionMode.MonitorUnderCursor:
                _displaySelectionMode = DisplaySelectionMode.MonitorUnderCursor;
                _useCursorMonitorSelection = true;
                _lockedScreen = null;
                break;

            case DisplaySelectionMode.AllDisplays:
                _displaySelectionMode = DisplaySelectionMode.AllDisplays;
                _useCursorMonitorSelection = false;
                foreach (Screen screen in GetOrderedScreens())
                {
                    _selectedMonitorDeviceNames.Add(screen.DeviceName);
                }
                break;

            case DisplaySelectionMode.CustomSelection:
                _displaySelectionMode = DisplaySelectionMode.CustomSelection;
                _useCursorMonitorSelection = false;
                EnsureSelectedMonitorsValid();
                if (_selectedMonitorDeviceNames.Count == 0)
                {
                    Screen primary = Screen.PrimaryScreen ?? GetOrderedScreens().First();
                    _selectedMonitorDeviceNames.Clear();
                    _selectedMonitorDeviceNames.Add(primary.DeviceName);
                }
                break;
        }

        _monitorLayoutDirty = true;
        SaveSettings();
        RefreshMenuAndTrayUi(rebuildPopup: true);
        RefreshDisplaySettingsUi();
        ApplyTransformCurrentPoint();
    }

    private string GetDisplaySelectionSummary()
    {
        if (_displaySelectionMode == DisplaySelectionMode.MonitorUnderCursor)
        {
            return L("Tray.DisplaySummaryCursor");
        }

        if (_displaySelectionMode == DisplaySelectionMode.AllDisplays)
        {
            return L("Tray.DisplaySummaryAll");
        }

        return string.Format(L("Tray.DisplaySummaryCount"), _selectedMonitorDeviceNames.Count);
    }

    private List<Screen> GetOrderedScreens()
    {
        return Screen.AllScreens
            .OrderByDescending(screen => screen.Primary)
            .ThenBy(screen => screen.DeviceName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string GetFriendlyScreenLabel(Screen screen, int fallbackIndex)
    {
        int displayNumber = TryGetDisplayNumber(screen.DeviceName) ?? fallbackIndex;
        return displayNumber switch
        {
            1 => L("Tray.PrimaryDisplay"),
            2 => L("Tray.SecondaryDisplay"),
            _ => L("Tray.MonitorNumber", displayNumber)
        };
    }

    private static int? TryGetDisplayNumber(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return null;
        }

        const string marker = "DISPLAY";
        int markerIndex = deviceName.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        string numericPart = deviceName[(markerIndex + marker.Length)..];
        return int.TryParse(numericPart, out int value) ? value : null;
    }

    private List<Screen> GetSelectedScreens()
    {
        EnsureSelectedMonitorsValid();

        if (_displaySelectionMode == DisplaySelectionMode.MonitorUnderCursor)
        {
            if (!_autoSwitchMonitor && _lockedScreen != null)
            {
                return [_lockedScreen];
            }

            if (GetCursorPos(out var pt))
            {
                Screen screen = Screen.FromPoint(new Point(pt.X, pt.Y));
                if (!_autoSwitchMonitor)
                {
                    _lockedScreen = screen;
                }

                return [screen];
            }

            if (_lockedScreen != null)
            {
                return [_lockedScreen];
            }

            Screen fallback = Screen.PrimaryScreen ?? Screen.AllScreens.First();
            return [fallback];
        }

        if (_displaySelectionMode == DisplaySelectionMode.AllDisplays)
        {
            return GetOrderedScreens();
        }

        var selectedNames = _selectedMonitorDeviceNames;

        var chosenScreens = GetOrderedScreens()
            .Where(screen => selectedNames.Contains(screen.DeviceName))
            .ToList();

        if (chosenScreens.Count == 0)
        {
            Screen primary = Screen.PrimaryScreen ?? Screen.AllScreens.First();
            chosenScreens.Add(primary);
        }

        return chosenScreens;
    }

    private static Point GetMonitorRelativePoint(Point sourcePoint, Rectangle sourceBounds, Rectangle targetBounds)
    {
        double normX = sourceBounds.Width <= 1 ? 0.5 : (sourcePoint.X - sourceBounds.Left) / (double)sourceBounds.Width;
        double normY = sourceBounds.Height <= 1 ? 0.5 : (sourcePoint.Y - sourceBounds.Top) / (double)sourceBounds.Height;

        normX = Math.Clamp(normX, 0.0, 1.0);
        normY = Math.Clamp(normY, 0.0, 1.0);

        int x = targetBounds.Left + (int)Math.Round(normX * targetBounds.Width);
        int y = targetBounds.Top + (int)Math.Round(normY * targetBounds.Height);
        return new Point(x, y);
    }
}
