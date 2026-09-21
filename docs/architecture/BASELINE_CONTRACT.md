# Trayify v0.3.3 Baseline Contract

STOW inherits the proven minimize-to-tray engine from Trayify v0.3.3.
The source baseline is commit `c4ab75082d1f7ecbeee32270cc01eea457a93276` and tag `v0.3.3`.
A second immutable marker, `trayify-v0.3.3-baseline`, identifies the handoff point.

## Non-regression rules

1. Prefer the exact HWND that STOW/Trayify originally hid.
2. Track PIDs for every app placed into tray mode.
3. If Electron/Chromium recreates its HWND, restore by enumerating windows for the tracked PID.
4. Never resolve a hidden restore target through the user-facing app enumeration; that enumeration intentionally filters invisible windows.
5. Do not stop tray enforcement until a valid restore target exists.
6. On restore failure, restore the hidden PID/HWND tracking state and keep the tray icon.
7. Disabling management for a hidden app must first restore it successfully.
8. Application exit must not leave a managed application orphaned and invisible.
9. Re-showing/recreated windows belonging to a hidden PID must be re-hidden while tray mode is active.
10. UI refactors must not change this behavior unless a dedicated regression test proves parity.

## Verified build

The untouched baseline builds successfully with `scripts/build.ps1` on Windows.
Baseline build SHA-256:
- `Trayify.exe`: `332eb69da41b2f4dad0ae38416b784cf8484622a91a4d8fd4d81e61901112812`
- `TrayifySetup.exe`: `31be37237419e5a5befc20648a29e61c79d3581dcaee08ff8cb0845673957db5`

These hashes are build evidence, not future release hashes.

## Migration principle

The engine is a protected asset, not a rewrite target.
The first STOW implementation should extract the existing Win32 behavior behind a narrow interface, then replace presentation and product identity around it.
