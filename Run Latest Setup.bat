@echo off
setlocal EnableExtensions

set "QUICKZOOM_ROOT=%~dp0"

powershell.exe -NoProfile -Command "$root=$env:QUICKZOOM_ROOT; $paths=@((Join-Path $root 'bin\Release\net10.0-windows\QuickZoom.exe'),(Join-Path $root '.codex-temp\setup-validation\QuickZoom.exe')); $builds=Join-Path $root 'Builds'; if(Test-Path -LiteralPath $builds){$paths += Get-ChildItem -LiteralPath $builds -Directory -Filter 'Build *' -ErrorAction SilentlyContinue | ForEach-Object { Join-Path $_.FullName 'QuickZoom.exe' }}; $candidates=$paths | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object { Get-Item -LiteralPath $_ }; $selected=$candidates | Sort-Object @{Expression={ $dll=Join-Path $_.DirectoryName 'QuickZoom.dll'; if(Test-Path -LiteralPath $dll){(Get-Item -LiteralPath $dll).LastWriteTimeUtc}else{$_.LastWriteTimeUtc}}} -Descending | Select-Object -First 1; if($null -eq $selected){Write-Host 'QuickZoom could not be found.'; exit 1}; Write-Host ('Launching ' + $selected.FullName); Start-Process -FilePath $selected.FullName -ArgumentList '-setup' -WorkingDirectory $selected.DirectoryName"

if errorlevel 1 (
    echo Build the Release configuration or create a packaged build first.
    pause
    exit /b 1
)
