using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace QuickZoom;

internal static class ErrorLog
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, DateTime> LastThrottledWriteUtc = new(StringComparer.Ordinal);
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly DateTime ProcessStartTimeUtc = DateTime.UtcNow;
    private static readonly System.Diagnostics.Stopwatch ProcessUptime = System.Diagnostics.Stopwatch.StartNew();
    private const long MaxLogBytes = 1024 * 1024;
    private static readonly TimeSpan DefaultThrottleInterval = TimeSpan.FromSeconds(30);
    private static bool _debugLoggingEnabled;
    private static string? _configuredVersionHash;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern int GetKeyboardType(int nTypeFlag);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int MDT_EFFECTIVE_DPI = 0;

    private struct POINT
    {
        public int X;
        public int Y;
    }

    internal static void Configure(bool debugLoggingEnabled, string versionHash)
    {
        lock (Sync)
        {
            _debugLoggingEnabled = debugLoggingEnabled;
            string normalizedHash = string.IsNullOrWhiteSpace(versionHash) ? AppInfo.VersionHash : versionHash.Trim();
            if (string.Equals(_configuredVersionHash, normalizedHash, StringComparison.Ordinal))
            {
                return;
            }

            WipeLogsIfVersionChanged(normalizedHash);
            _configuredVersionHash = normalizedHash;
        }
    }

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
        if (!IsDebugLoggingEnabled())
        {
            return;
        }

        string throttleKey = source + "\n" + NormalizeThrottleMessage(message);
        if (!ShouldWriteThrottled(throttleKey, interval))
        {
            return;
        }

        Write(source, message);
    }

    internal static void Write(string source, string message)
    {
        if (!IsDebugLoggingEnabled())
        {
            return;
        }

        WriteCore(source, message);
    }

    internal static void WriteAlways(string source, string message)
    {
        EnsureConfigured();
        WriteCore(source, message);
    }

    internal static void WriteCrash(string source, Exception? exception)
    {
        string message = exception?.ToString() ?? "Unknown exception.";
        WriteCrash(source, message);
    }

    internal static void WriteCrash(string source, string message)
    {
        EnsureConfigured();
        WriteCore(source, message);
    }

    internal static void EnsureLogFileExists()
    {
        EnsureConfigured();
        WriteToPath(AppPaths.AppDataLogPath, string.Empty);
    }

    private static void WriteCore(string source, string message)
    {
        string entry =
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {message}{Environment.NewLine}" +
            $"    Build={AppInfo.DisplayVersion}; PID={Environment.ProcessId}; Uptime={FormatElapsed(ProcessUptime.Elapsed)}; BasePath={AppContext.BaseDirectory}{Environment.NewLine}{Environment.NewLine}";
        WriteToPath(AppPaths.RuntimeLogPath, entry);
        WriteToPath(AppPaths.AppDataLogPath, entry);
    }

    private static bool IsDebugLoggingEnabled()
    {
        EnsureConfigured();
        lock (Sync)
        {
            return _debugLoggingEnabled;
        }
    }

    private static void EnsureConfigured()
    {
        if (_configuredVersionHash != null)
        {
            return;
        }

        Configure(debugLoggingEnabled: false, AppInfo.VersionHash);
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
                EnsureHeaderWritten(path);
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

    private static void EnsureHeaderWritten(string path)
    {
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length > 0)
            {
                return;
            }

            File.WriteAllText(path, CreateSystemInfoHeader(), Utf8NoBom);
        }
        catch
        {
            // Best effort.
        }
    }

    private static string CreateSystemInfoHeader()
    {
        var builder = new StringBuilder();
        builder.AppendLine("QuickZoom diagnostic log");
        builder.AppendLine("============================================================");
        builder.AppendLine($"Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"ProcessStartedUtc: {ProcessStartTimeUtc:yyyy-MM-dd HH:mm:ss}Z");
        builder.AppendLine($"QuickZoom: {AppInfo.DisplayVersion} ({AppInfo.VersionHash})");
        builder.AppendLine($"Process: PID={Environment.ProcessId}; BasePath={AppContext.BaseDirectory}");
        builder.AppendLine($"Machine: {Environment.MachineName}; User={Environment.UserName}");
        builder.AppendLine($"Windows: {GetWindowsVersion()}");
        builder.AppendLine($".NET: {Environment.Version}; OSArchitecture={RuntimeInformation.OSArchitecture}; ProcessArchitecture={RuntimeInformation.ProcessArchitecture}");
        int keyboardType = GetKeyboardType(0);
        builder.AppendLine($"Input: MousePresent={SystemInformation.MousePresent}; MouseButtons={SystemInformation.MouseButtons}; MouseWheelPresent={SystemInformation.MouseWheelPresent}; KeyboardPresent={keyboardType > 0}; KeyboardType={keyboardType}; KeyboardSpeed={SystemInformation.KeyboardSpeed}; KeyboardDelay={SystemInformation.KeyboardDelay}");
        builder.AppendLine($"SystemMetrics: Monitors={GetSystemMetrics(80)}; RemoteSession={SystemInformation.TerminalServerSession}");
        builder.AppendLine("Displays:");
        foreach (Screen screen in Screen.AllScreens)
        {
            (float dpiX, float dpiY) = GetDisplayDpi(screen);

            builder.AppendLine($"  - {screen.DeviceName}; Primary={screen.Primary}; Bounds={screen.Bounds.Width}x{screen.Bounds.Height}@{screen.Bounds.X},{screen.Bounds.Y}; WorkingArea={screen.WorkingArea.Width}x{screen.WorkingArea.Height}; Dpi={dpiX:0.#}x{dpiY:0.#}; Scale={(dpiX > 0 ? dpiX / 96f * 100f : 0):0.#}%");
        }

        builder.AppendLine("Graphics:");
        foreach (string adapter in GetGraphicsAdapterInfo())
        {
            builder.AppendLine("  - " + adapter);
        }

        builder.AppendLine("============================================================");
        builder.AppendLine();
        return builder.ToString();
    }

    private static (float DpiX, float DpiY) GetDisplayDpi(Screen screen)
    {
        try
        {
            var point = new POINT
            {
                X = screen.Bounds.Left + Math.Max(1, screen.Bounds.Width / 2),
                Y = screen.Bounds.Top + Math.Max(1, screen.Bounds.Height / 2)
            };
            IntPtr monitor = MonitorFromPoint(point, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero &&
                GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) == 0 &&
                dpiX > 0 &&
                dpiY > 0)
            {
                return (dpiX, dpiY);
            }
        }
        catch
        {
            // Fall through to process DPI.
        }

        try
        {
            using Graphics graphics = Graphics.FromHwnd(IntPtr.Zero);
            return (graphics.DpiX, graphics.DpiY);
        }
        catch
        {
            return (0, 0);
        }
    }

    internal static string FormatElapsed(TimeSpan elapsed)
    {
        return elapsed.TotalHours >= 1
            ? elapsed.ToString(@"h\:mm\:ss\.fff")
            : elapsed.ToString(@"m\:ss\.fff");
    }

    private static string GetWindowsVersion()
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            string productName = key?.GetValue("ProductName")?.ToString() ?? Environment.OSVersion.VersionString;
            string displayVersion = key?.GetValue("DisplayVersion")?.ToString() ?? key?.GetValue("ReleaseId")?.ToString() ?? string.Empty;
            string build = key?.GetValue("CurrentBuildNumber")?.ToString() ?? Environment.OSVersion.Version.Build.ToString();
            string ubr = key?.GetValue("UBR")?.ToString() ?? string.Empty;
            return string.IsNullOrWhiteSpace(ubr)
                ? $"{productName} {displayVersion} (Build {build})"
                : $"{productName} {displayVersion} (Build {build}.{ubr})";
        }
        catch
        {
            return Environment.OSVersion.VersionString;
        }
    }

    private static IEnumerable<string> GetGraphicsAdapterInfo()
    {
        try
        {
            using RegistryKey? classKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
            if (classKey == null)
            {
                return ["Unavailable"];
            }

            var adapters = new List<string>();
            foreach (string subKeyName in classKey.GetSubKeyNames().Where(name => name.All(char.IsDigit)).OrderBy(name => name, StringComparer.Ordinal))
            {
                using RegistryKey? adapterKey = classKey.OpenSubKey(subKeyName);
                string? description = adapterKey?.GetValue("DriverDesc")?.ToString();
                if (string.IsNullOrWhiteSpace(description))
                {
                    continue;
                }

                string driverVersion = adapterKey?.GetValue("DriverVersion")?.ToString() ?? "unknown driver";
                string provider = adapterKey?.GetValue("ProviderName")?.ToString() ?? "unknown provider";
                string adapterType = GuessGraphicsAdapterType(description);
                adapters.Add($"{description}; Type={adapterType}; Driver={driverVersion}; Provider={provider}");
            }

            return adapters.Count > 0 ? adapters : ["Unavailable"];
        }
        catch
        {
            return ["Unavailable"];
        }
    }

    private static string GuessGraphicsAdapterType(string description)
    {
        string text = description.ToLowerInvariant();
        if (text.Contains("intel") || text.Contains("radeon graphics") || text.Contains("vega"))
        {
            return "likely integrated";
        }

        if (text.Contains("nvidia") || text.Contains("geforce") || text.Contains("quadro") || text.Contains("radeon rx") || text.Contains("arc"))
        {
            return "likely discrete";
        }

        return "unknown";
    }

    private static void WipeLogsIfVersionChanged(string versionHash)
    {
        try
        {
            string markerPath = AppPaths.AppDataLogPath + ".version";
            string? directory = Path.GetDirectoryName(markerPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string previousHash = File.Exists(markerPath) ? File.ReadAllText(markerPath, Utf8NoBom).Trim() : string.Empty;
            if (string.Equals(previousHash, versionHash, StringComparison.Ordinal))
            {
                return;
            }

            DeleteLogFile(AppPaths.RuntimeLogPath);
            DeleteLogFile(AppPaths.RuntimeLogPath + ".previous");
            DeleteLogFile(AppPaths.AppDataLogPath);
            DeleteLogFile(AppPaths.AppDataLogPath + ".previous");
            File.WriteAllText(markerPath, versionHash, Utf8NoBom);
            LastThrottledWriteUtc.Clear();
        }
        catch
        {
            // Best effort.
        }
    }

    private static void DeleteLogFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort.
        }
    }
}
