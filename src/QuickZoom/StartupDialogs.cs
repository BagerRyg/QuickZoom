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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

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

    public static void ShowAlreadyRunning()
    {
        ApplyStartupFontScale();
        UiLanguage language = UiText.GetStartupLanguage();
        ThemePalette palette = GetStartupPalette();
        using Form form = CreateAlreadyRunningForm(language, palette, showInTaskbar: true, out ModernButton closeButton);
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
            closeButton.Focus();
        }));
        _ = form.ShowDialog();
    }

    internal static void CaptureAlreadyRunningSmoke(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        foreach (UiLanguage language in Enum.GetValues<UiLanguage>())
        {
            foreach ((string themeName, ThemePalette palette) in new[]
                     {
                         ("dark", ThemePalettes.Dark),
                         ("light", ThemePalettes.Light)
                     })
            {
                string languageName = LocalizationManager.GetLanguageCode(language);
                string variantDirectory = Path.Combine(outputDirectory, themeName);
                Directory.CreateDirectory(variantDirectory);
                using Form form = CreateAlreadyRunningForm(
                    language,
                    palette,
                    showInTaskbar: false,
                    out _);
                Rectangle virtualScreen = SystemInformation.VirtualScreen;
                form.Location = new Point(virtualScreen.Right + 64, virtualScreen.Bottom + 64);
                form.Show();
                WaitForCaptureUi();
                using var bitmap = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                bitmap.Save(Path.Combine(variantDirectory, languageName + ".png"));
                form.Hide();
            }
        }
    }

    private static Form CreateAlreadyRunningForm(
        UiLanguage language,
        ThemePalette palette,
        bool showInTaskbar,
        out ModernButton closeButton)
    {
        string appName = UiText.Get(language, "Common.AppName");
        string heading = UiText.Get(language, "Startup.LatestAlreadyRunningHeading");
        string body = UiText.Get(language, "Startup.LatestAlreadyRunningBody");
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
            AccessibleDescription = body + " " + cardBody
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
            Padding = new Padding(0, 20, 0, 0),
            Margin = Padding.Empty,
            BackColor = palette.MenuBackground
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
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
            Text = UiText.Get(language, "Common.Close"),
            DialogResult = DialogResult.OK,
            AutoSize = false,
            Size = new Size(210, 42),
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
            Margin = Padding.Empty,
            Font = ControlDrawing.UiFont("Segoe UI", 10.5f, FontStyle.Bold)
        };
        closeButton.ApplyTheme(palette, emphasis: true);
        closeButton.SetProminentHover(
            ControlDrawing.Blend(palette.Accent, palette.Text, 54),
            ControlDrawing.Blend(palette.Accent, palette.Text, 118));
        footer.Controls.Add(closeButton, 1, 0);

        root.Controls.Add(titleLabel, 0, 0);
        root.Controls.Add(divider, 0, 1);
        root.Controls.Add(content, 0, 2);
        root.Controls.Add(footer, 0, 3);
        form.Controls.Add(root);
        form.AcceptButton = closeButton;
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

    public static void ShowTimedSuccess(string title, string heading, string body, int secondsUntilClose)
    {
        // Completion information must remain available until the user dismisses it.
        _ = secondsUntilClose;
        UiLanguage language = UiText.GetStartupLanguage();
        _ = ShowDialogCore(title, heading, body, UiText.Get(language, "Common.Continue"), null);
    }

    public static T ShowProgress<T>(string title, string heading, string body, Func<T> work)
    {
        ApplyStartupFontScale();
        ThemePalette palette = GetStartupPalette();

        using var form = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.Sizable,
            StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = true,
            MaximizeBox = false,
            ControlBox = true,
            ShowInTaskbar = true,
            AutoScaleMode = AutoScaleMode.Dpi,
            BackColor = palette.MenuBackground,
            ForeColor = palette.Text,
            Padding = new Padding(0),
            AccessibleRole = AccessibleRole.Dialog,
            AccessibleName = title,
            AccessibleDescription = heading + Environment.NewLine + body
        };
        SetResponsiveDialogSize(form, preferredClientWidth: 640, preferredClientHeight: 160, minimumClientWidth: 460, minimumClientHeight: 150);

        form.HandleCreated += (_, _) => TrySetDarkTitleBar(form.Handle, palette.Equals(ThemePalettes.Dark));

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = false,
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
        WindowChrome.TrySetDarkScrollBars(root, palette.MenuBackground.GetBrightness() < 0.5f);

        var spinner = new StartupSpinnerControl(palette)
        {
            Width = ControlDrawing.ScaleLogical(form, 64),
            Height = ControlDrawing.ScaleLogical(form, 48),
            Margin = new Padding(0, 2, 24, 0),
            AccessibleName = heading,
            AccessibleDescription = body
        };

        var headingLabel = new Label
        {
            AutoSize = true,
            Text = heading,
            Font = ControlDrawing.UiFont("Segoe UI Semibold", 12.5f, FontStyle.Bold),
            ForeColor = palette.Text,
            BackColor = palette.MenuBackground,
            MaximumSize = new Size(ControlDrawing.ScaleLogical(form, 500), 0),
            Margin = new Padding(0, 0, 0, 8),
            AccessibleRole = AccessibleRole.StaticText,
            AccessibleName = heading
        };

        var bodyLabel = new Label
        {
            AutoSize = true,
            Text = body,
            Font = ControlDrawing.UiFont("Segoe UI", 10f, FontStyle.Regular),
            ForeColor = palette.SecondaryText,
            BackColor = palette.MenuBackground,
            MaximumSize = new Size(ControlDrawing.ScaleLogical(form, 500), 0),
            Margin = new Padding(0),
            AccessibleRole = AccessibleRole.StaticText,
            AccessibleName = body
        };

        root.Controls.Add(spinner, 0, 0);
        root.SetRowSpan(spinner, 2);
        root.Controls.Add(headingLabel, 1, 0);
        root.Controls.Add(bodyLabel, 1, 1);
        form.Controls.Add(root);

        bool updatingLayout = false;

        void UpdateWrapWidth()
        {
            int width = Math.Max(
                ControlDrawing.ScaleLogical(form, 220),
                root.ClientSize.Width - root.Padding.Horizontal - (int)Math.Ceiling(root.ColumnStyles[0].Width));
            headingLabel.MaximumSize = new Size(width, 0);
            bodyLabel.MaximumSize = new Size(width, 0);
        }

        int MeasureContentHeight()
        {
            int textHeight =
                headingLabel.PreferredHeight + headingLabel.Margin.Vertical +
                bodyLabel.PreferredHeight + bodyLabel.Margin.Vertical;
            int spinnerHeight = spinner.Height + spinner.Margin.Vertical;
            return root.Padding.Vertical + Math.Max(textHeight, spinnerHeight);
        }

        void UpdateProgressLayout(bool fitToContent)
        {
            if (updatingLayout || form.IsDisposed)
            {
                return;
            }

            updatingLayout = true;
            try
            {
                root.AutoScroll = false;
                root.AutoScrollMinSize = Size.Empty;
                UpdateWrapWidth();
                root.PerformLayout();

                if (fitToContent)
                {
                    Rectangle area = Screen.FromControl(form).WorkingArea;
                    int chromeHeight = Math.Max(0, form.Height - form.ClientSize.Height);
                    int screenMargin = ControlDrawing.ScaleLogical(form, 24);
                    int maximumClientHeight = Math.Max(1, area.Height - (screenMargin * 2) - chromeHeight);
                    int minimumClientHeight = Math.Min(
                        maximumClientHeight,
                        Math.Max(1, form.MinimumSize.Height - chromeHeight));
                    int targetHeight = Math.Clamp(MeasureContentHeight(), minimumClientHeight, maximumClientHeight);
                    form.ClientSize = new Size(form.ClientSize.Width, targetHeight);

                    form.Location = new Point(
                        area.Left + Math.Max(0, (area.Width - form.Width) / 2),
                        area.Top + Math.Max(0, (area.Height - form.Height) / 2));
                }

                UpdateWrapWidth();
                root.PerformLayout();
                int contentHeight = MeasureContentHeight();
                int overflowTolerance = ControlDrawing.ScaleLogical(form, 2);
                bool needsScrollBar = contentHeight > root.ClientSize.Height + overflowTolerance;
                root.AutoScroll = needsScrollBar;
                root.AutoScrollMinSize = needsScrollBar ? new Size(0, contentHeight) : Size.Empty;
                root.HorizontalScroll.Enabled = false;
                root.HorizontalScroll.Visible = false;
            }
            finally
            {
                updatingLayout = false;
            }
        }

        form.ClientSizeChanged += (_, _) => UpdateProgressLayout(fitToContent: false);

        T? result = default;
        Exception? failure = null;
        var complete = new System.Threading.ManualResetEventSlim(false);
        int workCompleted = 0;

        form.FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing && System.Threading.Volatile.Read(ref workCompleted) == 0)
            {
                e.Cancel = true;
            }
        };

        form.Shown += (_, _) =>
        {
            UpdateProgressLayout(fitToContent: true);
            ForceToForeground(form);
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
                    System.Threading.Interlocked.Exchange(ref workCompleted, 1);
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
        string? secondaryText)
    {
        ApplyStartupFontScale();
        ThemePalette palette = GetStartupPalette();

        using var form = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.Sizable,
            StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = true,
            AutoScaleMode = AutoScaleMode.Dpi,
            AutoSize = false,
            BackColor = palette.MenuBackground,
            ForeColor = palette.Text,
            Padding = new Padding(0),
            AccessibleRole = AccessibleRole.Dialog,
            AccessibleName = title,
            AccessibleDescription = heading + Environment.NewLine + body
        };
        SetResponsiveDialogSize(form, preferredClientWidth: 640, preferredClientHeight: 300, minimumClientWidth: 420, minimumClientHeight: 240);

        form.HandleCreated += (_, _) => TrySetDarkTitleBar(form.Handle, palette.Equals(ThemePalettes.Dark));

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = palette.MenuBackground
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

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
            MaximumSize = new Size(ControlDrawing.ScaleLogical(form, 560), 0),
            AccessibleRole = AccessibleRole.StaticText,
            AccessibleName = heading
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
            MaximumSize = new Size(ControlDrawing.ScaleLogical(form, 560), 0),
            AccessibleRole = AccessibleRole.StaticText,
            AccessibleName = body
        };
        bodyPanel.Controls.Add(bodyLabel, 0, 0);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
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
        void UpdateWrapWidth()
        {
            int width = Math.Max(ControlDrawing.ScaleLogical(form, 280), form.ClientSize.Width - ControlDrawing.ScaleLogical(form, 64));
            headingLabel.MaximumSize = new Size(width, 0);
            bodyLabel.MaximumSize = new Size(width, 0);
        }

        form.ClientSizeChanged += (_, _) => UpdateWrapWidth();
        form.Shown += (_, _) =>
        {
            UpdateWrapWidth();
            ForceToForeground(form);
            primary.Focus();
        };

        return form.ShowDialog();
    }

    private static Button CreateButton(string text, DialogResult result, ThemePalette palette, bool primary)
    {
        if (AccessibilityPreferences.HighContrast)
        {
            return new Button
            {
                Text = text,
                DialogResult = result,
                AutoSize = true,
                Font = ControlDrawing.UiFont("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                MinimumSize = new Size(ControlDrawing.ScaleLogical(new Control(), 124), ControlDrawing.ScaleLogical(new Control(), 44)),
                Padding = new Padding(12, 4, 12, 4),
                Margin = new Padding(8, 0, 0, 0),
                FlatStyle = FlatStyle.System,
                BackColor = SystemColors.Control,
                ForeColor = SystemColors.ControlText,
                AccessibleRole = AccessibleRole.PushButton,
                AccessibleName = text,
                AccessibleDescription = text
            };
        }

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
            MinimumSize = new Size(ControlDrawing.ScaleLogical(new Control(), 124), ControlDrawing.ScaleLogical(new Control(), 44)),
            Padding = new Padding(12, 4, 12, 4),
            Margin = new Padding(8, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = textColor,
            UseVisualStyleBackColor = false,
            AccessibleRole = AccessibleRole.PushButton,
            AccessibleName = text,
            AccessibleDescription = text,
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

    private static void SetResponsiveDialogSize(
        Form form,
        int preferredClientWidth,
        int preferredClientHeight,
        int minimumClientWidth,
        int minimumClientHeight)
    {
        Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
        int screenMargin = ControlDrawing.ScaleLogical(form, 24);
        int maximumWidth = Math.Max(1, area.Width - (screenMargin * 2));
        int maximumHeight = Math.Max(1, area.Height - (screenMargin * 2));
        int scaledMinimumWidth = Math.Min(maximumWidth, ControlDrawing.ScaleLogical(form, minimumClientWidth));
        int scaledMinimumHeight = Math.Min(maximumHeight, ControlDrawing.ScaleLogical(form, minimumClientHeight));
        int width = Math.Clamp(ControlDrawing.ScaleLogical(form, preferredClientWidth), scaledMinimumWidth, maximumWidth);
        int height = Math.Clamp(ControlDrawing.ScaleLogical(form, preferredClientHeight), scaledMinimumHeight, maximumHeight);

        form.ClientSize = new Size(width, height);
        int chromeWidth = Math.Max(0, form.Width - form.ClientSize.Width);
        int chromeHeight = Math.Max(0, form.Height - form.ClientSize.Height);
        form.MinimumSize = new Size(scaledMinimumWidth + chromeWidth, scaledMinimumHeight + chromeHeight);
        form.MaximumSize = new Size(maximumWidth + chromeWidth, maximumHeight + chromeHeight);
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

    private static ThemePalette GetStartupPalette() =>
        AppThemeBootstrap.ShouldUseDarkPalette(AppThemeBootstrap.ReadPersistedThemeMode())
            ? ThemePalettes.Dark
            : ThemePalettes.Light;

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
            AccessibleRole = AccessibleRole.Graphic;
            TabStop = false;
            _timer = new System.Windows.Forms.Timer { Interval = 33 };
            _timer.Tick += (_, _) =>
            {
                _largeAngle = (_largeAngle - 1.9f) % 360f;
                _smallAngle = (_smallAngle + 1.45f) % 360f;
                Invalidate();
            };
        }

        public void Start()
        {
            if (AccessibilityPreferences.AnimationsEnabled)
            {
                _timer.Start();
            }
            else
            {
                _largeAngle = 0f;
                _smallAngle = 0f;
                Invalidate();
            }
        }

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
