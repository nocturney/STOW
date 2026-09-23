# Visual asset provenance

## STOW brand artwork

- `assets/source/STOW-icon-master.png` — approved handoff artwork and single source of truth for the STOW application icon.
- `STOW.png` — pixel-identical shipped PNG copied from the approved icon master.
- `STOW.ico` — multi-resolution Windows icon derived from the same approved icon master.
- `STOWHero.png` — transparent foreground extraction from the same approved icon master, used by Focus and About.
- Source concept: the approved STOW visual direction (layered blue/teal window cards entering a tray/container).
- Generation: `scripts/generate-stow-icon.py` derives the shipped PNG/ICO from the approved master; `scripts/generate-stow-hero.py` extracts the approved foreground artwork with Pillow only.
- Third-party visual content: none. No external logo, stock image, icon pack or font file is embedded in these assets.

The STOW icon is used as the application, taskbar, main tray and installer identity.

## Historical Trayify assets

`Trayify.png` and `Trayify.ico` are retained only for the preserved Trayify compatibility baseline. They are not the STOW application identity and must not be wired into STOW release builds.
