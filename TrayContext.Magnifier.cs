using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickZoom;

internal sealed partial class TrayContext
{
    [DllImport("Magnification.dll", ExactSpelling = true)]
    private static extern bool MagInitialize();

    [DllImport("Magnification.dll", ExactSpelling = true)]
    private static extern bool MagUninitialize();

    [DllImport("Magnification.dll", ExactSpelling = true)]
    private static extern bool MagSetFullscreenTransform(float magLevel, int xOffset, int yOffset);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    private const uint SPI_SETCURSORS = 0x0057;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private void EnsureMag(bool active)
    {
        if (active && !_magActive)
        {
            _magActive = MagInitialize();
            if (!_autoSwitchMonitor && GetCursorPos(out var ptLock))
            {
                _lockedScreen = Screen.FromPoint(new Point(ptLock.X, ptLock.Y));
            }
        }
        else if (!active && _magActive)
        {
            MagSetFullscreenTransform(1.0f, 0, 0);
            MagUninitialize();
            _magActive = false;
            SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
        }
    }

    private void DisableMagAndReset()
    {
        _zoomPercent = 100;
        _animTargetPercent = 100;
        EnsureMag(false);
    }

    private void ClampZoom()
    {
        _zoomPercent = Math.Max(MinPercent, Math.Min(_zoomPercent, _maxPercent));
        ApplyTransformCurrentPoint();
    }

    private static float PercentToMag(int percent) => Math.Max(1.0f, percent / 100f);

    private void ApplyTransformCurrentPoint()
    {
        if (_autoDisableAt100 && _zoomPercent <= 100)
        {
            DisableMagAndReset();
            return;
        }

        EnsureMag(true);
        if (GetCursorPos(out var pt))
        {
            ApplyTransformAtPoint(pt, PercentToMag(_zoomPercent));
        }
    }

    private void ApplyTransformAtPoint(POINT pt, float mag)
    {
        if (!_magActive)
        {
            return;
        }

        Screen screen;
        if (_autoSwitchMonitor)
        {
            screen = Screen.FromPoint(new Point(pt.X, pt.Y));
        }
        else
        {
            _lockedScreen ??= Screen.FromPoint(new Point(pt.X, pt.Y));
            screen = _lockedScreen;
        }

        Rectangle bounds = screen.Bounds;

        int viewW = Math.Max(1, (int)Math.Round(bounds.Width / mag));
        int viewH = Math.Max(1, (int)Math.Round(bounds.Height / mag));

        int offsetX;
        int offsetY;

        if (_centerCursor)
        {
            offsetX = (int)Math.Round(pt.X - (viewW / 2.0));
            offsetY = (int)Math.Round(pt.Y - (viewH / 2.0));
        }
        else
        {
            int relX = pt.X - bounds.Left;
            int relY = pt.Y - bounds.Top;

            offsetX = (int)Math.Round(pt.X - (relX / mag));
            offsetY = (int)Math.Round(pt.Y - (relY / mag));
        }

        int minX = bounds.Left;
        int minY = bounds.Top;
        int maxX = bounds.Right - viewW;
        int maxY = bounds.Bottom - viewH;

        if (offsetX < minX) offsetX = minX;
        if (offsetY < minY) offsetY = minY;
        if (offsetX > maxX) offsetX = maxX;
        if (offsetY > maxY) offsetY = maxY;

        MagSetFullscreenTransform(mag, offsetX, offsetY);
    }
}
