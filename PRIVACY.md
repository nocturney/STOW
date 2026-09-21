# Privacy

Trayify is designed to work locally.

## Data collection

Trayify does not include analytics, advertising, telemetry, crash reporting, or user tracking.

Trayify stores its application configuration locally under:

`%APPDATA%\Trayify`

That configuration contains the applications the user has chosen to manage and the identifiers needed to recognize their windows.

## Network access

Trayify makes a network request only when the user explicitly chooses **Help > Check for Updates**.

That request goes to GitHub to read the latest release information. If the user approves an update, Trayify downloads the official `TrayifySetup.exe` and `SHA256SUMS.txt` from the matching versioned GitHub Release, verifies them locally, and launches the installer.

No Trayify configuration is uploaded as part of the update check.

Package managers such as WinGet or Scoop may independently contact their own package sources according to those tools' normal behavior.

## Third parties

GitHub's own privacy and logging practices apply when Trayify connects to GitHub.
