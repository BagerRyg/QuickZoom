using System.Drawing;
using System.Windows.Forms;

namespace QuickZoom;

internal sealed partial class TrayPopupWindow
{
    private readonly Dictionary<Control, ContentMetrics> _contentMetrics = new();
    private readonly Dictionary<Control, Font> _fittedFonts = new();
    private Padding? _normalSurfacePadding;
    private int _surfaceDpi;

    internal float ContentScale { get; private set; } = 1f;

    internal void PrepareContentUpdate()
    {
        if (_contentMetrics.Count > 0)
            FitContentToWorkingArea(new Size(int.MaxValue / 2, int.MaxValue / 2));
    }

    // Keep absolute, uncompressed metrics: refreshes and display-picker changes
    // must not make the popup progressively smaller or alter settings text size.
    private sealed record ContentMetrics(Size Size, Size Minimum, Size Maximum,
        Padding Margin, Padding Padding, Font Font, int Dpi);

    private Size FitContentToWorkingArea(Size maximum)
    {
        Control[] controls = Descendants(ContentHost).Prepend(ContentHost).ToArray();
        if (_normalSurfacePadding == null)
        {
            _normalSurfacePadding = _surface.Padding;
            _surfaceDpi = DeviceDpi;
        }
        foreach (Control control in controls)
        {
            if (!_contentMetrics.ContainsKey(control))
            {
                _contentMetrics.Add(control, new ContentMetrics(control.Size, control.MinimumSize,
                    control.MaximumSize, control.Margin, control.Padding, control.Font, control.DeviceDpi));
            }
        }

        Size content = ApplyFit(1f, 1f);
        if (Fits(content)) return content;

        // Remove spare vertical space before reducing text and icons together.
        foreach (float density in new[] { 0.85f, 0.7f, 0.55f })
        {
            content = ApplyFit(1f, density);
            if (Fits(content)) return content;
        }

        float scale = 1f;
        for (int attempt = 0; attempt < 12 && !Fits(content); attempt++)
        {
            int availableWidth = Math.Max(1, maximum.Width - _surface.Padding.Horizontal - Padding.Horizontal);
            int availableHeight = Math.Max(1, maximum.Height - _surface.Padding.Vertical - Padding.Vertical);
            scale *= Math.Min(0.97f, Math.Min(availableWidth / (float)Math.Max(1, content.Width),
                availableHeight / (float)Math.Max(1, content.Height)));
            content = ApplyFit(Math.Max(0.1f, scale), 0.55f);
        }
        return content;

        bool Fits(Size size) => size.Width + _surface.Padding.Horizontal + Padding.Horizontal <= maximum.Width &&
            size.Height + _surface.Padding.Vertical + Padding.Vertical <= maximum.Height;

        Size ApplyFit(float fitScale, float density)
        {
            ContentScale = fitScale;
            foreach (Control control in controls) control.SuspendLayout();
            try
            {
                _surface.Padding = ScalePadding(_normalSurfacePadding.Value,
                    fitScale * DeviceDpi / _surfaceDpi, density);
                foreach (Control control in controls)
                {
                    ContentMetrics metrics = _contentMetrics[control];
                    float geometryScale = fitScale * control.DeviceDpi / metrics.Dpi;
                    control.MinimumSize = Size.Empty;
                    control.MaximumSize = Size.Empty;
                    control.Margin = ScalePadding(metrics.Margin, geometryScale, density);
                    control.Padding = ScalePadding(metrics.Padding, geometryScale, density);
                    if (control is Label)
                    {
                        Font font = fitScale == 1f ? metrics.Font : new Font(metrics.Font.FontFamily,
                            metrics.Font.SizeInPoints * fitScale, metrics.Font.Style);
                        control.Font = font;
                        if (_fittedFonts.Remove(control, out Font? previous)) previous.Dispose();
                        if (fitScale != 1f) _fittedFonts.Add(control, font);
                    }
                    control.Size = ScaleSize(metrics.Size, geometryScale);
                    control.MinimumSize = ScaleSize(metrics.Minimum, geometryScale);
                    control.MaximumSize = ScaleSize(metrics.Maximum, geometryScale);
                    if (control is TrayMenuRow)
                    {
                        int textHeight = control.Controls.OfType<Label>().Select(label => label.Font.Height).DefaultIfEmpty(0).Max();
                        int minimumHeight = textHeight + ControlDrawing.ScaleLogical(control, 12);
                        control.Height = Math.Max(minimumHeight, (int)Math.Round(metrics.Size.Height * geometryScale * density));
                        foreach (ToggleSwitchControl toggle in control.Controls.OfType<ToggleSwitchControl>())
                            toggle.Height = control.Height;
                    }
                    else if (control is TrayMenuDivider)
                    {
                        control.Height = Math.Max(2, (int)Math.Round(metrics.Size.Height * geometryScale * density));
                    }
                    else if (control is TrayModeButton button)
                    {
                        button.ApplyTheme(_palette);
                    }
                }
                // Toggle controls follow their row's fitted height even after
                // their deferred DPI initialization has run.
                foreach (ToggleSwitchControl toggle in controls.OfType<ToggleSwitchControl>())
                    if (toggle.Parent is TrayMenuRow row) toggle.Height = row.Height;
            }
            finally
            {
                for (int index = controls.Length - 1; index >= 0; index--)
                    controls[index].ResumeLayout(performLayout: true);
            }
            ContentHost.PerformLayout();
            return MeasureContentHost(ContentHost.Width);
        }
    }

    private static Size ScaleSize(Size size, float scale) => new(
        size.Width == 0 ? 0 : Math.Max(1, (int)Math.Round(size.Width * scale)),
        size.Height == 0 ? 0 : Math.Max(1, (int)Math.Round(size.Height * scale)));

    private static Padding ScalePadding(Padding padding, float scale, float density) => new(
        (int)Math.Round(padding.Left * scale), (int)Math.Round(padding.Top * scale * density),
        (int)Math.Round(padding.Right * scale), (int)Math.Round(padding.Bottom * scale * density));

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (Control descendant in Descendants(child)) yield return descendant;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        foreach (Font font in _fittedFonts.Values) font.Dispose();
        _fittedFonts.Clear();
        _contentMetrics.Clear();
    }
}
