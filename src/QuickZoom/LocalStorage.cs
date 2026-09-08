using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace QuickZoom;

internal static class LocalStorage
{
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(IntPtr token, int informationClass,
        out IntPtr information, int length, out int returnedLength);

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

        const int TokenLinkedToken = 19;
        if (!GetTokenInformation(identity.Token, TokenLinkedToken, out IntPtr linked,
            IntPtr.Size, out _) || linked == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Profile writes require a standard user token.");

        using var token = new SafeAccessTokenHandle(linked);
        WindowsIdentity.RunImpersonated(token, () =>
        {
            using WindowsIdentity filtered = WindowsIdentity.GetCurrent();
            if (new WindowsPrincipal(filtered).IsInRole(WindowsBuiltInRole.Administrator))
                throw new UnauthorizedAccessException("Profile writes require a standard user token.");
            action();
        });
    }
}
