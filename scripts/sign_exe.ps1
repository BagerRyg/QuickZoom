param(
  [Parameter(Mandatory=$true)][string]$ExePath,
  [Parameter(Mandatory=$true)][string]$PfxPath,
  [string]$PfxPassword = "",
  [string]$TimestampUrl = "http://timestamp.digicert.com",
  [switch]$NoTimestamp
)

# __QQ_WRAP__
try {
  $ErrorActionPreference = "Stop"

  $ErrorActionPreference = "Stop"

  function Find-Signtool {
    $candidates = @()

    # Common Windows SDK locations (x64)
    $roots = @(
      "$env:ProgramFiles(x86)\Windows Kits\10\bin",
      "$env:ProgramFiles(x86)\Windows Kits\11\bin",
      "$env:ProgramFiles\Windows Kits\10\bin",
      "$env:ProgramFiles\Windows Kits\11\bin"
    ) | Where-Object { $_ -and (Test-Path $_) }

    foreach ($root in $roots) {
      # Pick newest version folder if present
      $versionDirs = Get-ChildItem -Path $root -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^\d+\.' } |
        Sort-Object Name -Descending

      foreach ($vd in $versionDirs) {
        $p1 = Join-Path $vd.FullName 'x64\signtool.exe'
        $p2 = Join-Path $vd.FullName 'x86\signtool.exe'
        if (Test-Path $p1) { $candidates += $p1 }
        if (Test-Path $p2) { $candidates += $p2 }
      }

      # Also check for "...\bin\x64\signtool.exe" directly
      $direct1 = Join-Path $root 'x64\signtool.exe'
      $direct2 = Join-Path $root 'x86\signtool.exe'
      if (Test-Path $direct1) { $candidates += $direct1 }
      if (Test-Path $direct2) { $candidates += $direct2 }
    }

    # PATH fallback
    $fromPath = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($fromPath) { $candidates = @($fromPath.Source) + $candidates }

    $candidates | Select-Object -Unique | Select-Object -First 1
  }

  if (-not (Test-Path $ExePath)) { throw "Exe not found: $ExePath" }
  if (-not (Test-Path $PfxPath)) { throw "PFX not found: $PfxPath" }

  $signtool = Find-Signtool
  if (-not $signtool) {
    throw "signtool.exe not found. Install the Windows 10/11 SDK (Signing Tools) or add signtool to PATH."
  }

  Write-Host "Using signtool: $signtool"
  Write-Host "Signing: $ExePath"

  $args = @('sign', '/fd', 'sha256', '/f', $PfxPath)
  if ($PfxPassword -ne "") {
    $args += @('/p', $PfxPassword)
  }

  if (-not $NoTimestamp) {
    # RFC3161 timestamp
    $args += @('/tr', $TimestampUrl, '/td', 'sha256')
  }

  $args += @($ExePath)

  & $signtool @args
  if ($LASTEXITCODE -ne 0) {
    throw "signtool failed with exit code $LASTEXITCODE"
  }

  Write-Host "SUCCESS: Signed." -ForegroundColor Green

} catch {
  Write-Host "`nERROR: $($_.Exception.Message)" -ForegroundColor Red
  if ($_.ScriptStackTrace) { Write-Host $_.ScriptStackTrace -ForegroundColor DarkGray }
  Write-Host "`nFull error:" -ForegroundColor Yellow
  $_ | Format-List * -Force
} finally {
  Write-Host "`nPress Enter to close..." -ForegroundColor Cyan
  try { Read-Host | Out-Null } catch {}
}
