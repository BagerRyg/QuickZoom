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

$versionParts = @($releaseVersion.Split('.', [System.StringSplitOptions]::RemoveEmptyEntries))
if ($versionParts.Count -lt 2 -or $versionParts.Count -gt 3 -or
    ($versionParts | Where-Object { $_ -notmatch '^\d+$' }).Count -ne 0) {
    throw "ReleaseVersion must contain two or three numeric parts. Found: $releaseVersion"
}
$metadataVersion = (($versionParts + @('0', '0', '0'))[0..2] -join '.')

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
    "internal const string ProductVersion = `"$metadataVersion.$BuildNumber`";")

Set-Content -LiteralPath $appInfoPath -Value $updated -NoNewline

if (Test-Path -LiteralPath $projectPath) {
    $project = Get-Content -LiteralPath $projectPath -Raw
    $project = [regex]::Replace($project, '<Version>[^<]+</Version>', "<Version>$metadataVersion</Version>")
    $project = [regex]::Replace($project, '<FileVersion>[^<]+</FileVersion>', "<FileVersion>$metadataVersion.$BuildNumber</FileVersion>")
    $project = [regex]::Replace($project, '<InformationalVersion>[^<]+</InformationalVersion>', "<InformationalVersion>$metadataVersion.$BuildNumber</InformationalVersion>")
    $project = [regex]::Replace($project, '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$metadataVersion.0</AssemblyVersion>")
    Set-Content -LiteralPath $projectPath -Value $project -NoNewline
}
