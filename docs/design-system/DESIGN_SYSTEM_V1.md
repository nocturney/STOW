# STOW Design System v1

Status: approved visual direction translated into implementation tokens.
The handoff PNGs remain the visual source of truth when a token and a reference image appear to disagree.

## Brand

Name: **STOW**
Tagline: **A calmer desktop starts here.**
Visual idea: translucent blue windows/cards being stowed into a tray/container.
Tone: calm, modern, precise, native to Windows 11 without copying Windows Settings.

## Typography

Primary UI family: Inter.
Fallback: Segoe UI, Arial, sans-serif.
Use semibold for navigation emphasis and section titles; avoid heavy display weights in ordinary controls.

## Approved source palettes

Light reference palette: Navy `#0F172A`, Blue `#3B82F6`, Teal `#2CA58D`, Sky `#E7EEF8`, Off White `#FAFBFD`.
Dark reference palette: Navy `#0B1220`, Blue `#3B82F6`, Teal `#14B8A6`, Slate `#334155`, Off White `#E5E7EB`.

## Semantic color tokens

| Token | Light | Dark |
| --- | --- | --- |
| Canvas | `#FAFBFD` | `#0B1220` |
| Surface | `#FFFFFF` | `#0F1B2B` |
| Surface elevated | `#E7EEF8` | `#132238` |
| Text primary | `#0F172A` | `#E5E7EB` |
| Text secondary | `#52637D` | `#A7B4C7` |
| Border | `#D7E2EF` | `#334155` |
| Accent blue | `#3B82F6` | `#3B82F6` |
| Accent blue strong | `#2563EB` | `#2563EB` |
| Accent teal | `#2CA58D` | `#14B8A6` |
## Geometry and spacing

Base spacing unit: 4 px.
Primary spacing steps: 4, 8, 12, 16, 20, 24, 32, 40.
Control height: 32 px compact, 40 px standard.
Card radius: 10 px.
Button/input radius: 8 px.
Large panel radius: 12 px.
Navigation rail width: 220-240 px at standard desktop size.
Use 1 px borders and restrained shadows; depth should come mainly from surface contrast.

## Interaction states

Hover: raise surface contrast without changing layout.
Pressed: reduce brightness and shadow; no scale animation.
Selected navigation: blue-tinted rounded background plus stronger text/icon.
Focus: visible 2 px keyboard focus ring using accent blue.
Disabled: preserve readability; reduce contrast, not opacity to near-invisibility.
Danger actions use semantic red only where destructive behavior exists.

## Motion

Prefer 120-180 ms transitions for hover, selection and panel appearance.
Respect Windows reduced-motion settings.
No decorative looping animation.

## Theme rules

Light and Dark are one component system, not separate designs.
Teal is a secondary accent and must not compete with primary blue.
Avoid pure black backgrounds and pure white borders in Dark mode.
## Core components

- App row/card
- Search field
- Toggle
- Primary / secondary / ghost / danger button
- Navigation item
- Section header
- Status chip
- Rule row
- Rule editor panel
- Focus preset card
- Insight metric card
- Settings row
- About information card
- Empty, loading, error and unavailable states

Every component must define default, hover, pressed, focused, disabled and selected states where applicable.
