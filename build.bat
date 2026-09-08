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

set "CURRENT_BUILD="
for /f "tokens=6" %%N in ('findstr /C:"internal const int BuildNumber =" "%~dp0src\QuickZoom\AppInfo.cs"') do set "CURRENT_BUILD=%%N"
if not defined CURRENT_BUILD (
  echo ERROR: Could not read the current build number from AppInfo.cs.
  pause
  exit /b 1
)
set "CURRENT_BUILD=!CURRENT_BUILD:;=!"
set /a BUILD_NUMBER=CURRENT_BUILD+1

echo Updating AppInfo build number to %BUILD_NUMBER%...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\update-build-number.ps1" -BuildNumber %BUILD_NUMBER%
if errorlevel 1 (
  echo ERROR: Could not update AppInfo build number.
  pause
  exit /b 1
)
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
REM Output: .\Builds\Build N\QuickZoom.exe
REM ============================================================

set "PUBLISH_RID=win-x64"
set "PUBLISH_DIR=%CD%\Builds\Build %BUILD_NUMBER%"

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
REM Set SIGN_CERT_THUMBPRINT to a certificate in CurrentUser\My.
REM The private key remains in the certificate store; no password is passed.
REM Signing is verified and any signing or verification error fails the build.
REM
REM Example:
REM   set SIGN_CERT_THUMBPRINT=your40CharacterCertificateThumbprint
REM   build.bat
REM Optional: SIGN_TIMESTAMP_URL opts into build-time network timestamping.
REM The application does not use this URL. Restore and certificate verification
REM can also use build-machine networking; this is not a fully offline build.
REM
REM If signtool isn't installed, install the Windows 10/11 SDK
REM ("Windows SDK Signing Tools").

set "DO_SIGN=0"
if defined SIGN_CERT_THUMBPRINT set "DO_SIGN=1"

if "%DO_SIGN%"=="1" (
  echo.
  echo Signing enabled using the CurrentUser certificate store.

  REM -----------------------------
  REM Sign build output EXE (bin\Release\...)
  REM -----------------------------
  set "EXE_TO_SIGN="
  for /f "delims=" %%F in ('dir /b /s "bin\Release\*.exe" 2^>nul') do (
    if /i "%%~nxF"=="QuickZoom.exe" (
      if not defined EXE_TO_SIGN set "EXE_TO_SIGN=%%F"
    )
  )

  if defined EXE_TO_SIGN (
    echo Signing build output: "!EXE_TO_SIGN!"
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\sign-exe.ps1" -ExePath "!EXE_TO_SIGN!"
    if errorlevel 1 (
      echo ERROR: Signing build output failed.
      pause
      exit /b 1
    )
  ) else (
    echo ERROR: Could not find QuickZoom.exe under bin\Release\ to sign.
    exit /b 1
  )

  REM -----------------------------
  REM Sign self-contained publish EXE (Builds\Build N\QuickZoom.exe)
  REM -----------------------------
  set "PUBLISHED_EXE=%PUBLISH_DIR%\QuickZoom.exe"
  if exist "!PUBLISHED_EXE!" (
    echo Signing self-contained publish: "!PUBLISHED_EXE!"
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\sign-exe.ps1" -ExePath "!PUBLISHED_EXE!"
    if errorlevel 1 (
      echo ERROR: Signing self-contained publish failed.
      pause
      exit /b 1
    )
  ) else (
    echo ERROR: Published self-contained EXE not found at "!PUBLISHED_EXE!"
    exit /b 1
  )
)


echo.
echo ============================================================
echo SUCCESS
echo - Build output: .\bin\Release\
echo - Self-contained single-file: .\Builds\Build %BUILD_NUMBER%\QuickZoom.exe
echo ============================================================
echo.
pause
