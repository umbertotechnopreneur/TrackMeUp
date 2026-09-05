# World-clock asset locations

This map is the source of truth for the Urban Wash city artwork, its runtime
derivatives, and the composable atmosphere layers.

## Original city masters

- Directory: `design/world-clocks/watercolor/masters-v1/`
- Contents: one `summer` and one `winter` transparent PNG master for every
  city declared in the generation manifest.
- Naming: `<city-id>-summer.png` and `<city-id>-winter.png`.
- Typical dimensions: 1672×941 RGBA; a small accepted early subset is
  1672×940 and remains within the validated 16:9 tolerance.
- Policy: generated originals are immutable; rejected ImageGen outputs remain
  outside the repository and are never copied over an accepted master.

The exact city list, landmarks, palettes, and seasonal model are recorded in
[`generation-manifest-v1.json`](generation-manifest-v1.json). Shared prompt and
composition rules are in [`GENERATION_RULES.md`](GENERATION_RULES.md), with the
generation/review boundary in [`PROVENANCE.md`](PROVENANCE.md).

## Intermediate city WebP files

- Current build directory: `design/world-clocks/watercolor/runtime-v4/`
- Contents: one alpha WebP file per reviewed city/season pair at 1280×720 plus
  `runtime-asset-manifest.json`.
- Transformation: FFmpeg/libwebp quality 82, compression level 4, with decoded
  codec, dimensions, alpha range, byte length, and SHA-256 verified per file.
- Policy: these are reproducible intermediate build outputs retained for review
  and provenance. WinUI does not load them directly.

## Packaged city PNG files

- Directory: `TrackMeUp/Assets/WorldClocks/Skylines/`.
- Contents: one RGBA PNG per reviewed city/season pair at 1280×720.
- Transformation: each reviewed intermediate WebP is decoded and re-encoded
  losslessly as a WinUI-supported PNG. The packaged manifest records the
  intermediate and master hashes as well as each PNG hash and byte length.

The packaged SQLite catalog stores paths under `Skylines/`. The previous
generic `TrackMeUp/Assets/WorldClocks/Images/` directory is obsolete and must
not exist after promotion.

The superseded Wikimedia/`Images/` catalog builder has been removed. Validate
masters with `scripts/Test-WorldClockWatercolorAssets.ps1`, produce intermediate
WebP derivatives with `scripts/Convert-WorldClockWatercolorAssets.ps1`, and
produce/install the reviewed PNG catalog with
`scripts/promote_world_clock_watercolors.py`.

## Atmosphere overlays

- Backdrops: `TrackMeUp/Assets/WorldClocks/Overlays/Backdrops/`
  - eight 1672×941 generated RGBA PNG files for day, dawn, sunset, night,
    stars, golden hour, lightning, and aurora.
- Foregrounds: `TrackMeUp/Assets/WorldClocks/Overlays/Foregrounds/`
  - three 1672×941 generated RGBA PNG files for rain, fog, and snow.
- Provenance: `TrackMeUp/Assets/WorldClocks/Overlays/PROVENANCE.md`.

These overlay PNGs are currently both the selected generated originals and the
runtime files; they are intentionally separate from the manifest-declared city masters and
WebP derivatives. Local sunrise/sunset/day/night data selects decorative
backdrops. Rain, fog, snow, lightning, and aurora are never inferred from city,
season, or clock time and require real source-backed observations or an
explicit decorative option.

## Packaged catalog records

`TrackMeUp/Assets/WorldClocks/` also contains:

- `world-clocks.sqlite3` — the manifest-declared city catalog and matching `Skylines/` asset records;
- `SOURCE-MANIFEST.json` — packaged copy of the generation contract;
- `RUNTIME-ASSET-MANIFEST.json` — packaged copy of the exact intermediate WebP
  manifest;
- `PACKAGED-ASSET-MANIFEST.json` — exact source-bound manifest for the packaged PNG
  files loaded by WinUI;
- `ATTRIBUTION.json` and `ATTRIBUTION.md` — data/artwork attribution and hashes;
- `PROVENANCE.md` — release-ready city artwork provenance; and
- `ASSET-MAP.md` — the packaged counterpart of this location map.

The city artwork and overlays are outside the repository MIT grant. The project
owner explicitly authorized their public publication on 2026-08-30; the exact
scope and checksum chain are recorded in their provenance files and manifests.
