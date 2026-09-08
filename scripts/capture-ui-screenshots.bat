@echo off
setlocal
pushd "%~dp0.."

set "BUILD_DIR="
for /f "delims=" %%D in ('powershell -NoProfile -Command "Get-ChildItem -LiteralPath '.\Builds' -Directory -Filter 'Build *' -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^Build (\d+)$' } | Sort-Object { [int]($_.Name -replace '^Build ', '') } -Descending | Select-Object -First 1 -ExpandProperty FullName"') do set "BUILD_DIR=%%D"

if not defined BUILD_DIR (
    echo No numbered build was found in "Builds".
    echo Run build.bat first.
    popd
    exit /b 1
)

set "QUICKZOOM_EXE=%BUILD_DIR%\QuickZoom.exe"
if not exist "%QUICKZOOM_EXE%" (
    echo QuickZoom.exe was not found in "%BUILD_DIR%".
    popd
    exit /b 1
)

"%QUICKZOOM_EXE%" --capture-ui-screenshots
popd
