using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickZoom;

internal static class Program
{
    // Per-monitor v2 gives physical pixel coordinates across mixed-DPI setups.
    private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new(-4);

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiFlag);

    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    private static void EnablePerMonitorDpiAwareness()
    {
        try
        {
            if (!SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
            {
                SetProcessDPIAware();
            }
        }
        catch
        {
            // Best effort.
        }
    }

    private const int ERROR_CANCELLED = 1223;
    private const string ElevatedFlag = "--quickzoom-elevated";
    private const string ElevatedStartupTaskName = "QuickZoom Startup (Elevated)";

    [STAThread]
    private static void Main(string[] args)
    {
        if (!IsRunningAsAdministrator() && !HasArg(args, ElevatedFlag))
        {
            if (TryStartElevatedScheduledTask())
            {
                return;
            }

            if (TryRelaunchAsAdministrator(args))
            {
                return;
            }

            MessageBox.Show(
                "QuickZoom was not elevated. Zoom hotkeys may not work while an Administrator app is focused.\n\n" +
                "Run QuickZoom as Administrator to use zoom across elevated apps.",
                "QuickZoom",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        EnablePerMonitorDpiAwareness();
        try { Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); } catch { }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayContext());
    }

    private static bool IsRunningAsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryRelaunchAsAdministrator(string[] args)
    {
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            exePath = Application.ExecutablePath;
        }

        if (string.IsNullOrWhiteSpace(exePath))
        {
            return false;
        }

        string elevatedArgs = BuildElevatedArguments(args);
        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true,
            Verb = "runas",
            Arguments = elevatedArgs
        };

        try
        {
            Process.Start(startInfo);
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ERROR_CANCELLED)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryStartElevatedScheduledTask()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = "/Run /TN \"" + ElevatedStartupTaskName + "\"",
            CreateNoWindow = true,
            UseShellExecute = false
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            process.WaitForExit(1500);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildElevatedArguments(string[] args)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], ElevatedFlag, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (sb.Length > 0)
            {
                sb.Append(' ');
            }

            sb.Append(QuoteArgument(args[i]));
        }

        if (sb.Length > 0)
        {
            sb.Append(' ');
        }

        sb.Append(ElevatedFlag);
        return sb.ToString();
    }

    private static bool HasArg(string[] args, string value)
    {
        foreach (string arg in args)
        {
            if (string.Equals(arg, value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string QuoteArgument(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        if (!value.Contains(' ') && !value.Contains('"'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
