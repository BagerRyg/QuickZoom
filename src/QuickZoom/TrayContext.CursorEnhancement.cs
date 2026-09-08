using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace QuickZoom;

internal sealed partial class TrayContext
{
    private const int DI_NORMAL = 0x0003;
    private const int SM_CXCURSOR = 13;
    private const int SM_CYCURSOR = 14;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);

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

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int CursorScaleMinimum = 100;
    private const int CursorScaleMaximum = 500;

    private void ApplyCursorEnhancementIfNeeded()
    {
        if (_cursorEnhancementEnabled)
        {
            ApplyCursorEnhancement();
            RefreshMagnifierCursorRendering();
        }
        else if (_cursorEnhancementApplied)
        {
            RestoreSystemCursorScheme(reapplyCursorEnhancement: false);
            RefreshMagnifierCursorRendering();
        }
    }

    private void ScheduleCursorEnhancementApply()
    {
        if (!_cursorEnhancementEnabled)
        {
            return;
        }

        if (_cursorScaleApplyTimer == null)
        {
            _cursorScaleApplyTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _cursorScaleApplyTimer.Tick += OnCursorScaleApplyTimerTick;
        }

        _cursorScaleApplyTimer.Stop();
        _cursorScaleApplyTimer.Start();
    }

    private void OnCursorScaleApplyTimerTick(object? sender, EventArgs e)
    {
        RunGuarded("CursorEnhancement.ApplyTimer", () =>
        {
            _cursorScaleApplyTimer?.Stop();
            ApplyCursorEnhancementIfNeeded();
        });
    }

    private void ApplyCursorEnhancement()
    {
        if (_applyingCursorEnhancement)
        {
            return;
        }

        _applyingCursorEnhancement = true;
        try
        {
            RestoreSystemCursorScheme(reapplyCursorEnhancement: false);

            int baseWidth = Math.Max(16, GetSystemMetrics(SM_CXCURSOR));
            int baseHeight = Math.Max(16, GetSystemMetrics(SM_CYCURSOR));
            double scale = Math.Clamp(_cursorScale, CursorScaleMinimum, CursorScaleMaximum) / 100d;
            Color fillColor = Color.FromArgb(_cursorFillColorArgb);
            Color borderColor = Color.FromArgb(_cursorBorderColorArgb);

            foreach (uint cursorId in CursorSystemIds)
            {
                IntPtr systemCursor = LoadCursor(IntPtr.Zero, new IntPtr(cursorId));
                if (systemCursor == IntPtr.Zero)
                {
                    ErrorLog.WriteThrottled("CursorEnhancement.LoadCursor", $"LoadCursor failed for OCR value {cursorId} with Win32 error {Marshal.GetLastWin32Error()}.");
                    continue;
                }

                IntPtr enhancedCursor = CreateEnhancedCursor(systemCursor, baseWidth, baseHeight, scale, fillColor, borderColor);
                if (enhancedCursor == IntPtr.Zero)
                {
                    ErrorLog.WriteThrottled("CursorEnhancement.CreateCursor", $"Could not create enhanced cursor for OCR value {cursorId}.");
                    continue;
                }

                if (!SetSystemCursor(enhancedCursor, cursorId))
                {
                    _ = DestroyIcon(enhancedCursor);
                    int error = Marshal.GetLastWin32Error();
                    ErrorLog.Write("CursorEnhancement", $"SetSystemCursor failed for OCR value {cursorId} with Win32 error {error}.");
                }
            }

            _cursorEnhancementApplied = true;
            _cursorSpotlightOverridesSystemCursors = false;
        }
        catch (Exception ex)
        {
            ErrorLog.Write("CursorEnhancement", ex);
            RestoreSystemCursorScheme(reapplyCursorEnhancement: false);
        }
        finally
        {
            _applyingCursorEnhancement = false;
        }
    }

    private static IntPtr CreateEnhancedCursor(IntPtr sourceCursor, int baseWidth, int baseHeight, double scale, Color fillColor, Color borderColor)
    {
        if (!GetIconInfo(sourceCursor, out ICONINFO iconInfo))
        {
            return IntPtr.Zero;
        }

        try
        {
            using Bitmap source = RenderCursor(sourceCursor, baseWidth, baseHeight);
            using Bitmap scaled = ScaleCursorBitmap(source, scale);
            using Bitmap recolored = CursorBitmapProcessing.Recolor(scaled, fillColor, borderColor, Math.Max(1, (int)Math.Round(scale)));
            return CreateCursorFromBitmap(
                recolored,
                Math.Clamp((int)Math.Round(iconInfo.xHotspot * scale), 0, Math.Max(0, recolored.Width - 1)),
                Math.Clamp((int)Math.Round(iconInfo.yHotspot * scale), 0, Math.Max(0, recolored.Height - 1)));
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

    private static Bitmap RenderCursor(IntPtr cursor, int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        IntPtr hdc = graphics.GetHdc();
        try
        {
            _ = DrawIconEx(hdc, 0, 0, cursor, width, height, 0, IntPtr.Zero, DI_NORMAL);
        }
        finally
        {
            graphics.ReleaseHdc(hdc);
        }

        return bitmap;
    }

    private static Bitmap ScaleCursorBitmap(Bitmap source, double scale)
    {
        if (scale <= 1)
        {
            return new Bitmap(source);
        }

        var scaled = new Bitmap(
            Math.Max(1, (int)Math.Round(source.Width * scale)),
            Math.Max(1, (int)Math.Round(source.Height * scale)),
            PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(scaled);
        graphics.Clear(Color.Transparent);
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.DrawImage(source, new Rectangle(0, 0, scaled.Width, scaled.Height));
        return scaled;
    }

    private static IntPtr CreateCursorFromBitmap(Bitmap bitmap, int hotspotX, int hotspotY)
    {
        IntPtr hbmColor = bitmap.GetHbitmap(Color.FromArgb(0));
        IntPtr hbmMask = CreateBitmap(bitmap.Width, bitmap.Height, 1, 1, IntPtr.Zero);

        if (hbmColor == IntPtr.Zero || hbmMask == IntPtr.Zero)
        {
            if (hbmColor != IntPtr.Zero)
            {
                _ = DeleteObject(hbmColor);
            }

            if (hbmMask != IntPtr.Zero)
            {
                _ = DeleteObject(hbmMask);
            }

            return IntPtr.Zero;
        }

        try
        {
            var iconInfo = new ICONINFO
            {
                fIcon = false,
                xHotspot = hotspotX,
                yHotspot = hotspotY,
                hbmMask = hbmMask,
                hbmColor = hbmColor
            };

            return CreateIconIndirect(ref iconInfo);
        }
        finally
        {
            _ = DeleteObject(hbmColor);
            _ = DeleteObject(hbmMask);
        }
    }
}
