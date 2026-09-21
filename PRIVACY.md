# Privacy

Trayify is designed to work locally.

## Data collection

Trayify does not include analytics, advertising, telemetry, crash reporting, or user tracking.

Trayify stores its application configuration locally under:

`%APPDATA%\Trayify`

That configuration contains the applications the user has chosen to manage and the identifiers needed to recognize their windows.

## Network access

Trayify makes a network request only when the user explicitly chooses **Help > Check for Updates**.

That request goes to GitHub to read the latest release information and, when the user approves an update, to download the release executable and its published SHA-256 checksum.

No Trayify configuration is uploaded as part of the update check.

## Third parties

GitHub's own privacy and logging practices apply when Trayify connects to GitHub.
