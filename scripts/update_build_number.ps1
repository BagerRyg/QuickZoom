param(
    [Parameter(Mandatory = $true)]
    [int]$BuildNumber
)

$root = Split-Path -Parent $PSScriptRoot
$appInfoPath = Join-Path $root 'AppInfo.cs'

if (-not (Test-Path -LiteralPath $appInfoPath)) {
    throw "AppInfo.cs not found at $appInfoPath"
}

$content = Get-Content -LiteralPath $appInfoPath -Raw
$updated = [regex]::Replace(
    $content,
    'internal const int BuildNumber = \d+;',
    "internal const int BuildNumber = $BuildNumber;")

if ($updated -eq $content) {
    throw 'Could not find BuildNumber constant to update.'
}

Set-Content -LiteralPath $appInfoPath -Value $updated -NoNewline
