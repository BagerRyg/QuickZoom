using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace QuickZoom;

internal sealed partial class TrayContext
{
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
            Active = _useCursorMonitorSelection,
            Width = rowWidth
        };
        cursorRow.ActionRequested += (_, _) =>
        {
            _useCursorMonitorSelection = true;
            _lockedScreen = null;
            _monitorLayoutDirty = true;
            SaveSettings();
            UpdateTrayPopupState();
            ApplyTransformCurrentPoint();
        };
        _displayOptionsHost.Controls.Add(cursorRow);

        List<Screen> screens = GetOrderedScreens();
        bool allDisplaysSelected = !_useCursorMonitorSelection && _selectedMonitorDeviceNames.Count == screens.Count;

        var allRow = new TrayMenuRow(palette, L("Tray.AllDisplays"), allDisplaysSelected ? L("Tray.DisplaySelected") : string.Empty)
        {
            Active = allDisplaysSelected,
            Width = rowWidth
        };
        allRow.ActionRequested += (_, _) =>
        {
            _useCursorMonitorSelection = false;
            foreach (Screen screen in screens)
            {
                _selectedMonitorDeviceNames.Add(screen.DeviceName);
            }

            _monitorLayoutDirty = true;
            SaveSettings();
            UpdateTrayPopupState();
            ApplyTransformCurrentPoint();
        };
        _displayOptionsHost.Controls.Add(allRow);

        int index = 1;
        foreach (Screen screen in screens)
        {
            string label = screen.Primary
                ? string.Format(L("Tray.PrimaryDisplay"), screen.DeviceName)
                : string.Format(L("Tray.DisplayN"), index, screen.DeviceName);

            bool selected = !_useCursorMonitorSelection && _selectedMonitorDeviceNames.Contains(screen.DeviceName);
            var screenRow = new TrayMenuRow(palette, label, selected ? L("Tray.DisplayIncluded") : string.Empty)
            {
                Active = selected,
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
        ResetExitConfirmation();
        _useCursorMonitorSelection = false;
        if (_selectedMonitorDeviceNames.Contains(deviceName))
        {
            if (_selectedMonitorDeviceNames.Count == 1)
            {
                return;
            }

            _selectedMonitorDeviceNames.Remove(deviceName);
        }
        else
        {
            _selectedMonitorDeviceNames.Add(deviceName);
        }

        _monitorLayoutDirty = true;
        SaveSettings();
        UpdateTrayPopupState();
        ApplyTransformCurrentPoint();
    }

    private string GetDisplaySelectionSummary()
    {
        List<Screen> screens = GetOrderedScreens();
        if (_useCursorMonitorSelection)
        {
            return L("Tray.DisplaySummaryCursor");
        }

        if (_selectedMonitorDeviceNames.Count >= screens.Count)
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

    private List<Screen> GetSelectedScreens()
    {
        EnsureSelectedMonitorsValid();

        if (_useCursorMonitorSelection)
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
