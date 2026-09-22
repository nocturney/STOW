# Contributing to STOW

Thanks for considering a contribution.

## Before opening a change

- Search existing issues first.
- For bugs, include the Windows version, STOW version, affected application, and exact reproduction steps.
- For behavior changes or new features, prefer opening a feature request before a large implementation.
- Treat the Trayify v0.3.3 restore behavior as a compatibility contract until an explicitly reviewed replacement is proven safer.

## Development

STOW is intentionally native, local-first, and dependency-light.

Requirements:

- Windows
- Git
- .NET SDK matching `global.json`

Build and test with:

`dotnet build STOW.slnx -c Release`

`dotnet test STOW.slnx -c Release --no-build`

## Contribution licensing and provenance

By submitting a contribution to STOW, you represent that you have the right to submit it and you agree that your contribution is licensed under the same MIT License that covers STOW, unless a different license is explicitly agreed in writing before submission.

Do not copy code, fonts, icons, images, generated assets or other material from another project unless its license permits the intended use and the required attribution/notice material is included. If provenance is uncertain, do not include the material.

## Pull requests

A pull request should:

- Explain the user-facing problem and the proposed solution.
- Keep minimize/restore behavior safe: a failure must not orphan or terminate a managed application.
- Preserve local-only configuration and avoid adding telemetry.
- Keep UI code separated from raw Win32 window-management logic.
- Update user-facing documentation for visible behavior changes.
- Update `THIRD_PARTY_NOTICES.md` before release if any third-party code, runtime, font, or asset is bundled.
- Pass the Windows CI build and relevant regression tests.

## Style

Prefer straightforward modern .NET/WPF code in the UI and narrowly scoped Win32 adapters in the Windows platform layer. Avoid adding dependencies unless they materially improve reliability, accessibility, or security.
