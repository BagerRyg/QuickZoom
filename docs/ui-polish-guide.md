# QuickZoom UI Polish Guide for Codex

This document is a Codex-friendly UI/UX implementation brief for improving the QuickZoom settings window and tray menu. It should be used as a visual and behavior baseline when editing the app.

## Goal

Make the QuickZoom UI look tighter, calmer, more professional, and less like a prototype or “vibe-coded” interface.

Target style:

- Windows 11 Fluent-inspired dark UI
- Apple Settings-style grouped settings layout
- Compact, readable, and polished
- Green accent only
- Rounded but not bubbly
- Clear grouped rows
- No huge empty tiles
- No stretched text
- No mixed accent colors
- No oversized controls

## Main Problems to Fix

1. Text appears too spaced out and artificial.
2. Rows/cards are too tall.
3. Too much empty space in the lower half of pages.
4. Each setting is currently a large standalone tile instead of part of a clean grouped panel.
5. Accent colors are inconsistent: green, purple, blue, and orange compete visually.
6. Buttons lack clear primary/secondary hierarchy.
7. Sliders look unfinished and values are inconsistently formatted.
8. Shortcut controls do not look like proper editable key combinations.
9. Color swatch grids are too dense and cheap-looking.
10. Tray menu is too large, too flat, and lacks separators.

---

# Typography

Use normal Windows-style typography. Avoid custom tracking or letter spacing.

Recommended font stack:

```text
Segoe UI Variable, Segoe UI, sans-serif
```

Recommended sizes:

```text
Main page title:      32 px, weight 600
Page subtitle:        14 px, weight 400
Section title:        16 px, weight 600
Row title:            15 px, weight 600
Row description:      13 px, weight 400
Sidebar item:         15 px, weight 500
Button text:          13 px, weight 500
Tray header:          20 px, weight 600
Tray section label:   11 px, uppercase, 0.4 px letter spacing
```

Rules:

- Do not add letter spacing to normal labels.
- Use sentence-style capitalization for settings.
- Avoid Title Case for every row title.
- Keep descriptions short.
- Use either American English or British English consistently. Recommended: use `color`, not `colour`, for English UI.

---

# Layout and Spacing

Recommended window/layout values:

```text
Window size:             980 x 680 or 1040 x 720
Sidebar width:           230–250 px
Content left padding:    28–32 px
Content max width:       760–840 px
Simple row height:       64–72 px
Long row height:         84–96 px
Card/panel radius:       10–12 px
Card horizontal padding: 18 px
Card vertical padding:   14 px
Gap between rows:        0 px inside grouped panels
Gap between sections:    20–24 px
```

Avoid rows that are 100–130 px tall unless the row contains a complex control such as a preview, color picker, or multi-line shortcut editor.

## Preferred Page Structure

Use this structure on all settings pages:

```text
Page title
Page subtitle

Section title
Grouped setting panel

Section title
Grouped setting panel

Footer buttons
```

Use grouped panels with subtle dividers instead of separate giant tiles.

Preferred pattern:

```text
Section title
┌──────────────────────────────────────────────┐
│ Setting title                         Control │
│ Short setting description                     │
├──────────────────────────────────────────────┤
│ Setting title                         Control │
│ Short setting description                     │
├──────────────────────────────────────────────┤
│ Setting title                         Control │
│ Short setting description                     │
└──────────────────────────────────────────────┘
```

Use separate cards only for:

- Live cursor preview
- Complex color picker
- Shortcut recording interface
- Monitor selector
- Warning/status messages

---

# Color Palette

Use one primary accent. Recommended accent: green.

```text
Window background:        #0B0D12
Sidebar background:       #0F131A
Panel/card background:    #151A23
Panel hover:              #1A202B
Panel border:             #242B38
Divider:                  #262D3A

Primary text:             #F2F5F8
Secondary text:           #AAB3C2
Muted text:               #778191

Accent green:             #22C55E
Accent green hover:       #2ED86F
Accent green pressed:     #16A34A

Danger/reset:             #EF4444
Focus ring:               #60A5FA
```

Rules:

- Do not mix green, purple, blue, and orange as competing accents.
- Replace the current purple sidebar active bar with green unless purple becomes the official brand accent.
- Use blue only for keyboard focus rings or system links.
- Use red only for destructive/reset warnings.
- Orange should only appear as a selected cursor preview color if the user chose orange.

---

# Sidebar

The sidebar should feel compact and aligned.

Recommended values:

```text
Sidebar width:       230–250 px
Item height:         44–48 px
Item radius:         8 px
Icon size:           20 px
Icon stroke:         1.75–2 px
Horizontal padding:  14–16 px
Gap between items:   4–6 px
```

Active item:

```text
Background: #1A202B
Left bar:   #22C55E
Text:       #FFFFFF
Icon:       #FFFFFF
Radius:     8 px
```

Inactive item:

```text
Text:       #AAB3C2
Icon:       #AAB3C2
Hover bg:   #151A23
```

Rules:

- Use one icon family only.
- Use same stroke width for every icon.
- Align all icons to a 20x20 visual box.
- Do not mix purple active indicator with green toggles.
- Keep labels short.

Recommended sidebar icons:

```text
General:     sliders/settings
Display:     monitor
Appearance:  palette
Cursor:      mouse pointer
Zoom:        magnifier-plus/search-plus
Shortcuts:   keyboard
About:       circle-help
Quit:        power or log-out
```

---

# Buttons

Bottom buttons currently lack hierarchy. Use a clear primary action.

Recommended footer layout:

```text
[Reset page]                                  [Done]
```

Alternative if cancellation is needed:

```text
[Reset page]                         [Cancel] [Done]
```

Primary `Done` button:

```text
Background: #22C55E
Hover:      #2ED86F
Pressed:    #16A34A
Text:       #06100A
Height:     34–36 px
Radius:     8 px
Padding:    16–18 px horizontal
```

Secondary reset button:

```text
Background: transparent or #151A23
Border:     #303847
Text:       #C9D1DA
Hover bg:   #1A202B
Height:     34–36 px
Radius:     8 px
Padding:    16–18 px horizontal
```

Naming rules:

- Use `Reset page` if it resets only the current page.
- Use `Reset all settings` only if it resets everything.
- Avoid vague `Reset to defaults` unless the scope is obvious.
- Use `Reset Page`, `Cancel`, `Done` or sentence-style equivalents consistently.

---

# Toggles

Recommended toggle values:

```text
Width:       46–50 px
Height:      26 px
Knob size:   22 px
Off track:   #252B36
Off hover:   #303847
Off knob:    #D7DEE8
On track:    #22C55E
On hover:    #2ED86F
On pressed:  #16A34A
On knob:     #FFFFFF
```

Rules:

- Align toggle vertically with the row center.
- Toggle rows in the tray menu must not close the tray menu.
- Use disabled styling where a setting is unavailable because another setting makes it irrelevant.

---

# Sliders

Current sliders look loose and unfinished. Tighten them.

Recommended slider layout:

```text
Zoom step          [────●────────]   [35%]
Maximum zoom       [────────●────]   [450%]
Frame rate limit   [Dropdown: Auto]
```

Recommended slider styling:

```text
Track height:    4 px
Filled track:    #22C55E
Empty track:     #252B36
Thumb size:      16 px circle
Thumb border:    2 px #0B0D12
Value display:   compact pill or fixed-width text
```

Rules:

- Always show units: `35%`, `450%`, `120 FPS`.
- Do not show `450` without `%`.
- Do not show `360` without `FPS`.
- Use fixed-width value area so rows align.
- Avoid a free slider for refresh rate.

Refresh rate should be a dropdown, not a slider.

Recommended values:

```text
Auto
60 FPS
90 FPS
120 FPS
144 FPS
165 FPS
240 FPS
Unlimited / Experimental
```

Default: `Auto`.

---

# Dropdowns

Dropdowns need a stronger field style.

Recommended style:

```text
Width:       220–260 px
Height:      36 px
Background:  #111722
Border:      #2A3240
Hover border:#3A4658
Radius:      8 px
Text:        #F2F5F8
Chevron:     #AAB3C2
```

Rules:

- Do not let dropdowns look like plain text floating on the card.
- Use consistent width per page.
- Keep dropdown values short.

---

# Hotkey and Shortcut Controls

Shortcut rows should show actual combinations, not isolated keys.

Use keycap pills:

```text
[Alt] + [Mouse Wheel]
[Alt] + [+ / -]
[Alt] + [I]
[Alt] + [F]
[Middle Mouse Button]
```

Recommended keycap style:

```text
Background: #111722
Border:     #2A3240
Text:       #F2F5F8
Radius:     6 px
Height:     28–30 px
Padding:    8–10 px horizontal
```

Editing behavior:

- Hotkey fields should look clickable/editable.
- Provide a `Change` button or allow clicking the keycap area.
- When recording, show `Press shortcut...`.
- Detect and warn about conflicts.
- Show a short warning for common conflicts such as `Alt + F` being used by app menus in many programs.

Example conflict warning:

```text
Alt + F is commonly used by application menus.
```

---

# Color Picker

The cursor color swatch grid is currently too dense.

Recommended improvements:

- Use 8–12 clean presets by default.
- Add a `Custom…` button.
- Use larger swatches: 18–20 px.
- Use 6 px spacing between swatches.
- Add a clear selected ring.
- Separate fill color and outline color clearly.

Preferred layout:

```text
Cursor fill      ○ ○ ○ ○ ○ ○ ○ ○   [Custom…]
Cursor outline   ○ ○ ○ ○ ○ ○ ○ ○   [Custom…]
```

Selected swatch:

```text
Outer ring: #F2F5F8 or #22C55E
Inner border: #0B0D12
```

Rules:

- Do not show an overwhelming grid of tiny dots.
- Do not omit selected state.
- Use tooltips for swatch names only if needed.

---

# Cursor Preview

The cursor preview is a good feature, but should look like a dedicated live preview area.

Preferred structure:

```text
Live preview
┌────────────────────────────────────┐
│  Pointer      Text cursor      Hand │
└────────────────────────────────────┘
```

Recommended styling:

```text
Preview background: #111722
Preview border:     #242B38
Radius:             10–12 px
Icon size:          large enough to judge fill and outline
Alignment:          centered horizontally and vertically
```

Rules:

- Preview icons must be aligned.
- Preview should show selected fill, outline, and size.
- Optional: add dark/light background toggle for preview testing.

---

# Tooltips

Do not add tooltips everywhere. Use them only for settings that are technical or confusing.

Good tooltip candidates:

- Refresh rate
- Turn off at 100%
- Follow cursor between monitors
- Magnified displays
- Enhanced cursor
- Shortcut mode

Tooltip styling:

```text
Background: #111722
Border:     #2A3240
Text:       #DDE3EA
Max width:  280 px
Delay:      400 ms
Radius:     8 px
Padding:    8–10 px
```

Rules:

- Use a small `?` or `i` icon only where needed.
- Do not place help icons next to every row title.
- Keep tooltip text short.

---

# Tray Menu

The tray menu currently looks too large and flat. It should be tighter and match the settings window.

Recommended values:

```text
Width:              320–360 px
Padding:            14 px
Corner radius:      12 px
Background:         #0F131A
Border:             #242B38
Shadow:             strong but soft
Header:             20 px semibold
Section label:      11 px uppercase, 0.4 px letter spacing
Row height:         44–48 px
Icon size:          18–20 px
Toggle size:        44 x 24 px
```

Preferred structure:

```text
QuickZoom

Quick controls
Enabled                         [toggle]
Inverted colors                 [toggle]
Follow cursor                   [toggle]

Actions
Magnified displays              >
Keyboard shortcuts              >
Settings                        >
Reset cursor

About
Quit
```

Rules:

- Add separators between groups.
- Toggle rows must not close the tray menu.
- Navigation/action rows may close the tray menu.
- Use consistent icon size.
- The Quit icon should not be a huge harsh X.
- Keep text size smaller than current.
- Match the same dark palette as the settings window.

---

# Page-by-Page Improvements

## General Page

Current labels to replace:

```text
Smooth Zoom                  -> Smooth zoom
Disable Magnifier at 100%    -> Turn off at 100%
Center Cursor                -> Keep cursor centered
```

Recommended descriptions:

```text
Smooth zoom
Blends between zoom levels for softer transitions.

Turn off at 100%
Stops magnification when zoom returns to normal.

Keep cursor centered
Keeps the focus point near the center while zooming.
```

Possible extra General settings:

```text
Start with Windows
Show tray icon
Pause in full-screen apps
```

Use this layout:

```text
General
Core magnification behavior.

Behavior
┌────────────────────────────────────────────────────┐
│ Smooth zoom                                 Toggle │
│ Blends between zoom levels for softer transitions. │
├────────────────────────────────────────────────────┤
│ Turn off at 100%                            Toggle │
│ Stops magnification when zoom returns to normal.   │
├────────────────────────────────────────────────────┤
│ Keep cursor centered                        Toggle │
│ Keeps the focus point near the center while zooming.│
└────────────────────────────────────────────────────┘
```

## Display Page

Current page feels underdeveloped. Make monitor behavior clearer.

Recommended labels:

```text
Auto-switch monitor        -> Follow cursor between monitors
Magnified displays         -> Magnify
```

Recommended descriptions:

```text
Follow cursor between monitors
Automatically switches to the monitor under the cursor.

Magnify
Choose which displays QuickZoom should zoom.
```

Recommended dropdown values:

```text
All displays
Current monitor
Selected monitor
```

Recommended structure:

```text
Display
Monitor and screen behavior.

Monitor
┌────────────────────────────────────────────────────┐
│ Follow cursor between monitors              Toggle │
│ Automatically switches to the monitor under the cursor.│
└────────────────────────────────────────────────────┘

Magnification
┌────────────────────────────────────────────────────┐
│ Magnify                              [All displays] │
│ Choose which displays QuickZoom should zoom.        │
└────────────────────────────────────────────────────┘
```

Behavior rule:

- If `All displays` is selected, `Follow cursor between monitors` may be irrelevant. Hide or disable it when it does not apply.

## Appearance Page

Current page is mostly good but should be tighter.

Recommended labels:

```text
Theme mode       -> Theme
Language         -> Language
UI font size     -> Text size
```

Recommended descriptions:

```text
Theme
Follow Windows, always light, or always dark.

Language
Changes text in the tray menu, settings window, and dialogs.

Text size
Changes the size of text in QuickZoom.
```

Recommended text size values:

```text
Small
Default
Large
Extra large
```

Possible extra Appearance setting:

```text
Accent color
```

If accent color is added, use it globally for active sidebar state, toggles, sliders, and primary buttons.

## Cursor Page

Group the page into assistance and enhanced cursor settings.

Recommended labels:

```text
Locate Cursor on Wiggle  -> Highlight cursor on wiggle
Cursor enhancement       -> Enhanced cursor
Cursor size              -> Cursor size
Cursor colour            -> Cursor fill
Border colour            -> Cursor outline
Preview                  -> Live preview
```

Recommended descriptions:

```text
Highlight cursor on wiggle
Quickly wiggle the mouse to highlight the cursor.

Enhanced cursor
Uses a larger high-contrast cursor while QuickZoom is running.

Cursor size
Adjusts the enhanced cursor size.

Cursor fill
Sets the main color of the enhanced cursor.

Cursor outline
Sets the outline color of the enhanced cursor.
```

Recommended structure:

```text
Cursor
Cursor size and colors.

Cursor assistance
┌────────────────────────────────────────────────────┐
│ Highlight cursor on wiggle                  Toggle │
│ Quickly wiggle the mouse to highlight the cursor.  │
├────────────────────────────────────────────────────┤
│ Enhanced cursor                             Toggle │
│ Uses a larger high-contrast cursor while QuickZoom is running.│
└────────────────────────────────────────────────────┘

Enhanced cursor
┌────────────────────────────────────────────────────┐
│ Cursor size                       [slider] [180%]  │
│ Adjusts the enhanced cursor size.                  │
├────────────────────────────────────────────────────┤
│ Cursor fill                       swatches/custom  │
│ Sets the main color of the enhanced cursor.        │
├────────────────────────────────────────────────────┤
│ Cursor outline                    swatches/custom  │
│ Sets the outline color of the enhanced cursor.     │
└────────────────────────────────────────────────────┘

Live preview
┌────────────────────────────────────────────────────┐
│ Pointer             Text cursor              Hand  │
└────────────────────────────────────────────────────┘
```

## Zoom Page

The Zoom page is too empty and the refresh-rate slider should be replaced.

Recommended labels:

```text
Zoom step (%)   -> Zoom step
Max zoom (%)    -> Maximum zoom
Refresh rate    -> Frame rate limit
```

Recommended descriptions:

```text
Zoom step
Sets how much each zoom step changes.

Maximum zoom
Sets the highest zoom level QuickZoom can reach.

Frame rate limit
Limits how often the magnified view updates.
```

Recommended values:

```text
Zoom step:       35%
Maximum zoom:    450%
Frame rate:      Auto / 120 FPS / 144 FPS / etc.
```

Recommended structure:

```text
Zoom
Magnification range and speed.

Range and speed
┌────────────────────────────────────────────────────┐
│ Zoom step                         [slider] [35%]  │
│ Sets how much each zoom step changes.              │
├────────────────────────────────────────────────────┤
│ Maximum zoom                      [slider] [450%] │
│ Sets the highest zoom level QuickZoom can reach.   │
├────────────────────────────────────────────────────┤
│ Frame rate limit                  [Dropdown: Auto] │
│ Limits how often the magnified view updates.       │
└────────────────────────────────────────────────────┘
```

## Shortcuts Page

The Shortcuts page needs the most cleanup. It should look like a shortcut editor.

Recommended labels:

```text
Shortcut mode       -> Shortcut mode
Enable key          -> Zoom modifier key
Invert colors key   -> Invert colors shortcut
Follow cursor key   -> Follow cursor shortcut
```

Recommended descriptions:

```text
Shortcut mode
Choose whether QuickZoom accepts mouse shortcuts, keyboard shortcuts, or both.

Zoom modifier key
Hold this key and scroll to zoom.

Invert colors shortcut
Hold the modifier key and press this key to toggle inverted colors.

Follow cursor shortcut
Hold the modifier key and press this key to toggle cursor following.
```

Preferred visible combinations:

```text
Zoom with mouse:      Alt + Mouse Wheel
Zoom with keyboard:   Alt + Plus / Minus
Invert colors:        Alt + I
Follow cursor:        Alt + F
Reset cursor:         Alt + R
```

Recommended structure:

```text
Shortcuts
Keyboard and mouse controls.

Mode
┌────────────────────────────────────────────────────┐
│ Shortcut mode                        [Mouse + keyboard]│
│ Choose which shortcut types QuickZoom accepts.      │
└────────────────────────────────────────────────────┘

Zoom
┌────────────────────────────────────────────────────┐
│ Mouse zoom                         [Alt] + [Mouse Wheel] │
│ Hold the modifier key and scroll to zoom.           │
├────────────────────────────────────────────────────┤
│ Keyboard zoom                      [Alt] + [+ / -] │
│ Hold the modifier key and press plus or minus.      │
└────────────────────────────────────────────────────┘

Actions
┌────────────────────────────────────────────────────┐
│ Invert colors                      [Alt] + [I]     │
│ Toggles inverted colors.                            │
├────────────────────────────────────────────────────┤
│ Follow cursor                      [Alt] + [F]     │
│ Toggles cursor following.                           │
├────────────────────────────────────────────────────┤
│ Reset cursor                       [Alt] + [R]     │
│ Moves the cursor back to a safe position.           │
└────────────────────────────────────────────────────┘
```

---

# Empty Space Handling

Do not leave the bottom 35–45% of each page empty.

Fix options:

1. Make the settings window shorter.
2. Add a useful preview/status card where appropriate.
3. Keep footer buttons closer to content with a sticky footer divider.
4. Use a two-column layout only where it makes sense.
5. Do not add filler content just to fill space.

Good useful status examples:

```text
Current zoom: 100%
Shortcut: Alt + Scroll
Selected display: Display 1
Theme: Dark
```

---

# Interaction States

Every interactive control should have these states:

```text
Default
Hover
Pressed
Focused
Disabled
Selected / active where relevant
```

Focus ring:

```text
Color: #60A5FA
Width: 2 px
Offset: 2 px
Radius: match control radius
```

Rules:

- Keyboard navigation must be visible.
- Disabled controls must clearly explain why they are disabled, either in text or tooltip.
- Hover states should be subtle, not flashy.

---

# Accessibility Requirements

Minimum requirements:

- Maintain strong contrast between text and background.
- Do not rely on color alone for selected states.
- Use visible focus rings.
- Make hit targets at least 32x32 px, preferably 40x40 px.
- Ensure text scaling does not clip labels.
- Avoid truncated labels.
- Support high DPI scaling cleanly.
- Avoid hardcoded pixel clipping for large text modes.

---

# Implementation Checklist for Codex

Use this checklist when patching the UI.

## Typography

- [ ] Replace any custom letter spacing/tracking on normal labels.
- [ ] Use Segoe UI Variable / Segoe UI.
- [ ] Normalize text sizes and weights.
- [ ] Use sentence-style capitalization.
- [ ] Keep descriptions short.
- [ ] Use either `color` or `colour` consistently. Prefer `color`.

## Layout

- [ ] Reduce row height by roughly 20–35%.
- [ ] Replace separate huge tiles with grouped panels and dividers.
- [ ] Tighten content padding and row gaps.
- [ ] Avoid giant empty lower sections.
- [ ] Keep footer buttons aligned and visually connected to the layout.

## Colors

- [ ] Use green as the single main accent.
- [ ] Remove purple active sidebar indicator unless brand requires it.
- [ ] Use blue only for focus rings/links.
- [ ] Use red only for destructive actions.
- [ ] Normalize panel, border, divider, hover colors.

## Sidebar

- [ ] Set item height to around 44–48 px.
- [ ] Use one icon family.
- [ ] Normalize icon size to 20 px.
- [ ] Align icons and labels precisely.
- [ ] Use green active indicator.

## Buttons

- [ ] Make `Done` a primary filled button.
- [ ] Make reset secondary.
- [ ] Rename reset action to clarify scope.
- [ ] Add hover/pressed/focus states.

## Toggles

- [ ] Normalize toggle size.
- [ ] Align toggles vertically in rows.
- [ ] Add hover/pressed/disabled states.
- [ ] Ensure tray menu toggles do not close the menu.

## Sliders

- [ ] Use compact sliders with value pills.
- [ ] Always show units.
- [ ] Replace refresh-rate slider with dropdown.
- [ ] Align values in a fixed-width right column.

## Dropdowns

- [ ] Give dropdowns a real field background and border.
- [ ] Use consistent width.
- [ ] Keep values short.

## Shortcuts

- [ ] Show actual key combinations using keycap pills.
- [ ] Make shortcut fields editable/clickable.
- [ ] Add shortcut conflict warnings.
- [ ] Avoid showing only isolated `Alt` keys where a full combination is expected.

## Cursor Page

- [ ] Reduce color swatch count.
- [ ] Add selected swatch ring.
- [ ] Add `Custom…` color option.
- [ ] Make live preview a proper preview card.

## Tray Menu

- [ ] Reduce width/height and row sizes.
- [ ] Add separators between groups.
- [ ] Match settings-window palette.
- [ ] Use smaller icons.
- [ ] Make toggle rows non-closing.
- [ ] Make Quit row less harsh.

---

# Suggested Final UI Copy

## General

```text
General
Core magnification behavior.

Behavior
Smooth zoom
Blends between zoom levels for softer transitions.

Turn off at 100%
Stops magnification when zoom returns to normal.

Keep cursor centered
Keeps the focus point near the center while zooming.
```

## Display

```text
Display
Monitor and screen behavior.

Monitor
Follow cursor between monitors
Automatically switches to the monitor under the cursor.

Magnification
Magnify
Choose which displays QuickZoom should zoom.
```

Dropdown values:

```text
All displays
Current monitor
Selected monitor
```

## Appearance

```text
Appearance
Theme and visual options.

Interface
Theme
Follow Windows, always light, or always dark.

Language
Changes text in the tray menu, settings window, and dialogs.

Text size
Changes the size of text in QuickZoom.
```

Text size values:

```text
Small
Default
Large
Extra large
```

## Cursor

```text
Cursor
Cursor size and colors.

Cursor assistance
Highlight cursor on wiggle
Quickly wiggle the mouse to highlight the cursor.

Enhanced cursor
Uses a larger high-contrast cursor while QuickZoom is running.

Enhanced cursor
Cursor size
Adjusts the enhanced cursor size.

Cursor fill
Sets the main color of the enhanced cursor.

Cursor outline
Sets the outline color of the enhanced cursor.

Live preview
Shows the selected fill, outline, and size.
```

## Zoom

```text
Zoom
Magnification range and speed.

Range and speed
Zoom step
Sets how much each zoom step changes.

Maximum zoom
Sets the highest zoom level QuickZoom can reach.

Frame rate limit
Limits how often the magnified view updates.
```

Frame rate values:

```text
Auto
60 FPS
90 FPS
120 FPS
144 FPS
165 FPS
240 FPS
Unlimited / Experimental
```

## Shortcuts

```text
Shortcuts
Keyboard and mouse controls.

Mode
Shortcut mode
Choose which shortcut types QuickZoom accepts.

Zoom
Mouse zoom
Hold the modifier key and scroll to zoom.

Keyboard zoom
Hold the modifier key and press plus or minus.

Actions
Invert colors
Toggles inverted colors.

Follow cursor
Toggles cursor following.

Reset cursor
Moves the cursor back to a safe position.
```

Visible combinations:

```text
Alt + Mouse Wheel
Alt + Plus / Minus
Alt + I
Alt + F
Alt + R
Middle Mouse Button
```

## Tray Menu

```text
QuickZoom

Quick controls
Enabled
Inverted colors
Follow cursor

Actions
Magnified displays
Keyboard shortcuts
Settings
Reset cursor

About
Quit
```

---

# Non-Negotiable Quality Rules

1. No stretched letter spacing on normal UI text.
2. No mixed accent colors.
3. No huge standalone setting tiles unless the control is complex.
4. No inconsistent units on sliders.
5. No unlabeled shortcut fragments like only `Alt` where the full shortcut matters.
6. No clipped or truncated labels.
7. No oversized tray menu rows.
8. No UI elements without hover/focus/disabled states.
9. No inconsistent icon families.
10. No vague reset action without clear scope.

