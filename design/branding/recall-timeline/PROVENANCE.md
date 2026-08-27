# Recall Timeline asset provenance

This record accompanies the assets in this directory for publication review.

## Source

- Date: 2026-08-10.
- Method: Codex built-in image generation, followed by deterministic local refinement with Pillow.
- Visual references: no third-party images or brand assets were supplied to the generator. The palette and product framing were derived from TrackMeUp's own repository and approved icon direction.
- Human direction: the project owner specified the visual-memory timeline, the left-to-right blurred-to-sharp progression, the mixed folders/pictures/files/web pages, and the coral gesture that lifts a rediscovered page. The project owner selected the dark and light source pair used here.

## Selected generation prompt

```text
Use case: logo-brand
Asset type: ultra-wide GitHub README banner artwork, dark theme source for proposal 01 "Recall Cascade"
Primary request: design a horizontal visual-memory timeline for TrackMeUp. Across the banner from left to right, show a loosely overlapping sequence of simplified folders, photo thumbnails, document sheets, and browser-page cards. Their stacking should feel casually shuffled but compositionally balanced. The far-left items are very soft, translucent, and out of focus like old memories; each successive item becomes gradually sharper, clearer, and more opaque toward the far right. Near the middle-left, a vivid coral timeline point sends a thin coral thread upward and physically lifts one older browser-page card above the stream, clearly communicating "found the page from ten days ago."
Scene/backdrop: deep navy-black #0B1220 with subtle Mica-like depth, no scenery
Style/medium: premium vector-friendly editorial illustration; flat stylized file/folder/browser silhouettes with restrained translucent glass layers; abstract, modern, slightly spectral but not gothic architecture
Composition/framing: extremely wide banner; timeline runs continuously through the lower two-thirds from left edge to right edge; selected page rises at about 45% width; keep the upper-left quadrant calm and low-detail for the exact TrackMeUp wordmark added later
Lighting/mood: private, calm, intelligent, satisfying discovery
Color palette: deep navy #0B1220, slate #314157, pale silver #D9E1EA, restrained cyan #1C83AC, coral accent #F9665B
Text: none
Constraints: include recognizable stylized folder tabs, image-thumbnail glyphs, document-line glyphs, and browser-window chrome with no readable text; strict left-to-right blur-to-sharp progression; exactly one selected raised item; coral used only for the selection point/thread/outline; no words, letters, numbers, watermark, people, eyes, cameras, cloud icons, AI sparkles, charts, skulls, religious or occult imagery; no border; original design only
```

The light source was produced by adapting only the theme and contrast while preserving the selected composition, retrieval gesture, and left-to-right progression.

## Local refinement

- Center crop to 3:1 without changing the depicted geometry.
- Enlargement to 3840 x 1280 with a restrained unsharp-mask pass.
- Exact `TrackMeUp` wordmark rendered in Bahnschrift SemiBold; `Up` uses the coral retrieval accent.
- Downscaled README, square mark, and transparent wordmark derivatives.
- PNG conversion to truecolor RGBA, 8 bits per channel, with embedded sRGB profile.
- Automatic PNG-header validation and SHA-256 manifest generation.

Bahnschrift is used from the local Windows installation only to rasterize the wordmark. No font file is redistributed by this directory.

## Redistribution review

The intended destination is the TrackMeUp source-available repository. These
assets are project-authored material governed by the TrackMeUp Source-Available
License 1.0 unless an asset-specific notice states otherwise. The project owner
must complete human visual review and confirm the applicable image-generation
service terms and publication scope before release. This record is not legal
advice.
