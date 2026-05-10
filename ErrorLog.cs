using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace QuickZoom;

internal static class ErrorLog
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, DateTime> LastThrottledWriteUtc = new(StringComparer.Ordinal);
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private const long MaxLogBytes = 1024 * 1024;
    private static readonly TimeSpan DefaultThrottleInterval = TimeSpan.FromSeconds(30);

    internal static void Write(string source, Exception? exception)
    {
        string message = exception?.ToString() ?? "Unknown exception.";
        Write(source, message);
    }

    internal static void WriteThrottled(string source, Exception? exception)
    {
        WriteThrottled(source, exception?.ToString() ?? "Unknown exception.", DefaultThrottleInterval);
    }

    internal static void WriteThrottled(string source, string message)
    {
        WriteThrottled(source, message, DefaultThrottleInterval);
    }

    internal static void WriteThrottled(string source, string message, TimeSpan interval)
    {
        string throttleKey = source + "\n" + NormalizeThrottleMessage(message);
        if (!ShouldWriteThrottled(throttleKey, interval))
        {
            return;
        }

        Write(source, message);
    }

    internal static void Write(string source, string message)
    {
        string entry =
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {message}{Environment.NewLine}" +
            $"    Build={AppInfo.DisplayVersion}; PID={Environment.ProcessId}; BasePath={AppContext.BaseDirectory}{Environment.NewLine}{Environment.NewLine}";
        WriteToPath(AppPaths.RuntimeLogPath, entry);
        WriteToPath(AppPaths.AppDataLogPath, entry);
    }

    private static bool ShouldWriteThrottled(string key, TimeSpan interval)
    {
        DateTime nowUtc = DateTime.UtcNow;
        lock (Sync)
        {
            if (LastThrottledWriteUtc.TryGetValue(key, out DateTime lastUtc) &&
                nowUtc - lastUtc < interval)
            {
                return false;
            }

            LastThrottledWriteUtc[key] = nowUtc;
            return true;
        }
    }

    private static string NormalizeThrottleMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        int newlineIndex = message.IndexOfAny(['\r', '\n']);
        string firstLine = newlineIndex >= 0 ? message[..newlineIndex] : message;
        return firstLine.Length <= 300 ? firstLine : firstLine[..300];
    }

    private static void WriteToPath(string path, string entry)
    {
        try
        {
            lock (Sync)
            {
                string? directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                RotateIfNeeded(path);
                File.AppendAllText(path, entry, Utf8NoBom);
            }
        }
        catch
        {
            // Best effort.
        }
    }

    private static void RotateIfNeeded(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            var info = new FileInfo(path);
            if (info.Length < MaxLogBytes)
            {
                return;
            }

            string archivePath = path + ".previous";
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }

            File.Move(path, archivePath);
        }
        catch
        {
            // Keep logging best-effort even if rotation fails.
        }
    }
}
