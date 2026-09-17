using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace QuickZoom;

internal static class LocalStorage
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, bool inheritHandle, uint processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(SafeProcessHandle process, uint access,
        out SafeAccessTokenHandle token);

    internal static void RequireLocalPath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string root = Path.GetPathRoot(fullPath) ?? throw new IOException("A local path is required.");
        if (fullPath.StartsWith(@"\\", StringComparison.Ordinal) ||
            new DriveInfo(root).DriveType != DriveType.Fixed)
            throw new IOException("QuickZoom requires a fixed local drive.");

        string current = root;
        foreach (string component in fullPath[root.Length..].Split(Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, component);
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("QuickZoom does not use redirected storage paths.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    // The elevated magnifier must never lend its administrator token to profile writes.
    internal static void RunAsUser(Action action)
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
        {
            action();
            return;
        }

        // A UAC linked token can be identification-only: querying its user works,
        // but file access fails with ERROR_BAD_IMPERSONATION_LEVEL. Use the desktop
        // shell's primary token, with only the access required for impersonation.
        IntPtr shellWindow = GetShellWindow();
        if (shellWindow == IntPtr.Zero ||
            GetWindowThreadProcessId(shellWindow, out uint shellProcessId) == 0)
            throw new UnauthorizedAccessException("Profile writes require the user's desktop shell.");

        const uint ProcessQueryLimitedInformation = 0x1000;
        const uint TokenQueryAndDuplicate = 0x0008 | 0x0002;
        using SafeProcessHandle shellProcess = OpenProcess(ProcessQueryLimitedInformation, false, shellProcessId);
        if (shellProcess.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "The desktop shell could not be opened.");
        if (!OpenProcessToken(shellProcess, TokenQueryAndDuplicate, out SafeAccessTokenHandle token))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "The standard user token could not be opened.");

        using (token)
        {
            WindowsIdentity.RunImpersonated(token, () =>
            {
                using WindowsIdentity filtered = WindowsIdentity.GetCurrent();
                if (identity.User == null || filtered.User != identity.User ||
                    new WindowsPrincipal(filtered).IsInRole(WindowsBuiltInRole.Administrator))
                    throw new UnauthorizedAccessException("Profile writes require the same user's standard token.");
                action();
            });
        }
    }
}
