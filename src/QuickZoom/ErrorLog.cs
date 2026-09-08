using System;
using System.Diagnostics;

namespace QuickZoom;

// Logging is deliberately unavailable. Conditional calls also omit argument evaluation.
internal static class ErrorLog
{
    [Conditional("QUICKZOOM_DIAGNOSTICS_DISABLED")]
    internal static void Configure(bool debugLoggingEnabled, string versionHash) { }

    [Conditional("QUICKZOOM_DIAGNOSTICS_DISABLED")]
    internal static void Write(string source, Exception? exception) { }

    [Conditional("QUICKZOOM_DIAGNOSTICS_DISABLED")]
    internal static void Write(string source, string message) { }

    [Conditional("QUICKZOOM_DIAGNOSTICS_DISABLED")]
    internal static void WriteThrottled(string source, Exception? exception) { }

    [Conditional("QUICKZOOM_DIAGNOSTICS_DISABLED")]
    internal static void WriteThrottled(string source, string message) { }

    [Conditional("QUICKZOOM_DIAGNOSTICS_DISABLED")]
    internal static void WriteThrottled(string source, string message, TimeSpan interval) { }

    [Conditional("QUICKZOOM_DIAGNOSTICS_DISABLED")]
    internal static void WriteAlways(string source, string message) { }

    [Conditional("QUICKZOOM_DIAGNOSTICS_DISABLED")]
    internal static void WriteCrash(string source, Exception? exception) { }

    [Conditional("QUICKZOOM_DIAGNOSTICS_DISABLED")]
    internal static void WriteCrash(string source, string message) { }

    [Conditional("QUICKZOOM_DIAGNOSTICS_DISABLED")]
    internal static void EnsureLogFileExists() { }

    internal static string FormatElapsed(TimeSpan elapsed)
    {
        return elapsed.TotalHours >= 1
            ? elapsed.ToString(@"h\:mm\:ss\.fff")
            : elapsed.ToString(@"m\:ss\.fff");
    }
}
