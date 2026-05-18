using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace QuickZoom;

internal sealed class ColorPaletteControl : Control
{
    private readonly ThemePalette _palette;
    private readonly Color[] _colors;
    private Color _selectedColor;
    private int _hoveredIndex = -1;
    private const int SwatchSize = 18;
    private const int SwatchGap = 4;

    public ColorPaletteControl(ThemePalette palette, Color[] colors, Color selectedColor)
    {
        _palette = palette;
        _colors = colors;
        _selectedColor = selectedColor;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.UserPaint |
            ControlStyles.Selectable,
            true);
        Width = 264;
        Height = 66;
        AutoSize = false;
        BackColor = System.Drawing.Color.Transparent;
        Margin = new Padding(0);
        Padding = new Padding(0);
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    public event EventHandler<Color>? ColorSelected;

    public Color SelectedColor
    {
        get => _selectedColor;
        set
        {
            _selectedColor = value;
            Invalidate();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int nextHoveredIndex = HitTest(e.Location);
        if (_hoveredIndex != nextHoveredIndex)
        {
            _hoveredIndex = nextHoveredIndex;
            Invalidate();
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hoveredIndex = -1;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnClick(EventArgs e)
    {
        int index = _hoveredIndex;
        if (index >= 0 && index < _colors.Length)
        {
            SelectedColor = _colors[index];
            ColorSelected?.Invoke(this, _colors[index]);
        }

        base.OnClick(e);
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            int selectedIndex = Array.FindIndex(_colors, color => ColorsEqual(color, _selectedColor));
            if (selectedIndex >= 0)
            {
                ColorSelected?.Invoke(this, _colors[selectedIndex]);
            }

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

        for (int i = 0; i < _colors.Length; i++)
        {
            Rectangle rect = GetSwatchBounds(i);
            if (!ClientRectangle.IntersectsWith(rect))
            {
                continue;
            }

            DrawSwatch(e.Graphics, rect, _colors[i], i == _hoveredIndex, ColorsEqual(_colors[i], _selectedColor));
        }

        if (Focused)
        {
            Rectangle focusRect = new(0, 0, Width - 1, Height - 1);
            using Pen focusPen = new(System.Drawing.Color.FromArgb(120, _palette.Accent), 1.25f);
            e.Graphics.DrawRectangle(focusPen, focusRect);
        }
    }

    private int HitTest(Point point)
    {
        for (int i = 0; i < _colors.Length; i++)
        {
            if (GetSwatchBounds(i).Contains(point))
            {
                return i;
            }
        }

        return -1;
    }

    private Rectangle GetSwatchBounds(int index)
    {
        int columns = Math.Max(1, (Width + SwatchGap) / (SwatchSize + SwatchGap));
        int row = index / columns;
        int column = index % columns;
        return new Rectangle(column * (SwatchSize + SwatchGap), row * (SwatchSize + SwatchGap), SwatchSize, SwatchSize);
    }

    private void DrawSwatch(Graphics graphics, Rectangle bounds, Color color, bool hovered, bool selected)
    {
        Rectangle outer = new(bounds.X + 1, bounds.Y + 1, bounds.Width - 3, bounds.Height - 3);
        using GraphicsPath outerPath = ControlDrawing.RoundedRect(outer, 6);
        using SolidBrush fillBrush = new(color);
        using Pen fillBorder = new(color.GetBrightness() > 0.78f ? System.Drawing.Color.FromArgb(150, 60, 65, 72) : System.Drawing.Color.FromArgb(110, System.Drawing.Color.White));
        graphics.FillPath(fillBrush, outerPath);
        graphics.DrawPath(fillBorder, outerPath);

        if (hovered || selected)
        {
            Color outline = selected ? _palette.Accent : _palette.SecondaryText;
            using Pen outlinePen = new(outline, selected ? 2f : 1.25f);
            graphics.DrawPath(outlinePen, outerPath);
        }
    }

    private static bool ColorsEqual(Color left, Color right) => left.ToArgb() == Color.FromArgb(255, right).ToArgb();
}

internal sealed class ColorSwatchControl : Control
{
    private readonly ThemePalette _palette;
    private bool _hovered;
    private bool _isSelected;

    public ColorSwatchControl(ThemePalette palette, Color color)
    {
        _palette = palette;
        Color = color;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.UserPaint |
            ControlStyles.Selectable,
            true);
        Size = new Size(18, 18);
        Margin = new Padding(0, 0, 4, 4);
        BackColor = System.Drawing.Color.Transparent;
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    public Color Color { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            Invalidate();
        }
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

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            OnClick(EventArgs.Empty);
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

        Rectangle outer = new(1, 1, Width - 3, Height - 3);
        using GraphicsPath outerPath = ControlDrawing.RoundedRect(outer, 6);
        using SolidBrush fillBrush = new(Color);
        using Pen fillBorder = new(Color.GetBrightness() > 0.78f ? System.Drawing.Color.FromArgb(150, 60, 65, 72) : System.Drawing.Color.FromArgb(110, System.Drawing.Color.White));
        e.Graphics.FillPath(fillBrush, outerPath);
        e.Graphics.DrawPath(fillBorder, outerPath);

        if (_hovered || _isSelected || Focused)
        {
            Color outline = _isSelected ? _palette.Accent : _palette.SecondaryText;
            using Pen outlinePen = new(outline, _isSelected ? 2f : 1.25f);
            e.Graphics.DrawPath(outlinePen, outerPath);
        }
    }
}

internal sealed class CursorPreviewControl : Control
{
    private ThemePalette _palette;

    public CursorPreviewControl(ThemePalette palette, Color fillColor, Color borderColor, int scalePercent)
    {
        _palette = palette;
        FillColor = fillColor;
        BorderColor = borderColor;
        ScalePercent = scalePercent;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.UserPaint,
            true);
        Width = 260;
        Height = 58;
        Margin = new Padding(0);
        BackColor = Color.Transparent;
    }

    public Color FillColor { get; set; }
    public Color BorderColor { get; set; }
    public int ScalePercent { get; set; }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        Invalidate();
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

        Rectangle surface = new(0, 0, Width - 1, Height - 1);
        using GraphicsPath surfacePath = ControlDrawing.RoundedRect(surface, 10);
        using SolidBrush surfaceBrush = new(_palette.ButtonBackground);
        using Pen surfaceBorder = new(Color.FromArgb(70, _palette.Border));
        e.Graphics.FillPath(surfaceBrush, surfacePath);
        e.Graphics.DrawPath(surfaceBorder, surfacePath);

        using SolidBrush fill = new(FillColor);
        int scale = Math.Clamp((int)Math.Round(ScalePercent / 100d), 1, 5);
        using Pen border = new(BorderColor, Math.Max(2f, scale));

        DrawArrow(e.Graphics, fill, border, new Point(28, 8), scale);
        DrawIBeam(e.Graphics, fill, border, new Rectangle(104, 10, 30, 34), scale);
        DrawHand(e.Graphics, fill, border, new Point(180, 4), scale);
    }

    private static void DrawArrow(Graphics graphics, Brush fill, Pen border, Point origin, int scale)
    {
        using GraphicsPath path = new();
        path.AddPolygon(
        [
            new Point(origin.X, origin.Y),
            new Point(origin.X + 34, origin.Y + 25),
            new Point(origin.X + 19, origin.Y + 27),
            new Point(origin.X + 27, origin.Y + 43),
            new Point(origin.X + 20, origin.Y + 47),
            new Point(origin.X + 12, origin.Y + 31),
            new Point(origin.X, origin.Y + 39)
        ]);
        graphics.DrawPath(border, path);
        graphics.FillPath(fill, path);
    }

    private static void DrawIBeam(Graphics graphics, Brush fill, Pen border, Rectangle bounds, int scale)
    {
        using Pen fillPen = new((fill as SolidBrush)?.Color ?? Color.White, Math.Max(4f, 3f + scale));
        using Pen borderPen = new(border.Color, fillPen.Width + Math.Max(3f, scale));
        graphics.DrawLine(borderPen, bounds.Left + (bounds.Width / 2), bounds.Top, bounds.Left + (bounds.Width / 2), bounds.Bottom);
        graphics.DrawLine(borderPen, bounds.Left, bounds.Top, bounds.Right, bounds.Top);
        graphics.DrawLine(borderPen, bounds.Left, bounds.Bottom, bounds.Right, bounds.Bottom);
        graphics.DrawLine(fillPen, bounds.Left + (bounds.Width / 2), bounds.Top, bounds.Left + (bounds.Width / 2), bounds.Bottom);
        graphics.DrawLine(fillPen, bounds.Left, bounds.Top, bounds.Right, bounds.Top);
        graphics.DrawLine(fillPen, bounds.Left, bounds.Bottom, bounds.Right, bounds.Bottom);
    }

    private static void DrawHand(Graphics graphics, Brush fill, Pen border, Point origin, int scale)
    {
        using GraphicsPath path = new();
        path.AddLines(
        [
            new Point(origin.X + 8, origin.Y + 37),
            new Point(origin.X + 8, origin.Y + 20),
            new Point(origin.X + 16, origin.Y + 20),
            new Point(origin.X + 16, origin.Y + 31),
            new Point(origin.X + 22, origin.Y + 8),
            new Point(origin.X + 30, origin.Y + 10),
            new Point(origin.X + 28, origin.Y + 32),
            new Point(origin.X + 36, origin.Y + 16),
            new Point(origin.X + 44, origin.Y + 20),
            new Point(origin.X + 38, origin.Y + 38),
            new Point(origin.X + 30, origin.Y + 48),
            new Point(origin.X + 14, origin.Y + 48)
        ]);
        path.CloseFigure();
        graphics.DrawPath(border, path);
        graphics.FillPath(fill, path);
    }
}
