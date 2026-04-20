@echo off
setlocal EnableExtensions EnableDelayedExpansion

cd /d "%~dp0"

echo ============================================================
echo Build started: %DATE% %TIME%
echo Folder: %CD%
echo ============================================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
  echo ERROR: dotnet SDK not found in PATH.
  pause
  exit /b 1
)

echo Using:
where dotnet
for /f "delims=" %%v in ('dotnet --version') do set SDKVER=%%v
echo .NET SDK version: !SDKVER!
echo.

echo Restoring...
dotnet restore
if errorlevel 1 (
  echo ERROR: Restore failed.
  pause
  exit /b 1
)

echo Building (Release)...
dotnet build -c Release
if errorlevel 1 (
  echo ERROR: Build failed.
  pause
  exit /b 1
)

REM ============================================================
REM Publish self-contained single-file build (win-x64)
REM Output: .\dist\self-contained\win-x64\
REM ============================================================

set "PUBLISH_RID=win-x64"
set "PUBLISH_DIR=%CD%\dist\self-contained\%PUBLISH_RID%"

echo.
echo Publishing self-contained (Release, %PUBLISH_RID%)...
dotnet publish -c Release -r %PUBLISH_RID% --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:PublishTrimmed=false ^
  -p:DebugType=None ^
  -o "%PUBLISH_DIR%"
if errorlevel 1 (
  echo ERROR: Publish (self-contained) failed.
  pause
  exit /b 1
)


REM ============================================================
REM Optional code signing (self-signed or otherwise)
REM
REM If you set SIGN_PFX (and optionally SIGN_PWD), this script will try
REM to sign the built EXE using signtool (from the Windows SDK).
REM
REM Example:
REM   set SIGN_PFX=QuickZoom_Signing.pfx
REM   set SIGN_PWD=yourPfxPassword
REM   build.bat
REM
REM If signtool isn't installed, install the Windows 10/11 SDK
REM ("Windows SDK Signing Tools").

set "DO_SIGN=0"
if not "%SIGN_PFX%"=="" set "DO_SIGN=1"

if "%DO_SIGN%"=="1" (
  echo.
  echo Signing enabled: SIGN_PFX=%SIGN_PFX%

  if not exist "%SIGN_PFX%" (
    echo ERROR: SIGN_PFX file not found: "%SIGN_PFX%"
    echo Tip: Run scripts\create_signing_cert.ps1 to create a self-signed code-signing cert.
    pause
    exit /b 1
  )

  REM -----------------------------
  REM Sign build output EXE (bin\Release\...)
  REM -----------------------------
  set "EXE_TO_SIGN="
  for /f "delims=" %%F in ('dir /b /s "bin\Release\*.exe" 2^>nul') do (
    if /i "%%~nxF"=="QuickZoom.exe" (
      set "EXE_TO_SIGN=%%F"
      goto :foundexe
    )
  )
  :foundexe

  if not "%EXE_TO_SIGN%"=="" (
    echo Signing build output: "%EXE_TO_SIGN%"
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\sign_exe.ps1" -ExePath "%EXE_TO_SIGN%" -PfxPath "%SIGN_PFX%" -PfxPassword "%SIGN_PWD%"
    if errorlevel 1 (
      echo ERROR: Signing build output failed.
      pause
      exit /b 1
    )
  ) else (
    echo WARNING: Could not find QuickZoom.exe under bin\Release\ to sign.
  )

  REM -----------------------------
  REM Sign self-contained publish EXE (dist\self-contained\...\QuickZoom.exe)
  REM -----------------------------
  set "PUBLISHED_EXE=%PUBLISH_DIR%\QuickZoom.exe"
  if exist "%PUBLISHED_EXE%" (
    echo Signing self-contained publish: "%PUBLISHED_EXE%"
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\sign_exe.ps1" -ExePath "%PUBLISHED_EXE%" -PfxPath "%SIGN_PFX%" -PfxPassword "%SIGN_PWD%"
    if errorlevel 1 (
      echo ERROR: Signing self-contained publish failed.
      pause
      exit /b 1
    )
  ) else (
    echo WARNING: Published self-contained EXE not found at "%PUBLISHED_EXE%"
  )
)


echo.
echo ============================================================
echo SUCCESS
echo - Build output: .\bin\Release\
echo - Self-contained single-file: .\dist\self-contained\%PUBLISH_RID%\
echo ============================================================
echo.
pause
