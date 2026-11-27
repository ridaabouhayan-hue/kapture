---
name: "Kapture Design System"
description: "Calm, high-end Windows 11 Fluent design system for Kapture, a Dutch screenshot and quick-note productivity app with light and dark modes."
---

## Overview

Kapture is a modern 2026 Windows 11 desktop productivity tool built around one quiet superpower: every screenshot carries a quick note explaining why it was taken. The visual language blends Windows 11 Fluent calm with crisp, uncluttered craft—soft layered depth, subtle acrylic and mica translucency on floating panels, 8px to 12px rounded corners, generous breathing room, and a single confident Cobalt Blue accent (`#2563EB` in light mode, `#3B82F6` in dark mode), paired with the flat matte Kader-K selection-frame brand mark. No green accents appear anywhere in the interface. All UI copy is written in natural, concise Dutch.

## Colors

The palette pairs cool slate-tinted neutrals with a single Cobalt Blue accent. Light mode uses airy mica-inspired surfaces (`#FAF8FF` / `#F3F3FE` / `#FFFFFF`) with `#2563EB` as the primary action color. Dark mode uses deep, low-halate slate neutrals (`#11131B` / `#191B23` / `#1D1F27`) with `#3B82F6` as the primary accent. Status and semantic roles avoid green entirely, using Cobalt Blue for active/confirmed states, amber-terracotta for warnings, and crimson for destructive actions.

```yaml
colors:
  accent-light: "#2563EB"
  accent-dark: "#3B82F6"
  light:
    primary: "#2563EB"
    on-primary: "#FFFFFF"
    primary-container: "#DBE1FF"
    on-primary-container: "#00174B"
    secondary: "#495C95"
    on-secondary: "#FFFFFF"
    secondary-container: "#EEF0FF"
    on-secondary-container: "#182D63"
    tertiary: "#943700"
    on-tertiary: "#FFFFFF"
    tertiary-container: "#FFDBCD"
    on-tertiary-container: "#581E00"
    error: "#BA1A1A"
    on-error: "#FFFFFF"
    error-container: "#FFDAD6"
    on-error-container: "#93000A"
    background: "#F4F5FA"
    on-background: "#191B23"
    surface: "#FAF8FF"
    surface-dim: "#D9D9E5"
    surface-bright: "#FFFFFF"
    surface-container-lowest: "#FFFFFF"
    surface-container-low: "#F3F3FE"
    surface-container: "#EDEDF9"
    surface-container-high: "#E7E7F3"
    surface-container-highest: "#E1E2ED"
    on-surface: "#191B23"
    surface-variant: "#DFE1F4"
    on-surface-variant: "#434655"
    outline: "#737686"
    outline-variant: "#C3C6D7"
    inverse-surface: "#2E3039"
    inverse-on-surface: "#F0F0FB"
    inverse-primary: "#B4C5FF"
    shadow: "#000000"
    scrim: "#000000"
  dark:
    primary: "#3B82F6"
    on-primary: "#0C0E16"
    primary-container: "#003EA8"
    on-primary-container: "#DBE1FF"
    secondary: "#B4C5FF"
    on-secondary: "#182D63"
    secondary-container: "#282A32"
    on-secondary-container: "#DBE1FF"
    tertiary: "#FFB596"
    on-tertiary: "#581E00"
    tertiary-container: "#7D2D00"
    on-tertiary-container: "#FFDBCD"
    error: "#FFB4AB"
    on-error: "#690005"
    error-container: "#93000A"
    on-error-container: "#FFDAD6"
    background: "#11131B"
    on-background: "#E1E2ED"
    surface: "#11131B"
    surface-dim: "#0C0E16"
    surface-bright: "#373942"
    surface-container-lowest: "#0C0E16"
    surface-container-low: "#191B23"
    surface-container: "#1D1F27"
    surface-container-high: "#282A32"
    surface-container-highest: "#32343D"
    on-surface: "#E1E2ED"
    surface-variant: "#434655"
    on-surface-variant: "#C3C6D7"
    outline: "#8D90A0"
    outline-variant: "#434655"
    inverse-surface: "#E1E2ED"
    inverse-on-surface: "#2E3039"
    inverse-primary: "#2563EB"
    shadow: "#000000"
    scrim: "#000000"
```

## Typography

Inter provides crisp, neutral legibility at every desktop scale, from compact tray menus and floating toolbars to multi-column settings. JetBrains Mono is reserved for keyboard shortcuts (`Ctrl+Shift+S`), pixel dimensions (`1280 x 720`), and timestamps.

Icons: Lucide

```yaml
typography:
  headline-lg:
    fontFamily: "Inter"
    fontSize: 24px
    fontWeight: 600
    lineHeight: 32px
    letterSpacing: -0.3px
  headline-md:
    fontFamily: "Inter"
    fontSize: 18px
    fontWeight: 600
    lineHeight: 26px
    letterSpacing: -0.2px
  title-md:
    fontFamily: "Inter"
    fontSize: 15px
    fontWeight: 600
    lineHeight: 22px
    letterSpacing: -0.1px
  title-sm:
    fontFamily: "Inter"
    fontSize: 13px
    fontWeight: 600
    lineHeight: 18px
    letterSpacing: 0px
  body-md:
    fontFamily: "Inter"
    fontSize: 14px
    fontWeight: 400
    lineHeight: 20px
    letterSpacing: 0px
  body-sm:
    fontFamily: "Inter"
    fontSize: 12px
    fontWeight: 400
    lineHeight: 18px
    letterSpacing: 0px
  label-lg:
    fontFamily: "Inter"
    fontSize: 13px
    fontWeight: 500
    lineHeight: 18px
    letterSpacing: 0px
  label-md:
    fontFamily: "Inter"
    fontSize: 12px
    fontWeight: 500
    lineHeight: 16px
    letterSpacing: 0px
  label-sm:
    fontFamily: "Inter"
    fontSize: 11px
    fontWeight: 500
    lineHeight: 14px
    letterSpacing: 0.1px
  mono-sm:
    fontFamily: "JetBrains Mono"
    fontSize: 11px
    fontWeight: 500
    lineHeight: 16px
    letterSpacing: 0px
```

## Layout & Spacing

Desktop-first Windows 11 surfaces built on a strict 4px/8px grid. Floating panels (overlay toolbar, note bar, toast, tray flyout, context menu) use compact internal padding (`8px` to `16px`) and `8px` gaps between controls. Full application windows (Editor, Settings) use generous `24px` to `32px` outer padding, clear two-column or centered-canvas structure, and ample whitespace around the primary focal point.

```yaml
spacing:
  gutter-xs: 4px
  gutter-sm: 8px
  gutter-md: 12px
  gutter-lg: 16px
  gutter-xl: 24px
  section: 32px
  page-margin: 24px
```

## Elevation & Depth

Windows 11 Fluent layered depth:
- **Mica base windows** (Settings, Editor): solid or subtle-tinted neutral surfaces (`surface-container-low` for navigation rails, `surface-container-lowest` for active cards/canvas) separated by hairline `1px` `outline-variant` borders.
- **Acrylic floating panels** (Capture toolbar, Note bar, Toast, Tray flyout, Context menu): subtle translucent backgrounds (`bg-light-surface-container-lowest/85 dark:bg-dark-surface-container/85 backdrop-blur-xl`) paired with a crisp `1px` border and soft ambient shadow (`shadow-xl` / `shadow-2xl`).

## Shapes

Consistent Windows 11 Fluent corner radii between 8px and 12px for all primary containers, cards, inputs, and buttons. Small badges, chips, and icon buttons use 6px–8px; pills use `full`.

```yaml
rounded:
  sm: 6px
  DEFAULT: 8px
  md: 10px
  lg: 12px
  xl: 16px
  full: 9999px
```

## Motion & Micro-interaction

Snappy, quiet Fluent transitions. Hover and selection states resolve in 100ms; floating flyouts and toasts decelerate smoothly over 200ms. Nothing bounces or loops decoratively.

```yaml
motion:
  state-change:
    duration: 100ms
    delay: 0ms
    timingFunction: ease-out
  surface-enter:
    duration: 200ms
    delay: 0ms
    timingFunction: cubic-bezier(0.2, 0, 0, 1)
  surface-exit:
    duration: 150ms
    delay: 0ms
    timingFunction: cubic-bezier(0.3, 0, 1, 1)
```

## Voice & Content Strategy

- **Language:** 100% Dutch across all labels, placeholders, tooltips, and status messages.
- **Tone:** Calm, helpful, human, and direct—never technical or robotic. ("Waarom maak je deze screenshot?", "Al opgeslagen en gekopieerd", "Opgeslagen en gekopieerd").
- **Capitalisation:** Sentence case everywhere (`Nieuwe screenshot`, `Notitie toevoegen...`, `Nu naar Claude`).
- **No green accents:** Confirmation states ("Al opgeslagen en gekopieerd", active presets, selection borders) use the indigo-violet primary accent or calm neutral ink.

## Components

Component implementations live as individual HTML sticker sheet files in
`components/`. Each file contains the canonical, styled HTML components with
`<!-- COMPONENT: name -->` comment markers. Read each file before use.

- **Button** → `@components/button.html`: `button-primary`, `button-icon`, `button-with-icon`
- **Card** → `@components/card.html`: `card-elevated`, `card-action`, `card-interactive`, `card-interactive-2`
- **Chip** → `@components/chip.html`: `chip-action`, `chip-input`, `chip-with-icon`
- **Input Field** → `@components/input-field.html`: `input-field-text`, `input-field-icon`
