using System;
using System.ComponentModel;
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
    private static extern bool MagSetWindowSource(IntPtr hwnd, RECT rect);

    [DllImport("Magnification.dll", ExactSpelling = true)]
    private static extern bool MagSetFullscreenTransform(float magLevel, int xOffset, int yOffset);

    [DllImport("Magnification.dll", ExactSpelling = true)]
    private static extern bool MagSetWindowTransform(IntPtr hwnd, [In] ref MAGTRANSFORM pTransform);

    [DllImport("Magnification.dll", ExactSpelling = true)]
    private static extern bool MagSetWindowFilterList(IntPtr hwnd, int dwFilterMode, int count, IntPtr[] pHWND);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle,
        string lpClassName,
        string? lpWindowName,
        int dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    private const uint SPI_SETCURSORS = 0x0057;
    private const uint LWA_ALPHA = 0x00000002;
    private const int WS_CHILD = 0x40000000;
    private const int WS_VISIBLE = 0x10000000;
    private const int WS_DISABLED = 0x08000000;
    private const int MS_SHOWMAGNIFIEDCURSOR = 0x0001;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_CLIPCHILDREN = 0x02000000;
    private const int MW_FILTERMODE_EXCLUDE = 0;
    private const int WM_NCHITTEST = 0x0084;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 0x0003;
    private const int HTTRANSPARENT = -1;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MAGTRANSFORM
    {
        public float v00; public float v01; public float v02;
        public float v10; public float v11; public float v12;
        public float v20; public float v21; public float v22;
    }

    private sealed class MonitorMagnifierHostForm : Form
    {
        public MonitorMagnifierHostForm(Rectangle bounds)
        {
            Bounds = bounds;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.Black;
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_LAYERED;
                cp.Style |= WS_CLIPCHILDREN;
                return cp;
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
    }

    private sealed class MonitorMagnifierWindow : IDisposable
    {
        private readonly MonitorMagnifierHostForm _host;
        private IntPtr _magnifierHandle;

        public IntPtr HostHandle => _host.Handle;
        public IntPtr MagnifierHandle => _magnifierHandle;

        public MonitorMagnifierWindow(Rectangle bounds)
        {
            _host = new MonitorMagnifierHostForm(bounds);
            _host.Bounds = bounds;
            _ = _host.Handle;
            _ = SetLayeredWindowAttributes(_host.Handle, 0, 255, LWA_ALPHA);

            _magnifierHandle = CreateWindowEx(
                WS_EX_TRANSPARENT,
                "Magnifier",
                null,
                WS_CHILD | WS_VISIBLE | WS_DISABLED | MS_SHOWMAGNIFIEDCURSOR,
                0,
                0,
                bounds.Width,
                bounds.Height,
                _host.Handle,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            if (_magnifierHandle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                _host.Close();
                _host.Dispose();
                throw new Win32Exception(error, "Failed to create magnifier child window.");
            }

            _host.Show();
        }

        public void UpdateBounds(Rectangle bounds)
        {
            _host.Bounds = bounds;
            if (_magnifierHandle != IntPtr.Zero)
            {
                _ = MoveWindow(_magnifierHandle, 0, 0, bounds.Width, bounds.Height, true);
            }
        }

        public void Apply(float magnification, RECT sourceRect)
        {
            if (_magnifierHandle == IntPtr.Zero)
            {
                return;
            }

            var transform = new MAGTRANSFORM
            {
                v00 = magnification,
                v11 = magnification,
                v22 = 1f
            };

            _ = MagSetWindowTransform(_magnifierHandle, ref transform);
            _ = MagSetWindowSource(_magnifierHandle, sourceRect);
        }

        public void Dispose()
        {
            _host.Hide();
            if (_magnifierHandle != IntPtr.Zero)
            {
                _ = DestroyWindow(_magnifierHandle);
                _magnifierHandle = IntPtr.Zero;
            }

            _host.Close();
            _host.Dispose();
        }
    }

    private void EnsureMag(bool active)
    {
        if (active && !_magActive)
        {
            _magActive = MagInitialize();
            _monitorLayoutDirty = true;
            if (!_autoSwitchMonitor && GetCursorPos(out var ptLock))
            {
                _lockedScreen = Screen.FromPoint(new Point(ptLock.X, ptLock.Y));
            }
        }

        if (active)
        {
            bool shouldUseFullscreen = ShouldUseFullscreenBackend();
            if (shouldUseFullscreen != _useFullscreenBackend)
            {
                if (shouldUseFullscreen)
                {
                    DestroyMonitorWindows();
                }
                else
                {
                    _ = MagSetFullscreenTransform(1.0f, 0, 0);
                    _monitorLayoutDirty = true;
                }

                _useFullscreenBackend = shouldUseFullscreen;
            }

            if (_useFullscreenBackend)
            {
                DestroyMonitorWindows();
            }
            else
            {
                int selectedCount = GetSelectedScreens().Count;
                if (_monitorLayoutDirty || _monitorWindows.Count != selectedCount)
                {
                    SyncMonitorWindows();
                    _monitorLayoutDirty = false;
                }
            }
        }
        else if (_magActive)
        {
            bool wasFullscreenBackend = _useFullscreenBackend;
            DestroyMonitorWindows();
            if (wasFullscreenBackend)
            {
                _ = MagSetFullscreenTransform(1.0f, 0, 0);
            }
            MagUninitialize();
            _magActive = false;
            _useFullscreenBackend = false;
            _monitorLayoutDirty = true;

            // Cursor reset is only needed for fullscreen magnification transitions.
            if (wasFullscreenBackend)
            {
                SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
            }
        }
    }

    private bool ShouldUseFullscreenBackend()
    {
        int selectedCount = GetSelectedScreens().Count;
        int allCount = Screen.AllScreens.Length;
        // Fullscreen API magnifies the entire desktop; use it only for all-displays mode.
        return selectedCount == allCount;
    }

    private void SyncMonitorWindows()
    {
        if (!_magActive)
        {
            return;
        }

        var selectedScreens = GetSelectedScreens();
        var selectedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Screen screen in selectedScreens)
        {
            selectedKeys.Add(screen.DeviceName);
            if (!_monitorWindows.TryGetValue(screen.DeviceName, out MonitorMagnifierWindow? window))
            {
                try
                {
                    window = new MonitorMagnifierWindow(screen.Bounds);
                    _monitorWindows[screen.DeviceName] = window;
                }
                catch
                {
                    DisableMagAndReset();
                    MessageBox.Show(
                        "QuickZoom could not initialize monitor magnification windows on this system. " +
                        "Magnification was turned off to avoid black overlays.",
                        "QuickZoom",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
            }
            else
            {
                window.UpdateBounds(screen.Bounds);
            }
        }

        var toRemove = new List<string>();
        foreach (var kvp in _monitorWindows)
        {
            if (!selectedKeys.Contains(kvp.Key))
            {
                kvp.Value.Dispose();
                toRemove.Add(kvp.Key);
            }
        }

        foreach (string key in toRemove)
        {
            _monitorWindows.Remove(key);
            _lastAnchorByMonitor.Remove(key);
        }

        ApplyMagnifierFilterLists();
    }

    private void DestroyMonitorWindows()
    {
        foreach (var window in _monitorWindows.Values)
        {
            window.Dispose();
        }

        _monitorWindows.Clear();
        _lastAnchorByMonitor.Clear();
    }

    private void ApplyMagnifierFilterLists()
    {
        if (_monitorWindows.Count == 0)
        {
            return;
        }

        var hostHandles = new IntPtr[_monitorWindows.Count];
        int i = 0;
        foreach (MonitorMagnifierWindow window in _monitorWindows.Values)
        {
            hostHandles[i++] = window.HostHandle;
        }

        foreach (MonitorMagnifierWindow window in _monitorWindows.Values)
        {
            if (window.MagnifierHandle != IntPtr.Zero)
            {
                _ = MagSetWindowFilterList(window.MagnifierHandle, MW_FILTERMODE_EXCLUDE, hostHandles.Length, hostHandles);
            }
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
        if (!_magActive)
        {
            return;
        }

        POINT point = GetReferencePoint();
        ApplyTransformAtPoint(point, PercentToMag(_zoomPercent));
    }

    private POINT GetReferencePoint()
    {
        if (_followCursor && GetCursorPos(out var pt))
        {
            return pt;
        }

        if (_staticCenter.X != 0 || _staticCenter.Y != 0)
        {
            return _staticCenter;
        }

        return GetCursorPos(out pt) ? pt : default;
    }

    private void ApplyTransformAtPoint(POINT pt, float mag)
    {
        if (!_magActive)
        {
            return;
        }

        var selectedScreens = GetSelectedScreens();
        if (selectedScreens.Count == 0)
        {
            return;
        }

        if (_useFullscreenBackend)
        {
            ApplyFullscreenTransform(pt, mag, selectedScreens);
            return;
        }

        Screen cursorScreen = Screen.FromPoint(new Point(pt.X, pt.Y));
        Screen lockedScreen = _lockedScreen ?? cursorScreen;
        if (!_autoSwitchMonitor)
        {
            _lockedScreen ??= lockedScreen;
        }

        foreach (Screen screen in selectedScreens)
        {
            if (!_monitorWindows.TryGetValue(screen.DeviceName, out MonitorMagnifierWindow? window))
            {
                continue;
            }

            Point anchorPoint;
            if (_autoSwitchMonitor)
            {
                if (string.Equals(screen.DeviceName, cursorScreen.DeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    anchorPoint = new Point(pt.X, pt.Y);
                    _lastAnchorByMonitor[screen.DeviceName] = anchorPoint;
                }
                else
                {
                    if (!_lastAnchorByMonitor.TryGetValue(screen.DeviceName, out anchorPoint))
                    {
                        anchorPoint = new Point(
                            screen.Bounds.Left + (screen.Bounds.Width / 2),
                            screen.Bounds.Top + (screen.Bounds.Height / 2));
                    }
                }
            }
            else
            {
                if (string.Equals(screen.DeviceName, lockedScreen.DeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    anchorPoint = new Point(pt.X, pt.Y);
                    _lastAnchorByMonitor[screen.DeviceName] = anchorPoint;
                }
                else
                {
                    if (!_lastAnchorByMonitor.TryGetValue(screen.DeviceName, out anchorPoint))
                    {
                        anchorPoint = new Point(
                            screen.Bounds.Left + (screen.Bounds.Width / 2),
                            screen.Bounds.Top + (screen.Bounds.Height / 2));
                    }
                }
            }

            RECT sourceRect = BuildSourceRect(screen.Bounds, anchorPoint, mag);
            window.Apply(mag, sourceRect);
        }
    }

    private void ApplyFullscreenTransform(POINT pt, float mag, List<Screen> selectedScreens)
    {
        Point anchorPoint = new(pt.X, pt.Y);
        Rectangle bounds;
        bool relativeToVirtualScreen = false;

        if (selectedScreens.Count == 1)
        {
            Screen selected = selectedScreens[0];
            bounds = selected.Bounds;

            if (selected.Bounds.Contains(anchorPoint))
            {
                _lastAnchorByMonitor[selected.DeviceName] = anchorPoint;
            }
            else if (!_lastAnchorByMonitor.TryGetValue(selected.DeviceName, out anchorPoint))
            {
                anchorPoint = new Point(
                    selected.Bounds.Left + (selected.Bounds.Width / 2),
                    selected.Bounds.Top + (selected.Bounds.Height / 2));
            }
        }
        else
        {
            // Fullscreen offsets are relative to the primary monitor, so when we
            // want to roam the entire desktop we must first clamp against the
            // virtual screen and then translate that source position back into
            // primary-monitor-relative offsets.
            bounds = SystemInformation.VirtualScreen;
            relativeToVirtualScreen = true;
        }

        RECT rect = BuildSourceRect(bounds, anchorPoint, mag);
        int xOffset = rect.left;
        int yOffset = rect.top;

        if (relativeToVirtualScreen)
        {
            xOffset -= (int)Math.Round(bounds.Left / mag);
            yOffset -= (int)Math.Round(bounds.Top / mag);
        }

        _ = MagSetFullscreenTransform(mag, xOffset, yOffset);
    }

    private RECT BuildSourceRect(Rectangle bounds, Point anchorPoint, float mag)
    {
        int viewW = Math.Max(1, (int)Math.Round(bounds.Width / mag));
        int viewH = Math.Max(1, (int)Math.Round(bounds.Height / mag));

        int offsetX;
        int offsetY;

        if (_centerCursor)
        {
            offsetX = (int)Math.Round(anchorPoint.X - (viewW / 2.0));
            offsetY = (int)Math.Round(anchorPoint.Y - (viewH / 2.0));
        }
        else
        {
            int relX = anchorPoint.X - bounds.Left;
            int relY = anchorPoint.Y - bounds.Top;
            offsetX = (int)Math.Round(anchorPoint.X - (relX / mag));
            offsetY = (int)Math.Round(anchorPoint.Y - (relY / mag));
        }

        int minX = bounds.Left;
        int minY = bounds.Top;
        int maxX = bounds.Right - viewW;
        int maxY = bounds.Bottom - viewH;

        if (offsetX < minX) offsetX = minX;
        if (offsetY < minY) offsetY = minY;
        if (offsetX > maxX) offsetX = maxX;
        if (offsetY > maxY) offsetY = maxY;

        return new RECT
        {
            left = offsetX,
            top = offsetY,
            right = offsetX + viewW,
            bottom = offsetY + viewH
        };
    }
}
