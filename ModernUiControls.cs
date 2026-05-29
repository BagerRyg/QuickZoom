using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickZoom;

internal static class ControlDrawing
{
    internal static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int safeRadius = Math.Max(1, radius);
        int diameter = safeRadius * 2;
        GraphicsPath path = new();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    internal static void ApplyRoundedRegion(Control control, int radius)
    {
        if (control.Width <= 1 || control.Height <= 1)
        {
            return;
        }

        using GraphicsPath path = RoundedRect(new Rectangle(0, 0, control.Width - 1, control.Height - 1), radius);
        Region? oldRegion = control.Region;
        control.Region = new Region(path);
        oldRegion?.Dispose();
    }

    internal static int ScaleLogical(Control control, int logicalPixels)
    {
        float dpi = 96f;
        try
        {
            using Graphics g = control.CreateGraphics();
            dpi = g.DpiX;
        }
        catch
        {
            // Fall back to 100% scale if graphics are not ready yet.
        }

        return Math.Max(1, (int)Math.Round(logicalPixels * (dpi / 96f)));
    }

    internal static float UiFontScale { get; set; } = 1.14f;

    internal static Font UiFont(string familyName, float emSize, FontStyle style)
    {
        return new Font(familyName, Math.Max(7f, emSize * UiFontScale), style);
    }

    internal static Color EffectiveBackColor(Control control)
    {
        Control? current = control.Parent;
        while (current != null)
        {
            Color color = current.BackColor;
            if (color.A > 0 && color != Color.Transparent)
            {
                return color;
            }

            current = current.Parent;
        }

        return SystemColors.Control;
    }
}

internal static class WindowChrome
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    internal static void TrySetDarkTitleBar(Form form, bool enabled)
    {
        try
        {
            void Apply()
            {
                if (form.Handle == IntPtr.Zero)
                {
                    return;
                }

                int useDark = enabled ? 1 : 0;
                _ = DwmSetWindowAttribute(form.Handle, 20, ref useDark, sizeof(int));
                _ = DwmSetWindowAttribute(form.Handle, 19, ref useDark, sizeof(int));
            }

            if (form.IsHandleCreated)
            {
                Apply();
            }
            else
            {
                form.HandleCreated += (_, _) => Apply();
            }
        }
        catch
        {
            // Best effort.
        }
    }
}

internal interface ISurfaceBackgroundProvider
{
    Color SurfaceBackgroundColor { get; }
}

internal interface IChildSurfaceBackgroundRenderer
{
    void PaintChildSurfaceBackground(Graphics graphics, Rectangle childBounds);
}

internal static class ControlContrast
{
    internal static Color FieldBackground(ThemePalette palette) => palette.MenuBackground.GetBrightness() < 0.5f
        ? palette.ButtonBackground
        : Color.FromArgb(232, 238, 246);

    internal static Color FieldHover(ThemePalette palette) => palette.MenuBackground.GetBrightness() < 0.5f
        ? Color.FromArgb(38, 46, 58)
        : Color.FromArgb(220, 230, 242);

    internal static Color FieldPressed(ThemePalette palette) => palette.MenuBackground.GetBrightness() < 0.5f
        ? Color.FromArgb(45, 54, 68)
        : Color.FromArgb(208, 222, 238);

    internal static Color FieldBorder(ThemePalette palette) => palette.MenuBackground.GetBrightness() < 0.5f
        ? Color.FromArgb(82, 94, 112)
        : Color.FromArgb(142, 156, 174);

    internal static Color SubtleTrack(ThemePalette palette) => palette.MenuBackground.GetBrightness() < 0.5f
        ? Color.FromArgb(28, 33, 42)
        : Color.FromArgb(218, 227, 238);
}

internal class ModernSurfacePanel : Panel
{
    private int _cornerRadius = 16;
    private int _borderAlpha = 26;

    public ModernSurfacePanel()
    {
        DoubleBuffered = true;
        Resize += (_, _) => ControlDrawing.ApplyRoundedRegion(this, _cornerRadius);
    }

    public int CornerRadius
    {
        get => _cornerRadius;
        set
        {
            _cornerRadius = Math.Max(6, value);
            ControlDrawing.ApplyRoundedRegion(this, _cornerRadius);
            Invalidate();
        }
    }

    public int BorderAlpha
    {
        get => _borderAlpha;
        set
        {
            _borderAlpha = Math.Clamp(value, 0, 255);
            Invalidate();
        }
    }

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        ControlDrawing.ApplyRoundedRegion(this, _cornerRadius);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using GraphicsPath path = ControlDrawing.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), _cornerRadius);
        Color borderBase = BackColor.GetBrightness() > 0.72f
            ? Color.FromArgb(112, 124, 139)
            : Color.White;
        using Pen borderPen = new(Color.FromArgb(_borderAlpha, borderBase));
        e.Graphics.DrawPath(borderPen, path);
    }
}

internal sealed class ToggleSwitchControl : Control
{
    private bool _isOn;
    private ThemePalette _palette;
    private bool _hovered;
    private bool _pressed;

    public ToggleSwitchControl(ThemePalette palette)
    {
        _palette = palette;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.UserPaint |
            ControlStyles.Selectable,
            true);
        Size = new Size(42, 24);
        Cursor = Cursors.Hand;
        TabStop = true;
        BackColor = Color.Transparent;
    }

    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (_isOn == value)
            {
                return;
            }

            _isOn = value;
            Invalidate();
        }
    }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        if (!Enabled)
        {
            base.OnMouseEnter(e);
            return;
        }

        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (!Enabled)
        {
            base.OnMouseDown(e);
            return;
        }

        _pressed = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnClick(EventArgs e)
    {
        if (!Enabled)
        {
            return;
        }

        IsOn = !IsOn;
        base.OnClick(e);
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!Enabled)
        {
            base.OnKeyDown(e);
            return;
        }

        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            IsOn = !IsOn;
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        Rectangle trackRect = new(0, 2, Width - 1, Height - 4);
        using GraphicsPath trackPath = ControlDrawing.RoundedRect(trackRect, trackRect.Height / 2);

        Color trackColor = !Enabled
            ? ControlContrast.FieldBackground(_palette)
            : _isOn
            ? (_hovered ? _palette.AccentHover : _palette.Accent)
            : (_hovered ? ControlContrast.FieldHover(_palette) : ControlContrast.FieldBackground(_palette));

        if (Enabled && _pressed)
        {
            trackColor = _isOn ? _palette.AccentPressed : ControlContrast.FieldPressed(_palette);
        }

        using SolidBrush trackBrush = new(trackColor);
        using Pen borderPen = new(ControlContrast.FieldBorder(_palette));
        e.Graphics.FillPath(trackBrush, trackPath);
        e.Graphics.DrawPath(borderPen, trackPath);

        int knobSize = trackRect.Height - 4;
        int knobX = _isOn ? trackRect.Right - knobSize - 2 : trackRect.Left + 2;
        Rectangle knobRect = new(knobX, trackRect.Top + 2, knobSize, knobSize);

        using SolidBrush knobBrush = new(Enabled ? Color.FromArgb(245, 247, 250) : Color.FromArgb(160, 164, 170));
        using Pen knobBorder = new(Enabled ? Color.FromArgb(48, 52, 56) : Color.FromArgb(96, 100, 106));
        e.Graphics.FillEllipse(knobBrush, knobRect);
        e.Graphics.DrawEllipse(knobBorder, knobRect);

        if (Enabled && Focused)
        {
            Rectangle focusRect = new(0, 0, Width - 1, Height - 1);
            using GraphicsPath focusPath = ControlDrawing.RoundedRect(focusRect, focusRect.Height / 2);
            using Pen focusPen = new(Color.FromArgb(140, _palette.Accent), 1.5f);
            e.Graphics.DrawPath(focusPen, focusPath);
        }
    }

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        Size = new Size(ControlDrawing.ScaleLogical(this, 42), ControlDrawing.ScaleLogical(this, 24));
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        Color backColor = Parent is ISurfaceBackgroundProvider provider
            ? provider.SurfaceBackgroundColor
            : ControlDrawing.EffectiveBackColor(this);
        using SolidBrush brush = new(backColor);
        pevent.Graphics.FillRectangle(brush, ClientRectangle);
    }
}

internal sealed class QuickActionTile : ModernSurfacePanel
{
    private readonly Panel _dot;
    private readonly Label _titleLabel;
    private readonly Label _stateLabel;
    private readonly ToggleSwitchControl _toggle;
    private ThemePalette _palette;
    private bool _hovered;

    public QuickActionTile(ThemePalette palette, string iconText, string title, string stateText, bool isOn)
    {
        _palette = palette;
        CornerRadius = 16;
        Width = 372;
        Height = 58;
        Margin = new Padding(0, 0, 0, 10);
        Padding = new Padding(14, 10, 14, 10);
        Cursor = Cursors.Hand;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 22));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56));

        _dot = new Panel
        {
            Width = 14,
            Height = 14,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 10, 10, 10),
            BackColor = Color.Transparent
        };
        _dot.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle circleRect = new(0, 0, _dot.Width - 1, _dot.Height - 1);
            using SolidBrush fill = new(Color.FromArgb(32, _palette.Accent));
            using Pen border = new(Color.FromArgb(72, _palette.Accent));
            e.Graphics.FillEllipse(fill, circleRect);
            e.Graphics.DrawEllipse(border, circleRect);
        };

        var textRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };

        _titleLabel = new Label
        {
            Text = title,
            AutoSize = true,
            Font = ControlDrawing.UiFont("Segoe UI Semibold", 10.5f, FontStyle.Bold),
            Margin = new Padding(0, 6, 8, 0),
            BackColor = Color.Transparent
        };

        _stateLabel = new Label
        {
            Text = stateText,
            AutoSize = true,
            Font = ControlDrawing.UiFont("Segoe UI", 9f, FontStyle.Regular),
            Margin = new Padding(0, 8, 0, 0),
            BackColor = Color.Transparent
        };

        _toggle = new ToggleSwitchControl(palette)
        {
            IsOn = isOn,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 4, 0, 0)
        };

        textRow.Controls.Add(_titleLabel);
        textRow.Controls.Add(_stateLabel);
        layout.Controls.Add(_dot, 0, 0);
        layout.Controls.Add(textRow, 1, 0);
        layout.Controls.Add(_toggle, 2, 0);
        Controls.Add(layout);

        foreach (Control control in new Control[] { this, layout, _dot, _titleLabel, _stateLabel })
        {
            control.Click += (_, _) => ActionRequested?.Invoke(this, EventArgs.Empty);
            control.MouseEnter += (_, _) => SetHovered(true);
            control.MouseLeave += (_, _) => SetHovered(false);
        }

        _toggle.Click += (_, _) => ActionRequested?.Invoke(this, EventArgs.Empty);
        ApplyTheme(palette);
    }

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        Width = ControlDrawing.ScaleLogical(this, 372);
        Height = ControlDrawing.ScaleLogical(this, 58);
        Padding = new Padding(
            ControlDrawing.ScaleLogical(this, 14),
            ControlDrawing.ScaleLogical(this, 10),
            ControlDrawing.ScaleLogical(this, 14),
            ControlDrawing.ScaleLogical(this, 10));
    }

    public event EventHandler? ActionRequested;

    public bool IsOn
    {
        get => _toggle.IsOn;
        set => _toggle.IsOn = value;
    }

    public string StateText
    {
        get => _stateLabel.Text;
        set => _stateLabel.Text = value;
    }

    public ToggleSwitchControl Toggle => _toggle;

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        BackColor = _hovered ? palette.ButtonHover : palette.ControlBackground;
        BorderAlpha = _hovered ? 38 : 24;
        _titleLabel.ForeColor = palette.Text;
        _stateLabel.ForeColor = palette.SecondaryText;
        _toggle.ApplyTheme(palette);
        _dot.Invalidate();
        Invalidate(true);
    }

    private void SetHovered(bool hovered)
    {
        _hovered = hovered;
        ApplyTheme(_palette);
    }
}

internal sealed class TrayMenuSectionLabel : Label
{
    public TrayMenuSectionLabel()
    {
        AutoSize = true;
        Margin = new Padding(8, 6, 8, 3);
        Font = ControlDrawing.UiFont("Segoe UI Semibold", 8.25f, FontStyle.Bold);
        BackColor = Color.Transparent;
    }

    public void ApplyTheme(ThemePalette palette)
    {
        ForeColor = palette.SecondaryText;
    }
}

internal sealed class TrayMenuDivider : Control
{
    private ThemePalette _palette;

    public TrayMenuDivider(ThemePalette palette)
    {
        _palette = palette;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.UserPaint,
            true);
        Height = 8;
        Margin = new Padding(0, 3, 0, 3);
        BackColor = Color.Transparent;
    }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        int y = Height / 2;
        using Pen pen = new(Color.FromArgb(56, _palette.Border));
        e.Graphics.DrawLine(pen, 8, y, Width - 8, y);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        using SolidBrush brush = new(ControlDrawing.EffectiveBackColor(this));
        pevent.Graphics.FillRectangle(brush, ClientRectangle);
    }
}

internal sealed class TrayMenuRow : Control, ISurfaceBackgroundProvider, IChildSurfaceBackgroundRenderer
{
    private readonly FluentIconControl? _iconControl;
    private readonly Label _titleLabel;
    private readonly Label? _rightLabel;
    private readonly ToggleSwitchControl? _toggle;
    private ThemePalette _palette;
    private bool _hovered;
    private bool _pressed;
    private bool _active;
    private bool _isDestructive;
    private bool _isSuccess;

    public TrayMenuRow(ThemePalette palette, string title, string? rightText = null, ToggleSwitchControl? toggle = null, TrayFluentIcon? icon = null)
    {
        _palette = palette;
        SuspendLayout();
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.UserPaint |
            ControlStyles.Selectable,
            true);

        Margin = new Padding(0, 0, 0, 2);
        Padding = new Padding(12, 0, 12, 0);
        Cursor = Cursors.Hand;
        TabStop = true;
        BackColor = Color.Transparent;

        if (icon.HasValue)
        {
            _iconControl = new FluentIconControl(palette, icon.Value);
            Controls.Add(_iconControl);
        }

        _titleLabel = new Label
        {
            Text = title,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = ControlDrawing.UiFont("Segoe UI Semibold", 9.75f, FontStyle.Bold),
            BackColor = Color.Transparent
        };
        Controls.Add(_titleLabel);

        if (toggle != null)
        {
            _toggle = toggle;
            _toggle.BackColor = Color.Transparent;
            Controls.Add(_toggle);
        }
        else if (!string.IsNullOrWhiteSpace(rightText))
        {
            _rightLabel = new Label
            {
                Text = rightText,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Font = ControlDrawing.UiFont("Segoe UI", 8.75f, FontStyle.Regular),
                BackColor = Color.Transparent
            };
            Controls.Add(_rightLabel);
        }

        foreach (Control control in Controls)
        {
            control.MouseEnter += (_, _) => SetState(hovered: true, pressed: _pressed);
            control.MouseLeave += (_, _) => SetState(hovered: false, pressed: false);
            control.MouseDown += (_, _) => SetState(hovered: true, pressed: true);
            control.MouseUp += (_, _) => SetState(hovered: true, pressed: false);
        }

        _titleLabel.Click += (_, _) => ActionRequested?.Invoke(this, EventArgs.Empty);
        if (_rightLabel != null)
        {
            _rightLabel.Click += (_, _) => ActionRequested?.Invoke(this, EventArgs.Empty);
        }

        if (_toggle != null)
        {
            _toggle.Click += (_, _) => ActionRequested?.Invoke(this, EventArgs.Empty);
        }

        Height = ControlDrawing.ScaleLogical(this, fontScaleAwareLogicalHeight());
        ApplyTheme(palette);
        ResumeLayout(performLayout: true);

        int fontScaleAwareLogicalHeight()
        {
            return 32 + (int)Math.Round((ControlDrawing.UiFontScale - 1f) * 18f);
        }
    }

    public event EventHandler? ActionRequested;

    public string Title
    {
        get => _titleLabel.Text;
        set => _titleLabel.Text = value;
    }

    public string RightText
    {
        get => _rightLabel?.Text ?? string.Empty;
        set
        {
            if (_rightLabel != null)
            {
                _rightLabel.Text = value;
            }
        }
    }

    public bool Active
    {
        get => _active;
        set
        {
            _active = value;
            Invalidate();
        }
    }

    public bool IsDestructive
    {
        get => _isDestructive;
        set
        {
            _isDestructive = value;
            Invalidate();
        }
    }

    public bool IsSuccess
    {
        get => _isSuccess;
        set
        {
            _isSuccess = value;
            Invalidate(true);
        }
    }

    public Color SurfaceBackgroundColor
    {
        get
        {
            if (_isSuccess)
            {
                return _palette.MenuBackground.GetBrightness() > 0.65f
                    ? Color.FromArgb(217, 247, 225)
                    : Color.FromArgb(24, 94, 54);
            }

            if (_hovered || _pressed || _active)
            {
                if (_isDestructive)
                {
                    if (_palette.MenuBackground.GetBrightness() > 0.65f)
                    {
                        return _pressed
                            ? Color.FromArgb(255, 207, 207)
                            : Color.FromArgb(255, 224, 224);
                    }

                    return _pressed
                        ? Color.FromArgb(108, 28, 36)
                        : Color.FromArgb(84, 24, 31);
                }

                return _pressed
                    ? _palette.ButtonPressed
                    : _hovered
                        ? _palette.ButtonHover
                        : Color.FromArgb(28, _palette.Accent);
            }

            return ControlDrawing.EffectiveBackColor(this);
        }
    }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        _titleLabel.ForeColor = palette.Text;
        if (_iconControl != null)
        {
            _iconControl.ApplyTheme(palette);
        }
        if (_rightLabel != null)
        {
            _rightLabel.ForeColor = palette.SecondaryText;
        }

        _toggle?.ApplyTheme(palette);
        Invalidate();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (_titleLabel == null)
        {
            return;
        }

        int innerHeight = Height - Padding.Vertical;
        int y = Padding.Top;
        int right = Width - Padding.Right;
        int left = Padding.Left;

        if (_iconControl != null)
        {
            int iconWidth = ControlDrawing.ScaleLogical(this, 20);
            int iconHeight = Math.Min(innerHeight, ControlDrawing.ScaleLogical(this, 18));
            _iconControl.Bounds = new Rectangle(left, y + Math.Max(0, (innerHeight - iconHeight) / 2), iconWidth, iconHeight);
            left = _iconControl.Right + ControlDrawing.ScaleLogical(this, 10);
        }

        if (_toggle != null)
        {
            Size toggleSize = _toggle.Size;
            _toggle.Location = new Point(right - toggleSize.Width, y + Math.Max(0, (innerHeight - toggleSize.Height) / 2));
            _titleLabel.Bounds = new Rectangle(left, y, Math.Max(ControlDrawing.ScaleLogical(this, 48), _toggle.Left - left - ControlDrawing.ScaleLogical(this, 14)), innerHeight);
        }
        else if (_rightLabel != null)
        {
            int accessoryWidth = Math.Max(72, Math.Min(120, TextRenderer.MeasureText(_rightLabel.Text, _rightLabel.Font).Width + 8));
            _rightLabel.Bounds = new Rectangle(right - accessoryWidth, y, accessoryWidth, innerHeight);
            _titleLabel.Bounds = new Rectangle(left, y, Math.Max(40, _rightLabel.Left - left - 10), innerHeight);
        }
        else
        {
            _titleLabel.Bounds = new Rectangle(left, y, Width - Padding.Right - left, innerHeight);
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        SetState(hovered: true, pressed: _pressed);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetState(hovered: false, pressed: false);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        SetState(_hovered, pressed: true);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        SetState(_hovered, pressed: false);
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        ActionRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            ActionRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        using SolidBrush brush = new(ControlDrawing.EffectiveBackColor(this));
        pevent.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        if (_hovered || _pressed || _active || _isSuccess)
        {
            Rectangle fillRect = new(4, 1, Math.Max(8, Width - 8), Math.Max(8, Height - 2));
            using GraphicsPath path = ControlDrawing.RoundedRect(fillRect, 9);
            Color fill = SurfaceBackgroundColor;
            using SolidBrush fillBrush = new(fill);
            Color borderColor = _isDestructive
                ? Color.FromArgb(186, 82, 92)
                : (_isSuccess ? _palette.Accent : (_active ? _palette.Accent : _palette.Border));
            int borderAlpha = _isDestructive ? 76 : (_isSuccess ? 96 : (_active ? 72 : 28));
            using Pen borderPen = new(Color.FromArgb(borderAlpha, borderColor));
            e.Graphics.FillPath(fillBrush, path);
            e.Graphics.DrawPath(borderPen, path);
        }

    }

    public void PaintChildSurfaceBackground(Graphics graphics, Rectangle childBounds)
    {
        using Region clip = graphics.Clip?.Clone() ?? new Region(childBounds);
        graphics.SetClip(childBounds);

        using SolidBrush backgroundBrush = new(ControlDrawing.EffectiveBackColor(this));
        graphics.FillRectangle(backgroundBrush, childBounds);

        if (_hovered || _pressed || _active || _isSuccess)
        {
            Rectangle fillRect = new(4, 1, Math.Max(8, Width - 8), Math.Max(8, Height - 2));
            using GraphicsPath path = ControlDrawing.RoundedRect(fillRect, 9);
            using SolidBrush fillBrush = new(SurfaceBackgroundColor);
            graphics.FillPath(fillBrush, path);
        }

        graphics.Clip = clip;
    }

    private void SetState(bool hovered, bool pressed)
    {
        _hovered = hovered;
        _pressed = pressed;
        Invalidate();
    }

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        Height = ControlDrawing.ScaleLogical(this, 32);
        Padding = new Padding(
            ControlDrawing.ScaleLogical(this, 12),
            0,
            ControlDrawing.ScaleLogical(this, 12),
            0);
    }
}

internal sealed class KeyBadgeControl : Control, ISurfaceBackgroundProvider
{
    private ThemePalette _palette;
    private bool _hovered;
    private bool _pressed;

    public KeyBadgeControl(ThemePalette palette, string text)
    {
        _palette = palette;
        Text = text;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.UserPaint |
            ControlStyles.Selectable,
            true);
        BackColor = Color.Transparent;
        Size = new Size(180, 74);
        Margin = new Padding(0);
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        Invalidate();
    }

    protected override void OnTextChanged(EventArgs e)
    {
        Invalidate();
        base.OnTextChanged(e);
    }

    public Color SurfaceBackgroundColor => _pressed
        ? ControlContrast.FieldPressed(_palette)
        : (_hovered ? ControlContrast.FieldHover(_palette) : ControlContrast.FieldBackground(_palette));

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _pressed = true;
        Focus();
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            OnClick(EventArgs.Empty);
        }

        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        Rectangle rect = new(0, 0, Width - 1, Height - 1);
        using GraphicsPath path = ControlDrawing.RoundedRect(rect, 12);
        using SolidBrush fill = new(SurfaceBackgroundColor);
        using Pen border = new(ControlContrast.FieldBorder(_palette));
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);

        Rectangle innerRect = new(5, 5, Width - 11, Height - 11);
        using GraphicsPath innerPath = ControlDrawing.RoundedRect(innerRect, 10);
        using Pen innerPen = new(Color.FromArgb(55, Color.White));
        e.Graphics.DrawPath(innerPen, innerPath);

        int iconSize = 24;
        Rectangle iconRect = new(12, (Height - iconSize) / 2, iconSize, iconSize);
        using Pen iconPen = new(_palette.Text, 1.8f);
        if (Text.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
            Text.Contains("Windows", StringComparison.OrdinalIgnoreCase))
        {
            DrawWindowsLogo(e.Graphics, iconPen, iconRect);
        }
        else
        {
            e.Graphics.DrawRectangle(iconPen, iconRect);
            e.Graphics.DrawLine(iconPen, iconRect.Left + 5, iconRect.Top + 7, iconRect.Right - 5, iconRect.Top + 7);
            e.Graphics.DrawLine(iconPen, iconRect.Left + 5, iconRect.Top + 13, iconRect.Right - 5, iconRect.Top + 13);
        }

        Rectangle textRect = new(iconRect.Right + 10, 0, Width - iconRect.Right - 22, Height);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            ControlDrawing.UiFont("Segoe UI Semibold", 11f, FontStyle.Bold),
            textRect,
            _palette.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        if (Focused)
        {
            using Pen focusPen = new(Color.FromArgb(120, _palette.Accent), 1.5f);
            e.Graphics.DrawPath(focusPen, path);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        Color backColor = Parent is ISurfaceBackgroundProvider provider
            ? provider.SurfaceBackgroundColor
            : ControlDrawing.EffectiveBackColor(this);
        using SolidBrush brush = new(backColor);
        pevent.Graphics.FillRectangle(brush, ClientRectangle);
    }

    private static void DrawWindowsLogo(Graphics graphics, Pen pen, Rectangle rect)
    {
        Point[] topLeft =
        [
            new(rect.Left + 2, rect.Top + 3),
            new(rect.Left + rect.Width / 2 - 1, rect.Top + 2),
            new(rect.Left + rect.Width / 2 - 1, rect.Top + rect.Height / 2 - 1),
            new(rect.Left + 2, rect.Top + rect.Height / 2 - 2)
        ];
        Point[] topRight =
        [
            new(rect.Left + rect.Width / 2 + 2, rect.Top + 2),
            new(rect.Right - 2, rect.Top),
            new(rect.Right - 2, rect.Top + rect.Height / 2 - 1),
            new(rect.Left + rect.Width / 2 + 2, rect.Top + rect.Height / 2 - 1)
        ];
        Point[] bottomLeft =
        [
            new(rect.Left + 2, rect.Top + rect.Height / 2 + 2),
            new(rect.Left + rect.Width / 2 - 1, rect.Top + rect.Height / 2 + 1),
            new(rect.Left + rect.Width / 2 - 1, rect.Bottom - 2),
            new(rect.Left + 2, rect.Bottom - 4)
        ];
        Point[] bottomRight =
        [
            new(rect.Left + rect.Width / 2 + 2, rect.Top + rect.Height / 2 + 1),
            new(rect.Right - 2, rect.Top + rect.Height / 2 + 1),
            new(rect.Right - 2, rect.Bottom),
            new(rect.Left + rect.Width / 2 + 2, rect.Bottom - 2)
        ];

        using SolidBrush brush = new(pen.Color);
        graphics.FillPolygon(brush, topLeft);
        graphics.FillPolygon(brush, topRight);
        graphics.FillPolygon(brush, bottomLeft);
        graphics.FillPolygon(brush, bottomRight);
    }
}

internal sealed class ModernActionRow : ModernSurfacePanel
{
    private readonly Label _titleLabel;
    private readonly Label _subtitleLabel;
    private readonly Label _accessoryLabel;
    private ThemePalette _palette;
    private bool _hovered;
    private bool _active;
    private readonly bool _hasSubtitle;

    public ModernActionRow(ThemePalette palette, string title, string subtitle = "")
    {
        _palette = palette;
        _hasSubtitle = !string.IsNullOrWhiteSpace(subtitle);
        CornerRadius = 14;
        Width = 380;
        Height = _hasSubtitle ? 70 : 54;
        Margin = new Padding(0, 0, 0, 10);
        Padding = new Padding(16, _hasSubtitle ? 10 : 8, 16, _hasSubtitle ? 10 : 8);
        Cursor = Cursors.Hand;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));

        var textStack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        textStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        if (_hasSubtitle)
        {
            textStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        _titleLabel = new Label
        {
            Text = title,
            AutoSize = true,
            Font = ControlDrawing.UiFont("Segoe UI Semibold", 10.2f, FontStyle.Bold),
            Margin = new Padding(0),
            BackColor = Color.Transparent
        };
        _subtitleLabel = new Label
        {
            Text = subtitle,
            AutoSize = true,
            Font = ControlDrawing.UiFont("Segoe UI", 9f, FontStyle.Regular),
            Margin = new Padding(0, 4, 0, 0),
            MaximumSize = new Size(420, 0),
            BackColor = Color.Transparent
        };
        textStack.Controls.Add(_titleLabel, 0, 0);
        if (_hasSubtitle)
        {
            textStack.Controls.Add(_subtitleLabel, 0, 1);
        }

        _accessoryLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = ControlDrawing.UiFont("Segoe UI Semibold", 9f, FontStyle.Bold),
            BackColor = Color.Transparent,
            AutoEllipsis = true
        };

        layout.Controls.Add(textStack, 0, 0);
        layout.Controls.Add(_accessoryLabel, 1, 0);
        Controls.Add(layout);

        foreach (Control control in new Control[] { this, layout, textStack, _titleLabel, _subtitleLabel, _accessoryLabel })
        {
            control.Click += (_, _) => OnClick(EventArgs.Empty);
            control.MouseEnter += (_, _) => SetHovered(true);
            control.MouseLeave += (_, _) => SetHovered(false);
        }

        ApplyTheme(palette);
    }

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        Width = ControlDrawing.ScaleLogical(this, 380);
        Height = ControlDrawing.ScaleLogical(this, _hasSubtitle ? 70 : 54);
        Padding = new Padding(
            ControlDrawing.ScaleLogical(this, 16),
            ControlDrawing.ScaleLogical(this, _hasSubtitle ? 10 : 8),
            ControlDrawing.ScaleLogical(this, 16),
            ControlDrawing.ScaleLogical(this, _hasSubtitle ? 10 : 8));
    }

    public string Title
    {
        get => _titleLabel.Text;
        set => _titleLabel.Text = value;
    }

    public string Subtitle
    {
        get => _subtitleLabel.Text;
        set => _subtitleLabel.Text = value;
    }

    public string AccessoryText
    {
        get => _accessoryLabel.Text;
        set => _accessoryLabel.Text = value;
    }

    public bool IsActive
    {
        get => _active;
        set
        {
            _active = value;
            ApplyTheme(_palette);
        }
    }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        BackColor = _hovered ? palette.ButtonHover : palette.ControlBackground;
        if (_active)
        {
            BackColor = Color.FromArgb(46, palette.Accent);
            BorderAlpha = 54;
        }
        else
        {
            BorderAlpha = _hovered ? 34 : 20;
        }

        _titleLabel.ForeColor = palette.Text;
        _subtitleLabel.ForeColor = palette.SecondaryText;
        _accessoryLabel.ForeColor = _active ? palette.Text : palette.SecondaryText;
        Invalidate(true);
    }

    private void SetHovered(bool hovered)
    {
        _hovered = hovered;
        ApplyTheme(_palette);
    }
}

internal sealed class ModernButton : Button
{
    private Color _outlineColor = Color.Transparent;
    private bool _successHoverEnabled;
    private Color _successNormalText;
    private Color _successHoverText;

    public ModernButton()
    {
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        AutoSize = true;
        MinimumSize = new Size(120, 38);
        Padding = new Padding(14, 0, 14, 0);
        TextAlign = ContentAlignment.MiddleCenter;
        FlatAppearance.BorderSize = 0;
        Resize += (_, _) => ControlDrawing.ApplyRoundedRegion(this, 12);
    }

    public void SetOutlineColor(Color color)
    {
        _outlineColor = color;
        Invalidate();
    }

    public void ApplyTheme(ThemePalette palette, bool emphasis = false, bool destructive = false, bool destructiveHoverEnabled = false)
    {
        _successHoverEnabled = false;
        bool lightPalette = palette.MenuBackground.GetBrightness() > 0.65f;
        Color destructiveBack = lightPalette ? Color.FromArgb(255, 247, 247) : Color.FromArgb(74, 24, 31);
        Color destructiveHoverColor = lightPalette ? Color.FromArgb(255, 224, 224) : Color.FromArgb(96, 28, 36);
        Color destructivePressed = lightPalette ? Color.FromArgb(255, 207, 207) : Color.FromArgb(118, 32, 42);
        Color destructiveBorder = lightPalette ? Color.FromArgb(198, 48, 55) : Color.FromArgb(132, 58, 66);
        Color destructiveText = lightPalette ? Color.FromArgb(142, 28, 36) : palette.Text;
        bool useDestructiveBorder = destructive || destructiveHoverEnabled;

        BackColor = destructive
            ? destructiveBack
            : emphasis ? palette.Accent : palette.ButtonBackground;
        ForeColor = useDestructiveBorder ? destructiveText : emphasis && !destructive ? Color.White : palette.Text;
        FlatAppearance.BorderColor = useDestructiveBorder
            ? destructiveBorder
            : emphasis ? palette.Accent : palette.Border;
        _outlineColor = FlatAppearance.BorderColor;
        FlatAppearance.MouseOverBackColor = destructive || destructiveHoverEnabled
            ? destructiveHoverColor
            : emphasis ? palette.AccentHover : palette.ButtonHover;
        FlatAppearance.MouseDownBackColor = destructive || destructiveHoverEnabled
            ? destructivePressed
            : emphasis ? palette.AccentPressed : palette.ButtonPressed;
        Invalidate();
    }

    public void ApplySuccessOutlineTheme(ThemePalette palette)
    {
        _successHoverEnabled = true;
        BackColor = palette.ButtonBackground;
        _successNormalText = palette.Text;
        _successHoverText = Color.White;
        ForeColor = _successNormalText;
        FlatAppearance.BorderColor = Color.FromArgb(72, 163, 96);
        _outlineColor = FlatAppearance.BorderColor;
        FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 145, 86);
        FlatAppearance.MouseDownBackColor = Color.FromArgb(50, 124, 72);
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        if (_successHoverEnabled)
        {
            ForeColor = _successHoverText;
        }

        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (_successHoverEnabled)
        {
            ForeColor = _successNormalText;
        }

        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        base.OnPaint(pevent);
        if (_outlineColor == Color.Transparent || Width <= 4 || Height <= 4)
        {
            return;
        }

        pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle borderBounds = new(1, 1, Width - 3, Height - 3);
        using GraphicsPath borderPath = ControlDrawing.RoundedRect(borderBounds, 11);
        using Pen borderPen = new(_outlineColor, 1f);
        pevent.Graphics.DrawPath(borderPen, borderPath);
    }
}

internal sealed class SettingsSidebarItem : Control, ISurfaceBackgroundProvider, IChildSurfaceBackgroundRenderer
{
    private static readonly Color SidebarBackgroundDark = Color.FromArgb(17, 20, 26);
    private static readonly Color SidebarHoverDark = Color.FromArgb(29, 34, 43);
    private static readonly Color SidebarSelectedDark = Color.FromArgb(39, 47, 60);
    private static readonly Color SidebarBackgroundLight = Color.FromArgb(248, 250, 252);
    private static readonly Color SidebarHoverLight = Color.FromArgb(226, 233, 242);
    private static readonly Color SidebarSelectedLight = Color.FromArgb(214, 226, 240);

    private readonly FluentIconControl _iconControl;
    private readonly Label _titleLabel;
    private ThemePalette _palette;
    private bool _hovered;
    private bool _pressed;
    private bool _selected;

    public SettingsSidebarItem(ThemePalette palette, string title, TrayFluentIcon icon)
    {
        _palette = palette;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.UserPaint,
            true);

        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        TabStop = false;
        Margin = new Padding(0, 0, 0, 6);
        Padding = new Padding(12, 0, 10, 0);

        _iconControl = new FluentIconControl(palette, icon)
        {
            BackColor = Color.Transparent
        };
        _titleLabel = new Label
        {
            Text = title,
            AutoSize = false,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = ControlDrawing.UiFont("Segoe UI Semibold", 9.4f, FontStyle.Bold),
            AutoEllipsis = true
        };

        Controls.Add(_iconControl);
        Controls.Add(_titleLabel);

        foreach (Control control in Controls)
        {
            control.Click += (_, _) => OnClick(EventArgs.Empty);
            control.MouseEnter += (_, _) => SetState(true, _pressed);
            control.MouseLeave += (_, _) => SetState(false, false);
            control.MouseDown += (_, _) => SetState(true, true);
            control.MouseUp += (_, _) => SetState(true, false);
        }

        ApplyTheme(palette);
        Height = 44;
    }

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value)
            {
                return;
            }

            _selected = value;
            ApplyTheme(_palette);
        }
    }

    public string Title
    {
        get => _titleLabel.Text;
        set => _titleLabel.Text = value;
    }

    public Color SurfaceBackgroundColor
    {
        get
        {
            if (_selected)
            {
                return _useDarkPalette ? SidebarSelectedDark : SidebarSelectedLight;
            }

            if (_pressed)
            {
                return _palette.ButtonPressed;
            }

            if (_hovered)
            {
                return _useDarkPalette ? SidebarHoverDark : SidebarHoverLight;
            }

            return _useDarkPalette ? SidebarBackgroundDark : SidebarBackgroundLight;
        }
    }

    private bool _useDarkPalette => _palette.MenuBackground.GetBrightness() < 0.45f;

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        _iconControl.ApplyTheme(palette);
        _titleLabel.ForeColor = _selected ? palette.Text : palette.SecondaryText;
        Invalidate(true);
    }

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        Height = ControlDrawing.ScaleLogical(this, 44);
        Padding = new Padding(
            ControlDrawing.ScaleLogical(this, 12),
            0,
            ControlDrawing.ScaleLogical(this, 10),
            0);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_iconControl is null || _titleLabel is null)
        {
            return;
        }

        int iconSize = ControlDrawing.ScaleLogical(this, 18);
        _iconControl.Bounds = new Rectangle(Padding.Left + 6, (Height - iconSize) / 2, iconSize, iconSize);
        _titleLabel.Bounds = new Rectangle(_iconControl.Right + 9, 0, Math.Max(40, Width - _iconControl.Right - Padding.Right - 9), Height);
        ControlDrawing.ApplyRoundedRegion(this, ControlDrawing.ScaleLogical(this, 14));
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        SetState(true, _pressed);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetState(false, false);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        SetState(_hovered, true);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        SetState(_hovered, false);
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            OnClick(EventArgs.Empty);
        }

        base.OnKeyDown(e);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        Color backColor = Parent is ISurfaceBackgroundProvider provider
            ? provider.SurfaceBackgroundColor
            : ControlDrawing.EffectiveBackColor(this);
        using SolidBrush brush = new(backColor);
        pevent.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        Rectangle fillRect = new(1, 1, Math.Max(1, Width - 2), Math.Max(1, Height - 2));
        using GraphicsPath fillPath = ControlDrawing.RoundedRect(fillRect, ControlDrawing.ScaleLogical(this, 14));
        using SolidBrush fillBrush = new(SurfaceBackgroundColor);
        e.Graphics.FillPath(fillBrush, fillPath);

    }

    public void PaintChildSurfaceBackground(Graphics graphics, Rectangle childBounds)
    {
        using Region clip = graphics.Clip?.Clone() ?? new Region(childBounds);
        graphics.SetClip(childBounds);
        using SolidBrush backgroundBrush = new(SurfaceBackgroundColor);
        graphics.FillRectangle(backgroundBrush, childBounds);
        graphics.Clip = clip;
    }

    private void SetState(bool hovered, bool pressed)
    {
        _hovered = hovered;
        _pressed = pressed;
        Invalidate();
    }
}

internal sealed class ModernTabButton : Button
{
    private bool _selected;
    private ThemePalette _palette;

    public ModernTabButton(ThemePalette palette)
    {
        _palette = palette;
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        AutoSize = true;
        MinimumSize = new Size(118, 40);
        Padding = new Padding(16, 0, 16, 0);
        TextAlign = ContentAlignment.MiddleCenter;
        Margin = new Padding(0, 0, 10, 0);
        ApplyTheme(palette, false);
    }

    public bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            ApplyTheme(_palette, _selected);
        }
    }

    public void ApplyTheme(ThemePalette palette, bool selected)
    {
        _palette = palette;
        _selected = selected;
        BackColor = selected ? palette.ButtonHover : palette.ButtonBackground;
        ForeColor = palette.Text;
        FlatAppearance.BorderColor = selected ? palette.Accent : palette.Border;
        FlatAppearance.MouseOverBackColor = palette.ButtonHover;
        FlatAppearance.MouseDownBackColor = palette.ButtonPressed;
    }
}

internal sealed class ModernTabBar : Panel
{
    private readonly FlowLayoutPanel _flow;
    private readonly Dictionary<string, ModernTabButton> _buttons = new(StringComparer.Ordinal);
    private ThemePalette _palette;
    private string? _selectedKey;

    public ModernTabBar(ThemePalette palette)
    {
        _palette = palette;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(0);
        BackColor = Color.Transparent;

        _flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        Controls.Add(_flow);
    }

    public event Action<string>? SelectionChanged;

    public void SetTabs(IEnumerable<(string Key, string Text)> tabs)
    {
        _flow.Controls.Clear();
        _buttons.Clear();

        foreach ((string key, string text) in tabs)
        {
            var button = new ModernTabButton(_palette)
            {
                Text = text,
                Tag = key
            };
            button.Click += (_, _) => SelectTab((string)button.Tag!);
            _buttons[key] = button;
            _flow.Controls.Add(button);
        }
    }

    public void SelectTab(string key, bool notify = true)
    {
        _selectedKey = key;
        foreach ((string buttonKey, ModernTabButton button) in _buttons)
        {
            button.Selected = string.Equals(buttonKey, key, StringComparison.Ordinal);
        }

        if (notify)
        {
            SelectionChanged?.Invoke(key);
        }
    }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        foreach ((string key, ModernTabButton button) in _buttons)
        {
            button.ApplyTheme(palette, string.Equals(key, _selectedKey, StringComparison.Ordinal));
        }
    }
}

internal sealed class ModernDropdown : Control, ISurfaceBackgroundProvider
{
    private ThemePalette _palette;
    private readonly List<string> _items = new();
    private int _selectedIndex = -1;
    private bool _hovered;
    private bool _pressed;
    private ContextMenuStrip? _activeMenu;
    private int _menuMinimumWidth;

    public ModernDropdown(ThemePalette palette)
    {
        _palette = palette;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.UserPaint |
            ControlStyles.Selectable,
            true);
        Font = ControlDrawing.UiFont("Segoe UI", 9.5f, FontStyle.Regular);
        Height = 36;
        Width = 240;
        Cursor = Cursors.Hand;
        TabStop = true;
        BackColor = Color.Transparent;
        ApplyTheme(palette);
    }

    public event EventHandler? SelectedIndexChanged;

    public List<string> Items => _items;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int next = value < 0 || value >= _items.Count ? -1 : value;
            if (_selectedIndex == next)
            {
                return;
            }

            _selectedIndex = next;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string? SelectedItem => _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;

    public int MenuMinimumWidth
    {
        get => _menuMinimumWidth;
        set => _menuMinimumWidth = Math.Max(0, value);
    }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        Invalidate();
    }

    public Color SurfaceBackgroundColor => _pressed ? ControlContrast.FieldPressed(_palette) : _hovered ? ControlContrast.FieldHover(_palette) : ControlContrast.FieldBackground(_palette);

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _pressed = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        ShowMenu();
    }

    protected override bool IsInputKey(Keys keyData)
    {
        return keyData is Keys.Enter or Keys.Space or Keys.Down or Keys.Up || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Enter:
            case Keys.Space:
                ShowMenu();
                e.Handled = true;
                break;
            case Keys.Down:
                if (_items.Count > 0)
                {
                    SelectedIndex = Math.Min(_items.Count - 1, Math.Max(0, _selectedIndex + 1));
                }
                e.Handled = true;
                break;
            case Keys.Up:
                if (_items.Count > 0)
                {
                    SelectedIndex = Math.Max(0, _selectedIndex <= 0 ? 0 : _selectedIndex - 1);
                }
                e.Handled = true;
                break;
        }

        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle rect = new(0, 0, Width - 1, Height - 1);
        using GraphicsPath path = ControlDrawing.RoundedRect(rect, 10);
        using SolidBrush fill = new(SurfaceBackgroundColor);
        using Pen border = new(ControlContrast.FieldBorder(_palette));
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);

        string text = SelectedItem ?? string.Empty;
        Rectangle textBounds = Rectangle.FromLTRB(12, 0, Width - 34, Height);
        TextRenderer.DrawText(
            e.Graphics,
            text,
            Font,
            textBounds,
            _palette.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        Point center = new(Width - 17, Height / 2);
        using GraphicsPath chevron = new();
        chevron.AddLines(new Point[]
        {
            new Point(center.X - 4, center.Y - 2),
            new Point(center.X, center.Y + 2),
            new Point(center.X + 4, center.Y - 2)
        });
        using Pen chevronPen = new(_palette.SecondaryText, 1.6f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        e.Graphics.DrawPath(chevronPen, chevron);

        if (Focused)
        {
            Rectangle focusRect = new(0, 0, Width - 1, Height - 1);
            using GraphicsPath focusPath = ControlDrawing.RoundedRect(focusRect, 10);
            using Pen focusPen = new(Color.FromArgb(120, _palette.Accent), 1.5f);
            e.Graphics.DrawPath(focusPen, focusPath);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        Color backColor = Parent is ISurfaceBackgroundProvider provider
            ? provider.SurfaceBackgroundColor
            : ControlDrawing.EffectiveBackColor(this);
        using SolidBrush brush = new(backColor);
        pevent.Graphics.FillRectangle(brush, ClientRectangle);
    }

    private void ShowMenu()
    {
        if (_items.Count == 0)
        {
            return;
        }

        if (_activeMenu != null && !_activeMenu.IsDisposed && _activeMenu.Visible)
        {
            return;
        }

        if (_activeMenu != null)
        {
            _activeMenu.Dispose();
            _activeMenu = null;
        }

        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            ShowCheckMargin = false,
            AutoSize = false,
            BackColor = _palette.MenuBackground,
            ForeColor = _palette.Text,
            Font = Font,
            Renderer = new DarkMenuRenderer(_palette)
        };
        int desiredWidth = Math.Max(Math.Max(Width, MinimumSize.Width), _menuMinimumWidth);
        foreach (string itemText in _items)
        {
            int textWidth = TextRenderer.MeasureText(itemText, Font).Width;
            desiredWidth = Math.Max(desiredWidth, textWidth + 28);
        }

        int itemHeight = Math.Max(28, Font.Height + 12);
        menu.MinimumSize = new Size(desiredWidth, 0);
        menu.Size = new Size(desiredWidth, Math.Max(1, _items.Count * itemHeight + 4));

        for (int i = 0; i < _items.Count; i++)
        {
            string itemText = _items[i];
            int itemIndex = i;
            var item = new ToolStripMenuItem(itemText)
            {
                AutoSize = false,
                Size = new Size(desiredWidth - 2, itemHeight)
            };
            item.Click += (_, _) => SelectedIndex = itemIndex;
            menu.Items.Add(item);
        }

        _activeMenu = menu;
        menu.Closed += (_, _) =>
        {
            if (_activeMenu != menu)
            {
                return;
            }

            _activeMenu = null;
            if (IsHandleCreated && !IsDisposed)
            {
                try
                {
                    BeginInvoke((MethodInvoker)(() =>
                    {
                        try
                        {
                            if (!menu.IsDisposed)
                            {
                                menu.Dispose();
                            }
                        }
                        catch (Exception ex)
                        {
                            ErrorLog.WriteThrottled("ModernDropdown.DisposeMenu", ex);
                        }
                    }));
                }
                catch (Exception ex)
                {
                    ErrorLog.WriteThrottled("ModernDropdown.BeginDisposeMenu", ex);
                }
            }
            else if (!menu.IsDisposed)
            {
                menu.Dispose();
            }
        };
        menu.Show(this, new Point(0, Height));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_activeMenu != null)
            {
                _activeMenu.Dispose();
                _activeMenu = null;
            }
        }

        base.Dispose(disposing);
    }
}

internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    private readonly ThemePalette _palette;

    public DarkMenuRenderer(ThemePalette palette) : base(new DarkMenuColorTable(palette))
    {
        _palette = palette;
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        Rectangle rect = new(2, 1, e.Item.Width - 4, e.Item.Height - 2);
        Color backColor = e.Item.Selected ? ControlContrast.FieldHover(_palette) : ControlContrast.FieldBackground(_palette);
        using SolidBrush brush = new(backColor);
        e.Graphics.FillRectangle(brush, rect);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = _palette.Text;
        base.OnRenderItemText(e);
    }
}

internal sealed class DarkMenuColorTable : ProfessionalColorTable
{
    private readonly ThemePalette _palette;

    public DarkMenuColorTable(ThemePalette palette)
    {
        _palette = palette;
        UseSystemColors = false;
    }

    public override Color ToolStripDropDownBackground => _palette.MenuBackground;
    public override Color MenuBorder => _palette.Border;
    public override Color MenuItemBorder => Color.Transparent;
    public override Color MenuItemSelected => ControlContrast.FieldHover(_palette);
    public override Color MenuItemSelectedGradientBegin => ControlContrast.FieldHover(_palette);
    public override Color MenuItemSelectedGradientEnd => ControlContrast.FieldHover(_palette);
    public override Color MenuItemPressedGradientBegin => ControlContrast.FieldPressed(_palette);
    public override Color MenuItemPressedGradientMiddle => ControlContrast.FieldPressed(_palette);
    public override Color MenuItemPressedGradientEnd => ControlContrast.FieldPressed(_palette);
    public override Color ImageMarginGradientBegin => _palette.MenuBackground;
    public override Color ImageMarginGradientMiddle => _palette.MenuBackground;
    public override Color ImageMarginGradientEnd => _palette.MenuBackground;
}

internal sealed class ModernSlider : Control
{
    private ThemePalette _palette;
    private int _minimum;
    private int _maximum = 100;
    private int _value;
    private int _snapStep = 1;
    private bool _dragging;
    private bool _hovered;

    public ModernSlider(ThemePalette palette)
    {
        _palette = palette;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.UserPaint |
            ControlStyles.Selectable,
            true);
        BackColor = Color.Transparent;
        TabStop = true;
        Cursor = Cursors.Hand;
        Height = 28;
        Width = 280;
    }

    public event EventHandler? ValueChanged;

    public int Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;
            if (_maximum < _minimum)
            {
                _maximum = _minimum;
            }

            Value = Math.Clamp(_value, _minimum, _maximum);
            Invalidate();
        }
    }

    public int Maximum
    {
        get => _maximum;
        set
        {
            _maximum = Math.Max(value, _minimum);
            Value = Math.Clamp(_value, _minimum, _maximum);
            Invalidate();
        }
    }

    public int SnapStep
    {
        get => _snapStep;
        set => _snapStep = Math.Max(1, value);
    }

    public int Value
    {
        get => _value;
        set
        {
            int next = Snap(Math.Clamp(value, _minimum, _maximum));
            if (_value == next)
            {
                return;
            }

            _value = next;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        _dragging = true;
        UpdateValueFromX(e.X);
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging)
        {
            UpdateValueFromX(e.X);
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _dragging = false;
        base.OnMouseUp(e);
    }

    protected override bool IsInputKey(Keys keyData)
    {
        return keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End
            || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Left:
            case Keys.Down:
                Value -= _snapStep;
                e.Handled = true;
                break;
            case Keys.Right:
            case Keys.Up:
                Value += _snapStep;
                e.Handled = true;
                break;
            case Keys.Home:
                Value = _minimum;
                e.Handled = true;
                break;
            case Keys.End:
                Value = _maximum;
                e.Handled = true;
                break;
        }

        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        Rectangle trackRect = new(0, (Height / 2) - 3, Width - 1, 6);
        using GraphicsPath trackPath = ControlDrawing.RoundedRect(trackRect, 3);
        using SolidBrush trackBrush = new(ControlContrast.SubtleTrack(_palette));
        using Pen trackBorder = new(ControlContrast.FieldBorder(_palette));
        e.Graphics.FillPath(trackBrush, trackPath);
        e.Graphics.DrawPath(trackBorder, trackPath);

        float ratio = _maximum == _minimum ? 1f : (float)(_value - _minimum) / (_maximum - _minimum);
        int fillWidth = Math.Max(6, (int)Math.Round(trackRect.Width * ratio));
        Rectangle fillRect = new(trackRect.X, trackRect.Y, Math.Min(trackRect.Width, fillWidth), trackRect.Height);
        using GraphicsPath fillPath = ControlDrawing.RoundedRect(fillRect, 3);
        using SolidBrush fillBrush = new(_hovered ? _palette.AccentHover : _palette.Accent);
        e.Graphics.FillPath(fillBrush, fillPath);

        int knobSize = 18;
        int knobX = Math.Clamp(trackRect.X + (int)Math.Round((trackRect.Width - knobSize) * ratio), trackRect.X, trackRect.Right - knobSize);
        Rectangle knobRect = new(knobX, (Height - knobSize) / 2, knobSize, knobSize);
        using SolidBrush knobBrush = new(Color.FromArgb(244, 246, 249));
        using Pen knobBorder = new(Color.FromArgb(56, 60, 66));
        e.Graphics.FillEllipse(knobBrush, knobRect);
        e.Graphics.DrawEllipse(knobBorder, knobRect);

        if (Focused)
        {
            Rectangle focusRect = new(0, 0, Width - 1, Height - 1);
            using GraphicsPath focusPath = ControlDrawing.RoundedRect(focusRect, 8);
            using Pen focusPen = new(Color.FromArgb(130, _palette.Accent), 1.5f);
            e.Graphics.DrawPath(focusPen, focusPath);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        Color backColor = Parent is ISurfaceBackgroundProvider provider
            ? provider.SurfaceBackgroundColor
            : ControlDrawing.EffectiveBackColor(this);
        using SolidBrush brush = new(backColor);
        pevent.Graphics.FillRectangle(brush, ClientRectangle);
    }

    private void UpdateValueFromX(int x)
    {
        int usableWidth = Math.Max(1, Width - 18);
        float ratio = Math.Clamp((float)(x - 9) / usableWidth, 0f, 1f);
        int rawValue = _minimum + (int)Math.Round((_maximum - _minimum) * ratio);
        Value = rawValue;
    }

    private int Snap(int value)
    {
        int normalized = value - _minimum;
        int snapped = (int)Math.Round(normalized / (double)_snapStep) * _snapStep;
        return Math.Clamp(_minimum + snapped, _minimum, _maximum);
    }
}

internal sealed class SettingsRow : ModernSurfacePanel
{
    private readonly Label _titleLabel;
    private readonly Label _descriptionLabel;
    private readonly Label _statusLabel;
    private readonly TableLayoutPanel _grid;
    private readonly TableLayoutPanel _left;
    private readonly TableLayoutPanel _right;
    private readonly Control _accessoryControl;
    private readonly int _rightColumnWidth;
    private readonly string? _valueText;

    public SettingsRow(ThemePalette palette, string title, string description, Control control, int rightColumnWidth = 220, string? valueText = null, bool compactDescription = false)
    {
        bool hasDescription = !string.IsNullOrWhiteSpace(description);
        _accessoryControl = control;
        _rightColumnWidth = Math.Max(96, rightColumnWidth);
        _valueText = valueText;
        CornerRadius = 16;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Margin = new Padding(0, 0, 0, 8);
        Padding = hasDescription ? new Padding(14, 12, 14, 12) : new Padding(14, 11, 14, 11);
        MinimumSize = new Size(0, hasDescription ? 66 : 50);
        BackColor = palette.ControlBackground;

        _grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, _rightColumnWidth));
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0, 2, 12, 2)
        };
        _left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _titleLabel = new Label
        {
            Text = title,
            AutoSize = true,
            Font = ControlDrawing.UiFont("Segoe UI Semibold", 10f, FontStyle.Bold),
            Margin = new Padding(0),
            BackColor = Color.Transparent
        };
        _descriptionLabel = new Label
        {
            Text = description,
            AutoSize = true,
            Font = ControlDrawing.UiFont("Segoe UI", 8.8f, FontStyle.Regular),
            Margin = new Padding(0, 4, 0, 0),
            BackColor = Color.Transparent
        };
        _statusLabel = new Label
        {
            AutoSize = true,
            Font = ControlDrawing.UiFont("Segoe UI Semibold", 8.6f, FontStyle.Bold),
            Margin = new Padding(0, 6, 0, 0),
            BackColor = Color.Transparent,
            Visible = false
        };

        _right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0, 2, 0, 0)
        };
        _right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        control.Margin = new Padding(10, 0, 0, 0);
        control.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _right.Controls.Add(control, 0, 0);

        _left.Controls.Add(_titleLabel, 0, 0);
        if (hasDescription)
        {
            _left.Controls.Add(_descriptionLabel, 0, 1);
        }
        _left.Controls.Add(_statusLabel, 0, 2);

        _grid.Controls.Add(_left, 0, 0);
        _grid.Controls.Add(_right, 1, 0);
        Controls.Add(_grid);

        Resize += (_, _) => UpdateLayoutMetrics();
        UpdateLayoutMetrics();
        ApplyTheme(palette);
    }

    public void ApplyTheme(ThemePalette palette)
    {
        BackColor = palette.ControlBackground;
        BorderAlpha = 20;
        _titleLabel.ForeColor = palette.Text;
        _descriptionLabel.ForeColor = palette.SecondaryText;
        Invalidate(true);
    }

    public void SetStatus(string? text, Color color)
    {
        _statusLabel.Text = text ?? string.Empty;
        _statusLabel.ForeColor = color;
        _statusLabel.Visible = !string.IsNullOrWhiteSpace(text);
        UpdateLayoutMetrics();
    }

    private void UpdateLayoutMetrics()
    {
        int availableWidth = Math.Max(320, Width - Padding.Horizontal);
        int leftWidth = Math.Max(210, availableWidth - _rightColumnWidth - 14);
        _grid.ColumnStyles[1].Width = _rightColumnWidth;
        _titleLabel.MaximumSize = new Size(leftWidth, 0);
        _descriptionLabel.MaximumSize = new Size(leftWidth, 0);
        _statusLabel.MaximumSize = new Size(leftWidth, 0);

        if (_valueText != null && _accessoryControl is Label valueLabel)
        {
            valueLabel.MaximumSize = new Size(_rightColumnWidth, 0);
        }
    }
}

internal sealed class SettingsSection : Panel
{
    private readonly Label _titleLabel;
    private readonly Label _descriptionLabel;
    private readonly TableLayoutPanel _rows;
    private int _nextRowIndex;

    public SettingsSection(ThemePalette palette, string title, string description)
    {
        bool hasTitle = !string.IsNullOrWhiteSpace(title);
        bool hasDescription = !string.IsNullOrWhiteSpace(description);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Dock = DockStyle.Top;
        Margin = new Padding(0, 0, 0, 16);
        Padding = new Padding(0);
        BackColor = Color.Transparent;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        _titleLabel = new Label
        {
            Text = title,
            AutoSize = true,
            Font = ControlDrawing.UiFont("Segoe UI Semibold", 9.5f, FontStyle.Bold),
            Margin = new Padding(0),
            BackColor = Color.Transparent,
            ForeColor = palette.SecondaryText
        };
        _descriptionLabel = new Label
        {
            Text = description,
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            Font = ControlDrawing.UiFont("Segoe UI", 8.6f, FontStyle.Regular),
            Margin = new Padding(0, 4, 0, 12),
            BackColor = Color.Transparent,
            ForeColor = palette.SecondaryText
        };

        _rows = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 0,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };
        _rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int rowIndex = 0;
        if (hasTitle)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(_titleLabel, 0, rowIndex++);
        }

        if (hasDescription)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(_descriptionLabel, 0, rowIndex++);
        }

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_rows, 0, rowIndex);
        Controls.Add(layout);
        Resize += (_, _) => UpdateRowWidths();
    }

    public void AddRow(Control row)
    {
        row.Dock = DockStyle.Top;
        row.Margin = new Padding(0, 0, 0, 8);
        row.MinimumSize = new Size(0, row.MinimumSize.Height);
        row.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        _rows.RowCount++;
        _rows.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _rows.Controls.Add(row, 0, _nextRowIndex++);
        UpdateRowWidths();
    }

    public void ClearRows()
    {
        foreach (Control control in _rows.Controls)
        {
            control.Dispose();
        }

        _rows.Controls.Clear();
        _rows.RowStyles.Clear();
        _rows.RowCount = 0;
        _nextRowIndex = 0;
    }

    private void UpdateRowWidths()
    {
        int targetWidth = Math.Max(360, ClientSize.Width);
        foreach (Control row in _rows.Controls)
        {
            row.Width = targetWidth;
        }
    }
}

internal class SettingsPageView : UserControl
{
    private readonly Label _titleLabel;
    private readonly Label _descriptionLabel;
    private readonly TableLayoutPanel _sectionHost;
    private int _nextSectionIndex;

    public SettingsPageView(ThemePalette palette, string title, string description)
    {
        bool hasTitle = !string.IsNullOrWhiteSpace(title);
        bool hasDescription = !string.IsNullOrWhiteSpace(description);
        Dock = DockStyle.Top;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        AutoScroll = false;
        BackColor = palette.MenuBackground;
        Padding = new Padding(0);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };

        _titleLabel = new Label
        {
            Text = title,
            AutoSize = true,
            Font = ControlDrawing.UiFont("Segoe UI Semibold", 18f, FontStyle.Bold),
            Margin = new Padding(0),
            BackColor = Color.Transparent,
            ForeColor = palette.Text
        };
        _descriptionLabel = new Label
        {
            Text = description,
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            Font = ControlDrawing.UiFont("Segoe UI", 9f, FontStyle.Regular),
            Margin = new Padding(0, 4, 0, 12),
            BackColor = Color.Transparent,
            ForeColor = palette.SecondaryText
        };
        _sectionHost = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 0,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };
        _sectionHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int rowIndex = 0;
        if (hasTitle)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(_titleLabel, 0, rowIndex++);
        }

        if (hasDescription)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(_descriptionLabel, 0, rowIndex++);
        }

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_sectionHost, 0, rowIndex);
        Controls.Add(layout);
        Resize += (_, _) => UpdateSectionWidths();
    }

    public void AddSection(SettingsSection section)
    {
        section.Dock = DockStyle.Top;
        section.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        _sectionHost.RowCount++;
        _sectionHost.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _sectionHost.Controls.Add(section, 0, _nextSectionIndex++);
        UpdateSectionWidths();
    }

    private void UpdateSectionWidths()
    {
        int targetWidth = Math.Max(400, ClientSize.Width);
        foreach (Control section in _sectionHost.Controls)
        {
            section.Width = targetWidth;
        }
    }
}

internal sealed class GeneralSettingsPageView : SettingsPageView
{
    public GeneralSettingsPageView(ThemePalette palette, string title, string description) : base(palette, title, description) { }
}

internal sealed class DisplaySettingsPageView : SettingsPageView
{
    public DisplaySettingsPageView(ThemePalette palette, string title, string description) : base(palette, title, description) { }
}

internal sealed class AppearanceSettingsPageView : SettingsPageView
{
    public AppearanceSettingsPageView(ThemePalette palette, string title, string description) : base(palette, title, description) { }
}

internal sealed class CursorSettingsPageView : SettingsPageView
{
    public CursorSettingsPageView(ThemePalette palette, string title, string description) : base(palette, title, description) { }
}

internal sealed class ZoomSettingsPageView : SettingsPageView
{
    public ZoomSettingsPageView(ThemePalette palette, string title, string description) : base(palette, title, description) { }
}

internal sealed class ShortcutsSettingsPageView : SettingsPageView
{
    public ShortcutsSettingsPageView(ThemePalette palette, string title, string description) : base(palette, title, description) { }
}

internal sealed class AboutSettingsPageView : SettingsPageView
{
    public AboutSettingsPageView(ThemePalette palette, string title, string description) : base(palette, title, description) { }
}

internal sealed class SettingsContentHost : Panel
{
    private readonly ThemePalette _palette;
    private Control? _activePage;
    private int _scrollOffset;
    private int _contentHeight;
    private bool _showScrollBar;
    private Rectangle _thumbRect;
    private bool _draggingThumb;
    private int _dragStartY;
    private int _dragStartOffset;
    private const int ScrollBarWidth = 10;
    private const int ScrollBarInset = 2;

    public SettingsContentHost(ThemePalette palette)
    {
        _palette = palette;
        SetStyle(ControlStyles.Selectable, true);
        DoubleBuffered = true;
        BackColor = palette.MenuBackground;
        AutoScroll = false;
        TabStop = true;
    }

    public void SetActivePage(Control page)
    {
        if (_activePage != page)
        {
            _scrollOffset = 0;
        }

        _activePage = page;
        AttachScrollInput(page);
        LayoutActivePage();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutActivePage();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        Focus();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!_showScrollBar)
        {
            return;
        }

        ScrollBy(-Math.Sign(e.Delta) * 72);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!_showScrollBar || e.Button != MouseButtons.Left)
        {
            return;
        }

        if (_thumbRect.Contains(e.Location))
        {
            _draggingThumb = true;
            _dragStartY = e.Y;
            _dragStartOffset = _scrollOffset;
            Capture = true;
            return;
        }

        ScrollBy(e.Y < _thumbRect.Top ? -ClientSize.Height : ClientSize.Height);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_draggingThumb || _activePage == null)
        {
            return;
        }

        int maxOffset = Math.Max(0, _contentHeight - ClientSize.Height);
        int trackHeight = Math.Max(1, ClientSize.Height - (ScrollBarInset * 2));
        int travel = Math.Max(1, trackHeight - _thumbRect.Height);
        int deltaOffset = (int)Math.Round((e.Y - _dragStartY) * (maxOffset / (double)travel));
        SetScrollOffset(_dragStartOffset + deltaOffset);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left)
        {
            _draggingThumb = false;
            Capture = false;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        base.OnPaintBackground(e);
        if (!_showScrollBar)
        {
            return;
        }

        Rectangle track = new(ClientSize.Width - ScrollBarWidth, ScrollBarInset, ScrollBarWidth - ScrollBarInset, ClientSize.Height - (ScrollBarInset * 2));
        using GraphicsPath trackPath = ControlDrawing.RoundedRect(track, 4);
        using SolidBrush trackBrush = new(Color.FromArgb(70, _palette.ButtonBackground));
        e.Graphics.FillPath(trackBrush, trackPath);

        using GraphicsPath thumbPath = ControlDrawing.RoundedRect(_thumbRect, 4);
        using SolidBrush thumbBrush = new(Color.FromArgb(180, _palette.Border));
        e.Graphics.FillPath(thumbBrush, thumbPath);
    }

    private void ScrollBy(int delta) => SetScrollOffset(_scrollOffset + delta);

    private void AttachScrollInput(Control control)
    {
        control.MouseWheel -= OnChildMouseWheel;
        control.MouseWheel += OnChildMouseWheel;
        control.MouseEnter -= OnChildMouseEnter;
        control.MouseEnter += OnChildMouseEnter;

        foreach (Control child in control.Controls)
        {
            AttachScrollInput(child);
        }
    }

    private void OnChildMouseEnter(object? sender, EventArgs e) => Focus();

    private void OnChildMouseWheel(object? sender, MouseEventArgs e)
    {
        if (_showScrollBar)
        {
            ScrollBy(-Math.Sign(e.Delta) * 72);
        }
    }

    private void SetScrollOffset(int offset)
    {
        if (_activePage == null)
        {
            return;
        }

        int maxOffset = Math.Max(0, _contentHeight - ClientSize.Height);
        int nextOffset = Math.Clamp(offset, 0, maxOffset);
        if (nextOffset == _scrollOffset)
        {
            return;
        }

        _scrollOffset = nextOffset;
        PositionActivePage();
    }

    private void LayoutActivePage()
    {
        if (_activePage == null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        int pageWidth = Math.Max(400, ClientSize.Width - (_showScrollBar ? ScrollBarWidth + 8 : 0));
        _activePage.MinimumSize = new Size(pageWidth, 0);
        _activePage.Width = pageWidth;
        _activePage.PerformLayout();

        int pageHeight = GetNaturalContentHeight(_activePage, pageWidth);
        bool needsScrollBar = pageHeight > ClientSize.Height;
        if (needsScrollBar != _showScrollBar)
        {
            _showScrollBar = needsScrollBar;
            pageWidth = Math.Max(400, ClientSize.Width - (_showScrollBar ? ScrollBarWidth + 8 : 0));
            _activePage.MinimumSize = new Size(pageWidth, 0);
            _activePage.Width = pageWidth;
            _activePage.PerformLayout();
            pageHeight = GetNaturalContentHeight(_activePage, pageWidth);
        }

        _contentHeight = pageHeight;
        _activePage.Height = _contentHeight;
        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, _contentHeight - ClientSize.Height));
        PositionActivePage();
    }

    private void PositionActivePage()
    {
        if (_activePage == null)
        {
            return;
        }

        _activePage.Location = new Point(0, -_scrollOffset);
        UpdateThumb(_contentHeight);
        Invalidate();
    }

    private void UpdateThumb(int pageHeight)
    {
        if (!_showScrollBar)
        {
            _thumbRect = Rectangle.Empty;
            return;
        }

        int trackHeight = Math.Max(1, ClientSize.Height - (ScrollBarInset * 2));
        int thumbHeight = Math.Max(32, (int)Math.Round(trackHeight * (ClientSize.Height / (double)Math.Max(ClientSize.Height, pageHeight))));
        int maxOffset = Math.Max(1, pageHeight - ClientSize.Height);
        int travel = Math.Max(0, trackHeight - thumbHeight);
        int thumbY = ScrollBarInset + (int)Math.Round(travel * (_scrollOffset / (double)maxOffset));
        _thumbRect = new Rectangle(ClientSize.Width - ScrollBarWidth, thumbY, ScrollBarWidth - ScrollBarInset, thumbHeight);
    }

    private static int GetNaturalContentHeight(Control control, int width)
    {
        int preferredHeight = control.GetPreferredSize(new Size(width, 0)).Height;
        int childBottom = 0;
        foreach (Control child in control.Controls)
        {
            childBottom = Math.Max(childBottom, child.Bottom + child.Margin.Bottom);
        }

        return Math.Max(preferredHeight, childBottom);
    }
}

internal sealed class SettingsPageDefinition
{
    public SettingsPageDefinition(Type pageType, string title, TrayFluentIcon icon, Func<UserControl> createPage)
    {
        PageType = pageType;
        Title = title;
        Icon = icon;
        CreatePage = createPage;
    }

    public Type PageType { get; }
    public string Title { get; }
    public TrayFluentIcon Icon { get; }
    public Func<UserControl> CreatePage { get; }
}

internal sealed class SettingsForm : Form
{
    private readonly Dictionary<Type, UserControl> _pageCache = new();
    private readonly Dictionary<Type, SettingsSidebarItem> _navItems = new();
    private readonly Dictionary<Type, Func<UserControl>> _pageFactories = new();
    private readonly SettingsContentHost _contentHost;
    private readonly Size _minimumClientSize;
    private Type? _currentPageType;

    public SettingsForm(
        ThemePalette palette,
        bool useDarkTheme,
        string title,
        Size clientSize,
        string appName,
        string doneText,
        ModernButton resetButton,
        IReadOnlyList<SettingsPageDefinition> pages)
    {
        Text = title;
        StartPosition = FormStartPosition.Manual;
        ClientSize = clientSize;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = palette.MenuBackground;
        ForeColor = palette.Text;
        KeyPreview = true;
        _minimumClientSize = clientSize;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(20),
            Margin = new Padding(0),
            BackColor = palette.MenuBackground
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var sidebarSurface = new ModernSurfacePanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 18,
            BorderAlpha = 14,
            Margin = new Padding(0, 0, 16, 0),
            Padding = new Padding(12, 14, 12, 14),
            BackColor = useDarkTheme ? Color.FromArgb(17, 20, 26) : palette.ControlBackground
        };

        var sidebarLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var sidebarHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 16),
            Padding = new Padding(0),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent
        };
        sidebarHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sidebarHeader.Controls.Add(new Label
        {
            Text = appName,
            AutoSize = true,
            Font = ControlDrawing.UiFont("Segoe UI Semibold", 15f, FontStyle.Bold),
            Margin = new Padding(0),
            ForeColor = palette.Text,
            BackColor = Color.Transparent
        }, 0, 0);

        var navHost = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };

        foreach (SettingsPageDefinition page in pages)
        {
            _pageFactories[page.PageType] = page.CreatePage;
            var item = new SettingsSidebarItem(palette, page.Title, page.Icon);
            Type pageType = page.PageType;
            item.Click += (_, _) => ShowPage(pageType);
            _navItems[pageType] = item;
            navHost.Controls.Add(item);
        }

        void UpdateSidebarItemWidths()
        {
            int itemWidth = Math.Max(ControlDrawing.ScaleLogical(this, 246), navHost.ClientSize.Width - 8);
            foreach (SettingsSidebarItem item in _navItems.Values)
            {
                item.Width = itemWidth;
            }
        }

        navHost.Resize += (_, _) => UpdateSidebarItemWidths();
        sidebarLayout.Controls.Add(sidebarHeader, 0, 0);
        sidebarLayout.Controls.Add(navHost, 0, 1);
        sidebarSurface.Controls.Add(sidebarLayout);

        _contentHost = new SettingsContentHost(palette)
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = palette.MenuBackground
        };

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 8, 0, 0),
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };
        var closeButton = new ModernButton
        {
            Text = doneText,
            DialogResult = DialogResult.OK
        };
        closeButton.ApplySuccessOutlineTheme(palette);
        closeButton.Click += (_, _) => Close();
        footer.Controls.Add(closeButton);
        footer.Controls.Add(resetButton);

        var rightLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = palette.MenuBackground
        };
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rightLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rightLayout.Controls.Add(_contentHost, 0, 0);
        rightLayout.Controls.Add(footer, 0, 1);

        root.Controls.Add(sidebarSurface, 0, 0);
        root.Controls.Add(rightLayout, 1, 0);
        Controls.Add(root);
        AcceptButton = closeButton;
        CancelButton = closeButton;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        };
        Shown += (_, _) => UpdateSidebarItemWidths();
    }

    public event EventHandler<Type>? PageShown;

    public void ShowPage(Type pageType)
    {
        if (_currentPageType == pageType &&
            _pageCache.TryGetValue(pageType, out UserControl? currentPage) &&
            currentPage.Visible)
        {
            return;
        }

        if (!_pageCache.TryGetValue(pageType, out UserControl? page))
        {
            if (!_pageFactories.TryGetValue(pageType, out Func<UserControl>? factory))
            {
                return;
            }

            page = factory();
            page.Dock = DockStyle.Top;
            page.MinimumSize = new Size(Math.Max(400, _contentHost.ClientSize.Width), 0);
            page.Visible = false;
            _pageCache[pageType] = page;
            _contentHost.Controls.Add(page);
        }

        Type? previousPageType = _currentPageType;
        _contentHost.SuspendLayout();
        try
        {
            if (previousPageType != null &&
                _pageCache.TryGetValue(previousPageType, out UserControl? previousPage) &&
                previousPage != page)
            {
                previousPage.Visible = false;
            }

            page.Visible = true;
            page.BringToFront();
            _contentHost.SetActivePage(page);
        }
        finally
        {
            _contentHost.ResumeLayout(performLayout: true);
        }

        if (previousPageType != null &&
            previousPageType != pageType &&
            _navItems.TryGetValue(previousPageType, out SettingsSidebarItem? previousItem))
        {
            previousItem.Selected = false;
        }

        if (_navItems.TryGetValue(pageType, out SettingsSidebarItem? selectedItem))
        {
            selectedItem.Selected = true;
        }

        _currentPageType = pageType;
        FitToCurrentPage();
        PageShown?.Invoke(this, pageType);
    }

    public void FitToCurrentPage()
    {
        if (_currentPageType == null ||
            !_pageCache.TryGetValue(_currentPageType, out UserControl? page) ||
            _contentHost.ClientSize.Width <= 0)
        {
            return;
        }

        int pageWidth = Math.Max(400, _contentHost.ClientSize.Width);
        page.MinimumSize = new Size(pageWidth, 0);
        page.Width = pageWidth;
        page.PerformLayout();

        int preferredPageHeight = page.GetPreferredSize(new Size(pageWidth, 0)).Height;
        int nonContentHeight = Math.Max(0, ClientSize.Height - _contentHost.Height);
        int desiredClientHeight = Math.Max(_minimumClientSize.Height, preferredPageHeight + nonContentHeight);
        Rectangle area = Screen.FromControl(this).WorkingArea;
        int maxClientHeight = Math.Max(
            _minimumClientSize.Height,
            (int)Math.Round((area.Height - ControlDrawing.ScaleLogical(this, 16)) * 0.8));
        int nextHeight = Math.Min(desiredClientHeight, maxClientHeight);

        if (nextHeight != ClientSize.Height)
        {
            ClientSize = new Size(Math.Max(ClientSize.Width, _minimumClientSize.Width), nextHeight);
        }
    }
}

internal sealed class TrayPopupWindow : Form
{
    private readonly ThemePalette _palette;
    private readonly ModernSurfacePanel _surface;
    private readonly Panel _scrollHost;
    private const int DefaultLogicalContentWidth = 300;

    public TrayPopupWindow(ThemePalette palette)
    {
        _palette = palette;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = palette.MenuBackground;
        Padding = new Padding(1);

        _surface = new ModernSurfacePanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 16,
            BackColor = palette.MenuBackground,
            BorderAlpha = 32,
            Padding = new Padding(10)
        };

        _scrollHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = palette.MenuBackground,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        ContentHost = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };

        _scrollHost.Controls.Add(ContentHost);
        _surface.Controls.Add(_scrollHost);
        Controls.Add(_surface);

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        };

        Deactivate += (_, _) =>
        {
            if (IgnoreDeactivateClose)
            {
                return;
            }

            if (!IsDisposed && Visible)
            {
                try
                {
                    BeginInvoke((MethodInvoker)(() =>
                    {
                        try
                        {
                            if (!IsDisposed && Visible && !ContainsFocus && !IgnoreDeactivateClose)
                            {
                                Close();
                            }
                        }
                        catch (Exception ex)
                        {
                            ErrorLog.WriteThrottled("TrayPopup.DeactivateClose", ex);
                        }
                    }));
                }
                catch (Exception ex)
                {
                    ErrorLog.WriteThrottled("TrayPopup.BeginDeactivateClose", ex);
                }
            }
        };
    }

    public FlowLayoutPanel ContentHost { get; }
    public bool IgnoreDeactivateClose { get; set; }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ClassStyle |= 0x00020000;
            return cp;
        }
    }

    public void ShowAnchored(Point anchor)
    {
        Show();
        LayoutAnchored(anchor);
        Activate();
    }

    public void RefreshAnchoredLayout(Point anchor)
    {
        if (IsDisposed)
        {
            return;
        }

        LayoutAnchored(anchor);
    }

    private void LayoutAnchored(Point anchor)
    {
        Rectangle area = Screen.FromPoint(anchor).WorkingArea;
        int maxClientHeight = Math.Max(ControlDrawing.ScaleLogical(this, 220), area.Height - ControlDrawing.ScaleLogical(this, 24));
        int maxClientWidth = Math.Max(
            ControlDrawing.ScaleLogical(this, 260),
            area.Width - ControlDrawing.ScaleLogical(this, 24) - _surface.Padding.Horizontal - Padding.Horizontal);
        int popupContentWidth = Math.Min(GetRequestedContentWidth(), maxClientWidth);

        ContentHost.MinimumSize = new Size(popupContentWidth, 0);
        ContentHost.MaximumSize = new Size(popupContentWidth, 0);
        ContentHost.Width = popupContentWidth;
        ContentHost.PerformLayout();

        Size desiredContent = MeasureContentHost(popupContentWidth);
        int naturalClientHeight = Math.Max(
            ControlDrawing.ScaleLogical(this, 90),
            desiredContent.Height + _surface.Padding.Vertical + Padding.Vertical);
        bool needsVerticalScroll = naturalClientHeight > maxClientHeight;
        int availableContentWidth = popupContentWidth;

        ContentHost.MinimumSize = new Size(availableContentWidth, 0);
        ContentHost.MaximumSize = new Size(availableContentWidth, 0);
        ContentHost.Width = availableContentWidth;
        ContentHost.PerformLayout();
        desiredContent = MeasureContentHost(availableContentWidth);

        int clientWidth = popupContentWidth + _surface.Padding.Horizontal + Padding.Horizontal +
            (needsVerticalScroll ? SystemInformation.VerticalScrollBarWidth : 0);
        int clientHeight = Math.Min(
            Math.Max(ControlDrawing.ScaleLogical(this, 90), desiredContent.Height + _surface.Padding.Vertical + Padding.Vertical),
            maxClientHeight);

        int viewportHeight = Math.Max(1, clientHeight - _surface.Padding.Vertical - Padding.Vertical);
        bool requiresVerticalScroll = desiredContent.Height > viewportHeight;
        _scrollHost.AutoScroll = requiresVerticalScroll;
        _scrollHost.AutoScrollMinSize = requiresVerticalScroll ? new Size(0, desiredContent.Height) : Size.Empty;
        _scrollHost.VerticalScroll.Visible = requiresVerticalScroll;
        _scrollHost.HorizontalScroll.Enabled = false;
        _scrollHost.HorizontalScroll.Visible = false;
        _scrollHost.PerformLayout();
        _surface.PerformLayout();
        PerformLayout();
        ClientSize = new Size(clientWidth, clientHeight);

        int gutter = ControlDrawing.ScaleLogical(this, 8);
        int x = anchor.X - Width + gutter + 4;
        int y = anchor.Y - Height;

        if (x < area.Left + gutter)
        {
            x = area.Left + gutter;
        }

        if (x + Width > area.Right - gutter)
        {
            x = area.Right - Width - gutter;
        }

        if (y < area.Top + gutter)
        {
            y = Math.Min(area.Bottom - Height - gutter, anchor.Y + gutter + 4);
        }

        if (y + Height > area.Bottom)
        {
            y = area.Bottom - Height;
        }

        Location = new Point(x, y);
    }

    private int GetRequestedContentWidth()
    {
        int requestedWidth = Math.Max(
            ControlDrawing.ScaleLogical(this, DefaultLogicalContentWidth),
            Math.Max(ContentHost.MinimumSize.Width, ContentHost.Width));

        foreach (Control child in ContentHost.Controls)
        {
            requestedWidth = Math.Max(requestedWidth, child.Width + child.Margin.Horizontal);
            requestedWidth = Math.Max(requestedWidth, child.MinimumSize.Width + child.Margin.Horizontal);
        }

        return requestedWidth;
    }

    private Size MeasureContentHost(int width)
    {
        int measuredHeight = 0;
        int measuredWidth = width;

        foreach (Control child in ContentHost.Controls)
        {
            if (!child.Visible)
            {
                continue;
            }

            measuredHeight = Math.Max(measuredHeight, child.Bottom + child.Margin.Bottom);
            measuredWidth = Math.Max(measuredWidth, child.Right + child.Margin.Right);
        }

        if (measuredHeight <= 0)
        {
            Size preferred = ContentHost.GetPreferredSize(new Size(width, 0));
            measuredHeight = preferred.Height;
            measuredWidth = Math.Max(measuredWidth, preferred.Width);
        }

        return new Size(measuredWidth, measuredHeight);
    }
}
