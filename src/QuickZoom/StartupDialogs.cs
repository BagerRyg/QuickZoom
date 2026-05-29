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

    public static void ShowTrayInfo(string title, string heading, string body)
    {
        ApplyStartupFontScale();
        UiLanguage language = UiText.GetStartupLanguage();
        ThemePalette palette = GetWindowsAppsUseDarkMode() ? ThemePalettes.Dark : ThemePalettes.Light;

        using var form = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.Manual,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            AutoScaleMode = AutoScaleMode.Dpi,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = palette.MenuBackground,
            ForeColor = palette.Text,
            Padding = new Padding(0),
            MinimumSize = new Size(ControlDrawing.ScaleLogical(new Control(), 420), 0)
        };
        form.HandleCreated += (_, _) => TrySetDarkTitleBar(form.Handle, palette.Equals(ThemePalettes.Dark));

        var root = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(18),
            Margin = new Padding(0),
            BackColor = palette.MenuBackground
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ControlDrawing.ScaleLogical(form, 46)));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var icon = new StartupInfoIconControl(palette)
        {
            Width = ControlDrawing.ScaleLogical(form, 30),
            Height = ControlDrawing.ScaleLogical(form, 30),
            Margin = new Padding(0, 2, 14, 0)
        };
        root.Controls.Add(icon, 0, 0);
        root.SetRowSpan(icon, 2);

        root.Controls.Add(new Label
        {
            Text = heading,
            AutoSize = true,
            Font = ControlDrawing.UiFont("Segoe UI Semibold", 10.5f, FontStyle.Bold),
            ForeColor = palette.Text,
            BackColor = Color.Transparent,
            MaximumSize = new Size(ControlDrawing.ScaleLogical(form, 340), 0),
            Margin = new Padding(0, 0, 0, 5)
        }, 1, 0);

        root.Controls.Add(new Label
        {
            Text = body,
            AutoSize = true,
            Font = ControlDrawing.UiFont("Segoe UI", 9f, FontStyle.Regular),
            ForeColor = palette.SecondaryText,
            BackColor = Color.Transparent,
            MaximumSize = new Size(ControlDrawing.ScaleLogical(form, 340), 0),
            Margin = new Padding(0)
        }, 1, 1);

        var ok = CreateButton(UiText.Get(language, "Common.Ok"), DialogResult.OK, palette, true);
        ok.Margin = new Padding(0, 14, 0, 0);
        root.Controls.Add(ok, 1, 2);

        form.Controls.Add(root);
        form.AcceptButton = ok;
        form.CancelButton = ok;
        form.Shown += (_, _) =>
        {
            Rectangle area = Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.WorkingArea;
            form.Location = new Point(area.Right - form.Width - 18, area.Bottom - form.Height - 18);
        };

        _ = form.ShowDialog();
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
            ClientSize = new Size(ControlDrawing.ScaleLogical(new Control(), 640), ControlDrawing.ScaleLogical(new Control(), 160)),
            BackColor = palette.MenuBackground,
            ForeColor = palette.Text,
            Padding = new Padding(0)
        };

        form.HandleCreated += (_, _) => TrySetDarkTitleBar(form.Handle, palette.Equals(ThemePalettes.Dark));

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(28, 22, 28, 18),
            BackColor = palette.MenuBackground
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ControlDrawing.ScaleLogical(form, 80)));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var spinner = new StartupSpinnerControl(palette)
        {
            Width = ControlDrawing.ScaleLogical(form, 64),
            Height = ControlDrawing.ScaleLogical(form, 48),
            Margin = new Padding(0, 2, 24, 0)
        };

        var headingLabel = new Label
        {
            AutoSize = true,
            Text = heading,
            Font = ControlDrawing.UiFont("Segoe UI Semibold", 12.5f, FontStyle.Bold),
            ForeColor = palette.Text,
            BackColor = palette.MenuBackground,
            MaximumSize = new Size(ControlDrawing.ScaleLogical(form, 500), 0),
            Margin = new Padding(0, 0, 0, 8)
        };

        var bodyLabel = new Label
        {
            AutoSize = true,
            Text = body,
            Font = ControlDrawing.UiFont("Segoe UI", 10f, FontStyle.Regular),
            ForeColor = palette.SecondaryText,
            BackColor = palette.MenuBackground,
            MaximumSize = new Size(ControlDrawing.ScaleLogical(form, 500), 0),
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
            Padding = new Padding(0),
            MinimumSize = new Size(ControlDrawing.ScaleLogical(new Control(), 560), 0)
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

        var headerPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 1,
            Padding = new Padding(28, 24, 28, 8),
            Margin = new Padding(0),
            BackColor = palette.MenuBackground
        };
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var headingLabel = new Label
        {
            AutoSize = true,
            Text = heading,
            Font = ControlDrawing.UiFont("Segoe UI Semibold", 12.2f, FontStyle.Bold),
            ForeColor = palette.Text,
            BackColor = palette.MenuBackground,
            MaximumSize = new Size(ControlDrawing.ScaleLogical(form, 500), 0)
        };
        headerPanel.Controls.Add(headingLabel, 0, 0);

        var bodyPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 1,
            Padding = new Padding(28, 0, 28, 22),
            Margin = new Padding(0),
            BackColor = palette.MenuBackground
        };
        bodyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bodyPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var bodyLabel = new Label
        {
            AutoSize = true,
            Text = body,
            Font = ControlDrawing.UiFont("Segoe UI", 10f, FontStyle.Regular),
            ForeColor = palette.SecondaryText,
            BackColor = palette.MenuBackground,
            MaximumSize = new Size(ControlDrawing.ScaleLogical(form, 500), 0)
        };
        bodyPanel.Controls.Add(bodyLabel, 0, 0);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(28, 16, 28, 18),
            Margin = new Padding(0),
            BackColor = palette.MenuBackground.GetBrightness() < 0.5f
                ? Color.FromArgb(18, 22, 29)
                : Color.FromArgb(246, 248, 251)
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
        form.MinimumSize = new Size(ControlDrawing.ScaleLogical(form, 560), 0);

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
        bool lightPalette = palette.MenuBackground.GetBrightness() > 0.65f;
        Color backColor = primary
            ? (lightPalette ? Color.FromArgb(242, 250, 245) : Color.FromArgb(24, 39, 31))
            : (lightPalette ? Color.FromArgb(253, 247, 247) : Color.FromArgb(38, 30, 32));
        Color borderColor = primary
            ? (lightPalette ? Color.FromArgb(92, 166, 112) : Color.FromArgb(72, 145, 96))
            : (lightPalette ? Color.FromArgb(190, 96, 100) : Color.FromArgb(122, 72, 76));
        Color hoverColor = primary
            ? (lightPalette ? Color.FromArgb(224, 244, 231) : Color.FromArgb(30, 54, 40))
            : (lightPalette ? Color.FromArgb(252, 236, 236) : Color.FromArgb(51, 34, 38));
        Color pressedColor = primary
            ? (lightPalette ? Color.FromArgb(206, 235, 216) : Color.FromArgb(36, 70, 50))
            : (lightPalette ? Color.FromArgb(247, 220, 220) : Color.FromArgb(66, 39, 44));
        Color textColor = primary
            ? (lightPalette ? Color.FromArgb(36, 103, 58) : Color.FromArgb(214, 244, 224))
            : (lightPalette ? Color.FromArgb(128, 50, 56) : Color.FromArgb(244, 210, 214));

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
            ForeColor = textColor,
            UseVisualStyleBackColor = false,
            FlatAppearance =
            {
                BorderColor = borderColor,
                MouseOverBackColor = hoverColor,
                MouseDownBackColor = pressedColor
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
        private float _largeAngle;
        private float _smallAngle;

        public StartupSpinnerControl(ThemePalette palette)
        {
            _palette = palette;
            DoubleBuffered = true;
            BackColor = palette.MenuBackground;
            _timer = new System.Windows.Forms.Timer { Interval = 16 };
            _timer.Tick += (_, _) =>
            {
                _largeAngle = (_largeAngle - 1.9f) % 360f;
                _smallAngle = (_smallAngle + 1.45f) % 360f;
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

            float scale = Math.Min(ClientSize.Width / 60f, ClientSize.Height / 40f);
            if (scale <= 0.1f)
            {
                return;
            }

            float originX = (ClientSize.Width - (60f * scale)) / 2f;
            float originY = (ClientSize.Height - (40f * scale)) / 2f;
            Color gearColor = _palette.MenuBackground.GetBrightness() < 0.5f
                ? Color.White
                : Color.FromArgb(37, 99, 235);
            using SolidBrush gearBrush = new(gearColor);
            using SolidBrush baseBrush = new(_palette.MenuBackground);

            DrawGear(
                e.Graphics,
                gearBrush,
                baseBrush,
                new RectangleF(originX, originY, 36f * scale, 36f * scale),
                _largeAngle,
                8f * scale,
                4f * scale,
                scale);
            DrawGear(
                e.Graphics,
                gearBrush,
                baseBrush,
                new RectangleF(originX + (35f * scale), originY + (15f * scale), 24f * scale, 24f * scale),
                _smallAngle,
                5f * scale,
                2.5f * scale,
                scale);
        }

        private static void DrawGear(
            Graphics graphics,
            Brush gearBrush,
            Brush baseBrush,
            RectangleF bounds,
            float angle,
            float centerHoleRadius,
            float outerHoleRadius,
            float scale)
        {
            float centerX = bounds.X + (bounds.Width / 2f);
            float centerY = bounds.Y + (bounds.Height / 2f);
            System.Drawing.Drawing2D.GraphicsState state = graphics.Save();
            graphics.TranslateTransform(centerX, centerY);
            graphics.RotateTransform(angle);
            graphics.TranslateTransform(-centerX, -centerY);

            graphics.FillEllipse(gearBrush, bounds);
            FillHole(graphics, baseBrush, centerX, centerY, centerHoleRadius);

            float left = bounds.Left;
            float top = bounds.Top;
            float right = bounds.Right;
            float bottom = bounds.Bottom;
            float nearRight = bounds.X + (bounds.Width * 0.83f);
            float nearLeft = bounds.X + (bounds.Width * 0.14f);
            float nearTop = bounds.Y + (bounds.Height * 0.14f);
            float nearBottom = bounds.Y + (bounds.Height * 0.83f);

            FillHole(graphics, baseBrush, centerX, top, outerHoleRadius);
            FillHole(graphics, baseBrush, left, centerY, outerHoleRadius);
            FillHole(graphics, baseBrush, right, centerY, outerHoleRadius);
            FillHole(graphics, baseBrush, centerX, bottom, outerHoleRadius);
            FillHole(graphics, baseBrush, nearRight, nearTop, outerHoleRadius);
            FillHole(graphics, baseBrush, nearRight, nearBottom, outerHoleRadius);
            FillHole(graphics, baseBrush, nearLeft, nearBottom, outerHoleRadius);
            FillHole(graphics, baseBrush, nearLeft, nearTop, outerHoleRadius);

            graphics.Restore(state);
        }

        private static void FillHole(Graphics graphics, Brush brush, float centerX, float centerY, float radius)
        {
            graphics.FillEllipse(brush, centerX - radius, centerY - radius, radius * 2f, radius * 2f);
        }
    }

    private sealed class StartupInfoIconControl : Control
    {
        private readonly ThemePalette _palette;

        public StartupInfoIconControl(ThemePalette palette)
        {
            _palette = palette;
            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int side = Math.Min(ClientSize.Width, ClientSize.Height) - 2;
            if (side <= 0)
            {
                return;
            }

            Rectangle rect = new((ClientSize.Width - side) / 2, (ClientSize.Height - side) / 2, side, side);
            Color fill = _palette.MenuBackground.GetBrightness() < 0.5f
                ? Color.FromArgb(34, 62, 92)
                : Color.FromArgb(224, 239, 255);
            Color stroke = _palette.MenuBackground.GetBrightness() < 0.5f
                ? Color.FromArgb(96, 165, 250)
                : Color.FromArgb(55, 118, 190);

            using SolidBrush fillBrush = new(fill);
            using Pen strokePen = new(stroke, 1.4f);
            e.Graphics.FillEllipse(fillBrush, rect);
            e.Graphics.DrawEllipse(strokePen, rect);

            TextRenderer.DrawText(
                e.Graphics,
                "i",
                ControlDrawing.UiFont("Segoe UI Semibold", 11f, FontStyle.Bold),
                rect,
                stroke,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}
