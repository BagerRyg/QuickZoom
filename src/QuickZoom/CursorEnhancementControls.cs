using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickZoom;

internal sealed class ColorPaletteControl : Control
{
    private readonly ThemePalette _palette;
    private readonly Color[] _colors;
    private Color _selectedColor;
    private int _hoveredIndex = -1;
    private int _keyboardIndex = -1;
    private bool _updatingHeight;
    private const int LogicalVisibleSwatchSize = 18;
    private const int LogicalHitTargetSize = 28;

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
        Width = 600;
        Height = 84;
        AutoSize = false;
        BackColor = System.Drawing.Color.Transparent;
        Margin = new Padding(0);
        Padding = new Padding(0);
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.List;
        _keyboardIndex = Array.FindIndex(_colors, color => ColorsEqual(color, _selectedColor));
    }

    public event EventHandler<Color>? ColorSelected;

    public IReadOnlyList<string>? AccessibleColorNames { get; set; }

    public Color SelectedColor
    {
        get => _selectedColor;
        set
        {
            _selectedColor = value;
            _keyboardIndex = Array.FindIndex(_colors, color => ColorsEqual(color, _selectedColor));
            AccessibilityNotifyClients(AccessibleEvents.Selection, Math.Max(0, _keyboardIndex + 1));
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
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

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        _keyboardIndex = HitTest(e.Location);
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hoveredIndex = -1;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnClick(EventArgs e)
    {
        // MouseDown records the hit-tested cell even for touch/pen input,
        // where no preceding hover event is guaranteed.
        int index = _keyboardIndex;
        if (index >= 0 && index < _colors.Length)
        {
            SelectedColor = _colors[index];
            ColorSelected?.Invoke(this, _colors[index]);
        }

        base.OnClick(e);
    }

    protected override bool IsInputKey(Keys keyData) => keyData is
        Keys.Space or Keys.Enter or Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End ||
        base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            SelectIndex(_keyboardIndex);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End)
        {
            int columns = GetColumnCount();
            int current = _keyboardIndex >= 0 ? _keyboardIndex : 0;
            _keyboardIndex = e.KeyCode switch
            {
                Keys.Left => Math.Max(0, current - 1),
                Keys.Right => Math.Min(_colors.Length - 1, current + 1),
                Keys.Up => Math.Max(0, current - columns),
                Keys.Down => Math.Min(_colors.Length - 1, current + columns),
                Keys.Home => 0,
                Keys.End => _colors.Length - 1,
                _ => current
            };
            AccessibilityNotifyClients(AccessibleEvents.Focus, _keyboardIndex + 1);
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            Invalidate();
            e.Handled = true;
            e.SuppressKeyPress = true;
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

            DrawSwatch(
                e.Graphics,
                rect,
                _colors[i],
                i == _hoveredIndex,
                ColorsEqual(_colors[i], _selectedColor),
                ControlDrawing.ShouldDrawFocus(this, ShowFocusCues) && i == _keyboardIndex);
        }

    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdatePreferredHeight();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        if (_keyboardIndex < 0 && _colors.Length > 0)
        {
            _keyboardIndex = 0;
        }

        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    private int HitTest(Point point)
    {
        for (int i = 0; i < _colors.Length; i++)
        {
            if (GetHitTargetBounds(i).Contains(point))
            {
                return i;
            }
        }

        return -1;
    }

    private Rectangle GetSwatchBounds(int index)
    {
        Rectangle hitTarget = GetHitTargetBounds(index);
        int swatchSize = Math.Min(
            ControlDrawing.ScaleLogical(this, LogicalVisibleSwatchSize),
            Math.Min(hitTarget.Width, hitTarget.Height));
        int x = hitTarget.Left + ((hitTarget.Width - swatchSize) / 2);
        int y = hitTarget.Top + ((hitTarget.Height - swatchSize) / 2);
        return new Rectangle(x, y, swatchSize, swatchSize);
    }

    private Rectangle GetHitTargetBounds(int index)
    {
        int hitTargetSize = ControlDrawing.ScaleLogical(this, LogicalHitTargetSize);
        int columns = GetColumnCount();
        int row = index / columns;
        int column = index % columns;
        return new Rectangle(column * hitTargetSize, row * hitTargetSize, hitTargetSize, hitTargetSize);
    }

    private void DrawSwatch(Graphics graphics, Rectangle bounds, Color color, bool hovered, bool selected, bool keyboardFocused)
    {
        Rectangle outer = new(bounds.X + 1, bounds.Y + 1, Math.Max(1, bounds.Width - 2), Math.Max(1, bounds.Height - 2));
        using GraphicsPath outerPath = ControlDrawing.RoundedRect(outer, Math.Max(3, ControlDrawing.ScaleLogical(this, 4)));
        using SolidBrush fillBrush = new(color);
        using Pen fillBorder = new(color.GetBrightness() > 0.78f ? System.Drawing.Color.FromArgb(150, 60, 65, 72) : System.Drawing.Color.FromArgb(110, System.Drawing.Color.White));
        graphics.FillPath(fillBrush, outerPath);
        graphics.DrawPath(fillBorder, outerPath);

        if (hovered || selected || keyboardFocused)
        {
            Color outline = keyboardFocused ? ControlDrawing.FocusColor(_palette) : selected ? _palette.Accent : _palette.SecondaryText;
            using Pen outlinePen = new(outline, keyboardFocused ? 2f : selected ? 1.5f : 1.25f);
            graphics.DrawPath(outlinePen, outerPath);
        }
    }

    private int GetColumnCount()
    {
        int cellSize = ControlDrawing.ScaleLogical(this, LogicalHitTargetSize);
        return Math.Max(1, Width / Math.Max(1, cellSize));
    }

    private void UpdatePreferredHeight()
    {
        if (_updatingHeight || _colors.Length == 0 || Width <= 0)
        {
            return;
        }

        _updatingHeight = true;
        int cellSize = ControlDrawing.ScaleLogical(this, LogicalHitTargetSize);
        int rows = (int)Math.Ceiling(_colors.Length / (double)GetColumnCount());
        int preferredHeight = Math.Max(cellSize, rows * cellSize);
        if (Height != preferredHeight)
        {
            Height = preferredHeight;
        }
        _updatingHeight = false;
    }

    private void SelectIndex(int index)
    {
        if (index < 0 || index >= _colors.Length)
        {
            return;
        }

        SelectedColor = _colors[index];
        ColorSelected?.Invoke(this, _colors[index]);
    }

    private string GetAccessibleColorName(int index)
    {
        if (AccessibleColorNames != null && index >= 0 && index < AccessibleColorNames.Count &&
            !string.IsNullOrWhiteSpace(AccessibleColorNames[index]))
        {
            return AccessibleColorNames[index];
        }

        Color color = _colors[index];
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new ColorPaletteAccessibleObject(this);

    private sealed class ColorPaletteAccessibleObject(ColorPaletteControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.List;

        public override string? Value
        {
            get => owner._keyboardIndex >= 0 && owner._keyboardIndex < owner._colors.Length
                ? owner.GetAccessibleColorName(owner._keyboardIndex)
                : null;
            set { }
        }

        public override int GetChildCount() => owner._colors.Length;

        public override AccessibleObject? GetChild(int index) => index >= 0 && index < owner._colors.Length
            ? new ColorItemAccessibleObject(owner, index)
            : null;
    }

    private sealed class ColorItemAccessibleObject(ColorPaletteControl owner, int index) : AccessibleObject
    {
        public override string? Name
        {
            get => owner.GetAccessibleColorName(index);
            set { }
        }

        public override AccessibleRole Role => AccessibleRole.RadioButton;

        public override AccessibleStates State => AccessibleStates.Focusable |
            AccessibleStates.Selectable |
            (ColorsEqual(owner._colors[index], owner.SelectedColor) ? AccessibleStates.Checked | AccessibleStates.Selected : AccessibleStates.None) |
            (owner.Focused && owner._keyboardIndex == index ? AccessibleStates.Focused : AccessibleStates.None);

        public override Rectangle Bounds
        {
            get
            {
                Rectangle bounds = owner.GetHitTargetBounds(index);
                return new Rectangle(owner.PointToScreen(bounds.Location), bounds.Size);
            }
        }

        public override void DoDefaultAction()
        {
            owner._keyboardIndex = index;
            owner.SelectIndex(index);
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
        Size = new Size(40, 40);
        Margin = new Padding(0, 0, 6, 6);
        BackColor = System.Drawing.Color.Transparent;
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleName = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        AccessibleRole = AccessibleRole.RadioButton;
    }

    public Color Color { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
            AccessibilityNotifyClients(AccessibleEvents.Selection, -1);
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
            e.SuppressKeyPress = true;
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

        bool keyboardFocused = ControlDrawing.ShouldDrawFocus(this, ShowFocusCues);
        if (_hovered || _isSelected || keyboardFocused)
        {
            Color outline = keyboardFocused ? ControlDrawing.FocusColor(_palette) : _isSelected ? _palette.Accent : _palette.SecondaryText;
            using Pen outlinePen = new(outline, keyboardFocused ? 2f : _isSelected ? 1.5f : 1.25f);
            e.Graphics.DrawPath(outlinePen, outerPath);
        }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new ColorSwatchAccessibleObject(this);

    private sealed class ColorSwatchAccessibleObject(ColorSwatchControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.RadioButton;

        public override AccessibleStates State => base.State |
            AccessibleStates.Focusable |
            AccessibleStates.Selectable |
            (owner.IsSelected ? AccessibleStates.Checked | AccessibleStates.Selected : AccessibleStates.None);

        public override string? Value
        {
            get => owner.AccessibleName;
            set { }
        }

        public override void DoDefaultAction() => owner.OnClick(EventArgs.Empty);
    }
}

internal sealed class CursorPreviewControl : Control
{
    private ThemePalette _palette;
    private Color _fillColor;
    private Color _borderColor;
    private int _scalePercent;
    private Bitmap[]? _cachedPreviews;
    private Rectangle[]? _cachedVisibleBounds;
    private bool _previewBuildRunning;
    private bool _previewBuildPending;
    private int _previewGeneration;
    private const int PreviewSlotSize = 164;
    private const int PreviewBaseCursorSize = 32;

    public CursorPreviewControl(ThemePalette palette, Color fillColor, Color borderColor, int scalePercent)
    {
        _palette = palette;
        _fillColor = fillColor;
        _borderColor = borderColor;
        _scalePercent = scalePercent;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.UserPaint,
            true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        Width = 540;
        Height = 172;
        Margin = new Padding(0);
        BackColor = Color.Transparent;
        AccessibleRole = AccessibleRole.Graphic;
    }

    public Color FillColor
    {
        get => _fillColor;
        set
        {
            if (_fillColor == value) return;
            _fillColor = value;
            InvalidatePreviewCache();
        }
    }

    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            if (_borderColor == value) return;
            _borderColor = value;
            InvalidatePreviewCache();
        }
    }

    public int ScalePercent
    {
        get => _scalePercent;
        set
        {
            int normalized = Math.Clamp(value, 100, 500);
            if (_scalePercent == normalized) return;
            _scalePercent = normalized;
            InvalidatePreviewCache();
        }
    }

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

        EnsurePreviewBuildQueued();
        int outerPadding = Math.Min(6, Math.Max(2, Width / 40));
        int slotGap = Math.Min(18, Math.Max(2, Width / 30));
        int availableWidth = Math.Max(3, Width - (outerPadding * 2) - (slotGap * 2));
        int slotSize = Math.Max(1, Math.Min(PreviewSlotSize, Math.Min(Math.Max(1, Height - 8), availableWidth / 3)));
        int totalWidth = (slotSize * 3) + (slotGap * 2);
        int startX = Math.Max(0, (Width - totalWidth) / 2);
        int startY = Math.Max(0, (Height - slotSize) / 2);

        for (int index = 0; index < 3; index++)
        {
            Rectangle slot = new(startX + (index * (slotSize + slotGap)), startY, slotSize, slotSize);
            DrawPreviewBackground(e.Graphics, slot);
            DrawCachedCursorPreview(e.Graphics, index, slot);
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_previewBuildPending)
        {
            StartPreviewCacheBuild();
        }
        else
        {
            EnsurePreviewBuildQueued();
        }
    }

    private void EnsurePreviewBuildQueued()
    {
        if ((_cachedPreviews != null && _cachedVisibleBounds != null) ||
            _previewBuildRunning ||
            _previewBuildPending ||
            IsDisposed)
        {
            return;
        }

        RequestPreviewCacheRebuild();
    }

    private void RequestPreviewCacheRebuild()
    {
        _previewGeneration++;
        _previewBuildPending = true;
        if (IsHandleCreated)
        {
            StartPreviewCacheBuild();
        }

        Invalidate();
    }

    private async void StartPreviewCacheBuild()
    {
        if (_previewBuildRunning || !_previewBuildPending || IsDisposed)
        {
            return;
        }

        _previewBuildPending = false;
        _previewBuildRunning = true;
        int generation = _previewGeneration;
        int scalePercent = _scalePercent;
        Color fillColor = _fillColor;
        Color borderColor = _borderColor;
        (Bitmap[] Previews, Rectangle[] VisibleBounds)? result = null;
        try
        {
            result = await Task.Run(() => BuildPreviewCache(scalePercent, fillColor, borderColor));
        }
        catch (Exception ex)
        {
            ErrorLog.Write("CursorPreview.Build", ex);
        }

        _previewBuildRunning = false;
        if (result.HasValue)
        {
            if (!IsDisposed && generation == _previewGeneration)
            {
                Bitmap[]? previousPreviews = _cachedPreviews;
                _cachedPreviews = result.Value.Previews;
                _cachedVisibleBounds = result.Value.VisibleBounds;
                DisposePreviews(previousPreviews);
                Invalidate();
            }
            else
            {
                DisposePreviews(result.Value.Previews);
            }
        }

        if (!IsDisposed && _previewBuildPending)
        {
            StartPreviewCacheBuild();
        }
    }

    private static (Bitmap[] Previews, Rectangle[] VisibleBounds) BuildPreviewCache(
        int scalePercent,
        Color fillColor,
        Color borderColor)
    {
        Cursor[] cursors = [Cursors.Default, Cursors.IBeam, Cursors.Hand];
        var previews = new Bitmap[cursors.Length];
        var visibleBounds = new Rectangle[cursors.Length];
        double scale = Math.Clamp(scalePercent, 100, 500) / 100d;
        int cursorSize = Math.Clamp((int)Math.Round(PreviewBaseCursorSize * scale), PreviewBaseCursorSize, PreviewSlotSize);
        int outlineRadius = Math.Max(1, (int)Math.Round(scale));

        try
        {
            for (int i = 0; i < cursors.Length; i++)
            {
                using Bitmap source = CursorBitmapProcessing.RenderSystemCursor(cursors[i], cursorSize, cursorSize);
                Bitmap preview = CursorBitmapProcessing.Recolor(source, fillColor, borderColor, outlineRadius);
                previews[i] = preview;
                visibleBounds[i] = CursorBitmapProcessing.GetVisibleBounds(preview);
            }

            return (previews, visibleBounds);
        }
        catch
        {
            DisposePreviews(previews);
            throw;
        }
    }

    private void DrawCachedCursorPreview(Graphics graphics, int index, Rectangle slot)
    {
        if (_cachedPreviews == null || _cachedVisibleBounds == null)
        {
            return;
        }

        Bitmap preview = _cachedPreviews[index];
        Rectangle visibleBounds = _cachedVisibleBounds[index];
        int slotCenterX = slot.Left + (slot.Width / 2);
        int slotCenterY = slot.Top + (slot.Height / 2);
        int inset = Math.Max(2, Math.Min(6, slot.Width / 12));
        int availableWidth = Math.Max(1, slot.Width - (inset * 2));
        int availableHeight = Math.Max(1, slot.Height - (inset * 2));
        float scale = Math.Min(
            1f,
            Math.Min(
                availableWidth / (float)Math.Max(1, visibleBounds.Width),
                availableHeight / (float)Math.Max(1, visibleBounds.Height)));

        if (scale >= 0.999f)
        {
            int drawX = slotCenterX - visibleBounds.Left - (visibleBounds.Width / 2);
            int drawY = slotCenterY - visibleBounds.Top - (visibleBounds.Height / 2);
            graphics.DrawImageUnscaled(preview, drawX, drawY);
            return;
        }

        float visibleCenterX = visibleBounds.Left + (visibleBounds.Width / 2f);
        float visibleCenterY = visibleBounds.Top + (visibleBounds.Height / 2f);
        RectangleF destination = new(
            slotCenterX - (visibleCenterX * scale),
            slotCenterY - (visibleCenterY * scale),
            preview.Width * scale,
            preview.Height * scale);
        GraphicsState state = graphics.Save();
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.DrawImage(
            preview,
            destination,
            new RectangleF(0, 0, preview.Width, preview.Height),
            GraphicsUnit.Pixel);
        graphics.Restore(state);
    }

    private void DrawPreviewBackground(Graphics graphics, Rectangle slot)
    {
        Rectangle bounds = new(slot.X, slot.Y, slot.Width - 1, slot.Height - 1);
        using GraphicsPath path = ControlDrawing.RoundedRect(bounds, 8);
        Color backgroundColor = AccessibilityPreferences.HighContrast
            ? SystemColors.Window
            : _palette.ControlBackground;
        using SolidBrush backgroundBrush = new(backgroundColor);
        graphics.FillPath(backgroundBrush, path);

        Color borderColor = AccessibilityPreferences.HighContrast ? SystemColors.WindowText : _palette.Border;
        using Pen borderPen = new(borderColor, AccessibilityPreferences.HighContrast ? 2f : 1f);
        graphics.DrawPath(borderPen, path);
    }

    private void InvalidatePreviewCache()
    {
        RequestPreviewCacheRebuild();
    }

    private static void DisposePreviews(Bitmap[]? previews)
    {
        if (previews == null)
        {
            return;
        }

        foreach (Bitmap bitmap in previews)
        {
            bitmap?.Dispose();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _previewGeneration++;
            _previewBuildPending = false;
            DisposePreviews(_cachedPreviews);
            _cachedPreviews = null;
            _cachedVisibleBounds = null;
        }

        base.Dispose(disposing);
    }

}

internal static class CursorBitmapProcessing
{
    private const int DiNormal = 0x0003;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DrawIconEx(
        IntPtr hdc,
        int xLeft,
        int yTop,
        IntPtr hIcon,
        int cxWidth,
        int cyWidth,
        int istepIfAniCur,
        IntPtr hbrFlickerFreeDraw,
        int diFlags);

    internal static Bitmap RenderSystemCursor(Cursor cursor, int width, int height)
    {
        int targetWidth = Math.Max(1, width);
        int targetHeight = Math.Max(1, height);
        var bitmap = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        IntPtr hdc = graphics.GetHdc();
        bool rendered;
        try
        {
            rendered = DrawIconEx(
                hdc,
                0,
                0,
                cursor.Handle,
                targetWidth,
                targetHeight,
                0,
                IntPtr.Zero,
                DiNormal);
        }
        finally
        {
            graphics.ReleaseHdc(hdc);
        }

        if (!rendered)
        {
            cursor.DrawStretched(graphics, new Rectangle(Point.Empty, bitmap.Size));
        }

        return bitmap;
    }

    internal static Bitmap Recolor(Bitmap source, Color fillColor, Color borderColor, int outlineRadius)
    {
        int width = source.Width;
        int height = source.Height;
        int radius = Math.Max(1, outlineRadius);
        var output = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        Rectangle bounds = new(0, 0, width, height);
        byte[] sourcePixels = ReadPixels(source, bounds, out int sourceStride);
        int outputStride = GetStride(output, bounds);
        byte[] outputPixels = new byte[outputStride * height];
        bool[] mask = new bool[width * height];

        for (int y = 0; y < height; y++)
        {
            int sourceRow = y * sourceStride;
            int maskRow = y * width;
            for (int x = 0; x < width; x++)
            {
                mask[maskRow + x] = sourcePixels[sourceRow + (x * 4) + 3] > 16;
            }
        }

        int radiusSquared = radius * radius;
        var outlineOffsets = new List<Point>();
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if ((dx * dx) + (dy * dy) <= radiusSquared)
                {
                    outlineOffsets.Add(new Point(dx, dy));
                }
            }
        }

        bool[] outlineMask = new bool[width * height];
        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                if (!mask[row + x])
                {
                    continue;
                }

                foreach (Point offset in outlineOffsets)
                {
                    int outlineX = x + offset.X;
                    int outlineY = y + offset.Y;
                    if ((uint)outlineX >= (uint)width || (uint)outlineY >= (uint)height)
                    {
                        continue;
                    }

                    int outlineIndex = (outlineY * width) + outlineX;
                    if (!mask[outlineIndex])
                    {
                        outlineMask[outlineIndex] = true;
                    }
                }
            }
        }

        for (int y = 0; y < height; y++)
        {
            int sourceRow = y * sourceStride;
            int outputRow = y * outputStride;
            for (int x = 0; x < width; x++)
            {
                int pixelOffset = outputRow + (x * 4);
                int sourceOffset = sourceRow + (x * 4);
                if (mask[(y * width) + x])
                {
                    outputPixels[pixelOffset] = fillColor.B;
                    outputPixels[pixelOffset + 1] = fillColor.G;
                    outputPixels[pixelOffset + 2] = fillColor.R;
                    outputPixels[pixelOffset + 3] = sourcePixels[sourceOffset + 3];
                }
                else if (outlineMask[(y * width) + x])
                {
                    outputPixels[pixelOffset] = borderColor.B;
                    outputPixels[pixelOffset + 1] = borderColor.G;
                    outputPixels[pixelOffset + 2] = borderColor.R;
                    outputPixels[pixelOffset + 3] = borderColor.A;
                }
            }
        }

        WritePixels(output, bounds, outputPixels, outputStride);
        return output;
    }

    internal static Rectangle GetVisibleBounds(Bitmap bitmap)
    {
        Rectangle bounds = new(0, 0, bitmap.Width, bitmap.Height);
        byte[] pixels = ReadPixels(bitmap, bounds, out int stride);
        int left = bitmap.Width;
        int top = bitmap.Height;
        int right = -1;
        int bottom = -1;

        for (int y = 0; y < bitmap.Height; y++)
        {
            int row = y * stride;
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (pixels[row + (x * 4) + 3] <= 16)
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
            ? bounds
            : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    private static byte[] ReadPixels(Bitmap bitmap, Rectangle bounds, out int stride)
    {
        BitmapData data = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            stride = Math.Abs(data.Stride);
            byte[] pixels = new byte[stride * bounds.Height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            return pixels;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static int GetStride(Bitmap bitmap, Rectangle bounds)
    {
        BitmapData data = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            return Math.Abs(data.Stride);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static void WritePixels(Bitmap bitmap, Rectangle bounds, byte[] pixels, int stride)
    {
        BitmapData data = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            int destinationStride = Math.Abs(data.Stride);
            if (destinationStride == stride)
            {
                Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
                return;
            }

            for (int y = 0; y < bounds.Height; y++)
            {
                Marshal.Copy(pixels, y * stride, IntPtr.Add(data.Scan0, y * data.Stride), Math.Min(stride, destinationStride));
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
