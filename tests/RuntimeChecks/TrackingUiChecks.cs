using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

internal static class TrackingUiChecks
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    internal static void Run(Assembly assembly, string root, bool capture = false)
    {
        Type type = assembly.GetType("QuickZoom.TrayContext", true)!;
        using var context = (IDisposable)type.GetConstructors()[0].Invoke([null, true, false, Keys.None]);
        object? Call(string name, params object?[] args) => type.GetMethod(name, Instance)!.Invoke(context, args);
        object? Get(string name) => type.GetField(name, Instance)!.GetValue(context);
        void Set(string name, object value) => type.GetField(name, Instance)!.SetValue(context, value);
        Type modeType = assembly.GetType("QuickZoom.TrackingMode", true)!;
        Set("_zoomPercent", 100);
        Set("_invertColors", false);
        Set("_trackingMode", Enum.ToObject(modeType, 0));
        Set("_followCursor", true);
        using var page = (Control)Call("BuildCursorSettingsPage")!;
        var dropdown = (Control)Get("_settingsTrackingDropdown")!;
        var selectedIndex = dropdown.GetType().GetProperty("SelectedIndex")!;
        var pauseToggle = (Control)Get("_settingsPauseFollowingToggle")!;
        var toggleValue = pauseToggle.GetType().GetProperty("IsOn")!;
        string output = Path.Combine(root, "tracking-ui");
        if (capture) Directory.CreateDirectory(output);
        try
        {
            Call("ShowTrayPopup", new Point(1000, 900), false);
            var popup = (Form)Get("_trayPopup")!;
            var header = (Control)Get("_followRow")!;
            var menu = (ContextMenuStrip)Get("_followOptionsMenu")!;
            var items = (IDictionary)Get("_followModeItems")!;
            var pauseItem = (ToolStripMenuItem)Get("_pauseFollowingItem")!;
            Rectangle trayBounds = popup.Bounds;
            Check(items.Count == 3 && !menu.Visible, "tray following selector starts collapsed with exactly three modes");
            Check(((ToolStripMenuItem)items[Enum.ToObject(modeType, 0)]!).Text ==
                (string)Call("L", "Tray.FollowAutomatic", Array.Empty<object>())!, "the tray uses the concise Automatic label");
            Check(header.AccessibilityObject.Role == AccessibleRole.ButtonDropDown &&
                header.AccessibilityObject.State.HasFlag(AccessibleStates.Collapsed) &&
                header.AccessibilityObject.Value == (string)Call("L", "Tray.FollowAutomatic", Array.Empty<object>())!,
                "Follow exposes its collapsed state and current mode");
            if (capture) Save(popup, "collapsed");
            header.AccessibilityObject.DoDefaultAction();
            Application.DoEvents();
            Check(menu.Visible && header.AccessibilityObject.State.HasFlag(AccessibleStates.Expanded), "activating Follow opens the flyout");
            Check(popup.Bounds == trayBounds, "opening the following flyout does not move or resize the tray");
            if (capture)
            {
                Save(popup, "expanded");
                Save(menu, "expanded-menu");
            }
            // Exercise the native mouse-down dismissal before the trigger's
            // mouse-up/Click, including both child labels. Keep the pointer still.
            Point originalLocation = popup.Location;
            int outsideClickDismissals = 0;
            ToolStripDropDownClosingEventHandler observeDismissal = (_, e) =>
            {
                if (e.CloseReason == ToolStripDropDownCloseReason.AppClicked) outsideClickDismissals++;
            };
            menu.Closing += observeDismissal;
            foreach (Control target in new[] { header }.Concat(header.Controls.OfType<Label>()))
            {
                menu.Close(ToolStripDropDownCloseReason.Keyboard);
                Application.DoEvents();
                Point hit = target == header ? new Point(8, header.Height / 2) :
                    new Point(target.Width / 2, target.Height / 2);
                Point targetScreen = target.PointToScreen(hit);
                popup.Location = new Point(popup.Left + Cursor.Position.X - targetScreen.X,
                    popup.Top + Cursor.Position.Y - targetScreen.Y);
                Rectangle clickBounds = popup.Bounds;
                Call("SetFollowOptionsExpanded", true);
                int previousDismissals = outsideClickDismissals;
                MouseClick(target, hit);
                Application.DoEvents();
                Check(outsideClickDismissals > previousDismissals && !menu.Visible && !popup.IsDisposed &&
                    popup.Bounds == clickBounds && header.Focused &&
                    header.AccessibilityObject.State.HasFlag(AccessibleStates.Collapsed),
                    "clicking the open Follow trigger closes it without reopening: " + target.GetType().Name);
                MouseClick(target, hit);
                Application.DoEvents();
                Check(menu.Visible && popup.Bounds == clickBounds,
                    "the next Follow click opens normally after closing: " + target.GetType().Name);
            }
            menu.Closing -= observeDismissal;
            menu.Close(ToolStripDropDownCloseReason.Keyboard);
            Application.DoEvents();
            popup.Location = originalLocation;
            Call("SetFollowOptionsExpanded", true);
            menu.Close(ToolStripDropDownCloseReason.Keyboard);
            Call("SetFollowOptionsExpanded", true);
            Application.DoEvents();
            Check(menu.Visible && header.AccessibilityObject.State.HasFlag(AccessibleStates.Expanded) &&
                popup.Bounds == trayBounds, "a queued close callback cannot dismiss a newly reopened following flyout");
            popup.GetType().GetProperty("IgnoreDeactivateClose", Instance)!.SetValue(popup, false);
            typeof(Form).GetMethod("OnDeactivate", Instance)!.Invoke(popup, [EventArgs.Empty]);
            Application.DoEvents();
            Check(!popup.IsDisposed && menu.Visible, "opening a child flyout prevents parent tray deactivation from dismissing it");
            popup.GetType().GetProperty("IgnoreDeactivateClose", Instance)!.SetValue(popup, true);

            Check(items.Values.Cast<ToolStripMenuItem>().All(item =>
                !string.IsNullOrWhiteSpace(item.AccessibilityObject.Description) &&
                item.AccessibilityObject.Description == item.AccessibleDescription),
                "each following mode exposes its helper description to assistive technology");
            MenuKey(menu, Keys.Down);
            Check(((ToolStripMenuItem)items[Enum.ToObject(modeType, 1)]!).Selected,
                "the native Down key moves from Automatic to Mouse only");
            MenuKey(menu, Keys.Down);
            MenuKey(menu, Keys.Down);
            Check(pauseItem.Selected && pauseItem.Enabled && menu.Items[menu.Items.Count - 1] == pauseItem,
                "native keyboard navigation reaches Pause after all three modes and skips the divider");
            MenuKey(menu, Keys.Up);
            MenuKey(menu, Keys.Up);
            MenuKey(menu, Keys.Enter);
            Application.DoEvents();
            Check(Convert.ToInt32(Get("_trackingMode")) == 1 && !menu.Visible && header.Focused,
                "native Up and Enter select a mode and return keyboard focus to Follow");

            Call("SetFollowOptionsExpanded", true);
            ToolStripMenuItem[] constrainedChoices = menu.Items.OfType<ToolStripMenuItem>().ToArray();
            int constrainedHeight = Math.Max(180, constrainedChoices.Max(item => item.Height) +
                menu.Padding.Vertical + SystemInformation.MenuHeight * 2 + 16);
            menu.GetType().GetMethod("FitTo", Instance)!.Invoke(menu,
                [header, new Rectangle(0, 0, menu.Width, constrainedHeight)]);
            constrainedChoices[0].Select();
            Check(menu.Height <= constrainedHeight &&
                menu.Items.Cast<ToolStripItem>().Sum(item => item.Height) > menu.DisplayRectangle.Height,
                "a short working area clamps the following flyout and requires scrolling");
            for (int index = 0; index < constrainedChoices.Length; index++)
            {
                if (index > 0) MenuKey(menu, Keys.Down);
                CheckReachable(constrainedChoices[index], "Down");
            }
            if (capture) Save(menu, "constrained-footer");
            for (int index = constrainedChoices.Length - 2; index >= 0; index--)
            {
                MenuKey(menu, Keys.Up);
                CheckReachable(constrainedChoices[index], "Up");
            }
            for (int index = 1; index < constrainedChoices.Length; index++) MenuKey(menu, Keys.Down);
            MenuKey(menu, Keys.Enter);
            Application.DoEvents();
            Check(!(bool)Get("_followCursor")! && Convert.ToInt32(Get("_trackingMode")) == 1 &&
                !menu.Visible && header.Focused && popup.Bounds == trayBounds,
                "the scrolled Pause action works and returns focus without moving the tray");

            void CheckReachable(ToolStripMenuItem item, string direction)
            {
                Rectangle viewport = menu.DisplayRectangle;
                Point center = new(item.Bounds.Left + item.Width / 2, item.Bounds.Top + item.Height / 2);
                Check(menu.Visible && item.Selected && item.Bounds.Top >= viewport.Top &&
                    item.Bounds.Bottom <= viewport.Bottom && menu.GetItemAt(center) == item,
                    "native " + direction + " scrolls the complete following action into view: " + item.Text);
            }

            for (int mode = 0; mode < 3; mode++)
            {
                Call("SetFollowCursor", false);
                Call("SetFollowOptionsExpanded", true);
                var choice = (ToolStripMenuItem)items[Enum.ToObject(modeType, mode)]!;
                choice.PerformClick();
                Application.DoEvents();
                Check(Convert.ToInt32(Get("_trackingMode")) == mode && (bool)Get("_followCursor")! && !menu.Visible &&
                    header.AccessibilityObject.State.HasFlag(AccessibleStates.Collapsed),
                    "tray selection applies mode, resumes following, and closes the flyout: " + mode);
                Check((int)selectedIndex.GetValue(dropdown)! == mode && !(bool)toggleValue.GetValue(pauseToggle)!,
                    "tray choice synchronizes the existing Settings controls: " + mode);
                Check(items.Values.Cast<ToolStripMenuItem>().Count(item => item.Checked) == 1 && choice.Checked &&
                    choice.AccessibilityObject.Role == AccessibleRole.MenuItem &&
                    choice.AccessibilityObject.State.HasFlag(AccessibleStates.Checked), "exactly one menu choice exposes a checked state");
                Check(popup.Bounds == trayBounds && header.Focused,
                    "changing the following mode preserves tray bounds and returns focus: " + mode);
            }

            Call("SetFollowOptionsExpanded", true);
            pauseItem.PerformClick();
            Check(!(bool)Get("_followCursor")! && (bool)toggleValue.GetValue(pauseToggle)! &&
                Convert.ToInt32(Get("_trackingMode")) == 2 && !menu.Visible &&
                header.AccessibleName == (string)Call("L", "Settings.TrackingMode", Array.Empty<object>())! &&
                header.AccessibilityObject.Value == (string)Call("L", "Tray.FollowPaused", Array.Empty<object>())!,
                "tray pause shows Paused beside Follow and preserves the mode");
            Check(popup.Bounds == trayBounds, "pausing following preserves tray bounds");
            if (capture) Save(popup, "paused");
            Call("SetFollowOptionsExpanded", true);
            Check(pauseItem.Text == (string)Call("L", "Tray.ResumeFollowing", Array.Empty<object>())! &&
                ((ToolStripMenuItem)items[Enum.ToObject(modeType, 2)]!).Checked, "the paused flyout offers Resume and retains the selected mode");
            if (capture) Save(menu, "paused-menu");
            MenuKey(menu, Keys.Escape);
            Application.DoEvents();
            Check(!menu.Visible && !popup.IsDisposed && !(bool)Get("_followCursor")! &&
                Convert.ToInt32(Get("_trackingMode")) == 2 &&
                header.AccessibilityObject.State.HasFlag(AccessibleStates.Collapsed) && header.Focused,
                "Escape preserves pause and the selected mode and returns focus to Follow");
            Call("SetFollowOptionsExpanded", true);
            pauseItem.PerformClick();
            Check((bool)Get("_followCursor")! && Convert.ToInt32(Get("_trackingMode")) == 2,
                "resume preserves the previously selected mode");
            selectedIndex.SetValue(dropdown, 1);
            Check(Convert.ToInt32(Get("_trackingMode")) == 1 &&
                header.AccessibilityObject.Value == (string)Call("L", "Settings.TrackingMouseOnly", Array.Empty<object>())!,
                "Settings selection immediately updates the open tray");
            pauseToggle.AccessibilityObject.DoDefaultAction();
            Check(!(bool)Get("_followCursor")!, "Settings pause action controls following");
            Call("SetFollowCursor", true);
            Check(!(bool)toggleValue.GetValue(pauseToggle)!, "the existing shortcut setter updates the Settings pause state");
            Call("SetFollowOptionsExpanded", true);
            popup.GetType().GetMethod("TryHandleNavigationKey", Instance)!.Invoke(popup, [Keys.Escape]);
            Check(!menu.Visible && !popup.IsDisposed, "Escape first closes the following flyout without dismissing the tray");
            Call("SetFollowOptionsExpanded", true);
            Call("ShowTrayPopup", new Point(1000, 900), false);
            Check(popup.IsDisposed && menu.IsDisposed && pauseItem.IsDisposed, "rebuilding the tray disposes its open following flyout");
            popup = (Form)Get("_trayPopup")!;
            menu = (ContextMenuStrip)Get("_followOptionsMenu")!;
            Check(!menu.IsDisposed && items.Count == 3 && !menu.Visible, "the rebuilt tray has a fresh collapsed following flyout");
            popup.GetType().GetMethod("TryHandleNavigationKey", Instance)!.Invoke(popup, [Keys.Escape]);
            Check(popup.IsDisposed && menu.IsDisposed, "Escape with the flyout closed dismisses the tray and disposes the menu");
            Check(Get("_followOptionsMenu") == null && Get("_followRow") == null &&
                Get("_pauseFollowingItem") == null && items.Count == 0, "closing the tray clears its following controls");

            Call("ShowTrayPopup", new Point(1000, 900), false);
            popup = (Form)Get("_trayPopup")!;
            menu = (ContextMenuStrip)Get("_followOptionsMenu")!;
            popup.GetType().GetProperty("CaptureMode", Instance)!.SetValue(popup, false);
            popup.GetType().GetProperty("IgnoreDeactivateClose", Instance)!.SetValue(popup, true);
            Call("OnFollowOptionsClosed", menu, ToolStripDropDownCloseReason.AppFocusChange);
            Application.DoEvents();
            Check(popup.IsDisposed && menu.IsDisposed,
                "an explicit outside dismissal closes the tray even while the overlay activation guard is active");
        }
        finally { Call("CloseTrayPopup"); Set("_runtimeStopped", true); }
        if (capture) CaptureGeometryVariants(assembly, output);

        void Save(Control control, string name)
        {
            control.Refresh();
            using var bitmap = new Bitmap(control.Width, control.Height);
            control.DrawToBitmap(bitmap, control.ClientRectangle);
            bitmap.Save(Path.Combine(output, name + ".png"));
        }
    }

    private static void CaptureGeometryVariants(Assembly assembly, string output)
    {
        const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        Type drawing = assembly.GetType("QuickZoom.ControlDrawing", true)!;
        PropertyInfo fontScale = drawing.GetProperty("UiFontScale", Static)!;
        PropertyInfo followWindowsScale = drawing.GetProperty("FollowWindowsTextScale", Static)!;
        PropertyInfo focusCaptureTarget = drawing.GetProperty("FocusCaptureTarget", Static)!;
        PropertyInfo highContrast = assembly.GetType("QuickZoom.AccessibilityPreferences", true)!
            .GetProperty("CaptureHighContrast", Static)!;
        object previousFollowWindowsScale = followWindowsScale.GetValue(null)!;
        object? previousFocusTarget = focusCaptureTarget.GetValue(null);
        object previousHighContrast = highContrast.GetValue(null)!;
        followWindowsScale.SetValue(null, false);
        object previousFontScale = fontScale.GetValue(null)!;
        try
        {
            foreach (var variant in new[]
            {
                (Name: "dark-en-default", Language: "English", Dark: true, Scale: 1f, Contrast: false),
                (Name: "dark-en-225", Language: "English", Dark: true, Scale: 2.25f, Contrast: false),
                (Name: "light-fi-225", Language: "Finnish", Dark: false, Scale: 2.25f, Contrast: false),
                (Name: "high-contrast-en-225", Language: "English", Dark: true, Scale: 2.25f, Contrast: true)
            })
            {
                highContrast.SetValue(null, variant.Contrast);
                Type type = assembly.GetType("QuickZoom.TrayContext", true)!;
                using var context = (IDisposable)type.GetConstructors()[0].Invoke([null, true, false, Keys.None]);
                object? Call(string name, params object?[] args) => type.GetMethod(name, Instance)!.Invoke(context, args);
                object? Get(string name) => type.GetField(name, Instance)!.GetValue(context);
                void Set(string name, object value) => type.GetField(name, Instance)!.SetValue(context, value);
                void SetEnum(string name, string value) => Set(name, Enum.Parse(type.GetField(name, Instance)!.FieldType, value));
                SetEnum("_language", variant.Language);
                SetEnum("_themeMode", variant.Dark ? "Dark" : "Light");
                SetEnum("_uiFontSize", "Default");
                Set("_useDarkTheme", variant.Dark);
                Set("_zoomPercent", 100);
                Set("_invertColors", false);
                Set("_followCursor", true);
                Call("ApplyUiFontScale");
                fontScale.SetValue(null, variant.Scale);
                string variantOutput = Path.Combine(output, variant.Name);
                Directory.CreateDirectory(variantOutput);
                try
                {
                    Rectangle area = Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.VirtualScreen;
                    Call("ShowTrayPopup", new Point(area.Right - 24, area.Bottom - 24), false);
                    var popup = (Form)Get("_trayPopup")!;
                    var row = (Control)Get("_followRow")!;
                    var magnifyRow = (Control)Get("_magnifyRow")!;
                    Rectangle trayBounds = popup.Bounds;
                    Check(row.Height == magnifyRow.Height, "Follow retains a single standard tray row: " + variant.Name);
                    foreach (string mode in new[] { "Automatic", "MouseOnly", "KeyboardAndTyping" })
                    {
                        SetEnum("_trackingMode", mode);
                        Call("UpdateFollowingUi");
                        Label[] labels = row.Controls.OfType<Label>().OrderBy(label => label.Left).ToArray();
                        Check(labels.Length == 2 && labels[0].Right <= labels[1].Left && labels.All(label =>
                        {
                            Size textSize = TextRenderer.MeasureText(label.Text, label.Font, Size.Empty, TextFormatFlags.SingleLine);
                            return row.ClientRectangle.Contains(label.Bounds) && label.Width >= textSize.Width && label.Height >= textSize.Height;
                        }), "Follow title and complete mode text fit without overlap: " + variant.Name + "/" + mode);
                    }
                    Set("_followCursor", false);
                    Call("UpdateFollowingUi");
                    Check(row.Controls.OfType<Label>().All(label => label.Width >=
                        TextRenderer.MeasureText(label.Text, label.Font, Size.Empty, TextFormatFlags.SingleLine).Width),
                        "the paused Follow label fits: " + variant.Name);
                    Set("_followCursor", true);
                    SetEnum("_trackingMode", "Automatic");
                    Call("SetFollowOptionsExpanded", true);
                    var menu = (ContextMenuStrip)Get("_followOptionsMenu")!;
                    using (var bitmap = new Bitmap(menu.Width, menu.Height))
                        menu.DrawToBitmap(bitmap, menu.ClientRectangle);
                    var cardArea = new Rectangle(menu.Padding.Left, menu.Padding.Top,
                        menu.ClientSize.Width - menu.Padding.Horizontal, menu.ClientSize.Height - menu.Padding.Vertical);
                    int contentHeight = menu.Items.Cast<ToolStripItem>().Where(item => item.Available)
                        .Sum(item => item.Height + item.Margin.Vertical);
                    bool allItemsFit = contentHeight <= cardArea.Height;
                    Check(menu.Padding.All > 0 && menu.Items.OfType<ToolStripMenuItem>().All(item =>
                        item.Bounds.Left >= cardArea.Left && item.Bounds.Right <= cardArea.Right &&
                        (!allItemsFit || cardArea.Contains(item.Bounds))),
                        "flyout cards retain even frame insets while allowing native vertical scrolling: " + variant.Name);
                    MethodInfo getItemLayout = menu.GetType().GetMethod("GetItemLayout", Instance)!;
                    var descriptionFont = (Font)menu.GetType().GetProperty("DescriptionFont", Instance)!.GetValue(menu)!;
                    Check(menu.Items.OfType<ToolStripMenuItem>().All(item =>
                    {
                        object itemLayout = getItemLayout.Invoke(menu, [item])!;
                        Rectangle Bounds(string name) => (Rectangle)itemLayout.GetType()
                            .GetProperty(name, Instance)!.GetValue(itemLayout)!;
                        Rectangle title = Bounds("Title"), description = Bounds("Description");
                        Rectangle icon = Bounds("Icon"), selection = Bounds("Selection");
                        var itemBounds = new Rectangle(Point.Empty, item.Size);
                        string helper = (string)item.GetType().GetProperty("Description", Instance)!.GetValue(item)!;
                        Size titleSize = TextRenderer.MeasureText(item.Text, menu.Font, Size.Empty,
                            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                        Size descriptionSize = string.IsNullOrEmpty(helper) ? Size.Empty :
                            TextRenderer.MeasureText(helper, descriptionFont, new Size(description.Width, int.MaxValue),
                                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                        return itemBounds.Contains(title) && itemBounds.Contains(icon) && itemBounds.Contains(selection) &&
                            title.Width >= titleSize.Width && title.Height >= titleSize.Height &&
                            icon.Right <= title.Left && title.Right <= selection.Left &&
                            (descriptionSize.IsEmpty || itemBounds.Contains(description) &&
                                title.Bottom <= description.Top && description.Width >= descriptionSize.Width &&
                                description.Height >= descriptionSize.Height && icon.Right <= description.Left &&
                                description.Right <= selection.Left && item.AccessibleDescription == helper);
                    }), "flyout titles and complete helper text fit between mode icons and selection checks: " + variant.Name);
                    object layout = type.GetMethod("GetSimulatedCaptureLayout", Static)!.Invoke(null, [popup, row, true])!;
                    Rectangle workingArea = (Rectangle)layout.GetType().GetProperty("WorkingArea", Instance)!.GetValue(layout)!;
                    Check(menu.Width >= Math.Min(row.Width, workingArea.Width) && menu.Width <= workingArea.Width &&
                        menu.Height <= workingArea.Height && popup.Bounds == trayBounds,
                        "the flyout spans the trigger and fits the working area without resizing the tray: " + variant.Name);
                    Call("CloseTrayPopup");
                    Call("CaptureTrayMenu", variantOutput);
                }
                finally { Call("CloseTrayPopup"); Set("_runtimeStopped", true); }
            }
        }
        finally
        {
            fontScale.SetValue(null, previousFontScale);
            followWindowsScale.SetValue(null, previousFollowWindowsScale);
            highContrast.SetValue(null, previousHighContrast);
            focusCaptureTarget.SetValue(null, previousFocusTarget);
        }
    }

    private static void MenuKey(ContextMenuStrip menu, Keys key)
    {
        object?[] args = [Message.Create(menu.Handle, 0x0100, (nint)key, 0), key];
        bool handled = (bool)menu.GetType().GetMethod("ProcessCmdKey", Instance)!.Invoke(menu, args)!;
        if (!handled)
            menu.GetType().GetMethod("ProcessDialogKey", Instance)!.Invoke(menu, [key]);
    }

    private static void MouseClick(Control target, Point point)
    {
        nint location = (nint)((point.Y << 16) | (point.X & 0xffff));
        foreach (int messageId in new[] { 0x0201, 0x0202 })
        {
            Message message = Message.Create(target.Handle, messageId, messageId == 0x0201 ? 1 : 0, location);
            if (!Application.FilterMessage(ref message))
                _ = SendMessage(message.HWnd, message.Msg, message.WParam, message.LParam);
        }
    }

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint window, int message, nint wParam, nint lParam);

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }
}
