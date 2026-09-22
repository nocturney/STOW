# Privacy

STOW is designed to work locally.

## Local data

STOW does not include its own analytics, advertising, telemetry, user profiling or cloud account.

STOW stores product data locally under:

`%APPDATA%\STOW`

That local data can include:

- `config.txt` — managed applications and the identifiers needed to recognize their windows;
- `rules.json` — app rules created by the user;
- `focus-presets.json` — saved Focus presets containing local names and managed-app keys to keep visible;
- `focus-schedules.json` — recurring Focus schedules, their saved-preset references and the last locally consumed occurrence marker used to prevent duplicate schedule triggers;
- `settings.json` — product preferences such as theme, accessibility text/contrast/font choices, startup, Focus behavior and whether local scheduled-Focus notifications are enabled;
- `activity.jsonl` — bounded local activity history used by Insights;
- `update-check.txt` — the time of the last explicit update check;
- `legal-acceptance.txt` — the local terms revision, acceptance timestamp and acceptance source used to avoid repeatedly asking for the same license/third-party-term acknowledgment;
- migration markers used to preserve a safe upgrade path from Trayify.

Insights are calculated from local history. STOW does not upload configuration, rules, settings or activity history.

## Network access

STOW directly contacts GitHub only when the user invokes a feature that needs GitHub data, including:

- **Check for Updates**;
- **Release Notes** / **What's New**.

Those requests read public STOW release metadata from GitHub. STOW identifies itself with a User-Agent containing the STOW application version. Normal internet metadata such as the user's IP address is visible to GitHub as part of the network connection and is governed by GitHub's own policies.

If the user approves an update for a standard installed copy, STOW downloads the official `STOWSetup.exe` and `SHA256SUMS.txt` from the matching versioned GitHub Release, verifies them locally, and launches the installer.

STOW does not upload its local app list, rules, settings, activity history or diagnostics as part of update or release-note requests.

Opening a project/support link launches the user's default browser; the destination site then operates under its own privacy terms. Privacy, STOW License, third-party notices and applicable bundled Microsoft terms are available inside STOW without opening a browser.

Package managers such as WinGet or Scoop may independently contact their own package sources according to those tools' normal behavior.

## Operating-system services

When the user enables scheduled-Focus notifications, STOW uses its local Windows notification-area (system tray) icon to show start/end notifications. STOW does not send notification contents to a STOW cloud service, and notification failure does not affect Focus behavior.

STOW does not implement its own crash-reporting service. Windows and the .NET runtime can participate in operating-system features such as Windows Error Reporting or security/reputation checks according to the user's Windows settings and organizational policy. Those services are provided by Microsoft, not by STOW.

## Diagnostics

About > Diagnostics builds a local text summary and copies it to the clipboard. STOW does not automatically send that diagnostic summary anywhere.

## Deleting local data

The interactive uninstaller offers **Also remove my STOW settings**. Silent uninstall can request the same behavior with `/REMOVESETTINGS=1`.

If settings are retained during uninstall, the user can delete `%APPDATA%\STOW` manually later.

## Third parties

GitHub's privacy practices apply when STOW connects to GitHub or opens GitHub in the user's browser. Microsoft/Windows privacy and administrative settings apply to operating-system services such as Windows Error Reporting and security reputation checks.
