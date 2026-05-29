@echo off
setlocal
pushd "%~dp0"

set "BUILD_DIR="
for /f "delims=" %%D in ('powershell -NoProfile -Command "Get-ChildItem -Directory -Filter 'Build *' | Sort-Object { [int]($_.Name -replace '^Build ', '') } | Select-Object -Last 1 -ExpandProperty FullName"') do set "BUILD_DIR=%%D"

if not defined BUILD_DIR (
    echo No Build folder found.
    popd
    exit /b 1
)

if not exist "%BUILD_DIR%\QuickZoom.exe" (
    echo QuickZoom.exe was not found in "%BUILD_DIR%".
    popd
    exit /b 1
)

"%BUILD_DIR%\QuickZoom.exe" --capture-ui-screenshots
popd
