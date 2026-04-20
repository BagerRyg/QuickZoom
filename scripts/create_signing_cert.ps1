<#
Creates a self-signed *code signing* certificate and exports it to a PFX.

Usage (PowerShell):
  .\create_signing_cert.ps1

Optional:
  .\create_signing_cert.ps1 -Subject "CN=QuickZoom (Jonas)" -OutDir ".\signing" -InstallTrust

Outputs:
  - <OutDir>\QuickZoom_Signing.pfx
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

$pfxPath = Join-Path $OutDir "QuickZoom_Signing.pfx"
$cerPath = Join-Path $OutDir "QuickZoom_Signing.cer"

Write-Host "Creating self-signed code signing certificate: $Subject" -ForegroundColor Cyan

$cert = New-SelfSignedCertificate \
  -Type CodeSigningCert \
  -Subject $Subject \
  -KeyAlgorithm RSA \
  -KeyLength 2048 \
  -HashAlgorithm SHA256 \
  -KeyExportPolicy Exportable \
  -KeySpec Signature \
  -CertStoreLocation "Cert:\CurrentUser\My" \
  -NotAfter (Get-Date).AddYears(5)

Write-Host "Created certificate thumbprint: $($cert.Thumbprint)" -ForegroundColor Green

$pwd = Read-Host -Prompt "Enter a password to protect the PFX" -AsSecureString

Export-PfxCertificate -Cert "Cert:\CurrentUser\My\$($cert.Thumbprint)" -FilePath $pfxPath -Password $pwd | Out-Null
Export-Certificate -Cert "Cert:\CurrentUser\My\$($cert.Thumbprint)" -FilePath $cerPath | Out-Null

Write-Host "Exported:" -ForegroundColor Cyan
Write-Host "  PFX: $pfxPath"
Write-Host "  CER: $cerPath"

if ($InstallTrust.IsPresent) {
  Write-Host "\nInstalling certificate into CurrentUser Trusted Root + Trusted Publishers..." -ForegroundColor Yellow
  Import-Certificate -FilePath $cerPath -CertStoreLocation "Cert:\CurrentUser\Root" | Out-Null
  Import-Certificate -FilePath $cerPath -CertStoreLocation "Cert:\CurrentUser\TrustedPublisher" | Out-Null
  Write-Host "Trust installed for CURRENT USER on THIS PC." -ForegroundColor Green
}

Write-Host "\nNext step:" -ForegroundColor Cyan
Write-Host "  set SIGN_PFX=\"$pfxPath\"" 
Write-Host "  set SIGN_PWD=<your password>" 
Write-Host "  build.bat" 
