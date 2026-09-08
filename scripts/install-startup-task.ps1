[CmdletBinding()]
param(
    [string]$ExePath,
    [string]$TaskName = "QuickZoom Startup (Elevated)",
    [string]$TargetUser = [Security.Principal.WindowsIdentity]::GetCurrent().Name,
    [switch]$RunNow
)

$ErrorActionPreference = "Stop"
if ($TaskName -ne "QuickZoom Startup (Elevated)") {
    throw "QuickZoom supports only its verified startup task name."
}
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script from an elevated PowerShell window (Run as Administrator)."
}
if ([string]::IsNullOrWhiteSpace($ExePath)) {
    $root = Split-Path -Parent $PSScriptRoot
    $ExePath = Get-ChildItem -LiteralPath (Join-Path $root 'Builds') -Directory -Filter 'Build *' -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^Build\s+(\d+)$' } |
        Sort-Object { [int]($_.Name -replace '^Build\s+', '') } -Descending |
        ForEach-Object { Join-Path $_.FullName 'QuickZoom.exe' } |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($ExePath)) {
    throw "Provide -ExePath pointing to the reviewed QuickZoom build."
}
$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
if ($resolvedExe.StartsWith('\\') -or [IO.Path]::GetFileName($resolvedExe) -ne 'QuickZoom.exe') {
    throw "A local QuickZoom.exe is required."
}
if ([string]::IsNullOrWhiteSpace($TargetUser) -or $TargetUser.Contains('"')) {
    throw "A valid target Windows user is required."
}
# The application owns payload ACLs, task XML and readback verification.
$process = Start-Process -FilePath $resolvedExe -ArgumentList @('--setup-install-startup-task', '--startup-task-user', ('"' + $TargetUser + '"')) -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) {
    throw "QuickZoom could not install and verify its protected startup task."
}
Write-Host "QuickZoom startup task installed and verified for $TargetUser."
if ($RunNow) {
    & (Join-Path ([Environment]::SystemDirectory) 'schtasks.exe') /Run /TN $TaskName
    if ($LASTEXITCODE -ne 0) { throw "Could not start the verified startup task." }
}
