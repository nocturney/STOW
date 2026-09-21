# Contributing to Trayify

Thanks for considering a contribution.

## Before opening a change

- Search existing issues first.
- For bugs, include the Windows version, Trayify version, affected application, and exact reproduction steps.
- For behavior changes or new features, prefer opening a feature request before a large implementation.

## Development

Trayify is intentionally small and dependency-light.

Requirements:

- Windows
- Git
- Windows PowerShell
- .NET Framework C# compiler available under `%WINDIR%\Microsoft.NET\Framework*`

Build with:

`powershell -ExecutionPolicy Bypass -File scripts\build.ps1`

## Pull requests

A pull request should:

- Explain the user-facing problem and the proposed solution.
- Keep minimize/restore behavior safe: a failure should not terminate a managed application.
- Preserve local-only configuration and avoid adding telemetry.
- Update `CHANGELOG.md` for user-visible changes.
- Update `THIRD_PARTY_NOTICES.md` before release if any third-party code or asset is bundled.
- Pass the Windows CI build.

## Style

Prefer straightforward WinForms/Win32 code and standard-library APIs. Avoid adding dependencies unless they materially improve reliability or security.
