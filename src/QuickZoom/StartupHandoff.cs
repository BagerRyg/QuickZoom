using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace QuickZoom;

// Per-user, per-process signals contain no executable paths or commands. The
// process start time prevents a reused PID from inheriting a stale signal.
internal static class StartupHandoff
{
    private const uint Synchronize = 0x00100000;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeWaitHandle OpenEvent(uint desiredAccess, bool inheritHandle, string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(SafeWaitHandle handle, uint milliseconds);

    internal static IDisposable? MarkYielding() => CreateMarker("Yielding");
    internal static IDisposable? MarkReady() => CreateMarker("Ready");
    internal static bool IsYielding(Process process) => IsMarked(process, "Yielding");
    internal static bool IsReady(Process process) => IsMarked(process, "Ready");

    private static IDisposable? CreateMarker(string kind)
    {
        try
        {
            using Process process = Process.GetCurrentProcess();
            var signal = new EventWaitHandle(false, EventResetMode.ManualReset, EventName(process, kind), out bool created);
            if (!created)
            {
                signal.Dispose();
                return null;
            }
            signal.Set();
            return signal;
        }
        catch (Exception ex)
        {
            ErrorLog.Write("StartupHandoff.Create", ex);
            return null;
        }
    }

    private static bool IsMarked(Process process, string kind)
    {
        try
        {
            if (process.HasExited) return false;
            // Opening with synchronization rights alone allows a normal launcher
            // to observe an elevated child's signal without requesting writes.
            using SafeWaitHandle handle = OpenEvent(Synchronize, false, EventName(process, kind));
            return !handle.IsInvalid && WaitForSingleObject(handle, 0) == 0 && !process.HasExited;
        }
        catch (Exception ex)
        {
            ErrorLog.WriteThrottled("StartupHandoff.Read", ex);
            return false;
        }
    }

    private static string EventName(Process process, string kind)
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        string sid = identity.User?.Value ?? throw new InvalidOperationException("The Windows user has no SID.");
        return $@"Local\QuickZoom.{kind}.{sid}.{process.Id}.{process.StartTime.ToUniversalTime().Ticks}";
    }
}
