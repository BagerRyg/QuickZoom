using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace QuickZoom;

internal static class InstalledAppService
{
    private static readonly string StateRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QuickZoom");

    private static readonly string InstallRoot = Path.Combine(StateRoot, "managed-install");
    private static readonly string VersionsRoot = Path.Combine(InstallRoot, "versions");
    private static readonly string CurrentInstallPointerPath = Path.Combine(InstallRoot, "current.txt");
    private static readonly string LegacyVersionsRoot = Path.Combine(StateRoot, "versions");
    private static readonly string LegacyCurrentInstallPointerPath = Path.Combine(StateRoot, "current.txt");
    private const string LocalesFolderName = "locales";
    private const int PreviousManagedVersionRetentionCount = 1;

    private static readonly string[] OptionalPayloadFileNames =
    [
        "D3DCompiler_47_cor3.dll",
        "PenImc_cor3.dll",
        "PresentationNative_cor3.dll",
        "vcruntime140_cor3.dll",
        "wpfgfx_cor3.dll",
        "QuickZoom.pdb"
    ];

    internal static bool IsManagedInstallPath(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return false;
        }

        string fullPath = Path.GetFullPath(exePath);
        return IsUnderRoot(fullPath, VersionsRoot) || IsUnderRoot(fullPath, LegacyVersionsRoot);
    }

    internal static bool NeedsSecureInstallMigration(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return false;
        }

        string fullPath = Path.GetFullPath(exePath);
        return IsUnderRoot(fullPath, LegacyVersionsRoot) && !IsUnderRoot(fullPath, VersionsRoot);
    }

    internal static bool ShouldOfferInstallOrUpdate(string? currentExePath)
    {
        if (string.IsNullOrWhiteSpace(currentExePath))
        {
            return false;
        }

        if (IsManagedInstallPath(currentExePath))
        {
            return NeedsSecureInstallMigration(currentExePath);
        }

        string? installedExePath = GetCurrentInstalledExecutablePath();
        if (string.IsNullOrWhiteSpace(installedExePath) || !File.Exists(installedExePath))
        {
            return true;
        }

        if (NeedsSecureInstallMigration(installedExePath))
        {
            return true;
        }

        return !string.Equals(
            GetPayloadId(currentExePath),
            GetPayloadId(installedExePath),
            StringComparison.OrdinalIgnoreCase);
    }

    internal static string? GetCurrentInstalledExecutablePath()
    {
        try
        {
            string? currentPointerTarget = ReadInstalledExecutablePointer(CurrentInstallPointerPath, VersionsRoot);
            if (!string.IsNullOrWhiteSpace(currentPointerTarget))
            {
                return currentPointerTarget;
            }

            string? legacyPointerTarget = ReadInstalledExecutablePointer(LegacyCurrentInstallPointerPath, LegacyVersionsRoot);
            if (!string.IsNullOrWhiteSpace(legacyPointerTarget))
            {
                return legacyPointerTarget;
            }

            string? managedInstall = FindNewestExecutableUnder(VersionsRoot);
            if (!string.IsNullOrWhiteSpace(managedInstall))
            {
                return managedInstall;
            }

            return FindNewestExecutableUnder(LegacyVersionsRoot);
        }
        catch
        {
            return null;
        }
    }

    internal static bool IsCurrentInstalledExecutablePath(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return false;
        }

        string? installedExePath = GetCurrentInstalledExecutablePath();
        if (string.IsNullOrWhiteSpace(installedExePath))
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(exePath),
            Path.GetFullPath(installedExePath),
            StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TryPrepareInstalledPayload(string sourceExePath, out string installedExePath, out string? errorMessage)
    {
        installedExePath = sourceExePath;
        errorMessage = null;

        try
        {
            sourceExePath = Path.GetFullPath(sourceExePath);
            string sourceDirectory = Path.GetDirectoryName(sourceExePath)
                ?? throw new InvalidOperationException("Could not determine the source directory.");

            Directory.CreateDirectory(InstallRoot);
            Directory.CreateDirectory(VersionsRoot);
            HardenInstallDirectory(InstallRoot);
            HardenInstallDirectory(VersionsRoot);

            string payloadId = GetPayloadId(sourceExePath);
            string targetDirectory = Path.Combine(VersionsRoot, payloadId);
            if (!IsUnderRoot(Path.GetFullPath(targetDirectory), VersionsRoot))
            {
                throw new InvalidOperationException("The managed install target resolved outside the QuickZoom install root.");
            }

            Directory.CreateDirectory(targetDirectory);
            HardenInstallDirectory(targetDirectory);

            foreach ((string sourcePath, string relativePath) in EnumeratePayloadFiles(sourceExePath, sourceDirectory))
            {
                string destinationFile = Path.Combine(targetDirectory, NormalizeRelativePath(relativePath));
                if (!IsUnderRoot(Path.GetFullPath(destinationFile), targetDirectory))
                {
                    throw new InvalidOperationException("A payload file resolved outside the managed install target.");
                }

                string? destinationDirectory = Path.GetDirectoryName(destinationFile);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                    HardenInstallDirectory(destinationDirectory);
                }

                if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationFile), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                File.Copy(sourcePath, destinationFile, true);
            }

            installedExePath = Path.Combine(targetDirectory, Path.GetFileName(sourceExePath));
            FilePersistence.WriteAllTextAtomic(CurrentInstallPointerPath, installedExePath);
            HardenInstallDirectory(targetDirectory);
            HardenInstallFile(CurrentInstallPointerPath);
            CleanupOldManagedVersions(targetDirectory);
            return File.Exists(installedExePath);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            ErrorLog.Write("InstalledAppService", ex);
            return false;
        }
    }

    private static string? ReadInstalledExecutablePointer(string pointerPath, string expectedRoot)
    {
        if (!File.Exists(pointerPath))
        {
            return null;
        }

        string? pointer = File.ReadAllText(pointerPath).Trim();
        if (string.IsNullOrWhiteSpace(pointer))
        {
            return null;
        }

        string fullPointer = Path.GetFullPath(pointer);
        if (IsUnderRoot(fullPointer, expectedRoot) && LooksLikeQuickZoomExecutable(fullPointer) && File.Exists(fullPointer))
        {
            return fullPointer;
        }

        return null;
    }

    private static string? FindNewestExecutableUnder(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            return null;
        }

        DateTime newestWrite = DateTime.MinValue;
        string? newestExe = null;
        foreach (string candidate in Directory.GetFiles(rootPath, "QuickZoom.exe", SearchOption.AllDirectories))
        {
            DateTime writeTime = File.GetLastWriteTimeUtc(candidate);
            if (writeTime > newestWrite)
            {
                newestWrite = writeTime;
                newestExe = candidate;
            }
        }

        return newestExe;
    }

    private static IEnumerable<(string SourcePath, string RelativePath)> EnumeratePayloadFiles(string sourceExePath, string sourceDirectory)
    {
        yield return (sourceExePath, Path.GetFileName(sourceExePath));

        foreach (string fileName in OptionalPayloadFileNames)
        {
            string candidate = Path.Combine(sourceDirectory, fileName);
            if (File.Exists(candidate) && !string.Equals(candidate, sourceExePath, StringComparison.OrdinalIgnoreCase))
            {
                yield return (candidate, fileName);
            }
        }

        string baseName = Path.GetFileNameWithoutExtension(sourceExePath);
        foreach (string extension in new[] { ".json", ".runtimeconfig.json", ".deps.json" })
        {
            string candidate = Path.Combine(sourceDirectory, baseName + extension);
            if (File.Exists(candidate))
            {
                yield return (candidate, Path.GetFileName(candidate));
            }
        }

        string localesDirectory = Path.Combine(sourceDirectory, LocalesFolderName);
        if (!Directory.Exists(localesDirectory))
        {
            yield break;
        }

        foreach (string localeFile in Directory.GetFiles(localesDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            yield return (localeFile, Path.Combine(LocalesFolderName, Path.GetFileName(localeFile)));
        }
    }

    private static string GetPayloadId(string exePath)
    {
        exePath = Path.GetFullPath(exePath);
        string sourceDirectory = Path.GetDirectoryName(exePath)
            ?? throw new InvalidOperationException("Could not determine the source directory.");

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach ((string sourcePath, string relativePath) in EnumeratePayloadFiles(exePath, sourceDirectory))
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(NormalizeRelativePath(relativePath));
            hash.AppendData(nameBytes);
            hash.AppendData([0]);

            using FileStream stream = File.OpenRead(sourcePath);
            byte[] buffer = new byte[81920];
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                hash.AppendData(buffer, 0, bytesRead);
            }
        }

        byte[] digest = hash.GetHashAndReset();
        return Convert.ToHexString(digest.AsSpan(0, 8));
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    private static void HardenInstallDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        DirectoryInfo directoryInfo = new(path);
        directoryInfo.SetAccessControl(CreateInstallDirectorySecurity());
    }

    private static void HardenInstallFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        FileInfo fileInfo = new(path);
        fileInfo.SetAccessControl(CreateInstallFileSecurity());
    }

    private static void CleanupOldManagedVersions(string currentVersionDirectory)
    {
        try
        {
            if (!Directory.Exists(VersionsRoot))
            {
                return;
            }

            string currentFullPath = Path.GetFullPath(currentVersionDirectory);
            string? pointerExePath = ReadInstalledExecutablePointer(CurrentInstallPointerPath, VersionsRoot);
            string? pointerDirectory = string.IsNullOrWhiteSpace(pointerExePath)
                ? null
                : Path.GetDirectoryName(pointerExePath);

            var removableVersions = new List<DirectoryInfo>();
            foreach (DirectoryInfo directory in new DirectoryInfo(VersionsRoot).EnumerateDirectories())
            {
                string directoryPath = Path.GetFullPath(directory.FullName);
                if (PathsEqual(directoryPath, currentFullPath) || PathsEqual(directoryPath, pointerDirectory))
                {
                    continue;
                }

                removableVersions.Add(directory);
            }

            foreach (DirectoryInfo oldVersion in removableVersions
                .OrderByDescending(directory => directory.LastWriteTimeUtc)
                .Skip(PreviousManagedVersionRetentionCount))
            {
                TryDeleteManagedVersionDirectory(oldVersion);
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write("InstalledAppService.Cleanup", ex);
        }
    }

    private static void TryDeleteManagedVersionDirectory(DirectoryInfo directory)
    {
        try
        {
            if (!IsUnderRoot(Path.GetFullPath(directory.FullName), VersionsRoot))
            {
                return;
            }

            directory.Delete(recursive: true);
            ErrorLog.Write("InstalledAppService.Cleanup", "Removed old managed install version: " + directory.FullName);
        }
        catch (Exception ex)
        {
            ErrorLog.Write("InstalledAppService.Cleanup", "Could not remove old managed install version '" + directory.FullName + "'. " + ex.Message);
        }
    }

    private static DirectorySecurity CreateInstallDirectorySecurity()
    {
        SecurityIdentifier userSid = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("Could not determine the current Windows user.");
        SecurityIdentifier adminsSid = new(WellKnownSidType.BuiltinAdministratorsSid, null);
        SecurityIdentifier systemSid = new(WellKnownSidType.LocalSystemSid, null);

        DirectorySecurity security = new();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(systemSid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(adminsSid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(userSid, FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        return security;
    }

    private static FileSecurity CreateInstallFileSecurity()
    {
        SecurityIdentifier userSid = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("Could not determine the current Windows user.");
        SecurityIdentifier adminsSid = new(WellKnownSidType.BuiltinAdministratorsSid, null);
        SecurityIdentifier systemSid = new(WellKnownSidType.LocalSystemSid, null);

        FileSecurity security = new();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(systemSid, FileSystemRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(adminsSid, FileSystemRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(userSid, FileSystemRights.ReadAndExecute, AccessControlType.Allow));
        return security;
    }

    private static bool IsUnderRoot(string fullPath, string rootPath)
    {
        string normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(rootPath));
        return fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeQuickZoomExecutable(string path)
    {
        try
        {
            return string.Equals(Path.GetFileName(path), "QuickZoom.exe", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;
    }
}
