# Trayify

Trayify is a lightweight Windows utility that adds **minimize-to-tray** behavior to desktop applications that do not provide it themselves.

Current version: **v0.3.0**

## Highlights

- Clean list of user-facing/taskbar applications rather than every background process.
- Per-application minimize-to-tray toggle.
- Persistent local configuration.
- Per-app tray icons with restore/disable actions.
- Electron/Chromium handling for apps that re-show their windows.
- Automatic startup while at least one managed app is enabled.
- Built-in update checks and verified installer-based self-updates.
- No telemetry, analytics, advertising, or tracking.
- Native WinForms/Win32 implementation with no bundled UI framework.

## Installer

The recommended download is **TrayifySetup.exe** from GitHub Releases.

The installer is per-user and does not require administrator rights. It installs Trayify to:

`%LOCALAPPDATA%\Trayify\Trayify.exe`

It provides:

- Optional Desktop shortcut.
- Optional Start menu shortcut.
- Optional Pin-to-Start assistance.
- Proper Windows **Settings > Apps > Installed apps** registration.
- Graphical uninstall with an option to keep or remove Trayify settings.
- Silent install/uninstall modes for package managers.

### Silent install

`TrayifySetup.exe /VERYSILENT /NOLAUNCH`

### Silent uninstall

`%LOCALAPPDATA%\Trayify\Uninstall.exe /UNINSTALL /VERYSILENT`

## Pin to Start

Windows 11 does not expose a supported ordinary-installer API to silently pin a desktop application to Start.

Trayify Setup therefore creates the normal Start menu shortcut and can open Windows' application surface with instructions for the user to choose **Pin to Start**. It does not modify undocumented Start-menu databases.

## Package managers

### WinGet

Trayify includes WinGet manifest metadata and uses `TrayifySetup.exe` as a per-user EXE installer.

Once the package is accepted into the Microsoft community catalog, installation is:

`winget install ChristianVelvet.Trayify`

### Scoop

A Scoop manifest is maintained under `packaging/scoop/trayify.json`.

It installs the standalone executable in portable mode. Portable copies use Scoop for upgrades rather than Trayify's installer-based self-update.

## Updating

For a normal installed copy, choose **Help > Check for Updates**.

Trayify downloads the new official installer and `SHA256SUMS.txt`, verifies the installer checksum and file version, and only then starts the installer.

Portable/package-manager copies should normally be upgraded through their package manager.

## Privacy

Trayify works locally and has no telemetry. See [PRIVACY.md](PRIVACY.md).

## Security

See [SECURITY.md](SECURITY.md).

Current binaries are not Authenticode-signed, so Windows SmartScreen may warn on a newly downloaded release. Published SHA-256 checksums verify integrity, but they are not a substitute for publisher code signing.

## License

Trayify is open source under the [MIT License](LICENSE).

No third-party code or assets are bundled in the current release. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Build

Requirements:

- Windows
- Windows PowerShell
- .NET Framework C# compiler included with Windows

Build with:

`powershell -ExecutionPolicy Bypass -File scripts\build.ps1`

The build produces:

- `Trayify.exe`
- `TrayifySetup.exe`
- `Trayify-vX.Y.Z-win.zip`
- `SHA256SUMS.txt`
- release notes

## Release process

- `VERSION` is the source release number.
- CI builds pushes and pull requests on Windows.
- A matching `vX.Y.Z` tag creates a GitHub Release automatically.
- WinGet and Scoop metadata are updated against the immutable versioned release URLs.

See [CHANGELOG.md](CHANGELOG.md).
