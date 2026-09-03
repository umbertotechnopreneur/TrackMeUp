# Urban Wash generation rules

These rules define the approved TrackMeUp world-clock skyline asset family.

## Deliverables

- Catalog scope: manifest-driven; every declared city has two reviewed seasonal masters.
- Two technical season ids per city: `summer` and `winter`.
- Production master name: `masters-v1/<city-id>-<season>.png`.
- Master format: 16:9 RGBA PNG with genuine transparency.
- Generate each distinct asset with its own built-in ImageGen call.
- Do not overwrite an existing master. A replacement must use a new versioned directory.

The `winter` id is a storage contract, not a requirement to depict snow. Tropical and equatorial cities use wet/dry or palette-only seasonal changes. Southern-hemisphere cities should depict their local summer and winter honestly.

## Approved visual language

Urban Wash is an airy architectural watercolor with restrained graphite underdrawing, broad translucent pigment masses, softly feathered edges, and low visual noise. The skyline must be immediately recognizable but not photographic.

Composition requirements:

- exact 16:9 landscape;
- essential content inside `x=16–84%`;
- primary landmark on a lateral third, normally near `x=24%` or `x=76%`;
- urban mass mostly below `y=65%`;
- keep `x=35–67%`, `y=8–65%` quiet and pale for the 122-DIP celestial orb;
- softly dissolve the outer 5–7% instead of creating a hard rectangle;
- no important landmark or dark edge in the central safe area.

The image must contain real alpha transparency. Do not paint a sky, paper rectangle, solid color, or checkerboard. Do not include text, labels, flags, logos, people, vehicles, sun, moon, stars, weather icons, signature, or watermark.

## Prompt template

```text
Use case: stylized-concept
Asset type: TrackMeUp Urban Wash world-clock skyline master; technical season id {SEASON}
Primary request: create an original panoramic architectural watercolor of {CITY}, {SEASON_DESCRIPTION}
Scene/backdrop: an isolated skyline and restrained low foreground wash on a genuinely transparent canvas; no sky, paper, filled background, or checkerboard
Subject: {LANDMARKS}; keep the city unmistakably recognizable without a collage of unrelated monuments
Style/medium: airy premium hand-painted architectural watercolor, restrained graphite underdrawing, broad translucent pigment masses, subtle granulation and softly feathered broken edges; recognizable but not photographic
Composition/framing: exact 16:9; essential content inside x=16–84%; primary landmark on {LANDMARK_POSITION}; urban mass mostly below y=65%; keep x=35–67%, y=8–65% quiet, pale and sparse for a large circular UI overlay
Lighting/mood: diffuse time-neutral daylight; seasonal atmosphere without painted weather
Color palette: {PALETTE}
Constraints: high-resolution RGBA PNG with genuine alpha transparency; broad upper field and exterior corners alpha 0; suitable for display behind UI at low opacity
Avoid: sky, clouds, paper texture, checkerboard, border, typography, people, vehicles, flags, logos, sun, moon, stars, weather effects, night lighting, photorealism, 3D rendering, vector silhouette, black outlines, HDR, saturated postcard colors, signature, watermark, dense center, cropped landmark
```

## Seasonal modes

- `true-winter`: foliage, palette, and restrained snow/frost may change; never add active snowfall.
- `mild-winter`: cooler palette, damp stone, and sparse deciduous foliage; little or no snow.
- `wet-dry`: `summer` represents the wetter/lusher season and `winter` the drier/warmer season; no cold cues.
- `palette-only`: preserve vegetation and architecture; use only subtle atmospheric and palette changes.

Generate winter independently when edit mode flattens a checkerboard into RGB. A flattened checkerboard is a rejected output.

## Acceptance checks

For every selected master:

1. `ffprobe` reports the expected 16:9 dimensions and `pix_fmt=rgba`.
2. FFmpeg alpha extraction reports `YMIN=0` and `YMAX=255`.
3. The city and required landmark are visually recognizable.
4. The central orb safe area remains quiet.
5. No forbidden object or baked background is present.
6. Bind the selected file's SHA-256 under `reviewedMasters` in
   `generation-manifest-v1.json`; retain the prompt, generation date, built-in
   tool mode, and review boundary in the adjacent provenance records.

`Test-WorldClockWatercolorAssets.ps1` is the shared executable quality gate. It
requires one actual PNG stream, `pix_fmt=rgba`, the accepted near-16:9 master
dimensions, full 0-255 alpha range, the exact manifest-declared city/season set
when run with `-RequireComplete`, and the reviewed SHA-256 binding. The converter
invokes that same gate before producing any runtime derivative.
