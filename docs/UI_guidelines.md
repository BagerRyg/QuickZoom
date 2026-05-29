# QuickZoom UI & UX Implementation Guidelines

Use this document as the **default implementation standard** for all new features, settings pages, tray UI elements, and control updates in QuickZoom.

Do not treat this as optional styling guidance. New UI should follow these rules unless there is a strong product or technical reason not to.

---

## 1. Product Design Goal

QuickZoom should feel like a **premium desktop application** with the polish of modern Apple Settings, Slack, and other high-end software.

The app must look:
- modern
- dark
- clean
- spacious
- aligned
- professional
- intentional
- calm and polished

Avoid anything that feels like:
- old Win32 forms
- manually placed controls
- random spacing
- clipped text
- stock system controls with no styling
- cramped tray menus
- inconsistent alignment between pages

---

## 2. Core Design Principles

### 2.1 Use layout systems, not manual placement
All UI must be built with layout containers and consistent spacing.

Do **not** place controls by eye or hardcode positions unless absolutely necessary.

Use:
- rows
- columns
- grids
- stack panels
- card sections
- shared control columns

### 2.2 Consistency over one-off styling
Every settings page and quick panel should reuse the same patterns.

Create reusable components instead of restyling each control separately.

### 2.3 Spacing is part of the design
The app should breathe. Do not compress labels, descriptions, toggles, inputs, or buttons together.

### 2.4 Strong hierarchy
The user should immediately understand:
- where they are
- what section they are in
- what each setting does
- what control belongs to what label

### 2.5 Premium dark mode
Use dark surfaces with soft contrast. Avoid harsh pure black and bright white.

---

## 3. Visual System

### 3.1 Color palette
Recommended baseline palette:

- `Background`: `#111315`
- `Surface`: `#171A1D`
- `Elevated Surface`: `#1C2024`
- `Border`: `rgba(255,255,255,0.08)`
- `Primary Text`: `#F5F7FA`
- `Secondary Text`: `#A7B0BA`
- `Disabled Text`: muted gray with lower contrast
- `Accent`: `#56C271`
- `Destructive`: `#C15B5B`

Rules:
- Do not use pure black for major surfaces
- Do not use neon accent colors
- Do not use bright red for normal off states
- Use neutral gray for ordinary off toggles
- Use red only for destructive or warning-related actions

### 3.2 Corners and shape language
Use rounded corners consistently.

Recommended radius tokens:
- Small: `8px`
- Medium: `12px`
- Large: `16px`

### 3.3 Borders and elevation
Use subtle borders and soft separation.

Rules:
- Prefer low-contrast borders
- Use shadows lightly
- Do not create heavy/glossy UI
- Surfaces should separate through tone first, shadow second

---

## 4. Typography

Use one modern, readable font family consistently across the app.

Recommended hierarchy:

- Window Title: `30–34px`, semibold
- Page Title: `18–24px`, semibold
- Section Description: `13–15px`, regular
- Setting Label: `14–16px`, medium
- Setting Helper Text: `12–13px`, regular
- Button Text: `13–14px`, medium

Rules:
- All page text should align to a common left edge
- Use muted color for secondary text
- Never allow titles or subtitles to clip or overlap
- Keep helper text compact but readable
- Keep line length reasonable

---

## 5. Spacing System

Use a consistent spacing scale.

Recommended spacing:
- Outer window padding: `24px`
- Section spacing: `24–32px`
- Label to helper text: `4px`
- Row-to-row spacing: `16–20px`
- Card internal padding: `16–20px`
- Tab gap: `8–12px`
- Control height: `36–40px` minimum
- Settings row height: `56–72px`

Rules:
- Do not mix random spacing values
- Use the same spacing rhythm across every page
- Keep controls aligned to shared edges
- Avoid giant dead gaps and avoid cramped stacking

---

## 6. Settings Page Structure

Every settings page should follow the same structure:

1. Page title
2. Short page description
3. One or more grouped settings sections
4. Settings rows inside each section

### 6.1 Settings row pattern
Each setting row should contain:
- a left content block
  - label
  - optional helper text
- a right control block
  - toggle, dropdown, numeric input, or button

Example:

**Smooth Zoom**  
Makes zoom transitions feel fluid and less abrupt.  
`[toggle aligned to the right]`

Rules:
- Controls must align to a shared right column
- Labels and descriptions must align to a shared left column
- Rows must be vertically centered
- Rows should use consistent min height
- Optional dividers can be used between rows

Do not let controls float independently.

---

## 7. Tabs

The current tab style should be replaced with a modern segmented or pill-style tab bar.

Requirements:
- rounded corners
- consistent height
- balanced horizontal padding
- subtle hover state
- selected state with elevated fill or soft accent outline
- dark appearance matching the rest of the app

Rules:
- Tabs must not look like old boxed buttons
- Avoid hard-edged legacy styling
- Tab spacing must be even
- Selected state should be obvious but tasteful

---

## 8. Toggle Switches

### 8.1 Toggle style
All toggles should be redesigned in a premium Apple-inspired style.

Requirements:
- pill-shaped track
- circular thumb
- smooth animation
- large enough hit target
- subtle active glow or highlight
- proper hover, focus, disabled, and pressed states

Recommended size:
- around `44x24px` or similar

### 8.2 Toggle states
- **On:** accent-colored track, thumb on right
- **Off:** neutral gray track, thumb on left
- **Hover:** slight brighten
- **Focus:** visible focus ring
- **Disabled:** faded track and thumb

Rules:
- Do not make standard off state red
- Use red only when a toggle represents disabling a critical protection or a destructive option
- Toggle animation should be smooth and subtle, roughly `150–200ms`

---

## 9. Inputs and Dropdowns

Numeric inputs, dropdowns, and other form controls must match the app style.

Requirements:
- dark filled surface
- rounded corners
- subtle border
- clean inner padding
- consistent heights
- custom or restyled dropdown arrow
- visible focus state
- no harsh stock white chrome

Rules:
- All controls in the same context should share height and spacing
- Spinboxes should not look like default OS controls
- Dropdowns should line up to the same control column as toggles

---

## 10. Buttons

Buttons should feel modern and restrained.

Requirements:
- rounded corners
- dark surface styling
- subtle border
- hover state with slight lift or brighten
- proper padding
- minimum height `36–40px`

Rules:
- Avoid old default system button styling
- Prefer one strong primary action and quieter secondary actions
- Remove redundant buttons when window-level close controls are enough

---

## 11. Header and Window Layout

The app header must be clear and uncluttered.

Structure:
- top title
- subtitle beneath title
- tabs beneath subtitle

Rules:
- no text clipping
- no overlapping header text
- no awkward duplicate title treatments
- no giant empty strip with misaligned text
- maintain clear vertical rhythm

The whole top region should feel deliberate and polished.

---

## 12. Tray / Quick Panel Design

The tray panel should behave like a modern quick settings panel, not a fragile menu.

### 12.1 Visual design
Use compact cards or tiles with enough padding.

Each quick action should show:
- icon
- readable label
- optional state text like “On” or “Off”
- toggle control with enough size and spacing

Examples:
- Magnifier — On
- Invert Colors — Off
- Focus Indicator — On

Rules:
- Avoid unreadable abbreviations as the only label
- Avoid crammed small toggles in tiny boxes
- Give each quick action enough room to breathe
- Use consistent padding and alignment

### 12.2 Better quick toggle layout
Preferred layouts:
- icon + text on left, toggle on right
- or icon + label + state stacked with toggle below

Use whichever gives the cleanest result without crowding.

---

## 13. Tray Panel Behavior

### 13.1 Required behavior
The tray quick panel must **stay open while interacting with it**.

It must remain open when the user:
- toggles a switch
- clicks inside the panel
- uses dropdowns or buttons inside the panel

It should close only when:
- the user clicks outside the panel
- the user presses Escape
- the user explicitly closes it
- the app loses focus, if that is an intentional product decision

### 13.2 Implementation rule
Do **not** use a standard auto-closing context menu for interactive tray UI.

Instead use:
- a custom popup
- a flyout
- a borderless tool window
- or another manually managed interactive panel

### 13.3 Technical direction
Likely fix:
- replace `ContextMenu` / `ContextMenuStrip`-style behavior with a custom popup host
- keep the panel open during internal pointer and keyboard interactions
- close only through explicit outside-click detection or manual dismiss logic
- mark internal interaction events as handled where needed

Framework-specific direction:
- **WPF / WinUI:** prefer `Popup`, `Flyout`, or a custom borderless window with manual dismissal logic
- **WinForms:** prefer a custom borderless form anchored to the tray icon instead of `ContextMenuStrip`

---

## 14. Alignment Rules

Alignment must be strict and intentional.

Rules:
- All page text aligns to one left content column
- All controls align to one right control column
- Row contents are vertically centered
- Labels, descriptions, toggles, inputs, and buttons must line up across rows
- Use grid-based layout for settings pages

Recommended settings grid:
- left column: flexible text content
- right column: fixed or min-width control area

Do not allow:
- random label offsets
- controls at different right edges
- section text starting at inconsistent x positions
- helper text drifting away from its label

---

## 15. Motion and Interaction Polish

Use subtle animation across the app.

Recommended animation uses:
- toggle thumb slide
- hover fade or brighten
- tab state transition
- popup appear with soft fade and slight scale

Rules:
- keep motion subtle
- avoid flashy animations
- use roughly `120–180ms ease-out` for most microinteractions
- animations should reinforce quality, not distract

---

## 16. Accessibility and Robustness

The UI must remain usable and stable under real-world conditions.

Requirements:
- keyboard navigation works
- focus states are visible
- text contrast remains readable
- controls have comfortable hit targets
- layout survives DPI scaling
- localization does not break layout
- longer text does not clip
- 125%, 150%, and 200% scaling must be supported cleanly

Rules:
- no clipped labels
- no overlapping header text
- no fixed pixel assumptions that break on scaling
- no tiny click targets

---

## 17. Responsiveness and Future-Proofing

The design must handle:
- longer labels
- additional helper text
- more settings rows
- more tabs
- multiple languages
- window resizing
- future feature additions

Rules:
- build flexible containers
- avoid fragile one-off spacing hacks
- design reusable components now so future features inherit the same polish

---

## 18. Required Reusable Components

Use shared components where possible:
- `SettingsPage`
- `SettingsSection`
- `SettingsRow`
- `ToggleSwitch`
- `ModernTabBar`
- `ModernButton`
- `ModernDropdown`
- `ModernNumberInput`
- `QuickActionTile`
- `TrayPopupWindow`

Every new feature or settings area should reuse these patterns instead of inventing new UI structures.

---

## 19. Anti-Patterns to Avoid

Do not ship UI that has any of the following unless unavoidable:
- default legacy system buttons and inputs
- cramped tray toggles
- text clipping
- overlapping header text
- inconsistent left margins
- inconsistent right alignment of controls
- random vertical gaps
- red used for normal off state
- context menu behavior for interactive quick settings
- tiny unreadable labels like abbreviations without explanation
- hardcoded control positions that break on scaling

---

## 20. Implementation Standard for All Future UI Work

When adding any new feature, setting, quick toggle, or control:

1. Use the existing spacing system
2. Use the shared component patterns
3. Follow the dark theme tokens
4. Keep controls aligned to a shared grid
5. Add helper text where useful
6. Match the premium dark visual style
7. Ensure tray interactions stay open when appropriate
8. Test DPI scaling and text overflow
9. Avoid default OS-looking controls
10. Make the result feel like a polished commercial product

---

## 21. Final Quality Bar

Before shipping any new UI, ask:

- Does this look modern and premium?
- Does it feel consistent with the rest of the app?
- Is spacing clean and intentional?
- Are labels and controls perfectly aligned?
- Does dark mode feel tasteful and polished?
- Would this look at home in a premium desktop app like Slack or Apple Settings?
- Does the tray panel behave correctly during interaction?

If the answer is no, refine it before shipping.

---

## 22. Short Directive for Codex

Use these UI rules as the default standard for all future QuickZoom work. Build UI with reusable components, layout containers, premium dark styling, Apple-inspired toggle switches, strict alignment, modern tabs, and a persistent interactive tray popup that does not close during internal interaction.
