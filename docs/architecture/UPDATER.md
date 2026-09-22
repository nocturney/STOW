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
- No telemetry or cloud account is required.
