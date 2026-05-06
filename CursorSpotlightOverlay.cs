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
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const int WM_NCHITTEST = 0x0084;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 0x0003;
    private const int HTTRANSPARENT = -1;
    private const int CURSOR_SHOWING = 0x00000001;
    private const int DI_NORMAL = 0x0003;
    private const int SM_CXCURSOR = 13;
    private const int SM_CYCURSOR = 14;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorInfo(ref CURSORINFO pci);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

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

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int GetObject(IntPtr hObject, int nCount, out BITMAP lpObject);

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

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    private readonly Color _transparentKey = Color.Lime;
    private IntPtr _cursorHandle;
    private Bitmap? _cursorBitmap;
    private Bitmap? _spotlightBitmap;
    private IntPtr _bitmapCursorHandle;
    private IntPtr _spotlightCursorHandle;
    private Size _spotlightSourceSize;
    private float _spotlightScale;
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

    public bool UpdateSpotlight(Point cursorPoint, double progress)
    {
        if (!TryPrepareCursor(cursorPoint, progress))
        {
            HideSpotlight();
            return false;
        }

        if (!Visible)
        {
            Show();
        }

        if (Handle != IntPtr.Zero)
        {
            _ = SetWindowPos(
                Handle,
                HWND_TOPMOST,
                0,
                0,
                0,
                0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        Invalidate();
        return true;
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

        RefreshSpotlightBitmapIfNeeded();
        if (_spotlightBitmap == null)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.None;
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        e.Graphics.CompositingQuality = CompositingQuality.HighSpeed;

        int drawWidth = _spotlightBitmap.Width;
        int drawHeight = _spotlightBitmap.Height;
        int drawX = (int)Math.Round((_hotspotX * _scale * -1) + ((Width - drawWidth) / 2.0) + _hotspotX);
        int drawY = (int)Math.Round((_hotspotY * _scale * -1) + ((Height - drawHeight) / 2.0) + _hotspotY);

        e.Graphics.DrawImageUnscaled(_spotlightBitmap, drawX, drawY);
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
            Size cursorSize = GetCursorBitmapSize(iconInfo);
            int baseWidth = cursorSize.Width;
            int baseHeight = cursorSize.Height;
            _cursorHandle = cursorInfo.hCursor;
            _hotspotX = iconInfo.xHotspot;
            _hotspotY = iconInfo.yHotspot;
            _scale = 2f;
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
        _spotlightBitmap?.Dispose();
        _spotlightBitmap = null;
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

    private void RefreshSpotlightBitmapIfNeeded()
    {
        if (_cursorBitmap == null)
        {
            return;
        }

        Size sourceSize = _cursorBitmap.Size;
        if (_spotlightBitmap != null &&
            _spotlightCursorHandle == _cursorHandle &&
            _spotlightSourceSize == sourceSize &&
            Math.Abs(_spotlightScale - _scale) < 0.001f)
        {
            return;
        }

        _spotlightBitmap?.Dispose();
        int width = Math.Max(1, (int)Math.Round(_cursorBitmap.Width * _scale));
        int height = Math.Max(1, (int)Math.Round(_cursorBitmap.Height * _scale));
        _spotlightBitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        _spotlightCursorHandle = _cursorHandle;
        _spotlightSourceSize = sourceSize;
        _spotlightScale = _scale;

        using Graphics graphics = Graphics.FromImage(_spotlightBitmap);
        graphics.Clear(Color.Transparent);
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.CompositingQuality = CompositingQuality.HighSpeed;
        graphics.DrawImage(_cursorBitmap, new Rectangle(0, 0, width, height));
    }

    private static Size GetCursorBitmapSize(ICONINFO iconInfo)
    {
        if (TryGetBitmapSize(iconInfo.hbmColor, out Size colorSize))
        {
            return colorSize;
        }

        if (TryGetBitmapSize(iconInfo.hbmMask, out Size maskSize))
        {
            return new Size(maskSize.Width, Math.Max(16, maskSize.Height / 2));
        }

        return new Size(
            Math.Max(16, GetSystemMetrics(SM_CXCURSOR)),
            Math.Max(16, GetSystemMetrics(SM_CYCURSOR)));
    }

    private static bool TryGetBitmapSize(IntPtr bitmapHandle, out Size size)
    {
        size = Size.Empty;
        if (bitmapHandle == IntPtr.Zero)
        {
            return false;
        }

        if (GetObject(bitmapHandle, Marshal.SizeOf<BITMAP>(), out BITMAP bitmap) == 0)
        {
            return false;
        }

        if (bitmap.bmWidth <= 0 || bitmap.bmHeight <= 0)
        {
            return false;
        }

        size = new Size(bitmap.bmWidth, bitmap.bmHeight);
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cursorBitmap?.Dispose();
            _cursorBitmap = null;
            _spotlightBitmap?.Dispose();
            _spotlightBitmap = null;
        }

        base.Dispose(disposing);
    }
}
