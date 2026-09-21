# STOW Installer & Trayify Migration

STOW uses a per-user installer derived from the proven Trayify setup flow, with stricter runtime-safety rules.

## Safety contract

- Install target: `%LOCALAPPDATA%\STOW\STOW.exe`.
- No administrator rights are required for ordinary install/uninstall.
- A running STOW instance is never force-killed during upgrade or uninstall.
- Setup requests shutdown through `Local\STOWRequestExit` and waits for the runtime to restore every hidden managed app.
- If STOW cannot exit safely, setup fails instead of orphaning hidden windows.
- If legacy Trayify is still running, setup refuses to continue. Trayify must exit through its own tray-menu safe shutdown first.
- Existing STOW config is never overwritten by installer migration.
- Legacy Trayify config is retained as rollback data.

## Upgrade behavior

- `STOWSetup.exe /VERYSILENT /NOLAUNCH` performs a silent in-place upgrade.
- Payload replacement is staged in the install directory and uses `File.Replace` with a rollback file when upgrading an existing install.
- Installer and payload file versions must match `STOW_VERSION`/the numeric file version before packaging succeeds.
- Start Menu and optional desktop shortcuts are managed per user.
- Apps & Features registration uses the current-user uninstall registry key.
- The uninstaller is copied to `%LOCALAPPDATA%\STOW\Uninstall.exe`.

## Startup migration

The installer follows the same policy as the runtime: STOW starts with Windows whenever at least one managed app is enabled.
It reads STOW config first, falling back to legacy Trayify config during migration, writes the STOW Run value when required, and removes the legacy Trayify Run value.
If a Trayify Run value existed, a migration marker is retained under `%APPDATA%\STOW`.

## Verified regression — 2026-09-21

The interactive user-session installer regression passed end to end with `0.4.0-preview.1`:

- silent uninstall exited 0;
- install directory removed;
- `%APPDATA%\STOW\config.txt` retained with identical SHA-256;
- STOW startup entry removed on uninstall;
- Apps & Features entry removed on uninstall;
- silent reinstall exited 0;
- app and uninstaller restored;
- config SHA-256 remained identical;
- Apps & Features registered `0.4.0-preview.1`;
- startup restored from enabled managed apps;
- installed STOW executable started successfully.

Manual regression runner: `scripts/test-stow-installer.ps1`.
