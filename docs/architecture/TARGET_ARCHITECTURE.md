# STOW Target Architecture

STOW is a Windows desktop product with a modern WPF UI and a separately testable minimize-to-tray engine.
Electron is explicitly excluded as a UI dependency.

## Target projects

- `STOW.App` — WPF shell, navigation, screens, view models and theme resources.
- `STOW.Engine` — product-level orchestration and minimize-to-tray state machine.
- `STOW.Platform.Windows` — HWND/PID enumeration, Win32 calls, tray integration and Windows startup.
- `STOW.Infrastructure` — configuration, migration, update metadata, diagnostics and logging.
- `STOW.Engine.Tests` — deterministic engine/state tests.
- `STOW.IntegrationTests` — Windows behavior tests against real helper windows.

## Dependency direction

`STOW.App -> STOW.Engine -> abstractions`
`STOW.Platform.Windows -> STOW.Engine abstractions`
`STOW.Infrastructure -> STOW.Engine abstractions`

The App must not contain raw Win32 restore logic.
The Platform project must not know about pages such as Apps, Rules or Focus.
## Compatibility boundary

During migration, the original v0.3.3 implementation remains available under `legacy/` or a dedicated compatibility adapter until parity tests pass.
Extraction is mechanical first: move code without behavioral edits, then test, then refactor.

## UI shell

Navigation order is fixed:
1. Apps
2. Rules
3. Focus
4. Insights
5. Settings

About is a separate destination anchored at the bottom of the navigation.

## Runtime target

Use .NET 10 and WPF for the new UI.
Keep the product per-user and avoid requiring administrator privileges for ordinary operation.
