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

## Focus

Use the approved Focus composition as the visual baseline.
Focus should activate a temporary calmer desktop state without changing permanent app configuration unless the user explicitly saves it.
Always show whether a Focus session is active and how to stop it.
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
