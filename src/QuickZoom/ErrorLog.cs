using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace QuickZoom;

// Session-only, opt-in diagnostics. Raw messages and exception messages can contain
// user content, paths, task XML or window titles, so they are never serialized.
internal static class ErrorLog
{
    internal const int MaxFileBytes = 1024 * 1024;
    private static readonly object Sync = new();
    private static readonly Queue<string> Pending = new();
    private static readonly Dictionary<string, long> LastWrite = new(StringComparer.Ordinal);
    private static bool _enabled;
    private static bool _workerRunning;
    private static string _path = string.Empty;

    internal static bool IsEnabled { get { lock (Sync) return _enabled; } }

    internal static bool StartSession(bool strictDataMode, string path, string version)
    {
        lock (Sync)
        {
            StopCore();
            if (strictDataMode) return false;
            try
            {
                _path = Path.GetFullPath(path);
                Append($"{DateTime.UtcNow:O} SESSION QuickZoom={SafeToken(version)}; local diagnostics; raw messages omitted{Environment.NewLine}");
                _enabled = true;
                return true;
            }
            catch
            {
                StopCore();
                return false;
            }
        }
    }

    internal static void Stop()
    {
        // Sharing the write lock ensures no queued event is written after Stop returns.
        lock (Sync) StopCore();
    }

    private static void StopCore()
    {
        _enabled = false;
        Pending.Clear();
        LastWrite.Clear();
    }

    internal static void Write(string source, Exception? exception, [CallerLineNumber] int line = 0) => Enqueue(source, exception, "ERROR", line);
    internal static void Write(string source, string message, [CallerLineNumber] int line = 0) => Enqueue(source, null, "EVENT", line);
    internal static void WriteAlways(string source, string message, [CallerLineNumber] int line = 0) => Enqueue(source, null, "EVENT", line);
    internal static void WriteCrash(string source, Exception? exception, [CallerLineNumber] int line = 0) => Enqueue(source, exception, "CRASH", line, immediate: true);
    internal static void WriteCrash(string source, string message, [CallerLineNumber] int line = 0) => Enqueue(source, null, "CRASH", line, immediate: true);
    internal static void WriteThrottled(string source, Exception? exception, [CallerLineNumber] int line = 0) => Enqueue(source, exception, "ERROR", line, TimeSpan.FromSeconds(10));
    internal static void WriteThrottled(string source, string message, [CallerLineNumber] int line = 0) => Enqueue(source, null, "EVENT", line, TimeSpan.FromSeconds(10));
    internal static void WriteThrottled(string source, string message, TimeSpan interval, [CallerLineNumber] int line = 0) => Enqueue(source, null, "EVENT", line, interval);

    private static void Enqueue(string source, Exception? exception, string kind, int line,
        TimeSpan? interval = null, bool immediate = false)
    {
        lock (Sync)
        {
            if (!_enabled) return;
            string eventId = SafeToken(source) + ":" + line.ToString(CultureInfo.InvariantCulture);
            long now = Environment.TickCount64;
            if (LastWrite.TryGetValue(eventId, out long previous) && now - previous < (interval ?? TimeSpan.FromMilliseconds(250)).TotalMilliseconds)
                return;
            if (LastWrite.Count >= 256) LastWrite.Clear();
            LastWrite[eventId] = now;
            string details = exception == null ? string.Empty :
                $" type={SafeToken(exception.GetType().Name)} hresult=0x{exception.HResult:X8}" +
                (exception is Win32Exception native ? $" win32={native.NativeErrorCode}" : string.Empty);
            string entry = $"{DateTime.UtcNow:O} {kind} {eventId}{details}{Environment.NewLine}";
            if (immediate)
            {
                try { Append(entry); }
                catch { StopCore(); }
                return;
            }
            if (Pending.Count >= 128) return;
            Pending.Enqueue(entry);
            if (_workerRunning) return;
            _workerRunning = true;
            ThreadPool.QueueUserWorkItem(_ => Drain());
        }
    }

    private static void Drain()
    {
        while (true)
        {
            lock (Sync)
            {
                if (!_enabled || Pending.Count == 0)
                {
                    _workerRunning = false;
                    return;
                }
                try { Append(Pending.Dequeue()); }
                catch { StopCore(); }
            }
        }
    }

    private static void Append(string entry)
    {
        LocalStorage.RunAsUser(() =>
        {
            LocalStorage.RequireLocalPath(_path);
            string previousPath = _path + ".previous";
            LocalStorage.RequireLocalPath(previousPath);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            if (File.Exists(_path) && new FileInfo(_path).Length + Encoding.UTF8.GetByteCount(entry) > MaxFileBytes)
                File.Move(_path, previousPath, overwrite: true);
            File.AppendAllText(_path, entry, new UTF8Encoding(false));
        });
    }

    private static string SafeToken(string value) => value.Length <= 96 &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' or ' ')
            ? value : "redacted";

    internal static string FormatElapsed(TimeSpan elapsed)
    {
        return elapsed.TotalHours >= 1
            ? elapsed.ToString(@"h\:mm\:ss\.fff")
            : elapsed.ToString(@"m\:ss\.fff");
    }
}
