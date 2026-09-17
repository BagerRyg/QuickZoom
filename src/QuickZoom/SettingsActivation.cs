using System;
using System.Security.Principal;
using System.Threading;

namespace QuickZoom;

// A signal only: the existing process opens its own Settings window. No commands,
// paths or user data are accepted from another process.
internal sealed class SettingsActivation : IDisposable
{
    private readonly EventWaitHandle _signal;
    private readonly RegisteredWaitHandle _registration;
    private int _disposed;
    private static string EventName
    {
        get
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            return @"Local\QuickZoom.OpenSettings." + identity.User?.Value;
        }
    }

    internal SettingsActivation(Action openSettings, string? eventName = null)
    {
        _signal = new EventWaitHandle(false, EventResetMode.AutoReset, eventName ?? EventName);
        try
        {
            _registration = ThreadPool.RegisterWaitForSingleObject(_signal, (_, _) =>
            {
                if (Volatile.Read(ref _disposed) != 0) return;
                try { openSettings(); }
                catch (Exception ex) { ErrorLog.WriteThrottled("Settings.Activate", ex); }
            }, null, Timeout.Infinite, executeOnlyOnce: false);
        }
        catch
        {
            _signal.Dispose();
            throw;
        }
    }

    internal static bool Request(string? eventName = null)
    {
        try
        {
            if (!EventWaitHandle.TryOpenExisting(eventName ?? EventName, out EventWaitHandle? signal)) return false;
            using (signal) return signal.Set();
        }
        catch (UnauthorizedAccessException) { return false; }
        catch (WaitHandleCannotBeOpenedException) { return false; }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _registration.Unregister(null);
        _signal.Dispose();
    }
}
