# Tray Menu Style Guide

## Purpose
This document defines the required visual and interaction style for all tray menus, context menus, and similar floating action menus in the application.

The goal is simple:

**Every tray menu must look like a premium native dark desktop menu, not a homemade popup, not a generic web dropdown, and not a recycled settings panel.**

---

## Design Reference Direction

Use the design language of polished dark interfaces similar in quality to:
- Slack
- Linear
- Notion
- Discord
- high-end desktop productivity apps

This does **not** mean copying their layouts directly.  
It means borrowing their visual discipline:
- near-black layered surfaces
- soft rounded corners
- subtle shadows
- restrained borders
- clean spacing rhythm
- inset hover states
- clear hierarchy
- low-noise design

---

## Core Design Principles

### 1. Premium dark surface, not flat black
Dark mode does **not** mean using a plain black rectangle.

The menu surface must feel layered and refined:
- deep charcoal or near-black base
- slightly lighter interactive layers
- subtle surface separation from background
- calm contrast, not harsh contrast

Avoid:
- flat `#000000` panels
- muddy grey blocks
- default browser-like dropdown styling
- cheap glassmorphism
- loud gradients

---

### 2. Compact, controlled, desktop-appropriate density
A tray menu is a compact desktop component. It must not look oversized, stretched, cramped, or panel-like.

Requirements:
- width should be based on content
- rows should be compact but comfortable
- no giant empty horizontal space
- no tiny compressed rows
- no unnecessary height

Target feel:
- efficient
- premium
- intentional
- desktop-native

---

### 3. Spacing must do the heavy lifting
The quality of the menu should come mostly from spacing and alignment, not loud decoration.

Use:
- consistent outer padding
- consistent row height
- consistent left/right inset
- consistent gap between labels and shortcuts
- consistent spacing above and below separators
- clean section rhythm

Do not:
- place text loosely in a box
- let rows feel randomly spaced
- mix different padding values without reason

---

### 4. Hover and active states must be inset and rounded
Interactive states must feel integrated into the menu.

Hover and selected states must:
- sit **inside** the menu with inset spacing
- have their own rounded corners
- feel like a soft internal layer
- never appear as a flat full-width slab

Avoid:
- edge-to-edge grey hover bars
- square highlight rectangles
- mismatched corner radii
- visually heavy active states

This is one of the highest-priority rules.

---

### 5. Hierarchy must be calm and obvious
Each menu item should communicate hierarchy clearly.

Use:
- primary text for item labels
- secondary, dimmer text for keyboard shortcuts or metadata
- subtle dividers for grouping
- optional icons only when useful

The menu should never feel visually noisy or overdesigned.

---

## Visual Style Specification

## Surface
Use a floating menu container with the following characteristics:
- deep charcoal / near-black background
- subtle 1px border with very low contrast
- soft shadow that lifts the menu off the desktop
- modern rounded corners
- crisp silhouette
- no thick outline

### Surface goals
The menu surface should feel:
- premium
- calm
- dense
- modern
- native-quality

### Surface anti-goals
Do not make it feel:
- web-like
- boxy
- flat
- homemade
- aggressively styled

---

## Corners and Shape Language

### Outer container
- Use a soft modern corner radius
- Rounded enough to feel modern
- Not so rounded that it becomes playful or mobile-like

### Item states
- Hover, pressed, and selected states must also be rounded
- Inner state radius should be slightly smaller than the menu radius
- All radii should feel part of one system

Avoid:
- square hover states
- inconsistent radius values
- row backgrounds touching edges harshly

---

## Row Design

Each menu item row must:
- be vertically centered
- have consistent height
- have clear left and right alignment
- be fully clickable
- provide enough padding for a premium feel

### Row layout
Recommended internal row structure:
- left area: optional icon + label
- right area: shortcut / metadata / submenu chevron

### Alignment rules
- labels align left
- shortcuts align right
- icon alignment must be visually centered, not just mathematically centered
- every row should share the same horizontal insets

---

## Typography

Typography must feel calm, crisp, and modern.

### Primary labels
- brighter than secondary text
- medium or semibold weight
- easy to scan
- never oversized

### Secondary text
Use for:
- keyboard shortcuts
- helper metadata
- counts
- submenu hints

Secondary text should:
- be lower contrast than labels
- never compete with primary actions
- remain clearly readable

### Section labels
If the menu uses grouped sections:
- keep section labels subtle and restrained
- smaller and dimmer than action labels
- do not let them dominate

Avoid:
- too many font weights
- oversized headings
- excessively thin low-contrast text
- loud all-caps everywhere unless used sparingly and intentionally

---

## Color and Contrast

### Base palette behavior
Use a restrained dark palette:
- near-black base
- slightly lighter hover surfaces
- muted secondary text
- faint dividers
- soft white primary text

### Accent usage
Accent colors should be minimal and purposeful.
Use accent only when needed for:
- selected state
- active toggle
- focus indication
- status where meaningful

Avoid:
- bright default blue everywhere
- random accent splashes
- over-saturated UI elements
- strong color contrast that breaks the dark theme discipline

---

## Divider Styling

Dividers should be structural, not decorative.

Requirements:
- very low contrast
- enough spacing above and below
- used only where grouping improves comprehension
- no visual heaviness

Avoid:
- loud lines
- too many separators
- separators used as a crutch for bad spacing

In premium dark UI, dividers should barely announce themselves.

---

## Icons

Icons are optional. If used, they must be disciplined.

Requirements:
- small
- monochrome or very restrained
- consistent stroke weight
- optically aligned
- supportive, not dominant

Use icons only when they improve scanability or recognition.

Avoid:
- oversized icons
- random icon styles
- inconsistent icon weights
- colorful decorative icons in tray menus

---

## Shadows and Depth

Depth must be subtle.

Use:
- a soft layered shadow
- enough separation from background to feel floating
- restrained elevation

Avoid:
- giant fuzzy shadows
- no shadow at all
- strong neon glow
- “card on card on card” stacking overload

The menu should float, not shout.

---

## Sizing Rules

Tray menus should be content-driven.

Requirements:
- width should fit the longest realistic label + shortcut combination
- do not force excessive width
- keep top and bottom padding modest and balanced
- row heights should feel desktop-native

Avoid:
- giant panels for tiny amounts of content
- narrow widths that crush alignment
- mobile-style oversized card proportions

---

## Interaction Rules

### Open behavior
The menu should open:
- anchored correctly to the tray icon
- without layout jump
- without flicker
- without strange offset

### Close behavior
The menu should close only when appropriate:
- clicking outside
- selecting an action
- pressing Escape
- losing focus where expected

Do not close it unexpectedly because of unrelated interaction issues.

### Hover behavior
Hover must:
- respond immediately
- feel smooth
- not shift layout
- not resize rows
- not alter typography position

### Pressed state
Pressed state should:
- feel slightly deeper than hover
- remain subtle
- confirm interaction without becoming heavy

### Keyboard support
Menus should support proper keyboard navigation where relevant:
- arrow navigation
- enter/space activation where appropriate
- escape to dismiss
- visible focus state if keyboard navigation is enabled

---

## Context Menu vs Settings Panel

A tray context menu is **not** a settings page and **not** a generic panel.

Do not style it like:
- a card-based settings screen
- a modal dialog
- a sidebar
- a mobile action sheet
- a generic HTML dropdown

It must look like a refined desktop context menu first.

You may borrow visual cues from premium dark sidebars and filter panels, but the final result must remain:
- compact
- direct
- contextual
- desktop-native

---

## Design Language to Borrow from Inspiration

From the dark UI inspirations, borrow:
- near-black premium surfaces
- quiet typography
- strong spacing rhythm
- inset rounded active rows
- low-contrast separators
- crisp iconography
- soft hierarchy
- clean visual restraint

Do **not** borrow literally:
- oversized mobile card layouts
- long filter-panel structure
- huge vertical sections
- giant footer buttons
- sidebar navigation architecture

Borrow the **feel**, not the exact component type.

---

## Anti-Patterns: Never Ship These

Do not ship tray menus with:
- flat grey hover bars
- pure black blocks with plain text
- oversized empty width
- cramped row spacing
- harsh white separators
- random gradients
- mismatched corner radii
- shortcuts with same emphasis as labels
- cheap web dropdown styling
- panel layouts pretending to be menus
- generic Electron-looking hacky popups
- hover states touching container edges awkwardly

If a menu looks “custom” in a bad way, it fails this spec.

---

## Quality Bar

Every tray menu must pass this simple test:

### It should feel like
- a premium production desktop app
- a polished native dark context menu
- something designed intentionally
- part of a cohesive design system

### It should not feel like
- a quick prototype
- a black box with text
- a recycled settings card
- a web dropdown
- a homemade utility app popup

---

## Recommended Implementation Structure

Build tray menus as dedicated menu components, not ad hoc popup containers.

Recommended component structure:
- `TrayContextMenu`
- `TrayMenuItem`
- `TrayMenuDivider`
- `TrayMenuSectionLabel` if needed
- `TrayMenuShortcut`
- `TrayMenuIcon`
- `TrayMenuSubmenuChevron` if needed

Use shared design tokens for:
- menu background
- menu border
- menu shadow
- menu radius
- item height
- item horizontal padding
- hover background
- pressed background
- primary text
- secondary text
- divider color
- focus ring if applicable

Do not hardcode one-off visual values separately in different menus.

---

## Final Design Goal

**The tray menu should feel like a premium native dark desktop context menu with compact elegant density, inset rounded interaction states, quiet hierarchy, and precise spacing.**

If the result looks like a random dark popup with labels inside, it is wrong.
