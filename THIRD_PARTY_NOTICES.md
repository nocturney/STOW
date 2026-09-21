# Third-party notices

## STOW

STOW is built with modern .NET and WPF. Self-contained Windows builds redistribute .NET runtime and Windows Desktop runtime components.

Each self-contained STOW distribution includes the license and notice files copied from the exact runtime packs used by the build:

- `DOTNET_RUNTIME_LICENSE.txt`
- `DOTNET_RUNTIME_THIRD_PARTY_NOTICES.txt`
- `DOTNET_WINDOWS_DESKTOP_LICENSE.txt`
- `DOTNET_WINDOWS_DESKTOP_THIRD_PARTY_NOTICES.txt`

STOW does not currently bundle external font files. The UI requests Inter when available and falls back to Windows-provided Segoe UI fonts.

## Trayify v0.3.3 compatibility baseline

The preserved Trayify v0.3.3 application binary does not bundle third-party libraries, frameworks, fonts, icons, or other third-party assets. It uses Windows/.NET Framework system APIs and Win32 APIs supplied by the operating system.

Any new bundled dependency or asset must be reviewed and reflected here before publication.
