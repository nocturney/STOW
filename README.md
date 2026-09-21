# Trayify

Trayify is a lightweight Windows utility that adds **minimize-to-tray** behavior to desktop applications that do not provide it themselves.

## Features

- Shows only user-facing applications with real taskbar windows, plus apps already configured in Trayify.
- Per-app toggle for minimize-to-tray behavior.
- Remembers enabled applications between runs.
- Automatically starts with Windows while at least one managed app is enabled.
- Restores hidden apps from their tray icon.
- Works around Electron/Chromium apps that recreate or re-show their windows.
- Built-in **Help > Check for Updates** using GitHub Releases.
- Built-in links to the changelog and repository.
- Small native WinForms executable with no Electron runtime.

## Install

### Recommended

1. Download the latest release ZIP.
2. Extract it.
3. Run `Install-Trayify.cmd`.

Trayify will be copied to:

`%LOCALAPPDATA%\Trayify\Trayify.exe`

### Portable

You can also run `Trayify.exe` directly from any folder.  
If you enable an app, Trayify will register its current executable path for Windows startup.

## Usage

1. Open Trayify.
2. Find an application in the list.
3. Enable the **Tray** checkbox.
4. Minimize that application normally.
5. Trayify hides it from the taskbar and exposes it through the system tray.
6. Double-click the app tray icon to restore it.

Configured applications remain visible in Trayify even when they are currently closed or hidden.

## Notes

- Some elevated/admin applications may require Trayify to run elevated before Windows allows it to control their windows.
- Trayify does not terminate managed applications when hiding them.
- Settings are stored under `%APPDATA%\Trayify`.

## Build

Trayify is intentionally kept as a single-file WinForms project.

Run:

`powershell -ExecutionPolicy Bypass -File scripts\build.ps1`

The build script uses the .NET Framework C# compiler already available on Windows and writes release files to `dist`.

## Version

Current release: **v0.1.0**

See [CHANGELOG.md](CHANGELOG.md) for release history.
