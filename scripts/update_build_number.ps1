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
$releaseVersion = [regex]::Match($content, 'internal const string ReleaseVersion = "([^"]+)";').Groups[1].Value
if ([string]::IsNullOrWhiteSpace($releaseVersion)) {
    throw 'Could not find ReleaseVersion constant.'
}

if ($content -notmatch 'internal const int BuildNumber = \d+;' -or
    $content -notmatch 'internal const string ProductVersion = "[^"]+";') {
    throw 'Could not find version constants to update.'
}

$updated = [regex]::Replace(
    $content,
    'internal const int BuildNumber = \d+;',
    "internal const int BuildNumber = $BuildNumber;")
$updated = [regex]::Replace(
    $updated,
    'internal const string ProductVersion = "[^"]+";',
    "internal const string ProductVersion = `"$releaseVersion.$BuildNumber`";")

Set-Content -LiteralPath $appInfoPath -Value $updated -NoNewline

if (Test-Path -LiteralPath $projectPath) {
    $project = Get-Content -LiteralPath $projectPath -Raw
    $project = [regex]::Replace($project, '<Version>[^<]+</Version>', "<Version>$releaseVersion</Version>")
    $project = [regex]::Replace($project, '<FileVersion>[^<]+</FileVersion>', "<FileVersion>$releaseVersion.$BuildNumber</FileVersion>")
    $project = [regex]::Replace($project, '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$releaseVersion.0</AssemblyVersion>")
    Set-Content -LiteralPath $projectPath -Value $project -NoNewline
}
