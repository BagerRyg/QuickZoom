# QuickZoom 3.3.54

QuickZoom 3.3.54 is the Windows x64 standalone release of build 355. Download **QuickZoom.exe** from this release; it includes the .NET runtime and all five interface languages.

## Changes since Build 345

- Automatic following responds to mouse movement, typing, and keyboard focus. Choose Mouse only or Keyboard and typing when you want more control.
- Pause and resume following from the tray, Mouse settings, or **Alt + F**. The Follow menu offers the same modes throughout the app.
- The tray menu fits smaller screens by tightening spacing and scaling its contents when needed.
- Setup waits for startup configuration and the app's controls to be ready before completing, with clearer progress and retry states. The final success page stays visible until you dismiss it; double-clicks or a held Enter/Space key no longer skip it.
- Settings have improved responsiveness, keyboard behaviour, and layout.
- The README now covers the current app and includes current English and Danish screenshots. Full novice-friendly manuals explain setup, settings, everyday use, and troubleshooting in [English](https://github.com/BagerRyg/QuickZoom/blob/v3.3.54/Manuals/QuickZoom-User-Manual-English.md) and [Danish](https://github.com/BagerRyg/QuickZoom/blob/v3.3.54/Manuals/QuickZoom-Brugervejledning-Dansk.md).

## Download and update

1. Download **QuickZoom.exe** and save it on a local fixed drive.
2. If QuickZoom is already running, choose **Quit → Are you sure?** from its tray menu before replacing the old downloaded copy.
3. Run the new executable. Complete any requested automatic-startup update, then check **Settings → About** for **Version 3.3.54, Build 355**.

Windows 10 or 11, 64-bit, is required. No separate .NET installation is needed. Your existing local preferences are normally reused. Automatic startup is optional and requests administrator permission when configured.

The Windows file/product version is **3.3.54.355**: release 3.3.54, internal build 355. The executable is unsigned. **SHA256SUMS.txt** contains its checksum; compare it with `Get-FileHash .\QuickZoom.exe -Algorithm SHA256` after downloading.

Keyboard following depends on the target application's accessibility support. **Mouse only** remains available when an application does not expose a usable caret or focus position.

## Validation

The build and focused runtime, privacy, setup, tray-layout, UI, startup-task XML, and locale checks passed. The complete aggregate run is **not green**: another application obstructed its desktop-dependent Follow-menu mouse test. Isolated runs of that test passed. The real UAC flow still needs a retest in the Windows 10 VM. The bundled app version and uploaded executable checksum are verified separately; see **release-validation.json** for the scope and limitation.
