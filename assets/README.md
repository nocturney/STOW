# Visual asset provenance

## STOW brand artwork

- `STOW.png` — project-owned raster master for the approved STOW application mark.
- `STOW.ico` — multi-resolution Windows icon derived from the same STOW artwork.
- `STOWHero.png` — transparent hero artwork used by Focus and About.
- Source concept: the approved STOW visual direction (layered blue/teal window cards entering a tray/container).
- Generation: `scripts/generate-stow-icon.py` and `scripts/generate-stow-hero.py` using Pillow as development tooling.
- Third-party visual content: none. No external logo, stock image, icon pack or font file is embedded in these assets.

The STOW icon is used as the application, taskbar, main tray and installer identity.

## Historical Trayify assets

`Trayify.png` and `Trayify.ico` are retained only for the preserved Trayify compatibility baseline. They are not the STOW application identity and must not be wired into STOW release builds.
