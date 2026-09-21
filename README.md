# Trayify

**Trayify** is a lightweight Windows utility that adds **minimize-to-tray** behavior to desktop applications that do not provide it themselves.

Current version: **v0.2.0**

## Features

- Shows user-facing/taskbar applications instead of every background process.
- Keeps previously configured applications visible even when they are closed or hidden.
- Per-application minimize-to-tray toggle.
- Per-application tray icon with Restore and Disable actions.
- Persistent local configuration.
- Automatic Windows startup while at least one managed application is enabled.
- Electron/Chromium handling for applications that re-show or recreate windows.
- Built-in update checker and **Download & Install** self-update flow.
- SHA-256 verification before an update replaces the installed executable.
- Built-in links to the changelog, license, privacy policy, security policy, and repository.
- No telemetry, analytics, advertising, or tracking.
- Small WinForms executable with no bundled third-party runtime.

## Requirements

- Windows 10 or Windows 11
- .NET Framework available with Windows
- Internet access is **not** required for normal use; it is used only when the user explicitly checks for updates.

## Install

### Recommended

1. Download the latest `Trayify-vX.Y.Z-win.zip` from GitHub Releases.
2. Extract it.
3. Run `Install-Trayify.cmd`.

Trayify is installed to:

`%LOCALAPPDATA%\Trayify\Trayify.exe`

User settings are stored separately under:

`%APPDATA%\Trayify`

### Portable

`Trayify.exe` can also be run directly from any writable folder.

If at least one application is enabled, Trayify registers its current executable path under the current user's Windows startup settings.

## Updating

Choose **Help > Check for Updates**.

When a newer GitHub Release exists, Trayify can:

1. Download the new `Trayify.exe`.
2. Download the release's `SHA256SUMS.txt`.
3. Verify the executable checksum.
4. Verify that the executable file version matches the release tag.
5. Launch a temporary updater.
6. Exit Trayify safely.
7. Replace the old executable with rollback protection.
8. Restart Trayify.

If verification fails, the installed executable is not replaced.

## Uninstall

Run `Uninstall-Trayify.cmd` from the release package.

The uninstaller removes Trayify and its Windows startup entry. User settings under `%APPDATA%\Trayify` are left in place so a later reinstall can reuse them.

## Privacy

Trayify has no telemetry or tracking. See [PRIVACY.md](PRIVACY.md).

## Security

See [SECURITY.md](SECURITY.md).

Current binaries are **not Authenticode-signed**, so Windows SmartScreen may warn on a newly downloaded release. Release checksums are published for integrity verification; checksum verification is not a substitute for publisher code signing.

## Open-source license

Trayify is released under the [MIT License](LICENSE).

Trayify currently bundles no third-party libraries or assets. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Bug reports and feature requests use the repository's issue templates.

## Build

Trayify is intentionally dependency-light and kept as a single-file WinForms application.

Run:

`powershell -ExecutionPolicy Bypass -File scripts\build.ps1`

The build creates release artifacts under `dist\`.

## Release process

- `VERSION` is the source of the release number.
- The build verifies that `VERSION` matches the version embedded in the C# source.
- CI builds every push to `main` and every pull request.
- Pushing a matching `vX.Y.Z` tag triggers the release workflow.

See [CHANGELOG.md](CHANGELOG.md) for release history.
