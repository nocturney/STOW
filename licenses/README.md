# Bundled Microsoft license references

This folder preserves offline snapshots of Microsoft license terms relevant to Windows self-contained STOW releases.

Canonical sources:

- .NET Library License: https://dotnet.microsoft.com/en-us/dotnet_library_license.htm
- Windows SDK License: https://learn.microsoft.com/en-us/legal/windows-sdk/license
- .NET Windows license information: https://github.com/dotnet/core/blob/main/license-information-windows.md
- .NET asset licensing model: https://github.com/dotnet/runtime/blob/main/docs/project/licensing-assets.md

Snapshots in this folder were retrieved on 2026-09-22 for release-compliance review. The canonical Microsoft terms remain authoritative.

STOW's build also copies the license and third-party-notice files from the exact .NET runtime packs resolved for each release. Those generated copies and their SHA-256 values are recorded in `LEGAL_MANIFEST.txt`.

For Windows self-contained WPF builds, Microsoft's Windows license information identifies specific native binaries under non-MIT terms. STOW therefore records the exact `D3DCompiler_47_cor3.dll` sourced from the resolved Windows Desktop runtime pack and its SHA-256; Microsoft identifies that binary as governed by the Windows SDK License. The same Microsoft document identifies the single-file Windows runtime and WPF native components such as `PresentationNative_cor3.dll`, `vcruntime140_cor3.dll` and `wpfgfx_cor3.dll` as governed by the .NET Library License.

These materials are provided to preserve attribution and applicable third-party terms. They are not a claim that Microsoft endorses STOW.
