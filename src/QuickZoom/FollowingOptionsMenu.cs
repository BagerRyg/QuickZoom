using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace QuickZoom;

// Keep native menu dismissal, keyboard navigation and checked accessibility states.
// Only the surface and item layout are custom.
internal sealed class FollowingOptionsMenu : ContextMenuStrip
{
    private readonly Font _titleFont = ControlDrawing.UiFont("Segoe UI Semibold", 9.75f, FontStyle.Bold);
    private readonly Font _descriptionFont = ControlDrawing.UiFont("Segoe UI", 9f, FontStyle.Regular);

    internal FollowingOptionsMenu(ThemePalette palette)
    {
        AutoSize = false;
        ShowImageMargin = false;
        ShowCheckMargin = false;
        ShowItemToolTips = false;
        Font = _titleFont;
        BackColor = palette.MenuBackground;
        ForeColor = palette.Text;
        Renderer = new FollowingMenuRenderer(this, palette);
    }

    internal Font DescriptionFont => _descriptionFont;
    internal int Gap => ControlDrawing.ScaleLogical(this, 6);
    protected override Padding DefaultPadding => new(ControlDrawing.ScaleLogical(this, 8));

    internal void FitTo(Control anchor, Rectangle workingArea)
    {
        int pad = ControlDrawing.ScaleLogical(anchor, 8);
        Padding = new Padding(pad);
        int titleWidth = Items.OfType<FollowingOptionItem>().Max(item =>
            TextRenderer.MeasureText(item.Text, Font).Width);
        int width = Math.Min(workingArea.Width, Math.Max(anchor.Width,
            titleWidth + ControlDrawing.ScaleLogical(anchor, 92)));
        MinimumSize = Size.Empty;
        MaximumSize = new Size(workingArea.Width, workingArea.Height);
        int contentWidth = width - Padding.Horizontal;
        int height = Padding.Vertical;
        foreach (ToolStripItem item in Items)
        {
            item.AutoSize = false;
            item.Margin = Padding.Empty;
            if (item is FollowingOptionItem choice)
            {
                int textWidth = Math.Max(1, contentWidth - ControlDrawing.ScaleLogical(anchor, 76));
                int titleHeight = TextRenderer.MeasureText(choice.Text, Font).Height;
                int descriptionHeight = string.IsNullOrEmpty(choice.Description) ? 0 :
                    TextRenderer.MeasureText(choice.Description, _descriptionFont, new Size(textWidth, int.MaxValue),
                        TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + ControlDrawing.ScaleLogical(anchor, 3);
                item.Size = new Size(contentWidth, Math.Max(ControlDrawing.ScaleLogical(anchor, 44),
                    titleHeight + descriptionHeight + ControlDrawing.ScaleLogical(anchor, 20)));
            }
            else item.Size = new Size(contentWidth, ControlDrawing.ScaleLogical(anchor, 13));
            height += item.Height;
        }
        Size = new Size(width, Math.Min(height, workingArea.Height));
        PerformLayout();
    }

    internal FollowingItemLayout GetItemLayout(FollowingOptionItem item)
    {
        int S(int value) => ControlDrawing.ScaleLogical(this, value);
        int left = S(40);
        int width = Math.Max(1, item.Width - left - S(36));
        int titleHeight = TextRenderer.MeasureText(item.Text, Font).Height;
        int descriptionHeight = string.IsNullOrEmpty(item.Description) ? 0 :
            TextRenderer.MeasureText(item.Description, _descriptionFont, new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
        int gap = descriptionHeight == 0 ? 0 : S(3);
        int top = Math.Max(S(8), (item.Height - titleHeight - descriptionHeight - gap) / 2);
        return new FollowingItemLayout(
            new Rectangle(left, top, width, titleHeight),
            new Rectangle(left, top + titleHeight + gap, width, descriptionHeight),
            new Rectangle(S(11), (item.Height - S(19)) / 2, S(19), S(19)),
            new Rectangle(item.Width - S(27), (item.Height - S(16)) / 2, S(16), S(16)));
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (Width < 2 || Height < 2) return;
        using GraphicsPath path = ControlDrawing.RoundedRect(ClientRectangle, ControlDrawing.ScaleLogical(this, 12));
        Region? old = Region;
        Region = new Region(path);
        old?.Dispose();
    }

    protected override bool ProcessCmdKey(ref Message m, Keys keyData)
    {
        if (keyData is Keys.Left or Keys.Escape)
        {
            Close(ToolStripDropDownCloseReason.Keyboard);
            return true;
        }
        if (keyData == Keys.Space && Items.OfType<FollowingOptionItem>().FirstOrDefault(item => item.Selected) is { } selected)
        {
            selected.PerformClick();
            return true;
        }
        return base.ProcessCmdKey(ref m, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _titleFont.Dispose();
            _descriptionFont.Dispose();
        }
    }

    private sealed class FollowingMenuRenderer(FollowingOptionsMenu menu, ThemePalette palette) : ToolStripRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.Clear(palette.MenuBackground);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using GraphicsPath path = ControlDrawing.RoundedRect(new Rectangle(0, 0, menu.Width - 1, menu.Height - 1),
                ControlDrawing.ScaleLogical(menu, 12));
            using Pen pen = new(AccessibilityPreferences.HighContrast ? SystemColors.WindowText :
                ControlContrast.FieldBorder(palette));
            e.Graphics.DrawPath(pen, path);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item is not FollowingOptionItem item) return;
            bool highContrast = AccessibilityPreferences.HighContrast;
            bool highlighted = item.Selected || item.Checked;
            Color text = highContrast && highlighted ? SystemColors.HighlightText : palette.Text;
            Color secondary = highContrast && highlighted ? text : palette.SecondaryText;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle surface = new(0, 1, item.Width - 1, item.Height - 3);
            using GraphicsPath path = ControlDrawing.RoundedRect(surface, ControlDrawing.ScaleLogical(menu, 8));
            if (highlighted)
            {
                Color fill = highContrast ? SystemColors.Highlight : item.Selected && !item.Checked
                    ? ControlContrast.FieldHover(palette) : ControlDrawing.Blend(palette.MenuBackground, palette.Accent, 28);
                using SolidBrush brush = new(fill);
                e.Graphics.FillPath(brush, path);
            }
            // Hover/focus and the saved mode remain separate: the check always marks the saved mode.
            if (item.Selected)
            {
                using Pen pen = new(highContrast ? SystemColors.HighlightText :
                    ControlDrawing.Blend(palette.MenuBackground, item.Checked ? palette.Accent : palette.Text, 100));
                e.Graphics.DrawPath(pen, path);
            }
            FollowingItemLayout layout = menu.GetItemLayout(item);
            TextRenderer.DrawText(e.Graphics, item.Text, menu.Font, layout.Title, text,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
            if (layout.Description.Height > 0)
                TextRenderer.DrawText(e.Graphics, item.Description, menu.DescriptionFont, layout.Description, secondary,
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            if (item.IconPath != null)
                FluentTrayIcons.Draw(e.Graphics, item.IconPath, layout.Icon, secondary);
            else if (item.IsResumeAction)
            {
                Rectangle r = layout.Icon;
                using SolidBrush brush = new(secondary);
                e.Graphics.FillPolygon(brush, [new Point(r.Left + r.Width / 4, r.Top + r.Height / 6),
                    new Point(r.Right - r.Width / 5, r.Top + r.Height / 2), new Point(r.Left + r.Width / 4, r.Bottom - r.Height / 6)]);
            }
            else
            {
                // Pause/resume is an action, separate from the three mutually exclusive modes.
                using Pen pen = new(secondary, Math.Max(2, layout.Icon.Width / 8f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                int x = layout.Icon.Left + layout.Icon.Width / 3;
                e.Graphics.DrawLine(pen, x, layout.Icon.Top + 3, x, layout.Icon.Bottom - 3);
                x = layout.Icon.Right - layout.Icon.Width / 3;
                e.Graphics.DrawLine(pen, x, layout.Icon.Top + 3, x, layout.Icon.Bottom - 3);
            }
            if (item.Checked)
            {
                Rectangle r = layout.Selection;
                using Pen pen = new(highContrast ? text : palette.Accent, Math.Max(2, r.Width / 8f))
                { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                e.Graphics.DrawLines(pen, [new Point(r.Left + r.Width / 6, r.Top + r.Height / 2),
                    new Point(r.Left + r.Width * 2 / 5, r.Bottom - r.Height / 4), new Point(r.Right - r.Width / 8, r.Top + r.Height / 4)]);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) { }
        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e) { }
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int inset = ControlDrawing.ScaleLogical(menu, 10);
            using Pen pen = new(palette.Border);
            e.Graphics.DrawLine(pen, inset, e.Item.Height / 2, e.Item.Width - inset, e.Item.Height / 2);
        }
    }
}

internal sealed record FollowingItemLayout(Rectangle Title, Rectangle Description, Rectangle Icon, Rectangle Selection);

internal sealed class FollowingOptionItem : ToolStripMenuItem
{
    internal FollowingOptionItem(string title, string description, TrayFluentIcon? icon = null) : base(title)
    {
        Description = description;
        AccessibleDescription = description;
        IconPath = icon.HasValue ? FluentTrayIcons.Create(icon.Value) : null;
    }

    internal string Description { get; }
    internal bool IsResumeAction { get; set; }
    internal GraphicsPath? IconPath { get; }

    protected override void SetBounds(Rectangle rect)
    {
        // ToolStripMenuItem subtracts the owner's left padding for native check
        // gutters. Our icons are inside the card, so retain the surface inset.
        if (Owner is FollowingOptionsMenu menu) rect.X += menu.Padding.Left;
        base.SetBounds(rect);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) IconPath?.Dispose();
        base.Dispose(disposing);
    }
}
