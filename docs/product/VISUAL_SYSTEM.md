# STOW visual system

This document records the approved visual direction for STOW so future UI work converges instead of drifting.

## Product character

STOW should feel like a calm, polished Windows 11 utility rather than a generic WPF application.

- Native Windows behavior, WPF implementation.
- Deep navy / blue foundation with restrained teal accents.
- Light and Dark are the same product system, not separate designs.
- Rounded geometry, thin borders, quiet depth, generous alignment and deliberate density.
- Blue is the primary interactive color. Teal is secondary and should be used sparingly for focus/privacy/positive status.
- Avoid decorative UI that implies functionality STOW does not actually provide.

## Official brand artwork

- `assets/STOW.png` is the approved raster master.
- `assets/STOW.ico` is the Windows multi-resolution icon.
- `assets/STOWHero.png` is the transparent hero treatment used in Focus and About.
- The mark is layered blue/teal window cards entering a tray/container, with blue behind and the smaller teal card in front.
- The same identity is used by the app shell, executable, taskbar, main tray icon, installer and About.
- Asset provenance is documented in `assets/README.md` and `CREDITS.md`; generators live in `scripts/generate-stow-icon.py` and `scripts/generate-stow-hero.py`.

## Shell

- Custom Windows chrome with native drag/resize behavior and integrated minimize/maximize/close controls.
- Rounded outer window in normal state; square edge in maximized state.
- Sidebar remains visible on all primary screens.
- Navigation order: Apps, Rules, Focus, Insights, Settings. About is anchored separately at the bottom.
- Search lives in the shell. Apps, Rules and Settings filter live; Enter may navigate to matching product areas.
- A Focus shortcut is available in the top bar.
- Page headers use the display font resource; body copy uses the text font resource.

## Layout and components

- Default window: approximately 1440 × 860.
- Content uses a 12–14 px rhythm for gaps and 14–18 px card padding.
- Cards use quiet borders and rounded corners; nested surfaces use the elevated surface token.
- Primary actions use blue fills. Secondary actions use surface + border.
- Toggle switches are compact pill switches rather than native checkbox chrome.
- Filters use compact pill buttons.
- Tables are visually light: subtle header labels and thin row dividers instead of heavy grid chrome.
- Application rows should show the real local executable icon when available.

## Screen composition

### Apps
Managed and Available applications are visible together. Managed apps expose state, management toggle and compact actions. Available apps can be added directly. A bottom Focus CTA connects app management to the Focus workflow.

### Rules
A rule table/list occupies the primary area; the selected rule editor occupies a secondary panel. Filters distinguish All, App, Focus and Startup rules. Empty state must remain useful and should never display fake rules.

### Focus
The first row contains Current session, Quick presets and Focus schedule. The second row contains Apps during Focus plus Saved presets / Session state. Presets and schedules always use real persisted data.

### Insights
Local-only privacy status is explicit. Use real local activity to populate seven-day metrics, chart, most-stowed app and recent activity. Never synthesize activity to make the dashboard look populated.

### Settings
Use two responsive columns of icon-led cards. Appearance includes Light/Dark/System segmentation and visual previews. Accessibility remains a first-class card and must remain functional at 200% text.

### About
Preserve the approved hero/version composition: STOW identity and build information, visual hero, What's new, and More information actions. Release notes use actual release information.

## Accessibility constraints

Visual convergence must not regress:
- 100%, 125%, 150% and 200% in-app text scaling.
- Windows Contrast Themes / enhanced contrast.
- keyboard navigation and Alt access keys.
- named UI Automation actions and screen-reader semantics.
- visible keyboard focus.
- no information communicated by color alone.

When the approved visual concept conflicts with accessibility or restore safety, accessibility and restore safety win; the visual implementation should adapt rather than removing those guarantees.
