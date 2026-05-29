@echo off
setlocal

net session >nul 2>&1
if not "%errorlevel%"=="0" (
    echo Requesting administrator permissions...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

echo Removing QuickZoom startup scheduled tasks...

call :RemoveTask "QuickZoom Startup (Elevated)"
call :RemoveTask "QuickZoom Startup"
call :RemoveTask "QuickZoom Startup (Legacy)"
call :RemoveTask "QuickZoom Elevated Startup"
call :RemoveTask "QuickZoom 2 Startup"
call :RemoveTask "QuickZoom2 Startup"

echo.
echo Done. You can launch QuickZoom again to test startup service creation.
pause
exit /b

:RemoveTask
schtasks /Delete /TN %1 /F >nul 2>&1
if "%errorlevel%"=="0" (
    echo Removed: %~1
) else (
    echo Not found: %~1
)
exit /b
