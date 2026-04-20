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
            form.HandleCreated += (_, _) =>
            {
                if (form.Handle == IntPtr.Zero)
                {
                    return;
                }

                int useDark = enabled ? 1 : 0;
                _ = DwmSetWindowAttribute(form.Handle, 20, ref useDark, sizeof(int));
                _ = DwmSetWindowAttribute(form.Handle, 19, ref useDark, sizeof(int));
            };
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
        using Pen borderPen = new(Color.FromArgb(_borderAlpha, 255, 255, 255));
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
        IsOn = !IsOn;
        base.OnClick(e);
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
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

        Color trackColor = _isOn
            ? (_hovered ? _palette.AccentHover : _palette.Accent)
            : (_hovered ? _palette.ButtonHover : _palette.ButtonBackground);

        if (_pressed)
        {
            trackColor = _isOn ? _palette.AccentPressed : _palette.ButtonPressed;
        }

        using SolidBrush trackBrush = new(trackColor);
        using Pen borderPen = new(_palette.Border);
        e.Graphics.FillPath(trackBrush, trackPath);
        e.Graphics.DrawPath(borderPen, trackPath);

        int knobSize = trackRect.Height - 4;
        int knobX = _isOn ? trackRect.Right - knobSize - 2 : trackRect.Left + 2;
        Rectangle knobRect = new(knobX, trackRect.Top + 2, knobSize, knobSize);

        using SolidBrush knobBrush = new(Color.FromArgb(245, 247, 250));
        using Pen knobBorder = new(Color.FromArgb(48, 52, 56));
        e.Graphics.FillEllipse(knobBrush, knobRect);
        e.Graphics.DrawEllipse(knobBorder, knobRect);

        if (Focused)
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
            Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold),
            Margin = new Padding(0, 6, 8, 0),
            BackColor = Color.Transparent
        };

        _stateLabel = new Label
        {
            Text = stateText,
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
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
        Font = new Font("Segoe UI Semibold", 8.25f, FontStyle.Bold);
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
            Font = new Font("Segoe UI Semibold", 9.75f, FontStyle.Bold),
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
                Font = new Font("Segoe UI", 8.75f, FontStyle.Regular),
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

        Height = 32;
        ApplyTheme(palette);
        ResumeLayout(performLayout: true);
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

    public Color SurfaceBackgroundColor
    {
        get
        {
            if (_hovered || _pressed || _active)
            {
                if (_isDestructive)
                {
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
            int iconWidth = 20;
            int iconHeight = Math.Min(innerHeight, ControlDrawing.ScaleLogical(this, 18));
            _iconControl.Bounds = new Rectangle(left, y + Math.Max(0, (innerHeight - iconHeight) / 2), iconWidth, iconHeight);
            left = _iconControl.Right + 10;
        }

        if (_toggle != null)
        {
            Size toggleSize = _toggle.Size;
            _toggle.Location = new Point(right - toggleSize.Width, y + Math.Max(0, (innerHeight - toggleSize.Height) / 2));
            _titleLabel.Bounds = new Rectangle(left, y, Math.Max(40, _toggle.Left - left - 10), innerHeight);
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

        if (_hovered || _pressed || _active)
        {
            Rectangle fillRect = new(4, 1, Math.Max(8, Width - 8), Math.Max(8, Height - 2));
            using GraphicsPath path = ControlDrawing.RoundedRect(fillRect, 9);
            Color fill = SurfaceBackgroundColor;
            using SolidBrush fillBrush = new(fill);
            Color borderColor = _isDestructive
                ? Color.FromArgb(186, 82, 92)
                : (_active ? _palette.Accent : _palette.Border);
            int borderAlpha = _isDestructive ? 76 : (_active ? 72 : 28);
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

        if (_hovered || _pressed || _active)
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
        Size = new Size(94, 34);
        Margin = new Padding(0);
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        Invalidate();
    }

    public Color SurfaceBackgroundColor => _pressed
        ? _palette.ButtonPressed
        : (_hovered ? _palette.ButtonHover : _palette.ButtonBackground);

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
        using GraphicsPath path = ControlDrawing.RoundedRect(rect, 10);
        using SolidBrush fill = new(SurfaceBackgroundColor);
        using Pen border = new(Color.FromArgb(50, _palette.Border));
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);

        Rectangle iconRect = new(10, 9, 16, 16);
        using Pen iconPen = new(_palette.SecondaryText, 1.25f);
        e.Graphics.DrawRectangle(iconPen, iconRect);
        e.Graphics.DrawLine(iconPen, iconRect.Left + 4, iconRect.Top + 5, iconRect.Right - 4, iconRect.Top + 5);
        e.Graphics.DrawLine(iconPen, iconRect.Left + 4, iconRect.Top + 9, iconRect.Right - 4, iconRect.Top + 9);

        Rectangle textRect = new(iconRect.Right + 8, 0, Width - iconRect.Right - 18, Height);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
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
            Font = new Font("Segoe UI Semibold", 10.2f, FontStyle.Bold),
            Margin = new Padding(0),
            BackColor = Color.Transparent
        };
        _subtitleLabel = new Label
        {
            Text = subtitle,
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
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
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
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
    public ModernButton()
    {
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        AutoSize = true;
        MinimumSize = new Size(120, 38);
        Padding = new Padding(14, 0, 14, 0);
        TextAlign = ContentAlignment.MiddleCenter;
    }

    public void ApplyTheme(ThemePalette palette, bool emphasis = false, bool destructive = false, bool destructiveHoverEnabled = false)
    {
        Color destructiveBack = Color.FromArgb(74, 24, 31);
        Color destructiveHoverColor = Color.FromArgb(96, 28, 36);
        Color destructivePressed = Color.FromArgb(118, 32, 42);
        Color destructiveBorder = Color.FromArgb(132, 58, 66);
        bool useDestructiveBorder = destructive || destructiveHoverEnabled;

        BackColor = destructive
            ? destructiveBack
            : emphasis ? palette.Accent : palette.ButtonBackground;
        ForeColor = emphasis && !destructive ? Color.FromArgb(16, 22, 18) : palette.Text;
        FlatAppearance.BorderColor = useDestructiveBorder
            ? destructiveBorder
            : emphasis ? palette.Accent : palette.Border;
        FlatAppearance.MouseOverBackColor = destructive || destructiveHoverEnabled
            ? destructiveHoverColor
            : emphasis ? palette.AccentHover : palette.ButtonHover;
        FlatAppearance.MouseDownBackColor = destructive || destructiveHoverEnabled
            ? destructivePressed
            : emphasis ? palette.AccentPressed : palette.ButtonPressed;
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
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
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

    public Color SurfaceBackgroundColor => _pressed ? _palette.ButtonPressed : _hovered ? _palette.ButtonHover : _palette.ButtonBackground;

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
        using Pen border = new(Color.FromArgb(48, _palette.Border));
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
                BeginInvoke((MethodInvoker)(() =>
                {
                    if (!menu.IsDisposed)
                    {
                        menu.Dispose();
                    }
                }));
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
        Color backColor = e.Item.Selected ? _palette.ButtonHover : _palette.MenuBackground;
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
    public override Color MenuItemSelected => _palette.ButtonHover;
    public override Color MenuItemSelectedGradientBegin => _palette.ButtonHover;
    public override Color MenuItemSelectedGradientEnd => _palette.ButtonHover;
    public override Color MenuItemPressedGradientBegin => _palette.ButtonPressed;
    public override Color MenuItemPressedGradientMiddle => _palette.ButtonPressed;
    public override Color MenuItemPressedGradientEnd => _palette.ButtonPressed;
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
        using SolidBrush trackBrush = new(_palette.ButtonBackground);
        using Pen trackBorder = new(Color.FromArgb(60, _palette.Border));
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
    private readonly TableLayoutPanel _grid;
    private readonly TableLayoutPanel _left;
    private readonly TableLayoutPanel _right;
    private readonly Control _accessoryControl;
    private readonly int _rightColumnWidth;
    private readonly string? _valueText;

    public SettingsRow(ThemePalette palette, string title, string description, Control control, int rightColumnWidth = 220, string? valueText = null)
    {
        bool hasDescription = !string.IsNullOrWhiteSpace(description);
        _accessoryControl = control;
        _rightColumnWidth = Math.Max(96, rightColumnWidth);
        _valueText = valueText;
        CornerRadius = 14;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Margin = new Padding(0, 0, 0, 12);
        Padding = hasDescription ? new Padding(18, 16, 18, 16) : new Padding(18, 14, 18, 14);
        MinimumSize = new Size(0, hasDescription ? 72 : 56);
        BackColor = palette.ControlBackground;

        _grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
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
        _grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0, 2, 18, 2)
        };
        _left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _titleLabel = new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 10.8f, FontStyle.Bold),
            Margin = new Padding(0),
            BackColor = Color.Transparent
        };
        _descriptionLabel = new Label
        {
            Text = description,
            AutoSize = true,
            Font = new Font("Segoe UI", 9.1f, FontStyle.Regular),
            Margin = new Padding(0, 4, 0, 0),
            BackColor = Color.Transparent
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
        BorderAlpha = 22;
        _titleLabel.ForeColor = palette.Text;
        _descriptionLabel.ForeColor = palette.SecondaryText;
        Invalidate(true);
    }

    private void UpdateLayoutMetrics()
    {
        int availableWidth = Math.Max(520, Width - Padding.Horizontal);
        int leftWidth = Math.Max(280, availableWidth - _rightColumnWidth - 18);
        _grid.ColumnStyles[1].Width = _rightColumnWidth;
        _titleLabel.MaximumSize = new Size(leftWidth, 0);
        _descriptionLabel.MaximumSize = new Size(leftWidth, 0);

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
        Margin = new Padding(0, 0, 0, 22);
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
            Font = new Font("Segoe UI Semibold", 12.2f, FontStyle.Bold),
            Margin = new Padding(0),
            BackColor = Color.Transparent,
            ForeColor = palette.Text
        };
        _descriptionLabel = new Label
        {
            Text = description,
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            Font = new Font("Segoe UI", 9.2f, FontStyle.Regular),
            Margin = new Padding(0, 6, 0, 14),
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
    }

    public void AddRow(Control row)
    {
        row.Dock = DockStyle.Top;
        row.Margin = new Padding(0, 0, 0, 10);
        row.MinimumSize = new Size(1120, row.MinimumSize.Height);
        row.Width = 1120;
        _rows.RowCount++;
        _rows.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _rows.Controls.Add(row, 0, _nextRowIndex++);
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
}

internal sealed class SettingsPageView : Panel
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
            Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold),
            Margin = new Padding(0),
            BackColor = Color.Transparent,
            ForeColor = palette.Text
        };
        _descriptionLabel = new Label
        {
            Text = description,
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            Font = new Font("Segoe UI", 10f, FontStyle.Regular),
            Margin = new Padding(0, 8, 0, 24),
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
    }

    public void AddSection(SettingsSection section)
    {
        section.Dock = DockStyle.Top;
        section.Width = 1120;
        _sectionHost.RowCount++;
        _sectionHost.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _sectionHost.Controls.Add(section, 0, _nextSectionIndex++);
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
        BackColor = Color.FromArgb(18, 18, 18);
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
                BeginInvoke((MethodInvoker)(() =>
                {
                    if (!IsDisposed && Visible && !ContainsFocus && !IgnoreDeactivateClose)
                    {
                        Close();
                    }
                }));
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
        int popupContentWidth = ControlDrawing.ScaleLogical(this, DefaultLogicalContentWidth);
        Rectangle area = Screen.FromPoint(anchor).WorkingArea;
        int maxClientHeight = Math.Max(ControlDrawing.ScaleLogical(this, 220), area.Height - ControlDrawing.ScaleLogical(this, 24));

        ContentHost.MinimumSize = new Size(popupContentWidth, 0);
        ContentHost.MaximumSize = new Size(popupContentWidth, 0);
        ContentHost.Width = popupContentWidth;
        ContentHost.PerformLayout();

        Size desiredContent = MeasureContentHost(popupContentWidth);
        int naturalClientHeight = Math.Max(
            ControlDrawing.ScaleLogical(this, 90),
            desiredContent.Height + _surface.Padding.Vertical + Padding.Vertical);
        bool needsVerticalScroll = naturalClientHeight > maxClientHeight;
        int availableContentWidth = popupContentWidth - (needsVerticalScroll ? SystemInformation.VerticalScrollBarWidth : 0);
        availableContentWidth = Math.Max(ControlDrawing.ScaleLogical(this, 220), availableContentWidth);

        ContentHost.MinimumSize = new Size(availableContentWidth, 0);
        ContentHost.MaximumSize = new Size(availableContentWidth, 0);
        ContentHost.Width = availableContentWidth;
        ContentHost.PerformLayout();
        desiredContent = MeasureContentHost(availableContentWidth);

        int clientWidth = popupContentWidth + _surface.Padding.Horizontal + Padding.Horizontal;
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
        int y = anchor.Y - Height - gutter;

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

        if (y + Height > area.Bottom - gutter)
        {
            y = area.Bottom - Height - gutter;
        }

        Location = new Point(x, y);
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
