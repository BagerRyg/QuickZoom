using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickZoom;

internal sealed class CursorSpotlightOverlay : Form
{
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WM_NCHITTEST = 0x0084;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 0x0003;
    private const int HTTRANSPARENT = -1;
    private const int CURSOR_SHOWING = 0x00000001;
    private const int DI_NORMAL = 0x0003;
    private const int SM_CXCURSOR = 13;
    private const int SM_CYCURSOR = 14;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorInfo(ref CURSORINFO pci);

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CURSORINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    private readonly Color _transparentKey = Color.Lime;
    private IntPtr _cursorHandle;
    private Bitmap? _cursorBitmap;
    private IntPtr _bitmapCursorHandle;
    private int _hotspotX;
    private int _hotspotY;
    private float _scale = 1f;

    public CursorSpotlightOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        DoubleBuffered = true;
        BackColor = _transparentKey;
        TransparencyKey = _transparentKey;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT;
            return cp;
        }
    }

    public void UpdateSpotlight(Point cursorPoint, double progress)
    {
        if (!TryPrepareCursor(cursorPoint, progress))
        {
            HideSpotlight();
            return;
        }

        if (!Visible)
        {
            Show();
        }

        Invalidate();
    }

    public void HideSpotlight()
    {
        if (Visible)
        {
            Hide();
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST)
        {
            m.Result = (IntPtr)HTTRANSPARENT;
            return;
        }

        if (m.Msg == WM_MOUSEACTIVATE)
        {
            m.Result = (IntPtr)MA_NOACTIVATE;
            return;
        }

        base.WndProc(ref m);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (_cursorHandle == IntPtr.Zero)
        {
            return;
        }

        if (_cursorBitmap == null)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        e.Graphics.CompositingQuality = CompositingQuality.HighQuality;

        int drawWidth = (int)Math.Round(_cursorBitmap.Width * _scale);
        int drawHeight = (int)Math.Round(_cursorBitmap.Height * _scale);
        int drawX = (int)Math.Round((_hotspotX * _scale * -1) + ((Width - drawWidth) / 2.0) + _hotspotX);
        int drawY = (int)Math.Round((_hotspotY * _scale * -1) + ((Height - drawHeight) / 2.0) + _hotspotY);

        e.Graphics.DrawImage(_cursorBitmap, new Rectangle(drawX, drawY, drawWidth, drawHeight));
    }

    private bool TryPrepareCursor(Point cursorPoint, double progress)
    {
        var cursorInfo = new CURSORINFO
        {
            cbSize = Marshal.SizeOf<CURSORINFO>()
        };

        if (!GetCursorInfo(ref cursorInfo) || cursorInfo.flags != CURSOR_SHOWING || cursorInfo.hCursor == IntPtr.Zero)
        {
            return false;
        }

        if (!GetIconInfo(cursorInfo.hCursor, out ICONINFO iconInfo))
        {
            return false;
        }

        try
        {
            int baseWidth = Math.Max(16, GetSystemMetrics(SM_CXCURSOR));
            int baseHeight = Math.Max(16, GetSystemMetrics(SM_CYCURSOR));
            _cursorHandle = cursorInfo.hCursor;
            _hotspotX = iconInfo.xHotspot;
            _hotspotY = iconInfo.yHotspot;
            _scale = 3.2f - (float)(Math.Max(0d, Math.Min(1d, progress)) * 2.2d);
            RefreshCursorBitmapIfNeeded(baseWidth, baseHeight);

            int drawWidth = (int)Math.Round(baseWidth * _scale);
            int drawHeight = (int)Math.Round(baseHeight * _scale);
            int padding = 36;
            int left = cursorPoint.X - (int)Math.Round(_hotspotX * _scale) - padding;
            int top = cursorPoint.Y - (int)Math.Round(_hotspotY * _scale) - padding;
            int width = drawWidth + (padding * 2);
            int height = drawHeight + (padding * 2);
            Bounds = new Rectangle(left, top, width, height);
            return true;
        }
        finally
        {
            if (iconInfo.hbmColor != IntPtr.Zero)
            {
                _ = DeleteObject(iconInfo.hbmColor);
            }

            if (iconInfo.hbmMask != IntPtr.Zero)
            {
                _ = DeleteObject(iconInfo.hbmMask);
            }
        }
    }

    private void RefreshCursorBitmapIfNeeded(int baseWidth, int baseHeight)
    {
        if (_cursorHandle == IntPtr.Zero)
        {
            return;
        }

        if (_cursorBitmap != null &&
            _bitmapCursorHandle == _cursorHandle &&
            _cursorBitmap.Width == baseWidth &&
            _cursorBitmap.Height == baseHeight)
        {
            return;
        }

        _cursorBitmap?.Dispose();
        _cursorBitmap = new Bitmap(baseWidth, baseHeight, PixelFormat.Format32bppArgb);
        _bitmapCursorHandle = _cursorHandle;
        using Graphics graphics = Graphics.FromImage(_cursorBitmap);
        graphics.Clear(Color.Transparent);

        IntPtr hdc = graphics.GetHdc();
        try
        {
            _ = DrawIconEx(hdc, 0, 0, _cursorHandle, baseWidth, baseHeight, 0, IntPtr.Zero, DI_NORMAL);
        }
        finally
        {
            graphics.ReleaseHdc(hdc);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cursorBitmap?.Dispose();
            _cursorBitmap = null;
        }

        base.Dispose(disposing);
    }
}
