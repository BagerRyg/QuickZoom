using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickZoom;

internal sealed partial class TrayContext
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public int mouseData;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private void InstallHook()
    {
        _proc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;

        _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(curModule.ModuleName!), 0);
        if (_hook == IntPtr.Zero)
        {
            MessageBox.Show("Failed to set low-level mouse hook.", "QuickZoom", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ExitThread();
        }
    }

    private void InstallKeyboardHook()
    {
        _kbdProc = KeyboardHookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;

        _kbdHook = SetWindowsHookEx(WH_KEYBOARD_LL, _kbdProc, GetModuleHandle(curModule.ModuleName!), 0);
        if (_kbdHook == IntPtr.Zero)
        {
            MessageBox.Show("Failed to set low-level keyboard hook.", "QuickZoom", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ExitThread();
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (!_enabled)
        {
            _enableKeyPressed = false;
            _wheelDeltaRemainder = 0;
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        if (nCode >= 0 && wParam.ToInt32() == WM_MOUSEWHEEL)
        {
            if (_enableKeyPressed)
            {
                var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                int wheelDelta = (short)((data.mouseData >> 16) & 0xFFFF);
                _wheelDeltaRemainder += wheelDelta;
                int detents = _wheelDeltaRemainder / 120;
                _wheelDeltaRemainder %= 120;
                detents = Math.Max(-3, Math.Min(3, detents));

                HandleZoomDetents(detents);
                return (IntPtr)1;
            }

            _wheelDeltaRemainder = 0;
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (!_enabled)
        {
            _enableKeyPressed = false;
            return CallNextHookEx(_kbdHook, nCode, wParam, lParam);
        }

        if (nCode < 0)
        {
            return CallNextHookEx(_kbdHook, nCode, wParam, lParam);
        }

        int message = wParam.ToInt32();
        var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
        int vk = (int)data.vkCode;

        if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
        {
            if (IsEnableKeyMatch(_enableKey, vk))
            {
                _enableKeyPressed = true;
            }

            if (_enableKeyPressed)
            {
                const int VK_OEM_PLUS = 0xBB;
                const int VK_OEM_MINUS = 0xBD;
                const int VK_ADD = 0x6B;
                const int VK_SUBTRACT = 0x6D;

                if (vk == VK_OEM_PLUS || vk == VK_ADD)
                {
                    HandleZoomDetents(+1);
                    return (IntPtr)1;
                }

                if (vk == VK_OEM_MINUS || vk == VK_SUBTRACT)
                {
                    HandleZoomDetents(-1);
                    return (IntPtr)1;
                }
            }
        }
        else if (message == WM_KEYUP || message == WM_SYSKEYUP)
        {
            if (IsEnableKeyMatch(_enableKey, vk))
            {
                _enableKeyPressed = false;
                _wheelDeltaRemainder = 0;
            }
        }

        return CallNextHookEx(_kbdHook, nCode, wParam, lParam);
    }

    private static bool IsEnableKeyMatch(Keys enableKey, int vk)
    {
        return enableKey switch
        {
            Keys.ControlKey => vk == (int)Keys.ControlKey || vk == (int)Keys.LControlKey || vk == (int)Keys.RControlKey,
            Keys.Menu => vk == (int)Keys.Menu || vk == (int)Keys.LMenu || vk == (int)Keys.RMenu,
            Keys.ShiftKey => vk == (int)Keys.ShiftKey || vk == (int)Keys.LShiftKey || vk == (int)Keys.RShiftKey,
            Keys.LWin => vk == (int)Keys.LWin || vk == (int)Keys.RWin,
            Keys.RWin => vk == (int)Keys.LWin || vk == (int)Keys.RWin,
            _ => vk == (int)enableKey
        };
    }

    private void HandleZoomDetents(int detents)
    {
        if (detents == 0)
        {
            return;
        }

        int basePercent = _smoothZoom ? _animTargetPercent : _zoomPercent;
        int newTarget = Math.Max(MinPercent, Math.Min(_maxPercent, basePercent + (detents * _stepPercent)));

        if (_smoothZoom)
        {
            // Avoid perceived delay when stepping up from 100% with smooth animation.
            if (_zoomPercent <= 100 && newTarget > 100)
            {
                _zoomPercent = 101;
                ApplyTransformCurrentPoint();
            }

            _animStartPercent = _zoomPercent;
            _animTargetPercent = newTarget;
            _animElapsedMs = 0;

            if (!_animTimer.Enabled)
            {
                _animTimer.Start();
            }
        }
        else
        {
            _zoomPercent = newTarget;
            _animTargetPercent = _zoomPercent;
            ApplyTransformCurrentPoint();
        }

        if (GetCursorPos(out var pt))
        {
            _staticCenter = pt;
        }
    }
}
