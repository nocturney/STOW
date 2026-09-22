# Trayify -> STOW Migration Contract

Migration must preserve user configuration and the proven runtime behavior while changing product identity.

## Existing Trayify identity

- Config: `%APPDATA%\Trayify\config.txt`
- Install path: `%LOCALAPPDATA%\Trayify\Trayify.exe`
- Startup registry value: `Trayify`
- Current package identity: `ChristianVelvet.Trayify`
- Repository: `nocturney/trayify`

## Target STOW identity

- Config root: `%APPDATA%\STOW`
- Install root: `%LOCALAPPDATA%\STOW`
- Startup registry value: `STOW`
- Executable: `STOW.exe`
- Installer: `STOWSetup.exe`
- Repository: `nocturney/STOW`
- WinGet target identity: `ChristianVelvet.STOW`
- Scoop manifest: `stow`

## Implementation status

Configuration migration, startup handoff, installed-app registration, safe installer upgrade/uninstall, updater routing, and package-manager identity generation are implemented and regression-gated.
The original Trayify configuration remains preserved as rollback data.
Package-manager metadata is generated from final release artifacts and is not submitted until the matching GitHub release assets exist.

## One-time migration algorithm

1. Detect an existing STOW configuration. Never overwrite newer STOW data with legacy data.
2. If STOW data is absent, detect Trayify configuration.
3. Copy Trayify configuration into a migration staging location.
4. Parse and validate every managed-app record before committing the migration.
5. Write STOW configuration atomically.
6. Preserve the original Trayify configuration as rollback evidence.
7. Replace the startup entry only after the STOW executable is installed and validated.
8. Record migration version and timestamp locally so the operation is idempotent.
9. On any failure, keep Trayify data and startup behavior intact.

## Installer/upgrade requirements

A STOW installer must recognize Trayify installs and package-manager locations.
Do not leave both products auto-starting.
Do not uninstall or remove Trayify data until STOW has successfully imported it.
Uninstalling STOW must never delete retained Trayify rollback data unless the user explicitly chooses to remove legacy settings.

## Release gate

The v0.3.3 restore parity gate and real GrokBot/Electron handoff have passed. Installer migration and updater gates have also passed.
The remaining final binary-trust blocker is Authenticode signing with a real Code Signing certificate, followed by final signed-release validation and package-manager submission.
