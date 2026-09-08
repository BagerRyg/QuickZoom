using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickZoom;

internal static class ControlDrawing
{
    private static readonly float WindowsTextScale = AccessibilityPreferences.WindowsTextScale;
    private static float _userUiFontScale = 1f;

    internal static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int maximumRadius = Math.Max(1, Math.Min(bounds.Width, bounds.Height) / 2);
        int safeRadius = Math.Clamp(radius, 1, maximumRadius);
        int diameter = safeRadius * 2;
        GraphicsPath path = new();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    internal static int ScaleLogical(Control control, int logicalPixels)
    {
        int dpi = 96;
        try
        {
            dpi = control.DeviceDpi;
        }
        catch
        {
            // Fall back to 100% scale if the control is not ready yet.
        }

        return Math.Max(1, (int)Math.Round(logicalPixels * (Math.Max(1, dpi) / 96f)));
    }

    internal static bool FollowWindowsTextScale { get; set; } = true;

    internal static float UiFontScale
    {
        get => _userUiFontScale * (FollowWindowsTextScale ? WindowsTextScale : 1f);
        set => _userUiFontScale = Math.Clamp(value, 0.75f, 2.5f);
    }

    internal static Font UiFont(string familyName, float emSize, FontStyle style)
    {
        return new Font(familyName, Math.Max(7f, emSize * UiFontScale), style);
    }

    internal static Color FocusColor(ThemePalette palette) => AccessibilityPreferences.HighContrast
        ? SystemColors.Highlight
        : palette.Text;

    internal static Control? FocusCaptureTarget { get; set; }

    internal static bool ShouldDrawFocus(Control control, bool showFocusCues) =>
        ReferenceEquals(FocusCaptureTarget, control) ||
        (control.Focused && (showFocusCues || AccessibilityPreferences.HighContrast));

    internal static Color ContrastText(Color background)
    {
        if (AccessibilityPreferences.HighContrast)
        {
            return SystemColors.HighlightText;
        }

        static double Channel(byte value)
        {
            double normalized = value / 255d;
            return normalized <= 0.04045d
                ? normalized / 12.92d
                : Math.Pow((normalized + 0.055d) / 1.055d, 2.4d);
        }

        double luminance = (0.2126d * Channel(background.R)) +
            (0.7152d * Channel(background.G)) +
            (0.0722d * Channel(background.B));
        double darkContrast = (luminance + 0.05d) / 0.05d;
        double lightContrast = 1.05d / (luminance + 0.05d);
        return darkContrast >= lightContrast
            ? Color.FromArgb(10, 15, 22)
            : Color.White;
    }

    internal static Color Blend(Color background, Color foreground, int foregroundAlpha)
    {
        int alpha = Math.Clamp(foregroundAlpha, 0, 255);
        int inverse = 255 - alpha;
        return Color.FromArgb(
            255,
            ((foreground.R * alpha) + (background.R * inverse) + 127) / 255,
            ((foreground.G * alpha) + (background.G * inverse) + 127) / 255,
            ((foreground.B * alpha) + (background.B * inverse) + 127) / 255);
    }

    internal static void DrawFocusRing(Graphics graphics, Rectangle bounds, int radius, ThemePalette palette)
    {
        if (bounds.Width <= 2 || bounds.Height <= 2)
        {
            return;
        }

        using GraphicsPath focusPath = RoundedRect(bounds, Math.Max(2, radius));
        using Pen outerPen = new(
            AccessibilityPreferences.HighContrast ? SystemColors.WindowText : Color.FromArgb(220, FocusColor(palette)),
            AccessibilityPreferences.HighContrast ? 3f : 2f);
        graphics.DrawPath(outerPen, focusPath);
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
    private const int DwmwaCloak = 13;
    private const int WmSetRedraw = 0x000B;
    private const uint RdwInvalidate = 0x0001;
    private const uint RdwErase = 0x0004;
    private const uint RdwAllChildren = 0x0080;
    private const uint RdwUpdateNow = 0x0100;
    private const uint RdwFrame = 0x0400;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmFlush();

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? pszSubAppName, string? pszSubIdList);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RedrawWindow(IntPtr hwnd, IntPtr updateRect, IntPtr updateRegion, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    internal static void TrySetDarkTitleBar(Form form, bool enabled)
    {
        try
        {
            enabled &= !AccessibilityPreferences.HighContrast;
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

    internal static void TrySetDarkScrollBars(Control control, bool enabled)
    {
        try
        {
            enabled &= !AccessibilityPreferences.HighContrast;
            void Apply()
            {
                if (control.Handle == IntPtr.Zero)
                {
                    return;
                }

                _ = SetWindowTheme(control.Handle, enabled ? "DarkMode_Explorer" : null, null);
            }

            if (control.IsHandleCreated)
            {
                Apply();
            }
            else
            {
                control.HandleCreated += (_, _) => Apply();
            }
        }
        catch
        {
            // Best effort.
        }
    }

    internal static bool TrySetCloaked(Form form, bool cloaked)
    {
        try
        {
            int value = cloaked ? 1 : 0;
            return DwmSetWindowAttribute(form.Handle, DwmwaCloak, ref value, sizeof(int)) == 0;
        }
        catch
        {
            return false;
        }
    }

    internal static void TryFlushComposition()
    {
        try
        {
            _ = DwmFlush();
        }
        catch
        {
            // Best effort.
        }
    }

    internal static void RedrawNow(Control control)
    {
        if (!control.IsHandleCreated || control.IsDisposed)
        {
            return;
        }

        _ = RedrawWindow(
            control.Handle,
            IntPtr.Zero,
            IntPtr.Zero,
            RdwInvalidate | RdwErase | RdwAllChildren | RdwUpdateNow | RdwFrame);
    }

    internal static bool TrySetRedraw(Control control, bool enabled)
    {
        if (!control.IsHandleCreated || control.IsDisposed)
        {
            return false;
        }

        _ = SendMessage(control.Handle, WmSetRedraw, enabled ? new IntPtr(1) : IntPtr.Zero, IntPtr.Zero);
        return true;
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
    internal static Color FieldBackground(ThemePalette palette) => AccessibilityPreferences.HighContrast
        ? SystemColors.Window
        : palette.MenuBackground.GetBrightness() < 0.5f
        ? palette.ButtonBackground
        : Color.FromArgb(232, 238, 246);

    internal static Color FieldHover(ThemePalette palette) => AccessibilityPreferences.HighContrast
        ? SystemColors.Highlight
        : palette.MenuBackground.GetBrightness() < 0.5f
        ? Color.FromArgb(38, 46, 58)
        : Color.FromArgb(220, 230, 242);

    internal static Color FieldPressed(ThemePalette palette) => AccessibilityPreferences.HighContrast
        ? SystemColors.HotTrack
        : palette.MenuBackground.GetBrightness() < 0.5f
        ? Color.FromArgb(45, 54, 68)
        : Color.FromArgb(208, 222, 238);

    internal static Color FieldBorder(ThemePalette palette) => AccessibilityPreferences.HighContrast
        ? SystemColors.WindowText
        : palette.MenuBackground.GetBrightness() < 0.5f
        ? Color.FromArgb(82, 94, 112)
        : Color.FromArgb(142, 156, 174);

    internal static Color SubtleTrack(ThemePalette palette) => AccessibilityPreferences.HighContrast
        ? SystemColors.ControlDark
        : palette.MenuBackground.GetBrightness() < 0.5f
        ? Color.FromArgb(28, 33, 42)
        : Color.FromArgb(218, 227, 238);
}

internal class ModernSurfacePanel : Panel
{
    private int _cornerRadius = 9;
    private int _borderAlpha = 26;
    private bool _searchTargetHighlighted;

    public ModernSurfacePanel()
    {
        DoubleBuffered = true;
        Resize += (_, _) => Invalidate();
    }

    public int CornerRadius
    {
        get => _cornerRadius;
        set
        {
            _cornerRadius = Math.Max(3, value);
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

    public bool SearchTargetHighlighted
    {
        get => _searchTargetHighlighted;
        set
        {
            if (_searchTargetHighlighted == value)
            {
                return;
            }

            _searchTargetHighlighted = value;
            Invalidate();
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using SolidBrush outerBrush = new(ControlDrawing.EffectiveBackColor(this));
        e.Graphics.FillRectangle(outerBrush, ClientRectangle);
        if (Width <= 1 || Height <= 1)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        int scaledRadius = ControlDrawing.ScaleLogical(this, _cornerRadius);
        using GraphicsPath surfacePath = ControlDrawing.RoundedRect(
            new Rectangle(0, 0, Width - 1, Height - 1),
            scaledRadius);
        using SolidBrush surfaceBrush = new(BackColor);
        e.Graphics.FillPath(surfaceBrush, surfacePath);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width <= 4 || Height <= 4)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        int inset = ControlDrawing.ScaleLogical(this, 1);
        int scaledRadius = ControlDrawing.ScaleLogical(this, _cornerRadius);
        Rectangle borderBounds = new(
            inset,
            inset,
            Math.Max(1, Width - (inset * 2) - 1),
            Math.Max(1, Height - (inset * 2) - 1));
        using GraphicsPath path = ControlDrawing.RoundedRect(borderBounds, Math.Max(inset * 2, scaledRadius - inset));
        Color borderBase = AccessibilityPreferences.HighContrast
            ? SystemColors.WindowText
            : BackColor.GetBrightness() > 0.72f
            ? Color.FromArgb(112, 124, 139)
            : Color.White;
        Color borderColor = AccessibilityPreferences.HighContrast
            ? borderBase
            : ControlDrawing.Blend(BackColor, borderBase, _borderAlpha);
        using Pen borderPen = new(borderColor, Math.Max(1f, DeviceDpi / 96f));
        e.Graphics.DrawPath(borderPen, path);

        if (_searchTargetHighlighted)
        {
            Rectangle highlightBounds = Rectangle.Inflate(ClientRectangle, -1, -1);
            highlightBounds.Width = Math.Max(1, highlightBounds.Width - 1);
            highlightBounds.Height = Math.Max(1, highlightBounds.Height - 1);
            using GraphicsPath highlightPath = ControlDrawing.RoundedRect(
                highlightBounds,
                Math.Max(inset * 2, scaledRadius - inset));
            using Pen highlightPen = new(Color.FromArgb(235, Color.White), 1f);
            e.Graphics.DrawPath(highlightPen, highlightPath);
        }
    }
}

internal sealed class ToggleSwitchControl : Control
{
    private const int AnimationDurationMs = 160;
    private bool _isOn;
    private ThemePalette _palette;
    private bool _hovered;
    private bool _pressed;
    private bool _showStateText;
    private bool _useWarningActiveColor;
    private string _onText = string.Empty;
    private string _offText = string.Empty;
    private System.Windows.Forms.Timer? _animationTimer;
    private float _animationProgress;
    private float _animationStartProgress;
    private float _animationTargetProgress;
    private long _animationStartTime;

    public ToggleSwitchControl(ThemePalette palette)
    {
        _palette = palette;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint |
            ControlStyles.Selectable,
            true);
        Size = new Size(60, 44);
        Cursor = Cursors.Hand;
        TabStop = true;
        BackColor = palette.ControlBackground;
        AccessibleRole = AccessibleRole.CheckButton;
    }

    public string OnText
    {
        get => _onText;
        set
        {
            _onText = value ?? string.Empty;
            if (_showStateText)
            {
                ApplyPreferredSize();
            }

            Invalidate();
        }
    }

    public string OffText
    {
        get => _offText;
        set
        {
            _offText = value ?? string.Empty;
            if (_showStateText)
            {
                ApplyPreferredSize();
            }

            Invalidate();
        }
    }

    public bool ShowStateText
    {
        get => _showStateText;
        set
        {
            if (_showStateText == value)
            {
                return;
            }

            _showStateText = value;
            ApplyPreferredSize();
            Invalidate();
        }
    }

    public bool UseWarningActiveColor
    {
        get => _useWarningActiveColor;
        set
        {
            if (_useWarningActiveColor == value)
            {
                return;
            }

            _useWarningActiveColor = value;
            Invalidate();
        }
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
            SetAnimationTarget(value ? 1f : 0f);
            AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
        }
    }

    internal void SetStateForCapture(bool isOn)
    {
        _animationTimer?.Stop();
        _isOn = isOn;
        _animationProgress = isOn ? 1f : 0f;
        _animationStartProgress = _animationProgress;
        _animationTargetProgress = _animationProgress;
        Invalidate();
    }

    public void PerformToggle()
    {
        if (Enabled)
        {
            OnClick(EventArgs.Empty);
        }
    }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        BackColor = palette.ControlBackground;
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

        Focus();
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
            PerformToggle();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        int trackHeight = Math.Min(ControlDrawing.ScaleLogical(this, 28), Math.Max(12, Height - ControlDrawing.ScaleLogical(this, 12)));
        int horizontalInset = ControlDrawing.ScaleLogical(this, 2);
        Rectangle trackRect = new(
            horizontalInset,
            (Height - trackHeight) / 2,
            Math.Max(8, Width - (horizontalInset * 2) - 1),
            trackHeight);
        using GraphicsPath trackPath = ControlDrawing.RoundedRect(trackRect, trackRect.Height / 2);

        Color offTrackColor = _hovered
            ? ControlContrast.FieldHover(_palette)
            : ControlContrast.FieldBackground(_palette);
        bool lightPalette = _palette.MenuBackground.GetBrightness() > 0.65f;
        Color warningNormal = lightPalette ? Color.FromArgb(234, 88, 12) : Color.FromArgb(249, 115, 22);
        Color warningHover = lightPalette ? Color.FromArgb(249, 115, 22) : Color.FromArgb(251, 146, 60);
        Color warningPressed = lightPalette ? Color.FromArgb(194, 65, 12) : Color.FromArgb(234, 88, 12);
        Color onTrackColor = _useWarningActiveColor
            ? (_hovered ? warningHover : warningNormal)
            : (_hovered ? _palette.AccentHover : _palette.Accent);

        if (Enabled && _pressed)
        {
            offTrackColor = ControlContrast.FieldPressed(_palette);
            onTrackColor = _useWarningActiveColor ? warningPressed : _palette.AccentPressed;
        }

        Color trackColor = Enabled
            ? BlendColor(offTrackColor, onTrackColor, _animationProgress)
            : ControlContrast.FieldBackground(_palette);
        Rectangle shadowRect = trackRect;
        shadowRect.Offset(0, ControlDrawing.ScaleLogical(this, 1));
        using GraphicsPath shadowPath = ControlDrawing.RoundedRect(shadowRect, shadowRect.Height / 2);
        using SolidBrush shadowBrush = new(Color.FromArgb(_palette.MenuBackground.GetBrightness() < 0.5f ? 72 : 38, Color.Black));
        e.Graphics.FillPath(shadowBrush, shadowPath);

        using SolidBrush trackBrush = new(trackColor);
        using Pen borderPen = new(ControlContrast.FieldBorder(_palette), AccessibilityPreferences.HighContrast ? 2f : 1.25f);
        e.Graphics.FillPath(trackBrush, trackPath);
        e.Graphics.DrawPath(borderPen, trackPath);

        if (!AccessibilityPreferences.HighContrast)
        {
            int highlightInset = Math.Max(4, trackRect.Height / 4);
            using Pen highlightPen = new(Color.FromArgb(_hovered ? 54 : 34, Color.White), 1f);
            e.Graphics.DrawLine(
                highlightPen,
                trackRect.Left + highlightInset,
                trackRect.Top + 2,
                trackRect.Right - highlightInset,
                trackRect.Top + 2);
        }

        int knobInset = ControlDrawing.ScaleLogical(this, 3);
        int knobSize = trackRect.Height - (knobInset * 2);
        int knobStartX = trackRect.Left + knobInset;
        int knobEndX = trackRect.Right - knobSize - knobInset;
        int knobX = knobStartX + (int)Math.Round((knobEndX - knobStartX) * _animationProgress);
        Rectangle knobRect = new(knobX, trackRect.Top + knobInset, knobSize, knobSize);

        if (_showStateText)
        {
            DrawStateText(e.Graphics, trackRect, knobRect, trackColor);
        }

        Color knobColor = AccessibilityPreferences.HighContrast
            ? (_isOn ? SystemColors.HighlightText : SystemColors.WindowText)
            : (Enabled ? Color.FromArgb(245, 247, 250) : Color.FromArgb(160, 164, 170));
        Color knobBorderColor = AccessibilityPreferences.HighContrast
            ? SystemColors.WindowText
            : (Enabled ? Color.FromArgb(48, 52, 56) : Color.FromArgb(96, 100, 106));
        Rectangle knobShadowRect = knobRect;
        knobShadowRect.Offset(0, ControlDrawing.ScaleLogical(this, 1));
        using SolidBrush knobShadowBrush = new(Color.FromArgb(68, Color.Black));
        e.Graphics.FillEllipse(knobShadowBrush, knobShadowRect);
        using LinearGradientBrush knobBrush = new(
            knobRect,
            knobColor,
            Enabled ? Color.FromArgb(224, 228, 234) : knobColor,
            LinearGradientMode.Vertical);
        using Pen knobBorder = new(knobBorderColor);
        e.Graphics.FillEllipse(knobBrush, knobRect);
        e.Graphics.DrawEllipse(knobBorder, knobRect);

        if (Enabled && HasKeyboardFocusVisual)
        {
            int focusGap = ControlDrawing.ScaleLogical(this, 2);
            Rectangle focusBounds = Rectangle.Inflate(trackRect, focusGap, focusGap);
            focusBounds.Intersect(Rectangle.Inflate(ClientRectangle, -1, -1));
            ControlDrawing.DrawFocusRing(e.Graphics, focusBounds, Math.Max(8, focusBounds.Height / 2), _palette);
        }
    }

    private bool HasKeyboardFocusVisual =>
        ControlDrawing.ShouldDrawFocus(this, ShowFocusCues) ||
        Parent is TrayMenuRow { ShowsToggleFocusVisual: true };

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        ApplyPreferredSize();
        if (IsHandleCreated)
        {
            BeginInvoke((MethodInvoker)(() =>
            {
                if (!IsDisposed)
                {
                    // Dynamic settings pages can be auto-scaled after their
                    // child handles are created. Reapply the logical target
                    // once so every toggle ends up at the same DPI size.
                    ApplyPreferredSize();
                }
            }));
        }
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ApplyPreferredSize();
    }

    private void ApplyPreferredSize()
    {
        int targetWidth = ControlDrawing.ScaleLogical(this, _showStateText ? 68 : 48);
        if (_showStateText)
        {
            using Font font = CreateStateFont();
            Size onSize = TextRenderer.MeasureText(_onText, font, Size.Empty, TextFormatFlags.NoPadding);
            Size offSize = TextRenderer.MeasureText(_offText, font, Size.Empty, TextFormatFlags.NoPadding);
            int textWidth = Math.Max(onSize.Width, offSize.Width);
            targetWidth = Math.Max(
                targetWidth,
                ControlDrawing.ScaleLogical(this, 28) + textWidth + ControlDrawing.ScaleLogical(this, 18));
        }

        Size = new Size(targetWidth, ControlDrawing.ScaleLogical(this, 44));
    }

    private void DrawStateText(Graphics graphics, Rectangle trackRect, Rectangle knobRect, Color trackColor)
    {
        string text = _isOn ? _onText : _offText;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }
        int outerPadding = ControlDrawing.ScaleLogical(this, 8);
        int knobGap = ControlDrawing.ScaleLogical(this, 4);
        Rectangle textBounds = _isOn
            ? Rectangle.FromLTRB(trackRect.Left + outerPadding, trackRect.Top, knobRect.Left - knobGap, trackRect.Bottom)
            : Rectangle.FromLTRB(knobRect.Right + knobGap, trackRect.Top, trackRect.Right - outerPadding, trackRect.Bottom);

        if (textBounds.Width < 14)
        {
            return;
        }

        Color textColor = Enabled
            ? (_isOn ? ControlDrawing.ContrastText(trackColor) : _palette.SecondaryText)
            : _palette.DisabledText;
        using Font font = CreateStateFont();
        TextRenderer.DrawText(
            graphics,
            text,
            font,
            textBounds,
            textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private static Font CreateStateFont() => ControlDrawing.UiFont("Segoe UI Semibold", 8f, FontStyle.Bold);

    private void SetAnimationTarget(float target)
    {
        _animationTargetProgress = Math.Clamp(target, 0f, 1f);
        if (!IsHandleCreated || !AccessibilityPreferences.AnimationsEnabled)
        {
            StopAnimation();
            _animationProgress = _animationTargetProgress;
            Invalidate();
            return;
        }

        _animationStartProgress = _animationProgress;
        _animationStartTime = Environment.TickCount64;
        _animationTimer ??= CreateAnimationTimer();
        _animationTimer.Start();
        Invalidate();
    }

    private System.Windows.Forms.Timer CreateAnimationTimer()
    {
        var timer = new System.Windows.Forms.Timer { Interval = 15 };
        timer.Tick += (_, _) => AdvanceAnimation();
        return timer;
    }

    private void AdvanceAnimation()
    {
        float elapsed = Math.Max(0, Environment.TickCount64 - _animationStartTime);
        float linearProgress = Math.Clamp(elapsed / AnimationDurationMs, 0f, 1f);
        float easedProgress = linearProgress * linearProgress * (3f - (2f * linearProgress));
        _animationProgress = _animationStartProgress + ((_animationTargetProgress - _animationStartProgress) * easedProgress);
        Invalidate();

        if (linearProgress >= 1f)
        {
            _animationProgress = _animationTargetProgress;
            StopAnimation();
        }
    }

    private void StopAnimation()
    {
        _animationTimer?.Stop();
    }

    private static Color BlendColor(Color from, Color to, float amount)
    {
        float normalized = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            (int)Math.Round(from.A + ((to.A - from.A) * normalized)),
            (int)Math.Round(from.R + ((to.R - from.R) * normalized)),
            (int)Math.Round(from.G + ((to.G - from.G) * normalized)),
            (int)Math.Round(from.B + ((to.B - from.B) * normalized)));
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animationTimer?.Stop();
            _animationTimer?.Dispose();
            _animationTimer = null;
        }

        base.Dispose(disposing);
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

    protected override AccessibleObject CreateAccessibilityInstance() => new ToggleAccessibleObject(this);

    private sealed class ToggleAccessibleObject(ToggleSwitchControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.CheckButton;

        public override AccessibleStates State => base.State |
            (owner.IsOn ? AccessibleStates.Checked : AccessibleStates.None) |
            (owner.Enabled ? AccessibleStates.Focusable : AccessibleStates.Unavailable);

        public override string? Value
        {
            get => owner.IsOn ? owner.OnText : owner.OffText;
            set { }
        }

        public override void DoDefaultAction()
        {
            if (owner.Enabled)
            {
                owner.OnClick(EventArgs.Empty);
            }
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
        CornerRadius = 9;
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
        Font = ControlDrawing.UiFont("Segoe UI Semibold", 8.75f, FontStyle.Bold);
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
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
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
        using Pen pen = new(Color.FromArgb(82, _palette.Border), 2f);
        e.Graphics.DrawLine(pen, 8, y, Width - 8, y);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        using SolidBrush brush = new(ControlDrawing.EffectiveBackColor(this));
        pevent.Graphics.FillRectangle(brush, ClientRectangle);
    }
}

internal sealed class TrayModeButton : Control, IChildSurfaceBackgroundRenderer
{
    private readonly FluentIconControl _iconControl;
    private readonly string _description;
    private readonly string _label;
    private Rectangle _descriptionBounds;
    private Rectangle _labelBounds;
    private Font? _descriptionFont;
    private Font? _labelFont;
    private ThemePalette _palette;
    private bool _hovered;
    private bool _pressed;
    private bool _selected;

    internal event EventHandler? NavigationExitRequested;

    public TrayModeButton(ThemePalette palette, TrayFluentIcon icon, string label, string? description = null)
    {
        _palette = palette;
        Icon = icon;
        _label = label;
        _description = description ?? string.Empty;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.UserPaint |
            ControlStyles.Selectable,
            true);

        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        TabStop = true;
        Margin = new Padding(0);
        AccessibleName = label;
        AccessibleDescription = _description;
        AccessibleRole = AccessibleRole.RadioButton;
        Padding = new Padding(0);

        _iconControl = new FluentIconControl(palette, icon)
        {
            BackColor = Color.Transparent
        };
        Controls.Add(_iconControl);
        foreach (Control control in Controls)
        {
            control.Click += (_, _) => OnClick(EventArgs.Empty);
            control.MouseEnter += (_, _) => SetState(hovered: true, pressed: _pressed);
            control.MouseLeave += (_, _) => SetState(hovered: false, pressed: false);
            control.MouseDown += (_, _) => SetState(hovered: true, pressed: true);
            control.MouseUp += (_, _) => SetState(hovered: true, pressed: false);
        }

        ApplyTheme(palette);
    }

    public TrayFluentIcon Icon { get; }

    public int VisualHeightLogical { get; set; }

    public bool HorizontalArrowNavigationOnly { get; set; }

    public bool Selected
    {
        get => _selected;
        set
        {
            TabStop = value;
            if (_selected == value)
            {
                return;
            }

            _selected = value;
            AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
            AccessibilityNotifyClients(AccessibleEvents.Selection, -1);
            Invalidate(true);
        }
    }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        _iconControl.ApplyTheme(palette);
        _labelFont?.Dispose();
        _labelFont = ModeButtonFont();
        _descriptionFont?.Dispose();
        _descriptionFont = string.IsNullOrEmpty(_description) ? null : ModeButtonDescriptionFont();
        Invalidate(true);
    }

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        MinimumSize = GetPreferredSize(Size.Empty);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        return GetPreferredSizeForOwner(this);
    }

    public Size GetPreferredSizeForOwner(Control scaleOwner)
    {
        using Font font = ModeButtonFont();
        Size textSize = TextRenderer.MeasureText(_label, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        int iconSize = ControlDrawing.ScaleLogical(scaleOwner, 18);
        int gap = ControlDrawing.ScaleLogical(scaleOwner, 6);
        int horizontalPadding = ControlDrawing.ScaleLogical(scaleOwner, 9);
        int verticalPadding = ControlDrawing.ScaleLogical(scaleOwner, 8);
        int titleWidth = iconSize + gap + textSize.Width;
        int contentHeight = Math.Max(iconSize, textSize.Height);
        int width = horizontalPadding + titleWidth + horizontalPadding;

        if (!string.IsNullOrEmpty(_description))
        {
            using Font descriptionFont = ModeButtonDescriptionFont();
            Size descriptionSize = TextRenderer.MeasureText(
                _description,
                descriptionFont,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            int descriptionGap = ControlDrawing.ScaleLogical(scaleOwner, 5);
            int preferredDescriptionWidth = Math.Min(
                descriptionSize.Width,
                ControlDrawing.ScaleLogical(scaleOwner, 170));
            width = horizontalPadding + Math.Max(titleWidth, preferredDescriptionWidth) + horizontalPadding;
            contentHeight += descriptionGap + descriptionSize.Height;
        }

        int minimumHeight = string.IsNullOrEmpty(_description) ? 44 : 64;
        int height = Math.Max(ControlDrawing.ScaleLogical(scaleOwner, minimumHeight), contentHeight + (verticalPadding * 2));
        return new Size(width, height);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        int iconSize = ControlDrawing.ScaleLogical(this, 18);
        int gap = ControlDrawing.ScaleLogical(this, 6);
        using Font font = ModeButtonFont();
        Size textSize = TextRenderer.MeasureText(_label, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        int totalWidth = iconSize + gap + textSize.Width;
        int sidePadding = ControlDrawing.ScaleLogical(this, 7);
        int startX = Math.Max(sidePadding, (Width - totalWidth) / 2);
        int titleHeight = Math.Max(iconSize, textSize.Height);

        if (string.IsNullOrEmpty(_description))
        {
            int iconY = (Height - iconSize) / 2;
            _iconControl.Bounds = new Rectangle(startX, iconY, iconSize, iconSize);
            _labelBounds = new Rectangle(
                _iconControl.Right + gap,
                0,
                Math.Max(1, Width - (_iconControl.Right + gap) - sidePadding),
                Height);
            _descriptionBounds = Rectangle.Empty;
            return;
        }

        int descriptionGap = ControlDrawing.ScaleLogical(this, 5);
        using Font descriptionFont = ModeButtonDescriptionFont();
        int descriptionWidth = Math.Max(1, Width - (sidePadding * 2));
        Size descriptionSize = TextRenderer.MeasureText(
            _description,
            descriptionFont,
            new Size(descriptionWidth, int.MaxValue),
            TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
        int totalHeight = titleHeight + descriptionGap + descriptionSize.Height;
        int titleY = Math.Max(sidePadding, (Height - totalHeight) / 2);
        int iconYWithDescription = titleY + ((titleHeight - iconSize) / 2);
        _iconControl.Bounds = new Rectangle(startX, iconYWithDescription, iconSize, iconSize);
        _labelBounds = new Rectangle(
            _iconControl.Right + gap,
            titleY,
            Math.Max(1, Width - (_iconControl.Right + gap) - sidePadding),
            titleHeight);
        _descriptionBounds = new Rectangle(
            sidePadding,
            titleY + titleHeight + descriptionGap,
            descriptionWidth,
            Math.Max(1, Height - (titleY + titleHeight + descriptionGap) - sidePadding));
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
        Focus();
        SetState(_hovered, pressed: true);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        SetState(_hovered, pressed: false);
    }

    protected override bool IsInputKey(Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        return key is Keys.Enter or Keys.Space or Keys.Left or Keys.Right or Keys.Home or Keys.End ||
            (!HorizontalArrowNavigationOnly && key is Keys.Up or Keys.Down) ||
            base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Left || (!HorizontalArrowNavigationOnly && e.KeyCode == Keys.Up))
        {
            if (e.KeyCode == Keys.Left && IsNavigationEdge(-1) && NavigationExitRequested != null)
            {
                NavigationExitRequested.Invoke(this, EventArgs.Empty);
            }
            else
            {
                NavigateToSibling(-1, selectEdge: false);
            }
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Right || (!HorizontalArrowNavigationOnly && e.KeyCode == Keys.Down))
        {
            NavigateToSibling(1, selectEdge: false);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Home)
        {
            NavigateToSibling(1, selectEdge: true);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.End)
        {
            NavigateToSibling(-1, selectEdge: true);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        base.OnKeyDown(e);
    }

    internal void NavigateForCapture(Keys key) => OnKeyDown(new KeyEventArgs(key));

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        using SolidBrush brush = new(ControlDrawing.EffectiveBackColor(this));
        pevent.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        PaintButtonSurface(e.Graphics);
        TextRenderer.DrawText(
            e.Graphics,
            _label,
            _labelFont ?? Font,
            _labelBounds,
            AccessibilityPreferences.HighContrast && (_selected || _hovered || _pressed)
                ? SystemColors.HighlightText
                : _palette.Text,
            TextFormatFlags.Left |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPadding |
            TextFormatFlags.NoPrefix);

        if (!string.IsNullOrEmpty(_description) && !_descriptionBounds.IsEmpty)
        {
            TextRenderer.DrawText(
                e.Graphics,
                _description,
                _descriptionFont ?? Font,
                _descriptionBounds,
                AccessibilityPreferences.HighContrast && (_selected || _hovered || _pressed)
                    ? SystemColors.HighlightText
                    : _palette.SecondaryText,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.Top |
                TextFormatFlags.WordBreak |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPadding |
                TextFormatFlags.NoPrefix);
        }
    }

    public void PaintChildSurfaceBackground(Graphics graphics, Rectangle childBounds)
    {
        using Region clip = graphics.Clip?.Clone() ?? new Region(childBounds);
        graphics.SetClip(childBounds);
        PaintButtonSurface(graphics);
        graphics.Clip = clip;
    }

    private void PaintButtonSurface(Graphics graphics)
    {
        using SolidBrush backgroundBrush = new(ControlDrawing.EffectiveBackColor(this));
        graphics.FillRectangle(backgroundBrush, ClientRectangle);

        bool highlighted = _selected || _hovered || _pressed;
        int visualHeight = VisualHeightLogical > 0
            ? Math.Min(Math.Max(8, Height - 2), ControlDrawing.ScaleLogical(this, VisualHeightLogical))
            : Math.Max(8, Height - 2);
        Rectangle fillRect = new(1, Math.Max(1, (Height - visualHeight) / 2), Math.Max(8, Width - 2), visualHeight);
        Color border = AccessibilityPreferences.HighContrast
            ? SystemColors.WindowText
            : (_selected ? _palette.Accent : _palette.Border);
        using GraphicsPath fillPath = ControlDrawing.RoundedRect(fillRect, ControlDrawing.ScaleLogical(this, 10));
        if (highlighted)
        {
            Color fill = AccessibilityPreferences.HighContrast
                ? SystemColors.Highlight
                : _selected
                ? Color.FromArgb(_pressed ? 62 : 46, _palette.Accent)
                : _pressed ? _palette.ButtonPressed : _palette.ButtonHover;
            using SolidBrush fillBrush = new(fill);
            graphics.FillPath(fillBrush, fillPath);
        }

        int borderAlpha = _selected ? 118 : highlighted ? 92 : 72;
        float borderWidth = Math.Max(1f, DeviceDpi / 96f);
        using Pen borderPen = new(
            AccessibilityPreferences.HighContrast ? border : Color.FromArgb(borderAlpha, border),
            borderWidth);
        graphics.DrawPath(borderPen, fillPath);

        if (ControlDrawing.ShouldDrawFocus(this, ShowFocusCues))
        {
            Rectangle focusBounds = Rectangle.Inflate(fillRect, -2, -2);
            ControlDrawing.DrawFocusRing(graphics, focusBounds, ControlDrawing.ScaleLogical(this, 9), _palette);
        }
    }

    protected override void OnClick(EventArgs e)
    {
        if (CanFocus)
        {
            Focus();
        }

        base.OnClick(e);
    }

    private void SetState(bool hovered, bool pressed)
    {
        _hovered = hovered;
        _pressed = pressed;
        Invalidate(true);
    }

    private void NavigateToSibling(int direction, bool selectEdge)
    {
        TrayModeButton[] items = GetVisualNavigationItems();
        if (items.Length == 0)
        {
            return;
        }

        int currentIndex = Array.IndexOf(items, this);
        if (currentIndex < 0)
        {
            return;
        }

        int nextIndex = selectEdge
            ? (direction > 0 ? 0 : items.Length - 1)
            : (currentIndex + direction + items.Length) % items.Length;
        TrayModeButton next = items[nextIndex];
        next.Select();
        next.Focus();
    }

    private bool IsNavigationEdge(int direction)
    {
        TrayModeButton[] items = GetVisualNavigationItems();
        int currentIndex = Array.IndexOf(items, this);
        return currentIndex < 0 || (direction < 0 ? currentIndex == 0 : currentIndex == items.Length - 1);
    }

    private TrayModeButton[] GetVisualNavigationItems()
    {
        return Parent?.Controls
            .OfType<TrayModeButton>()
            .Where(item => item.Visible && item.Enabled)
            .OrderBy(item => item.Left)
            .ThenBy(item => item.Top)
            .ToArray() ?? [];
    }

    private static Font ModeButtonFont() => ControlDrawing.UiFont("Segoe UI Semibold", 9f, FontStyle.Bold);

    private static Font ModeButtonDescriptionFont() => ControlDrawing.UiFont("Segoe UI", 7.8f, FontStyle.Regular);

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate(true);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate(true);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new ModeButtonAccessibleObject(this);

    private sealed class ModeButtonAccessibleObject(TrayModeButton owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.RadioButton;

        public override AccessibleStates State => base.State |
            AccessibleStates.Focusable |
            AccessibleStates.Selectable |
            (owner.Selected ? AccessibleStates.Checked | AccessibleStates.Selected : AccessibleStates.None) |
            (owner.Enabled ? AccessibleStates.None : AccessibleStates.Unavailable);

        public override void DoDefaultAction()
        {
            if (owner.Enabled)
            {
                owner.OnClick(EventArgs.Empty);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _labelFont?.Dispose();
            _descriptionFont?.Dispose();
        }

        base.Dispose(disposing);
    }

}

internal sealed class TrayMenuRow : Control, ISurfaceBackgroundProvider, IChildSurfaceBackgroundRenderer
{
    private readonly GraphicsPath? _iconPath;
    private readonly Label _titleLabel;
    private readonly Label? _rightLabel;
    private readonly ToggleSwitchControl? _toggle;
    private Rectangle _iconBounds;
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
        AccessibleName = title;
        AccessibleDescription = toggle?.AccessibleDescription ?? rightText ?? string.Empty;
        AccessibleRole = toggle != null ? AccessibleRole.CheckButton : AccessibleRole.PushButton;

        if (icon.HasValue)
        {
            _iconPath = FluentTrayIcons.Create(icon.Value);
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
            _toggle.TabStop = false;
            if (string.IsNullOrWhiteSpace(_toggle.AccessibleName))
            {
                _toggle.AccessibleName = title;
            }
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

        _titleLabel.Click += (_, _) => OnClick(EventArgs.Empty);
        if (_rightLabel != null)
        {
            _rightLabel.Click += (_, _) => OnClick(EventArgs.Empty);
        }

        if (_toggle != null)
        {
            _toggle.Click += (_, _) => OnClick(EventArgs.Empty);
        }

        Height = ControlDrawing.ScaleLogical(this, fontScaleAwareLogicalHeight());
        ApplyTheme(palette);
        ResumeLayout(performLayout: true);

        int fontScaleAwareLogicalHeight()
        {
            return 44 + (int)Math.Round((ControlDrawing.UiFontScale - 1f) * 18f);
        }
    }

    public event EventHandler? ActionRequested;

    public string Title
    {
        get => _titleLabel.Text;
        set
        {
            _titleLabel.Text = value;
            AccessibleName = value;
            AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
        }
    }

    public string RightText
    {
        get => _rightLabel?.Text ?? string.Empty;
        set
        {
            if (_rightLabel != null)
            {
                _rightLabel.Text = value;
                AccessibleDescription = value;
                AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            }
        }
    }

    public bool Active
    {
        get => _active;
        set
        {
            _active = value;
            AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
            if (AccessibilityPreferences.HighContrast)
            {
                ApplyTheme(_palette);
            }
            else
            {
                Invalidate();
            }
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
            AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
            if (value)
            {
                AccessibilityNotifyClients(AccessibleEvents.SystemAlert, -1);
            }

            if (AccessibilityPreferences.HighContrast)
            {
                ApplyTheme(_palette);
            }
            else
            {
                Invalidate(true);
            }
        }
    }

    public Color SurfaceBackgroundColor
    {
        get
        {
            if (AccessibilityPreferences.HighContrast)
            {
                return _hovered || _pressed || _active || _isSuccess || ShowsRowFocusVisual
                    ? SystemColors.Highlight
                    : SystemColors.Window;
            }

            if (_isSuccess)
            {
                return _palette.MenuBackground.GetBrightness() > 0.65f
                    ? Color.FromArgb(217, 247, 225)
                    : Color.FromArgb(24, 94, 54);
            }

            if (_hovered || _pressed || _active || ShowsRowFocusVisual)
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

                return ShowsRowFocusVisual && !_hovered && !_pressed && !_active
                    ? Color.FromArgb(34, _palette.Accent)
                    : _pressed
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
        bool highContrastHighlight = AccessibilityPreferences.HighContrast &&
            (_hovered || _pressed || _active || _isSuccess || ShowsRowFocusVisual);
        _titleLabel.ForeColor = highContrastHighlight ? SystemColors.HighlightText : palette.Text;
        if (_rightLabel != null)
        {
            _rightLabel.ForeColor = highContrastHighlight ? SystemColors.HighlightText : palette.SecondaryText;
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

        if (_iconPath != null)
        {
            int iconWidth = ControlDrawing.ScaleLogical(this, 20);
            int iconHeight = Math.Min(innerHeight, ControlDrawing.ScaleLogical(this, 18));
            _iconBounds = new Rectangle(left, y + Math.Max(0, (innerHeight - iconHeight) / 2), iconWidth, iconHeight);
            left = _iconBounds.Right + ControlDrawing.ScaleLogical(this, 10);
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
        Focus();
        SetState(_hovered, pressed: true);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        SetState(_hovered, pressed: false);
    }

    protected override void OnClick(EventArgs e)
    {
        Focus();
        base.OnClick(e);
        ActionRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space)
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

        Rectangle fillRect = new(4, 1, Math.Max(8, Width - 8), Math.Max(8, Height - 2));
        if (_hovered || _pressed || _active || _isSuccess || ShowsRowFocusVisual)
        {
            using GraphicsPath path = ControlDrawing.RoundedRect(fillRect, 9);
            Color fill = SurfaceBackgroundColor;
            using SolidBrush fillBrush = new(fill);
            Color borderColor = AccessibilityPreferences.HighContrast
                ? SystemColors.WindowText
                : _isDestructive
                ? Color.FromArgb(186, 82, 92)
                : (_isSuccess ? _palette.Accent : (_active ? _palette.Accent : _palette.Border));
            int borderAlpha = AccessibilityPreferences.HighContrast ? 255 : _isDestructive ? 76 : (_isSuccess ? 96 : (_active ? 72 : 28));
            using Pen borderPen = new(Color.FromArgb(borderAlpha, borderColor));
            e.Graphics.FillPath(fillBrush, path);
            if (!ShowsRowFocusVisual)
            {
                e.Graphics.DrawPath(borderPen, path);
            }
        }

        if (ShowsRowFocusVisual)
        {
            ControlDrawing.DrawFocusRing(e.Graphics, fillRect, ControlDrawing.ScaleLogical(this, 9), _palette);
        }

        if (_iconPath != null)
        {
            bool highContrastHighlight = AccessibilityPreferences.HighContrast &&
                (_hovered || _pressed || _active || _isSuccess || ShowsRowFocusVisual);
            FluentTrayIcons.Draw(
                e.Graphics,
                _iconPath,
                _iconBounds,
                highContrastHighlight ? SystemColors.HighlightText : _palette.Text);
        }
    }

    public void PaintChildSurfaceBackground(Graphics graphics, Rectangle childBounds)
    {
        using Region clip = graphics.Clip?.Clone() ?? new Region(childBounds);
        graphics.SetClip(childBounds);

        using SolidBrush backgroundBrush = new(ControlDrawing.EffectiveBackColor(this));
        graphics.FillRectangle(backgroundBrush, childBounds);

        if (_hovered || _pressed || _active || _isSuccess || ShowsRowFocusVisual)
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
        if (AccessibilityPreferences.HighContrast)
        {
            ApplyTheme(_palette);
        }
        else
        {
            InvalidateRowVisuals();
        }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        if (AccessibilityPreferences.HighContrast)
        {
            ApplyTheme(_palette);
        }
        else
        {
            InvalidateRowVisuals();
        }
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        if (AccessibilityPreferences.HighContrast)
        {
            ApplyTheme(_palette);
        }
        else
        {
            InvalidateRowVisuals();
        }
    }

    private void InvalidateRowVisuals()
    {
        Invalidate();
        _titleLabel.Invalidate();
        _rightLabel?.Invalidate();
        _toggle?.Invalidate();
    }

    private bool HasKeyboardFocusVisual => ControlDrawing.ShouldDrawFocus(this, ShowFocusCues);

    internal bool ShowsToggleFocusVisual => _toggle != null && HasKeyboardFocusVisual;

    private bool ShowsRowFocusVisual => _toggle == null && HasKeyboardFocusVisual;

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        Height = ControlDrawing.ScaleLogical(this, 44 + (int)Math.Round((ControlDrawing.UiFontScale - 1f) * 18f));
        Padding = new Padding(
            ControlDrawing.ScaleLogical(this, 12),
            0,
            ControlDrawing.ScaleLogical(this, 12),
            0);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new MenuRowAccessibleObject(this);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _iconPath?.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class MenuRowAccessibleObject(TrayMenuRow owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => owner._toggle != null ? AccessibleRole.CheckButton : AccessibleRole.PushButton;

        public override AccessibleStates State => base.State |
            AccessibleStates.Focusable |
            (owner._toggle?.IsOn == true ? AccessibleStates.Checked : AccessibleStates.None) |
            (owner.Active ? AccessibleStates.Selected : AccessibleStates.None) |
            (owner.Enabled ? AccessibleStates.None : AccessibleStates.Unavailable);

        public override string? Value
        {
            get => owner.RightText;
            set { }
        }

        public override void DoDefaultAction()
        {
            if (owner.Enabled)
            {
                owner.OnClick(EventArgs.Empty);
            }
        }
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
        Size = new Size(150, 92);
        Margin = new Padding(0);
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
    }

    public bool ShowWindowsLogo { get; set; }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        Invalidate();
    }

    protected override void OnTextChanged(EventArgs e)
    {
        Invalidate();
        AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
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

        Rectangle shadowRect = new(0, 7, Width - 1, Height - 8);
        using GraphicsPath shadowPath = ControlDrawing.RoundedRect(shadowRect, 10);
        using SolidBrush shadow = new(Color.FromArgb(95, Color.Black));
        e.Graphics.FillPath(shadow, shadowPath);

        int pressedOffset = _pressed ? 4 : 0;
        Rectangle rect = new(0, pressedOffset, Width - 1, Height - 9);
        using GraphicsPath path = ControlDrawing.RoundedRect(rect, 10);
        using SolidBrush fill = new(SurfaceBackgroundColor);
        using Pen border = new(ControlContrast.FieldBorder(_palette), 1.5f);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);

        Rectangle innerRect = new(6, pressedOffset + 6, Width - 13, Height - 23);
        using GraphicsPath innerPath = ControlDrawing.RoundedRect(innerRect, 7);
        using Pen innerPen = new(Color.FromArgb(72, Color.White));
        e.Graphics.DrawPath(innerPen, innerPath);

        Rectangle textRect = new(16, pressedOffset, Width - 32, Height - 9);
        if (ShowWindowsLogo)
        {
            int iconSize = 30;
            Rectangle iconRect = new(24, pressedOffset + ((Height - 9 - iconSize) / 2), iconSize, iconSize);
            using Pen iconPen = new(_palette.Text, 2f);
            DrawWindowsLogo(e.Graphics, iconPen, iconRect);
            textRect = new(iconRect.Right + 14, pressedOffset, Width - iconRect.Right - 30, Height - 9);
        }

        using Font textFont = ControlDrawing.UiFont("Segoe UI Semibold", 16f, FontStyle.Bold);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            textFont,
            textRect,
            _palette.Text,
            (ShowWindowsLogo ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter) |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis);

        if (ControlDrawing.ShouldDrawFocus(this, ShowFocusCues))
        {
            Rectangle focusBounds = Rectangle.Inflate(rect, -2, -2);
            ControlDrawing.DrawFocusRing(e.Graphics, focusBounds, 8, _palette);
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

    protected override AccessibleObject CreateAccessibilityInstance() => new KeyBadgeAccessibleObject(this);

    private sealed class KeyBadgeAccessibleObject(KeyBadgeControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.PushButton;

        public override string? Value
        {
            get => owner.Text;
            set { }
        }

        public override void DoDefaultAction()
        {
            if (owner.Enabled)
            {
                owner.OnClick(EventArgs.Empty);
            }
        }
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
        CornerRadius = 8;
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

internal sealed class ModernButton : Control, IButtonControl
{
    private ThemePalette _palette = ThemePalettes.Light;
    private Color _outlineColor = Color.Transparent;
    private Color _hoverBackColor;
    private Color _hoverOutlineColor = Color.Empty;
    private Color _pressedBackColor;
    private bool _successHoverEnabled;
    private Color _successNormalText;
    private Color _successHoverText;
    private bool _hovered;
    private bool _pressed;
    private bool _isDefaultButton;
    private DialogResult _dialogResult;

    public ModernButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable |
            ControlStyles.StandardClick |
            ControlStyles.StandardDoubleClick |
            ControlStyles.UserPaint,
            true);
        AutoSize = true;
        MinimumSize = new Size(120, 44);
        Padding = new Padding(14, 0, 14, 0);
        TabStop = true;
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.PushButton;
        Resize += (_, _) => Invalidate();
    }

    public DialogResult DialogResult
    {
        get => _dialogResult;
        set => _dialogResult = value;
    }

    public bool WrapText { get; set; }

    public void NotifyDefault(bool value)
    {
        if (_isDefaultButton == value)
        {
            return;
        }

        _isDefaultButton = value;
        Invalidate();
    }

    public void PerformClick()
    {
        if (Enabled && Visible)
        {
            OnClick(EventArgs.Empty);
        }
    }

    internal void SetInteractionStateForCapture(bool hovered, bool pressed = false)
    {
        _hovered = hovered;
        _pressed = pressed;
        if (_successHoverEnabled)
        {
            ForeColor = hovered ? _successHoverText : _successNormalText;
        }

        Invalidate();
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        Size textSize = TextRenderer.MeasureText(
            Text ?? string.Empty,
            Font,
            Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        int preferredHeight = textSize.Height + ControlDrawing.ScaleLogical(this, 16);
        return new Size(
            Math.Max(MinimumSize.Width, textSize.Width + Padding.Horizontal),
            Math.Max(MinimumSize.Height, preferredHeight));
    }

    public void SetOutlineColor(Color color)
    {
        _outlineColor = color;
        Invalidate();
    }

    internal void SetProminentHover(Color backgroundColor, Color outlineColor)
    {
        _hoverBackColor = backgroundColor;
        _hoverOutlineColor = outlineColor;
        Invalidate();
    }

    public void ApplyTheme(ThemePalette palette, bool emphasis = false, bool destructive = false, bool destructiveHoverEnabled = false)
    {
        _palette = palette;
        _successHoverEnabled = false;
        _hoverOutlineColor = Color.Empty;
        if (AccessibilityPreferences.HighContrast)
        {
            BackColor = emphasis ? SystemColors.Highlight : SystemColors.Control;
            ForeColor = emphasis ? SystemColors.HighlightText : SystemColors.ControlText;
            _outlineColor = SystemColors.WindowText;
            _hoverBackColor = SystemColors.Highlight;
            _pressedBackColor = SystemColors.HotTrack;
            Invalidate();
            return;
        }

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
        ForeColor = useDestructiveBorder
            ? destructiveText
            : emphasis && !destructive ? ControlDrawing.ContrastText(palette.Accent) : palette.Text;
        _outlineColor = useDestructiveBorder
            ? destructiveBorder
            : emphasis ? palette.Accent : palette.Border;
        _hoverBackColor = destructive || destructiveHoverEnabled
            ? destructiveHoverColor
            : emphasis ? palette.AccentHover : palette.ButtonHover;
        _pressedBackColor = destructive || destructiveHoverEnabled
            ? destructivePressed
            : emphasis ? palette.AccentPressed : palette.ButtonPressed;
        Invalidate();
    }

    public void ApplySuccessOutlineTheme(ThemePalette palette)
    {
        _palette = palette;
        if (AccessibilityPreferences.HighContrast)
        {
            ApplyTheme(palette);
            return;
        }

        _successHoverEnabled = true;
        BackColor = palette.ButtonBackground;
        _successNormalText = palette.Text;
        Color successHoverBack = Color.FromArgb(62, 145, 86);
        _successHoverText = ControlDrawing.ContrastText(successHoverBack);
        ForeColor = _successNormalText;
        _outlineColor = Color.FromArgb(72, 163, 96);
        _hoverBackColor = successHoverBack;
        _pressedBackColor = Color.FromArgb(50, 124, 72);
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        if (_successHoverEnabled)
        {
            ForeColor = _successHoverText;
        }

        base.OnMouseEnter(e);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        if (_successHoverEnabled)
        {
            ForeColor = _successNormalText;
        }

        base.OnMouseLeave(e);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        if (mevent.Button == MouseButtons.Left)
        {
            Focus();
            _pressed = true;
            Invalidate();
        }

        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _pressed = false;
        base.OnMouseUp(mevent);
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            PerformClick();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Space)
        {
            _pressed = true;
            Invalidate();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space)
        {
            bool performClick = _pressed;
            _pressed = false;
            Invalidate();
            if (performClick)
            {
                PerformClick();
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        base.OnKeyUp(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        _pressed = false;
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        Form? form = FindForm();
        base.OnClick(e);
        if (form != null && !form.IsDisposed && _dialogResult != DialogResult.None)
        {
            form.DialogResult = _dialogResult;
        }
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        using SolidBrush brush = new(ControlDrawing.EffectiveBackColor(this));
        pevent.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        if (Width <= 4 || Height <= 4)
        {
            return;
        }

        pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        pevent.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        int inset = ControlDrawing.ScaleLogical(this, 1);
        int radius = ControlDrawing.ScaleLogical(this, 7);
        Rectangle surfaceBounds = new(
            inset,
            inset,
            Math.Max(1, Width - (inset * 2) - 1),
            Math.Max(1, Height - (inset * 2) - 1));
        using GraphicsPath surfacePath = ControlDrawing.RoundedRect(surfaceBounds, radius);
        Color fillColor = !Enabled
            ? ControlDrawing.Blend(ControlDrawing.EffectiveBackColor(this), BackColor, 150)
            : _pressed && !_pressedBackColor.IsEmpty
            ? _pressedBackColor
            : _hovered && !_hoverBackColor.IsEmpty
            ? _hoverBackColor
            : BackColor;
        using SolidBrush fillBrush = new(fillColor);
        pevent.Graphics.FillPath(fillBrush, surfacePath);

        if (_outlineColor != Color.Transparent)
        {
            Color outlineColor = _isDefaultButton && !_successHoverEnabled
                ? ControlDrawing.Blend(_outlineColor, _palette.Text, 72)
                : _outlineColor;
            if (_hovered && !_hoverOutlineColor.IsEmpty)
            {
                outlineColor = _hoverOutlineColor;
            }

            using Pen borderPen = new(outlineColor, Math.Max(1f, DeviceDpi / 96f));
            pevent.Graphics.DrawPath(borderPen, surfacePath);
        }

        Color textColor = Enabled ? ForeColor : _palette.SecondaryText;
        TextFormatFlags textFlags =
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPadding |
            TextFormatFlags.NoPrefix |
            (WrapText
                ? TextFormatFlags.WordBreak
                : TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(
            pevent.Graphics,
            Text,
            Font,
            ClientRectangle,
            textColor,
            textFlags);

        if (ControlDrawing.ShouldDrawFocus(this, ShowFocusCues))
        {
            int focusInset = ControlDrawing.ScaleLogical(this, 3);
            ControlDrawing.DrawFocusRing(
                pevent.Graphics,
                new Rectangle(
                    focusInset,
                    focusInset,
                    Math.Max(1, Width - (focusInset * 2) - 1),
                    Math.Max(1, Height - (focusInset * 2) - 1)),
                ControlDrawing.ScaleLogical(this, 6),
                _palette);
        }
    }
}

internal sealed class SettingsSidebarItem : Control, ISurfaceBackgroundProvider, IChildSurfaceBackgroundRenderer
{
    private static readonly Color SidebarBackgroundDark = Color.FromArgb(17, 20, 26);
    private static readonly Color SidebarHoverDark = Color.FromArgb(29, 34, 43);
    private static readonly Color SidebarSelectedDark = Color.FromArgb(39, 47, 60);
    private static readonly Color SidebarSelectedHoverDark = Color.FromArgb(48, 58, 74);
    private static readonly Color SidebarSelectedPressedDark = Color.FromArgb(34, 42, 55);
    private static readonly Color SidebarBackgroundLight = Color.FromArgb(248, 250, 252);
    private static readonly Color SidebarHoverLight = Color.FromArgb(226, 233, 242);
    private static readonly Color SidebarSelectedLight = Color.FromArgb(214, 226, 240);
    private static readonly Color SidebarSelectedHoverLight = Color.FromArgb(199, 216, 235);
    private static readonly Color SidebarSelectedPressedLight = Color.FromArgb(188, 208, 231);

    private readonly FluentIconControl _iconControl;
    private readonly Label _titleLabel;
    private ThemePalette _palette;
    private bool _compact;
    private bool _hovered;
    private bool _pressed;
    private bool _selected;

    internal event EventHandler? ContentNavigationRequested;

    public SettingsSidebarItem(ThemePalette palette, string title, TrayFluentIcon icon)
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
        Cursor = Cursors.Hand;
        TabStop = false;
        Margin = new Padding(0, 0, 0, 2);
        Padding = new Padding(5, 0, 4, 0);
        AccessibleName = title;
        AccessibleRole = AccessibleRole.PageTab;

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
            control.MouseLeave += (_, _) => RefreshHoverFromPointer();
            control.MouseDown += (_, _) => SetState(true, true);
            control.MouseUp += (_, _) => SetState(true, false);
        }

        ApplyTheme(palette);
        Height = 40;
    }

    public bool Selected
    {
        get => _selected;
        set
        {
            TabStop = value;
            if (_selected == value)
            {
                return;
            }

            _selected = value;
            if (value && Parent != null)
            {
                foreach (Control control in Parent.Controls)
                {
                    if (control is SettingsSidebarItem sibling && sibling != this)
                    {
                        sibling.SetState(hovered: false, pressed: false);
                    }
                }
            }

            AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
            AccessibilityNotifyClients(AccessibleEvents.Selection, -1);
            ApplyTheme(_palette);
        }
    }

    public string Title
    {
        get => _titleLabel.Text;
        set
        {
            _titleLabel.Text = value;
            AccessibleName = value;
        }
    }

    public bool Compact
    {
        get => _compact;
        set
        {
            if (_compact == value)
            {
                return;
            }

            _compact = value;
            _titleLabel.Visible = !value;
            UpdateChildBounds();
            Invalidate(true);
        }
    }

    public Color SurfaceBackgroundColor
    {
        get
        {
            if (AccessibilityPreferences.HighContrast)
            {
                return _selected || _hovered || _pressed
                    ? SystemColors.Highlight
                    : SystemColors.Window;
            }

            if (_selected && _pressed)
            {
                return _useDarkPalette ? SidebarSelectedPressedDark : SidebarSelectedPressedLight;
            }

            if (_selected && _hovered)
            {
                return _useDarkPalette ? SidebarSelectedHoverDark : SidebarSelectedHoverLight;
            }

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
        _titleLabel.ForeColor = AccessibilityPreferences.HighContrast && (_selected || _hovered || _pressed)
            ? SystemColors.HighlightText
            : (_selected ? palette.Text : palette.SecondaryText);
        Invalidate(true);
    }

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        Height = Math.Max(
            ControlDrawing.ScaleLogical(this, 40),
            _titleLabel.Font.Height + ControlDrawing.ScaleLogical(this, 12));
        Padding = new Padding(
            ControlDrawing.ScaleLogical(this, 5),
            0,
            ControlDrawing.ScaleLogical(this, 4),
            0);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateChildBounds();
        Invalidate();
    }

    private void UpdateChildBounds()
    {
        if (_iconControl is null || _titleLabel is null)
        {
            return;
        }

        int iconSize = ControlDrawing.ScaleLogical(this, 18);
        int iconX = _compact
            ? Math.Max(0, (Width - iconSize) / 2)
            : Padding.Left + ControlDrawing.ScaleLogical(this, 2);
        _iconControl.Bounds = new Rectangle(iconX, (Height - iconSize) / 2, iconSize, iconSize);
        _titleLabel.Bounds = _compact
            ? Rectangle.Empty
            : new Rectangle(
                _iconControl.Right + ControlDrawing.ScaleLogical(this, 4),
                0,
                Math.Max(
                    ControlDrawing.ScaleLogical(this, 40),
                    Width - _iconControl.Right - Padding.Right - ControlDrawing.ScaleLogical(this, 4)),
                Height);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        SetState(true, _pressed);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        RefreshHoverFromPointer();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        SetState(_hovered, true);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        SetState(_hovered, false);
    }

    protected override void OnClick(EventArgs e)
    {
        Focus();
        base.OnClick(e);
    }

    protected override bool IsInputKey(Keys keyData) => keyData is
        Keys.Enter or Keys.Space or Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Home or Keys.End ||
        base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            OnClick(EventArgs.Empty);
        }
        else if (e.KeyCode == Keys.Up)
        {
            NavigateToSibling(-1, selectEdge: false);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Down)
        {
            NavigateToSibling(1, selectEdge: false);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == (RightToLeft == RightToLeft.Yes ? Keys.Left : Keys.Right))
        {
            ContentNavigationRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode is Keys.Left or Keys.Right)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Home)
        {
            NavigateToSibling(1, selectEdge: true);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.End)
        {
            NavigateToSibling(-1, selectEdge: true);
            e.Handled = true;
            e.SuppressKeyPress = true;
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

        if ((_selected || _hovered) && !AccessibilityPreferences.HighContrast)
        {
            int borderAlpha = _selected
                ? (_hovered ? 138 : 104)
                : 72;
            using Pen stateBorder = new(Color.FromArgb(borderAlpha, _palette.Accent), _selected ? 1.5f : 1.2f);
            e.Graphics.DrawPath(stateBorder, fillPath);
        }

        if (ControlDrawing.ShouldDrawFocus(this, ShowFocusCues))
        {
            ControlDrawing.DrawFocusRing(e.Graphics, new Rectangle(3, 3, Width - 7, Height - 7), ControlDrawing.ScaleLogical(this, 12), _palette);
        }
    }

    public void PaintChildSurfaceBackground(Graphics graphics, Rectangle childBounds)
    {
        using Region clip = graphics.Clip?.Clone() ?? new Region(childBounds);
        graphics.SetClip(childBounds);
        using SolidBrush backgroundBrush = new(SurfaceBackgroundColor);
        graphics.FillRectangle(backgroundBrush, childBounds);
        graphics.Clip = clip;
    }

    internal void SetHoveredForCapture(bool hovered) => SetState(hovered, pressed: false);

    internal void EnterContentForCapture() => ContentNavigationRequested?.Invoke(this, EventArgs.Empty);

    private void SetState(bool hovered, bool pressed)
    {
        if (hovered && Parent != null)
        {
            foreach (Control control in Parent.Controls)
            {
                if (control is SettingsSidebarItem sibling && sibling != this && (sibling._hovered || sibling._pressed))
                {
                    sibling.SetState(hovered: false, pressed: false);
                }
            }
        }

        _hovered = hovered;
        _pressed = pressed;
        if (AccessibilityPreferences.HighContrast)
        {
            ApplyTheme(_palette);
        }
        else
        {
            Invalidate(true);
        }
    }

    private void RefreshHoverFromPointer()
    {
        if (IsDisposed)
        {
            return;
        }

        if (!IsHandleCreated)
        {
            SetState(hovered: false, pressed: false);
            return;
        }

        bool pointerInside = ClientRectangle.Contains(PointToClient(Cursor.Position));
        SetState(pointerInside, pressed: false);
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

    private void NavigateToSibling(int direction, bool selectEdge)
    {
        if (Parent == null)
        {
            return;
        }

        var items = new List<SettingsSidebarItem>();
        foreach (Control control in Parent.Controls)
        {
            if (control is SettingsSidebarItem item && item.Visible && item.Enabled)
            {
                items.Add(item);
            }
        }

        if (items.Count == 0)
        {
            return;
        }

        int currentIndex = items.IndexOf(this);
        int nextIndex = selectEdge
            ? (direction > 0 ? 0 : items.Count - 1)
            : (currentIndex + direction + items.Count) % items.Count;
        SettingsSidebarItem next = items[nextIndex];
        next.Focus();
        next.OnClick(EventArgs.Empty);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new SidebarAccessibleObject(this);

    private sealed class SidebarAccessibleObject(SettingsSidebarItem owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.PageTab;

        public override AccessibleStates State => base.State |
            AccessibleStates.Focusable |
            AccessibleStates.Selectable |
            (owner.Selected ? AccessibleStates.Selected : AccessibleStates.None) |
            (owner.Enabled ? AccessibleStates.None : AccessibleStates.Unavailable);

        public override void DoDefaultAction()
        {
            if (owner.Enabled)
            {
                owner.OnClick(EventArgs.Empty);
            }
        }
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
    private const int LogicalFieldHeight = 32;
    private const int LogicalItemHeight = 26;
    private const int LogicalItemVerticalPadding = 6;
    private const int LogicalMenuBorderHeight = 2;
    private ThemePalette _palette;
    private readonly List<string> _items = new();
    private int _selectedIndex = -1;
    private bool _hovered;
    private bool _pressed;
    private ContextMenuStrip? _activeMenu;
    private int _menuMinimumWidth;

    internal sealed class MenuCapture : IDisposable
    {
        internal MenuCapture(Bitmap bitmap, Rectangle screenBounds, bool openedAbove, int itemHeight)
        {
            Bitmap = bitmap;
            ScreenBounds = screenBounds;
            OpenedAbove = openedAbove;
            ItemHeight = itemHeight;
        }

        internal Bitmap Bitmap { get; }

        internal Rectangle ScreenBounds { get; }

        internal bool OpenedAbove { get; }

        internal int ItemHeight { get; }

        public void Dispose() => Bitmap.Dispose();
    }

    private readonly record struct MenuLayout(
        Rectangle AnchorBounds,
        Rectangle WorkingArea,
        int Width,
        int Height,
        int ItemHeight,
        bool OpensAbove,
        bool AlignsRight)
    {
        internal Point AnchorPoint => new(
            AlignsRight ? AnchorBounds.Right : AnchorBounds.Left,
            OpensAbove ? AnchorBounds.Top : AnchorBounds.Bottom);

        internal ToolStripDropDownDirection Direction => OpensAbove
            ? (AlignsRight ? ToolStripDropDownDirection.AboveLeft : ToolStripDropDownDirection.AboveRight)
            : (AlignsRight ? ToolStripDropDownDirection.BelowLeft : ToolStripDropDownDirection.BelowRight);

        internal Rectangle ScreenBounds => new(
            AlignsRight ? AnchorPoint.X - Width : AnchorPoint.X,
            OpensAbove ? AnchorPoint.Y - Height : AnchorPoint.Y,
            Width,
            Height);
    }

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
        Height = LogicalFieldHeight;
        Width = 220;
        Cursor = Cursors.Hand;
        TabStop = true;
        BackColor = Color.Transparent;
        AccessibleRole = AccessibleRole.ComboBox;
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
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            AccessibilityNotifyClients(AccessibleEvents.Selection, -1);
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string? SelectedItem => _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;

    public int MenuMinimumWidth
    {
        get => _menuMinimumWidth;
        set => _menuMinimumWidth = Math.Max(0, value);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        int widestText = 0;
        foreach (string item in _items)
        {
            widestText = Math.Max(
                widestText,
                TextRenderer.MeasureText(
                    item,
                    Font,
                    Size.Empty,
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width);
        }

        int horizontalChrome = ControlDrawing.ScaleLogical(this, 52);
        int minimumWidth = ControlDrawing.ScaleLogical(this, 160);
        int preferredWidth = Math.Max(minimumWidth, widestText + horizontalChrome);
        int preferredHeight = Math.Max(Height, ControlDrawing.ScaleLogical(this, LogicalFieldHeight));
        return new Size(preferredWidth, preferredHeight);
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
        Focus();
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
        return keyData is Keys.Enter or Keys.Space or Keys.Down or Keys.Up or Keys.Home or Keys.End || base.IsInputKey(keyData);
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
            case Keys.Home:
                if (_items.Count > 0)
                {
                    SelectedIndex = 0;
                }
                e.Handled = true;
                break;
            case Keys.End:
                if (_items.Count > 0)
                {
                    SelectedIndex = _items.Count - 1;
                }
                e.Handled = true;
                break;
        }

        if (e.Handled)
        {
            e.SuppressKeyPress = true;
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
        Color contentColor = AccessibilityPreferences.HighContrast && (_hovered || _pressed)
            ? SystemColors.HighlightText
            : _palette.Text;
        TextRenderer.DrawText(
            e.Graphics,
            text,
            Font,
            textBounds,
            contentColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        Point center = new(Width - 17, Height / 2);
        using GraphicsPath chevron = new();
        chevron.AddLines(new Point[]
        {
            new Point(center.X - 4, center.Y - 2),
            new Point(center.X, center.Y + 2),
            new Point(center.X + 4, center.Y - 2)
        });
        using Pen chevronPen = new(
            AccessibilityPreferences.HighContrast && (_hovered || _pressed) ? SystemColors.HighlightText : _palette.SecondaryText,
            1.6f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        e.Graphics.DrawPath(chevronPen, chevron);

        if (ControlDrawing.ShouldDrawFocus(this, ShowFocusCues))
        {
            ControlDrawing.DrawFocusRing(e.Graphics, new Rectangle(2, 2, Width - 5, Height - 5), 8, _palette);
        }
    }

    internal MenuCapture RenderMenuForCapture(Rectangle? anchorBounds = null, Rectangle? workingArea = null)
    {
        if (_items.Count == 0)
        {
            throw new InvalidOperationException("Cannot capture an empty dropdown menu.");
        }

        MenuLayout layout = CalculateMenuLayout(anchorBounds, workingArea);
        using ContextMenuStrip menu = CreateMenu(layout);
        _ = menu.Handle;
        menu.PerformLayout();
        menu.Size = new Size(layout.Width, layout.Height);

        var bitmap = new Bitmap(layout.Width, layout.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        try
        {
            menu.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            return new MenuCapture(bitmap, layout.ScreenBounds, layout.OpensAbove, layout.ItemHeight);
        }
        catch
        {
            bitmap.Dispose();
            throw;
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

        MenuLayout layout = CalculateMenuLayout();
        ContextMenuStrip menu = CreateMenu(layout);
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
        menu.Show(layout.AnchorPoint, layout.Direction);
    }

    private MenuLayout CalculateMenuLayout(Rectangle? anchorBoundsOverride = null, Rectangle? workingAreaOverride = null)
    {
        Rectangle anchorBounds = anchorBoundsOverride ?? RectangleToScreen(ClientRectangle);
        Rectangle workingArea = workingAreaOverride ?? Screen.FromRectangle(anchorBounds).WorkingArea;
        int desiredWidth = Math.Max(Math.Max(Width, MinimumSize.Width), _menuMinimumWidth);
        foreach (string itemText in _items)
        {
            int textWidth = TextRenderer.MeasureText(itemText, Font).Width;
            desiredWidth = Math.Max(desiredWidth, textWidth + ControlDrawing.ScaleLogical(this, 20));
        }

        desiredWidth = Math.Min(desiredWidth, workingArea.Width);
        int itemHeight = Math.Max(
            ControlDrawing.ScaleLogical(this, LogicalItemHeight),
            Font.Height + ControlDrawing.ScaleLogical(this, LogicalItemVerticalPadding));
        int naturalMenuHeight = Math.Max(
            1,
            (_items.Count * itemHeight) + ControlDrawing.ScaleLogical(this, LogicalMenuBorderHeight));
        int spaceBelow = Math.Max(0, workingArea.Bottom - anchorBounds.Bottom);
        int spaceAbove = Math.Max(0, anchorBounds.Top - workingArea.Top);
        bool opensAbove = spaceBelow < naturalMenuHeight && spaceAbove > spaceBelow;
        int availableHeight = opensAbove ? spaceAbove : spaceBelow;
        int menuHeight = Math.Min(naturalMenuHeight, Math.Max(1, availableHeight));
        bool alignRightEdge = anchorBounds.Left + desiredWidth > workingArea.Right ||
            (desiredWidth > anchorBounds.Width && anchorBounds.Right - desiredWidth >= workingArea.Left);

        return new MenuLayout(
            anchorBounds,
            workingArea,
            desiredWidth,
            menuHeight,
            itemHeight,
            opensAbove,
            alignRightEdge);
    }

    private ContextMenuStrip CreateMenu(MenuLayout layout)
    {
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
        int desiredWidth = layout.Width;
        int itemHeight = layout.ItemHeight;
        int menuHeight = layout.Height;
        menu.MinimumSize = new Size(desiredWidth, 0);
        menu.MaximumSize = new Size(desiredWidth, menuHeight);
        menu.Size = new Size(desiredWidth, menuHeight);

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

        return menu;
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new DropdownAccessibleObject(this);

    private sealed class DropdownAccessibleObject(ModernDropdown owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.ComboBox;

        public override AccessibleStates State => base.State |
            AccessibleStates.Focusable |
            AccessibleStates.HasPopup |
            (owner.Enabled ? AccessibleStates.None : AccessibleStates.Unavailable);

        public override string? Value
        {
            get => owner.SelectedItem;
            set { }
        }

        public override void DoDefaultAction()
        {
            if (owner.Enabled)
            {
                owner.ShowMenu();
            }
        }
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
        e.TextColor = AccessibilityPreferences.HighContrast && e.Item.Selected
            ? SystemColors.HighlightText
            : _palette.Text;
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

internal sealed class CompactNumericTextBox : ClipboardFreeTextBox, IMessageFilter
{
    private const int WmLeftButtonDown = 0x0201;
    private const int WmNonClientLeftButtonDown = 0x00A1;
    private bool _messageFilterInstalled;
    private bool _raisingOutsideClick;

    internal event EventHandler? CommitRequested;

    internal event EventHandler? CancelRequested;

    internal int MinimumValue { get; set; }

    internal int MaximumValue { get; set; } = 100;

    internal Func<int, string>? ValueFormatter { get; set; }

    internal bool TryGetNumericValue(out int value)
    {
        return TryParseCandidate(Text, out value) && value >= MinimumValue && value <= MaximumValue;
    }

    internal bool AcceptsValueText(string text)
    {
        return TryParseCandidate(text, out int value) && value >= MinimumValue && value <= MaximumValue;
    }

    public bool PreFilterMessage(ref Message m)
    {
        if (!Focused || _raisingOutsideClick ||
            (m.Msg != WmLeftButtonDown && m.Msg != WmNonClientLeftButtonDown))
        {
            return false;
        }

        Control? target = FromHandle(m.HWnd);
        if (target == this || target == Parent || (target != null && Contains(target)))
        {
            return false;
        }

        try
        {
            _raisingOutsideClick = true;
            CommitRequested?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _raisingOutsideClick = false;
        }

        return false;
    }

    protected override bool IsInputKey(Keys keyData)
    {
        return (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown ||
            base.IsInputKey(keyData);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        if (key == Keys.Enter)
        {
            CommitRequested?.Invoke(this, EventArgs.Empty);
            return true;
        }

        if (key == Keys.Escape)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        if (!_messageFilterInstalled)
        {
            Application.AddMessageFilter(this);
            _messageFilterInstalled = true;
        }
    }

    protected override void OnLostFocus(EventArgs e)
    {
        RemoveMessageFilter();
        base.OnLostFocus(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            RemoveMessageFilter();
        }

        base.Dispose(disposing);
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) && (e.KeyChar < '0' || e.KeyChar > '9'))
        {
            e.Handled = true;
        }

        base.OnKeyPress(e);
    }


    private bool TryParseCandidate(string text, out int value)
    {
        string trimmed = text.Trim();
        if (int.TryParse(trimmed, NumberStyles.None, CultureInfo.CurrentCulture, out value))
        {
            return true;
        }

        string digits = new(trimmed.Where(character => character is >= '0' and <= '9').ToArray());
        if (digits.Length == 0 ||
            !int.TryParse(digits, NumberStyles.None, CultureInfo.CurrentCulture, out value) ||
            ValueFormatter == null)
        {
            value = 0;
            return false;
        }

        return string.Equals(trimmed, ValueFormatter(value).Trim(), StringComparison.CurrentCulture);
    }

    private void RemoveMessageFilter()
    {
        if (!_messageFilterInstalled)
        {
            return;
        }

        Application.RemoveMessageFilter(this);
        _messageFilterInstalled = false;
    }
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
        Height = 40;
        Width = 280;
        AccessibleRole = AccessibleRole.Slider;
    }

    public event EventHandler? ValueChanged;

    internal Action<int>? CaptureValueChanged { get; set; }

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
            SetValue(value, snap: true);
        }
    }

    public void SetExactValue(int value)
    {
        SetValue(value, snap: false);
    }

    internal void SetValueForCapture(int value)
    {
        _value = Snap(Math.Clamp(value, _minimum, _maximum));
        CaptureValueChanged?.Invoke(_value);
        Invalidate();
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
        return keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown
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
            case Keys.PageDown:
                Value -= _snapStep * 10;
                e.Handled = true;
                break;
            case Keys.PageUp:
                Value += _snapStep * 10;
                e.Handled = true;
                break;
        }

        if (e.Handled)
        {
            e.SuppressKeyPress = true;
        }

        base.OnKeyDown(e);
    }

    internal void NavigateForCapture(Keys key) => OnKeyDown(new KeyEventArgs(key));

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        int horizontalInset = ControlDrawing.ScaleLogical(this, 12);
        Rectangle trackRect = new(horizontalInset, (Height / 2) - 3, Math.Max(8, Width - (horizontalInset * 2)), 6);
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

        int knobSize = Math.Min(ControlDrawing.ScaleLogical(this, 24), Math.Max(18, Height - 8));
        int knobX = Math.Clamp(trackRect.X + (int)Math.Round((trackRect.Width - knobSize) * ratio), trackRect.X, trackRect.Right - knobSize);
        Rectangle knobRect = new(knobX, (Height - knobSize) / 2, knobSize, knobSize);
        using SolidBrush knobBrush = new(Color.FromArgb(244, 246, 249));
        using Pen knobBorder = new(Color.FromArgb(56, 60, 66));
        e.Graphics.FillEllipse(knobBrush, knobRect);
        e.Graphics.DrawEllipse(knobBorder, knobRect);

        if (ControlDrawing.ShouldDrawFocus(this, ShowFocusCues))
        {
            int focusGap = ControlDrawing.ScaleLogical(this, 3);
            Rectangle focusBounds = Rectangle.Inflate(knobRect, focusGap, focusGap);
            focusBounds.Intersect(Rectangle.Inflate(ClientRectangle, -1, -1));
            ControlDrawing.DrawFocusRing(e.Graphics, focusBounds, Math.Max(6, focusBounds.Height / 2), _palette);
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
        int horizontalInset = ControlDrawing.ScaleLogical(this, 12);
        int usableWidth = Math.Max(1, Width - (horizontalInset * 2));
        float ratio = Math.Clamp((float)(x - horizontalInset) / usableWidth, 0f, 1f);
        int rawValue = _minimum + (int)Math.Round((_maximum - _minimum) * ratio);
        Value = rawValue;
    }

    private int Snap(int value)
    {
        if (value <= _minimum)
        {
            return _minimum;
        }

        if (value >= _maximum)
        {
            return _maximum;
        }

        int normalized = value - _minimum;
        int snapped = (int)Math.Round(normalized / (double)_snapStep) * _snapStep;
        return Math.Clamp(_minimum + snapped, _minimum, _maximum);
    }

    private void SetValue(int value, bool snap)
    {
        int next = Math.Clamp(value, _minimum, _maximum);
        if (snap)
        {
            next = Snap(next);
        }

        if (_value == next)
        {
            return;
        }

        _value = next;
        Invalidate();
        AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new SliderAccessibleObject(this);

    private sealed class SliderAccessibleObject(ModernSlider owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.Slider;

        public override AccessibleStates State => base.State |
            AccessibleStates.Focusable |
            (owner.Enabled ? AccessibleStates.None : AccessibleStates.Unavailable);

        public override string? Value
        {
            get => owner.Value.ToString(CultureInfo.CurrentCulture);
            set
            {
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out int parsed))
                {
                    owner.Value = parsed;
                }
            }
        }
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
    private readonly int _accessoryPreferredWidth;
    private readonly string? _valueText;
    private readonly bool _compactDescription;
    private readonly string _baseAccessibleDescription;
    private bool _isStacked;
    private bool _updatingLayoutMetrics;

    public SettingsRow(ThemePalette palette, string title, string description, Control control, int rightColumnWidth = 220, string? valueText = null, bool compactDescription = false)
    {
        bool hasDescription = !string.IsNullOrWhiteSpace(description);
        _accessoryControl = control;
        _rightColumnWidth = Math.Max(96, rightColumnWidth);
        _accessoryPreferredWidth = control.Width;
        _valueText = valueText;
        _compactDescription = compactDescription;
        _baseAccessibleDescription = description;
        CornerRadius = 9;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Margin = new Padding(0, 0, 0, 8);
        Padding = hasDescription ? new Padding(14, 12, 14, 12) : new Padding(14, 11, 14, 11);
        MinimumSize = new Size(0, hasDescription ? 66 : 50);
        BackColor = palette.ControlBackground;
        AccessibleName = title;
        AccessibleDescription = description;
        AccessibleRole = AccessibleRole.Grouping;

        if (string.IsNullOrWhiteSpace(control.AccessibleName))
        {
            control.AccessibleName = title;
        }

        if (string.IsNullOrWhiteSpace(control.AccessibleDescription))
        {
            control.AccessibleDescription = description;
        }

        _grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = palette.ControlBackground,
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
            BackColor = palette.ControlBackground,
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
            Visible = false,
            AccessibleRole = AccessibleRole.Alert
        };

        _right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 1,
            BackColor = palette.ControlBackground,
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
        control.SizeChanged += (_, _) => UpdateLayoutMetrics();
        Click += (_, _) => ActivateFromSurface();
        _titleLabel.Click += (_, _) => ActivateFromSurface();
        _descriptionLabel.Click += (_, _) => ActivateFromSurface();
        _statusLabel.Click += (_, _) => ActivateFromSurface();
        if (control is ToggleSwitchControl)
        {
            Cursor = Cursors.Hand;
            _titleLabel.Cursor = Cursors.Hand;
            _descriptionLabel.Cursor = Cursors.Hand;
            _statusLabel.Cursor = Cursors.Hand;
        }
        UpdateLayoutMetrics();
        ApplyTheme(palette);
    }

    public void ApplyTheme(ThemePalette palette)
    {
        BackColor = palette.ControlBackground;
        _grid.BackColor = palette.ControlBackground;
        _left.BackColor = palette.ControlBackground;
        _right.BackColor = palette.ControlBackground;
        BorderAlpha = 20;
        _titleLabel.ForeColor = palette.Text;
        _descriptionLabel.ForeColor = palette.SecondaryText;
        Invalidate(true);
    }

    public void SetStatus(string? text, Color color)
    {
        string normalizedText = text ?? string.Empty;
        bool visible = !string.IsNullOrWhiteSpace(text);
        if (_statusLabel.Text == normalizedText &&
            _statusLabel.ForeColor == color &&
            _statusLabel.Visible == visible)
        {
            return;
        }

        _statusLabel.Text = normalizedText;
        _statusLabel.ForeColor = color;
        _statusLabel.Visible = visible;
        _statusLabel.AccessibleName = normalizedText;
        AccessibleDescription = visible
            ? string.Concat(_baseAccessibleDescription, Environment.NewLine, normalizedText)
            : _baseAccessibleDescription;
        AccessibilityNotifyClients(AccessibleEvents.DescriptionChange, -1);
        if (visible)
        {
            AccessibilityNotifyClients(AccessibleEvents.SystemAlert, -1);
        }
        UpdateLayoutMetrics();
    }

    public bool FocusAccessory()
    {
        Control? target = FindFirstFocusableControl(_accessoryControl);
        if (target == null)
        {
            return false;
        }

        target.Focus();
        return target.Focused || target.ContainsFocus;
    }

    private void ActivateFromSurface()
    {
        if (_accessoryControl is ToggleSwitchControl toggle)
        {
            toggle.Focus();
            toggle.PerformToggle();
            return;
        }

        FocusAccessory();
    }

    private static Control? FindFirstFocusableControl(Control root)
    {
        if (root.Visible && root.Enabled && root.CanSelect && root.TabStop)
        {
            return root;
        }

        foreach (Control child in root.Controls.Cast<Control>().OrderBy(control => control.TabIndex))
        {
            Control? target = FindFirstFocusableControl(child);
            if (target != null)
            {
                return target;
            }
        }

        return null;
    }

    private void UpdateLayoutMetrics()
    {
        if (_updatingLayoutMetrics || Width <= 0)
        {
            return;
        }

        _updatingLayoutMetrics = true;
        int availableWidth = Math.Max(1, Width - Padding.Horizontal);
        Size gridMaximumSize = new(availableWidth, 0);
        if (_grid.MaximumSize != gridMaximumSize)
        {
            _grid.MaximumSize = gridMaximumSize;
        }
        if (_grid.Width != availableWidth)
        {
            _grid.Width = availableWidth;
        }
        // Child controls report their current DPI-scaled width once parented.
        // Scaling the cached constructor width again made identical toggles
        // render at different widths depending on handle-creation timing.
        int accessoryPreferredWidth = Math.Max(_accessoryControl.Width, _accessoryPreferredWidth);
        // Dynamic page controls already report their current DPI-scaled width.
        // Scaling the requested column again produced large empty columns and
        // made rows stack much earlier than their content required.
        int effectiveRightColumnWidth = Math.Max(
            _rightColumnWidth,
            accessoryPreferredWidth + ControlDrawing.ScaleLogical(this, 10));
        float textScale = Math.Max(1f, ControlDrawing.UiFontScale);
        int stackThreshold = Math.Max(
            ControlDrawing.ScaleLogical(this, 560),
            effectiveRightColumnWidth + ControlDrawing.ScaleLogical(this, 280));
        bool shouldStack = availableWidth < stackThreshold || ControlDrawing.UiFontScale >= 1.55f;
        if (_isStacked != shouldStack)
        {
            _isStacked = shouldStack;
            _grid.SuspendLayout();
            _grid.ColumnStyles.Clear();
            _grid.RowStyles.Clear();
            if (_isStacked)
            {
                _grid.RowCount = 2;
                _grid.SetCellPosition(_left, new TableLayoutPanelCellPosition(0, 0));
                _grid.SetCellPosition(_right, new TableLayoutPanelCellPosition(0, 1));
                _grid.ColumnCount = 1;
                _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _left.Padding = new Padding(0, 2, 0, 2);
                _right.Padding = new Padding(0, 10, 0, 0);
                _accessoryControl.Margin = new Padding(0);
                _accessoryControl.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            }
            else
            {
                _grid.ColumnCount = 2;
                _grid.SetCellPosition(_left, new TableLayoutPanelCellPosition(0, 0));
                _grid.SetCellPosition(_right, new TableLayoutPanelCellPosition(1, 0));
                _grid.RowCount = 1;
                _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, effectiveRightColumnWidth));
                _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _left.Padding = new Padding(0, 2, 12, 2);
                _right.Padding = new Padding(0, 2, 0, 0);
                _accessoryControl.Margin = new Padding(10, 0, 0, 0);
                _accessoryControl.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            }

            _grid.ResumeLayout(performLayout: true);
        }

        int leftWidth = _isStacked
            ? availableWidth
            : Math.Max(ControlDrawing.ScaleLogical(this, 210), availableWidth - effectiveRightColumnWidth - ControlDrawing.ScaleLogical(this, 14));
        int descriptionWidth = _isStacked
            ? leftWidth
            : Math.Min(
                leftWidth,
                ControlDrawing.ScaleLogical(
                    this,
                    (int)Math.Round((_compactDescription ? 400 : 470) * textScale)));
        if (!_isStacked && _grid.ColumnStyles.Count > 1)
        {
            if ((int)Math.Round(_grid.ColumnStyles[1].Width) != effectiveRightColumnWidth)
            {
                _grid.ColumnStyles[1].Width = effectiveRightColumnWidth;
            }
        }

        Size titleMaximumSize = new(leftWidth, 0);
        Size descriptionMaximumSize = new(Math.Max(1, descriptionWidth), 0);
        if (_titleLabel.MaximumSize != titleMaximumSize)
        {
            _titleLabel.MaximumSize = titleMaximumSize;
        }

        if (_descriptionLabel.MaximumSize != descriptionMaximumSize)
        {
            _descriptionLabel.MaximumSize = descriptionMaximumSize;
        }

        if (_statusLabel.MaximumSize != descriptionMaximumSize)
        {
            _statusLabel.MaximumSize = descriptionMaximumSize;
        }

        if (!_accessoryControl.AutoSize && _accessoryPreferredWidth > 0)
        {
            int accessoryWidth = _isStacked
                ? Math.Min(accessoryPreferredWidth, availableWidth)
                : accessoryPreferredWidth;
            int nextAccessoryWidth = Math.Max(1, accessoryWidth);
            if (_accessoryControl.Width != nextAccessoryWidth)
            {
                _accessoryControl.Width = nextAccessoryWidth;
            }
        }

        if (_valueText != null && _accessoryControl is Label valueLabel)
        {
            Size valueMaximumSize = new(_isStacked ? availableWidth : effectiveRightColumnWidth, 0);
            if (valueLabel.MaximumSize != valueMaximumSize)
            {
                valueLabel.MaximumSize = valueMaximumSize;
            }
        }

        _updatingLayoutMetrics = false;
    }
}

internal sealed class SettingsSection : Panel
{
    private readonly TableLayoutPanel _layout;
    private readonly Label _titleLabel;
    private readonly Label _descriptionLabel;
    private readonly TableLayoutPanel _rows;
    private int _nextRowIndex;
    private int _updateDepth;
    private SettingsForm? _atomicUpdateOwner;

    public SettingsSection(ThemePalette palette, string title, string description)
    {
        bool hasTitle = !string.IsNullOrWhiteSpace(title);
        bool hasDescription = !string.IsNullOrWhiteSpace(description);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Dock = DockStyle.Top;
        Margin = new Padding(0, 0, 0, 16);
        Padding = new Padding(0);
        BackColor = palette.MenuBackground;

        _layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 1,
            BackColor = palette.MenuBackground,
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
            BackColor = palette.MenuBackground
        };
        _rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int rowIndex = 0;
        if (hasTitle)
        {
            _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _layout.Controls.Add(_titleLabel, 0, rowIndex++);
        }

        if (hasDescription)
        {
            _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _layout.Controls.Add(_descriptionLabel, 0, rowIndex++);
        }

        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.Controls.Add(_rows, 0, rowIndex);
        Controls.Add(_layout);
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

    public void BeginRowsUpdate()
    {
        if (_updateDepth++ != 0)
        {
            return;
        }

        _atomicUpdateOwner = FindForm() as SettingsForm;
        _atomicUpdateOwner?.BeginAtomicUpdate();
        SuspendLayout();
        _rows.SuspendLayout();
    }

    public void EndRowsUpdate()
    {
        if (_updateDepth <= 0 || --_updateDepth != 0)
        {
            return;
        }

        SettingsForm? atomicUpdateOwner = _atomicUpdateOwner;
        _atomicUpdateOwner = null;
        try
        {
            _rows.ResumeLayout(performLayout: true);
            ResumeLayout(performLayout: true);
            UpdateRowWidths();
        }
        finally
        {
            atomicUpdateOwner?.EndAtomicUpdate();
        }
    }

    private void UpdateRowWidths()
    {
        if (ClientSize.Width <= 0)
        {
            return;
        }

        int targetWidth = Math.Max(360, ClientSize.Width);
        foreach (Control row in _rows.Controls)
        {
            if (row.Width != targetWidth)
            {
                row.Width = targetWidth;
            }
        }
    }
}

internal class SettingsPageView : UserControl
{
    private readonly Label _titleLabel;
    private readonly Label _descriptionLabel;
    private readonly TableLayoutPanel _layout;
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

        _layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = palette.MenuBackground
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
            BackColor = palette.MenuBackground
        };
        _sectionHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int rowIndex = 0;
        if (hasTitle)
        {
            _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _layout.Controls.Add(_titleLabel, 0, rowIndex++);
        }

        if (hasDescription)
        {
            _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _layout.Controls.Add(_descriptionLabel, 0, rowIndex++);
        }

        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.Controls.Add(_sectionHost, 0, rowIndex);
        Controls.Add(_layout);
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
        if (ClientSize.Width <= 0)
        {
            return;
        }

        int targetWidth = Math.Max(400, ClientSize.Width);
        Size maximumSize = new(targetWidth, int.MaxValue);
        if (_layout.MaximumSize != maximumSize)
        {
            _layout.MaximumSize = maximumSize;
        }

        if (_layout.Width != targetWidth)
        {
            _layout.Width = targetWidth;
        }

        if (_sectionHost.MaximumSize != maximumSize)
        {
            _sectionHost.MaximumSize = maximumSize;
        }

        if (_sectionHost.Width != targetWidth)
        {
            _sectionHost.Width = targetWidth;
        }

        foreach (Control section in _sectionHost.Controls)
        {
            if (section.MaximumSize != maximumSize)
            {
                section.MaximumSize = maximumSize;
            }

            if (section.Width != targetWidth)
            {
                section.Width = targetWidth;
            }
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
    private readonly VScrollBar _verticalScrollBar;
    private readonly HashSet<Control> _scrollInputControls = new();
    private Control? _activePage;
    private Control? _lastLaidOutPage;
    private bool _resetScrollOnNextLayout;
    private bool _layoutInProgress;
    private bool _layoutQueued;
    private Size _lastLayoutClientSize;
    private int _contentHeight;

    public SettingsContentHost(ThemePalette palette)
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.ApplyThemingImplicitly,
            true);
        DoubleBuffered = true;
        BackColor = palette.MenuBackground;
        AutoScroll = false;
        TabStop = false;

        _verticalScrollBar = new VScrollBar
        {
            Dock = DockStyle.Right,
            SmallChange = 72,
            TabStop = false,
            Visible = false
        };
        _verticalScrollBar.ValueChanged += (_, _) => PositionActivePage();
        Controls.Add(_verticalScrollBar);
        WindowChrome.TrySetDarkScrollBars(_verticalScrollBar, palette.MenuBackground.GetBrightness() < 0.5f);
    }

    public int ScrollY => _verticalScrollBar.Visible ? _verticalScrollBar.Value : 0;

    internal Control? ActivePage => _activePage;

    public void RestoreScrollY(int scrollY)
    {
        LayoutActivePage(force: true);
        SetScrollY(scrollY);
    }

    public void EnsureControlVisible(Control control, bool ignoreVisibility = false)
    {
        if (_activePage == null || control.IsDisposed || (!ignoreVisibility && !control.Visible) || !IsHandleCreated)
        {
            return;
        }

        Rectangle bounds = RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
        int currentScrollY = ScrollY;
        int nextScrollY = currentScrollY;
        if (bounds.Top < 0)
        {
            nextScrollY += bounds.Top;
        }
        else if (bounds.Bottom > ClientSize.Height)
        {
            nextScrollY += bounds.Bottom - ClientSize.Height;
        }

        int maxScrollY = Math.Max(0, _contentHeight - ClientSize.Height);
        nextScrollY = Math.Clamp(nextScrollY, 0, maxScrollY);
        if (nextScrollY != currentScrollY)
        {
            SetScrollY(nextScrollY);
        }
    }

    public void SetActivePage(Control page)
    {
        if (_activePage != page)
        {
            _resetScrollOnNextLayout = true;
        }

        _activePage = page;
        page.Dock = DockStyle.None;
        page.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        AttachScrollInput(page);
        page.BringToFront();
        _verticalScrollBar.BringToFront();
        LayoutActivePage(force: true);
    }

    public void PreparePage(Control page)
    {
        AttachScrollInput(page);
        int pageWidth = Math.Max(400, ClientSize.Width);
        page.MinimumSize = new Size(pageWidth, 0);
        page.MaximumSize = new Size(pageWidth, int.MaxValue);
        page.Width = pageWidth;
        page.PerformLayout();
    }

    public void RefreshActivePageLayout()
    {
        LayoutActivePage(force: true);
    }

    public void RefreshActivePageLayoutIfNeeded()
    {
        LayoutActivePage();
    }

    public void BeginInteractiveResize()
    {
        QueueActivePageLayout();
    }

    public void EndInteractiveResize()
    {
        LayoutActivePage(force: true);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        ScrollFromWheel(e.Delta);
    }

    private void AttachScrollInput(Control control)
    {
        if (!_scrollInputControls.Add(control))
        {
            return;
        }

        control.MouseWheel += HandleChildMouseWheel;
        if (control is ScrollableControl or ContainerControl || control.HasChildren)
        {
            control.ControlAdded += HandleScrollControlAdded;
        }

        control.Disposed += HandleScrollControlDisposed;
        foreach (Control child in control.Controls)
        {
            AttachScrollInput(child);
        }
    }

    private void HandleScrollControlAdded(object? sender, ControlEventArgs e)
    {
        if (e.Control != null)
        {
            AttachScrollInput(e.Control);
        }
    }

    private void HandleScrollControlDisposed(object? sender, EventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        control.MouseWheel -= HandleChildMouseWheel;
        control.ControlAdded -= HandleScrollControlAdded;
        control.Disposed -= HandleScrollControlDisposed;
        _scrollInputControls.Remove(control);
    }

    private void HandleChildMouseWheel(object? sender, MouseEventArgs e)
    {
        ScrollFromWheel(e.Delta);
        if (e is HandledMouseEventArgs handledEvent)
        {
            handledEvent.Handled = true;
        }
    }

    private void ScrollFromWheel(int delta)
    {
        if (!_verticalScrollBar.Visible || delta == 0)
        {
            return;
        }

        SetScrollY(ScrollY - (Math.Sign(delta) * _verticalScrollBar.SmallChange));
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        QueueActivePageLayout();
    }

    private void QueueActivePageLayout()
    {
        if (_layoutQueued || IsDisposed)
        {
            return;
        }

        if (!IsHandleCreated)
        {
            LayoutActivePage();
            return;
        }

        _layoutQueued = true;
        try
        {
            BeginInvoke((MethodInvoker)(() =>
            {
                _layoutQueued = false;
                if (!IsDisposed)
                {
                    LayoutActivePage();
                }
            }));
        }
        catch (InvalidOperationException)
        {
            _layoutQueued = false;
        }
    }

    private void LayoutActivePage(bool force = false)
    {
        if (_layoutInProgress ||
            _activePage == null ||
            ClientSize.Width <= 0 ||
            ClientSize.Height <= 0 ||
            (!force && !_resetScrollOnNextLayout && _lastLaidOutPage == _activePage && _lastLayoutClientSize == ClientSize))
        {
            return;
        }

        _layoutInProgress = true;
        int previousScrollY = _resetScrollOnNextLayout ? 0 : ScrollY;
        SuspendLayout();
        try
        {
            int pageWidth = Math.Max(400, ClientSize.Width);
            int pageHeight = LayoutPage(pageWidth);
            int overflowTolerance = ControlDrawing.ScaleLogical(this, 2);
            bool needsScrollBar = pageHeight > ClientSize.Height + overflowTolerance;
            if (needsScrollBar)
            {
                pageWidth = Math.Max(400, ClientSize.Width - _verticalScrollBar.Width - GetScrollBarGap());
                pageHeight = LayoutPage(pageWidth);
            }

            _contentHeight = Math.Max(1, pageHeight);
            _activePage.Size = new Size(pageWidth, Math.Max(1, pageHeight));
            _verticalScrollBar.LargeChange = Math.Max(1, ClientSize.Height);
            _verticalScrollBar.Maximum = Math.Max(0, _contentHeight - 1);
            _verticalScrollBar.Bounds = new Rectangle(
                Math.Max(0, ClientSize.Width - _verticalScrollBar.Width),
                0,
                _verticalScrollBar.Width,
                ClientSize.Height);
            _verticalScrollBar.Visible = needsScrollBar;
            SetScrollY(needsScrollBar ? previousScrollY : 0);
            PositionActivePage();
            _verticalScrollBar.BringToFront();
        }
        finally
        {
            ResumeLayout(performLayout: false);
            _layoutInProgress = false;
            _resetScrollOnNextLayout = false;
            _lastLaidOutPage = _activePage;
            _lastLayoutClientSize = ClientSize;
            Invalidate(invalidateChildren: true);
        }
    }

    private int LayoutPage(int pageWidth)
    {
        if (_activePage == null)
        {
            return 0;
        }

        Size minimumSize = new(pageWidth, 0);
        Size maximumSize = new(pageWidth, int.MaxValue);
        if (_activePage.MinimumSize != minimumSize)
        {
            _activePage.MinimumSize = minimumSize;
        }

        if (_activePage.MaximumSize != maximumSize)
        {
            _activePage.MaximumSize = maximumSize;
        }

        if (_activePage.Width != pageWidth)
        {
            _activePage.Width = pageWidth;
        }

        _activePage.PerformLayout();
        int childBottom = GetChildBottom(_activePage);
        return Math.Max(1, childBottom);
    }

    private int GetScrollBarGap() => ControlDrawing.ScaleLogical(this, 12);

    private void SetScrollY(int scrollY)
    {
        int maxScrollY = Math.Max(0, _contentHeight - ClientSize.Height);
        int nextScrollY = Math.Clamp(scrollY, 0, maxScrollY);
        if (_verticalScrollBar.Value != nextScrollY)
        {
            _verticalScrollBar.Value = nextScrollY;
            return;
        }

        PositionActivePage();
    }

    private void PositionActivePage()
    {
        if (_activePage == null)
        {
            return;
        }

        Point nextLocation = new(0, -ScrollY);
        if (_activePage.Location != nextLocation)
        {
            _activePage.Location = nextLocation;
        }

    }

    private static int GetChildBottom(Control control)
    {
        int childBottom = 0;
        foreach (Control child in control.Controls)
        {
            if (!child.Visible)
            {
                continue;
            }

            childBottom = Math.Max(childBottom, child.Bottom + child.Margin.Bottom);
        }

        return childBottom;
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

internal readonly record struct SettingsUiState(
    Type? PageType,
    string? ActiveControlAccessibleName,
    int[] ActiveControlPath,
    int ScrollY);

internal sealed class SettingsForm : Form
{
    private readonly Dictionary<Type, UserControl> _pageCache = new();
    private readonly Dictionary<Type, SettingsSidebarItem> _navItems = new();
    private readonly Dictionary<Type, Func<UserControl>> _pageFactories = new();
    private readonly HashSet<Control> _focusTrackedControls = new();
    private readonly ThemePalette _palette;
    private readonly SettingsContentHost _contentHost;
    private readonly SettingsSearchControl _searchControl;
    private readonly System.Windows.Forms.Timer _searchHighlightTimer = new() { Interval = 2400 };
    private readonly Size _minimumClientSize;
    private Type? _currentPageType;
    private ModernSurfacePanel? _highlightedSearchTarget;
    private Control? _pendingFocusScrollControl;
    private bool _focusScrollQueued;
    private bool _allowClose;
    private bool _atomicRedrawSuspended;
    private int _atomicUpdateDepth;

    internal bool CaptureMode { get; set; }

    public SettingsForm(
        ThemePalette palette,
        bool useDarkTheme,
        string title,
        Size clientSize,
        string appName,
        string doneText,
        ModernButton resetButton,
        IReadOnlyList<SettingsPageDefinition> pages,
        IReadOnlyList<SettingsSearchEntry> searchEntries,
        string searchPlaceholder,
        string searchNoResults,
        string searchAccessibleDescription)
    {
        _palette = palette;
        _searchHighlightTimer.Tick += (_, _) => ClearSearchTargetHighlight();
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        Text = title;
        StartPosition = FormStartPosition.Manual;
        ClientSize = clientSize;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = true;
        MaximizeBox = true;
        ShowInTaskbar = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = palette.MenuBackground;
        ForeColor = palette.Text;
        KeyPreview = true;
        _minimumClientSize = clientSize;
        UpdateMinimumWindowSize();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(8),
            Margin = new Padding(0),
            BackColor = palette.MenuBackground
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var sidebarSurface = new ModernSurfacePanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 10,
            BorderAlpha = 14,
            Margin = new Padding(0, 0, 6, 0),
            Padding = new Padding(5, 6, 5, 6),
            BackColor = AccessibilityPreferences.HighContrast
                ? palette.ControlBackground
                : (useDarkTheme ? Color.FromArgb(17, 20, 26) : palette.ControlBackground)
        };

        var sidebarLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = sidebarSurface.BackColor
        };
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var sidebarHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 6),
            Padding = new Padding(0),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent
        };
        sidebarHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var appNameLabel = new Label
        {
            Text = appName,
            AutoSize = true,
            Font = ControlDrawing.UiFont("Segoe UI Semibold", 15f, FontStyle.Bold),
            Margin = new Padding(0),
            ForeColor = palette.Text,
            BackColor = Color.Transparent
        };
        sidebarHeader.Controls.Add(appNameLabel, 0, 0);

        var navHost = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = Color.Transparent,
            AccessibleName = appName,
            AccessibleRole = AccessibleRole.PageTabList
        };

        var sidebarToolTip = new ToolTip
        {
            AutoPopDelay = 5000,
            InitialDelay = 350,
            ReshowDelay = 100,
            ShowAlways = true
        };
        Disposed += (_, _) => sidebarToolTip.Dispose();

        foreach (SettingsPageDefinition page in pages)
        {
            _pageFactories[page.PageType] = page.CreatePage;
            var item = new SettingsSidebarItem(palette, page.Title, page.Icon);
            Type pageType = page.PageType;
            item.Click += (_, _) => ShowPage(pageType);
            item.ContentNavigationRequested += (_, _) => FocusActivePageFromSidebar();
            _navItems[pageType] = item;
            navHost.Controls.Add(item);
            sidebarToolTip.SetToolTip(item, page.Title);
        }

        bool compactLayout = false;
        bool updatingSidebarItemWidths = false;
        int lastSidebarItemWidth = -1;
        void UpdateSidebarItemWidths()
        {
            if (updatingSidebarItemWidths)
            {
                return;
            }

            updatingSidebarItemWidths = true;
            try
            {
                int availableWidth = compactLayout
                    ? ControlDrawing.ScaleLogical(this, 40)
                    : Math.Max(1, navHost.ClientSize.Width - ControlDrawing.ScaleLogical(this, 3));
                if (availableWidth == lastSidebarItemWidth)
                {
                    return;
                }

                lastSidebarItemWidth = availableWidth;

                foreach (SettingsSidebarItem item in _navItems.Values)
                {
                    item.Width = availableWidth;
                }
            }
            finally
            {
                updatingSidebarItemWidths = false;
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
            BackColor = palette.MenuBackground,
            TabIndex = 1
        };

        _searchControl = new SettingsSearchControl(
            palette,
            searchEntries,
            searchPlaceholder,
            searchNoResults,
            searchAccessibleDescription)
        {
            Dock = DockStyle.Top,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
            TabIndex = 0
        };
        _searchControl.ResultActivated += (_, entry) => NavigateToSearchResult(entry);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 8, 0, 0),
            Padding = new Padding(0),
            BackColor = palette.MenuBackground
        };
        var closeButton = new ModernButton
        {
            Text = doneText,
            DialogResult = DialogResult.OK,
            AutoSize = false,
            MinimumSize = Size.Empty
        };
        resetButton.AutoSize = false;
        resetButton.MinimumSize = Size.Empty;
        closeButton.ApplySuccessOutlineTheme(palette);
        closeButton.Click += (_, _) => Close();
        footer.Controls.Add(closeButton);
        footer.Controls.Add(resetButton);

        void UpdateFooterButtonMetrics()
        {
            if (IsDisposed || closeButton.IsDisposed || resetButton.IsDisposed)
            {
                return;
            }

            int horizontalPadding = ControlDrawing.ScaleLogical(this, compactLayout ? 24 : 32);
            int minimumWidth = ControlDrawing.ScaleLogical(this, compactLayout ? 120 : 150);
            int minimumHeight = ControlDrawing.ScaleLogical(this, compactLayout ? 40 : 44);
            Size doneTextSize = TextRenderer.MeasureText(
                closeButton.Text,
                closeButton.Font,
                Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            Size resetTextSize = TextRenderer.MeasureText(
                resetButton.Text,
                resetButton.Font,
                Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            int buttonWidth = Math.Max(
                minimumWidth,
                Math.Max(doneTextSize.Width, resetTextSize.Width) + horizontalPadding);
            int buttonHeight = Math.Max(
                minimumHeight,
                Math.Max(doneTextSize.Height, resetTextSize.Height) + ControlDrawing.ScaleLogical(this, 18));
            Size buttonSize = new(buttonWidth, buttonHeight);

            if (closeButton.Size != buttonSize)
            {
                closeButton.Size = buttonSize;
            }

            if (resetButton.Size != buttonSize)
            {
                resetButton.Size = buttonSize;
            }
        }

        resetButton.TextChanged += (_, _) => UpdateFooterButtonMetrics();
        UpdateFooterButtonMetrics();

        var rightLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = palette.MenuBackground,
            TabIndex = 1
        };
        rightLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rightLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rightLayout.Controls.Add(_searchControl, 0, 0);
        rightLayout.Controls.Add(_contentHost, 0, 1);
        rightLayout.Controls.Add(footer, 0, 2);

        root.Controls.Add(sidebarSurface, 0, 0);
        root.Controls.Add(rightLayout, 1, 0);
        Controls.Add(root);

        bool updatingSidebarMetrics = false;
        void ApplySidebarMetrics()
        {
            if (updatingSidebarMetrics || ClientSize.Width <= 0 || root.ColumnStyles.Count < 2)
            {
                return;
            }

            updatingSidebarMetrics = true;
            try
            {
                bool nextCompactLayout = ClientSize.Width < ControlDrawing.ScaleLogical(this, 700);
                if (compactLayout != nextCompactLayout)
                {
                    compactLayout = nextCompactLayout;
                    root.Padding = new Padding(ControlDrawing.ScaleLogical(this, compactLayout ? 5 : 8));
                    sidebarSurface.Margin = new Padding(
                        0,
                        0,
                        ControlDrawing.ScaleLogical(this, compactLayout ? 4 : 6),
                        0);
                    sidebarSurface.Padding = new Padding(
                        ControlDrawing.ScaleLogical(this, compactLayout ? 2 : 5),
                        ControlDrawing.ScaleLogical(this, compactLayout ? 4 : 6),
                        ControlDrawing.ScaleLogical(this, compactLayout ? 2 : 5),
                        ControlDrawing.ScaleLogical(this, compactLayout ? 4 : 6));
                    sidebarHeader.Visible = !compactLayout;
                    foreach (SettingsSidebarItem item in _navItems.Values)
                    {
                        item.Compact = compactLayout;
                    }

                    lastSidebarItemWidth = -1;
                    UpdateFooterButtonMetrics();
                }

                int sidebarWidth = compactLayout
                    ? ControlDrawing.ScaleLogical(this, 64)
                    : Math.Clamp(
                        (int)Math.Round(ClientSize.Width * 0.24),
                        ControlDrawing.ScaleLogical(this, 190),
                        ControlDrawing.ScaleLogical(this, 230));
                if ((int)Math.Round(root.ColumnStyles[0].Width) != sidebarWidth)
                {
                    root.ColumnStyles[0].Width = sidebarWidth;
                }

                UpdateSidebarItemWidths();
            }
            finally
            {
                updatingSidebarMetrics = false;
            }
        }

        ResizeBegin += (_, _) =>
        {
            _contentHost.BeginInteractiveResize();
        };
        ResizeEnd += (_, _) =>
        {
            ApplySidebarMetrics();
            UpdateFooterButtonMetrics();
            _contentHost.EndInteractiveResize();
        };
        Resize += (_, _) =>
        {
            ApplySidebarMetrics();
            _contentHost.RefreshActivePageLayoutIfNeeded();
        };
        LocationChanged += (_, _) => UpdateMinimumWindowSize();
        DpiChanged += (_, _) =>
        {
            lastSidebarItemWidth = -1;
            UpdateMinimumWindowSize();
            if (IsHandleCreated)
            {
                BeginInvoke((MethodInvoker)(() =>
                {
                    ApplySidebarMetrics();
                    UpdateFooterButtonMetrics();
                }));
            }
        };
        AcceptButton = closeButton;
        Shown += (_, _) =>
        {
            ApplySidebarMetrics();
            UpdateFooterButtonMetrics();
            if (!CaptureMode)
            {
                PrepareForKeyboardEntry();
            }
        };
        ApplySidebarMetrics();
    }

    internal void PrepareForKeyboardEntry()
    {
        ActiveControl = null;
    }

    private void UpdateMinimumWindowSize()
    {
        Rectangle area = IsHandleCreated
            ? Screen.FromControl(this).WorkingArea
            : Screen.FromPoint(Cursor.Position).WorkingArea;
        int screenInset = ControlDrawing.ScaleLogical(this, 32);
        int maximumWidth = Math.Max(640, area.Width - screenInset);
        int maximumHeight = Math.Max(480, area.Height - screenInset);
        Size nextMinimumSize = new(
            Math.Min(ControlDrawing.ScaleLogical(this, 520), maximumWidth),
            Math.Min(ControlDrawing.ScaleLogical(this, 400), maximumHeight));
        if (MinimumSize != nextMinimumSize)
        {
            MinimumSize = nextMinimumSize;
        }
    }

    public event EventHandler<Type>? PageShown;

    public void RenderBeforeReveal()
    {
        PerformLayout();
        _contentHost.RefreshActivePageLayout();
        WindowChrome.RedrawNow(this);
    }

    internal void PrepareForCapture()
    {
        EnsureHandles(this);
        RenderBeforeReveal();

        static void EnsureHandles(Control root)
        {
            _ = root.Handle;
            foreach (Control child in root.Controls)
            {
                EnsureHandles(child);
            }
        }
    }

    internal void PrepareInteractionCapture(string query)
    {
        _searchControl.SetQueryForCapture(query, scrollToBottom: true);
        foreach (SettingsSidebarItem item in _navItems.Values)
        {
            item.SetHoveredForCapture(hovered: false);
        }

        _navItems.Values.FirstOrDefault(item => !item.Selected)?.SetHoveredForCapture(hovered: true);
    }

    internal void EnsureControlVisibleForCapture(Control control)
    {
        _ = _contentHost.Handle;
        _contentHost.EnsureControlVisible(control, ignoreVisibility: true);
        PerformLayout();
        _contentHost.RefreshActivePageLayout();
    }

    internal bool PrepareControlStateCapture()
    {
        Control? activePage = _contentHost.ActivePage;
        if (activePage == null)
        {
            return false;
        }

        int changedControlCount = 0;
        int sliderIndex = 0;
        foreach (Control control in EnumerateDescendants(activePage))
        {
            if (control is ToggleSwitchControl toggle && toggle.Enabled)
            {
                toggle.SetStateForCapture(!toggle.IsOn);
                changedControlCount++;
            }
            else if (control is ModernSlider slider && slider.Enabled)
            {
                double ratio = sliderIndex++ % 2 == 0 ? 0.3d : 0.7d;
                int value = slider.Minimum + (int)Math.Round((slider.Maximum - slider.Minimum) * ratio);
                slider.SetValueForCapture(value);
                changedControlCount++;
            }
        }

        PerformLayout();
        _contentHost.RefreshActivePageLayout();
        return changedControlCount > 0;

        static IEnumerable<Control> EnumerateDescendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (Control descendant in EnumerateDescendants(child))
                {
                    yield return descendant;
                }
            }
        }
    }

    internal void ValidateNumericInputsForCapture()
    {
        Control? activePage = _contentHost.ActivePage;
        if (activePage == null)
        {
            return;
        }

        foreach (CompactNumericTextBox input in EnumerateDescendants(activePage).OfType<CompactNumericTextBox>())
        {
            Func<int, string>? formatter = input.ValueFormatter;
            int topInset = input.Top;
            int bottomInset = input.Parent == null ? topInset : input.Parent.ClientSize.Height - input.Bottom;
            if (formatter == null ||
                !input.AcceptsValueText(formatter(input.MinimumValue)) ||
                !input.AcceptsValueText(formatter(input.MaximumValue)) ||
                !input.AcceptsValueText(input.Text) ||
                input.AcceptsValueText($"invalid{input.MinimumValue}") ||
                input.AcceptsValueText((input.MinimumValue - 1).ToString(CultureInfo.CurrentCulture)) ||
                input.AcceptsValueText((input.MaximumValue + 1).ToString(CultureInfo.CurrentCulture)) ||
                input.Parent == null ||
                input.Parent.Width > 92 ||
                Math.Abs(topInset - bottomInset) > 1)
            {
                throw new InvalidOperationException(
                    $"Numeric input '{input.AccessibleName}' failed its range-validation audit.");
            }

            if (!input.TryGetNumericValue(out int currentValue))
            {
                throw new InvalidOperationException(
                    $"Numeric input '{input.AccessibleName}' did not expose its current value.");
            }

            input.Text = currentValue.ToString(CultureInfo.CurrentCulture);
            Message enterMessage = Message.Create(input.Handle, 0x0100, (nint)Keys.Enter, IntPtr.Zero);
            if (!input.PreProcessMessage(ref enterMessage) ||
                !string.Equals(input.Text, formatter(currentValue), StringComparison.CurrentCulture))
            {
                throw new InvalidOperationException(
                    $"Numeric input '{input.AccessibleName}' did not apply Enter without closing.");
            }

            int alternateValue = currentValue == input.MinimumValue ? input.MaximumValue : input.MinimumValue;
            input.Text = alternateValue.ToString(CultureInfo.CurrentCulture);
            Message escapeMessage = Message.Create(input.Handle, 0x0100, (nint)Keys.Escape, IntPtr.Zero);
            if (!input.PreProcessMessage(ref escapeMessage) ||
                !string.Equals(input.Text, formatter(currentValue), StringComparison.CurrentCulture))
            {
                throw new InvalidOperationException(
                    $"Numeric input '{input.AccessibleName}' did not restore its value on Escape.");
            }
        }

        static IEnumerable<Control> EnumerateDescendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (Control descendant in EnumerateDescendants(child))
                {
                    yield return descendant;
                }
            }
        }
    }

    internal void ValidateDropdownWidthsForCapture()
    {
        foreach (ModernDropdown dropdown in EnumerateDescendants(this).OfType<ModernDropdown>())
        {
            if (!dropdown.Visible || dropdown.Items.Count == 0)
            {
                continue;
            }

            int requiredWidth = dropdown.GetPreferredSize(Size.Empty).Width;
            if (dropdown.Width < requiredWidth)
            {
                throw new InvalidOperationException(
                    $"Dropdown '{dropdown.AccessibleName}' is {dropdown.Width}px wide but requires {requiredWidth}px for its localized labels.");
            }
        }

        static IEnumerable<Control> EnumerateDescendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (Control descendant in EnumerateDescendants(child))
                {
                    yield return descendant;
                }
            }
        }
    }

    internal void BeginAtomicUpdate()
    {
        if (_atomicUpdateDepth++ != 0)
        {
            return;
        }

        _atomicRedrawSuspended = WindowChrome.TrySetRedraw(_contentHost, enabled: false);
        _contentHost.SuspendLayout();
    }

    internal void EndAtomicUpdate()
    {
        if (_atomicUpdateDepth <= 0 || --_atomicUpdateDepth != 0)
        {
            return;
        }

        try
        {
            _contentHost.ResumeLayout(performLayout: false);
            _contentHost.RefreshActivePageLayout();
        }
        finally
        {
            if (_atomicRedrawSuspended)
            {
                _ = WindowChrome.TrySetRedraw(_contentHost, enabled: true);
                _atomicRedrawSuspended = false;
            }

            WindowChrome.RedrawNow(_contentHost);
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (TryHandleWindowKey(keyData))
        {
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (TryHandleWindowKey(keyData))
        {
            return true;
        }

        return base.ProcessDialogKey(keyData);
    }

    private bool TryHandleWindowKey(Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        bool controlPressed = (keyData & Keys.Control) == Keys.Control;
        if (controlPressed && key == Keys.F)
        {
            _searchControl.FocusSearch();
            return true;
        }

        if (key == Keys.Escape)
        {
            if (_searchControl.TryDismiss())
            {
                return true;
            }

            Control? focusedControl = FindFocusedControl(this);
            if (focusedControl is TextBox && !_searchControl.ContainsSearchFocus)
            {
                return false;
            }

            Close();
            return true;
        }

        Keys sidebarReturnKey = RightToLeft == RightToLeft.Yes ? Keys.Right : Keys.Left;
        if (key == sidebarReturnKey && !controlPressed && (keyData & Keys.Alt) != Keys.Alt)
        {
            Control? focusedControl = FindFocusedControl(this);
            if (focusedControl != null &&
                !KeepsHorizontalArrowKeys(focusedControl) &&
                IsDescendantOf(focusedControl, _contentHost))
            {
                return FocusCurrentSidebarItem();
            }
        }

        if (key is not (Keys.Up or Keys.Down) || controlPressed || (keyData & Keys.Alt) == Keys.Alt)
        {
            return false;
        }

        Control? focused = FindFocusedControl(this);
        if (focused is not (ToggleSwitchControl or KeyBadgeControl or ModernButton))
        {
            return false;
        }

        return SelectNextControl(
            focused,
            forward: key == Keys.Down,
            tabStopOnly: true,
            nested: true,
            wrap: true);
    }

    private static bool KeepsHorizontalArrowKeys(Control control)
    {
        return control is TrayModeButton or ModernSlider or TextBoxBase;
    }

    private bool FocusActivePageFromSidebar()
    {
        if (_currentPageType == null ||
            !_pageCache.TryGetValue(_currentPageType, out UserControl? page))
        {
            return false;
        }

        _contentHost.RestoreScrollY(0);
        bool focused = FocusFirstFocusableDescendant(page);
        if (focused)
        {
            Control? target = FindFocusedControl(page);
            if (target != null)
            {
                _contentHost.EnsureControlVisible(target);
            }
        }

        return focused;
    }

    private bool FocusCurrentSidebarItem()
    {
        if (_currentPageType == null ||
            !_navItems.TryGetValue(_currentPageType, out SettingsSidebarItem? item) ||
            !item.CanFocus)
        {
            return false;
        }

        item.Focus();
        item.Select();
        return item.Focused || ReferenceEquals(FindActiveControl(this), item);
    }

    internal bool FocusSidebarFromContent() => FocusCurrentSidebarItem();

    internal Control PrepareFirstTabCapture()
    {
        PrepareForKeyboardEntry();
        bool moved = SelectNextControl(null, forward: true, tabStopOnly: true, nested: true, wrap: false);
        Control? active = FindFocusedControl(this) ?? FindActiveControl(this);
        if (!moved ||
            active is not SettingsSidebarItem item ||
            !item.Selected)
        {
            throw new InvalidOperationException(
                $"The first Tab did not focus the active settings navigation item. " +
                $"Moved={moved}; Active={active?.GetType().Name ?? "none"}; " +
                $"Name={active?.AccessibleName ?? "none"}.");
        }

        return item;
    }

    internal Control PrepareSidebarContentEntryCapture()
    {
        if ((FindFocusedControl(this) ?? FindActiveControl(this)) is not SettingsSidebarItem item)
        {
            throw new InvalidOperationException("The settings navigation item was not focused before content entry.");
        }

        item.EnterContentForCapture();
        Control? focused = FindFocusedControl(this) ?? FindActiveControl(this);
        if (focused == null || !IsDescendantOf(focused, _contentHost))
        {
            throw new InvalidOperationException("The sidebar arrow action did not enter the active settings page.");
        }

        return focused;
    }

    internal Control PrepareSidebarReturnCapture()
    {
        Control? active = FindFocusedControl(this) ?? FindActiveControl(this);
        if (active is TrayModeButton modeButton)
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                modeButton.NavigateForCapture(Keys.Left);
                active = FindFocusedControl(this) ?? FindActiveControl(this);
                if (active is SettingsSidebarItem)
                {
                    break;
                }

                if (active is not TrayModeButton nextModeButton)
                {
                    break;
                }

                modeButton = nextModeButton;
            }
        }
        else
        {
            _ = TryHandleWindowKey(Keys.Left);
        }

        if ((FindFocusedControl(this) ?? FindActiveControl(this)) is not SettingsSidebarItem item)
        {
            throw new InvalidOperationException("The content arrow action did not return to settings navigation.");
        }

        return item;
    }

    internal Control PrepareModeLeftArrowCapture(string fromAccessibleName, string expectedAccessibleName)
    {
        if (FindByAccessibleName(_contentHost, fromAccessibleName) is not TrayModeButton fromButton ||
            FindByAccessibleName(_contentHost, expectedAccessibleName) is not TrayModeButton expectedButton)
        {
            throw new InvalidOperationException("The requested mode buttons were not available for keyboard capture.");
        }

        TrayModeButton? selectedButton = FindModeButtons(_contentHost).FirstOrDefault(button => button.Selected);
        if (selectedButton == null)
        {
            throw new InvalidOperationException("The mode selector did not have a selected option before keyboard navigation.");
        }

        fromButton.Select();
        fromButton.Focus();
        if (TryHandleWindowKey(Keys.Left))
        {
            throw new InvalidOperationException("The settings window intercepted the mode selector's Left arrow.");
        }

        fromButton.NavigateForCapture(Keys.Left);

        Control? active = FindFocusedControl(this) ?? FindActiveControl(this);
        if (!ReferenceEquals(active, expectedButton))
        {
            throw new InvalidOperationException(
                $"Left arrow focused '{active?.AccessibleName ?? "none"}' instead of '{expectedAccessibleName}'.");
        }

        if (!selectedButton.Selected || expectedButton.Selected != ReferenceEquals(expectedButton, selectedButton))
        {
            throw new InvalidOperationException("Arrow navigation changed the selected zoom mode before activation.");
        }

        return expectedButton;

        static IEnumerable<TrayModeButton> FindModeButtons(Control root)
        {
            foreach (Control child in root.Controls)
            {
                if (child is TrayModeButton button)
                {
                    yield return button;
                }

                foreach (TrayModeButton descendant in FindModeButtons(child))
                {
                    yield return descendant;
                }
            }
        }
    }

    internal ModernSlider PrepareSliderArrowCapture(out int originalValue)
    {
        ModernSlider? slider = FindFirstVisibleSlider(_contentHost);
        if (slider == null || slider.Minimum == slider.Maximum)
        {
            throw new InvalidOperationException("No adjustable slider was available for keyboard capture.");
        }

        slider.Select();
        slider.Focus();
        originalValue = slider.Value;
        Keys increaseKey = originalValue < slider.Maximum ? Keys.Up : Keys.Down;
        Keys restoreKey = increaseKey == Keys.Up ? Keys.Down : Keys.Up;

        if (TryHandleWindowKey(increaseKey))
        {
            throw new InvalidOperationException("The settings window intercepted the slider's vertical arrow key.");
        }

        slider.NavigateForCapture(increaseKey);
        int changedValue = slider.Value;
        if (changedValue == originalValue)
        {
            throw new InvalidOperationException("The focused slider did not respond to a vertical arrow key.");
        }

        slider.NavigateForCapture(restoreKey);
        if (slider.Value != originalValue)
        {
            throw new InvalidOperationException("Opposite vertical slider arrows did not restore the original value.");
        }

        Keys horizontalKey = originalValue < slider.Maximum ? Keys.Right : Keys.Left;
        if (TryHandleWindowKey(horizontalKey))
        {
            throw new InvalidOperationException("The settings window intercepted the slider's horizontal arrow key.");
        }

        slider.NavigateForCapture(horizontalKey);
        if (slider.Value == originalValue)
        {
            throw new InvalidOperationException("The focused slider did not respond to a horizontal arrow key.");
        }

        Control? active = FindFocusedControl(this) ?? FindActiveControl(this);
        if (!ReferenceEquals(active, slider))
        {
            throw new InvalidOperationException("Slider arrow navigation moved focus away from the slider.");
        }

        return slider;

        static ModernSlider? FindFirstVisibleSlider(Control root)
        {
            foreach (Control child in root.Controls)
            {
                if (child is ModernSlider slider && slider.Visible && slider.Enabled)
                {
                    return slider;
                }

                ModernSlider? descendant = FindFirstVisibleSlider(child);
                if (descendant != null)
                {
                    return descendant;
                }
            }

            return null;
        }
    }

    private void NavigateToSearchResult(SettingsSearchEntry entry)
    {
        ShowPage(entry.PageType);
        BeginInvoke((MethodInvoker)(() =>
        {
            if (IsDisposed || !_pageCache.TryGetValue(entry.PageType, out UserControl? page))
            {
                return;
            }

            Control? target = FindByAccessibleName(page, entry.Title);
            if (target != null)
            {
                ShowSearchTargetHighlight(FindSurfaceAncestor(target));
                _contentHost.EnsureControlVisible(target);
                bool focused = target is SettingsRow row
                    ? row.FocusAccessory()
                    : FocusFirstFocusableDescendant(target);
                if (focused)
                {
                    _contentHost.EnsureControlVisible(target);
                    return;
                }
            }

            if (entry.PageType == typeof(ZoomSettingsPageView) && FocusFirstFocusableDescendant(page))
            {
                return;
            }

            if (_navItems.TryGetValue(entry.PageType, out SettingsSidebarItem? navItem))
            {
                navItem.Focus();
            }
        }));
    }

    private static ModernSurfacePanel? FindSurfaceAncestor(Control? control)
    {
        for (Control? current = control; current != null; current = current.Parent)
        {
            if (current is ModernSurfacePanel surface)
            {
                return surface;
            }
        }

        return null;
    }

    private void ShowSearchTargetHighlight(ModernSurfacePanel? target)
    {
        ClearSearchTargetHighlight();
        if (target == null || target.IsDisposed)
        {
            return;
        }

        _highlightedSearchTarget = target;
        target.SearchTargetHighlighted = true;
        _searchHighlightTimer.Start();
    }

    private void ClearSearchTargetHighlight()
    {
        _searchHighlightTimer.Stop();
        if (_highlightedSearchTarget is { IsDisposed: false } target)
        {
            target.SearchTargetHighlighted = false;
        }

        _highlightedSearchTarget = null;
    }

    private static bool FocusFirstFocusableDescendant(Control root)
    {
        if (root.Visible && root.Enabled && root.CanSelect && root.TabStop)
        {
            root.Select();
            root.Focus();
            return root.Focused || root.ContainsFocus ||
                ReferenceEquals(FindActiveControl(root.FindForm()), root);
        }

        foreach (Control child in root.Controls.Cast<Control>().OrderBy(control => control.TabIndex))
        {
            if (FocusFirstFocusableDescendant(child))
            {
                return true;
            }
        }

        return false;
    }

    private static Control? FindActiveControl(ContainerControl? root)
    {
        Control? active = root?.ActiveControl;
        while (active is ContainerControl container && container.ActiveControl != null)
        {
            active = container.ActiveControl;
        }

        return active;
    }

    public SettingsUiState CaptureUiState()
    {
        Control? activeControl = FindFocusedControl(this);
        return new SettingsUiState(
            _currentPageType,
            activeControl?.AccessibleName,
            BuildControlPath(activeControl),
            _contentHost.ScrollY);
    }

    public void RestoreUiState(SettingsUiState state)
    {
        if (state.PageType != null && _pageFactories.ContainsKey(state.PageType))
        {
            ShowPage(state.PageType);
        }

        void ApplyState()
        {
            if (IsDisposed)
            {
                return;
            }

            Control? focusTarget = ResolveControlPath(state.ActiveControlPath);
            if (focusTarget == null && !string.IsNullOrWhiteSpace(state.ActiveControlAccessibleName))
            {
                focusTarget = FindByAccessibleName(this, state.ActiveControlAccessibleName);
            }

            if (focusTarget?.CanFocus == true)
            {
                focusTarget.Focus();
            }

            _contentHost.RestoreScrollY(state.ScrollY);
        }

        if (IsHandleCreated)
        {
            BeginInvoke((MethodInvoker)ApplyState);
        }
        else
        {
            EventHandler? shownHandler = null;
            shownHandler = (_, _) =>
            {
                Shown -= shownHandler;
                BeginInvoke((MethodInvoker)ApplyState);
            };
            Shown += shownHandler;
        }
    }

    public void ClosePermanently()
    {
        _allowClose = true;
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ClearSearchTargetHighlight();
            _searchHighlightTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            DialogResult = DialogResult.None;
            Hide();
        }

        base.OnFormClosing(e);
    }

    private static Control? FindFocusedControl(Control root)
    {
        if (root.Focused)
        {
            return root;
        }

        foreach (Control child in root.Controls)
        {
            if (!child.ContainsFocus && !child.Focused)
            {
                continue;
            }

            return FindFocusedControl(child) ?? child;
        }

        return null;
    }

    private int[] BuildControlPath(Control? control)
    {
        if (control == null || control == this)
        {
            return [];
        }

        var reversePath = new List<int>();
        Control? current = control;
        while (current != null && current != this)
        {
            Control? parent = current.Parent;
            if (parent == null)
            {
                return [];
            }

            int childIndex = parent.Controls.IndexOf(current);
            if (childIndex < 0)
            {
                return [];
            }

            reversePath.Add(childIndex);
            current = parent;
        }

        reversePath.Reverse();
        return reversePath.ToArray();
    }

    private Control? ResolveControlPath(int[]? path)
    {
        if (path == null || path.Length == 0)
        {
            return null;
        }

        Control current = this;
        foreach (int childIndex in path)
        {
            if (childIndex < 0 || childIndex >= current.Controls.Count)
            {
                return null;
            }

            current = current.Controls[childIndex];
        }

        return current;
    }

    private static Control? FindByAccessibleName(Control root, string accessibleName)
    {
        foreach (Control child in root.Controls)
        {
            if (string.Equals(child.AccessibleName, accessibleName, StringComparison.CurrentCulture))
            {
                return child;
            }

            Control? match = FindByAccessibleName(child, accessibleName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    public void ShowPage(Type pageType)
    {
        ShowPageCore(pageType, force: false, resizeWhenFirstPageShown: true);
    }

    public void RebuildPage(Type pageType)
    {
        bool wasCurrentPage = _currentPageType == pageType;
        UserControl? previousPage = null;
        if (_pageCache.TryGetValue(pageType, out UserControl? cachedPage))
        {
            _pageCache.Remove(pageType);
            if (wasCurrentPage)
            {
                previousPage = cachedPage;
            }
            else
            {
                _contentHost.Controls.Remove(cachedPage);
                cachedPage.Dispose();
            }
        }

        if (wasCurrentPage)
        {
            ShowPageCore(pageType, force: true, resizeWhenFirstPageShown: false, previousPageOverride: previousPage);
            if (previousPage != null)
            {
                _contentHost.Controls.Remove(previousPage);
                previousPage.Dispose();
            }

            _contentHost.Invalidate(true);
            Invalidate(true);
            WindowChrome.RedrawNow(this);
        }
    }

    private void ShowPageCore(
        Type pageType,
        bool force,
        bool resizeWhenFirstPageShown,
        UserControl? previousPageOverride = null)
    {
        if (!force &&
            _currentPageType == pageType &&
            _pageCache.TryGetValue(pageType, out UserControl? currentPage) &&
            currentPage.Visible)
        {
            return;
        }

        Type? previousPageType = _currentPageType;
        UserControl? previousPage = previousPageOverride;
        if (previousPageType != null)
        {
            previousPage ??= _pageCache.GetValueOrDefault(previousPageType);
        }

        if (previousPageType != null &&
            previousPageType != pageType &&
            _navItems.TryGetValue(previousPageType, out SettingsSidebarItem? previousItem))
        {
            previousItem.Selected = false;
            previousItem.Update();
        }

        if (_navItems.TryGetValue(pageType, out SettingsSidebarItem? nextItem))
        {
            nextItem.Selected = true;
            nextItem.Update();
        }

        UserControl? page = GetOrCreatePage(pageType);
        if (page == null)
        {
            if (previousPageType != null && _navItems.TryGetValue(previousPageType, out SettingsSidebarItem? restoreItem))
            {
                restoreItem.Selected = true;
                restoreItem.Update();
            }

            if (nextItem != null)
            {
                nextItem.Selected = false;
                nextItem.Update();
            }

            return;
        }

        _contentHost.SuspendLayout();
        try
        {
            if (page.Parent != _contentHost)
            {
                _contentHost.Controls.Add(page);
                _contentHost.PreparePage(page);
            }
        }
        finally
        {
            _contentHost.ResumeLayout(performLayout: false);
        }

        BeginAtomicUpdate();
        try
        {
            SuspendLayout();
            _contentHost.SuspendLayout();
            try
            {
                _contentHost.SetActivePage(page);

                if (previousPage != null && previousPage != page)
                {
                    previousPage.Visible = false;
                }

                page.Visible = true;
                page.BringToFront();
            }
            finally
            {
                _contentHost.ResumeLayout(performLayout: false);
                ResumeLayout(performLayout: false);
            }

            _currentPageType = pageType;
            if (resizeWhenFirstPageShown && previousPageType == null)
            {
                FitToCurrentPage(resizeWindow: true);
            }

            PageShown?.Invoke(this, pageType);
        }
        finally
        {
            EndAtomicUpdate();
        }
    }

    public void FitToCurrentPage(bool resizeWindow = false)
    {
        if (_currentPageType == null ||
            !_pageCache.TryGetValue(_currentPageType, out UserControl? page) ||
            _contentHost.ClientSize.Width <= 0)
        {
            return;
        }

        _contentHost.RefreshActivePageLayout();
        int pageWidth = Math.Max(400, page.Width);

        if (resizeWindow)
        {
            int preferredPageHeight = Math.Max(1, page.Height);
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

    private UserControl? GetOrCreatePage(Type pageType)
    {
        if (_pageCache.TryGetValue(pageType, out UserControl? page))
        {
            return page;
        }

        if (!_pageFactories.TryGetValue(pageType, out Func<UserControl>? factory))
        {
            return null;
        }

        page = factory();
        page.Dock = DockStyle.None;
        page.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        page.MinimumSize = new Size(Math.Max(400, _contentHost.ClientSize.Width), 0);
        page.Visible = false;
        _pageCache[pageType] = page;
        RegisterFocusTracking(page);
        return page;
    }

    private void RegisterFocusTracking(Control control)
    {
        if (!_focusTrackedControls.Add(control))
        {
            return;
        }

        if (control.TabStop)
        {
            control.Enter += HandleTrackedControlEnter;
        }

        if (control is ScrollableControl or ContainerControl || control.HasChildren)
        {
            control.ControlAdded += HandleTrackedControlAdded;
        }

        control.Disposed += HandleTrackedControlDisposed;
        foreach (Control child in control.Controls)
        {
            RegisterFocusTracking(child);
        }
    }

    private void HandleTrackedControlAdded(object? sender, ControlEventArgs e)
    {
        if (e.Control != null)
        {
            RegisterFocusTracking(e.Control);
        }
    }

    private void HandleTrackedControlDisposed(object? sender, EventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        control.Enter -= HandleTrackedControlEnter;
        control.ControlAdded -= HandleTrackedControlAdded;
        control.Disposed -= HandleTrackedControlDisposed;
        _focusTrackedControls.Remove(control);
    }

    private void HandleTrackedControlEnter(object? sender, EventArgs e)
    {
        if (sender is not Control control || IsDisposed || !IsDescendantOf(control, _contentHost))
        {
            return;
        }

        _pendingFocusScrollControl = control;
        if (_focusScrollQueued)
        {
            return;
        }

        _focusScrollQueued = true;

        try
        {
            BeginInvoke((MethodInvoker)(() =>
            {
                _focusScrollQueued = false;
                Control? pendingControl = _pendingFocusScrollControl;
                _pendingFocusScrollControl = null;
                if (!IsDisposed && pendingControl is { IsDisposed: false, Visible: true })
                {
                    _contentHost.EnsureControlVisible(pendingControl);
                }
            }));
        }
        catch (InvalidOperationException)
        {
            _focusScrollQueued = false;
            _pendingFocusScrollControl = null;
            // The form can be closing while focus changes.
        }
    }

    private static bool IsDescendantOf(Control control, Control ancestor)
    {
        for (Control? current = control; current != null; current = current.Parent)
        {
            if (current == ancestor)
            {
                return true;
            }
        }

        return false;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using SolidBrush brush = new(_palette.MenuBackground);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override bool ShowWithoutActivation => CaptureMode || base.ShowWithoutActivation;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            if (CaptureMode)
            {
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
            }

            return cp;
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
        SetStyle(ControlStyles.ApplyThemingImplicitly, true);
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
            CornerRadius = 9,
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
    internal bool CaptureMode { get; set; }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (TryHandleNavigationKey(keyData))
        {
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (TryHandleNavigationKey(keyData))
        {
            return true;
        }

        return base.ProcessDialogKey(keyData);
    }

    private bool TryHandleNavigationKey(Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        if (key == Keys.Escape)
        {
            Close();
            return true;
        }

        if (key == Keys.Tab)
        {
            return MoveKeyboardFocus((keyData & Keys.Shift) == Keys.Shift ? -1 : 1);
        }

        if (key is Keys.Up or Keys.Down)
        {
            return MoveKeyboardFocus(key == Keys.Down ? 1 : -1);
        }

        Control? focused = FindFocusedDescendant(this);
        if (focused is TrayMenuRow && key is Keys.Home or Keys.End)
        {
            return FocusKeyboardEdge(first: key == Keys.Home);
        }

        return false;
    }

    private bool MoveKeyboardFocus(int direction)
    {
        List<Control> targets = GetKeyboardTargets();
        if (targets.Count == 0)
        {
            return false;
        }

        Control? focused = FindFocusedDescendant(this);
        int currentIndex = focused == null
            ? -1
            : targets.FindIndex(control => control == focused || control.ContainsFocus);
        int nextIndex = currentIndex < 0
            ? (direction > 0 ? 0 : targets.Count - 1)
            : (currentIndex + direction + targets.Count) % targets.Count;
        return FocusKeyboardTarget(targets[nextIndex]);
    }

    private bool FocusKeyboardEdge(bool first)
    {
        List<Control> targets = GetKeyboardTargets();
        return targets.Count > 0 && FocusKeyboardTarget(first ? targets[0] : targets[^1]);
    }

    private bool FocusKeyboardTarget(Control target)
    {
        if (!target.CanSelect)
        {
            return false;
        }

        target.Focus();
        EnsureKeyboardTargetVisible(target);
        return target.Focused || target.ContainsFocus;
    }

    private void EnsureKeyboardTargetVisible(Control target)
    {
        if (!_scrollHost.IsHandleCreated || target.IsDisposed)
        {
            return;
        }

        Rectangle bounds = _scrollHost.RectangleToClient(target.RectangleToScreen(target.ClientRectangle));
        int currentScrollY = Math.Max(0, -_scrollHost.AutoScrollPosition.Y);
        int nextScrollY = currentScrollY;
        if (bounds.Top < 0)
        {
            nextScrollY += bounds.Top;
        }
        else if (bounds.Bottom > _scrollHost.ClientSize.Height)
        {
            nextScrollY += bounds.Bottom - _scrollHost.ClientSize.Height;
        }

        int maxScrollY = Math.Max(0, ContentHost.Height - _scrollHost.ClientSize.Height);
        nextScrollY = Math.Clamp(nextScrollY, 0, maxScrollY);
        if (nextScrollY != currentScrollY)
        {
            _scrollHost.AutoScrollPosition = new Point(0, nextScrollY);
            _scrollHost.Invalidate(true);
        }
    }

    private List<Control> GetKeyboardTargets()
    {
        var targets = new List<Control>();

        static void Collect(Control parent, ICollection<Control> result)
        {
            foreach (Control child in parent.Controls)
            {
                if (!child.Visible || !child.Enabled)
                {
                    continue;
                }

                if (child is TrayMenuRow || child is TrayModeButton { TabStop: true })
                {
                    if (child.CanSelect)
                    {
                        result.Add(child);
                    }

                    continue;
                }

                Collect(child, result);
            }
        }

        Collect(ContentHost, targets);
        return targets
            .OrderBy(control => control.PointToScreen(Point.Empty).Y)
            .ThenBy(control => control.PointToScreen(Point.Empty).X)
            .ToList();
    }

    private static Control? FindFocusedDescendant(Control root)
    {
        if (root.Focused)
        {
            return root;
        }

        foreach (Control child in root.Controls)
        {
            if (!child.Focused && !child.ContainsFocus)
            {
                continue;
            }

            return FindFocusedDescendant(child) ?? child;
        }

        return null;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ClassStyle |= 0x00020000;
            if (CaptureMode)
            {
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
            }
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => CaptureMode || base.ShowWithoutActivation;

    public void ShowAnchored(Point anchor)
    {
        Show();
        LayoutAnchored(anchor);
        Activate();
    }

    internal void LayoutForCapture(Point anchor)
    {
        _ = Handle;
        LayoutAnchored(anchor);
        ContentHost.PerformLayout();
        PerformLayout();
    }

    internal void ShowForCapture()
    {
        Rectangle virtualScreen = SystemInformation.VirtualScreen;
        Location = new Point(virtualScreen.Right + 64, virtualScreen.Bottom + 64);
        Show();
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
