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
        if (_menu == null || _menu.IsDisposed)
        {
            return;
        }

        try
        {
            _menu.BeginInvoke((MethodInvoker)(() =>
            {
                EnsureSelectedMonitorsValid();
                RebuildDisplayMenuItems();
                EnsureLockedScreenStillValid();
                _monitorLayoutDirty = true;

                if (_magActive)
                {
                    ApplyTransformCurrentPoint();
                }
            }));
        }
        catch
        {
            // Ignore shutdown race conditions.
        }
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

    private void RebuildDisplayMenuItems()
    {
        if (_displayMenu == null)
        {
            return;
        }

        EnsureSelectedMonitorsValid();
        _displayMenu.DropDownItems.Clear();

        var screens = GetOrderedScreens();
        int selectedCount = _selectedMonitorDeviceNames.Count;

        var allItem = new ToolStripMenuItem("All Displays")
        {
            Checked = selectedCount == screens.Count
        };
        allItem.Click += (_, _) =>
        {
            foreach (Screen screen in screens)
            {
                _selectedMonitorDeviceNames.Add(screen.DeviceName);
            }

            _monitorLayoutDirty = true;
            RebuildDisplayMenuItems();
            SaveSettings();
            ApplyTransformCurrentPoint();
        };
        _displayMenu.DropDownItems.Add(allItem);
        _displayMenu.DropDownItems.Add(new ToolStripSeparator());

        int index = 1;
        foreach (Screen screen in screens)
        {
            string label = screen.Primary
                ? $"Primary ({screen.DeviceName})"
                : $"Display {index} ({screen.DeviceName})";

            var item = new ToolStripMenuItem(label)
            {
                CheckOnClick = true,
                Checked = _selectedMonitorDeviceNames.Contains(screen.DeviceName),
                Tag = screen.DeviceName
            };

            item.Click += (_, _) =>
            {
                string deviceName = (string)item.Tag!;
                if (item.Checked)
                {
                    _selectedMonitorDeviceNames.Add(deviceName);
                }
                else
                {
                    if (_selectedMonitorDeviceNames.Count == 1 &&
                        _selectedMonitorDeviceNames.Contains(deviceName))
                    {
                        item.Checked = true;
                        return;
                    }

                    _selectedMonitorDeviceNames.Remove(deviceName);
                }

                _monitorLayoutDirty = true;
                RebuildDisplayMenuItems();
                SaveSettings();
                ApplyTransformCurrentPoint();
            };

            _displayMenu.DropDownItems.Add(item);
            index++;
        }
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
        var selectedNames = _selectedMonitorDeviceNames;

        var screens = GetOrderedScreens()
            .Where(screen => selectedNames.Contains(screen.DeviceName))
            .ToList();

        if (screens.Count == 0)
        {
            Screen primary = Screen.PrimaryScreen ?? Screen.AllScreens.First();
            screens.Add(primary);
        }

        return screens;
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
