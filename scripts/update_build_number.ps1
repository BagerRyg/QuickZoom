param(
    [Parameter(Mandatory = $true)]
    [int]$BuildNumber
)

$root = Split-Path -Parent $PSScriptRoot
$appInfoPath = Join-Path $root 'src\QuickZoom\AppInfo.cs'
$projectPath = Join-Path $root 'QuickZoom.csproj'

if (-not (Test-Path -LiteralPath $appInfoPath)) {
    throw "AppInfo.cs not found at $appInfoPath"
}

$content = Get-Content -LiteralPath $appInfoPath -Raw
$updated = [regex]::Replace(
    $content,
    'internal const int BuildNumber = \d+;',
    "internal const int BuildNumber = $BuildNumber;")
$updated = [regex]::Replace(
    $updated,
    'internal const string ProductVersion = "2\.0\.\d+\.0";',
    "internal const string ProductVersion = `"2.0.$BuildNumber.0`";")

if ($updated -eq $content) {
    throw 'Could not find BuildNumber constant to update.'
}

Set-Content -LiteralPath $appInfoPath -Value $updated -NoNewline

if (Test-Path -LiteralPath $projectPath) {
    $project = Get-Content -LiteralPath $projectPath -Raw
    $project = [regex]::Replace($project, '<Version>2\.0\.\d+</Version>', "<Version>2.0.$BuildNumber</Version>")
    $project = [regex]::Replace($project, '<FileVersion>2\.0\.\d+\.0</FileVersion>', "<FileVersion>2.0.$BuildNumber.0</FileVersion>")
    Set-Content -LiteralPath $projectPath -Value $project -NoNewline
}
