# STOW Package Managers

STOW keeps WinGet and Scoop installs in portable/package-managed mode. The package managers install `STOW.exe` directly rather than invoking `STOWSetup.exe`.

## Why portable mode

- WinGet/Scoop remain the authority for upgrades and uninstall.
- Package-managed executables live under package-manager paths that STOW recognizes.
- STOW therefore does not launch its own installer updater for those copies.
- Startup registration resolves the package-manager command path instead of pinning a version-specific binary.
- The standard per-user installer remains reserved for `%LOCALAPPDATA%\STOW\STOW.exe` installations.

## Package identities

- WinGet target identity: `ChristianVelvet.STOW`.
- Scoop manifest name: `stow`.
- STOW is a new package identity; Trayify metadata is not silently repurposed.

## Metadata generation

Generate manifests only after the final release binary has been built (and signed for a signed release), because signing changes the SHA-256:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\generate-package-metadata.ps1 -Version 1.0.0
```

The generator reads the `STOW.exe` hash from `dist-stow\SHA256SUMS.txt`, then writes WinGet and Scoop metadata under `packaging\generated` using the GitHub release URL for `v<version>`.
Prerelease metadata is blocked by default; use `-AllowPrerelease` only for an intentional preview-package submission.

Do not submit generated metadata until the corresponding GitHub release assets exist at the generated URLs.

## Legal/distribution behavior

Portable WinGet/Scoop installs bypass `STOWSetup.exe`, so STOW itself enforces the current `LEGAL_TERMS_REVISION` before the runtime/tray engine starts. The first-run window provides offline access to the STOW MIT License, privacy notice, credits, runtime notices, Microsoft .NET Library License and Windows SDK License, and requires explicit acknowledgment before continuing.

The generated WinGet metadata adds documentation links plus an installation note describing the one-time acknowledgment. The Scoop manifest includes the same disclosure in its notes. This avoids relying on WinGet's optional Agreements field, whose use in the community repository has publisher/verification constraints.

Package-manager publication still requires the normal release gates: exact released asset URL/hash, legal/SBOM verification, applicable repository/store policy compliance, and Stable signing policy when publishing a Stable STOW release.
