# QuickZoom

<p align="center">
  <img src="assets/icons/magnifier-dark.ico" alt="QuickZoom" width="96">
</p>

<h3 align="center">Lightweight screen magnification for Windows 10 and 11</h3>

<p align="center">
  QuickZoom is a fast, tray-based accessibility tool built on the native Windows magnification engine.
</p>

<p align="center">
  <a href="https://github.com/BagerRyg/QuickZoom/releases">
    <img src="https://img.shields.io/badge/version-3.0-6ee08f?style=for-the-badge" alt="Version 3.0">
  </a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%20%2F%2011-64748b?style=for-the-badge" alt="Windows 10 and Windows 11">
  <img src="https://img.shields.io/badge/runtime-.NET%2010-512bd4?style=for-the-badge" alt=".NET 10">
  <img src="https://img.shields.io/badge/license-GPLv3-blue?style=for-the-badge" alt="GPLv3 license">
</p>

<p align="center">
  <a href="https://dev.ryg.dk/quickzoom/">Website</a> |
  <a href="https://github.com/BagerRyg/QuickZoom/releases">Download</a> |
  <a href="#features">Features</a> |
  <a href="#settings-reference">Settings Reference</a> |
  <a href="#screenshots">Screenshots</a> |
  <a href="#shortcuts">Shortcuts</a> |
  <a href="#translations">Translations</a> |
  <a href="#build-from-source">Build from source</a>
</p>

---

## About

QuickZoom is a lightweight magnification and accessibility tool for Windows 10 and Windows 11.

It was built to extend and improve the native Windows magnification experience with faster access, better tray controls, smoother everyday use, and practical multi-monitor support. The goal is simple: make zooming the desktop feel quick, reliable, and easy to adjust.

QuickZoom is also a free and open-source alternative for users who do not need a large paid accessibility suite. Tools such as ZoomText and SuperNova can be powerful, but they can also be expensive, resource-heavy, and more complex than some users need. QuickZoom focuses on the core magnification features that matter most during normal PC use.

## Why QuickZoom?

QuickZoom is designed for users who want:

- Fast zoom control without opening a large application window.
- A simple tray menu with the most important actions one click away.
- A full settings window for deeper configuration.
- Smooth magnification using the native Windows magnification engine.
- Reliable multi-monitor magnification.
- A portable, self-contained app that can also start automatically with Windows.
- A free, open-source accessibility tool that is easy to inspect, modify, and improve.

## Features

- Tray-based quick controls.
- Full settings window for advanced configuration.
- Mouse and keyboard zoom shortcuts.
- Smooth zoom transitions.
- Follow-cursor magnification.
- Optional center-cursor behavior.
- Auto-disable at 100% zoom.
- Inverted colors mode.
- Cursor enhancement and wiggle-to-locate support.
- Single-display and multi-display magnification modes.
- Magnification across all active displays.
- Per-monitor display selection.
- Dark, light, and system theme support.
- English, Danish, German, Norwegian, and Swedish interface.
- Optional elevated startup support for better compatibility with administrator apps.
- Portable self-contained release builds.

## Settings Reference

This section follows the visible QuickZoom interface order: first the tray menu, then the Settings window sidebar from top to bottom.

### Tray Menu

#### Zoom Mode

Switches between Fullscreen, Lens, and Docked without opening the full Settings window.  
It changes the magnification style immediately, using the same saved mode settings shown under Settings > Zoom.

#### Fullscreen

Magnifies the selected display area itself, so the whole chosen screen view becomes larger.  
This is the most traditional magnifier mode and is best for continuous reading or navigation.  
When Follow Cursor is enabled, the view pans as the pointer moves.  
It changes the whole visible workspace, so users get strong magnification but less surrounding context.

#### Lens

Shows a floating magnifier lens around the mouse cursor while the rest of the screen stays normal size.  
It is useful for inspecting small text, icons, buttons, or UI details without zooming the full desktop.  
The lens uses the current zoom level and follows the cursor with smoothing.  
It gives more context than fullscreen zoom because only the lens area is magnified.

#### Docked

Shows a fixed magnified tile attached to a screen edge.  
The tile updates around the cursor, while the rest of the screen stays normal size.  
It is useful when a user wants a stable preview area instead of a floating lens.  
The docked tile can cover part of the workspace, but it avoids constantly moving around the screen.

#### Enabled

Turns QuickZoom's zoom controls on or off.  
Turning it off resets zoom back to 100% and removes active magnification unless inverted colors still require fullscreen magnification.

#### Inverted Colors

Enables the inverted-colors feature and its hotkey behavior.  
When turned off, any active inverted view is cleared.

#### Follow Cursor

Controls whether fullscreen magnification pans with the pointer.  
When off, fullscreen zoom holds closer to the last focus point instead of constantly tracking the cursor.

#### Magnified Displays

Opens the quick display picker in the tray menu.  
It lets users choose all displays, the monitor under the cursor, or individual monitors without opening Settings.

#### Keyboard Shortcuts

Opens Settings > Shortcuts.  
Use it when changing the enable key, invert key, follow-cursor key, or mouse/keyboard shortcut mode.

#### Settings

Opens the full Settings window.  
This is where persistent zoom, display, cursor, appearance, shortcut, and about options live.

#### Reset Cursor

Restores the Windows cursor scheme and reapplies QuickZoom cursor enhancement if needed.  
Use it if the pointer looks wrong after changing cursor settings, waking from sleep, or closing another cursor tool.

#### About

Opens Settings > About.  
It shows the current build, startup status, app paths, debug logging, and usage help.

#### Quit

Closes QuickZoom completely.  
It also tears down magnification, hooks, overlays, timers, tray icon state, and temporary cursor changes.

### Settings > General

#### Smooth Zoom

Blends between zoom levels instead of jumping instantly.  
With it enabled, scrolling or pressing zoom keys feels softer and less abrupt.  
It can feel slightly less immediate because QuickZoom animates toward the target zoom level.  
With it disabled, every zoom step applies immediately, which feels sharper and more direct.

#### Disable Magnifier at 100%

Turns magnification off when zoom returns to 100% and no active visual effect needs it.  
This keeps the desktop in a normal state when the user is not zoomed in.  
It can reduce unnecessary magnifier work and avoids leaving hidden magnification windows active.  
For Lens and Docked modes, returning to 100% removes the overlay instead of showing a non-magnified tile.

#### Center Cursor

Changes fullscreen panning so the cursor or focus point stays closer to the center of the magnified view.  
With it enabled, moving the mouse pans the zoomed screen more aggressively around the pointer.  
This can feel focused and predictable for reading near the cursor.  
With it disabled, the cursor is allowed to sit closer to its natural screen position, which can feel smoother and less locked.

### Settings > Zoom

#### Mode

Chooses the main magnification style: Fullscreen, Lens, or Docked.  
Fullscreen magnifies the selected screen area itself.  
Lens creates a floating magnified area around the cursor.  
Docked creates a fixed magnified tile on a screen edge.  
Changing this setting affects how much of the desktop changes, how much context stays visible, and how much screen space is covered.

#### Lens Size

Only appears when Mode is set to Lens.  
Sets the lens width in pixels from 100 px to 1400 px.  
Rectangle lenses use a 16:9 height based on this width; Square and Round lenses use the same width and height.  
A larger lens shows more surrounding context but covers more of the normal screen.  
A smaller lens is less intrusive but shows less magnified content at once.

#### Lens Shape

Only appears when Mode is set to Lens.  
Rectangle is best for reading text lines and wider UI areas.  
Square gives an even inspection area for icons, controls, and compact UI.  
Round gives a classic magnifier feel and keeps attention on the cursor area.  
The shape changes the lens outline and visible area, not the zoom level itself.

#### Dock Position

Only appears when Mode is set to Docked.  
Attaches the magnified tile to the top, bottom, left, or right edge of the current screen.  
Top and bottom are useful for reading horizontal content while keeping the main screen visible.  
Left and right can be better for wide monitors or when vertical screen space matters.  
If the cursor enters the docked area, QuickZoom may use the opposite edge so the preview does not sit directly under the pointer.

#### Tile Size

Only appears when Mode is set to Docked.  
Sets how much screen space the tile occupies, capped at 50% of the screen.  
For top and bottom docking it controls tile height.  
For left and right docking it controls tile width.  
A larger tile is easier to read but covers more workspace; a smaller tile is less disruptive but gives a smaller preview.

#### Zoom Step (%)

Sets how much each wheel detent or keyboard zoom step changes the zoom level.  
Small values give fine control; large values reach high zoom faster but feel more jumpy.

#### Max Zoom (%)

Sets the highest zoom level QuickZoom can reach.  
Higher values allow stronger magnification but reduce visible context and make panning feel more sensitive.

#### Refresh Rate

Controls how often QuickZoom updates follow-cursor and animated zoom movement.  
Higher values can feel smoother, especially on high refresh rate monitors.  
They can also use more CPU/GPU work because the magnifier updates more often.  
Unlimited uses the highest detected monitor refresh rate as the target.  
If movement feels heavy or unstable, lowering this value can make the app feel calmer.

### Settings > Display

#### Auto-switch Monitor

Controls whether QuickZoom follows the monitor your cursor moves onto.  
With it enabled, fullscreen magnification can move between monitors as the pointer crosses display boundaries.  
With it disabled, QuickZoom locks to the current monitor until the selection changes.  
This is useful when accidental monitor switching feels distracting.  
It matters most when using "Where Cursor Is Present" or selected-monitor fullscreen behavior.

#### Magnified Displays

Chooses which monitors QuickZoom includes in fullscreen magnification.  
All Displays magnifies every connected display and is usually the most consistent multi-monitor choice.  
Where Cursor Is Present magnifies only the monitor currently under the mouse.  
Custom Selection lets the user choose specific monitors.  
Lens and Docked modes mainly follow the cursor's current screen, so this setting is most important for fullscreen mode.

#### Custom Monitor Toggles

Only appear when Magnified Displays is set to Custom Selection.  
Each monitor row includes or removes that display from the magnified fullscreen view.  
At least one monitor remains selected so QuickZoom always has a valid target.  
This is useful for excluding a secondary display that should stay normal while another display is magnified.

### Settings > Cursor

#### Locate Cursor on Wiggle

Highlights the cursor after quick mouse-wiggle movement.  
It helps users find the pointer without changing the current zoom level.

#### Cursor Enhancement

Applies QuickZoom's enhanced Windows cursor set while the app is running.  
This can make the pointer easier to see during magnification or on busy backgrounds.  
It changes the system cursor appearance temporarily, then restores the normal cursor scheme when QuickZoom exits or resets it.  
The size and color settings below are most useful when this option is enabled.

#### Cursor Size

Scales the enhanced cursor set from 100% to 500%.  
Larger cursors are easier to track, but very large cursors can cover small UI targets.

#### Cursor Colour

Sets the main fill color for the enhanced cursor.  
High-contrast colors are easier to see against complex or changing backgrounds.

#### Border Colour

Sets the outline color for the enhanced cursor.  
A contrasting border helps the cursor remain visible on both light and dark content.

#### Preview

Shows the current cursor fill, border, and size choices.  
It is a visual check only and does not change zoom behavior by itself.

### Settings > Shortcuts

#### Shortcut Mode

Chooses whether QuickZoom accepts mouse shortcuts, keyboard shortcuts, or both.  
Both allows the default mouse wheel zoom and keyboard +/- zoom.  
Keyboard only disables mouse-wheel zoom and mouse invert triggers.  
Mouse only disables keyboard zoom and secondary-key toggles.  
This is useful if a shortcut conflicts with another app or if the user only wants one input style.

#### Enable Key

Sets the primary key held while zooming or using QuickZoom shortcut combos.  
By default this is Alt.  
The enable key works with mouse wheel, keyboard +/-, invert color key, and follow cursor key.  
Choosing a comfortable key matters because it is the main interaction point for zooming.  
Some keys may show warnings if they conflict with Windows or other QuickZoom shortcut roles.

#### Invert Colors Key

Sets the secondary keyboard key used with the enable key to toggle inverted colors.  
The default is I, so the default keyboard combo is Alt + I.  
Middle mouse click with the enable key remains the default mouse-style invert trigger when mouse shortcuts are enabled.  
Invert colors can reduce glare or improve contrast for some content.  
The actual color inversion affects the magnified view through the native Windows magnification color effect.

#### Follow Cursor Key

Sets the secondary keyboard key used with the enable key to toggle Follow Cursor.  
The default is F, so the default combo is Alt + F.  
It is most noticeable in fullscreen mode, where it changes whether the zoomed view pans with the mouse.  
Turning Follow Cursor off can be useful when reading a fixed area without the view moving.

#### Disable Alt Key in Office Apps

Only works when the Enable Key is Alt.  
Prevents Microsoft Office ribbon key tips from stealing focus while using Alt + mouse wheel.  
This makes zooming in Word, Excel, Outlook, PowerPoint, OneNote, Access, Publisher, and Visio feel less disruptive.  
It is disabled automatically when the enable key is not Alt.

### Settings > Appearance

#### Theme Mode

Chooses Auto - System, Dark, or Light.  
Auto follows Windows theme; Dark and Light keep QuickZoom fixed.

#### Language

Changes the tray menu, Settings window, and dialogs.  
The setting is saved and the UI refreshes after selection.

#### UI Font Size

Changes QuickZoom's own interface text size: Default, Large, or Extra large.  
It affects the tray menu and Settings window, improving readability but requiring layouts to fit larger text.

### Settings > About

#### Build and Startup

Shows the current QuickZoom version, build number, and startup service status.  
If startup support is broken, this row can show a repair action.  
The startup service helps QuickZoom launch elevated at sign-in, which can make shortcuts work better over administrator apps.  
This row does not change zoom feel directly, but it affects reliability after reboot and with elevated windows.

#### Locations

Provides buttons for the install folder and config folder.  
These are mainly for troubleshooting, updates, backups, or checking where settings and logs are stored.

#### Debug Logging

Lets the user open the log file and turn extra diagnostic logging on or off.  
Crash logs are always kept, but debug logging writes more detail while QuickZoom runs.  
It is useful when diagnosing broken hooks, startup issues, display changes, or magnification failures.  
For normal use it can stay off to keep logs quieter.

#### How to Use

Shows the basic usage instructions inside the app.  
It reminds users to hold the enable key and scroll, or use +/- to zoom, and use the invert hotkey for inverted colors.

### Settings Window Footer

#### Reset to Defaults

Restores the default QuickZoom settings, resets zoom to 100%, and turns off active magnification.  
It also refreshes cursor enhancement and rebuilds the tray/settings UI.

#### Done

Closes the Settings window.  
Pending setting changes are saved before the window is fully closed.

## Multi-monitor support

QuickZoom supports magnification on multiple displays.

The most reliable and smoothest mode is magnification across all active displays. This mode is recommended for most multi-monitor setups because it avoids many of the edge cases that can happen when only one selected monitor is magnified.

Per-monitor selection is also available for users who need a more specific setup.

## Protected video playback

Because QuickZoom uses the native Windows magnification engine, it can often magnify protected video playback surfaces such as TV players, streaming services, and DRM-protected browser video.

This can work better than some third-party magnification tools in certain setups, but behavior can still depend on the app, browser, GPU driver, hardware acceleration, and the type of protected content being played.

## Portable or automatic startup

QuickZoom can be run as a portable self-contained app.

No full installation is required for normal use. Download the release, run the executable, and QuickZoom starts in the system tray.

QuickZoom can also be configured to start automatically with Windows. This requires approving a single UAC prompt during setup because Windows needs permission to register the built-in elevated startup support.

## Shortcuts

These are the default shortcuts. They can be changed in Settings.

| Action | Shortcut |
| --- | --- |
| Zoom with mouse | `Alt` + mouse wheel |
| Zoom with keyboard | `Alt` + `+` / `-` |
| Invert colors with mouse | `Alt` + middle mouse button |
| Invert colors with keyboard | `Alt` + `I` |

## Screenshots

### Tray Menu

![QuickZoom tray menu](assets/screenshots/Build%20229/dark/en/tray-menu.png)

### Cursor Settings

![QuickZoom cursor settings](assets/screenshots/Build%20229/dark/en/settings-cursor.png)

### Keyboard Shortcuts

![QuickZoom shortcut settings](assets/screenshots/Build%20229/dark/en/settings-shortcuts.png)

## Requirements

- Windows 10 x64 or Windows 11 x64.
- .NET 10 Desktop Runtime, unless you use the self-contained release build.

## Translations

QuickZoom locale files are stored in `locales/` as JSON files. To add a new language:

1. Copy `locales/en.json`.
2. Rename the copy to the language code, for example `fr.json`.
3. Translate the JSON values only. Keep the keys unchanged.
4. Add the language to `UiLanguage` in `src/QuickZoom/UiText.cs`.
5. Add its file code in `src/QuickZoom/LocalizationManager.cs`.
6. Add its display name key to each locale file, then build normally.

Locale files are embedded in the app and also copied beside the executable during publish.

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
.\build.bat
```

Each run increments the build number and creates the standalone executable at `Builds\Build N\QuickZoom.exe`.

## Elevated startup support

QuickZoom can optionally install a managed startup copy and register elevated startup support. This helps QuickZoom work more consistently with applications that run as administrator.

This setup is optional. QuickZoom can also run directly without registering startup support.

## Project status

QuickZoom is actively developed as a focused Windows accessibility utility.

The project aims to stay lightweight, practical, and easy to use instead of becoming a large all-in-one accessibility suite.

## Limitations

QuickZoom relies on Windows' native magnification functionality, so some behavior can depend on Windows, GPU drivers, display scaling, and the application being magnified.

Fullscreen games, anti-cheat software, protected windows, and some video playback surfaces may behave differently depending on the system.

## License

QuickZoom is licensed under the GNU General Public License v3.0.

See [LICENSE](LICENSE) for the full license text.
