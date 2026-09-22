# STOW

**A calmer desktop starts here.**

STOW is a Windows desktop utility for keeping running applications quietly out of the way without closing them.
It is the successor to Trayify and is currently in a controlled migration from the proven Trayify v0.3.3 runtime.

## Current state

- Product name and visual direction: **STOW**.
- Approved navigation: Apps, Rules, Focus, Insights, Settings; About is separate at the bottom.
- UI/runtime: **WPF on modern .NET**; Electron is not used for UI.
- Apps, Rules, Focus, Insights, Settings and About now have operational runtime-backed slices.
- Existing minimize-to-tray engine: Trayify v0.3.3 behavior remains the protected compatibility baseline.
- Per-user installer migration, unsigned Preview releases and the GitHub self-updater are validated end to end.
- Stable Authenticode signing and final package-manager publication remain gated.

The baseline source commit is `c4ab75082d1f7ecbeee32270cc01eea457a93276`.
See [`docs/architecture/BASELINE_CONTRACT.md`](docs/architecture/BASELINE_CONTRACT.md) before changing window-management behavior.

## Repository layout

- `src/STOW.App` — new WPF presentation shell.
- `src/STOW.Engine` — product contracts, rules, settings and runtime state models.
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

Signed release package (requires a valid Code Signing certificate and Windows SDK Signing Tools):

```powershell
$env:STOW_SIGNING_CERT_THUMBPRINT = '<thumbprint>'
powershell -ExecutionPolicy Bypass -File scripts\build-stow.ps1 -Sign
```

Silent install/upgrade:

```powershell
STOWSetup.exe /VERYSILENT /NOLAUNCH /ACCEPTLICENSES=1
```

For a first-time silent install, `/ACCEPTLICENSES=1` records explicit acceptance of STOW's MIT License and the applicable bundled third-party terms. Existing installed copies can upgrade silently without repeating that flag. STOW also checks the current legal-terms revision on application startup, so portable/package-manager installs and upgrades from older builds receive the same one-time acknowledgment flow.

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

## License, privacy and third-party terms

STOW's own source code is MIT licensed. See [`LICENSE`](LICENSE).

Self-contained Windows builds redistribute Microsoft .NET/WPF components under their applicable terms. Release packages include the exact runtime-pack licenses/notices, Windows-specific Microsoft license references, credits and a machine-generated `LEGAL_MANIFEST.txt`.

See:

- [Third-party notices](THIRD_PARTY_NOTICES.md)
- [Credits and attributions](CREDITS.md)
- [Privacy](PRIVACY.md)
- [Security](SECURITY.md)
- [Legal/release compliance checklist](docs/release/LEGAL_AND_RELEASE_COMPLIANCE.md)

Release builds are checked with:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\verify-release-legal.ps1
```
