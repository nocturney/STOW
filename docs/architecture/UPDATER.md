# STOW Updater Contract

STOW checks GitHub Releases from `nocturney/STOW` and never replaces the installed executable directly.
A verified `STOWSetup.exe` is the only automatic update path for the standard per-user installation.

## Release selection

- Versions use Semantic Versioning, including prerelease identifiers.
- Preview builds follow the Preview channel and may receive prerelease or stable releases.
- Stable builds ignore prereleases, even if a GitHub release is accidentally not marked as prerelease.
- Draft releases are ignored.
- The updater examines published releases and selects the highest eligible SemVer.

## Required release assets

An automatically installable release must contain both:

- `STOWSetup.exe`
- `SHA256SUMS.txt`

The updater uses the exact asset download URLs returned by the GitHub API.

## Verification before launch

Before setup is started, STOW must verify all of the following:

1. `SHA256SUMS.txt` contains a SHA-256 entry for `STOWSetup.exe`.
2. The downloaded installer hash exactly matches that entry.
3. The installer `FileVersion` matches the numeric portion of the release tag.
4. The installer `ProductVersion` matches the full release SemVer, including prerelease identifiers.

Any mismatch aborts safely. The installed STOW binary is not replaced by the updater itself.

## Installation behavior

- Standard install path: `%LOCALAPPDATA%\STOW\STOW.exe`.
- Verified updates launch `STOWSetup.exe /VERYSILENT /LAUNCH`.
- The installer then requests STOW safe shutdown through `Local\STOWRequestExit` before replacement.
- Portable or package-managed copies do not self-update through the installer path; their release page/package manager is used instead.
- The last successful update check timestamp is stored locally under `%APPDATA%\STOW`.
- About > Release Notes uses the same public GitHub Releases metadata endpoint to retrieve the exact current release name/body/date into an internal STOW window. That flow never downloads or launches an installer.
- No telemetry or cloud account is required.

## Verified live update — 2026-09-22

The production updater path was exercised end to end against published GitHub prereleases:

- installed source: `0.4.0-preview.1` from `%LOCALAPPDATA%\STOW\STOW.exe`;
- detected release: `v0.4.0-preview.2`;
- downloaded `STOWSetup.exe` and `SHA256SUMS.txt` through `GitHubReleaseUpdater`;
- verified SHA-256, installer `FileVersion=0.4.0.0`, and `ProductVersion=0.4.0-preview.2`;
- launched the verified safe installer;
- installer exited the running STOW instance safely, upgraded it, and relaunched STOW;
- installed result: `0.4.0-preview.2+4f29b11f35790a3aedcf4ae6d7164db6c5357aa1`;
- `%APPDATA%\STOW\config.txt` retained an identical SHA-256 before and after update;
- Apps & Features registered `0.4.0-preview.2`;
- Windows startup remained `"%LOCALAPPDATA%\STOW\STOW.exe" --background`;
- the upgraded STOW process was running from the standard per-user install path.

This validates the unsigned-preview update path without weakening the separate requirement that stable releases be Authenticode-signed.
