using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
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
    private static extern bool MagSetFullscreenColorEffect([In] ref MAGCOLOREFFECT pEffect);

    [DllImport("Magnification.dll", ExactSpelling = true)]
    private static extern bool MagSetWindowTransform(IntPtr hwnd, [In] ref MAGTRANSFORM pTransform);

    [DllImport("Magnification.dll", ExactSpelling = true)]
    private static extern bool MagSetColorEffect(IntPtr hwnd, [In] ref MAGCOLOREFFECT pEffect);

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

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

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

    [StructLayout(LayoutKind.Sequential)]
    private struct MAGCOLOREFFECT
    {
        public float v00; public float v01; public float v02; public float v03; public float v04;
        public float v10; public float v11; public float v12; public float v13; public float v14;
        public float v20; public float v21; public float v22; public float v23; public float v24;
        public float v30; public float v31; public float v32; public float v33; public float v34;
        public float v40; public float v41; public float v42; public float v43; public float v44;
    }

    private static readonly MAGCOLOREFFECT IdentityColorEffect = new()
    {
        v00 = 1f,
        v11 = 1f,
        v22 = 1f,
        v33 = 1f,
        v44 = 1f
    };

    private static readonly MAGCOLOREFFECT InvertColorEffect = new()
    {
        v00 = -1f,
        v11 = -1f,
        v22 = -1f,
        v33 = 1f,
        v40 = 1f,
        v41 = 1f,
        v42 = 1f,
        v44 = 1f
    };

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
        private bool _hasLastFrame;
        private RECT _lastSourceRect;
        private float _lastMagnification;
        private bool _lastInvertColors;

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
            _hasLastFrame = false;
            if (_magnifierHandle != IntPtr.Zero)
            {
                _ = MoveWindow(_magnifierHandle, 0, 0, bounds.Width, bounds.Height, true);
            }
        }

        public void Apply(float magnification, RECT sourceRect, bool invertColors)
        {
            if (_magnifierHandle == IntPtr.Zero)
            {
                return;
            }

            if (_hasLastFrame &&
                Math.Abs(_lastMagnification - magnification) < 0.0001f &&
                RectEquals(_lastSourceRect, sourceRect) &&
                _lastInvertColors == invertColors)
            {
                return;
            }

            var transform = new MAGTRANSFORM
            {
                v00 = magnification,
                v11 = magnification,
                v22 = 1f
            };

            MAGCOLOREFFECT colorEffect = invertColors ? InvertColorEffect : IdentityColorEffect;
            _ = MagSetColorEffect(_magnifierHandle, ref colorEffect);
            _ = MagSetWindowTransform(_magnifierHandle, ref transform);
            _ = MagSetWindowSource(_magnifierHandle, sourceRect);
            _lastMagnification = magnification;
            _lastSourceRect = sourceRect;
            _lastInvertColors = invertColors;
            _hasLastFrame = true;
        }

        public void SetVisible(bool visible)
        {
            if (visible)
            {
                if (!_host.Visible)
                {
                    _host.Show();
                }
            }
            else if (_host.Visible)
            {
                _host.Hide();
            }
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

    private static bool RectEquals(RECT a, RECT b)
    {
        return a.left == b.left &&
            a.top == b.top &&
            a.right == b.right &&
            a.bottom == b.bottom;
    }

    private bool IsPerMonitorTrackingSuspended => _suspendPerMonitorTrackingForMenu || _suspendPerMonitorTrackingForShellUi;

    private void SetPerMonitorWindowsVisible(bool visible)
    {
        foreach (MonitorMagnifierWindow window in _monitorWindows.Values)
        {
            window.SetVisible(visible);
        }
    }

    private void UpdateShellUiTrackingState()
    {
        if (_useFullscreenBackend || !_magActive)
        {
            if (_suspendPerMonitorTrackingForShellUi)
            {
                _suspendPerMonitorTrackingForShellUi = false;
                SetPerMonitorWindowsVisible(!IsPerMonitorTrackingSuspended);
            }

            return;
        }

        bool shouldSuspend = IsShellPopupForeground();
        if (shouldSuspend == _suspendPerMonitorTrackingForShellUi)
        {
            return;
        }

        _suspendPerMonitorTrackingForShellUi = shouldSuspend;
        SetPerMonitorWindowsVisible(!IsPerMonitorTrackingSuspended);
    }

    private static bool IsShellPopupForeground()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        var className = new StringBuilder(256);
        if (GetClassName(hwnd, className, className.Capacity) <= 0)
        {
            return false;
        }

        string cls = className.ToString();
        return string.Equals(cls, "#32768", StringComparison.Ordinal) ||
            string.Equals(cls, "Shell_TrayWnd", StringComparison.Ordinal) ||
            string.Equals(cls, "NotifyIconOverflowWindow", StringComparison.Ordinal) ||
            string.Equals(cls, "TopLevelWindowForOverflowXamlIsland", StringComparison.Ordinal) ||
            string.Equals(cls, "Xaml_WindowedPopupClass", StringComparison.Ordinal);
    }

    private void EnsureMag(bool active)
    {
        if (active && !_magActive)
        {
            _magActive = MagInitialize();
            if (!_magActive)
            {
                if (!_magInitializationFailureLogged)
                {
                    _magInitializationFailureLogged = true;
                    ErrorLog.Write("Magnification", "MagInitialize failed. Magnification is not available in the current session.");
                }

                return;
            }

            _magInitializationFailureLogged = false;
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
                    var identity = IdentityColorEffect;
                    _ = MagSetFullscreenColorEffect(ref identity);
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
                var selectedScreens = GetSelectedScreens();
                if (_monitorLayoutDirty || !MonitorWindowLayoutMatches(selectedScreens))
                {
                    SyncMonitorWindows(selectedScreens);
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
                var identity = IdentityColorEffect;
                _ = MagSetFullscreenColorEffect(ref identity);
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

    private bool MonitorWindowLayoutMatches(List<Screen> selectedScreens)
    {
        if (_monitorWindows.Count != selectedScreens.Count)
        {
            return false;
        }

        foreach (Screen screen in selectedScreens)
        {
            if (!_monitorWindows.ContainsKey(screen.DeviceName))
            {
                return false;
            }
        }

        return true;
    }

    private void SyncMonitorWindows(List<Screen>? selectedScreens = null)
    {
        if (!_magActive)
        {
            return;
        }

        selectedScreens ??= GetSelectedScreens();
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
                catch (Exception ex)
                {
                    ErrorLog.Write("SyncMonitorWindows", ex);
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
        bool needsVisualEffect = _invertColors || _zoomPercent > 100;
        if (_autoDisableAt100 && !needsVisualEffect)
        {
            DisableMagAndReset();
            return;
        }

        UpdateShellUiTrackingState();
        EnsureMag(true);
        if (!_magActive)
        {
            return;
        }

        UpdateShellUiTrackingState();
        if (IsPerMonitorTrackingSuspended && !_useFullscreenBackend)
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

        UpdateShellUiTrackingState();

        if (IsPerMonitorTrackingSuspended)
        {
            return;
        }

        if (_monitorLayoutDirty || !MonitorWindowLayoutMatches(selectedScreens))
        {
            SyncMonitorWindows(selectedScreens);
            _monitorLayoutDirty = false;
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
            if (selectedScreens.Count == 1)
            {
                if (_useCursorMonitorSelection ||
                    string.Equals(screen.DeviceName, cursorScreen.DeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    anchorPoint = new Point(pt.X, pt.Y);
                    _lastAnchorByMonitor[screen.DeviceName] = anchorPoint;
                }
                else if (!_lastAnchorByMonitor.TryGetValue(screen.DeviceName, out anchorPoint))
                {
                    anchorPoint = new Point(
                        screen.Bounds.Left + (screen.Bounds.Width / 2),
                        screen.Bounds.Top + (screen.Bounds.Height / 2));
                }

                _lastAnchorByMonitor[screen.DeviceName] = anchorPoint;
            }
            else if (_autoSwitchMonitor)
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
            window.Apply(mag, sourceRect, _invertColors);
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

        MAGCOLOREFFECT colorEffect = _invertColors ? InvertColorEffect : IdentityColorEffect;
        _ = MagSetFullscreenColorEffect(ref colorEffect);
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
