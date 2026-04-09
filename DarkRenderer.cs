using System.Drawing;
using System.Windows.Forms;

namespace QuickZoom;

internal readonly struct ThemePalette
{
    public required Color MenuBackground { get; init; }
    public required Color HoverBackground { get; init; }
    public required Color Text { get; init; }
    public required Color DisabledText { get; init; }
    public required Color Separator { get; init; }
    public required Color ControlBackground { get; init; }
    public required Color ButtonBackground { get; init; }
    public required Color Border { get; init; }
    public required Color ButtonHover { get; init; }
    public required Color ButtonPressed { get; init; }
}

internal static class ThemePalettes
{
    public static ThemePalette Dark => new()
    {
        MenuBackground = Color.FromArgb(28, 28, 28),
        HoverBackground = Color.FromArgb(48, 48, 48),
        Text = Color.FromArgb(235, 235, 235),
        DisabledText = Color.Gray,
        Separator = Color.FromArgb(70, 70, 70),
        ControlBackground = Color.FromArgb(40, 40, 40),
        ButtonBackground = Color.FromArgb(48, 48, 48),
        Border = Color.FromArgb(70, 70, 70),
        ButtonHover = Color.FromArgb(60, 60, 60),
        ButtonPressed = Color.FromArgb(72, 72, 72)
    };

    public static ThemePalette Light => new()
    {
        MenuBackground = Color.FromArgb(248, 248, 248),
        HoverBackground = Color.FromArgb(230, 230, 230),
        Text = Color.FromArgb(24, 24, 24),
        DisabledText = Color.FromArgb(130, 130, 130),
        Separator = Color.FromArgb(210, 210, 210),
        ControlBackground = Color.FromArgb(255, 255, 255),
        ButtonBackground = Color.FromArgb(242, 242, 242),
        Border = Color.FromArgb(200, 200, 200),
        ButtonHover = Color.FromArgb(232, 232, 232),
        ButtonPressed = Color.FromArgb(220, 220, 220)
    };
}

internal sealed class TrayMenuRenderer(ThemePalette palette) : ToolStripRenderer
{
    private readonly ThemePalette _palette = palette;

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        e.Graphics.Clear(_palette.MenuBackground);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? _palette.Text : _palette.DisabledText;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item.Selected)
        {
            using var br = new SolidBrush(_palette.HoverBackground);
            e.Graphics.FillRectangle(br, new Rectangle(3, 2, e.Item.Width - 6, e.Item.Height - 4));
        }
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        using var p = new Pen(_palette.Separator);
        e.Graphics.DrawLine(p, 12, e.Item.Height / 2, e.Item.Width - 12, e.Item.Height / 2);
    }
}
