[CmdletBinding()]
param(
    [string]$ExePath,
    [string]$TaskName = "QuickZoom Startup (Elevated)",
    [switch]$RunNow
)

$ErrorActionPreference = "Stop"

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Resolve-DefaultExePath {
    param([string]$Root)

    $candidates = @(
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

$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
$currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name

$action = New-ScheduledTaskAction -Execute $resolvedExe
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $currentUser
$principal = New-ScheduledTaskPrincipal -UserId $currentUser -LogonType Interactive -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -MultipleInstances IgnoreNew

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
