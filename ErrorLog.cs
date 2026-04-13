using System;
using System.IO;

namespace QuickZoom;

internal static class ErrorLog
{
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
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, entry);
        }
        catch
        {
            // Best effort.
        }
    }
}
