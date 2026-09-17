using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;

namespace QuickZoom;

internal static class StartupDialogs
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

    public static void ShowWarning(string title, string heading, string body)
    {
        ApplyStartupFontScale();
        ThemePalette palette = GetStartupPalette();
        using Form form = CreateStartupMessageForm(
            UiText.GetStartupLanguage(), palette, showInTaskbar: true, out _,
            warningHeading: heading, warningBody: body);
        form.Text = title;
        ShowStagedDialog(form, palette);
    }

    public static void ShowAlreadyRunning()
    {
        ApplyStartupFontScale();
        UiLanguage language = UiText.GetStartupLanguage();
        ThemePalette palette = GetStartupPalette();
        using Form form = CreateStartupMessageForm(language, palette, showInTaskbar: true, out _);
        ShowStagedDialog(form, palette);
    }

    private static void ShowStagedDialog(Form form, ThemePalette palette)
    {
        Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Point finalLocation = new(
            area.Left + Math.Max(0, (area.Width - form.Width) / 2),
            area.Top + Math.Max(0, (area.Height - form.Height) / 2));
        form.Opacity = 0;
        form.Location = new Point(
            SystemInformation.VirtualScreen.Left - form.Width - 200,
            SystemInformation.VirtualScreen.Top - form.Height - 200);
        _ = form.Handle;
        WindowChrome.TrySetDarkTitleBar(form, palette.Equals(ThemePalettes.Dark));
        bool cloaked = WindowChrome.TrySetCloaked(form, cloaked: true);
        form.Shown += (_, _) => form.BeginInvoke((MethodInvoker)(() =>
        {
            form.PerformLayout();
            WindowChrome.RedrawNow(form);
            form.Opacity = 1;
            form.Location = finalLocation;
            WindowChrome.RedrawNow(form);
            if (cloaked)
            {
                _ = WindowChrome.TrySetCloaked(form, cloaked: false);
            }

            ForceToForeground(form);
            if (form.AcceptButton is Control primaryAction) primaryAction.Focus();
        }));
        _ = form.ShowDialog();
    }

    internal static void CaptureSmoke(string outputDirectory, string? languageFilter = null)
    {
        Directory.CreateDirectory(outputDirectory);
        float previousScale = ControlDrawing.UiFontScale;
        bool previousFollowWindows = ControlDrawing.FollowWindowsTextScale;
        ControlDrawing.FollowWindowsTextScale = false;
        try
        {
            foreach (UiLanguage language in Enum.GetValues<UiLanguage>())
            {
                if (languageFilter != null && LocalizationManager.GetLanguageCode(language) != languageFilter) continue;
                foreach ((string themeName, ThemePalette palette) in new[]
                         {
                         ("dark", ThemePalettes.Dark),
                         ("light", ThemePalettes.Light)
                     })
                {
                    foreach (float textScale in new[] { 1f, 2.25f })
                    {
                        ControlDrawing.UiFontScale = textScale;
                        string languageName = LocalizationManager.GetLanguageCode(language);
                        string variantDirectory = Path.Combine(outputDirectory, themeName, textScale > 1f ? "text-225" : "font-default");
                        Directory.CreateDirectory(variantDirectory);
                        foreach ((string prefix, string? headingKey, string? bodyKey) in new[]
                        {
                            ("", (string?)null, (string?)null),
                            ("setup-error-", "Setup.SaveFailedTitle", "Setup.SaveFailedBody"),
                            ("settings-read-error-", "Settings.ReadFailedTitle", "Settings.ReadFailedBody"),
                            ("settings-save-error-", "Settings.SaveFailedTitle", "Settings.SaveFailedBody")
                        })
                        {
                            using Form form = CreateStartupMessageForm(
                                language,
                                palette,
                                showInTaskbar: false,
                                out _,
                                warningHeading: headingKey != null ? UiText.Get(language, headingKey) : null,
                                warningBody: bodyKey != null ? UiText.Get(language, bodyKey) : null);
                            Rectangle virtualScreen = SystemInformation.VirtualScreen;
                            form.Location = new Point(virtualScreen.Right + 64, virtualScreen.Bottom + 64);
                            form.Show();
                            WaitForCaptureUi();
                            ValidateDialogText(form);
                            if (form.AcceptButton is not Control { Visible: true, Enabled: true })
                                throw new InvalidOperationException("The startup message has no accessible primary action.");
                            using var bitmap = new Bitmap(form.Width, form.Height);
                            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                            bitmap.Save(Path.Combine(variantDirectory, prefix + languageName + ".png"));
                            form.Hide();
                        }
                    }
                }
            }
        }
        finally
        {
            ControlDrawing.UiFontScale = previousScale;
            ControlDrawing.FollowWindowsTextScale = previousFollowWindows;
        }

        static void ValidateDialogText(Control control)
        {
            if (control is Label { Visible: true } label)
            {
                Size required = TextRenderer.MeasureText(label.Text, label.Font,
                    new Size(Math.Max(1, label.ClientSize.Width), int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                if (required.Height > label.ClientSize.Height + 2)
                    throw new InvalidOperationException($"Startup dialog label is clipped: {label.Text}");
            }
            if (control is ModernButton { Visible: true } button)
            {
                Size required = button.GetPreferredSize(Size.Empty);
                if (required.Width > button.Width || required.Height > button.Height)
                    throw new InvalidOperationException($"Startup dialog button is clipped: {button.Text}");
            }
            foreach (Control child in control.Controls) ValidateDialogText(child);
        }
    }

    private static Form CreateStartupMessageForm(
        UiLanguage language,
        ThemePalette palette,
        bool showInTaskbar,
        out ModernButton closeButton,
        string? warningHeading = null,
        string? warningBody = null)
    {
        bool isWarning = warningHeading != null;
        string appName = UiText.Get(language, "Common.AppName");
        string heading = warningHeading ?? UiText.Get(language, "Startup.LatestAlreadyRunningHeading");
        string body = warningBody ?? UiText.Get(language, "Startup.LatestAlreadyRunningBody");
        string cardTitle = UiText.Get(language, "Startup.LatestAlreadyRunningCardTitle");
        string cardBody = UiText.Get(language, "Startup.LatestAlreadyRunningCardBody");
        var form = new Form
        {
            Text = appName,
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            ShowInTaskbar = showInTaskbar,
            AutoScaleMode = AutoScaleMode.Dpi,
            ClientSize = new Size(840, 500),
            MinimumSize = new Size(700, 430),
            BackColor = palette.Border,
            ForeColor = palette.Text,
            Padding = new Padding(1),
            KeyPreview = true,
            AccessibleRole = AccessibleRole.Dialog,
            AccessibleName = heading,
            AccessibleDescription = isWarning ? body : body + " " + cardBody
        };
        try
        {
            form.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // The dialog remains usable if Windows cannot read the executable icon.
        }

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(46, 26, 46, 22),
            Margin = Padding.Empty,
            BackColor = palette.MenuBackground
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "QuickZoom 3",
            TextAlign = ContentAlignment.MiddleLeft,
            Font = ControlDrawing.UiFont("Segoe UI", 20f, FontStyle.Bold),
            ForeColor = palette.Text,
            BackColor = palette.MenuBackground,
            Margin = Padding.Empty,
            AutoEllipsis = false
        };
        var divider = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = palette.Border,
            Margin = Padding.Empty
        };
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            AutoScroll = true,
            Padding = new Padding(0, 20, 0, 0),
            Margin = Padding.Empty,
            BackColor = palette.MenuBackground
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, isWarning ? 0 : 18));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 174));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var headingLabel = NewSplashLabel(
            heading,
            palette.Text,
            ControlDrawing.UiFont("Segoe UI", 15.5f, FontStyle.Bold));
        var bodyLabel = NewSplashLabel(
            body,
            palette.SecondaryText,
            ControlDrawing.UiFont("Segoe UI", 10.2f, FontStyle.Regular));
        content.Controls.Add(headingLabel, 0, 0);
        content.Controls.Add(bodyLabel, 0, 1);

        var card = new ModernSurfacePanel
        {
            Dock = DockStyle.Fill,
            BackColor = palette.ControlBackground,
            CornerRadius = 14,
            BorderAlpha = 38,
            Padding = new Padding(22),
            Margin = Padding.Empty
        };
        var cardLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = palette.ControlBackground,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        cardLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        cardLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        var infoIcon = new StartupInfoIconControl(palette)
        {
            Dock = DockStyle.Fill,
            BackColor = palette.ControlBackground,
            Margin = new Padding(0, 4, 18, 4),
            AccessibleName = cardTitle,
            AccessibleDescription = cardBody
        };
        var cardTitleLabel = NewSplashLabel(
            cardTitle,
            palette.Text,
            ControlDrawing.UiFont("Segoe UI", 12.5f, FontStyle.Bold));
        var cardBodyLabel = NewSplashLabel(
            cardBody,
            palette.SecondaryText,
            ControlDrawing.UiFont("Segoe UI", 9.6f, FontStyle.Regular));
        cardLayout.Controls.Add(infoIcon, 0, 0);
        cardLayout.SetRowSpan(infoIcon, 2);
        cardLayout.Controls.Add(cardTitleLabel, 1, 0);
        cardLayout.Controls.Add(cardBodyLabel, 1, 1);
        card.Controls.Add(cardLayout);
        content.Controls.Add(card, 0, 3);
        card.Visible = !isWarning;

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 14, 0, 0),
            Margin = Padding.Empty,
            BackColor = palette.MenuBackground
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        closeButton = new ModernButton
        {
            Text = UiText.Get(language, isWarning ? "Common.Ok" : "Common.Close"),
            DialogResult = DialogResult.OK,
            AutoSize = true,
            Size = new Size(210, 42),
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
            Margin = Padding.Empty,
            Font = ControlDrawing.UiFont("Segoe UI", 10.5f, FontStyle.Bold)
        };
        closeButton.ApplyTheme(palette, emphasis: isWarning);
        closeButton.SetProminentHover(
            ControlDrawing.Blend(palette.Accent, palette.Text, 54),
            ControlDrawing.Blend(palette.Accent, palette.Text, 118));
        footer.Controls.Add(closeButton, 1, 0);
        var openSettingsButton = new ModernButton
        {
            Text = UiText.Get(language, "Common.OpenSettingsWindow"),
            AccessibleName = UiText.Get(language, "Common.OpenSettingsWindow"),
            AutoSize = true,
            Font = ControlDrawing.UiFont("Segoe UI", 10.5f, FontStyle.Bold),
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
            Margin = Padding.Empty
        };
        openSettingsButton.ApplyTheme(palette, emphasis: true);
        openSettingsButton.Click += (_, _) =>
        {
            if (SettingsActivation.Request()) form.Close();
            else bodyLabel.Text = cardBody;
        };
        footer.Controls.Add(openSettingsButton, 0, 0);
        openSettingsButton.Visible = !isWarning;

        root.Controls.Add(titleLabel, 0, 0);
        root.Controls.Add(divider, 0, 1);
        root.Controls.Add(content, 0, 2);
        root.Controls.Add(footer, 0, 3);
        form.Controls.Add(root);
        form.AcceptButton = isWarning ? closeButton : openSettingsButton;
        form.CancelButton = closeButton;

        void BeginWindowDrag(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            _ = ReleaseCapture();
            _ = SendMessage(form.Handle, 0x00A1, new IntPtr(2), IntPtr.Zero);
        }

        titleLabel.MouseDown += BeginWindowDrag;
        divider.MouseDown += BeginWindowDrag;
        bool updatingLayout = false;
        ModernButton secondaryAction = closeButton;
        void UpdateMeasuredLayout()
        {
            if (updatingLayout) return;
            updatingLayout = true;
            try
            {
                int width = Math.Max(1, root.ClientSize.Width - root.Padding.Horizontal);
                int Measure(Label label, int availableWidth) => TextRenderer.MeasureText(label.Text, label.Font,
                    new Size(Math.Max(1, availableWidth), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
                root.RowStyles[0].Height = titleLabel.Font.Height + 20;
                content.RowStyles[0].Height = Measure(headingLabel, width) + 8;
                content.RowStyles[1].Height = Measure(bodyLabel, width) + 8;
                int cardTextWidth = width - card.Padding.Horizontal - (int)cardLayout.ColumnStyles[0].Width;
                cardLayout.RowStyles[0].Height = Measure(cardTitleLabel, cardTextWidth) + 8;
                content.RowStyles[3].Height = isWarning ? 0 : card.Padding.Vertical + cardLayout.RowStyles[0].Height +
                    Measure(cardBodyLabel, cardTextWidth) + 8;
                int buttonHeight = Math.Max(isWarning ? 0 : openSettingsButton.GetPreferredSize(Size.Empty).Height,
                    secondaryAction.GetPreferredSize(Size.Empty).Height);
                root.RowStyles[3].Height = buttonHeight + footer.Padding.Vertical + 8;
                footer.ColumnStyles[1].Width = secondaryAction.GetPreferredSize(Size.Empty).Width + 16;
                int requiredHeight = root.Padding.Vertical + (int)root.RowStyles[0].Height + 1 +
                    content.Padding.Vertical + (int)(content.RowStyles[0].Height + content.RowStyles[1].Height +
                    content.RowStyles[2].Height + content.RowStyles[3].Height + root.RowStyles[3].Height);
                Rectangle workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
                form.ClientSize = new Size(form.ClientSize.Width,
                    Math.Min(workArea.Height - 32, Math.Max(500, requiredHeight)));
            }
            finally { updatingLayout = false; }
        }
        Rectangle dialogArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        form.MinimumSize = new Size(Math.Min(700, dialogArea.Width - 32), Math.Min(430, dialogArea.Height - 32));
        form.ClientSize = new Size(Math.Min(dialogArea.Width - 32,
            840 + (int)(Math.Max(0, ControlDrawing.UiFontScale - 1f) * 320)), 500);
        _ = form.Handle;
        form.PerformLayout();
        UpdateMeasuredLayout();
        form.ClientSizeChanged += (_, _) => UpdateMeasuredLayout();
        bodyLabel.TextChanged += (_, _) => UpdateMeasuredLayout();
        return form;
    }

    private static Label NewSplashLabel(string text, Color color, Font font) => new()
    {
        Dock = DockStyle.Fill,
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft,
        Font = font,
        ForeColor = color,
        BackColor = Color.Transparent,
        Margin = Padding.Empty,
        AutoEllipsis = false,
        AccessibleRole = AccessibleRole.StaticText,
        AccessibleName = text
    };

    private static void WaitForCaptureUi()
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (timer.ElapsedMilliseconds < 140)
        {
            Application.DoEvents();
            System.Threading.Thread.Sleep(10);
        }
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

    private static void ForceToForeground(Form form)
    {
        form.TopMost = true;
        form.BringToFront();
        form.Activate();
        _ = SetForegroundWindow(form.Handle);
        form.BeginInvoke((MethodInvoker)(() =>
        {
            if (!form.IsDisposed)
            {
                form.TopMost = false;
            }
        }));
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

    private static ThemePalette GetStartupPalette() =>
        AppThemeBootstrap.ShouldUseDarkPalette(AppThemeBootstrap.ReadPersistedThemeMode())
            ? ThemePalettes.Dark
            : ThemePalettes.Light;

    private sealed class StartupInfoIconControl : Control
    {
        private readonly ThemePalette _palette;

        public StartupInfoIconControl(ThemePalette palette)
        {
            _palette = palette;
            DoubleBuffered = true;
            BackColor = palette.MenuBackground;
            AccessibleRole = AccessibleRole.Graphic;
            TabStop = false;
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

            using SolidBrush glyph = new(_palette.InformationText);
            float centerX = rect.Left + rect.Width / 2f;
            float thickness = Math.Max(2f, rect.Width * 0.09f);
            e.Graphics.FillEllipse(glyph, centerX - thickness / 2, rect.Top + rect.Height * 0.25f,
                thickness, thickness);
            e.Graphics.FillRectangle(glyph, centerX - thickness / 2, rect.Top + rect.Height * 0.43f,
                thickness, rect.Height * 0.3f);
        }
    }
}
