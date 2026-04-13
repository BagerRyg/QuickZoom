using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickZoom;

internal sealed partial class TrayContext
{
    // Windows 10/11 dark title bar (best effort). If unsupported, it silently does nothing.
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private void ShowAboutDialog()
    {
        string installPath = InstalledAppService.GetCurrentInstalledExecutablePath() ?? L("About.NotInstalled");
        string settingsPath = AppPaths.SettingsPath;

        using var dlg = new Form
        {
            Text = "QuickZoom",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.CenterScreen,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };

        var root = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 2
        };

        var lbl = new Label
        {
            AutoSize = true,
            Text = L("About.Help") + "\n\n" +
                   AppInfo.DisplayVersion + "\n" +
                   L("About.Description") + "\n\n" +
                   StartupTaskService.GetStatusLabel(_language) + "\n\n" +
                   L("About.InstallPath") + ": " + installPath + "\n\n" +
                   L("About.SettingsPath") + ": " + settingsPath + "\n\n" +
                   L("About.Credits"),
            MaximumSize = new Size(640, 0)
        };

        var pathButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0),
            Margin = new Padding(0, 12, 0, 0),
            WrapContents = false
        };

        var openInstallButton = new Button
        {
            Text = L("About.OpenInstall"),
            AutoSize = true,
            MinimumSize = new Size(160, 32),
            Enabled = !string.Equals(installPath, L("About.NotInstalled"), StringComparison.OrdinalIgnoreCase)
        };
        openInstallButton.Click += (_, _) => OpenFileLocation(installPath);

        var openSettingsButton = new Button
        {
            Text = L("About.OpenSettings"),
            AutoSize = true,
            MinimumSize = new Size(170, 32)
        };
        openSettingsButton.Click += (_, _) => OpenFileLocation(settingsPath);

        pathButtons.Controls.Add(openInstallButton);
        pathButtons.Controls.Add(openSettingsButton);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0),
            Margin = new Padding(0, 14, 0, 0),
            WrapContents = false
        };

        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            MinimumSize = new Size(90, 32)
        };

        buttons.Controls.Add(ok);
        root.Controls.Add(lbl, 0, 0);
        root.Controls.Add(pathButtons, 0, 1);
        root.Controls.Add(buttons, 0, 2);
        dlg.Controls.Add(root);
        dlg.AcceptButton = ok;

        ApplyDialogTheme(dlg);
        dlg.ShowDialog();
    }

    private static void OpenFileLocation(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || string.Equals(path, "Not installed", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "/select,\"" + path + "\"",
                    UseShellExecute = true
                });
                return;
            }

            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "\"" + directory + "\"",
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            // Ignore shell-launch failures.
        }
    }

    private void ApplyDialogTheme(Form form)
    {
        var palette = CurrentTheme;
        var bg = palette.MenuBackground;
        var fg = palette.Text;
        var ctlBg = palette.ControlBackground;
        var btnBg = palette.ButtonBackground;
        var border = palette.Border;

        form.BackColor = bg;
        form.ForeColor = fg;

        try
        {
            form.HandleCreated += (_, _) => TrySetDarkTitleBar(form.Handle, _useDarkTheme);
        }
        catch
        {
            // Ignore title bar theming failures.
        }

        void ApplyTo(Control control)
        {
            if (control is Panel or TableLayoutPanel or FlowLayoutPanel or GroupBox)
            {
                control.BackColor = bg;
                control.ForeColor = fg;
            }

            if (control is Label label)
            {
                label.BackColor = bg;
                label.ForeColor = fg;
            }

            if (control is TextBox textBox)
            {
                textBox.BackColor = ctlBg;
                textBox.ForeColor = fg;
                textBox.BorderStyle = BorderStyle.FixedSingle;
            }

            if (control is ComboBox comboBox)
            {
                comboBox.BackColor = ctlBg;
                comboBox.ForeColor = fg;
                comboBox.FlatStyle = FlatStyle.Flat;
            }

            if (control is CheckBox checkBox)
            {
                checkBox.BackColor = bg;
                checkBox.ForeColor = fg;
            }

            if (control is NumericUpDown nud)
            {
                nud.BackColor = ctlBg;
                nud.ForeColor = fg;
                nud.BorderStyle = BorderStyle.FixedSingle;

                try
                {
                    foreach (Control inner in nud.Controls)
                    {
                        inner.BackColor = ctlBg;
                        inner.ForeColor = fg;
                    }
                }
                catch
                {
                    // Ignore styling failures of child controls.
                }
            }

            if (control is Button button)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = btnBg;
                button.ForeColor = fg;
                button.FlatAppearance.BorderColor = border;
                button.FlatAppearance.MouseOverBackColor = palette.ButtonHover;
                button.FlatAppearance.MouseDownBackColor = palette.ButtonPressed;
            }

            foreach (Control child in control.Controls)
            {
                ApplyTo(child);
            }
        }

        ApplyTo(form);
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

    private int? PromptForNumber(string title, int current, int min, int max)
    {
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
            Padding = new Padding(12)
        };

        var lbl = new Label
        {
            AutoSize = true,
            Text = title,
            MaximumSize = new Size(520, 0)
        };

        var num = new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Value = current,
            Width = 520,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(lbl, 0, 0);
        layout.Controls.Add(num, 0, 1);
        layout.Controls.Add(buttons, 0, 2);

        form.Controls.Add(layout);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        form.MinimumSize = new Size(380, 0);

        ApplyDialogTheme(form);
        return form.ShowDialog() == DialogResult.OK ? (int?)num.Value : null;
    }

    private Keys? PromptForKey(Keys current, string title = "Press the key you want to hold while scrolling", string? description = null)
    {
        using var form = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            KeyPreview = true,
            AutoScaleMode = AutoScaleMode.Dpi,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12)
        };

        var lbl = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(560, 0),
            Text = description ?? "Press a single key (letters, F-keys, Ctrl/Alt/Shift). Win key may be reserved by Windows."
        };

        var cur = new Label
        {
            AutoSize = true,
            Text = $"Current: {KeyLabel(current)}"
        };

        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            MinimumSize = new Size(100, 30)
        };

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false
        };
        buttons.Controls.Add(cancel);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 3
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(lbl, 0, 0);
        layout.Controls.Add(cur, 0, 1);
        layout.Controls.Add(buttons, 0, 2);

        form.Controls.Add(layout);
        ApplyDialogTheme(form);

        Keys? chosen = null;
        form.KeyDown += (_, e) =>
        {
            chosen = e.KeyCode;
            form.DialogResult = DialogResult.OK;
            form.Close();
        };

        return form.ShowDialog() == DialogResult.OK ? chosen : null;
    }
}
