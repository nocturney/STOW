# STOW Screen Contracts

The screen set is fixed: Apps, Rules, Focus, Insights, Settings and About.
About is not nested under Settings.

## Apps

Primary product home.
Show Managed Apps first, then discoverable/available desktop apps.
Support search, enable/disable management, current state and direct restore where relevant.
Do not expose background helper processes as ordinary apps.
Empty state should teach the minimize-to-tray value without onboarding clutter.

## Rules

List rules on the left/center and edit the selected rule in a dedicated panel.
Rules must describe automation intent in plain English and expose enabled state clearly.
Validation errors stay close to the edited condition/action.

Current operational slice:
- per-app rules for the `Minimize` trigger;
- actions: `Stow` and `KeepVisible`;
- enabled/disabled state;
- 0-100 priority with the highest-priority matching enabled rule winning;
- local atomic persistence in `%APPDATA%\\STOW\\rules.json`;
- default/failure fallback remains `Stow`, preserving the Trayify v0.3.3 behavior contract.

Focus and Startup rule categories remain visible in the information architecture but are intentionally disabled until those rule triggers are integrated with their corresponding runtimes. They must not appear functional before then.

## Focus

Use the approved Focus composition as the visual baseline.
Focus should activate a temporary calmer desktop state without changing permanent app configuration unless the user explicitly saves it.
Always show whether a Focus session is active and how to stop it.

Current operational slice:
- Start/End Focus is backed by the live tray engine.
- The user selects enabled managed apps to keep visible; other visible managed apps are stowed for the session.
- Apps launched while Focus is active are stowed on the next engine scan unless they are in Keep visible.
- Manual restore during Focus adds that app to Keep visible for the remainder of the session.
- STOW tracks which apps were stowed specifically by the Focus session. End Focus restores only those apps; apps that were already stowed before Focus remain stowed.
- If any Focus-hidden app cannot be restored, Focus remains active and keeps its tracking instead of orphaning the window.
- Deep work and Keep all visible are temporary setup helpers only; they do not modify permanent app configuration.
- Saved presets are operational and persisted atomically in `%APPDATA%\STOW\focus-presets.json`; each preset stores only a user-visible name and the managed-app keys to keep visible.
- Using a saved preset ignores app keys that are no longer enabled/managed instead of failing the Focus flow.
- Saving/deleting presets never changes the permanent managed-app configuration.
- Recurring Focus scheduling is operational and persisted atomically in `%APPDATA%\\STOW\\focus-schedules.json`.
- A schedule references a saved Focus preset, selected local weekdays, a local start time and a 5–720 minute duration.
- The scheduling runtime runs while STOW is running, checks local-time occurrences, and marks each occurrence before starting Focus so a crash/restart cannot repeatedly stow apps for the same occurrence.
- If STOW starts during an unconsumed active schedule window, that occurrence can still begin; occurrences fully missed while STOW was not running are not replayed later.
- Manual Focus takes priority over overlapping schedules; overlapping schedule occurrences are consumed rather than retriggered after the manual session ends.
- If multiple schedules overlap, the first runnable occurrence starts and the others are consumed while that Focus session is active.
- A schedule whose referenced preset is unavailable is surfaced as unavailable without blocking another valid due schedule.
- Scheduled Focus uses the normal configured Focus-end behavior and never weakens the existing restore-safety contract.
- No fabricated distraction score or recommendation metric is shown.

## Insights

Local-only by default.
Show useful desktop/tray behavior such as apps most often stowed, restored, or kept out of the way.
Do not require telemetry or a cloud account.
When insufficient local history exists, show an honest empty state rather than fabricated metrics.

Current operational slice:
- runtime activity is appended locally to `%APPDATA%\STOW\activity.jsonl`;
- recorded event types are app stow, app restore, Focus start and Focus end;
- event writes are best-effort observers and can never block or change tray/restore behavior;
- malformed or partially written history lines are skipped rather than making Insights unusable;
- the local history file is compacted when it grows beyond the bounded storage threshold;
- Insights shows real 7-day stow/restore/Focus counts, most-stowed app, retained-history range and recent events;
- restore events retain a local source label such as Manual, Focus, Shutdown, Disable or Remove;
- when no history exists, the screen remains an honest local-only empty state.

## Settings

Only mutable product settings belong here.
Examples: startup behavior, theme, notification preferences and compatible update behavior.
Do not place product/legal/reference documents here.

Current operational slice:
- settings are persisted atomically in `%APPDATA%\STOW\settings.json`;
- defaults preserve the existing behavior: keep running in tray, start with Windows, System theme and restore the previous desktop when Focus ends;
- General controls whether closing the manager window keeps STOW running in the tray or safely exits the app;
- Startup controls Windows startup registration, still gated by whether at least one managed app is enabled;
- Appearance persists System / Light / Dark and applies it immediately; the saved preference is restored on next launch;
- Focus behavior can either restore the previous desktop or leave Focus-hidden apps stowed when the session ends;
- Notifications remain visibly unavailable until a real notification runtime exists; no decorative toggle is presented as functional;
- Privacy, updates, license and notices remain in About rather than Settings.

## About

Standalone navigation destination anchored separately at the bottom.
Required sections:
- Version / Build / Channel
- Updates
- What's New
- Privacy
- License
- Open-source notices
- Diagnostics
- Support
- GitHub / Releases

Current operational slice:
- Version, build identity and Stable/Preview channel are read from the running binary;
- the last explicit update-check time is stored locally;
- Check for Updates uses GitHub Releases and preserves package-manager ownership for package-managed copies;
- standard installed copies can download `STOWSetup.exe` and `SHA256SUMS.txt`, verify SHA-256 plus installer version identity, and only then launch the installer;
- Release Notes opens an internal STOW window, fetches the exact current release body from the official GitHub Releases API, and renders it locally; GitHub is only a secondary opt-in link/fallback;
- Privacy and License open copies embedded in the running STOW binary, so they remain available offline;
- Open-source notices & credits opens the bundled notices/credits/runtime-license summary, including the applicable Microsoft .NET Library and Windows SDK terms, and can open the installed `%LOCALAPPDATA%\STOW\legal` folder for the full legal bundle;
- before runtime/tray management begins, STOW enforces the current local legal-terms revision once per user; portable/package-manager copies use the same in-app acknowledgment path as installer copies;
- Support opens the STOW issue tracker;
- GitHub & Releases opens the STOW source repository;
- Diagnostics creates a local summary of build/runtime/install-mode and STOW data-file presence, copies it to the clipboard, and performs no network upload;
- active policy/community documents must describe STOW; Trayify references remain only where they are explicitly historical or compatibility-related.

## Cross-screen requirements

Support Light and Dark themes, high DPI and keyboard navigation.
No critical action may be encoded by color alone.
Destructive or compatibility-sensitive operations must explain their consequence before execution.
