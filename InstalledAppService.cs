using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace QuickZoom;

internal static class InstalledAppService
{
    private static readonly string InstallRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QuickZoom");

    private static readonly string VersionsRoot = Path.Combine(InstallRoot, "versions");
    private static readonly string CurrentInstallPointerPath = Path.Combine(InstallRoot, "current.txt");

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
        string managedRoot = EnsureTrailingSeparator(Path.GetFullPath(VersionsRoot));
        return fullPath.StartsWith(managedRoot, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool ShouldOfferInstallOrUpdate(string? currentExePath)
    {
        if (string.IsNullOrWhiteSpace(currentExePath) || IsManagedInstallPath(currentExePath))
        {
            return false;
        }

        string? installedExePath = GetCurrentInstalledExecutablePath();
        if (string.IsNullOrWhiteSpace(installedExePath) || !File.Exists(installedExePath))
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
            if (File.Exists(CurrentInstallPointerPath))
            {
                string? pointer = File.ReadAllText(CurrentInstallPointerPath).Trim();
                if (!string.IsNullOrWhiteSpace(pointer) && File.Exists(pointer))
                {
                    return Path.GetFullPath(pointer);
                }
            }

            if (!Directory.Exists(VersionsRoot))
            {
                return null;
            }

            DateTime newestWrite = DateTime.MinValue;
            string? newestExe = null;
            foreach (string candidate in Directory.GetFiles(VersionsRoot, "QuickZoom.exe", SearchOption.AllDirectories))
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
        catch
        {
            return null;
        }
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

            string payloadId = GetPayloadId(sourceExePath);
            string targetDirectory = Path.Combine(VersionsRoot, payloadId);
            Directory.CreateDirectory(targetDirectory);

            foreach (string sourceFile in EnumeratePayloadFiles(sourceExePath, sourceDirectory))
            {
                string destinationFile = Path.Combine(targetDirectory, Path.GetFileName(sourceFile));
                if (string.Equals(Path.GetFullPath(sourceFile), Path.GetFullPath(destinationFile), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                File.Copy(sourceFile, destinationFile, true);
            }

            installedExePath = Path.Combine(targetDirectory, Path.GetFileName(sourceExePath));
            Directory.CreateDirectory(InstallRoot);
            File.WriteAllText(CurrentInstallPointerPath, installedExePath);
            return File.Exists(installedExePath);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static IEnumerable<string> EnumeratePayloadFiles(string sourceExePath, string sourceDirectory)
    {
        yield return sourceExePath;

        foreach (string fileName in OptionalPayloadFileNames)
        {
            string candidate = Path.Combine(sourceDirectory, fileName);
            if (File.Exists(candidate) && !string.Equals(candidate, sourceExePath, StringComparison.OrdinalIgnoreCase))
            {
                yield return candidate;
            }
        }

        string baseName = Path.GetFileNameWithoutExtension(sourceExePath);
        foreach (string extension in new[] { ".json", ".runtimeconfig.json", ".deps.json" })
        {
            string candidate = Path.Combine(sourceDirectory, baseName + extension);
            if (File.Exists(candidate))
            {
                yield return candidate;
            }
        }
    }

    private static string GetPayloadId(string exePath)
    {
        using var stream = File.OpenRead(exePath);
        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash.AsSpan(0, 8));
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;
    }
}
