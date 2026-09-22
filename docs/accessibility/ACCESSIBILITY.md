# STOW accessibility contract

STOW treats accessibility as a product requirement, not an optional skin.

This document is an engineering acceptance contract. It is not a claim of formal WCAG, EN 301 549, Section 508, or other legal certification.

## Always-on baseline

- Use standard WPF controls where possible so Windows UI Automation can expose names, roles, states and actions.
- Every product workflow must remain operable from the keyboard.
- Keyboard focus must be visibly identifiable; custom STOW buttons use an explicit 2-pixel focus treatment.
- Windows Contrast Themes take precedence over STOW Light/Dark theme choices automatically.
- Critical meaning must never depend on color alone.
- Normal text targets at least 4.5:1 contrast; essential non-text controls/state indicators target at least 3:1.
- STOW currently uses no decorative animation, flashing or blinking. If motion is introduced later, it must respect the Windows client-area animation preference.
- Display/DPI scaling must not make a critical control unreachable.

## User accessibility preferences

Settings > Accessibility persists locally in `%APPDATA%\STOW\settings.json`.

- Text size: Standard (100%), Large (125%), Extra large (150%), Maximum (200%).
- Increase contrast in STOW: forces the system-color high-contrast palette even when Windows Contrast Themes are off.
- Use the Windows UI font: replaces the normal STOW font fallback with Segoe UI Variable Text / Segoe UI.

These preferences apply live and survive restart. Existing settings files default safely to 100%, normal contrast and the normal STOW font fallback.

## Release acceptance

Before a release that changes UI, navigation, typography, colors or interaction:

1. Run the normal Engine and Integration suites.
2. Run an interactive keyboard-only smoke across Apps, Rules, Focus, Insights, Settings and About.
3. Verify meaningful UI Automation names/control types for interactive controls, including any icon/custom-content buttons.
4. Run the app at 200% STOW text size and verify critical controls remain readable, reachable and not clipped.
5. Run with increased contrast and with a Windows Contrast Theme.
6. Verify destructive confirmations and runtime error/status messages remain understandable without color.
7. Re-run live Win32 parity so accessibility work cannot regress hide/restore safety.

Use Narrator and Accessibility Insights for a manual assistive-technology pass before broadly marketed Stable releases.

## Known boundary

STOW's in-app text setting is independent of the Windows Accessibility > Text size slider. Windows display/DPI scaling is still handled by WPF, and Windows Contrast Themes are followed automatically.

Do not describe STOW as formally accessibility-certified unless an appropriate external audit has actually established that claim.
