using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickZoom;

internal sealed partial class TrayContext
{
    private TrackingMode _trackingMode = TrackingMode.Automatic;
    private readonly TrackingController _tracking = new();
    private FocusTrackingWorker? _focusTracking;
    private Point? _lastTrackingPoint;
    private readonly Dictionary<Rectangle, Rectangle> _trackingViewports = new();
    private Rectangle? _overlayTrackingViewport;

    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);

    private void ResetActivityTracking()
    {
        _tracking.Reset();
        _tracking.Mode = _trackingMode;
        _focusTracking?.Clear();
        _lastTrackingPoint = null;
        _trackingViewports.Clear();
        _overlayTrackingViewport = null;
    }

    private void SetTrackingMode(TrackingMode mode)
    {
        if (!Enum.IsDefined(mode)) return;
        ResetExitConfirmation();
        _trackingMode = mode;
        _followCursor = true;
        ResetActivityTracking();
        KeepTrayPopupOpenForOverlayActivation();
        SaveSettings();
        if (!_screenshotMode) ApplyTransformCurrentPoint();
        UpdateFollowTimerState();
        UpdateFollowingUi();
    }

    private void ObserveTrackingKeyboard(int nCode, IntPtr wParam, IntPtr lParam, IntPtr result)
    {
        if (nCode < 0 || result != IntPtr.Zero || wParam.ToInt32() is not (WM_KEYDOWN or WM_SYSKEYDOWN)) return;
        var input = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
        if (input.dwExtraInfo != ShortcutReplayExtraInfo)
            _tracking.ObserveKeyboard((Keys)input.vkCode, Environment.TickCount64);
    }

    private void ObserveTrackingPointer(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0) return;
        var input = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
        uint dpi = GetDpiForWindow(GetForegroundWindow());
        int threshold = Math.Max(6, (int)Math.Round(6 * dpi / 96.0));
        _tracking.ObservePointer(new Point(input.pt.X, input.pt.Y), wParam.ToInt32(), Environment.TickCount64, threshold);
    }

    private POINT ResolveActivityTracking(POINT fallback)
    {
        Point mouse = new(fallback.X, fallback.Y);
        if (!_followCursor && _lastTrackingPoint.HasValue)
            return new POINT { X = _lastTrackingPoint.Value.X, Y = _lastTrackingPoint.Value.Y };

        long now = Environment.TickCount64;
        IntPtr foreground = GetForegroundWindow();
        if (_trackingMode != TrackingMode.MouseOnly && _followCursor && !_screenshotMode)
            _focusTracking?.Refresh(foreground, _tracking.SelectionDirection, now);
        Point point = _tracking.Resolve(mouse, foreground, _focusTracking?.Snapshot, now);
        _lastTrackingPoint = point;
        return new POINT { X = point.X, Y = point.Y };
    }

    private RECT TrackSourceRectangle(Rectangle bounds, RECT original)
    {
        Rectangle source = Rectangle.FromLTRB(original.left, original.top, original.right, original.bottom);
        if (_tracking.Target is Rectangle target && target.IntersectsWith(bounds))
        {
            Rectangle? previous = _trackingViewports.TryGetValue(bounds, out Rectangle cached) ? cached : source;
            source = TrackingController.KeepVisible(bounds, source.Size, Rectangle.Intersect(bounds, target), previous);
        }
        _trackingViewports[bounds] = source;
        return new RECT { left = source.Left, top = source.Top, right = source.Right, bottom = source.Bottom };
    }

    private Point TrackOverlayAnchor(Point anchor, Rectangle screen, Size viewport)
    {
        if (_tracking.Target is Rectangle target && target.IntersectsWith(screen))
        {
            Rectangle source = TrackingController.KeepVisible(screen, viewport, target, _overlayTrackingViewport);
            _overlayTrackingViewport = source;
            return new Point(source.Left + source.Width / 2, source.Top + source.Height / 2);
        }
        _overlayTrackingViewport = new Rectangle(anchor.X - viewport.Width / 2, anchor.Y - viewport.Height / 2,
            viewport.Width, viewport.Height);
        return anchor;
    }
}
