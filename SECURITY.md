# Security Policy

## Supported versions

Security fixes are provided for the latest released version of Trayify.

| Version | Supported |
| --- | --- |
| Latest release | Yes |
| Older releases | No |

## Reporting a vulnerability

Please **do not open a public issue containing vulnerability details**.

Use GitHub's private vulnerability reporting feature for this repository:

1. Open the repository's **Security** page.
2. Choose **Report a vulnerability**.
3. Include reproduction steps, affected versions, and the potential impact.

Reports will be reviewed and handled through GitHub Security Advisories when appropriate.

## Update integrity

Standard installed copies of Trayify update through the official `TrayifySetup.exe` published in this repository's versioned GitHub Releases.

Before launching a downloaded update, Trayify verifies:

- the installer SHA-256 against the release's `SHA256SUMS.txt`;
- the installer FileVersion against the GitHub release tag.

The installer separately verifies the embedded Trayify payload version before replacing the installed application.

## Installation model

The official installer is per-user and installs under `%LOCALAPPDATA%\Trayify`, so normal installation does not require elevation.

Trayify does not use undocumented Start-menu database modifications to force pins.

## Code signing

Current releases are not Authenticode-signed. SHA-256 verification protects update integrity, but it is not a substitute for publisher code signing.
