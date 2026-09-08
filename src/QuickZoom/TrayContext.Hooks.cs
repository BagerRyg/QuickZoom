using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickZoom;

internal sealed partial class TrayContext
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_XBUTTONDOWN = 0x020B;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const uint LLKHF_EXTENDED = 0x01;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private static readonly IntPtr ShortcutReplayExtraInfo = new(unchecked((long)0x515A535550505245UL));

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint cInputs, INPUT[] pInputs, int cbSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

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

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mouse;
        [FieldOffset(0)] public KEYBDINPUT keyboard;
        [FieldOffset(0)] public HARDWAREINPUT hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    private void InstallHook()
    {
        _proc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;

        _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(curModule.ModuleName!), 0);
        if (_hook == IntPtr.Zero)
        {
            ErrorLog.Write("InstallHook", new Win32Exception(Marshal.GetLastWin32Error(), "Failed to set the low-level mouse hook."));
            MessageBox.Show(L("Error.MouseHookFailed"), L("Common.AppName"), MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            ErrorLog.Write("InstallKeyboardHook", new Win32Exception(Marshal.GetLastWin32Error(), "Failed to set the low-level keyboard hook."));
            MessageBox.Show(L("Error.KeyboardHookFailed"), L("Common.AppName"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            ExitThread();
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            return HookCallbackCore(nCode, wParam, lParam);
        }
        catch (Exception ex)
        {
            _wheelDeltaRemainder = 0;
            _leftMouseButtonPressed = false;
            _rightMouseButtonPressed = false;
            _zoomModeMouseChordTriggered = false;
            _suppressLeftMouseButtonUp = false;
            _suppressRightMouseButtonUp = false;
            ErrorLog.WriteThrottled("MouseHook", ex);
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }
    }

    private IntPtr HookCallbackCore(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam.ToInt32() == WM_MOUSEMOVE && _wiggleSpotlightEnabled)
        {
            MSLLHOOKSTRUCT movement = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            RecordCursorMovementForSpotlight(new Point(movement.pt.X, movement.pt.Y));
        }

        if (nCode >= 0)
        {
            int message = wParam.ToInt32();

            if (TryHandleZoomModeMouseChord(message))
            {
                return (IntPtr)1;
            }

            if (_invertEnabled &&
                MouseShortcutsAllowed() &&
                (message == WM_MBUTTONDOWN || message == WM_XBUTTONDOWN) &&
                MatchesInvertMouseTrigger(Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam), message))
            {
                MarkEnableKeyUsedByQuickZoom();
                ToggleInvertColors();
                return (IntPtr)1;
            }

            if (message == WM_MOUSEWHEEL)
            {
                if (_enabled && _enableKeyPressed && MouseShortcutsAllowed())
                {
                    var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    int wheelDelta = (short)((data.mouseData >> 16) & 0xFFFF);
                    _wheelDeltaRemainder += wheelDelta;
                    int detents = _wheelDeltaRemainder / 120;
                    _wheelDeltaRemainder %= 120;
                    detents = Math.Max(-3, Math.Min(3, detents));

                    MarkEnableKeyUsedByQuickZoom();
                    HandleZoomDetents(detents);
                    return (IntPtr)1;
                }

                _wheelDeltaRemainder = 0;
            }
        }

        if (!_enabled && !_invertEnabled)
        {
            _wheelDeltaRemainder = 0;
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            return KeyboardHookCallbackCore(nCode, wParam, lParam);
        }
        catch (Exception ex)
        {
            _enableKeyPressed = false;
            _invertKeyPressed = false;
            _followCursorKeyPressed = false;
            _zoomModeCycleKeyPressed = false;
            _controlKeyPressed = false;
            _altGrPressed = false;
            ResetEnableKeySuppressionState();
            _suppressedShortcutKeyUps.Clear();
            _wheelDeltaRemainder = 0;
            ErrorLog.WriteThrottled("KeyboardHook", ex);
            return CallNextHookEx(_kbdHook, nCode, wParam, lParam);
        }
    }

    private IntPtr KeyboardHookCallbackCore(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return CallNextHookEx(_kbdHook, nCode, wParam, lParam);
        }

        int message = wParam.ToInt32();
        var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
        int vk = (int)data.vkCode;
        if (data.dwExtraInfo == ShortcutReplayExtraInfo)
        {
            return CallNextHookEx(_kbdHook, nCode, wParam, lParam);
        }

        if (vk == (int)Keys.PrintScreen && IsQuickZoomForeground())
        {
            return CallNextHookEx(_kbdHook, nCode, wParam, lParam);
        }

        if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
        {
            if (IsControlKey(vk))
            {
                _controlKeyPressed = true;
            }

            if (vk == (int)Keys.RMenu && _controlKeyPressed)
            {
                _altGrPressed = true;
                if (TryReplaySuppressedEnableKeyWith(data))
                {
                    return (IntPtr)1;
                }

                return CallNextHookEx(_kbdHook, nCode, wParam, lParam);
            }

            if (_altGrPressed)
            {
                return CallNextHookEx(_kbdHook, nCode, wParam, lParam);
            }

            if (_controlKeyPressed &&
                IsAltEnableKey() &&
                IsEnableKeyMatch(_enableKey, vk))
            {
                return CallNextHookEx(_kbdHook, nCode, wParam, lParam);
            }

            if (IsEnableKeyMatch(_enableKey, vk))
            {
                bool wasAlreadyPressed = _enableKeyPressed;
                _enableKeyPressed = true;

                if (_suppressShortcutKeystrokes)
                {
                    if (!_enableKeyDownSuppressed)
                    {
                        BeginEnableKeySuppression(data);
                    }
                    else if (wasAlreadyPressed &&
                             !_enableKeyUsedByQuickZoom &&
                             !_replayedEnableKeyDown &&
                             TryReplaySuppressedEnableKeyDown())
                    {
                        return (IntPtr)1;
                    }

                    if (_enableKeyDownSuppressed && !_replayedEnableKeyDown)
                    {
                        return (IntPtr)1;
                    }
                }
            }

            bool zoomModeCyclePressed = KeyboardShortcutsAllowed() &&
                                        _enableKeyPressed &&
                                        !IsEnableKeyMatch(_enableKey, vk) &&
                                        vk == (int)Keys.Z;
            if (zoomModeCyclePressed)
            {
                if (!_zoomModeCycleKeyPressed)
                {
                    _zoomModeCycleKeyPressed = true;
                    CycleZoomModeShortcut();
                }

                MarkEnableKeyUsedByQuickZoom();
                _suppressedShortcutKeyUps.Add(vk);
                return (IntPtr)1;
            }

            if (_enableKeyPressed && IsProtectedAltShortcut(vk))
            {
                if (TryReplaySuppressedEnableKeyWith(data))
                {
                    return (IntPtr)1;
                }

                return CallNextHookEx(_kbdHook, nCode, wParam, lParam);
            }

            bool invertKeyPressed = _invertEnabled &&
                                    KeyboardShortcutsAllowed() &&
                                    !IsEnableKeyMatch(_enableKey, vk) &&
                                    IsInvertKeyMatch(vk) &&
                                    _enableKeyPressed;

            if (invertKeyPressed)
            {
                if (!_invertKeyPressed)
                {
                    _invertKeyPressed = true;
                    ToggleInvertColors();
                }

                MarkEnableKeyUsedByQuickZoom();
                _suppressedShortcutKeyUps.Add(vk);
                return (IntPtr)1;
            }

            bool followCursorTogglePressed = KeyboardShortcutsAllowed() &&
                                             !IsEnableKeyMatch(_enableKey, vk) &&
                                             IsFollowCursorKeyMatch(vk) &&
                                             _enableKeyPressed;

            if (followCursorTogglePressed)
            {
                if (!_followCursorKeyPressed)
                {
                    _followCursorKeyPressed = true;
                    SetFollowCursor(!_followCursor);
                }

                MarkEnableKeyUsedByQuickZoom();
                _suppressedShortcutKeyUps.Add(vk);
                return (IntPtr)1;
            }

            if (_enabled && _enableKeyPressed && KeyboardShortcutsAllowed())
            {
                const int VK_OEM_PLUS = 0xBB;
                const int VK_OEM_MINUS = 0xBD;
                const int VK_ADD = 0x6B;
                const int VK_SUBTRACT = 0x6D;

                if (vk == VK_OEM_PLUS || vk == VK_ADD)
                {
                    MarkEnableKeyUsedByQuickZoom();
                    _suppressedShortcutKeyUps.Add(vk);
                    HandleZoomDetents(+1);
                    return (IntPtr)1;
                }

                if (vk == VK_OEM_MINUS || vk == VK_SUBTRACT)
                {
                    MarkEnableKeyUsedByQuickZoom();
                    _suppressedShortcutKeyUps.Add(vk);
                    HandleZoomDetents(-1);
                    return (IntPtr)1;
                }
            }

            if (_enableKeyPressed &&
                _enableKeyDownSuppressed &&
                !_enableKeyUsedByQuickZoom &&
                !_replayedEnableKeyDown &&
                TryReplaySuppressedEnableKeyWith(data))
            {
                return (IntPtr)1;
            }
        }
        else if (message == WM_KEYUP || message == WM_SYSKEYUP)
        {
            if (vk == (int)Keys.RMenu && _altGrPressed)
            {
                _altGrPressed = false;
                return CallNextHookEx(_kbdHook, nCode, wParam, lParam);
            }

            if (_altGrPressed)
            {
                if (IsControlKey(vk))
                {
                    _controlKeyPressed = false;
                }

                return CallNextHookEx(_kbdHook, nCode, wParam, lParam);
            }

            if (IsControlKey(vk))
            {
                _controlKeyPressed = false;
            }

            if (_enableKeyPressed && IsEnableKeyMatch(_enableKey, vk))
            {
                bool keyDownWasSuppressed = _enableKeyDownSuppressed;
                bool keyDownWasReplayed = _replayedEnableKeyDown;
                bool keyWasUsedByQuickZoom = _enableKeyUsedByQuickZoom;
                _enableKeyPressed = false;
                _wheelDeltaRemainder = 0;

                if (keyDownWasSuppressed && !keyDownWasReplayed && !keyWasUsedByQuickZoom)
                {
                    _ = TryReplaySuppressedEnableKeyPress();
                }

                ResetEnableKeySuppressionState();
                if (keyDownWasSuppressed && !keyDownWasReplayed)
                {
                    return (IntPtr)1;
                }
            }

            if (_suppressedShortcutKeyUps.Remove(vk))
            {
                if (IsInvertKeyMatch(vk))
                {
                    _invertKeyPressed = false;
                }

                if (IsFollowCursorKeyMatch(vk))
                {
                    _followCursorKeyPressed = false;
                }

                if (vk == (int)Keys.Z)
                {
                    _zoomModeCycleKeyPressed = false;
                }

                return (IntPtr)1;
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

    private static bool IsControlKey(int vk)
    {
        return vk == (int)Keys.ControlKey || vk == (int)Keys.LControlKey || vk == (int)Keys.RControlKey;
    }

    private bool IsAltEnableKey()
    {
        return _enableKey == Keys.Menu || _enableKey == Keys.LMenu || _enableKey == Keys.RMenu;
    }

    private bool IsProtectedAltShortcut(int vk)
    {
        return IsAltEnableKey() &&
               (vk == (int)Keys.F4 || (vk == (int)Keys.Delete && _controlKeyPressed));
    }

    private void BeginEnableKeySuppression(KBDLLHOOKSTRUCT data)
    {
        _enableKeyDownSuppressed = true;
        _enableKeyUsedByQuickZoom = false;
        _replayedEnableKeyDown = false;
        _suppressedEnableVirtualKey = (int)data.vkCode;
        _suppressedEnableScanCode = (int)data.scanCode;
        _suppressedEnableExtended = (data.flags & LLKHF_EXTENDED) != 0;
    }

    private void MarkEnableKeyUsedByQuickZoom()
    {
        if (_enableKeyDownSuppressed)
        {
            _enableKeyUsedByQuickZoom = true;
        }
    }

    private void ResetEnableKeySuppressionState()
    {
        _enableKeyDownSuppressed = false;
        _enableKeyUsedByQuickZoom = false;
        _replayedEnableKeyDown = false;
        _suppressedEnableVirtualKey = 0;
        _suppressedEnableScanCode = 0;
        _suppressedEnableExtended = false;
    }

    private bool TryReplaySuppressedEnableKeyDown()
    {
        if (!_enableKeyDownSuppressed || _replayedEnableKeyDown)
        {
            return false;
        }

        bool sent = TrySendKeyboardInputs(CreateKeyboardInput(
            _suppressedEnableVirtualKey,
            _suppressedEnableScanCode,
            _suppressedEnableExtended,
            keyUp: false));
        if (sent)
        {
            _replayedEnableKeyDown = true;
        }

        return sent;
    }

    private bool TryReplaySuppressedEnableKeyWith(KBDLLHOOKSTRUCT currentKey)
    {
        if (!_enableKeyDownSuppressed || _replayedEnableKeyDown || _enableKeyUsedByQuickZoom)
        {
            return false;
        }

        bool sent = TrySendKeyboardInputs(
            CreateKeyboardInput(
                _suppressedEnableVirtualKey,
                _suppressedEnableScanCode,
                _suppressedEnableExtended,
                keyUp: false),
            CreateKeyboardInput(
                (int)currentKey.vkCode,
                (int)currentKey.scanCode,
                (currentKey.flags & LLKHF_EXTENDED) != 0,
                keyUp: false));
        if (sent)
        {
            _replayedEnableKeyDown = true;
        }

        return sent;
    }

    private bool TryReplaySuppressedEnableKeyPress()
    {
        if (!_enableKeyDownSuppressed || _replayedEnableKeyDown)
        {
            return false;
        }

        return TrySendKeyboardInputs(
            CreateKeyboardInput(
                _suppressedEnableVirtualKey,
                _suppressedEnableScanCode,
                _suppressedEnableExtended,
                keyUp: false),
            CreateKeyboardInput(
                _suppressedEnableVirtualKey,
                _suppressedEnableScanCode,
                _suppressedEnableExtended,
                keyUp: true));
    }

    private static INPUT CreateKeyboardInput(int virtualKey, int scanCode, bool extended, bool keyUp)
    {
        uint flags = extended ? KEYEVENTF_EXTENDEDKEY : 0;
        if (keyUp)
        {
            flags |= KEYEVENTF_KEYUP;
        }

        return new INPUT
        {
            type = INPUT_KEYBOARD,
            data = new INPUTUNION
            {
                keyboard = new KEYBDINPUT
                {
                    wVk = (ushort)virtualKey,
                    wScan = (ushort)scanCode,
                    dwFlags = flags,
                    dwExtraInfo = ShortcutReplayExtraInfo
                }
            }
        };
    }

    private static bool TrySendKeyboardInputs(params INPUT[] inputs)
    {
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent == inputs.Length)
        {
            return true;
        }

        ErrorLog.WriteThrottled(
            "ShortcutSuppression.SendInput",
            new Win32Exception(Marshal.GetLastWin32Error(), "Could not replay a non-QuickZoom keyboard shortcut."));
        return false;
    }

    private static bool IsQuickZoomForeground()
    {
        IntPtr foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return false;
        }

        GetWindowThreadProcessId(foreground, out uint processId);
        return processId == Environment.ProcessId;
    }

    private bool IsInvertKeyMatch(int vk)
    {
        return vk == (int)_invertKey;
    }

    private bool IsFollowCursorKeyMatch(int vk)
    {
        return vk == (int)_followCursorKey;
    }

    private bool MatchesInvertMouseTrigger(MSLLHOOKSTRUCT data, int message)
    {
        if (!_enableKeyPressed)
        {
            return false;
        }

        return message == WM_MBUTTONDOWN;
    }

    private bool TryHandleZoomModeMouseChord(int message)
    {
        bool leftEvent = message is WM_LBUTTONDOWN or WM_LBUTTONUP;
        bool rightEvent = message is WM_RBUTTONDOWN or WM_RBUTTONUP;
        if (!leftEvent && !rightEvent)
        {
            return false;
        }

        if (message == WM_LBUTTONDOWN)
        {
            _leftMouseButtonPressed = true;
        }
        else if (message == WM_LBUTTONUP)
        {
            _leftMouseButtonPressed = false;
        }
        else if (message == WM_RBUTTONDOWN)
        {
            _rightMouseButtonPressed = true;
        }
        else
        {
            _rightMouseButtonPressed = false;
        }

        bool suppress = false;
        if (_enableKeyPressed && MouseShortcutsAllowed())
        {
            if (message == WM_LBUTTONDOWN)
            {
                _suppressLeftMouseButtonUp = true;
                suppress = true;
            }
            else if (message == WM_RBUTTONDOWN)
            {
                _suppressRightMouseButtonUp = true;
                suppress = true;
            }

            if (_leftMouseButtonPressed &&
                _rightMouseButtonPressed &&
                !_zoomModeMouseChordTriggered)
            {
                _zoomModeMouseChordTriggered = true;
                MarkEnableKeyUsedByQuickZoom();
                CycleZoomModeShortcut();
                suppress = true;
            }
        }

        if (message == WM_LBUTTONUP && _suppressLeftMouseButtonUp)
        {
            _suppressLeftMouseButtonUp = false;
            suppress = true;
        }
        else if (message == WM_RBUTTONUP && _suppressRightMouseButtonUp)
        {
            _suppressRightMouseButtonUp = false;
            suppress = true;
        }

        if (!_leftMouseButtonPressed && !_rightMouseButtonPressed)
        {
            _zoomModeMouseChordTriggered = false;
        }

        return suppress;
    }

    private void ToggleInvertColors()
    {
        ResetExitConfirmation();
        _invertColors = !_invertColors;

        if (!_invertColors && _zoomPercent <= 100 && _autoDisableAt100)
        {
            DisableMagAndReset();
        }
        else
        {
            ApplyTransformCurrentPoint();
        }

        SaveSettings();
        RefreshMenuAndTrayUi();
    }

    private void HandleZoomDetents(int detents)
    {
        if (detents == 0)
        {
            return;
        }

        ResetExitConfirmation();

        bool animateZoom = _smoothZoom && AccessibilityPreferences.AnimationsEnabled;
        int basePercent = animateZoom ? _animTargetPercent : _zoomPercent;
        int newTarget = Math.Clamp(basePercent + (detents * _stepPercent), MinPercent, _maxPercent);

        if (animateZoom)
        {
            // Avoid perceived delay when stepping up from 100% with smooth animation.
            if (_zoomPercent <= 100 && newTarget > 100)
            {
                _zoomPercent = 101;
                ApplyTransformCurrentPoint();
            }

            if (GetCursorPos(out var animPt))
            {
                _animAnchorPoint = animPt;
                _animAnchorValid = true;
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
            _animAnchorValid = false;
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
