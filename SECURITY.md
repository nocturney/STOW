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

Trayify downloads updates only from this repository's GitHub Releases. Before replacing the installed executable, it verifies the downloaded `Trayify.exe` against the SHA-256 value published with that release.

## Code signing

Current releases are not Authenticode-signed. SHA-256 verification protects the Trayify update process against accidental corruption or mismatched release files, but it is not a substitute for publisher code signing.
