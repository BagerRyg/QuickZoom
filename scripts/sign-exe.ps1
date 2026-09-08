param(
  [Parameter(Mandatory=$true)][string]$ExePath,
  [string]$CertificateThumbprint = $env:SIGN_CERT_THUMBPRINT,
  [string]$TimestampUrl = $env:SIGN_TIMESTAMP_URL,
  [switch]$NoTimestamp
)

# __QQ_WRAP__
try {
  $ErrorActionPreference = "Stop"

  function Find-Signtool {
    $candidates = @()

    # Common Windows SDK locations (x64)
    $roots = @(
      "${env:ProgramFiles(x86)}\Windows Kits\10\bin",
      "${env:ProgramFiles(x86)}\Windows Kits\11\bin",
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

  if (-not (Test-Path -LiteralPath $ExePath -PathType Leaf)) { throw "Exe not found: $ExePath" }
  $ExePath = (Resolve-Path -LiteralPath $ExePath).Path
  if ($CertificateThumbprint -notmatch '^[0-9a-fA-F]{40}$') {
    throw "Set SIGN_CERT_THUMBPRINT to a 40-character certificate thumbprint from CurrentUser\My."
  }
  $certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$CertificateThumbprint"
  if (-not $certificate.HasPrivateKey) { throw "The signing certificate has no private key." }

  $signtool = Find-Signtool
  if (-not $signtool) {
    throw "signtool.exe not found. Install the Windows 10/11 SDK (Signing Tools) or add signtool to PATH."
  }

  Write-Host "Using signtool: $signtool"
  Write-Host "Signing: $ExePath"

  $signArguments = @('sign', '/fd', 'sha256', '/s', 'My', '/sha1', $CertificateThumbprint)

  if (-not $NoTimestamp -and -not [string]::IsNullOrWhiteSpace($TimestampUrl)) {
    # Explicit opt-in: the build signing tool contacts this RFC3161 server.
    # This is build-time traffic, not application runtime traffic.
    $timestampUri = [Uri]$TimestampUrl
    if (-not $timestampUri.IsAbsoluteUri -or $timestampUri.Scheme -notin @('http', 'https')) {
      throw "TimestampUrl must be an absolute HTTP or HTTPS URL."
    }
    $signArguments += @('/tr', $TimestampUrl, '/td', 'sha256')
  }

  $signArguments += @($ExePath)

  & $signtool @signArguments
  if ($LASTEXITCODE -ne 0) {
    throw "signtool failed with exit code $LASTEXITCODE"
  }

  & $signtool verify /pa $ExePath
  if ($LASTEXITCODE -ne 0) {
    throw "Signature verification failed with exit code $LASTEXITCODE. The certificate must be trusted on this build machine."
  }
  Write-Host "SUCCESS: Signed and verified." -ForegroundColor Green
  exit 0

} catch {
  Write-Host "`nERROR: $($_.Exception.Message)" -ForegroundColor Red
  exit 1
}
