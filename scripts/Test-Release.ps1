#Requires -Version 7.6
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path $PSScriptRoot -Parent),
    [switch]$NoRestore,
    [switch]$IncludeNative
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw 'QuickZoom release checks require Windows.' }
$repository = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$dotnet = (Get-Command dotnet -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
$powerShell = [Environment]::ProcessPath

function Invoke-CheckedProcess {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [int]$TimeoutSeconds = 180
    )

    $start = [Diagnostics.ProcessStartInfo]::new($FilePath)
    $start.WorkingDirectory = $repository
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }

    $process = [Diagnostics.Process]::Start($start)
    if ($null -eq $process) { throw "Could not start $FilePath." }
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            $null = $process.WaitForExit(5000)
            throw "$FilePath timed out after $TimeoutSeconds seconds."
        }
        $reads = [Threading.Tasks.Task]::WhenAll([Threading.Tasks.Task[]]@($stdout, $stderr))
        if (-not $reads.Wait(5000)) { throw "$FilePath did not close its output streams." }
        $output = $stdout.GetAwaiter().GetResult()
        $errorOutput = $stderr.GetAwaiter().GetResult()
        if ($output.Length -gt 0) { Write-Host $output.TrimEnd() }
        if ($errorOutput.Length -gt 0) { Write-Host $errorOutput.TrimEnd() }
        if ($process.ExitCode -ne 0) { throw "$FilePath failed with exit code $($process.ExitCode)." }
    } finally {
        $process.Dispose()
    }
}

$projects = @('QuickZoom.csproj', 'tests/RuntimeChecks/RuntimeChecks.csproj',
    'tests/PrivacyChecks/PrivacyChecks.csproj', 'tests/UiChecks/UiChecks.csproj')
foreach ($project in $projects) {
    Write-Host "Building $project..."
    $arguments = @('build', $project, '-c', 'Release', '--nologo')
    if ($NoRestore) { $arguments += '--no-restore' }
    Invoke-CheckedProcess -FilePath $dotnet -Arguments $arguments -TimeoutSeconds 600
}

$testRoot = Join-Path $repository ('test-validation/release-' + [Guid]::NewGuid().ToString('N'))
Write-Host "Validation files: $testRoot"
Invoke-CheckedProcess -FilePath (Join-Path $repository 'tests/RuntimeChecks/bin/Release/net10.0-windows/RuntimeChecks.exe') `
    -Arguments @((Join-Path $testRoot 'runtime'), '--no-render')
Invoke-CheckedProcess -FilePath (Join-Path $repository 'tests/RuntimeChecks/bin/Release/net10.0-windows/RuntimeChecks.exe') `
    -Arguments @((Join-Path $testRoot 'setup-per-monitor'), '--setup-startup', '--per-monitor-dpi')
Invoke-CheckedProcess -FilePath (Join-Path $repository 'tests/PrivacyChecks/bin/Release/net10.0-windows/PrivacyChecks.exe') `
    -Arguments @((Join-Path $testRoot 'privacy'))
Invoke-CheckedProcess -FilePath (Join-Path $repository 'tests/UiChecks/bin/Release/net10.0-windows/UiChecks.exe')

Invoke-CheckedProcess -FilePath $powerShell -Arguments @('-NoProfile', '-NonInteractive', '-File',
    (Join-Path $repository 'scripts/Test-StartupSetup.ps1'), '-RepositoryRoot', $repository,
    '-AssemblyPath', 'bin/Release/net10.0-windows/QuickZoom.dll')
Invoke-CheckedProcess -FilePath $powerShell -Arguments @('-NoProfile', '-NonInteractive', '-File',
    (Join-Path $repository 'scripts/Test-UiLocales.ps1'), '-RepositoryRoot', $repository)

if ($IncludeNative) {
    Invoke-CheckedProcess -FilePath (Join-Path $repository 'tests/RuntimeChecks/bin/Release/net10.0-windows/RuntimeChecks.exe') `
        -Arguments @('--native-only')
}
Write-Host 'PASS: all requested release checks passed without screenshots.'
