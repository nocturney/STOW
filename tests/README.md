# STOW Regression Test Plan

The test projects exist now, but behavioral tests are added only as the Trayify v0.3.3 engine is extracted.
Do not treat an empty scaffold test as evidence of parity.

## Required engine scenarios

1. Ordinary Win32 app: minimize -> hide -> tray icon -> restore original HWND.
2. Electron/GrokBot: hidden app recreates its main HWND -> restore through tracked PID.
3. Electron/GrokBot: hidden process re-shows a window -> enforcement hides it again.
4. Disable while hidden -> successful restore first, then disable management.
5. Restore target unavailable -> preserve PID/HWND tracking and tray icon.
6. Managed process exits while hidden -> tracking and tray icon are cleaned up.
7. STOW exits while apps are hidden -> no invisible orphan process remains.
8. Multiple windows for one managed process -> restore selection remains deterministic and safe.
9. PID reuse must never restore an unrelated process after the original process has exited.
10. UI app enumeration continues to filter non-user-facing/helper windows.

## Migration scenarios

- Trayify config imports once into an empty STOW profile.
- Existing STOW config is never overwritten by legacy data.
- Interrupted migration leaves Trayify data intact.
- Startup entry changes only after STOW installation validates successfully.
- Upgrade never leaves Trayify and STOW both configured to auto-start.
