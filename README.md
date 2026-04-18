# QuickZoom

QuickZoom is a lightweight screen magnifier for Windows 10 and Windows 11 x64. It is built for people who want a faster, cleaner, and more practical alternative to the built-in Windows Magnifier, without the overhead of larger commercial accessibility suites.

QuickZoom stays in the system tray, gives you instant zoom access with simple hotkeys, supports inverted colors, and is designed to feel responsive during everyday reading, browsing, and desktop work.

## Why QuickZoom

- Faster to access than the built-in Windows magnifier flow
- Lightweight tray-based design with quick controls
- Better suited for users who want zoom on demand instead of a full accessibility shell
- Works with elevated applications when the startup service is configured
- Built with a modern dark/light aware interface and a simpler settings experience

## Core Features

- System tray quick menu for everyday controls
- Enable or disable magnification instantly
- Hold-to-zoom workflow using keyboard and mouse shortcuts
- Optional inverted colors mode for bright pages, documents, and interfaces
- Follow cursor mode for natural movement while zooming
- Smooth zoom transitions
- Auto-disable magnification at 100%
- Optional center-cursor behavior
- Cursor locate on wiggle to help find the pointer quickly
- Multi-monitor support
- Choose all displays, monitor under cursor, or specific displays
- Per-monitor aware UI and tray behavior
- Startup service option for elevated app compatibility without repeated UAC prompts

## Main Functions

### Magnification

QuickZoom lets you zoom in and out quickly while working, reading, or navigating the desktop. By default:

- Hold `Alt` and use the mouse wheel to zoom
- Hold `Alt` and use `+` or `-` on the keyboard to adjust zoom

### Inverted Colors

QuickZoom can invert colors independently of magnification, which is useful for bright documents, web pages, PDFs, and white-background interfaces.

By default:

- Hold `Alt` and click the mouse wheel
- Or use `Alt + I`

### Multi-Monitor Behavior

QuickZoom supports multiple monitors and can:

- magnify all connected displays
- follow the monitor under the cursor
- let you choose exactly which monitor(s) to include

This makes it flexible for both single-monitor and multi-monitor workflows.

### Startup Service

QuickZoom includes an optional startup service setup flow that can:

- install QuickZoom into a managed location
- register an elevated startup task
- allow QuickZoom to work more reliably with elevated apps
- avoid repeated UAC prompts on every boot after one-time setup

## Supported UI Options

### Themes

QuickZoom supports:

- Auto - System
- Dark
- Light

The app can follow the Windows app theme automatically or stay fixed in your preferred mode.

### Languages

Current built-in UI languages:

- English
- Danish

## Settings You Can Adjust

QuickZoom includes a settings window for options you may not want in the tray all the time, including:

- zoom step
- maximum zoom level
- refresh rate
- magnified display mode
- theme mode
- language
- enable key
- invert hotkey
- follow cursor
- smooth zoom
- center cursor
- wiggle-to-locate cursor

## Who It Is For

QuickZoom is intended for users who:

- need quick temporary magnification while reading
- want a simpler alternative to Windows Magnifier
- want a tray-first workflow instead of a larger accessibility shell
- prefer something lighter than bulky paid products such as ZoomText or SuperNova

## Screenshot

Tray menu preview will be added here once the screenshot asset is committed into the repository.

Suggested path for the tray image:

```text
docs/images/tray-menu.png
```

Once the image file is added, this section can use:

```md
![QuickZoom tray menu](docs/images/tray-menu.png)
```

## Build and Run

### Requirements

- Windows 10 x64 or Windows 11 x64
- .NET 8 Desktop Runtime or later, unless you use the self-contained build

### Build From Source

```powershell
dotnet build .\QuickZoom.csproj -c Release
```

### Publish Self-Contained

```powershell
dotnet publish .\QuickZoom.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o .\Build 76
```

## Startup Service

QuickZoom can be configured to start elevated automatically so it can interact more reliably with elevated apps.

The project includes:

- managed install support
- scheduled task setup
- startup cleanup for older task variants
- validation of the current installed payload

## Availability and Use

QuickZoom is free to use as a local utility and the full source is available in this repository.

Important note:

- this repository does not currently include a formal `LICENSE` file

That means the code should not be presented as fully licensed open-source software yet. If you want, the next step should be to add the exact license you want for GitHub and redistribution.

If your intent is:

- GNU open-source licensing
- personal-use-only terms

those should be resolved clearly first, because they are not the same thing legally.

## Project Status

QuickZoom is an actively iterated Windows utility focused on practical accessibility, cleaner UX, and fast tray-based control.

If you want, the next documentation step can be:

1. add a proper screenshot asset to the repo
2. add a formal `LICENSE` file
3. add a release/download section for the latest self-contained build
