# QuickZoom user manual

**For QuickZoom 3.3.54, build 355 · Windows 10/11, 64-bit**

[Læs manualen på dansk](QuickZoom-Brugervejledning-Dansk.md)

We built QuickZoom to make small text, buttons, and other screen details easier to see. It enlarges what is on your screen without changing the font size or layout saved in your documents. You can enlarge the whole screen, use a moving lens, or keep an enlarged view along a screen edge.

This manual describes the controls in build 355. An older download may look different. You can find your version under **Open Settings → About**.

## Contents

1. [Start here: zoom and return to normal](#1-start-here-zoom-and-return-to-normal)
2. [Get QuickZoom ready](#2-get-quickzoom-ready)
3. [First setup, step by step](#3-first-setup-step-by-step)
4. [Find and use the tray menu](#4-find-and-use-the-tray-menu)
5. [Choose how the screen is enlarged](#5-choose-how-the-screen-is-enlarged)
6. [Choose what the view follows](#6-choose-what-the-view-follows)
7. [All everyday shortcuts](#7-all-everyday-shortcuts)
8. [Use the Settings window](#8-use-the-settings-window)
9. [General settings](#9-general-settings)
10. [Zoom settings](#10-zoom-settings)
11. [Display settings](#11-display-settings)
12. [Mouse settings](#12-mouse-settings)
13. [Shortcut settings](#13-shortcut-settings)
14. [Appearance settings](#14-appearance-settings)
15. [About, startup, and privacy](#15-about-startup-and-privacy)
16. [Examples for everyday use](#16-examples-for-everyday-use)
17. [Troubleshooting](#17-troubleshooting)
18. [Save, back up, reset, update, or remove QuickZoom](#18-save-back-up-reset-update-or-remove-quickzoom)

## 1. Start here: zoom and return to normal

These instructions assume you have completed setup and kept the default **Alt** activation key. If you chose another key, use that key wherever this manual says Alt. On a Danish keyboard, use the left Alt key, not AltGr.

### Make something bigger

1. Move your mouse pointer to the text or object you want to see.
2. Hold **Alt** and scroll the mouse wheel upwards. Alternatively, hold **Alt** and press the keyboard's **+** key.
3. Release Alt when the view is large enough. The enlargement stays in place; you do not have to keep holding the key.
4. Move the mouse to explore the enlarged view. With the default **Automatic** following mode, the view can also follow typing and keyboard navigation.

**100%** means normal size. **200%** means twice the normal size. The default zoom step adds 30 percentage points: 100%, 130%, 160%, and so on, up to the configured maximum.

If nothing changes, open QuickZoom's tray menu and check that **Magnification** is **On**. Turning that switch on makes zoom controls available; you still need to zoom in.

### Return to normal

- Hold **Alt** and scroll down, or press **Alt + −**, until you reach 100%.
- You can also open the tray menu and turn **Magnification** **Off**. This immediately returns the zoom level to 100%.
- If colours still look reversed, turn **Invert colours** **Off** in the tray menu too.
- To stop QuickZoom completely, choose **Quit**, then select **Are you sure?** in the same menu.

There is no dedicated global “reset zoom” or emergency-exit shortcut in this build. **Escape** normally closes QuickZoom menus or Settings; it does not cancel screen magnification. Closing Settings also leaves QuickZoom running.

## 2. Get QuickZoom ready

### What you need

- A PC running **Windows 10 or Windows 11, 64-bit (x64)**.
- A keyboard; a mouse with a scroll wheel is useful but optional for zooming.
- A folder on the PC's **local fixed drive**, such as a normal folder on C:.
- Permission to save preferences in your Windows user's local application-data folder.

The self-contained release includes its runtime, so you do not need to install .NET separately. A non-self-contained developer build requires the .NET 10 Desktop Runtime.

QuickZoom is a screen magnifier. It does not read text aloud, perform OCR, or change the actual text size inside your files. No account is needed for normal use.

### Download and launch

1. Get the Windows x64 application download from the project's [GitHub releases page](https://github.com/BagerRyg/QuickZoom/releases). Choose the ready-to-run app, not GitHub's **Source code** archives.
2. If the download is a ZIP file, right-click it and choose **Extract All** to unpack it to a local folder before running it. Keep any accompanying files together.
3. Open **QuickZoom.exe**.
4. Complete first setup below. Afterward, QuickZoom lives in the notification area near the Windows clock.

“Portable” means you can use QuickZoom without configuring its optional installation and automatic startup. It still saves preferences on this PC. This build does **not** run from network drives, removable drives, or redirected/link-based storage paths. If you downloaded it onto a USB drive or a network share, copy it to a normal local folder first. Workplace restrictions on local storage may require help from IT.

## 3. First setup, step by step

New users normally start with **Large setup** on, which makes the setup window and controls bigger. Turn the **Large setup** switch off for the standard, smaller setup window. This choice changes setup itself; to enlarge the everyday QuickZoom interface, use **Appearance → UI font size** later.

Select **Next** to continue and **Back** to revisit an earlier step. There are seven steps:

| Step | What to choose and why |
| --- | --- |
| **1. Choose your language** | Choose English, Dansk, Svenska, Norsk, or Suomi. QuickZoom initially uses a supported Windows interface language, or English if it cannot match one. |
| **2. Choose a theme** | **System** follows Windows. **Dark** uses a dark interface; **Light** uses a light interface. This changes QuickZoom's controls, not the colours of your documents. |
| **3. Choose your activation key** | Keep **Alt** for the easiest start. To change it, click the key button and press one key. You will hold this key while using QuickZoom shortcuts. Read any conflict warning before continuing. |
| **4. How to use QuickZoom** | Try holding your activation key while scrolling or pressing +/−. The page also teaches colour inversion and switching zoom mode. These are live practice actions when available. |
| **5. Choose your data mode** | **Standard data mode (default)** saves preferences locally and leaves troubleshooting logs off. **Strict Data mode** also blocks diagnostic and crash logging, even if logging is requested later. Neither mode uploads your data. |
| **6. Set up automatic startup** | Optional. Choose **Approve** to arrange for QuickZoom to start when you sign in to Windows, or **Skip** to start it yourself when needed. |
| **7. Setup complete** | Select **Finish**. Setup closes and QuickZoom keeps running in the notification area. |

**A detail about practice:** colour inversion is enabled for the practice session. In ordinary use, its default is off. After setup, turn on **Invert colours** in the tray menu before trying Alt+I or Alt+middle-click.

Practice magnification is cleared when you leave that step; it does not set your starting zoom level.

### If you choose automatic startup

Windows asks for administrator permission through its User Account Control (UAC) prompt. The setup screen explains that this unsigned build may appear as **Unknown publisher**. Approve only the QuickZoom copy you intended to run. If you cannot approve an administrator prompt, choose **Skip** or ask your IT administrator.

QuickZoom copies itself to its managed folder under `%ProgramFiles%\QuickZoom` and creates a Windows scheduled task. This also lets shortcuts work with many apps running as administrator. It does not provide magnification on Windows' secure sign-in or UAC screens.

Wait until setup says **Automatic startup is ready and verified**. If approval is declined or verification fails, use **Retry** or **Skip**. You can configure startup later at **Open Settings → About → Set up autostart**.

If setup is still preparing QuickZoom, wait for it to finish. If **Retry** appears because QuickZoom is not ready, select it. The completion page appears only after the app has prepared its tray icon and shortcuts.

## 4. Find and use the tray menu

The **notification area**, also called the **system tray**, is the part of the Windows taskbar near the clock. QuickZoom's icon looks like a magnifying glass. If you cannot see it, select the **up arrow** to show hidden icons.

Click the QuickZoom icon with either the left or right mouse button to open its menu. Clicking the icon again closes the menu. If space is limited, the tray menu reduces spacing and, if needed, text and icon size to fit the screen.

| Control | What it does |
| --- | --- |
| **Zoom mode: Fullscreen / Lens / Docked** | Changes the style of magnification. The selected button is highlighted. All modes use the same current zoom level. |
| **Magnification** | On allows the zoom shortcuts to enlarge the screen. Off returns zoom to 100%. Turning it on alone does not enlarge anything. Active colour inversion is controlled separately. |
| **Invert colours** | Enables the colour-inversion shortcuts. Turn it on, then use Alt+I or Alt+middle-click to reverse the colours. Turn the switch off to remove inversion and disable its shortcuts. |
| **Follow** | Opens choices for Automatic, Mouse only, or Keyboard and typing. A checkmark shows the selected mode. Selecting a mode also resumes following if it was paused. |
| **Pause following / Resume following** | Found in the Follow menu. Holds the view in place or lets it follow movement again. A **Paused** label reminds you when following is stopped. |
| **Magnified Displays** | Expands a quick monitor picker. Choose **Where Cursor Is Present**, **All Displays**, or individual displays. **Included** marks a display in your selection. This selection controls Fullscreen mode. |
| **Shortcut settings** | Opens Settings directly on the Shortcuts page. |
| **Open Settings** | Opens the full Settings window on General. |
| **Reset Cursor** | Reloads the Windows cursor scheme, then reapplies QuickZoom's cursor enhancement if enabled. Useful if the pointer looks wrong. It does not reset your other settings. |
| **About** | Opens version information, automatic-startup status, folder buttons, and privacy/logging controls. |
| **Quit** | Changes to **Are you sure?**. Select it again to close QuickZoom completely and remove its active effects. |

The footer also shows the **Startup Service** status. See [About, startup, and privacy](#15-about-startup-and-privacy) for what each status means.

## 5. Choose how the screen is enlarged

Choose a mode in the tray menu or under **Settings → Zoom → Mode**.

### Fullscreen

The chosen screen area is enlarged. Because larger content takes more room, you see a smaller part of your desktop at once. QuickZoom moves the visible area as you use the mouse or keyboard; this movement is called **panning**.

Use Fullscreen for reading or working at increased size for a longer time. You can choose which monitors are included under **Display**.

### Lens

A floating enlarged area appears around the point QuickZoom is following. The rest of the desktop remains normal size. With mouse following, moving the pointer moves the lens. With keyboard following, it can follow the typing position or focused control instead.

Use Lens to inspect a label, small button, icon, or short piece of text while keeping the surrounding desktop visible. Adjust **Lens size** and **Lens shape** under Zoom. The overlay is not a separate document window to drag or resize by its border.

### Docked

A magnified strip or tile sits along a screen edge. It shows the area around the mouse pointer or keyboard location being followed. The rest of the screen remains normal size.

Use Docked when you prefer a stable place to look for enlarged content. Choose its edge and size under Zoom. The tile covers part of the workspace. If the point being followed enters that area, QuickZoom can move the tile to the opposite edge to keep the target visible.

Lens and Docked use the current screen of the point being followed; the Fullscreen display-selection settings do not restrict them. At 100%, their overlays disappear unless colour inversion is active, even if **Disable Magnifier at 100%** is off.

## 6. Choose what the view follows

Open **Follow** in the tray menu, or **Settings → Mouse → Follow**. Following works in all three zoom modes.

| Mode | What happens | Useful when |
| --- | --- | --- |
| **Automatic (recommended)** | Follows typing and keyboard navigation. Deliberate mouse movement, clicking, or scrolling returns control to the pointer. Small accidental mouse movements are less likely to interrupt typing. | You switch between mouse work, typing, and Tab/arrow-key navigation. |
| **Mouse only** | Follows the mouse pointer. Moving between controls with the keyboard does not make the magnified view follow them. | You prefer to position the view yourself with the mouse. |
| **Keyboard and typing** | Follows the text insertion point or the control selected through keyboard navigation. Mouse movement does not take over following. | You mostly type or move through controls using the keyboard. |

The **text insertion point**, also called the **caret**, is the blinking mark showing where your next typed character will appear. **Keyboard focus** means the button, box, or other control that will receive your next keyboard action.

QuickZoom generally keeps the view steady while you read or type within the visible area. It moves as the typing position approaches the edge. It depends on the other app supplying usable position information; some apps, games, remote sessions, and custom controls may not do so. If following does not work in one app, move the mouse in Automatic mode or select Mouse only.

### Keep a passage still while reading

1. Bring the passage into view.
2. Press **Alt+F**, or select **Follow → Pause following** in the tray.
3. Read without the view following mouse or keyboard movement.
4. Press Alt+F again, choose **Resume following**, or select a Follow mode to resume.

In Settings, **Pause following = On** means following is paused. It does not mean magnification is turned off. Your selected Follow mode is kept, and the pause setting is saved. Pausing only stops the view from following movement; your app, typing, and video continue normally.

## 7. All everyday shortcuts

**Hold the activation key first, perform the action, then release it.** The defaults below use Alt. The + and − keys on a numeric keypad also work. On some keyboard layouts, the main + key is shared with another symbol.

| Action | Mouse | Keyboard |
| --- | --- | --- |
| Zoom in | Alt + scroll up | Alt + + |
| Zoom out | Alt + scroll down | Alt + − |
| Toggle inverted colours | Alt + press the mouse wheel/middle button | Alt + I |
| Switch zoom mode | Alt + press left and right mouse buttons together | Alt + Z |
| Pause or resume following | Use the tray's Follow menu | Alt + F |

- Colour-inversion shortcuts require **Invert colours = On** in the tray first.
- Mode switching cycles **Fullscreen → Docked → Lens → Fullscreen**. Z is fixed in this build; there is no separate setting to reassign it.
- A mouse's middle click means pressing the wheel down, not scrolling it. If that is difficult, use Alt+I.
- Release Alt before normal clicking. While you hold the activation key, QuickZoom can capture left/right clicks for the two-button mode-switch gesture.
- **Shortcut mode** can disable either mouse or keyboard actions. “Mouse only” still requires holding the activation key on the keyboard.
- Turning **Magnification** off disables zooming; it is not a master switch for every QuickZoom shortcut. Follow and mode-switch shortcuts can still work, and inversion is separate.

If a QuickZoom shortcut conflicts with another app, change its configurable key under **Shortcuts**. For example, Alt+F is also used for File menus in many apps. QuickZoom can capture its action even when **Give QuickZoom shortcuts priority** is off.

## 8. Use the Settings window

Open the tray menu and select **Open Settings**. The pages, in sidebar order, are **General, Zoom, Display, Mouse, Shortcuts, Appearance, About**.

### Find a setting

- Click a page name in the sidebar.
- At narrow window sizes, the sidebar may show icons. Use the menu button marked with three horizontal lines to **show or hide navigation labels**.
- Click **Search settings**, or press **Ctrl+F**, and type a word such as `cursor`, `language`, or `zoom`.
- Click a search result, or use Up/Down and Enter. QuickZoom opens the matching page and brings the setting into view where possible.
- Lens and Docked options only appear when that mode is selected. Searching for “lens size” does not automatically switch your zoom mode.

Search and number boxes require typing; they do not support copy, cut, paste, or drag-and-drop in this build.

### Change a setting

| Kind of control | How to use it |
| --- | --- |
| **On/Off switch** | Click to change the state. On means enabled, except that **Pause following** deliberately enables a pause. |
| **Drop-down list** | Open it and choose one item. |
| **Slider and number** | Drag the slider, or click its displayed number and type an exact whole number within the allowed range. Press Enter or leave the box to apply it. Escape cancels an unfinished numeric edit. |
| **Colour palette** | Click a colour square. The selected colour is marked and shown in the cursor preview. |
| **Shortcut key button** | Click, then press the single key you want. See the key-capture caution under Shortcuts. |

Use **Tab/Shift+Tab** to move between controls. Space or Enter activates buttons and switches; arrow keys operate lists and sliders. Scroll down if a page does not fit.

Changes normally take effect immediately and are saved automatically. There is no separate Apply button and no Cancel-all button. **Done** or the window's **X** closes Settings while leaving QuickZoom running. Escape first clears an active search or cancels certain edits; otherwise it closes Settings. It does not undo settings already applied.

## 9. General settings

| Setting | Default | What it means |
| --- | --- | --- |
| **Smooth Zoom** | On | Animates the change between zoom levels instead of jumping directly. Turn it off if you prefer an immediate change or find the animation uncomfortable. |
| **Disable Magnifier at 100%** | On | Stops active magnification when you return to normal size, unless an active colour-inversion effect still needs it. It does not quit QuickZoom or prevent you from zooming in again. |
| **Center Cursor** | Off | In Fullscreen, frames the followed point closer to the middle of the enlarged view. This can make the screen move more as you move the mouse. Screen edges limit how far it can centre. It does not move the actual mouse pointer. |

## 10. Zoom settings

### Mode and its extra controls

**Mode** selects Fullscreen, Lens, or Docked. Fullscreen is the default. Only the controls relevant to your chosen mode are shown.

| Setting | Default and range | What it means |
| --- | --- | --- |
| **Lens size** | 360 px; 100–1400 px | Sets the width of the lens. A larger lens shows more at once but covers more of the normal desktop. “px” means screen pixels. |
| **Lens shape** | Rectangle; also Square or Round | Rectangle is wide, with a 16:9 width-to-height ratio. Square and Round have equal width and height. Shape changes the visible outline, not the zoom level. |
| **Dock position** | Top; also Bottom, Left, Right | Chooses the preferred edge for the docked tile. It may move to the opposite edge when the followed point would be underneath it. |
| **Tile size** | 25%; 10–50% | Sets tile height for Top/Bottom, or tile width for Left/Right, as a share of the screen. It controls occupied space, not magnification strength. |

The lens and tile fit within the available screen. Their actual dimensions can be limited by your display. There is no separate lens-height control or separate zoom level for each mode.

### Range and speed

| Setting | Default and choices | What it means |
| --- | --- | --- |
| **Zoom step (%)** | 30; 1–200 | How many percentage points one wheel notch or zoom-key press adds/removes. Try 10 for finer adjustment. Higher values reach strong zoom faster. |
| **Max zoom (%)** | 400; 150–750 | The highest zoom allowed. 400% is four times normal size. This is a limit, not your current zoom level. Lowering it can reduce a currently higher zoom. The minimum working zoom is always 100%. |
| **Refresh rate** | 120 Hz; 60, 90, 120, 180, 240 Hz, or Unlimited | How often QuickZoom aims to update movement and animation. Higher values can look smoother but use more computer power. Try 60 Hz if the PC feels busy or movement is uneven. |

**Unlimited** uses the highest refresh rate detected among connected monitors, with a minimum target of 60 Hz. It does not mean an infinite update rate or guarantee that your PC will reach the target. Hz means updates per second.

## 11. Display settings

These settings control **Fullscreen** magnification. They are most useful when your PC has more than one monitor.

| Setting | Default | What it means |
| --- | --- | --- |
| **Auto-switch monitor** | On | Lets the active fullscreen view move between monitors as the followed point moves. Turning it off locks the active monitor; it matters especially with **Where Cursor Is Present**. It does not remove monitors from an All Displays selection. |
| **Magnified displays: All Displays** | Selected | Includes all connected monitors in fullscreen magnification. |
| **Magnified displays: Where Cursor Is Present** | — | Uses one active monitor rather than all of them. Normally this is the monitor with the pointer; with keyboard following, the active typing/focus position can determine it instead. Keep Auto-switch monitor on if you want that view to move between monitors. |
| **Magnified displays: Custom Selection** | — | Shows a switch for each monitor so you can choose which ones are included. At least one must remain selected. |
| **Identify displays** | Action button | Briefly shows a matching label on each monitor, such as Primary, Secondary, or Monitor 3. The labels disappear after about three seconds; Escape closes them sooner. |

Use **Identify displays** to match QuickZoom's names to your physical screens. The labels **Primary** and **Secondary** do not necessarily match which monitor Windows currently treats as its main display. QuickZoom's list chooses what to magnify; it does not change Windows' main monitor, resolution, or display arrangement.

You can also change display selection quickly from **Magnified Displays** in the tray. Choosing individual displays switches to a custom selection. After plugging in, unplugging, or rearranging monitors, check your selection again. QuickZoom refreshes its display list and chooses a valid selection if the old one is no longer available.

## 12. Mouse settings

This page includes keyboard-following controls as well as pointer appearance.

| Setting | Default | What it means |
| --- | --- | --- |
| **Follow** | Automatic (recommended) | Chooses Automatic, Mouse only, or Keyboard and typing. See [following](#6-choose-what-the-view-follows). Selecting a mode resumes following. |
| **Pause following** | Off | On holds the view still. Turn it off to resume the selected Follow mode. |
| **Locate Cursor on Wiggle** | On | Quickly move the mouse back and forth to show a temporary highlight around the pointer. No shortcut key is required. It does not change your zoom level. |
| **Cursor enhancement** | Off | Applies QuickZoom's larger/recoloured standard Windows pointers while the app runs. Turn it off to return to the normal Windows cursor scheme. Some apps draw their own pointer and may look different. |
| **Preview** | Display only | Shows your proposed pointer size, fill, and outline. You can experiment while Cursor enhancement is off. |
| **Cursor size** | 100%; 100–500% | Scales QuickZoom's enhanced pointer. 200% is twice its base size. This does not change screen magnification. |
| **Cursor colour** | White | The colour inside the enhanced pointer. Choose from the colour squares. |
| **Border colour** | Black | The pointer's outline. A contrasting border makes it easier to see on both light and dark backgrounds. |

The palettes offer 48 preset colours; there is no custom colour-code entry. A warning appears if the fill and border are too similar, but you can still keep those colours. Try a bright fill with a black outline if the pointer is hard to find.

Size and colour changes may take a moment to reach the actual pointer. They only affect it when **Cursor enhancement** is on. **Reset Cursor** in the tray refreshes the cursor but reapplies enhancement if it is still enabled. Quit restores the normal Windows cursor scheme.

## 13. Shortcut settings

| Setting | Default | What it means |
| --- | --- | --- |
| **Shortcut mode** | Both | **Both** enables mouse and keyboard actions. **Keyboard only** disables QuickZoom's mouse-wheel, middle-click, and two-button actions. **Mouse only** disables its keyboard action combinations, but still uses the keyboard activation key for mouse actions. |
| **Activation key** | Alt | The single key you hold before performing a QuickZoom action. Changing it changes the first key of all the combinations in this manual. |
| **Invert colours key** | I | The second key in the keyboard inversion shortcut: Alt+I by default. Middle-click remains the mouse alternative. The tray's Invert colours switch must be on. |
| **Pause/resume following key** | F | The second key in the pause/resume shortcut: Alt+F by default. Works in all three zoom modes. |
| **Give QuickZoom shortcuts priority** | Off | Helps prevent keys used for a QuickZoom action from also triggering actions or menus in another app. Useful if zooming opens an app menu. It is a general shortcut setting, not an Office-only option. |

Recognised QuickZoom action keys can still be captured with priority off. This switch does not resolve every shortcut conflict. Choose different keys or change Shortcut mode when another app needs the same combination. AltGr typing and unrelated Windows shortcuts are intended to remain available.

### Change a key safely

1. Click the current key button beside the setting.
2. Press **one key**, not the whole combination. For example, choose F for the second key; do not type Alt+F into that dialog.
3. The dialog closes and the key is applied immediately.
4. Check for a warning or red conflict message, then try the action.

**To cancel key capture, click the dialog's X before pressing a key. Do not press Escape: in this build, Escape can become the selected shortcut key.**

Keep the three configurable keys different. Avoid Z as a configurable key because it is also the fixed mode-switch action; avoid +/− as the activation key because they are zoom actions. A warning or conflict message in Settings does not necessarily stop a problematic choice from being saved. Choose another key if a warning appears or an action stops working.

Alt is a practical starting point. Ordinary letter keys can interfere with typing; Caps Lock changes capital-letter mode; Windows keys may conflict with Windows; Fn depends on your keyboard. AltGr is not a substitute for Alt. You can return the defaults individually by selecting Alt, I, and F.

## 14. Appearance settings

| Setting | Choices | What it means |
| --- | --- | --- |
| **Theme** | Follow Windows (default), Dark, Light | Changes QuickZoom's own colours. Follow Windows adapts to the Windows theme. It does not invert your screen. |
| **Language** | English, Dansk, Svenska, Norsk, Suomi | Changes QuickZoom's menus, setup text, Settings, and messages. It does not change Windows' language or your documents. |
| **UI font size** | Follow Windows (default), Large, Extra large | Makes QuickZoom's own interface text easier to read. Large and Extra large add size on top of Windows' Text Size preference. This is separate from desktop zoom and cursor size. |

Changes apply immediately and may rebuild the Settings window. If controls no longer fit, enlarge the window or scroll. QuickZoom also responds to Windows accessibility preferences such as high-contrast colours and reduced interface animations; these are Windows settings, not extra QuickZoom switches.

## 15. About, startup, and privacy

### Build and startup

**Build and startup** shows your QuickZoom version/build and automatic-startup status. “Startup Service” in the status text refers to QuickZoom's scheduled startup task.

| Status | Meaning and next step |
| --- | --- |
| **Configured** | Automatic startup is ready. QuickZoom should start when you sign in to Windows. |
| **Not configured** | Start QuickZoom yourself, or select **Set up autostart**. |
| **Needs repair** | The saved startup configuration needs attention. Select **Set up autostart** and follow the approval/verification steps. |
| **Status unavailable** | QuickZoom could not establish the startup state. Check again later; if necessary, use Set up autostart or ask IT for help. |

The **Set up autostart** button appears when startup is not ready. It opens the startup part of setup. There is no in-app switch to turn an already configured startup task off; see [removal and startup management](#18-save-back-up-reset-update-or-remove-quickzoom).

### Locations

- **Open Install Folder** opens the managed installation location in File Explorer. It is unavailable if QuickZoom has not been installed through startup setup. This is normal when using a downloaded copy directly.
- **Open Config Folder** opens the folder containing your preferences, normally `%LOCALAPPDATA%\QuickZoom`. File Explorer may select the settings file in that folder.

### Privacy and diagnostics

| Control | Default | What it does |
| --- | --- | --- |
| **Strict Data mode** | Off, unless chosen in setup | Blocks QuickZoom's diagnostic and crash logging. Turning it on stops active troubleshooting logging and disables its switch. Preferences still save, and existing log files remain. |
| **Troubleshooting logging** | Off | Records limited diagnostic events for the **current app session only**. It returns to off when QuickZoom restarts. Strict Data mode must be off to enable it. |
| **Local log files → Show log file** | Available when a log exists | Opens File Explorer with the local log selected. Open the file from there if you want to read it. |

Logs contain technical event details such as timestamps, build identifiers, event/source identifiers, exception types, and error codes. They do not record typed keys, document/screen content, window titles, or raw error-message text. Following uses position information supplied by Windows/apps; it does not need to read your typed text.

QuickZoom keeps at most two log files of up to 1 MB each, replacing older entries as needed. Logs remain local and are not uploaded. There are no application diagnostic/crash logs unless you enable troubleshooting logging. Strict Data mode does not delete old logs or prevent normal local preference storage.

To collect information for a problem: turn logging on, reproduce the problem, use Show log file, then turn logging off. Review a file before deciding to share it yourself. If logging cannot write to disk, it turns off and reports the problem. Check free space and permissions.

With logging enabled, About may also show **Theme engine**. This tells support which interface drawing method is active; it is information, not a setting you need to change.

## 16. Examples for everyday use

### Read a long email, web page, or document

Choose **Fullscreen** and **Automatic** following. Zoom in a few steps. Use your mouse, arrow keys, or normal scrolling to read. For a passage you want to study without movement, press Alt+F to pause following; press it again before moving on. Try a smaller Zoom step if one step feels too large.

### Inspect a small button without losing your place

Choose **Lens**, then **Mouse only** if you want complete pointer control. Zoom in and move over the button. A rectangular lens is useful for wide labels; Round or Square can suit icons. Zoom back out when finished.

### Type with an enlarged view

Use **Automatic**, or **Keyboard and typing** if you do not want mouse movement to move the view. Click a text field and start typing. QuickZoom follows the insertion point when the app exposes it. Try **Docked** if you prefer reading the enlarged text in a fixed strip. If it does not follow in that app, use Mouse only to position the view manually.

### Keep one monitor enlarged and another available for reference

Select **Fullscreen**, then open **Settings → Display → Magnified displays → Custom Selection** and include the monitor you want enlarged. Use **Identify displays** if you are unsure which is which. Alternatively, choose **Where Cursor Is Present** with Auto-switch monitor on to move the magnified view between screens.

### Find the pointer on a busy screen

Keep **Locate Cursor on Wiggle** on and quickly wiggle the mouse. For an easier-to-see pointer all the time, enable **Cursor enhancement**, increase Cursor size, and choose contrasting fill/border colours. Start with a moderate size so the pointer does not cover small buttons.

### Reduce glare on a bright page

Turn **Invert colours** on in the tray, then press **Alt+I**. Use Alt+I again to restore the original colours while keeping the shortcut enabled. To disable the feature completely, turn the tray switch off. Inversion reverses colours in the magnified view, including images; it does not change the document or turn an app into its own dark mode.

### Use a slower PC or prefer less movement

Try **Refresh rate: 60 Hz**, a lower zoom level, and **Smooth Zoom: Off**. Use Pause following when reading a fixed area. These are starting points to try, not required settings.

### Show details during a demonstration

Lens can help you point out a small interface detail while leaving the rest of the desktop visible. A larger enhanced cursor can also help. If you are sharing or recording your screen, first check the audience's actual view: magnifier overlays and effects may be captured differently by different sharing apps. Videos or protected content may also behave differently across apps and display drivers.

## 17. Troubleshooting

| Problem | What to try |
| --- | --- |
| **QuickZoom seems to disappear after setup.** | Look near the Windows clock and inside the hidden-icons up arrow. It normally runs in the tray, without a permanent main window. |
| **Magnification is On but the screen is normal size.** | Hold your activation key and zoom in. On enables the controls; it does not set a zoom level above 100%. |
| **Zoom shortcuts do nothing.** | Check that QuickZoom is running, Magnification is On, and Shortcut mode allows your chosen input. Check the Activation key. Use left Alt rather than AltGr with the defaults. |
| **I cannot zoom in any further.** | Check Max zoom under Zoom. You may already be at the limit. |
| **The view is too large or I have lost my place.** | Hold the activation key and repeatedly zoom out. Or turn Magnification Off in the tray; also turn Invert colours Off if necessary. Quit closes the app completely. |
| **The view stays still.** | Check for Paused beside Follow. Resume following or choose a Follow mode. In Keyboard and typing, mouse movement intentionally does not take over. |
| **Typing disappears outside the enlarged area.** | Choose Automatic or Keyboard and typing and resume following. If only one app is affected, its accessibility information may be unavailable; position the view with Mouse only instead. |
| **The lens or docked tile is gone.** | Zoom above 100%. Check Magnification is On. These overlays normally disappear at normal size. |
| **The docked tile changes edges.** | This is expected when the followed point would be underneath it. It uses the opposite edge to keep the target visible. |
| **The wrong monitor is enlarged.** | Check Display, identify the monitors, and review All Displays/Where Cursor Is Present/Custom Selection. Turn Auto-switch monitor on if you want the active view to move. These controls apply to Fullscreen. |
| **Inverted colours do not work.** | First turn Invert colours On in the tray, then use the shortcut. Check Shortcut mode and the configured inversion key. Practice during setup enables inversion separately. |
| **Turning Magnification Off did not restore normal colours.** | Turn Invert colours Off too. Zoom and inversion are separate controls. |
| **Alt opens another app's menu or a shortcut conflicts.** | Try Give QuickZoom shortcuts priority, or change the conflicting key. For Alt+F/File-menu conflicts, change the pause/resume key or choose Mouse only shortcuts if suitable. |
| **Mouse clicks act strangely while zooming.** | Release the activation key before normal clicking. Holding it enables the left+right-button mode gesture. |
| **A shortcut stopped working after I changed its key.** | Review the key buttons and warning messages. Choose distinct keys; restore Alt/I/F if unsure. If Escape was accidentally captured, select the desired key again. |
| **Zoom works in ordinary apps but not an administrator app.** | Configure automatic startup under About and approve the Windows prompt. Save your work, then sign out of Windows and sign back in so the configured task launches QuickZoom. Ask IT if you cannot approve setup. |
| **My cursor size/colours do not change.** | Enable Cursor enhancement; the preview works even when enhancement is off. Allow a moment for changes. Apps with their own pointer may not use the enhanced Windows pointer. |
| **The pointer looks wrong.** | Try Reset Cursor in the tray. To remove QuickZoom's enhancement, turn Cursor enhancement off. If needed, quit QuickZoom and reselect your cursor scheme in Windows. |
| **Settings text is too small.** | Choose Appearance → UI font size → Large or Extra large. Making setup large does not change everyday interface size. |
| **I cannot paste into search or a number box.** | Type the value instead; clipboard actions are disabled in these boxes. |
| **A typed value is rejected.** | Enter a whole number inside the displayed range. Do not type the % or px unit. |
| **Preferences could not be saved.** | Previously saved preferences are kept. Check free disk space and write permission for the local config folder, then change the setting again to retry. Do not assume the current visible choice will survive a restart. |
| **Preferences could not be loaded.** | QuickZoom leaves the saved file unchanged, uses temporary defaults, and blocks further automatic saves. Back up the original file, then restore a known-good backup or deliberately use Reset all settings to replace it with defaults. See the instructions below. |
| **Setup or startup approval fails.** | Retry after checking permissions, or Skip and launch manually. Later use About → Set up autostart. Declining the UAC prompt leaves the requested setup or update unapproved; an older startup installation may still exist. |
| **QuickZoom says it is already running.** | Use the dialog's Open Settings action or find its existing tray icon. Do not keep launching more copies. |
| **The app refuses its location.** | Move the downloaded app to a normal folder on a local fixed drive. Network, removable, and redirected storage paths are rejected. Ask IT if your user-data folder is redirected. |
| **Magnification fails, shows black areas, or acts oddly after sleep/display changes.** | Return to 100%, quit QuickZoom, and reopen it. Check the selected mode and displays. Avoid running another screen magnifier at the same time while troubleshooting. If needed, enable session logging and reproduce the problem. QuickZoom may disable magnification after an initialization failure to avoid leaving black overlays. |

For support, note your version/build, Windows version, number of monitors, zoom mode, Follow mode, and exact steps that cause the problem. Do not include document content or other personal information unless you choose to share it.

## 18. Save, back up, reset, update, or remove QuickZoom

### What is saved

Settings are saved automatically for your Windows user, normally in:

```text
%LOCALAPPDATA%\QuickZoom\settings.json
```

Open **About → Open Config Folder** to find it without typing the path. The same folder holds first-setup state in `first-run-setup.json` and, if enabled, local troubleshooting logs. Setup state remembers completion and whether automatic startup was skipped.

Preferences include your shortcut keys, modes, monitor selection, pointer appearance, following/pause choices, and privacy options. The current zoom percentage is not a saved starting zoom: a fresh launch starts at 100%. Troubleshooting logging also starts off each session. If colours were inverted when you quit, QuickZoom restores that effect next time, even at 100%.

### Back up and restore preferences

1. Open **About → Open Config Folder** and leave that File Explorer window open.
2. Quit QuickZoom using the tray's two-step Quit action.
3. In the folder you opened, copy `settings.json` to a backup folder. If you also want the first-setup choices/state, copy `first-run-setup.json`.
4. To restore, keep QuickZoom closed and copy your backed-up file(s) back to the same config folder, replacing the current copies only if that is your intention.
5. Start QuickZoom and check the settings. Recheck monitor selection if you are on another PC or have changed displays.

A settings backup does not install the app or recreate its Windows startup task. Configure automatic startup separately. There is no in-app import/export button.

### Reset all settings

Use this when you want to start over with QuickZoom's preferences, rather than merely zoom out.

1. Open Settings and click **Reset all settings** in the footer.
2. The button changes to **Reset all settings?**.
3. Click it again within ten seconds to confirm. Otherwise, the confirmation expires without resetting anything.

Reset returns zoom to 100%, removes active inversion, turns cursor enhancement off, and restores default settings, including shortcuts and language selection. Strict Data mode returns to off. It does not remove the installed app, erase existing log files, or disable automatic startup. The first-run wizard is not replayed by this button.

### Repeat the guided setup

If you want to revisit the setup wizard, quit QuickZoom first. Launch its executable with the `-setup` option. One way is to append a space and `-setup` to the **Target** of a Windows shortcut to QuickZoom, outside any quotation marks:

```text
"C:\YourQuickZoomFolder\QuickZoom.exe" -setup
```

Replace the example path with your actual QuickZoom location. This loads your existing choices so you can review them; it is not a reset to defaults. Remove `-setup` from that shortcut afterward if you want it to launch normally.

### Update QuickZoom

There is no in-app update-check button. Obtain the newer release from the project's releases page, quit the running QuickZoom, and extract all supplied files together into a normal local folder. Run the new QuickZoom.exe. Existing preferences are normally reused. If you use automatic startup, allow any requested startup/install update, then check **About** for the new build number and configured startup status. Follow the startup repair steps if needed.

Let setup update the managed installation. Do not replace individual files inside its `versions` folders yourself.

Keep your preferences backup until you have checked the new version. An installed copy and an older downloaded copy can coexist on disk; verify the version shown by the running app rather than relying on the folder name.

### Stop automatic startup

Closing QuickZoom stops it for now; it does not disable the next sign-in launch. Resetting preferences does not disable startup either.

This build has no in-app off switch for automatic startup. To disable it without deleting your preferences:

1. Open **Task Scheduler** from Windows Start. Administrator permission may be required.
2. Open **Task Scheduler Library** and find **QuickZoom Startup (Elevated)**.
3. Disable that task. You can enable it again later, or use QuickZoom's Set up autostart flow if repair is needed.

On a managed work PC, ask IT to make this change if you cannot edit the task. Do not disable unrelated Windows tasks.

If a later QuickZoom launch offers startup setup again, choose **Skip** to keep launching manually.

### Remove QuickZoom completely

1. Quit QuickZoom.
2. If you configured automatic startup, remove its **QuickZoom Startup (Elevated)** task in Task Scheduler so it will no longer try to launch.
3. Remove the QuickZoom program folder you downloaded. For the managed installation, remove `%ProgramFiles%\QuickZoom`; this normally needs administrator permission.
4. If you also want to erase preferences, setup state, and old logs, remove `%LOCALAPPDATA%\QuickZoom` after making any backup you want to keep.

There is no in-app uninstall command. The source package includes startup-task removal scripts for administrators; they are not needed for ordinary use. Removing program files alone does not erase preferences, and removing preferences alone does not remove the startup task.
