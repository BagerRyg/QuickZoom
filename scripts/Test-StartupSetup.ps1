param(
    [string]$RepositoryRoot = (Split-Path $PSScriptRoot -Parent),
    [string]$AssemblyPath = 'bin\Release\net10.0-windows\win-x64\QuickZoom.dll',
    [switch]$ValidateWithTaskScheduler
)

$ErrorActionPreference = 'Stop'
$taskRepository = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$taskAssemblyPath = Join-Path $taskRepository $AssemblyPath
$taskProgramSource = [IO.File]::ReadAllText((Join-Path $taskRepository 'src\QuickZoom\Program.cs'))
if ($taskProgramSource.Contains('FirstRunSetup.ShowStartupServiceOnly(')) {
    throw 'Application startup must not enter the autostart-only wizard.'
}
Write-Output 'PASS: application startup cannot enter the Settings-only autostart flow.'

$taskAssembly = [Reflection.Assembly]::LoadFrom($taskAssemblyPath)
$taskProgram = $taskAssembly.GetType('QuickZoom.Program', $true)
$taskEnabledReader = $taskAssembly.GetType('QuickZoom.StartupTaskService', $true).GetMethod(
    'IsEnabled', [Reflection.BindingFlags]'Static,NonPublic')
foreach ($taskEnabledCase in @(
    @{ Value = $null; Expected = $true },
    @{ Value = 'true'; Expected = $true },
    @{ Value = '1'; Expected = $true },
    @{ Value = 'false'; Expected = $false },
    @{ Value = '0'; Expected = $false }
)) {
    $taskElement = if ($null -eq $taskEnabledCase.Value) { $null } else {
        [System.Xml.Linq.XElement]::Parse('<Enabled>' + $taskEnabledCase.Value + '</Enabled>')
    }
    $taskEnabled = $taskEnabledReader.Invoke($null, [object[]]@($taskElement))
    if ($taskEnabled -ne $taskEnabledCase.Expected) { throw 'Incorrect task Enabled default or boolean handling.' }
}
Write-Output 'PASS: omitted Enabled defaults to true; explicitly disabled tasks/triggers remain rejected.'
$taskWriter = $taskProgram.GetMethod('WriteStartupTaskDefinition', [Reflection.BindingFlags]'Static,NonPublic')
if ($null -eq $taskWriter) { throw 'The production task-definition writer was not found.' }
$taskTestDirectory = Join-Path $taskRepository 'test-validation\startup-file-regression'
New-Item -ItemType Directory -Path $taskTestDirectory -Force | Out-Null
$taskXmlPath = Join-Path $taskTestDirectory ([Guid]::NewGuid().ToString('N') + '.xml')
$taskExecutable = Join-Path $taskTestDirectory 'Space & XML escaping\QuickZoom.exe'
$taskUser = [Security.Principal.WindowsIdentity]::GetCurrent().Name

try {
    $null = $taskWriter.Invoke($null, @([string]$taskXmlPath, [string]$taskExecutable, [string]$taskUser))
    $taskReader = [IO.File]::Open($taskXmlPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
    $taskReader.Dispose()
    Write-Output 'PASS: the production writer closes its handle before another reader opens the XML.'

    $taskXml = [IO.File]::ReadAllText($taskXmlPath)
    [xml]$taskDocument = $taskXml
    $taskNamespace = [Xml.XmlNamespaceManager]::new($taskDocument.NameTable)
    $taskNamespace.AddNamespace('t', 'http://schemas.microsoft.com/windows/2004/02/mit/task')
    if ($taskDocument.SelectSingleNode('//t:Exec/t:Command', $taskNamespace).InnerText -ne $taskExecutable -or
        $taskDocument.SelectSingleNode('//t:Exec/t:Arguments', $taskNamespace).InnerText -ne '--quickzoom-elevated') {
        throw 'Task XML changed the executable path or launch arguments.'
    }
    Write-Output 'PASS: spaces and XML characters round-trip without changing the task action.'
    if ($taskDocument.SelectSingleNode('//t:Settings/t:ExecutionTimeLimit', $taskNamespace).InnerText -ne 'PT0S') {
        throw 'The resident magnifier must not inherit the Task Scheduler 72-hour execution limit.'
    }
    Write-Output 'PASS: the startup task has no execution time limit.'

    try {
        $null = $taskWriter.Invoke($null, @([string]$taskXmlPath, [string]$taskExecutable, [string]$taskUser))
        throw 'The task-definition writer overwrote an existing file.'
    } catch {
        if ($_.Exception.GetBaseException() -isnot [IO.IOException]) { throw }
    }
    if ([IO.File]::ReadAllText($taskXmlPath) -ne $taskXml) { throw 'Existing task XML was modified.' }
    Write-Output 'PASS: existing files are never overwritten.'

    if ($ValidateWithTaskScheduler) {
        $taskScheduler = New-Object -ComObject 'Schedule.Service'
        $taskScheduler.Connect()
        $taskFolder = $taskScheduler.GetFolder('\')
        $taskProbeName = 'QuickZoom-ValidateOnly-' + [Guid]::NewGuid().ToString('N')
        # TASK_VALIDATE_ONLY = 1: Windows checks syntax but never registers a task.
        $null = $taskFolder.RegisterTask($taskProbeName, $taskXml, 1, $taskUser, $null, 3, $null)
        Write-Output 'PASS: Task Scheduler accepted the production XML in validation-only mode; no task created.'
    }
} finally {
    if (Test-Path -LiteralPath $taskXmlPath) { Remove-Item -LiteralPath $taskXmlPath }
}
