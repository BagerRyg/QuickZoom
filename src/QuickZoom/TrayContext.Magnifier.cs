using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    private const uint SPI_SETCURSORS = 0x0057;
    private const uint LWA_ALPHA = 0x00000002;
    private const int WS_CHILD = 0x40000000;
    private const int WS_VISIBLE = 0x10000000;
    private const int WS_DISABLED = 0x08000000;
    private const int MS_SHOWMAGNIFIEDCURSOR = 0x0001;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int ENUM_CURRENT_SETTINGS = -1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }
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

        public MonitorMagnifierWindow(Rectangle bounds, bool showMagnifiedCursor)
        {
            _host = new MonitorMagnifierHostForm(bounds);
            _host.Bounds = bounds;
            _ = _host.Handle;
            if (!SetLayeredWindowAttributes(_host.Handle, 0, 255, LWA_ALPHA))
            {
                ErrorLog.WriteThrottled("Magnification.SetLayeredWindowAttributes", new Win32Exception(Marshal.GetLastWin32Error()));
            }

            int style = WS_CHILD | WS_VISIBLE | WS_DISABLED;
            if (showMagnifiedCursor)
            {
                style |= MS_SHOWMAGNIFIEDCURSOR;
            }

            _magnifierHandle = CreateWindowEx(
                WS_EX_TRANSPARENT,
                "Magnifier",
                null,
                style,
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
                if (!MoveWindow(_magnifierHandle, 0, 0, bounds.Width, bounds.Height, true))
                {
                    ErrorLog.WriteThrottled("Magnification.MoveWindow", new Win32Exception(Marshal.GetLastWin32Error()));
                }
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

            if (!_hasLastFrame || _lastInvertColors != invertColors)
            {
                MAGCOLOREFFECT colorEffect = invertColors ? InvertColorEffect : IdentityColorEffect;
                if (!MagSetColorEffect(_magnifierHandle, ref colorEffect))
                {
                    ErrorLog.WriteThrottled("Magnification.SetColorEffect", "MagSetColorEffect failed.");
                    return;
                }
            }

            if (!_hasLastFrame || Math.Abs(_lastMagnification - magnification) >= 0.0001f)
            {
                var transform = new MAGTRANSFORM
                {
                    v00 = magnification,
                    v11 = magnification,
                    v22 = 1f
                };
                if (!MagSetWindowTransform(_magnifierHandle, ref transform))
                {
                    ErrorLog.WriteThrottled("Magnification.SetWindowTransform", "MagSetWindowTransform failed.");
                    return;
                }
            }

            if (!_hasLastFrame || !RectEquals(_lastSourceRect, sourceRect))
            {
                if (!MagSetWindowSource(_magnifierHandle, sourceRect))
                {
                    ErrorLog.WriteThrottled("Magnification.SetWindowSource", "MagSetWindowSource failed.");
                    return;
                }
            }

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

    private sealed class OverlayMagnifierHostForm : Form
    {
        public OverlayMagnifierHostForm(Rectangle bounds)
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

    private sealed class OverlayMagnifierWindow : IDisposable
    {
        private readonly OverlayMagnifierHostForm _host;
        private IntPtr _magnifierHandle;
        private bool _hasLastFrame;
        private RECT _lastSourceRect;
        private Rectangle _lastBounds;
        private Size _lastSize;
        private LensShape _lastShape = (LensShape)(-1);
        private float _lastMagnification;
        private MAGCOLOREFFECT _lastColorEffect;

        public IntPtr HostHandle => _host.Handle;
        public IntPtr MagnifierHandle => _magnifierHandle;

        public OverlayMagnifierWindow(Rectangle bounds, bool showMagnifiedCursor, LensShape shape)
        {
            _host = new OverlayMagnifierHostForm(bounds);
            _ = _host.Handle;
            if (!SetLayeredWindowAttributes(_host.Handle, 0, 255, LWA_ALPHA))
            {
                ErrorLog.WriteThrottled("OverlayMagnification.SetLayeredWindowAttributes", new Win32Exception(Marshal.GetLastWin32Error()));
            }

            int style = WS_CHILD | WS_VISIBLE | WS_DISABLED;
            if (showMagnifiedCursor)
            {
                style |= MS_SHOWMAGNIFIEDCURSOR;
            }

            _magnifierHandle = CreateWindowEx(
                WS_EX_TRANSPARENT,
                "Magnifier",
                null,
                style,
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
                throw new Win32Exception(error, "Failed to create overlay magnifier child window.");
            }

            _lastBounds = bounds;
            _lastSize = bounds.Size;
            _lastShape = shape;
            ApplyShape(shape);
            _host.Show();
        }

        public void UpdateBounds(Rectangle bounds, LensShape shape)
        {
            bool sizeChanged = _lastSize != bounds.Size;
            bool shapeChanged = _lastShape != shape;
            if (_lastBounds == bounds && !sizeChanged && !shapeChanged)
            {
                return;
            }

            _host.Bounds = bounds;
            _lastBounds = bounds;
            if (sizeChanged || shapeChanged)
            {
                _lastSize = bounds.Size;
                _lastShape = shape;
                ApplyShape(shape);
                _hasLastFrame = false;
            }

            if (sizeChanged &&
                _magnifierHandle != IntPtr.Zero &&
                !MoveWindow(_magnifierHandle, 0, 0, bounds.Width, bounds.Height, true))
            {
                ErrorLog.WriteThrottled("OverlayMagnification.MoveWindow", new Win32Exception(Marshal.GetLastWin32Error()));
            }
        }

        public void Apply(float magnification, RECT sourceRect, MAGCOLOREFFECT colorEffect)
        {
            if (_magnifierHandle == IntPtr.Zero)
            {
                return;
            }

            bool colorEffectChanged = !_hasLastFrame || !ColorEffectEquals(_lastColorEffect, colorEffect);
            if (_hasLastFrame &&
                Math.Abs(_lastMagnification - magnification) < 0.0001f &&
                RectEquals(_lastSourceRect, sourceRect) &&
                !colorEffectChanged)
            {
                return;
            }

            if (colorEffectChanged)
            {
                if (!MagSetColorEffect(_magnifierHandle, ref colorEffect))
                {
                    ErrorLog.WriteThrottled("OverlayMagnification.SetColorEffect", "MagSetColorEffect failed.");
                    return;
                }
            }

            if (!_hasLastFrame || Math.Abs(_lastMagnification - magnification) >= 0.0001f)
            {
                var transform = new MAGTRANSFORM
                {
                    v00 = magnification,
                    v11 = magnification,
                    v22 = 1f
                };
                if (!MagSetWindowTransform(_magnifierHandle, ref transform))
                {
                    ErrorLog.WriteThrottled("OverlayMagnification.SetWindowTransform", "MagSetWindowTransform failed.");
                    return;
                }
            }

            if (colorEffectChanged || !RectEquals(_lastSourceRect, sourceRect))
            {
                if (!MagSetWindowSource(_magnifierHandle, sourceRect))
                {
                    ErrorLog.WriteThrottled("OverlayMagnification.SetWindowSource", "MagSetWindowSource failed.");
                    return;
                }
            }

            _lastMagnification = magnification;
            _lastSourceRect = sourceRect;
            _lastColorEffect = colorEffect;
            _hasLastFrame = true;
        }

        public void ExcludeFromSource()
        {
            if (_magnifierHandle == IntPtr.Zero)
            {
                return;
            }

            IntPtr[] handles = [HostHandle];
            if (!MagSetWindowFilterList(_magnifierHandle, MW_FILTERMODE_EXCLUDE, handles.Length, handles))
            {
                ErrorLog.WriteThrottled("OverlayMagnification.FilterList", "MagSetWindowFilterList failed.");
            }
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

        private void ApplyShape(LensShape shape)
        {
            _host.Region?.Dispose();
            _host.Region = null;

            if (shape == LensShape.Rectangle || shape == LensShape.Square)
            {
                return;
            }

            using GraphicsPath path = new();
            Rectangle rect = new(Point.Empty, _host.Size);
            if (shape == LensShape.Circle)
            {
                path.AddEllipse(rect);
            }

            _host.Region = new Region(path);
        }

        public void Dispose()
        {
            _host.Hide();
            _host.Region?.Dispose();
            _host.Region = null;
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

    private static bool ColorEffectEquals(MAGCOLOREFFECT a, MAGCOLOREFFECT b)
    {
        return Math.Abs(a.v00 - b.v00) < 0.0001f &&
            Math.Abs(a.v11 - b.v11) < 0.0001f &&
            Math.Abs(a.v22 - b.v22) < 0.0001f &&
            Math.Abs(a.v33 - b.v33) < 0.0001f &&
            Math.Abs(a.v40 - b.v40) < 0.0001f &&
            Math.Abs(a.v41 - b.v41) < 0.0001f &&
            Math.Abs(a.v42 - b.v42) < 0.0001f &&
            Math.Abs(a.v44 - b.v44) < 0.0001f;
    }

    private void ResetFullscreenFrameCache()
    {
        _lastFullscreenMagnification = float.NaN;
        _lastFullscreenXOffset = int.MinValue;
        _lastFullscreenYOffset = int.MinValue;
        _lastFullscreenInvertColors = null;
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

        long now = Environment.TickCount64;
        if (now - _lastShellUiTrackingCheckTick < 100)
        {
            return;
        }

        _lastShellUiTrackingCheckTick = now;

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
            ResetFullscreenFrameCache();
            _monitorLayoutDirty = true;
            if (!_autoSwitchMonitor && GetCursorPos(out var ptLock))
            {
                _lockedScreen = Screen.FromPoint(new Point(ptLock.X, ptLock.Y));
            }
        }

        if (active)
        {
            if (_zoomMode != ZoomMode.Fullscreen)
            {
                DestroyMonitorWindows();
                if (_useFullscreenBackend)
                {
                    _ = MagSetFullscreenTransform(1.0f, 0, 0);
                    var identity = IdentityColorEffect;
                    _ = MagSetFullscreenColorEffect(ref identity);
                }

                _useFullscreenBackend = false;
                ResetFullscreenFrameCache();
                return;
            }

            DestroyOverlayWindow();
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
                ResetFullscreenFrameCache();
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
            DestroyOverlayWindow();
            DestroyMonitorWindows();
            if (wasFullscreenBackend)
            {
                _ = MagSetFullscreenTransform(1.0f, 0, 0);
                var identity = IdentityColorEffect;
                _ = MagSetFullscreenColorEffect(ref identity);
            }
            if (!MagUninitialize())
            {
                ErrorLog.WriteThrottled("Magnification.Uninitialize", "MagUninitialize failed.");
            }
            _magActive = false;
            _useFullscreenBackend = false;
            ResetFullscreenFrameCache();
            _monitorLayoutDirty = true;

            // Cursor reset is only needed for fullscreen magnification transitions.
            if (wasFullscreenBackend)
            {
                RestoreSystemCursorScheme();
            }
        }
    }

    private bool ShouldUseFullscreenBackend()
    {
        int selectedCount = GetSelectedScreens().Count;
        int allCount = GetOrderedScreens().Count;
        // Use the native fullscreen API only when the selected area is the full desktop.
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
                    window = new MonitorMagnifierWindow(screen.Bounds, !_cursorEnhancementEnabled);
                    _monitorWindows[screen.DeviceName] = window;
                }
                catch (Exception ex)
                {
                    ErrorLog.Write("SyncMonitorWindows", ex);
                    DisableMagAndReset();
                    MessageBox.Show(
                        L("Error.MagnifierInit"),
                        L("Common.AppName"),
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

    private void DestroyOverlayWindow()
    {
        _overlayWindow?.Dispose();
        _overlayWindow = null;
        _smoothedLensCenter = null;
    }

    private void RefreshMagnifierCursorRendering()
    {
        if (_zoomMode != ZoomMode.Fullscreen)
        {
            DestroyOverlayWindow();
            ApplyTransformCurrentPoint();
            return;
        }

        if (!_magActive || _useFullscreenBackend || _monitorWindows.Count == 0)
        {
            return;
        }

        DestroyMonitorWindows();
        _monitorLayoutDirty = true;
        SyncMonitorWindows();
        ApplyTransformCurrentPoint();
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
                if (!MagSetWindowFilterList(window.MagnifierHandle, MW_FILTERMODE_EXCLUDE, hostHandles.Length, hostHandles))
                {
                    ErrorLog.WriteThrottled("Magnification.FilterList", "MagSetWindowFilterList failed.");
                }
            }
        }
    }

    private void DisableMagAndReset()
    {
        _zoomPercent = 100;
        _animTargetPercent = 100;
        _animAnchorValid = false;
        EnsureMag(false);
        UpdateFollowTimerState();
    }

    private void ClampZoom()
    {
        _zoomPercent = Math.Max(MinPercent, Math.Min(_zoomPercent, _maxPercent));
        ApplyTransformCurrentPoint();
    }

    private static float PercentToMag(int percent) => Math.Max(1.0f, percent / 100f);

    private void ApplyTransformCurrentPoint()
    {
        if (_zoomMode != ZoomMode.Fullscreen && _zoomPercent <= 100 && !_invertColors)
        {
            DisableMagAndReset();
            return;
        }

        bool needsVisualEffect = _invertColors || _zoomPercent > 100;
        if (_autoDisableAt100 && !needsVisualEffect)
        {
            DisableMagAndReset();
            return;
        }

        EnsureMag(true);
        if (!_magActive)
        {
            UpdateFollowTimerState();
            return;
        }

        POINT point = GetReferencePointForTransform();
        ApplyTransformAtPoint(point, PercentToMag(_zoomPercent));
        UpdateFollowTimerState();
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

    private POINT GetReferencePointForTransform()
    {
        if (_zoomMode != ZoomMode.Fullscreen)
        {
            return GetCursorPos(out var overlayPoint) ? overlayPoint : default;
        }

        if (_animAnchorValid && _animTimer != null && _animTimer.Enabled && !_useFullscreenBackend)
        {
            return _animAnchorPoint;
        }

        return GetReferencePoint();
    }

    private void ApplyTransformAtPoint(POINT pt, float mag)
    {
        if (!_magActive)
        {
            return;
        }

        long frameStartTicks = Stopwatch.GetTimestamp();

        if (_zoomMode != ZoomMode.Fullscreen)
        {
            ApplyOverlayTransform(pt);
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

        if (selectedScreens.Count == 1)
        {
            LogSlowSingleMonitorFrame(frameStartTicks, selectedScreens[0]);
        }
    }

    private void LogSlowSingleMonitorFrame(long frameStartTicks, Screen selectedScreen)
    {
        double elapsedMs = (Stopwatch.GetTimestamp() - frameStartTicks) * 1000.0 / Stopwatch.Frequency;
        long now = Environment.TickCount64;
        if (elapsedMs < 9.0 || now - _lastSlowPerMonitorFrameLogTick < 3000)
        {
            return;
        }

        _lastSlowPerMonitorFrameLogTick = now;
        int refreshRate = GetScreenRefreshRate(selectedScreen);
        ErrorLog.Write(
            "PerMonitorPerf",
            $"Single-monitor frame took {elapsedMs:F2} ms on {GetFriendlyScreenLabel(selectedScreen, TryGetDisplayNumber(selectedScreen.DeviceName) ?? 1)} ({selectedScreen.DeviceName}), refresh={refreshRate}Hz, targetFps={GetEffectiveRenderingFps()}.");
    }

    private void ApplyOverlayTransform(POINT fallbackPoint)
    {
        Point anchor = GetTrackingPoint(new Point(fallbackPoint.X, fallbackPoint.Y));
        Screen screen = Screen.FromPoint(anchor);
        Rectangle bounds = _zoomMode == ZoomMode.Docked
            ? BuildDockBounds(screen.Bounds, anchor)
            : BuildLensBounds(anchor, screen.Bounds);

        float mag = PercentToMag(_zoomPercent);
        RECT sourceRect = BuildOverlaySourceRect(screen.Bounds, anchor, bounds.Size, mag);
        MAGCOLOREFFECT colorEffect = _invertColors ? InvertColorEffect : IdentityColorEffect;
        LensShape shape = _zoomMode == ZoomMode.Docked ? LensShape.Rectangle : _lensShape;

        try
        {
            bool showMagnifiedCursor = false;
            if (_overlayWindow == null)
            {
                KeepTrayPopupOpenForOverlayActivation();
            }

            if (_overlayWindow == null)
            {
                _overlayWindow = new OverlayMagnifierWindow(bounds, showMagnifiedCursor, shape);
                _overlayWindow.ExcludeFromSource();
            }

            _overlayWindow.UpdateBounds(bounds, shape);
            _overlayWindow.SetVisible(true);
            _overlayWindow.Apply(mag, sourceRect, colorEffect);
        }
        catch (Exception ex)
        {
            ErrorLog.Write("OverlayMagnification", ex);
            DisableMagAndReset();
            MessageBox.Show(
                L("Error.MagnifierInit"),
                L("Common.AppName"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private Rectangle BuildLensBounds(Point anchor, Rectangle screenBounds)
    {
        PointF target = new(anchor.X, anchor.Y);
        PointF center = _smoothedLensCenter.HasValue
            ? new PointF(
                (_smoothedLensCenter.Value.X * 0.68f) + (target.X * 0.32f),
                (_smoothedLensCenter.Value.Y * 0.68f) + (target.Y * 0.32f))
            : target;
        _smoothedLensCenter = center;

        int width = Math.Clamp(_lensSize, 100, Math.Max(100, screenBounds.Width));
        int height = _lensShape == LensShape.Rectangle
            ? Math.Clamp((int)Math.Round(width * 9.0 / 16.0), 56, Math.Max(56, screenBounds.Height))
            : width;
        int x = (int)Math.Round(center.X - (width / 2.0));
        int y = (int)Math.Round(center.Y - (height / 2.0));

        x = Math.Clamp(x, screenBounds.Left, Math.Max(screenBounds.Left, screenBounds.Right - width));
        y = Math.Clamp(y, screenBounds.Top, Math.Max(screenBounds.Top, screenBounds.Bottom - height));
        return new Rectangle(x, y, width, height);
    }

    private Rectangle BuildDockBounds(Rectangle screenBounds, Point anchor)
    {
        DockPosition position = _dockPosition;
        Rectangle preferredBounds = BuildDockBoundsForPosition(screenBounds, position);
        if (preferredBounds.Contains(anchor))
        {
            position = OppositeDockPosition(position);
        }

        return BuildDockBoundsForPosition(screenBounds, position);
    }

    private Rectangle BuildDockBoundsForPosition(Rectangle screenBounds, DockPosition position)
    {
        int sizePercent = Math.Clamp(_dockSizePercent, 10, 50);
        return position switch
        {
            DockPosition.Bottom => new Rectangle(
                screenBounds.Left,
                screenBounds.Bottom - Math.Max(80, (int)Math.Round(screenBounds.Height * sizePercent / 100.0)),
                screenBounds.Width,
                Math.Max(80, (int)Math.Round(screenBounds.Height * sizePercent / 100.0))),
            DockPosition.Left => new Rectangle(
                screenBounds.Left,
                screenBounds.Top,
                Math.Max(120, (int)Math.Round(screenBounds.Width * sizePercent / 100.0)),
                screenBounds.Height),
            DockPosition.Right => new Rectangle(
                screenBounds.Right - Math.Max(120, (int)Math.Round(screenBounds.Width * sizePercent / 100.0)),
                screenBounds.Top,
                Math.Max(120, (int)Math.Round(screenBounds.Width * sizePercent / 100.0)),
                screenBounds.Height),
            _ => new Rectangle(
                screenBounds.Left,
                screenBounds.Top,
                screenBounds.Width,
                Math.Max(80, (int)Math.Round(screenBounds.Height * sizePercent / 100.0)))
        };
    }

    private static DockPosition OppositeDockPosition(DockPosition position) => position switch
    {
        DockPosition.Bottom => DockPosition.Top,
        DockPosition.Left => DockPosition.Right,
        DockPosition.Right => DockPosition.Left,
        _ => DockPosition.Bottom
    };

    private static RECT BuildOverlaySourceRect(Rectangle sourceBounds, Point anchorPoint, Size viewportSize, float mag)
    {
        int viewW = Math.Max(1, (int)Math.Round(viewportSize.Width / mag));
        int viewH = Math.Max(1, (int)Math.Round(viewportSize.Height / mag));
        int offsetX = (int)Math.Round(anchorPoint.X - (viewW / 2.0));
        int offsetY = (int)Math.Round(anchorPoint.Y - (viewH / 2.0));

        int maxX = sourceBounds.Right - viewW;
        int maxY = sourceBounds.Bottom - viewH;
        offsetX = Math.Clamp(offsetX, sourceBounds.Left, Math.Max(sourceBounds.Left, maxX));
        offsetY = Math.Clamp(offsetY, sourceBounds.Top, Math.Max(sourceBounds.Top, maxY));

        return new RECT
        {
            left = offsetX,
            top = offsetY,
            right = offsetX + viewW,
            bottom = offsetY + viewH
        };
    }

    private Point GetTrackingPoint(Point fallback)
    {
        return fallback;
    }

    private Point GetFocusTrackingPoint(Point fallback)
    {
        if (TryGetAutomationFocusPoint(out Point automationPoint))
        {
            return automationPoint;
        }

        return TryGetGuiFocusPoint(out Point focusPoint) ? focusPoint : fallback;
    }

    private static bool TryGetAutomationFocusPoint(out Point point)
    {
        point = default;
        try
        {
            AutomationElement element = AutomationElement.FocusedElement;
            if (element == null)
            {
                return false;
            }

            System.Windows.Rect rect = element.Current.BoundingRectangle;
            if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0)
            {
                return false;
            }

            point = new Point((int)Math.Round(rect.Left + (rect.Width / 2.0)), (int)Math.Round(rect.Top + (rect.Height / 2.0)));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetGuiFocusPoint(out Point point)
    {
        point = default;
        IntPtr foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return false;
        }

        uint threadId = GetWindowThreadProcessId(foreground, out _);
        if (threadId == 0)
        {
            return false;
        }

        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        if (!GetGUIThreadInfo(threadId, ref info) || info.hwndFocus == IntPtr.Zero)
        {
            return false;
        }

        if (!GetWindowRect(info.hwndFocus, out RECT rect))
        {
            return false;
        }

        point = new Point(rect.left + ((rect.right - rect.left) / 2), rect.top + ((rect.bottom - rect.top) / 2));
        return true;
    }

    private static bool TryGetCaretPoint(out Point point)
    {
        point = default;
        IntPtr foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return false;
        }

        uint threadId = GetWindowThreadProcessId(foreground, out _);
        if (threadId == 0)
        {
            return false;
        }

        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        if (!GetGUIThreadInfo(threadId, ref info) || info.hwndCaret == IntPtr.Zero)
        {
            return false;
        }

        POINT caret = new()
        {
            X = info.rcCaret.left + ((info.rcCaret.right - info.rcCaret.left) / 2),
            Y = info.rcCaret.top + ((info.rcCaret.bottom - info.rcCaret.top) / 2)
        };

        if (!ClientToScreen(info.hwndCaret, ref caret))
        {
            return false;
        }

        point = new Point(caret.X, caret.Y);
        return true;
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
        if (_lastFullscreenInvertColors != _invertColors && !MagSetFullscreenColorEffect(ref colorEffect))
        {
            ErrorLog.WriteThrottled("Magnification.FullscreenColorEffect", "MagSetFullscreenColorEffect failed.");
        }
        else
        {
            _lastFullscreenInvertColors = _invertColors;
        }

        bool transformChanged = Math.Abs(_lastFullscreenMagnification - mag) >= 0.0001f ||
                                _lastFullscreenXOffset != xOffset ||
                                _lastFullscreenYOffset != yOffset;
        if (transformChanged && !MagSetFullscreenTransform(mag, xOffset, yOffset))
        {
            ErrorLog.WriteThrottled("Magnification.FullscreenTransform", "MagSetFullscreenTransform failed.");
        }
        else if (transformChanged)
        {
            _lastFullscreenMagnification = mag;
            _lastFullscreenXOffset = xOffset;
            _lastFullscreenYOffset = yOffset;
        }
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

    private int GetEffectiveRenderingFps()
    {
        return _fps == UnlimitedFps ? GetUnlimitedRenderingFps() : Math.Clamp(_fps, 60, 240);
    }

    private int GetUnlimitedRenderingFps()
    {
        int refreshRate = 60;
        foreach (Screen screen in Screen.AllScreens)
        {
            refreshRate = Math.Max(refreshRate, GetScreenRefreshRate(screen));
        }

        return refreshRate;
    }

    private int GetScreenRefreshRate(Screen screen)
    {
        try
        {
            var mode = new DEVMODE
            {
                dmSize = (ushort)Marshal.SizeOf<DEVMODE>()
            };

            if (EnumDisplaySettings(screen.DeviceName, ENUM_CURRENT_SETTINGS, ref mode) && mode.dmDisplayFrequency > 0)
            {
                return (int)Math.Clamp(mode.dmDisplayFrequency, 30u, 1000u);
            }
        }
        catch
        {
            // Ignore and fall back.
        }

        return 60;
    }
}
