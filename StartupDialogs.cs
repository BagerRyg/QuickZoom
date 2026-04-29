using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;

namespace QuickZoom;

internal static class StartupDialogs
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static bool ShowYesNo(string title, string heading, string body)
    {
        UiLanguage language = UiText.GetStartupLanguage();
        return ShowDialogCore(
            title,
            heading,
            body,
            UiText.Get(language, "Common.SetUp"),
            UiText.Get(language, "Common.NotNow")) == DialogResult.OK;
    }

    public static void ShowInfo(string title, string heading, string body)
    {
        UiLanguage language = UiText.GetStartupLanguage();
        _ = ShowDialogCore(title, heading, body, UiText.Get(language, "Common.Ok"), null);
    }

    public static void ShowWarning(string title, string heading, string body)
    {
        UiLanguage language = UiText.GetStartupLanguage();
        _ = ShowDialogCore(title, heading, body, UiText.Get(language, "Common.Ok"), null);
    }

    public static void ShowTimedSuccess(string title, string heading, string body, int secondsUntilClose)
    {
        UiLanguage language = UiText.GetStartupLanguage();
        _ = ShowDialogCore(title, heading, body, UiText.Get(language, "Common.Continue"), null, secondsUntilClose);
    }

    public static T ShowProgress<T>(string title, string heading, string body, Func<T> work)
    {
        ApplyStartupFontScale();
        ThemePalette palette = GetWindowsAppsUseDarkMode() ? ThemePalettes.Dark : ThemePalettes.Light;

        using var form = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false,
            MaximizeBox = false,
            ControlBox = false,
            ShowInTaskbar = false,
            AutoScaleMode = AutoScaleMode.Dpi,
            ClientSize = new Size(ControlDrawing.ScaleLogical(new Control(), 520), ControlDrawing.ScaleLogical(new Control(), 174)),
            BackColor = palette.MenuBackground,
            ForeColor = palette.Text,
            Padding = new Padding(22, 20, 22, 20)
        };

        form.HandleCreated += (_, _) => TrySetDarkTitleBar(form.Handle, palette.Equals(ThemePalettes.Dark));

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = palette.MenuBackground
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ControlDrawing.ScaleLogical(form, 72)));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var spinner = new StartupSpinnerControl(palette)
        {
            Width = ControlDrawing.ScaleLogical(form, 52),
            Height = ControlDrawing.ScaleLogical(form, 52),
            Margin = new Padding(0, 2, 20, 0)
        };

        var headingLabel = new Label
        {
            AutoSize = true,
            Text = heading,
            Font = ControlDrawing.UiFont("Segoe UI Semibold", 12f, FontStyle.Bold),
            ForeColor = palette.Text,
            BackColor = palette.MenuBackground,
            MaximumSize = new Size(ControlDrawing.ScaleLogical(form, 410), 0),
            Margin = new Padding(0, 0, 0, 8)
        };

        var bodyLabel = new Label
        {
            AutoSize = true,
            Text = body,
            Font = ControlDrawing.UiFont("Segoe UI", 10f, FontStyle.Regular),
            ForeColor = palette.Text,
            BackColor = palette.MenuBackground,
            MaximumSize = new Size(ControlDrawing.ScaleLogical(form, 410), 0),
            Margin = new Padding(0)
        };

        root.Controls.Add(spinner, 0, 0);
        root.SetRowSpan(spinner, 2);
        root.Controls.Add(headingLabel, 1, 0);
        root.Controls.Add(bodyLabel, 1, 1);
        form.Controls.Add(root);

        T? result = default;
        Exception? failure = null;
        var complete = new System.Threading.ManualResetEventSlim(false);

        form.Shown += (_, _) =>
        {
            spinner.Start();
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    result = work();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally
                {
                    complete.Set();
                    if (!form.IsDisposed)
                    {
                        try
                        {
                            form.BeginInvoke((MethodInvoker)form.Close);
                        }
                        catch
                        {
                            // The form is already closing.
                        }
                    }
                }
            });
        };

        form.FormClosed += (_, _) => spinner.Stop();
        _ = form.ShowDialog();
        complete.Wait();

        if (failure != null)
        {
            throw failure;
        }

        return result!;
    }

    private static DialogResult ShowDialogCore(
        string title,
        string heading,
        string body,
        string primaryText,
        string? secondaryText,
        int autoCloseSeconds = 0)
    {
        ApplyStartupFontScale();
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
            Font = ControlDrawing.UiFont("Segoe UI Semibold", 12f, FontStyle.Bold),
            ForeColor = palette.Text,
            BackColor = palette.MenuBackground,
            MaximumSize = new Size(ControlDrawing.ScaleLogical(form, 460), 0)
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
            Font = ControlDrawing.UiFont("Segoe UI", 10f, FontStyle.Regular),
            ForeColor = palette.Text,
            BackColor = palette.MenuBackground,
            MaximumSize = new Size(ControlDrawing.ScaleLogical(form, 460), 0)
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
        form.MinimumSize = new Size(ControlDrawing.ScaleLogical(form, 520), 0);

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
            Font = ControlDrawing.UiFont("Segoe UI Semibold", 9.5f, FontStyle.Bold),
            MinimumSize = new Size(ControlDrawing.ScaleLogical(new Control(), 124), ControlDrawing.ScaleLogical(new Control(), 40)),
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

    private static void ApplyStartupFontScale()
    {
        ControlDrawing.UiFontScale = ReadStartupUiFontSize() switch
        {
            0 => 1f,
            2 => 1.28f,
            _ => 1.14f
        };
    }

    private static int ReadStartupUiFontSize()
    {
        try
        {
            string path = AppPaths.SettingsPath;
            if (!File.Exists(path))
            {
                return 1;
            }

            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (document.RootElement.TryGetProperty("UiFontSize", out JsonElement element) &&
                element.ValueKind == JsonValueKind.Number &&
                element.TryGetInt32(out int value) &&
                value is >= 0 and <= 2)
            {
                return value;
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write("StartupDialogs", "Could not read startup UI font size. " + ex.Message);
        }

        return 1;
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

    private sealed class StartupSpinnerControl : Control
    {
        private readonly ThemePalette _palette;
        private readonly System.Windows.Forms.Timer _timer;
        private int _frame;

        public StartupSpinnerControl(ThemePalette palette)
        {
            _palette = palette;
            DoubleBuffered = true;
            BackColor = palette.MenuBackground;
            _timer = new System.Windows.Forms.Timer { Interval = 70 };
            _timer.Tick += (_, _) =>
            {
                _frame = (_frame + 1) % 12;
                Invalidate();
            };
        }

        public void Start() => _timer.Start();

        public void Stop()
        {
            _timer.Stop();
            _timer.Dispose();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int side = Math.Min(ClientSize.Width, ClientSize.Height);
            if (side <= 0)
            {
                return;
            }

            float centerX = ClientSize.Width / 2f;
            float centerY = ClientSize.Height / 2f;
            float radius = Math.Max(8f, side * 0.34f);
            float dotSize = Math.Max(4f, side * 0.13f);
            Color activeColor = Color.FromArgb(96, 165, 250);

            for (int i = 0; i < 12; i++)
            {
                int age = (i - _frame + 12) % 12;
                int alpha = Math.Max(45, 255 - (age * 18));
                double angle = (Math.PI * 2 * i / 12) - (Math.PI / 2);
                float x = centerX + (float)Math.Cos(angle) * radius - dotSize / 2f;
                float y = centerY + (float)Math.Sin(angle) * radius - dotSize / 2f;
                using var brush = new SolidBrush(Color.FromArgb(alpha, activeColor));
                e.Graphics.FillEllipse(brush, x, y, dotSize, dotSize);
            }
        }
    }
}
