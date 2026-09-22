# Privacy

STOW is designed to work locally.

## Data collection

STOW does not include analytics, advertising, telemetry, crash reporting, or user tracking.

STOW stores product data locally under:

`%APPDATA%\STOW`

That local data can include:

- `config.txt` — managed applications and the identifiers needed to recognize their windows;
- `rules.json` — app rules created by the user;
- `settings.json` — product preferences such as theme, startup and Focus behavior;
- `activity.jsonl` — bounded local activity history used by Insights;
- migration markers used to preserve a safe upgrade path from Trayify.

Insights are calculated from this local history. STOW does not upload that history.

## Network access

STOW makes a network request when the user explicitly chooses **Check for Updates** in About.

That request goes to GitHub to read STOW release information. If the user approves an update for a standard installed copy, STOW downloads the official `STOWSetup.exe` and `SHA256SUMS.txt` from the matching versioned GitHub Release, verifies them locally, and launches the installer.

No STOW configuration, rules, settings, or activity history is uploaded as part of the update check.

Package managers such as WinGet or Scoop may independently contact their own package sources according to those tools' normal behavior.

## Third parties

GitHub's own privacy and logging practices apply when STOW connects to GitHub.
