using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;

namespace QuickZoom;

// One bounded producer, never a task per frame. COM providers are queried only
// on this MTA thread; rendering and input hooks only read immutable snapshots.
internal sealed class FocusTrackingWorker : IDisposable
{
    private sealed record Request(IntPtr Foreground, long At, int Direction);
    private readonly AutoResetEvent _wake = new(false);
    private readonly Thread _thread;
    private Request? _request;
    private TrackingSnapshot? _snapshot;
    private volatile bool _stopped;
    private long _lastRequest;

    internal FocusTrackingWorker()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "QuickZoom focus tracking" };
        _thread.SetApartmentState(ApartmentState.MTA);
        _thread.Start();
    }

    internal TrackingSnapshot? Snapshot => Volatile.Read(ref _snapshot);

    internal void Refresh(IntPtr foreground, int direction, long now)
    {
        if (_stopped || foreground == IntPtr.Zero || now - _lastRequest < 35) return;
        _lastRequest = now;
        Volatile.Write(ref _request, new Request(foreground, now, direction));
        _wake.Set();
    }

    internal void Clear()
    {
        Volatile.Write(ref _request, null);
        Volatile.Write(ref _snapshot, null);
    }

    private void Run()
    {
        using var reader = new FocusGeometryReader();
        try
        {
            while (!_stopped)
            {
                _wake.WaitOne(100);
                Request? request = Interlocked.Exchange(ref _request, null);
                if (_stopped || request == null || Environment.TickCount64 - request.At > 400) continue;
                try
                {
                    TrackingSnapshot? result = reader.Read(request.Foreground, request.At, request.Direction);
                    if (!_stopped && Environment.TickCount64 - request.At <= 400)
                        Volatile.Write(ref _snapshot, result);
                }
                catch
                {
                    // A disconnected or non-responsive application's accessibility
                    // provider must not change the view or interrupt magnification.
                    Volatile.Write(ref _snapshot, null);
                }
            }
        }
        finally { _wake.Dispose(); }
    }

    public void Dispose()
    {
        if (_stopped) return;
        _stopped = true;
        Clear();
        try { _wake.Set(); } catch (ObjectDisposedException) { }
        // No UI-thread join: a foreign COM provider may be completing a timeout.
    }
}

internal sealed class FocusGeometryReader : IDisposable
{
    private AutomationInterop.IClient? _client;
    private AutomationInterop.ITextRange? _previousSelection;
    private string? _selectionOwner;
    private long _retryClientAfter;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct GuiInfo
    {
        public int Size, Flags;
        public IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret;
        public NativeRect CaretRect;
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint thread, ref GuiInfo info);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool IsChild(IntPtr parent, IntPtr child);
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

    internal TrackingSnapshot? Read(IntPtr foreground, long requestedAt, int direction)
    {
        if (foreground == IntPtr.Zero || foreground != GetForegroundWindow()) return null;
        // UIA rectangles are physical screen pixels. Native client coordinates
        // must use the same space, including on mixed-DPI monitors.
        IntPtr oldDpi = SetThreadDpiAwarenessContext(new IntPtr(-4));
        try
        {
            uint thread = GetWindowThreadProcessId(foreground, out uint process);
            var info = new GuiInfo { Size = Marshal.SizeOf<GuiInfo>() };
            Rectangle? nativeFocus = null;
            if (thread != 0 && GetGUIThreadInfo(thread, ref info))
            {
                if (info.Caret != IntPtr.Zero && info.CaretRect.Bottom > info.CaretRect.Top &&
                    (info.Caret == info.Focus || IsChild(info.Focus, info.Caret)))
                {
                    var first = new NativePoint { X = info.CaretRect.Left, Y = info.CaretRect.Top };
                    var last = new NativePoint { X = info.CaretRect.Right, Y = info.CaretRect.Bottom };
                    if (ClientToScreen(info.Caret, ref first) && ClientToScreen(info.Caret, ref last) &&
                        GetWindowRect(info.Caret, out NativeRect owner) &&
                        Rect(owner).Contains(first.X, first.Y))
                        return Finish(foreground, requestedAt,
                            new Rectangle(first.X, first.Y, Math.Max(1, last.X - first.X), Math.Max(1, last.Y - first.Y)), null);
                }
                if (info.Focus != IntPtr.Zero && info.Focus != foreground && GetWindowRect(info.Focus, out NativeRect focus))
                    nativeFocus = Valid(Rect(focus));
            }

            EnsureClient();
            AutomationInterop.IElement? element = null;
            try
            {
                element = _client?.GetFocusedElement();
                if (element == null || Convert.ToUInt32(element.GetCurrentPropertyValue(30002)) != process ||
                    element.GetCurrentPropertyValue(30008) is not true || element.GetCurrentPropertyValue(30022) is true)
                    return Finish(foreground, requestedAt, null, nativeFocus);

                Rectangle? focus = Bounds(element.GetCurrentPropertyValue(30001) as double[]) ?? nativeFocus;
                string owner = foreground.ToString() + ":" + string.Join(",", element.GetRuntimeId());
                if (_selectionOwner != owner)
                {
                    Release(_previousSelection);
                    _previousSelection = null;
                    _selectionOwner = owner;
                }
                Rectangle? caret = ReadCaret(element, direction);
                if (caret.HasValue && focus.HasValue && !focus.Value.IntersectsWith(caret.Value)) caret = null;
                return Finish(foreground, requestedAt, caret, focus);
            }
            catch (COMException) { return Finish(foreground, requestedAt, null, nativeFocus); }
            finally { Release(element); }
        }
        finally { if (oldDpi != IntPtr.Zero) SetThreadDpiAwarenessContext(oldDpi); }
    }

    private static TrackingSnapshot? Finish(IntPtr foreground, long at, Rectangle? caret, Rectangle? focus) =>
        foreground == GetForegroundWindow() ? new TrackingSnapshot(foreground, at, caret, focus) : null;

    private void EnsureClient()
    {
        if (_client != null || Environment.TickCount64 < _retryClientAfter) return;
        try
        {
            _client = (AutomationInterop.IClient)Activator.CreateInstance(Type.GetTypeFromCLSID(
                new Guid("e22ad333-b25f-460c-83d0-0581107395c9"), throwOnError: true)!)!;
            _client.SetConnectionTimeout(150);
            _client.SetTransactionTimeout(200);
        }
        catch
        {
            Release(_client);
            _client = null;
            _retryClientAfter = Environment.TickCount64 + 5000;
        }
    }

    private Rectangle? ReadCaret(AutomationInterop.IElement element, int direction)
    {
        object? pattern = null;
        AutomationInterop.ITextRange? range = null;
        AutomationInterop.IRangeArray? selections = null;
        try
        {
            try { pattern = element.GetCurrentPattern(10024); } catch (COMException) { }
            if (pattern is AutomationInterop.ITextPattern2 advanced)
            {
                try
                {
                    range = advanced.GetCaretRange(out bool active);
                    if (active && range != null)
                    {
                        Rectangle? caret = EndpointBounds(range, end: false);
                        if (caret.HasValue) return caret;
                    }
                }
                catch (COMException) { } // Older providers can still expose TextPattern.
                Release(range);
                range = null;
            }
            Release(pattern);
            pattern = null;
            try { pattern = element.GetCurrentPattern(10014); } catch (COMException) { }
            if (pattern is not AutomationInterop.ITextPattern text) return null;
            selections = text.GetSelection();
            if (selections == null || selections.GetLength() == 0) return null;
            range = selections.GetElement(direction < 0 ? 0 : selections.GetLength() - 1);
            bool end = direction >= 0;
            if (_previousSelection != null)
            {
                try
                {
                    bool startChanged = range.CompareEndpoints(0, _previousSelection, 0) != 0;
                    bool endChanged = range.CompareEndpoints(1, _previousSelection, 1) != 0;
                    if (startChanged != endChanged) end = endChanged;
                }
                catch (COMException) { } // A rebuilt document invalidates old ranges.
            }
            Rectangle? result = EndpointBounds(range, end);
            Release(_previousSelection);
            _previousSelection = range.Clone();
            return result;
        }
        catch (COMException) { return null; }
        finally
        {
            Release(range);
            Release(selections);
            Release(pattern);
        }
    }

    private static Rectangle? EndpointBounds(AutomationInterop.ITextRange range, bool end)
    {
        AutomationInterop.ITextRange copy = range.Clone();
        try
        {
            copy.MoveEndpointByRange(end ? 0 : 1, copy, end ? 1 : 0);
            Rectangle? rect = Bounds(copy.GetBoundingRectangles());
            if (rect.HasValue) return rect;
            // Many providers return no rectangle for a degenerate range. Expand
            // a clone to one character; this never changes selection or reads text.
            copy.ExpandToEnclosingUnit(0);
            rect = Bounds(copy.GetBoundingRectangles());
            return rect.HasValue ? new Rectangle(rect.Value.Left, rect.Value.Top, 1, rect.Value.Height) : null;
        }
        finally { Release(copy); }
    }

    internal static Rectangle? Bounds(double[]? values)
    {
        if (values == null || values.Length < 4) return null;
        double x = values[0], y = values[1], w = values[2], h = values[3];
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(w) || !double.IsFinite(h) ||
            Math.Abs(x) > 1000000 || Math.Abs(y) > 1000000 || w < 0 || w > 1000000 || h <= 0 || h > 1000000) return null;
        return new Rectangle((int)Math.Floor(x), (int)Math.Floor(y), Math.Max(1, (int)Math.Ceiling(w)), (int)Math.Ceiling(h));
    }

    private static Rectangle Rect(NativeRect rect) => Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
    private static Rectangle? Valid(Rectangle rect) => rect.Width > 0 && rect.Height > 0 ? rect : null;
    private static void Release(object? value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }

    public void Dispose()
    {
        Release(_previousSelection);
        Release(_client);
    }
}
