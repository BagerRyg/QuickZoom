using System;
using System.Drawing;
using System.Windows.Forms;

namespace QuickZoom;

internal enum TrackingMode
{
    Automatic = 0,
    MouseOnly = 1,
    KeyboardAndTyping = 2
}

// Only geometry and input intent are retained. No typed or selected text is read.
internal sealed record TrackingSnapshot(IntPtr Foreground, long RequestedAt, Rectangle? Caret, Rectangle? Focus);

internal sealed class TrackingController
{
    private bool _keyboardIntent;
    private long _intentStarted;
    private Point? _mouseOrigin;
    private Point? _mousePosition;
    private int _pressedButtons;
    private Point? _anchor;
    private Rectangle? _target;
    private IntPtr _foreground;

    internal TrackingMode Mode { get; set; }
    internal bool KeyboardActive { get; private set; }
    internal Rectangle? Target => KeyboardActive ? _target : null;
    internal int SelectionDirection { get; private set; } = 1;

    internal void Reset()
    {
        _keyboardIntent = false;
        _intentStarted = 0;
        _mouseOrigin = null;
        _mousePosition = null;
        _pressedButtons = 0;
        _anchor = null;
        _target = null;
        _foreground = IntPtr.Zero;
        KeyboardActive = false;
        SelectionDirection = 1;
    }

    internal void ObserveKeyboard(Keys key, long now)
    {
        if (_pressedButtons != 0 || !IsTrackingKey(key)) return;
        if (!_keyboardIntent)
        {
            _intentStarted = now;
            _mouseOrigin = _mousePosition;
        }
        _keyboardIntent = true;
        SelectionDirection = key is Keys.Left or Keys.Up or Keys.Home or Keys.PageUp or Keys.Back ? -1 : 1;
    }

    internal static bool IsTrackingKey(Keys key) =>
        key is >= Keys.A and <= Keys.Z or >= Keys.D0 and <= Keys.D9 or
        >= Keys.NumPad0 and <= Keys.Divide or >= Keys.OemSemicolon and <= Keys.OemBackslash or
        Keys.Tab or Keys.Enter or Keys.Space or Keys.Back or Keys.Delete or Keys.Insert or
        Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End or
        Keys.PageUp or Keys.PageDown or Keys.Escape or Keys.ProcessKey or Keys.Packet;

    internal void ObservePointer(Point point, int message, long now, int movementThreshold = 6)
    {
        _mouseOrigin ??= point;
        _mousePosition = point;
        int button = message switch
        {
            0x201 or 0x202 => 1, // left
            0x204 or 0x205 => 2, // right
            0x207 or 0x208 => 4, // middle
            0x20B or 0x20C => 8, // X buttons
            _ => 0
        };
        if (message is 0x201 or 0x204 or 0x207 or 0x20B) _pressedButtons |= button;
        if (message is 0x202 or 0x205 or 0x208 or 0x20C) _pressedButtons &= ~button;
        if (Mode == TrackingMode.KeyboardAndTyping && button == 0 && _pressedButtons == 0) return;

        long dx = point.X - (long)_mouseOrigin.Value.X;
        long dy = point.Y - (long)_mouseOrigin.Value.Y;
        bool deliberate = button != 0 || message is 0x20A or 0x20E || _pressedButtons != 0 ||
                          dx * dx + dy * dy >= movementThreshold * movementThreshold;
        if (!deliberate) return;
        _mouseOrigin = point;
        _keyboardIntent = false;
        _intentStarted = now;
    }

    internal Point Resolve(Point mouse, IntPtr foreground, TrackingSnapshot? sample, long now)
    {
        _mouseOrigin ??= mouse;
        _mousePosition = mouse;
        if (_foreground != foreground)
        {
            _foreground = foreground;
            // A delayed result from the previous app must not move the new view.
            _intentStarted = Math.Max(_intentStarted, now);
        }

        bool wantsKeyboard = Mode == TrackingMode.KeyboardAndTyping ||
                             Mode == TrackingMode.Automatic && _keyboardIntent;
        if (!wantsKeyboard)
        {
            KeyboardActive = false;
            _target = null;
            _anchor = mouse;
            return mouse;
        }

        if (_pressedButtons == 0 && sample != null && sample.Foreground == foreground &&
            sample.RequestedAt >= _intentStarted && now - sample.RequestedAt is >= 0 and <= 400)
        {
            Rectangle? target = sample.Caret ?? sample.Focus;
            if (target is Rectangle rect && rect.Width > 0 && rect.Height > 0)
            {
                _target = rect;
                // A large document is not a useful point to centre on. Keep the
                // nearest visible part instead of jumping to the document centre.
                _anchor = sample.Caret.HasValue
                    ? new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2)
                    : new Point(Math.Clamp((_anchor ?? mouse).X, rect.Left, rect.Right - 1),
                                Math.Clamp((_anchor ?? mouse).Y, rect.Top, rect.Bottom - 1));
                KeyboardActive = true;
            }
        }

        // Pausing, a blinking caret, or an unavailable provider never hands
        // control back to a stationary mouse or throws away the last good view.
        return _anchor ?? mouse;
    }

    internal static Rectangle KeepVisible(Rectangle bounds, Size viewport, Rectangle target, Rectangle? previous)
    {
        int width = Math.Clamp(viewport.Width, 1, bounds.Width);
        int height = Math.Clamp(viewport.Height, 1, bounds.Height);
        Rectangle start = previous ?? new Rectangle(target.X - width / 2, target.Y - height / 2, width, height);
        int x = Adjust(start.Left, width, target.Left, target.Right);
        int y = Adjust(start.Top, height, target.Top, target.Bottom);
        return new Rectangle(Math.Clamp(x, bounds.Left, bounds.Right - width),
            Math.Clamp(y, bounds.Top, bounds.Bottom - height), width, height);

        static int Adjust(int origin, int length, int first, int last)
        {
            int margin = Math.Max(1, length / 6);
            if (last - first > length - margin * 2)
            {
                // Large focused containers: preserve an overlapping view.
                if (first < origin + length - margin && last > origin + margin) return origin;
                return first - margin;
            }
            if (first < origin + margin) return first - margin;
            if (last > origin + length - margin) return last - length + margin;
            return origin;
        }
    }
}
