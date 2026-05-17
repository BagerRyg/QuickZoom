# QuickZoom

<p align="center">
  <img src="assets/icons/magnifier_dark.ico" alt="QuickZoom" width="96">
</p>

<h3 align="center">Fast tray-based screen magnification for Windows</h3>

<p align="center">
  A lightweight accessibility tool for quick desktop zoom, cursor focus, inverted colors, and multi-monitor workflows.
</p>

<p align="center">
  <a href="https://github.com/BagerRyg/QuickZoom/releases">
    <img src="https://img.shields.io/badge/version-2.122-6ee08f?style=for-the-badge" alt="Version 2.122">
  </a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%20%2F%2011-64748b?style=for-the-badge" alt="Windows 10 and Windows 11">
  <img src="https://img.shields.io/badge/runtime-.NET%2010-512bd4?style=for-the-badge" alt=".NET 10">
</p>

<p align="center">
  <a href="https://dev.ryg.dk/quickzoom/">Website</a> |
  <a href="https://github.com/BagerRyg/QuickZoom/releases">Download</a> |
  <a href="#features">Features</a> |
  <a href="#shortcuts">Shortcuts</a> |
  <a href="#build-from-source">Build from source</a>
</p>

---

QuickZoom is a Windows screen magnifier built for people who need zoom immediately without opening a heavy accessibility suite or digging through menus. It runs from the system tray, responds to simple keyboard and mouse shortcuts, and stays out of the way until you need it.

I built it because I am visually impaired and wanted something faster, simpler, and more comfortable than Windows Magnifier for daily PC use.

## Contents

- [Project goal](#project-goal)
- [Features](#features)
- [Shortcuts](#shortcuts)
- [Screenshots](#screenshots)
- [Requirements](#requirements)
- [Download](#download)
- [Build from source](#build-from-source)
- [Elevated startup support](#elevated-startup-support)
- [Status](#status)
- [License](#license)

## Project goal

QuickZoom is meant to be a practical accessibility tool for everyday Windows use:

- Fast temporary magnification.
- Simple tray-first controls.
- Better cursor visibility.
- Multi-monitor support.
- A lighter alternative to large commercial accessibility suites.

## Features

- Tray-based quick controls.
- Mouse and keyboard zoom shortcuts.
- Inverted colors mode.
- Follow cursor support.
- Smooth zoom transitions.
- Auto-disable at 100%.
- Optional center-cursor behavior.
- Cursor enhancement and wiggle-to-locate.
- Single-monitor and multi-monitor modes.
- Per-monitor display selection.
- Dark, light, and system theme support.
- English and Danish interface.
- Optional elevated startup support for better compatibility with administrator apps.

## Shortcuts

These are the default shortcuts. They can be changed in Settings.

| Action | Shortcut |
| --- | --- |
| Zoom with mouse | `Alt` + mouse wheel |
| Zoom with keyboard | `Alt` + `+` / `-` |
| Invert colors with mouse | `Alt` + middle mouse button |
| Invert colors with keyboard | `Alt` + `I` |

## Screenshots

### Tray menu

![QuickZoom tray menu](assets/images/tray_menu.png)

### Settings window

![QuickZoom settings window](assets/images/settings.png)

## Requirements

- Windows 10 x64 or Windows 11 x64.
- .NET 10 Desktop Runtime, unless you use the self-contained release build.

## Download

Download the latest release from the [GitHub releases page](https://github.com/BagerRyg/QuickZoom/releases).

The published release is intended for Windows x64.

## Build from source

Clone the repository and build the project in Release mode:

```powershell
git clone https://github.com/BagerRyg/QuickZoom.git
cd QuickZoom
dotnet build .\QuickZoom.csproj -c Release
```

Create a self-contained Windows x64 build:

```powershell
dotnet publish .\QuickZoom.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=None -o ".\Build 1"
```

## Elevated startup support

QuickZoom can optionally install a managed startup copy and register an elevated startup task. This helps when you want QuickZoom to keep working smoothly with applications that run as administrator.

This setup is optional. The app can also run directly without installing the startup task.

## Status

QuickZoom is actively developed as a focused Windows accessibility utility.

## License

QuickZoom is licensed under the GNU General Public License v3.0.

See [LICENSE](LICENSE) for the full license text.
