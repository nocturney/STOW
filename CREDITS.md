# Credits and attributions

STOW is developed by Christian Velvet and distributed under the MIT License.

## Project lineage

STOW is the successor to Trayify. The Trayify v0.3.3 source and regression baseline are preserved in this repository to protect the verified Windows restore behavior during the STOW migration. Historical Trayify references in architecture, migration and changelog material are intentional.

## Runtime components distributed with STOW

STOW is published as a self-contained Windows application using Microsoft .NET and WPF. The exact runtime pack versions used by each release are recorded in the generated `LEGAL_MANIFEST.txt`.

The release bundle carries the applicable .NET runtime licenses, third-party notices and the Windows-specific Microsoft license references described in `THIRD_PARTY_NOTICES.md`.

## Development and test tooling

The repository uses development/test tooling including xUnit.net, coverlet and Microsoft.NET.Test.Sdk. Newtonsoft.Json is currently present only transitively through test tooling.

These packages are not runtime dependencies of the shipping STOW application and are not bundled into `STOW.exe`.

## Fonts and visual assets

STOW does not bundle external font files. The UI requests Inter when it is already available on the user's system and otherwise falls back to Windows-provided Segoe UI fonts.

The STOW application icon and related brand artwork in `assets/source/STOW-icon-master.png`, `assets/STOW.png`, `assets/STOW.ico` and `assets/STOWHero.png` are original project artwork created specifically for STOW from the approved STOW visual concept. They do not incorporate third-party logos, photos, illustrations or icon packs. The shipped icon and transparent hero treatment can be reproduced from the approved master with `scripts/generate-stow-icon.py` and `scripts/generate-stow-hero.py`; Pillow is used only as development tooling and is not bundled with STOW.

Historical Trayify artwork remains in the repository solely for the preserved Trayify compatibility baseline and is not used as the STOW application identity.

## Services and product names

STOW uses the public GitHub Releases API for explicit update checks and release-note retrieval. No GitHub client SDK is bundled.

Windows, .NET, WPF, GitHub, WinGet, Scoop and other third-party names are the property of their respective owners. Their names are used only to identify compatibility, distribution channels or services. No endorsement is implied.
