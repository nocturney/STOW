# STOW

**A calmer desktop starts here.**

STOW is a Windows desktop utility for keeping running applications quietly out of the way without closing them.
It is the successor to Trayify and is currently in a controlled migration from the proven Trayify v0.3.3 runtime.

## Current state

- Product name and visual direction: **STOW**.
- Approved navigation: Apps, Rules, Focus, Insights, Settings; About is separate at the bottom.
- New UI target: **WPF on modern .NET**; Electron is not used for UI.
- Existing minimize-to-tray engine: Trayify v0.3.3 behavior is the protected compatibility baseline.
- Runtime handoff, per-user installer migration and GitHub updater are validated; signing and package-manager transition are still gated.

The baseline source commit is `c4ab75082d1f7ecbeee32270cc01eea457a93276`.
See [`docs/architecture/BASELINE_CONTRACT.md`](docs/architecture/BASELINE_CONTRACT.md) before changing window-management behavior.

## Repository layout

- `src/STOW.App` — new WPF presentation shell.
- `src/STOW.Engine` — future extracted product engine.
- `src/STOW.Platform.Windows` — Win32/window/tray implementation boundary.
- `src/STOW.Infrastructure` — config, migration, updates, diagnostics and logging.
- `tests` — regression and integration test projects plus the required test plan.
- `src/Trayify.cs` / `src/TrayifySetup.cs` — current v0.3.3 compatibility baseline during migration.

## Design direction

STOW uses a calm navy/blue system with restrained teal accents, Light + Dark themes and Windows 11-inspired spacing/surfaces.
The approved handoff images remain the visual source of truth.
See [`docs/design-system/DESIGN_SYSTEM_V1.md`](docs/design-system/DESIGN_SYSTEM_V1.md).

## Build

New STOW foundation:

```powershell
dotnet build STOW.slnx -c Release
```

Self-contained preview + installer package:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\build-stow.ps1
```

Silent install/upgrade:

```powershell
STOWSetup.exe /VERYSILENT /NOLAUNCH
```

Silent uninstall (settings retained by default):

```powershell
%LOCALAPPDATA%\STOW\Uninstall.exe /UNINSTALL /VERYSILENT
```

Untouched Trayify v0.3.3 compatibility baseline:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\build.ps1
```

## Migration rule

Do not rewrite the working minimize-to-tray engine merely to fit the new UI architecture.
Extract behind interfaces, prove regression parity, then refactor only when a test demonstrates preserved behavior.
See [`docs/migration/TRAYIFY_TO_STOW.md`](docs/migration/TRAYIFY_TO_STOW.md).

## License

MIT. See [`LICENSE`](LICENSE).
