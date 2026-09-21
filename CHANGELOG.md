# Changelog

All notable changes to Trayify are documented here.

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
