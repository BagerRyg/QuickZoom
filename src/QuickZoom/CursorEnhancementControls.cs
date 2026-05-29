using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
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
    private const int PreviewSlotSize = 164;
    private const int PreviewBaseCursorSize = 32;

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
        Width = 540;
        Height = 172;
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

        DrawCursorPreview(e.Graphics, Cursors.Default, new Rectangle(6, 4, PreviewSlotSize, PreviewSlotSize));
        DrawCursorPreview(e.Graphics, Cursors.IBeam, new Rectangle(188, 4, PreviewSlotSize, PreviewSlotSize));
        DrawCursorPreview(e.Graphics, Cursors.Hand, new Rectangle(370, 4, PreviewSlotSize, PreviewSlotSize));
    }

    private void DrawCursorPreview(Graphics graphics, Cursor cursor, Rectangle slot)
    {
        double scale = Math.Clamp(ScalePercent, 100, 500) / 100d;
        int cursorSize = Math.Clamp((int)Math.Round(PreviewBaseCursorSize * scale), PreviewBaseCursorSize, PreviewSlotSize);
        Rectangle bounds = new(
            slot.Left + ((slot.Width - cursorSize) / 2),
            slot.Top + ((slot.Height - cursorSize) / 2),
            cursorSize,
            cursorSize);

        using Bitmap source = new(cursorSize, cursorSize, PixelFormat.Format32bppArgb);
        using (Graphics sourceGraphics = Graphics.FromImage(source))
        {
            sourceGraphics.Clear(Color.Transparent);
            sourceGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            sourceGraphics.PixelOffsetMode = PixelOffsetMode.Half;
            cursor.DrawStretched(sourceGraphics, new Rectangle(Point.Empty, source.Size));
        }

        using Bitmap recolored = RecolorCursorPreview(source);
        Rectangle visibleBounds = GetVisibleBounds(recolored);
        int slotCenterX = slot.Left + (slot.Width / 2);
        int slotCenterY = slot.Top + (slot.Height / 2);
        int drawX = slotCenterX - visibleBounds.Left - (visibleBounds.Width / 2);
        int drawY = slotCenterY - visibleBounds.Top - (visibleBounds.Height / 2);
        graphics.DrawImage(recolored, new Rectangle(drawX, drawY, recolored.Width, recolored.Height));
    }

    private Bitmap RecolorCursorPreview(Bitmap source)
    {
        int outlineRadius = Math.Max(1, (int)Math.Round(ScalePercent / 100d));
        int width = source.Width;
        int height = source.Height;
        bool[,] mask = new bool[width, height];
        var output = new Bitmap(width, height, PixelFormat.Format32bppArgb);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                mask[x, y] = source.GetPixel(x, y).A > 16;
            }
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (mask[x, y])
                {
                    output.SetPixel(x, y, Color.FromArgb(source.GetPixel(x, y).A, FillColor));
                    continue;
                }

                if (HasNeighbor(mask, x, y, outlineRadius))
                {
                    output.SetPixel(x, y, BorderColor);
                }
            }
        }

        return output;
    }

    private static bool HasNeighbor(bool[,] mask, int x, int y, int radius)
    {
        int width = mask.GetLength(0);
        int height = mask.GetLength(1);

        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < width && ny < height && mask[nx, ny])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static Rectangle GetVisibleBounds(Bitmap bitmap)
    {
        int left = bitmap.Width;
        int top = bitmap.Height;
        int right = -1;
        int bottom = -1;

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A <= 16)
                {
                    continue;
                }

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        return right < left || bottom < top
            ? new Rectangle(0, 0, bitmap.Width, bitmap.Height)
            : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }
}
