# QuickZoom — Full Menu + Dark Icon
Build:
  dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true

## Optional: Digitally sign the EXE (self-signed)

Windows will still often warn on unsigned executables. You can sign the EXE using a
**self-signed code-signing certificate**.

Important limitations:
- A self-signed cert is **only trusted on PCs where you install/trust it**.
  To avoid "Unknown publisher" on another PC, you must import the exported `.cer`
  into that PC’s **Trusted Root Certification Authorities** and **Trusted Publishers**.
- SmartScreen reputation warnings can still appear even with a signature unless the
  signing cert is publicly trusted (commercial certificate) and/or has reputation.

### 1) Create the certificate
Run in PowerShell:

  .\create_signing_cert.ps1

This creates:
- `QuickZoom_Signing.pfx` (private key, used for signing)
- `QuickZoom_Signing.cer` (public cert, used to trust the signer on other machines)

### 2) Sign via build.bat
Set environment variables and build:

  set SIGN_PFX=QuickZoom_Signing.pfx
  set SIGN_PWD=...your password...
  build.bat

`build.bat` calls `sign_exe.ps1` which uses `signtool.exe` (Windows SDK). If you don’t
have it installed, install the Windows 10/11 SDK (Signing Tools).

## Run Elevated At Logon (No UAC Prompt Every Boot)

If you want QuickZoom to work over elevated apps without UAC prompts on every startup,
register a Task Scheduler task once:

```powershell
.\install_startup_task.ps1 -RunNow
```

Run this from an elevated PowerShell window (Run as Administrator).

If auto-detection does not find your EXE, pass it explicitly:

```powershell
.\install_startup_task.ps1 -ExePath ".\publish\win-x64-elevated-hotkey\QuickZoom.exe" -RunNow
```

To remove the startup task later:

```powershell
.\remove_startup_task.ps1
```
