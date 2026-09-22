# STOW Live Win32 Parity Gate

The STOW runtime is not considered ready for Trayify replacement based on mocked tests alone.
A dedicated interactive harness exercises the real Windows API path on a real desktop session.

## Harness

Project: `tests/STOW.LiveParityHarness`
Target app: `tests/STOW.WindowTestTarget`
Runner: `scripts/run-live-parity.ps1`

The harness must run in an interactive Windows session. Session 0 is intentionally rejected because DWM can cloak GUI windows there, which would make user-facing window discovery behave differently from the real desktop.

## Required scenarios

1. Real HWND: minimize -> detect iconic state -> hide -> track -> restore the original HWND.
2. Electron-style recreation: minimize -> hide -> destroy original HWND -> create a new hidden HWND in the same PID -> restore the replacement by tracked PID/title/class.
3. A successful restore must remove tray tracking.
4. No visibility filter may be introduced into hidden-window restore resolution.

## Verified result

Verified again on 2026-09-22 in the interactive user session:

- `PASS minimize-hide-restore`
- `PASS electron-hwnd-recreation`
- `PASS rule-keep-visible`
- `PASS focus-hide-end-restore`
- `RESULT=PASS`
- `EXIT_CODE=0`

This supplements the deterministic unit/integration suite; it does not replace it.

## Production handoff rule

Do not force-kill Trayify while it may be protecting hidden applications.
A production handoff to STOW must first ask Trayify to exit through its own safe shutdown path so Trayify restores every hidden managed application or refuses to exit.
Only after Trayify has fully exited may STOW become the active runtime.
