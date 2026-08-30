# World-clock Urban Wash asset provenance

This record accompanies the generated seasonal skyline masters and their deterministic runtime derivatives for publication review.

## Source

- Generation started: 2026-08-30.
- Method: Codex built-in image generation, one independent call per city and technical season id.
- Visual references: no third-party photographs, illustrations, logos, or other image inputs were supplied for the selected masters.
- Human direction: the project owner requested recognizable city landmarks rendered as seasonal watercolor backgrounds, selected the initial Paris and Rome direction, and approved the resulting Urban Wash style with “mi piace lo stile”.
- Catalog source: the city identities, coordinates, and time zones remain derived from the separately attributed GeoNames catalog.

## Production specification

The shared prompt, composition rules, forbidden content, seasonal modes, and alpha acceptance checks are defined in [`GENERATION_RULES.md`](GENERATION_RULES.md).

City-specific landmarks, palettes, and seasonal cues are defined in [`generation-manifest-v1.json`](generation-manifest-v1.json). The manifest contains exactly 101 unique catalog cities and binds the SHA-256 of each of the 202 selected master files under `reviewedMasters`. Those technical bindings identify the selected files and the publication record below identifies the owner's authorization.

Each prompt combines the shared Urban Wash template with the corresponding city and season record. In summary:

> Panoramic 16:9 architectural watercolor with restrained graphite underdrawing, broad translucent pigment, real RGBA transparency, a recognizable landmark on a lateral third, low urban mass, and a quiet central safe area for the live celestial orb. No sky, paper rectangle, checkerboard, text, people, vehicles, flags, logos, sun, moon, weather icon, signature, or watermark.

The technical `winter` id does not force European winter imagery. Tropical and equatorial cities use wet/dry or palette-only variants, and southern-hemisphere season cues follow local conditions.

## Selection and validation

- Selected masters are stored under `masters-v1/` as `<city-id>-<season>.png`.
- Original, converted, packaged, and overlay locations are mapped in
  [`ASSET_LOCATIONS.md`](ASSET_LOCATIONS.md).
- ImageGen outputs that bake a checkerboard, lose alpha, crowd the central orb area, misplace the landmark, or use implausible weather are rejected and regenerated.
- [`Test-WorldClockWatercolorAssets.ps1`](../../../scripts/Test-WorldClockWatercolorAssets.ps1) is the shared master gate: it requires an actual PNG codec, RGBA pixels, accepted dimensions, full 0–255 alpha range, the exact complete file set, and the reviewed SHA-256 binding.
- [`Convert-WorldClockWatercolorAssets.ps1`](../../../scripts/Convert-WorldClockWatercolorAssets.ps1) invokes that same gate before creating the retained 1280×720 alpha WebP intermediates with FFmpeg/libwebp and recording both source-master and intermediate checksums.
- [`promote_world_clock_watercolors.py`](../../../scripts/promote_world_clock_watercolors.py) validates those intermediates, transcodes them losslessly to the 1280×720 RGBA PNG files loaded by WinUI, and records the complete master/intermediate/package checksum chain.
- Automated checks do not replace ongoing visual QA of every city and landmark.

## Publication authorization

On 2026-08-30 the project owner explicitly authorized public publication of the complete generated asset set and accepted the applicable ImageGen service terms in that context. The 202 selected Urban Wash masters, their intermediate WebP derivatives, and their packaged PNG derivatives may therefore be published with TrackMeUp. This authorization does not place the artwork under the repository's MIT license; the asset-specific classification remains the one recorded in the repository licensing and attribution files.

The promoted catalog uses `TrackMeUp/Assets/WorldClocks/Skylines/`; the former
generic `Images/` directory is removed. `ASSET_LICENSING.md`,
`THIRD_PARTY_NOTICES.md`, the SQLite rows, and the distributed attribution
manifests identify the replacement images as TrackMeUp-directed project
artwork rather than Wikimedia derivatives.
