# STOW Screen Contracts

The screen set is fixed: Apps, Rules, Focus, Insights, Settings and About.
About is not nested under Settings.

## Apps

Primary product home.
Show Managed Apps first, then discoverable/available desktop apps.
Support search, enable/disable management, current state and direct restore where relevant.
Do not expose background helper processes as ordinary apps.
Empty state should teach the minimize-to-tray value without onboarding clutter.

## Rules

List rules on the left/center and edit the selected rule in a dedicated panel.
Rules must describe automation intent in plain English and expose enabled state clearly.
Validation errors stay close to the edited condition/action.

Current operational slice:
- per-app rules for the `Minimize` trigger;
- actions: `Stow` and `KeepVisible`;
- enabled/disabled state;
- 0-100 priority with the highest-priority matching enabled rule winning;
- local atomic persistence in `%APPDATA%\\STOW\\rules.json`;
- default/failure fallback remains `Stow`, preserving the Trayify v0.3.3 behavior contract.

Focus and Startup rule categories remain visible in the information architecture but are intentionally disabled until those rule triggers are integrated with their corresponding runtimes. They must not appear functional before then.

## Focus

Use the approved Focus composition as the visual baseline.
Focus should activate a temporary calmer desktop state without changing permanent app configuration unless the user explicitly saves it.
Always show whether a Focus session is active and how to stop it.

Current operational slice:
- Start/End Focus is backed by the live tray engine.
- The user selects enabled managed apps to keep visible; other visible managed apps are stowed for the session.
- Apps launched while Focus is active are stowed on the next engine scan unless they are in Keep visible.
- Manual restore during Focus adds that app to Keep visible for the remainder of the session.
- STOW tracks which apps were stowed specifically by the Focus session. End Focus restores only those apps; apps that were already stowed before Focus remain stowed.
- If any Focus-hidden app cannot be restored, Focus remains active and keeps its tracking instead of orphaning the window.
- Deep work and Keep all visible are temporary setup helpers only; they do not modify permanent app configuration.
- Saved presets and scheduling remain visibly disabled until persistence/scheduling runtimes are implemented.
- No fabricated distraction score or recommendation metric is shown.

## Insights

Local-only by default.
Show useful desktop/tray behavior such as apps most often stowed, restored, or kept out of the way.
Do not require telemetry or a cloud account.
When insufficient local history exists, show an honest empty state rather than fabricated metrics.

## Settings

Only mutable product settings belong here.
Examples: startup behavior, theme, notification preferences and compatible update behavior.
Do not place product/legal/reference documents here.

## About

Standalone navigation destination anchored separately at the bottom.
Required sections:
- Version / Build / Channel
- Updates
- What's New
- Privacy
- License
- Open-source notices
- Diagnostics
- Support
- GitHub / Releases

## Cross-screen requirements

Support Light and Dark themes, high DPI and keyboard navigation.
No critical action may be encoded by color alone.
Destructive or compatibility-sensitive operations must explain their consequence before execution.
