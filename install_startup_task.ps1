[CmdletBinding()]
param(
    [string]$ExePath,
    [string]$TaskName = "QuickZoom Startup (Elevated)",
    [switch]$RunNow
)

$ErrorActionPreference = "Stop"
$startupTaskPriority = 3
$installRoot = Join-Path $env:LOCALAPPDATA "QuickZoom\\managed-install"
$versionsRoot = Join-Path $installRoot "versions"
$currentInstallPointerPath = Join-Path $installRoot "current.txt"

function Protect-InstallLocation {
    param(
        [string]$Path,
        [switch]$Directory
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $userSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $systemSid = "S-1-5-18"
    $adminsSid = "S-1-5-32-544"

    if ($Directory) {
        $userGrant = "*$userSid:(OI)(CI)(RX)"
        $systemGrant = "*$systemSid:(OI)(CI)(F)"
        $adminsGrant = "*$adminsSid:(OI)(CI)(F)"
    }
    else {
        $userGrant = "*$userSid:(RX)"
        $systemGrant = "*$systemSid:(F)"
        $adminsGrant = "*$adminsSid:(F)"
    }

    & icacls.exe $Path /inheritance:r /grant:r $systemGrant $adminsGrant $userGrant | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not harden permissions on $Path"
    }
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Resolve-DefaultExePath {
    param([string]$Root)

    $candidates = @(
        (Get-ChildItem -LiteralPath $Root -Directory -Filter "Build *" -ErrorAction SilentlyContinue |
            Sort-Object {
                if ($_.Name -match '^Build\s+(\d+)$') { [int]$matches[1] } else { -1 }
            } -Descending |
            ForEach-Object { Join-Path $_.FullName "QuickZoom.exe" } |
            Where-Object { Test-Path -LiteralPath $_ }),
        (Join-Path $Root "dist\self-contained\win-x64\QuickZoom.exe"),
        (Join-Path $Root "publish\win-x64-elevated-hotkey\QuickZoom.exe"),
        (Join-Path $Root "publish\win-x64-theme-auto\QuickZoom.exe"),
        (Join-Path $Root "publish\win-x64\QuickZoom.exe"),
        (Join-Path $Root "bin\Release\net8.0-windows\win-x64\publish\QuickZoom.exe")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    return $null
}

function Get-PayloadId {
    param([string]$Path)

    $sha = [System.Security.Cryptography.SHA256]::Create()
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $hash = $sha.ComputeHash($stream)
        return -join ($hash[0..7] | ForEach-Object { $_.ToString("X2") })
    }
    finally {
        $stream.Dispose()
        $sha.Dispose()
    }
}

function Install-AppPayload {
    param([string]$SourceExe)

    $sourceExe = (Resolve-Path -LiteralPath $SourceExe).Path
    $sourceDir = Split-Path -Parent $sourceExe
    $payloadId = Get-PayloadId -Path $sourceExe
    New-Item -ItemType Directory -Force -Path $installRoot | Out-Null
    New-Item -ItemType Directory -Force -Path $versionsRoot | Out-Null
    Protect-InstallLocation -Path $installRoot -Directory
    Protect-InstallLocation -Path $versionsRoot -Directory
    $targetDir = Join-Path $versionsRoot $payloadId
    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
    Protect-InstallLocation -Path $targetDir -Directory

    $files = [System.Collections.Generic.List[string]]::new()
    $files.Add($sourceExe)

    foreach ($name in @(
        "D3DCompiler_47_cor3.dll",
        "PenImc_cor3.dll",
        "PresentationNative_cor3.dll",
        "vcruntime140_cor3.dll",
        "wpfgfx_cor3.dll",
        "QuickZoom.pdb"
    )) {
        $candidate = Join-Path $sourceDir $name
        if (Test-Path -LiteralPath $candidate) {
            $files.Add((Resolve-Path -LiteralPath $candidate).Path)
        }
    }

    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($sourceExe)
    foreach ($suffix in @(".json", ".runtimeconfig.json", ".deps.json")) {
        $candidate = Join-Path $sourceDir ($baseName + $suffix)
        if (Test-Path -LiteralPath $candidate) {
            $files.Add((Resolve-Path -LiteralPath $candidate).Path)
        }
    }

    foreach ($file in $files | Select-Object -Unique) {
        $destination = Join-Path $targetDir (Split-Path -Leaf $file)
        if ([string]::Equals($file, $destination, [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        Copy-Item -LiteralPath $file -Destination $destination -Force
    }

    $installedExe = Join-Path $targetDir (Split-Path -Leaf $sourceExe)
    Set-Content -LiteralPath $currentInstallPointerPath -Value $installedExe -NoNewline
    Protect-InstallLocation -Path $currentInstallPointerPath
    return $installedExe
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not (Test-IsAdministrator)) {
    throw "Run this script from an elevated PowerShell window (Run as Administrator)."
}

if ([string]::IsNullOrWhiteSpace($ExePath)) {
    $ExePath = Resolve-DefaultExePath -Root $root
}

if ([string]::IsNullOrWhiteSpace($ExePath)) {
    throw "Could not locate QuickZoom.exe automatically. Provide -ExePath."
}

if (-not (Test-Path -LiteralPath $ExePath)) {
    throw "QuickZoom.exe not found: $ExePath"
}

$resolvedExe = Install-AppPayload -SourceExe $ExePath
$currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name

$action = New-ScheduledTaskAction -Execute $resolvedExe
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $currentUser -RandomDelay (New-TimeSpan -Seconds 0)
$principal = New-ScheduledTaskPrincipal -UserId $currentUser -LogonType Interactive -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -MultipleInstances IgnoreNew `
    -Priority $startupTaskPriority

Register-ScheduledTask `
    -TaskName $TaskName `
    -Description "Launch QuickZoom at user logon with highest privileges." `
    -Action $action `
    -Trigger $trigger `
    -Principal $principal `
    -Settings $settings `
    -Force | Out-Null

Write-Host "Scheduled task created/updated:"
Write-Host "  Name: $TaskName"
Write-Host "  User: $currentUser"
Write-Host "  Exe : $resolvedExe"

if ($RunNow) {
    Start-ScheduledTask -TaskName $TaskName
    Write-Host "QuickZoom was started from the scheduled task."
}
