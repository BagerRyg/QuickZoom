using System;
using Microsoft.Win32;

namespace QuickZoom;

internal sealed partial class TrayContext
{
    private void SubscribePowerAndSessionChanges()
    {
        try
        {
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;
        }
        catch (Exception ex)
        {
            ErrorLog.WriteAlways("Power.Subscribe", ex.ToString());
        }
    }

    private void UnsubscribePowerAndSessionChanges()
    {
        try
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
        }
        catch (Exception ex)
        {
            ErrorLog.WriteAlways("Power.Unsubscribe", ex.ToString());
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
        {
            ErrorLog.WriteAlways("Power.Suspend", "System suspend notification received.");
            return;
        }

        if (e.Mode == PowerModes.Resume)
        {
            ErrorLog.WriteAlways("Power.Resume", "System resume notification received.");
            RecoverAfterResume("Power.Resume");
        }
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.SessionLogon)
        {
            ErrorLog.WriteAlways("Session." + e.Reason, "Session switch notification received.");
            RecoverAfterResume("Session." + e.Reason);
        }
    }

    private void RecoverAfterResume(string source)
    {
        RunOnUiThread(source, () =>
        {
            _enableKeyPressed = false;
            _invertKeyPressed = false;
            _followCursorKeyPressed = false;
            _controlKeyPressed = false;
            _suppressEnableKeyForForeground = false;
            _wheelDeltaRemainder = 0;
            _animAnchorValid = false;
            _animTimer?.Stop();
            _monitorLayoutDirty = true;

            EnsureSelectedMonitorsValid();
            EnsureLockedScreenStillValid();

            if (!_startupInitialized)
            {
                if (IsShellReady())
                {
                    CompleteStartupInitialization();
                }
            }
            else
            {
                RestoreTrayIcon();
            }

            if (_magActive || _zoomPercent > 100 || _invertColors)
            {
                ApplyTransformCurrentPoint();
            }

            ErrorLog.WriteAlways(source, "Resume recovery completed.");
        });
    }
}
