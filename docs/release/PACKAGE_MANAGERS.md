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
