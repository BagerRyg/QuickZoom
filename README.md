# QuickZoom

<p align="center">
  <img src="assets/icons/magnifier-dark.ico" alt="QuickZoom" width="96">
</p>

<h3 align="center">Screen magnification for Windows 10 and 11</h3>

<p align="center">
  Enlarge the whole screen, inspect details through a lens, or keep an enlarged view at a screen edge.
</p>

<p align="center">
  <a href="https://github.com/BagerRyg/QuickZoom/releases/latest"><img src="https://img.shields.io/badge/version-3.3.54-6ee08f?style=for-the-badge" alt="Version 3.3.54"></a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%20%2F%2011%20x64-64748b?style=for-the-badge" alt="Windows 10 and Windows 11 x64">
  <img src="https://img.shields.io/badge/runtime-.NET%2010-512bd4?style=for-the-badge" alt=".NET 10">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-GPLv3-blue?style=for-the-badge" alt="GPLv3 license"></a>
</p>

<p align="center">
  <a href="https://github.com/BagerRyg/QuickZoom/releases/latest">Download</a> ·
  <a href="Manuals/QuickZoom-User-Manual-English.md">English manual</a> ·
  <a href="Manuals/QuickZoom-Brugervejledning-Dansk.md">Dansk brugervejledning</a> ·
  <a href="#screenshots">Screenshots</a> ·
  <a href="#build-from-source">Build from source</a> ·
  <a href="https://dev.ryg.dk/quickzoom/">Website</a>
</p>

QuickZoom is a free, open-source desktop magnifier built on Windows' native magnification engine. It stays in the notification area near the clock, with mouse and keyboard shortcuts for everyday use. **Version 3.3.54 uses internal build 355.**

## Get started

1. Download **QuickZoom.exe** from the [latest release](https://github.com/BagerRyg/QuickZoom/releases/latest). The standalone Windows x64 release includes .NET; no separate runtime installation is needed.
2. Save it in a normal folder on a **local fixed drive**, then run it. Network drives, removable drives, and redirected/link-based storage paths are not supported.
3. Follow setup to choose your language, theme, activation key, privacy mode, and optional automatic startup. Keep **Alt** as the activation key for the defaults below.
4. Hold **Alt** and scroll up, or press **Alt + +**, to zoom in. Release Alt when the view is large enough; the enlargement stays in place.
5. Hold **Alt** and scroll down, or press **Alt + −**, to return to **100%**, which is normal size.

Click QuickZoom's tray icon with either mouse button to open its controls. If the icon is hidden, open the taskbar's hidden-icons arrow. On smaller screens, the menu reduces spacing and, if needed, text and icon size to fit. **Magnification** must be On for zoom shortcuts to work; turning it on alone does not enlarge the screen. Turning it Off returns zoom to 100%. **Quit → Are you sure?** closes the app completely; closing Settings leaves it running.

**Updating an existing copy?** Quit the running app before replacing its downloaded executable. Run the new copy, complete any requested startup update, and check **About** for the new version. Your local preferences are normally reused.

For setup help, every setting, examples, troubleshooting, backups, and removal instructions, read the [English user manual](Manuals/QuickZoom-User-Manual-English.md) or [danske brugervejledning](Manuals/QuickZoom-Brugervejledning-Dansk.md).

## Features

- **Three zoom modes:** Fullscreen, Lens, and Docked, sharing one current zoom level.
- **Mouse and keyboard following:** Automatic switches between pointer movement, typing, and keyboard focus; Mouse only and Keyboard and typing give explicit control. Pause/resume keeps a passage still while reading.
- **Multiple monitors:** magnify all displays, the active monitor, or a custom selection in Fullscreen. Identify displays helps match the list to your screens.
- **Adjustable magnification:** smooth transitions, configurable zoom steps, a maximum up to 750%, optional centring, and refresh-rate controls.
- **Pointer visibility:** wiggle to locate the pointer, or apply an enhanced Windows cursor with adjustable size, fill colour, and outline.
- **Colour inversion:** reverse the colours in the magnified view using a mouse or keyboard shortcut.
- **Accessible settings:** search, keyboard navigation, exact numeric entry, Windows text-size support, and light/dark/system themes.
- **Five interface languages:** English, Danish, Swedish, Norwegian, and Finnish.
- **Local preferences and optional logs:** Strict Data mode blocks diagnostic logging. No account is required.
- **Optional elevated autostart:** start at Windows sign-in with better access to applications running as administrator.

## Screenshots

Existing Build 354 captures in English and Danish, with dark and light themes. Click an image to view it at full size. The values shown are examples, not necessarily the defaults.

<table>
  <tr>
    <th>Tray controls</th>
    <th>Following and pause</th>
  </tr>
  <tr>
    <td align="center"><a href="assets/Screenshots/Build%20354/en/dark/tray/quickzoom-build-354-en-dark-tray-menu.webp"><img src="assets/Screenshots/Build%20354/en/dark/tray/quickzoom-build-354-en-dark-tray-menu.webp" alt="Dark tray menu with zoom modes, magnification, colour inversion, following, and Settings" width="300"></a></td>
    <td align="center"><a href="assets/Screenshots/Build%20354/en/dark/tray/quickzoom-build-354-en-dark-tray-follow-options.webp"><img src="assets/Screenshots/Build%20354/en/dark/tray/quickzoom-build-354-en-dark-tray-follow-options.webp" alt="Follow menu with Automatic, Mouse only, Keyboard and typing, and Pause following" width="300"></a></td>
  </tr>
  <tr>
    <th>Lens settings · dark theme</th>
    <th>Docked settings · light theme</th>
  </tr>
  <tr>
    <td><a href="assets/Screenshots/Build%20354/en/dark/settings/quickzoom-build-354-en-dark-settings-zoom-lens-controls.webp"><img src="assets/Screenshots/Build%20354/en/dark/settings/quickzoom-build-354-en-dark-settings-zoom-lens-controls.webp" alt="Lens mode with size, shape, zoom step, and maximum zoom controls" width="600"></a></td>
    <td><a href="assets/Screenshots/Build%20354/en/light/settings/quickzoom-build-354-en-light-settings-zoom-docked-controls.webp"><img src="assets/Screenshots/Build%20354/en/light/settings/quickzoom-build-354-en-light-settings-zoom-docked-controls.webp" alt="Docked mode with screen-edge position and tile-size controls" width="600"></a></td>
  </tr>
  <tr>
    <th>Cursor size and colours</th>
    <th>Danish interface</th>
  </tr>
  <tr>
    <td><a href="assets/Screenshots/Build%20354/en/light/settings/quickzoom-build-354-en-light-settings-mouse-cursor-options.webp"><img src="assets/Screenshots/Build%20354/en/light/settings/quickzoom-build-354-en-light-settings-mouse-cursor-options.webp" alt="Cursor-size slider and colour palettes for the pointer fill and border" width="600"></a></td>
    <td><a href="assets/Screenshots/Build%20354/da/dark/settings/quickzoom-build-354-da-dark-settings-appearance.webp"><img src="assets/Screenshots/Build%20354/da/dark/settings/quickzoom-build-354-da-dark-settings-appearance.webp" alt="Danish Appearance settings with theme, language, and interface font-size choices" width="600"></a></td>
  </tr>
</table>

## Zoom modes and following

| Mode | How it works |
| --- | --- |
| **Fullscreen** | Enlarges the selected displays and pans the visible area as the followed point moves. Choose All Displays, Where Cursor Is Present, or Custom Selection. |
| **Lens** | Shows a floating magnified area around the followed point. Choose Rectangle, Square, or Round and a width from 100 to 1400 px. |
| **Docked** | Shows an enlarged tile at the top, bottom, left, or right of the screen. Set its size to 10–50% of the screen; it can move to the opposite edge to avoid covering the followed point. |

All modes support **Automatic**, **Mouse only**, and **Keyboard and typing** following. Automatic follows typing and keyboard navigation, then returns to the pointer after deliberate mouse activity. **Pause following** freezes the view without forgetting the selected mode. Choosing a Follow mode resumes following.

Lens and Docked use the screen containing the followed point; Fullscreen display selection does not restrict them. Following a text caret or focused control depends on that application's Windows accessibility information. If an app does not provide a usable position, use Mouse only to position the view yourself.

## Shortcuts

Hold the activation key first, perform the action, then release it. These defaults use **Alt**; use left Alt rather than AltGr on keyboards that have both. Keyboard +/− and numeric-keypad +/− are supported.

| Action | Mouse | Keyboard |
| --- | --- | --- |
| Zoom in | Alt + scroll up | Alt + + |
| Zoom out | Alt + scroll down | Alt + − |
| Toggle inverted colours | Alt + middle click | Alt + I |
| Switch zoom mode | Alt + left and right mouse buttons together | Alt + Z |
| Pause/resume following | Follow menu in the tray | Alt + F |

Turn **Invert colours** On in the tray before using its shortcuts. The switch enables the feature; the shortcut toggles the effect. Turning the switch Off clears inversion. Magnification and inversion are separate controls.

Mode switching cycles **Fullscreen → Docked → Lens**. Z is fixed; the activation, inversion, and pause/resume keys can be changed under **Shortcuts**. **Shortcut mode** selects Both, Keyboard only, or Mouse only; Mouse only still needs the keyboard activation key. Release the activation key before normal clicking to avoid the two-button gesture.

If shortcuts also trigger another app's menus, try **Give QuickZoom shortcuts priority** or choose different keys. This setting applies generally, including Office; it preserves bare Windows-key actions, AltGr, and unrelated Windows shortcuts. Escape closes menus or Settings; it is not a global zoom-reset shortcut.

When changing a shortcut key, press one new key. To cancel that key-capture dialog, click its **X**: pressing Escape there can assign Escape as the shortcut key.

## Settings reference

Open the tray's **Open Settings**, or choose **Shortcut settings** to go straight to the key controls. Settings save automatically. Use **Ctrl+F** to search; sliders also accept typed whole numbers within their permitted range.

| Page | Available controls |
| --- | --- |
| **General** | Smooth Zoom; Disable Magnifier at 100%; Center Cursor for Fullscreen framing. |
| **Zoom** | Mode; Lens size/shape when Lens is selected; Dock position/Tile size when Docked is selected; Zoom step (1–200 percentage points); Max zoom (150–750%); Refresh rate (60, 90, 120, 180, 240 Hz, or Unlimited). |
| **Display** | Auto-switch monitor; Magnified displays; Identify displays; individual monitor switches under Custom Selection. These affect Fullscreen. |
| **Mouse** | Follow; Pause following; Locate Cursor on Wiggle; Cursor enhancement; Preview; Cursor size (100–500%); Cursor colour and Border colour from 48 preset swatches. |
| **Shortcuts** | Shortcut mode; Activation key; Invert colours key; Pause/resume following key; Give QuickZoom shortcuts priority. |
| **Appearance** | Theme: Follow Windows, Dark, or Light; Language; UI font size: Follow Windows, Large, or Extra large. |
| **About** | Version/build and startup status; Set up autostart when needed; install/config folder buttons; Strict Data mode; session troubleshooting logging; Show log file. |

Defaults include Fullscreen, Automatic following, 30-point zoom steps, a 400% maximum, and 120 Hz updates. **Unlimited** targets the highest detected monitor refresh rate, with a minimum of 60 Hz. It does not mean infinite updates.

Cursor size and colour can be previewed before enabling **Cursor enhancement**. Turning enhancement off restores the normal Windows cursor scheme. The tray's **Reset Cursor** reloads that scheme and reapplies enhancement if enabled.

**Done** closes Settings without quitting. **Reset all settings** requires a second click within ten seconds; it resets preferences and active zoom, but does not uninstall QuickZoom or disable its startup task. For exact defaults, control behaviour, and recovery instructions, use the [manual](Manuals/QuickZoom-User-Manual-English.md).

## Setup and automatic startup

The seven-step setup covers language, theme, activation key, a live practice page, data mode, optional automatic startup, and completion. Its Large setup option makes setup easier to read; everyday interface text size is configured separately under Appearance.

QuickZoom can run directly from a local folder without installing automatic startup. If you approve autostart, Windows requests administrator permission. QuickZoom installs a managed copy under `%ProgramFiles%\QuickZoom` and registers the **QuickZoom Startup (Elevated)** scheduled task for sign-in. This is optional and can also be configured later under **About → Set up autostart**.

Wait for startup verification, or choose **Skip** to launch manually. **Finish** closes setup after the tray and shortcuts are ready. There is no in-app autostart-off switch or uninstaller; the manuals explain how to disable the task and remove the app.

## Privacy and local files

Preferences are stored for the current Windows user in `%LOCALAPPDATA%\QuickZoom\settings.json`. **About → Open Config Folder** opens their location. Preferences and logs stay local and are not uploaded.

**Troubleshooting logging** is off by default and, when enabled, lasts only for the current session. Logs record limited technical events such as timestamps, build/source identifiers, exception types, and error codes. They exclude typed keys, screen/document content, window titles, and raw error-message text. At most two files of up to 1 MB are kept. Review a log before sharing it yourself.

**Strict Data mode**, available in setup and About, blocks diagnostic and crash logging. It keeps normal preference storage and does not erase old log files. There are no application diagnostic/crash logs unless logging is enabled.

## Requirements and limitations

- Windows 10 or Windows 11, **x64**.
- Local fixed-drive storage for the app and its user-data folder.
- The standalone release includes the runtime. Framework-dependent builds need the .NET 10 Desktop Runtime.
- Keyboard following depends on the target application's accessibility support. Elevated autostart can improve compatibility with administrator apps, but Windows' secure sign-in and UAC desktops are not supported.
- Games, custom controls, protected video, and screen-sharing or recording software may handle magnification differently. Check the result with the applications and displays you use.

QuickZoom enlarges screen content; it is not a screen reader or OCR tool, and it does not change the text size saved in your documents.

## Build from source

Use Windows with the .NET SDK required by [`global.json`](global.json) (10.0.401 baseline) and PowerShell 7.6 or newer for release validation.

```powershell
git clone https://github.com/BagerRyg/QuickZoom.git
cd QuickZoom
dotnet build .\QuickZoom.csproj -c Release
pwsh -NoProfile -File .\scripts\Test-Release.ps1
```

The release checks cover runtime, setup/startup, privacy, UI, and locale behaviour using isolated test files. They do not take screenshots or register real startup tasks. Add `-NoRestore` after restoring the projects, or `-IncludeNative` on an interactive Windows desktop for native magnifier/input-hook checks.

To create the next self-contained Windows x64 single-file build:

```powershell
.\build.bat
```

The script increments the internal build number, runs release validation, and publishes `Builds\Build N\QuickZoom.exe` only after checks pass. Optional signing uses `SIGN_CERT_THUMBPRINT` from the CurrentUser certificate store; see [`scripts/sign-exe.ps1`](scripts/sign-exe.ps1).

## Translations

The interface currently supports English (`en`), Danish (`da`), Swedish (`sv`), Norwegian (`no`), and Finnish (`fi`). Locale JSON files live in [`locales`](locales), are embedded in the app, and are also copied beside it when publishing.

To add a language, copy `en.json`, translate its values while preserving keys and format placeholders, and update its `$LanguageTag`, `$NativeName`, `$FormattingCulture`, and `$TextDirection` metadata. Add the language to `UiLanguage` in [`UiText.cs`](src/QuickZoom/UiText.cs) and the file-code mapping in [`LocalizationManager.cs`](src/QuickZoom/LocalizationManager.cs). Run the release checks to validate locale parity and UI behaviour.

## License

QuickZoom is licensed under the [GNU General Public License v3.0](LICENSE).
