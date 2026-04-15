using System;
using System.IO;
using System.Text;

namespace QuickZoom;

internal static class ErrorLog
{
    private static readonly object Sync = new();
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private const long MaxLogBytes = 1024 * 1024;

    internal static void Write(string source, Exception? exception)
    {
        string message = exception?.ToString() ?? "Unknown exception.";
        Write(source, message);
    }

    internal static void Write(string source, string message)
    {
        string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {message}{Environment.NewLine}{Environment.NewLine}";
        WriteToPath(AppPaths.RuntimeLogPath, entry);
        WriteToPath(AppPaths.AppDataLogPath, entry);
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
