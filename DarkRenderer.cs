using System.Drawing;
using System.Windows.Forms;

namespace QuickZoom;

internal readonly struct ThemePalette
{
    public required Color MenuBackground { get; init; }
    public required Color HoverBackground { get; init; }
    public required Color Text { get; init; }
    public required Color SecondaryText { get; init; }
    public required Color DisabledText { get; init; }
    public required Color Separator { get; init; }
    public required Color ControlBackground { get; init; }
    public required Color ButtonBackground { get; init; }
    public required Color Border { get; init; }
    public required Color ButtonHover { get; init; }
    public required Color ButtonPressed { get; init; }
    public required Color Accent { get; init; }
    public required Color AccentHover { get; init; }
    public required Color AccentPressed { get; init; }
}

internal static class ThemePalettes
{
    public static ThemePalette Dark => new()
    {
        MenuBackground = Color.FromArgb(14, 16, 20),
        HoverBackground = Color.FromArgb(35, 42, 53),
        Text = Color.FromArgb(244, 247, 251),
        SecondaryText = Color.FromArgb(170, 180, 195),
        DisabledText = Color.FromArgb(125, 135, 150),
        Separator = Color.FromArgb(42, 48, 58),
        ControlBackground = Color.FromArgb(24, 28, 35),
        ButtonBackground = Color.FromArgb(27, 32, 40),
        Border = Color.FromArgb(42, 48, 58),
        ButtonHover = Color.FromArgb(35, 42, 53),
        ButtonPressed = Color.FromArgb(46, 54, 66),
        Accent = Color.FromArgb(34, 197, 94),
        AccentHover = Color.FromArgb(45, 212, 106),
        AccentPressed = Color.FromArgb(28, 176, 84)
    };

    public static ThemePalette Light => new()
    {
        MenuBackground = Color.FromArgb(239, 243, 247),
        HoverBackground = Color.FromArgb(218, 226, 236),
        Text = Color.FromArgb(17, 24, 39),
        SecondaryText = Color.FromArgb(70, 82, 97),
        DisabledText = Color.FromArgb(132, 140, 148),
        Separator = Color.FromArgb(198, 207, 217),
        ControlBackground = Color.FromArgb(255, 255, 255),
        ButtonBackground = Color.FromArgb(247, 249, 252),
        Border = Color.FromArgb(190, 201, 213),
        ButtonHover = Color.FromArgb(226, 233, 242),
        ButtonPressed = Color.FromArgb(210, 221, 234),
        Accent = Color.FromArgb(72, 163, 96),
        AccentHover = Color.FromArgb(83, 175, 107),
        AccentPressed = Color.FromArgb(62, 145, 86)
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
