# Celestial interface artwork

Created on 2026-09-19 with the built-in ImageGen tool at the project owner's request, using the supplied celestial desktop mockup as a style reference. These are decorative AI-generated illustrations, not photographs, observations, geographic measurements or event predictions. Actual positions, phases, dates and sky gradients are supplied separately by the Core astronomical model. Existing NASA Sun/Moon photographs and geographic day/night textures remain separate with their original attribution.

The atlas is sampled as four columns and three rows of equal 362-pixel cells. Row 1: Mercury, Venus, Mars, Jupiter. Row 2: Saturn, Uranus, Neptune, sunrise. Row 3: sunset, blue hour, twilight, seasons. Planet textures and lighting are illustrative rather than date-specific. The horizon is decorative terrain, not the selected city's topography. The meteor thumbnail depicts a shower symbol, not a projected radiant or flight path. The horizon and meteor PNGs preserve generated alpha transparency; no local pixel edits were made. Licensing follows the project's asset policy in ASSET_LICENSING.md; this record does not assert third-party observation provenance or change existing artwork rights.

## celestial-atlas-v1.png

- Dimensions: 1448 × 1086 pixels.
- SHA-256: `c4e689f9338e862753226d4808eae65446f88baf0336ee4f60a76bc04fe02810`.
- Generation method: built-in ImageGen; original output copied without pixel edits.

### Final prompt

```text
Use case: productivity-visual
Asset type: ONE production UI sprite atlas PNG, 4 columns by 3 rows, 12 equal square cells, total aspect ratio 4:3.
Reference image: attached screenshot is STYLE REFERENCE ONLY. Create a reusable texture atlas for its lovely tiny planetary and astronomical event illustrations, not another screenshot.
Every cell exactly centered on a uniform very dark navy #0B1321 background, no dividers, no borders, no text. Leave 15 percent padding in each cell. A precise regular 4 by 3 grid is critical because this file will be sampled in software.
Row 1, left to right: Mercury realistic gray cratered sphere; Venus creamy golden cloudy sphere; Mars realistic rusty orange sphere; Jupiter large creamy brown banded sphere with red spot.
Row 2 left to right: Saturn pale gold sphere with clearly visible tilted rings fully inside cell; Uranus pale cyan sphere; Neptune deep cobalt blue sphere; Sunrise golden Sun half appearing above a fine horizontal horizon with delicate gold haze.
Row 3 left to right: Sunset orange Sun half below a fine horizon with warm fading glow; Blue hour layered mountain silhouette with deep blue twilight glow; Civil twilight same mountains with pale violet and gold horizon; Seasons delicate warm-gold Sun with an elegant thin orbital ellipse.
Style: exceptionally beautiful restrained cinematic photorealistic miniatures, matching the premium acrylic astronomy UI reference. All planets lit from upper left and slightly shadowed right, soft subtle halo, detailed texture readable at 48px, smoothly antialiased outlines. Reference look is dark elegant, not cartoon. Planets are illustrative thumbnails, not diagrams. Sunrise and sunset distinguished by different warm hue and glow. No letters, numbers, labels, grids, stars sprinkled between cells, interface chrome, watermarks, or extra objects. Exact 12 cells and exact ordering. The single atlas must cover the image edge to edge in equal cells with no external margin.
```

## sky-horizon-v1.png

- Dimensions: 1536 × 1024 pixels.
- SHA-256: `1c8b738ca6f5f5a05eb9fa98c2639fe9fec37c217c2e5360a1c26e410a966d9d`.
- Generation method: built-in ImageGen; original output copied without pixel edits.

### Final prompt

```text
Use case: illustration-story
Asset type: production UI decorative horizon foreground PNG with real transparency, 3:2 wide canvas.
Primary request: create the dark mountain and pine forest foreground from the attached astronomy UI style, isolated with transparent sky. This will be layered over a computed sky gradient.
Composition: top TWO THIRDS entirely transparent, NOT black, NOT a checkerboard image; bottom third holds a low scenic mountain horizon and layered pine trees. Highest tree branches at far left/right edges reach about halfway up image, leaving middle almost entirely open. Dark blue-gray distant mountain silhouettes low at middle, gently rising to left/right. Foreground near-black navy pine silhouettes at both lower corners, middle lower horizon open. Photorealistic atmospheric alpine night silhouettes, elegant soft depth. Entire canvas bottom edge fully filled dark navy forest silhouettes. No Sun, no Moon, no stars, no sky color, no text, no buildings, no frames or UI. Neutral dim blue-gray mountain layers allow dawn, day and sunset gradients behind. Preserve clean antialiased tree edges into alpha and landscape depth. Beautiful understated premium aesthetic matching reference, details remain harmonious small. Actual PNG alpha sky mandatory. Reference screenshot STYLE ONLY, don't reproduce interface.
```

## meteor-shower-v1.png

- Dimensions: 1254 × 1254 pixels.
- SHA-256: `329432ed39e5bc9679ef509e28b5d18eec4e9358309b82ad56a2fe5dac40bb33`.
- Generation method: built-in ImageGen; original output copied without pixel edits.

### Final prompt

```text
Use case: illustration-story
Asset type: single production astronomy event thumbnail PNG with transparent background.
Primary request: a beautiful delicate golden meteor streaking diagonally downward toward lower left, with a bright tiny warm-white head, tapered gold dust trail extending upper right and two extremely subtle smaller meteor trails behind it. Elegant premium astronomy UI miniature, realistic light and atmospheric glow, legible at 48 pixels, not a cartoon or emoji. Center composition occupies middle 70 percent of square canvas with generous transparent padding, no text, letters, labels, frame, badge, landscape, planets or scattered stars. Truly transparent alpha background (no black background and no checkerboard pixels). Color gold ivory warm amber with tasteful bloom, consistent with dark frosted acrylic celestial UI. Entire streak inside frame.
```
