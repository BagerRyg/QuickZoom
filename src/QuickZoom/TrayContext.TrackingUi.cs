using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace QuickZoom;

internal sealed partial class TrayContext
{
    private FollowingOptionsMenu? _followOptionsMenu;
    private readonly Dictionary<TrackingMode, ToolStripMenuItem> _followModeItems = new();
    private ToolStripMenuItem? _pauseFollowingItem;
    private bool _followMenuRestoringFocus;
    private int _followMenuGeneration;
    private ModernDropdown? _settingsTrackingDropdown;
    private ToggleSwitchControl? _settingsPauseFollowingToggle;
    private SettingsRow? _settingsTrackingRow;
    private bool _updatingFollowingUi;

    private string TrackingModeLabel(TrackingMode mode, bool compact = false) => mode switch
    {
        TrackingMode.MouseOnly => L("Settings.TrackingMouseOnly"),
        TrackingMode.KeyboardAndTyping => L("Settings.TrackingKeyboard"),
        _ => L(compact ? "Tray.FollowAutomatic" : "Settings.TrackingAutomatic")
    };

    private void AddTrayFollowingControls(FlowLayoutPanel parent, ThemePalette palette, int width)
    {
        _followRow = new TrayMenuRow(palette, L("Settings.TrackingMode"), TrackingModeLabel(_trackingMode, compact: true),
            icon: TrayFluentIcon.FollowCursor, isExpandable: true, inlineValue: true) { Width = width };
        _followRow.ActionRequested += (_, _) => ExecuteTrayAction(() =>
            SetFollowOptionsExpanded(_followOptionsMenu?.Visible != true));
        parent.Controls.Add(_followRow);
        _followOptionsMenu = CreateFollowOptionsMenu();
    }

    private FollowingOptionsMenu CreateFollowOptionsMenu()
    {
        ThemePalette palette = CurrentTheme;
        var menu = new FollowingOptionsMenu(palette)
        {
            AccessibleName = L("Settings.TrackingMode")
        };
        _followModeItems.Clear();
        foreach (TrackingMode mode in Enum.GetValues<TrackingMode>())
        {
            var item = new FollowingOptionItem(TrackingModeLabel(mode, compact: true),
                L("Tray.FollowHelp." + mode), mode switch
                {
                    TrackingMode.MouseOnly => TrayFluentIcon.FollowCursor,
                    TrackingMode.KeyboardAndTyping => TrayFluentIcon.KeyBinds,
                    _ => TrayFluentIcon.ResetCursor
                });
            item.Click += (_, _) => ExecuteTrayAction(() =>
            {
                menu.Close(ToolStripDropDownCloseReason.ItemClicked);
                SetTrackingMode(mode);
            });
            _followModeItems.Add(mode, item);
            menu.Items.Add(item);
        }
        menu.Items.Add(new ToolStripSeparator());
        _pauseFollowingItem = new FollowingOptionItem(L("Tray.PauseFollowing"), string.Empty);
        _pauseFollowingItem.Click += (_, _) => ExecuteTrayAction(() =>
        {
            menu.Close(ToolStripDropDownCloseReason.ItemClicked);
            SetFollowCursor(!_followCursor);
        });
        menu.Items.Add(_pauseFollowingItem);
        menu.Closing += (_, e) =>
        {
            // Let the trigger's Click handler close the open menu. Native
            // outside-click dismissal runs on mouse-down, before that handler.
            if (e.CloseReason == ToolStripDropDownCloseReason.AppClicked &&
                _followRow is { IsDisposed: false } row &&
                row.ClientRectangle.Contains(row.PointToClient(Cursor.Position)))
                e.Cancel = true;
        };
        menu.Closed += (_, e) => OnFollowOptionsClosed(menu, e.CloseReason);
        return menu;
    }

    private void SetFollowOptionsExpanded(bool expanded)
    {
        if (_followRow == null || _trayPopup == null) return;
        ResetExitConfirmation();
        if (!expanded)
        {
            _followOptionsMenu?.Close(ToolStripDropDownCloseReason.Keyboard);
            return;
        }
        if (_followOptionsMenu?.Visible == true) return;
        _followMenuGeneration++;
        _followMenuRestoringFocus = false;
        _followOptionsMenu ??= CreateFollowOptionsMenu();
        FollowingOptionsMenu menu = _followOptionsMenu;
        UpdateFollowingUi();
        Rectangle bounds = _followRow.RectangleToScreen(_followRow.ClientRectangle);
        Rectangle area = Screen.FromRectangle(bounds).WorkingArea;
        menu.FitTo(_followRow, area);
        bool above = area.Bottom - bounds.Bottom < menu.Height + menu.Gap && bounds.Top - area.Top > area.Bottom - bounds.Bottom;
        menu.Show(_followRow, new Point(_followRow.Width, above ? -menu.Gap : _followRow.Height + menu.Gap),
            above ? ToolStripDropDownDirection.AboveLeft : ToolStripDropDownDirection.BelowLeft);
        if (_trayPopup.CaptureMode) menu.Location = new Point(bounds.Right - menu.Width, bounds.Bottom + menu.Gap);
        _followRow.Active = true;
        if (_followModeItems.TryGetValue(_trackingMode, out ToolStripMenuItem? selected)) selected.Select();
    }

    private void OnFollowOptionsClosed(ContextMenuStrip menu, ToolStripDropDownCloseReason reason)
    {
        TrayPopupWindow? popup = _trayPopup;
        if (popup == null || popup.IsDisposed || !ReferenceEquals(menu, _followOptionsMenu)) return;
        if (_followRow != null) _followRow.Active = false;
        int generation = ++_followMenuGeneration;
        bool restoreFocus = reason is ToolStripDropDownCloseReason.Keyboard or ToolStripDropDownCloseReason.ItemClicked;
        _followMenuRestoringFocus = restoreFocus;
        popup.BeginInvoke((MethodInvoker)(() =>
        {
            if (popup.IsDisposed || !ReferenceEquals(_trayPopup, popup) || generation != _followMenuGeneration) return;
            try
            {
                if (restoreFocus && _followRow != null)
                {
                    if (!popup.CaptureMode) popup.Activate();
                    popup.FocusKeyboardTarget(_followRow);
                }
                else if (!popup.CaptureMode &&
                    (reason == ToolStripDropDownCloseReason.AppFocusChange || !popup.Bounds.Contains(Cursor.Position)))
                {
                    popup.Close();
                }
            }
            finally { _followMenuRestoringFocus = false; }
        }));
    }

    private void DisposeFollowOptionsMenu()
    {
        ContextMenuStrip? menu = _followOptionsMenu;
        _followOptionsMenu = null;
        _followModeItems.Clear();
        _pauseFollowingItem = null;
        _followMenuRestoringFocus = false;
        _followMenuGeneration++;
        menu?.Dispose();
    }

    private bool CollapseFollowOptions()
    {
        if (_followOptionsMenu?.Visible != true) return false;
        SetFollowOptionsExpanded(false);
        return true;
    }

    private void UpdateFollowingUi()
    {
        if (_updatingFollowingUi) return;
        _updatingFollowingUi = true;
        try
        {
            if (_followRow is { IsDisposed: false })
            {
                _followRow.Title = L("Settings.TrackingMode");
                _followRow.RightText = _followCursor ? TrackingModeLabel(_trackingMode, compact: true) : L("Tray.FollowPaused");
                _followRow.ApplyTheme(CurrentTheme);
            }
            foreach (var (mode, item) in _followModeItems)
            {
                item.Checked = mode == _trackingMode;
            }
            if (_pauseFollowingItem is { IsDisposed: false })
            {
                _pauseFollowingItem.Text = L(_followCursor ? "Tray.PauseFollowing" : "Tray.ResumeFollowing");
                ((FollowingOptionItem)_pauseFollowingItem).IsResumeAction = !_followCursor;
            }
            if (_settingsTrackingDropdown is { IsDisposed: false })
                _settingsTrackingDropdown.SelectedIndex = (int)_trackingMode;
            if (_settingsPauseFollowingToggle is { IsDisposed: false })
                _settingsPauseFollowingToggle.IsOn = !_followCursor;
            if (_settingsTrackingRow is { IsDisposed: false })
                _settingsTrackingRow.SetStatus(_followCursor ? null : L("Tray.FollowingPaused"), CurrentTheme.Text);
        }
        finally { _updatingFollowingUi = false; }
    }
}
