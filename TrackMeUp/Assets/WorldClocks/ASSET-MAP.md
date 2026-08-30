# World-clock asset locations

## Packaged runtime

- `Skylines/`: exactly 202 city/season RGBA PNG files at 1280×720.
- `Overlays/Backdrops/`: eight generated RGBA PNG atmosphere backdrops.
- `Overlays/Foregrounds/`: three generated RGBA PNG weather foregrounds.
- `Overlays/PROVENANCE.md`: overlay prompts, layering contract, checksums, and release boundary.
- `world-clocks.sqlite3`: city catalog and relative skyline paths under `Skylines/`.
- `SOURCE-MANIFEST.json`, `RUNTIME-ASSET-MANIFEST.json`, `PACKAGED-ASSET-MANIFEST.json`, `ATTRIBUTION.*`, and `PROVENANCE.md`: exact source/intermediate/package chain.

The obsolete generic `Images/` directory is intentionally absent.

## Repository-only masters and build output

- `design/world-clocks/watercolor/masters-v1/`: 202 original transparent PNG city masters.
- `design/world-clocks/watercolor/runtime-v1/`: 202 converted alpha WebP files plus the intermediate runtime manifest; these files are not loaded by WinUI.
- `design/world-clocks/watercolor/generation-manifest-v1.json`: the 101-city, two-season generation contract.
- `design/world-clocks/watercolor/GENERATION_RULES.md` and `PROVENANCE.md`: prompt and review records.

Overlay PNGs are currently both the selected generated originals and the packaged runtime files;
they are not duplicated into the city master or WebP directories.
