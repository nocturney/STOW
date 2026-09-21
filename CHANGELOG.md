# Changelog

All notable changes to Trayify are documented here.

## [0.3.2] - 2026-09-21

### Changed
- Package-manager installations now use a self-healing Windows startup command.
- WinGet/Scoop startup resolves Trayify through the package-manager command path instead of pinning a version-specific executable path.
- If a package-managed Trayify copy is removed while a startup entry remains, the stale startup entry removes itself on the next sign-in.

### Packaging
- v0.3.2 is the package-manager submission release for WinGet and Scoop metadata.
## [0.3.1] - 2026-09-21

### Changed
- WinGet packaging now uses the supported portable installer model instead of executing the unsigned graphical installer unattended.
- Windows startup uses WinGet's stable command link when Trayify is installed by WinGet, so upgrades do not leave a version-specific startup path behind.

### Packaging
- WinGet and Scoop package metadata target the standalone Trayify executable.
- The graphical TrayifySetup.exe remains the recommended interactive installer.
## [0.3.0] - 2026-09-21

### Added
- Native Trayify installer and uninstaller with a clean Windows-style UI.
- Per-user installation to %LOCALAPPDATA% without requiring administrator rights.
- Optional desktop shortcut.
- Optional Start menu shortcut.
- Pin-to-Start assistance that uses Windows-supported surfaces instead of modifying Start internals.
- Windows Apps & Features uninstall registration.
- Silent install and uninstall switches for package managers.
- Modernized Trayify interface with a cleaner header, filtering, spacing, typography, and status summary.
- Official WinGet packaging metadata.
- Scoop packaging metadata.

### Changed
- In-app updates now use the official Trayify installer so uninstall metadata and shortcuts stay synchronized.
- Release builds now produce TrayifySetup.exe with Trayify embedded inside it.
- SHA256SUMS.txt now covers both Trayify.exe and TrayifySetup.exe.
- The ZIP package is centered around the graphical installer rather than command-file installation.

### Security
- Installer payload version is verified before installation.
- In-app updates verify the installer SHA-256 and embedded file version before launching it.
- Pin-to-Start is not implemented through undocumented Start-menu database modifications.

## [0.2.0] - 2026-09-21

### Added
- Secure in-app Download & Install self-update flow.
- SHA-256 verification of downloaded updates before replacement.
- Executable-version verification against the GitHub Release tag.
- Rollback protection if an update cannot be applied.
- MIT open-source license.
- Privacy policy and security policy.
- Third-party notices.
- Contributing, support, and code-of-conduct documentation.
- Bug-report and feature-request issue forms.
- Pull-request template.
- Windows CI workflow.
- Automated tagged-release workflow.
- Uninstall command.
- Help-menu links for License, Privacy, and Security Policy.

### Changed
- Automatic table refresh now preserves selected row, current cell, and scroll position.
- Build/release packaging is driven by the VERSION file.
- Release packages now include license, privacy, security, third-party notices, installer, uninstaller, and checksums.

### Security
- Update replacement occurs only after checksum and version validation.
- Private vulnerability reporting is the preferred security-reporting path.
- Current releases remain unsigned; Authenticode code signing is documented as a future distribution-hardening step.

## [0.1.0] - 2026-09-21

### Added
- Initial Trayify application.
- GUI for selecting user-facing Windows applications.
- Per-application minimize-to-tray toggle.
- Persistent configuration.
- Automatic Windows startup while at least one application is enabled.
- Per-application tray icons with restore and disable actions.
- Support for Electron/Chromium applications that re-show or recreate windows.
- Filtering that hides background/helper windows from the application list.
- Selection, focus and scroll preservation during automatic GUI refreshes.
- Original Trayify application icon.
- File and Help menus.
- About dialog.
- GitHub repository and changelog shortcuts.
- Manual update check against GitHub Releases.


