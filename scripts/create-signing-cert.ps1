<#
Creates a self-signed code signing certificate with a nonexportable private key.
The private key stays in the CurrentUser certificate store; only the public certificate is exported.

Usage (PowerShell):
  .\create-signing-cert.ps1

Optional:
  .\create-signing-cert.ps1 -Subject "CN=QuickZoom (Jonas)" -OutDir ".\signing" -InstallTrust

Outputs:
  - <OutDir>\QuickZoom_Signing.cer

Notes:
  - Self-signed certificates are not trusted by default on other machines.
    To avoid "Unknown publisher" on another PC, you must import the .cer into:
      * CurrentUser\TrustedPublisher
      * CurrentUser\Root (Trusted Root)
    (or LocalMachine equivalents, if you manage the PC).
  - SmartScreen reputation warnings can still appear unless you use a publicly
    trusted cert (OV/EV) and build reputation over time.
#>

[CmdletBinding()]
param(
  [string]$Subject = "CN=QuickZoom (Self-Signed)",
  [string]$OutDir = ".",
  [switch]$InstallTrust
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $OutDir)) {
  New-Item -ItemType Directory -Path $OutDir | Out-Null
}

$cerPath = Join-Path $OutDir "QuickZoom_Signing.cer"

Write-Host "Creating self-signed code signing certificate: $Subject" -ForegroundColor Cyan

$certificateParameters = @{
  Type              = "CodeSigningCert"
  Subject           = $Subject
  KeyAlgorithm      = "RSA"
  KeyLength         = 2048
  HashAlgorithm     = "SHA256"
  KeyExportPolicy   = "NonExportable"
  KeySpec           = "Signature"
  CertStoreLocation = "Cert:\CurrentUser\My"
  NotAfter          = (Get-Date).AddYears(5)
}
$cert = New-SelfSignedCertificate @certificateParameters

Write-Host "Created certificate thumbprint: $($cert.Thumbprint)" -ForegroundColor Green

Export-Certificate -Cert "Cert:\CurrentUser\My\$($cert.Thumbprint)" -FilePath $cerPath | Out-Null

Write-Host "Exported:" -ForegroundColor Cyan
Write-Host "  CER: $cerPath"

if ($InstallTrust.IsPresent) {
  Write-Host "\nInstalling certificate into CurrentUser Trusted Root + Trusted Publishers..." -ForegroundColor Yellow
  Import-Certificate -FilePath $cerPath -CertStoreLocation "Cert:\CurrentUser\Root" | Out-Null
  Import-Certificate -FilePath $cerPath -CertStoreLocation "Cert:\CurrentUser\TrustedPublisher" | Out-Null
  Write-Host "Trust installed for CURRENT USER on THIS PC." -ForegroundColor Green
}

Write-Host "\nNext step:" -ForegroundColor Cyan
Write-Host "  set SIGN_CERT_THUMBPRINT=$($cert.Thumbprint)"
Write-Host "  REM Trust the public certificate on the build machine before signature verification."
Write-Host "  REM Optional build-time network timestamp: set SIGN_TIMESTAMP_URL=https://your-approved-timestamp-server"
Write-Host "  build.bat" 
