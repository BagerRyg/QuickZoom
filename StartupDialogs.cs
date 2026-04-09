using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickZoom;

internal static class StartupDialogs
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static bool ShowYesNo(string title, string heading, string body)
    {
        return ShowDialogCore(title, heading, body, "Set Up", "Not Now") == DialogResult.OK;
    }

    public static void ShowInfo(string title, string heading, string body)
    {
        _ = ShowDialogCore(title, heading, body, "OK", null);
    }

    public static void ShowWarning(string title, string heading, string body)
    {
        _ = ShowDialogCore(title, heading, body, "OK", null);
    }

    public static void ShowTimedSuccess(string title, string heading, string body, int secondsUntilClose)
    {
        _ = ShowDialogCore(title, heading, body, "Continue", null, secondsUntilClose);
    }

    private static DialogResult ShowDialogCore(
        string title,
        string heading,
        string body,
        string primaryText,
        string? secondaryText,
        int autoCloseSeconds = 0)
    {
        ThemePalette palette = GetWindowsAppsUseDarkMode() ? ThemePalettes.Dark : ThemePalettes.Light;

        using var form = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            AutoScaleMode = AutoScaleMode.Dpi,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = palette.MenuBackground,
            ForeColor = palette.Text,
            Padding = new Padding(0)
        };

        form.HandleCreated += (_, _) => TrySetDarkTitleBar(form.Handle, palette.Equals(ThemePalettes.Dark));

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = palette.MenuBackground
        };

        var headerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(22, 20, 22, 8),
            Margin = new Padding(0),
            BackColor = palette.MenuBackground
        };

        var headingLabel = new Label
        {
            AutoSize = true,
            Text = heading,
            Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold),
            ForeColor = palette.Text,
            BackColor = palette.MenuBackground,
            MaximumSize = new Size(420, 0)
        };
        headerPanel.Controls.Add(headingLabel);

        var bodyPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(22, 0, 22, 20),
            Margin = new Padding(0),
            BackColor = palette.MenuBackground
        };

        var bodyLabel = new Label
        {
            AutoSize = true,
            Text = body,
            Font = new Font("Segoe UI", 10f, FontStyle.Regular),
            ForeColor = palette.Text,
            BackColor = palette.MenuBackground,
            MaximumSize = new Size(420, 0)
        };
        bodyPanel.Controls.Add(bodyLabel);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(16),
            Margin = new Padding(0),
            BackColor = palette.ControlBackground
        };

        var primary = CreateButton(primaryText, DialogResult.OK, palette, true);
        buttons.Controls.Add(primary);

        Button? secondary = null;
        if (!string.IsNullOrWhiteSpace(secondaryText))
        {
            secondary = CreateButton(secondaryText, DialogResult.Cancel, palette, false);
            buttons.Controls.Add(secondary);
        }

        root.Controls.Add(headerPanel, 0, 0);
        root.Controls.Add(bodyPanel, 0, 1);
        root.Controls.Add(buttons, 0, 2);
        form.Controls.Add(root);
        form.AcceptButton = primary;
        form.CancelButton = secondary ?? primary;
        form.MinimumSize = new Size(480, 0);

        if (autoCloseSeconds > 0)
        {
            int remainingSeconds = autoCloseSeconds;
            primary.Text = $"{primaryText} ({remainingSeconds})";

            var timer = new System.Windows.Forms.Timer { Interval = 1000 };
            timer.Tick += (_, _) =>
            {
                remainingSeconds--;
                if (remainingSeconds <= 0)
                {
                    timer.Stop();
                    form.DialogResult = DialogResult.OK;
                    form.Close();
                    return;
                }

                primary.Text = $"{primaryText} ({remainingSeconds})";
            };

            form.Shown += (_, _) => timer.Start();
            form.FormClosed += (_, _) => timer.Dispose();
        }

        return form.ShowDialog();
    }

    private static Button CreateButton(string text, DialogResult result, ThemePalette palette, bool primary)
    {
        Color backColor = primary ? palette.ButtonHover : palette.ButtonBackground;
        Color borderColor = primary ? palette.ButtonHover : palette.Border;

        return new Button
        {
            Text = text,
            DialogResult = result,
            AutoSize = true,
            MinimumSize = new Size(112, 36),
            Margin = new Padding(8, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = palette.Text,
            UseVisualStyleBackColor = false,
            FlatAppearance =
            {
                BorderColor = borderColor,
                MouseOverBackColor = palette.ButtonHover,
                MouseDownBackColor = palette.ButtonPressed
            }
        };
    }

    private static void TrySetDarkTitleBar(IntPtr hwnd, bool enabled)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        int useDark = enabled ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, 20, ref useDark, sizeof(int));
        _ = DwmSetWindowAttribute(hwnd, 19, ref useDark, sizeof(int));
    }

    private static bool GetWindowsAppsUseDarkMode()
    {
        const string personalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        const string valueName = "AppsUseLightTheme";

        try
        {
            using Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(personalizeKey);
            object? value = key?.GetValue(valueName);
            if (value is int intValue)
            {
                return intValue == 0;
            }

            if (value is long longValue)
            {
                return longValue == 0;
            }
        }
        catch
        {
            // Fall back to light mode if registry cannot be read.
        }

        return false;
    }
}
