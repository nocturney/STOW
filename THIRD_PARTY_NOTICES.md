# Third-party notices

STOW is licensed under the MIT License. That license applies to STOW's own source code and does not relicense third-party components that are distributed with self-contained Windows builds.

## Microsoft .NET and WPF

STOW is published as a self-contained Windows application using Microsoft .NET and Windows Presentation Foundation (WPF).

Each release build records the exact resolved runtime pack versions in `LEGAL_MANIFEST.txt` and carries the license/notice files copied from those exact packs.

The legal bundle includes, as applicable:

- `DOTNET_RUNTIME_LICENSE.txt`
- `DOTNET_RUNTIME_THIRD_PARTY_NOTICES.txt`
- `DOTNET_WINDOWS_DESKTOP_LICENSE.txt`
- `MICROSOFT_DOTNET_LIBRARY_LICENSE.html`
- `MICROSOFT_WINDOWS_SDK_LICENSE.html`

Microsoft's Windows-specific .NET licensing information identifies the following relevant exceptions to the otherwise MIT-licensed .NET files:

- the Windows .NET runtime embedded in single-file applications is governed by the **.NET Library License**;
- WPF native components including `PresentationNative_cor3.dll`, `vcruntime140_cor3.dll` and `wpfgfx_cor3.dll` are governed by the **.NET Library License**;
- `D3DCompiler_47_cor3.dll`, used by WPF and present in STOW's resolved Windows Desktop runtime pack, is governed by the **Windows SDK License**;
- other .NET files are governed by the applicable MIT terms and third-party notices shipped with the runtime.

The Microsoft terms included with STOW and the canonical Microsoft sources govern those components; they are not relicensed under STOW's MIT License. `LEGAL_MANIFEST.txt` records the exact runtime-pack versions and the SHA-256 of the `D3DCompiler_47_cor3.dll` used by the build.

Canonical references are documented in `licenses/README.md`.

## Fonts and visual assets

STOW does not currently bundle external font files. The UI requests Inter only when it is already installed on the user's system and otherwise falls back to Windows-provided Segoe UI fonts.

STOW does not currently bundle third-party image, icon, photograph, illustration or logo files in the application source tree.

## Development/test dependencies

The repository uses test/development packages such as xUnit.net, coverlet and Microsoft.NET.Test.Sdk. Newtonsoft.Json is currently present only as a transitive test dependency.

These packages are not runtime dependencies of the shipping STOW application and are not included in the self-contained STOW executable.

## Trayify compatibility baseline

The preserved Trayify v0.3.3 source/binary baseline exists solely to preserve and regression-test the verified restore behavior during the STOW migration. Historical Trayify references are retained where needed for compatibility and project history.

## Release rule

Any new runtime package, font, image, icon, binary, copied code, generated asset or other externally sourced material must be reviewed for provenance and license obligations before publication. The release legal gate is intended to fail when an unreviewed runtime dependency or asset is introduced.
