# Celestial desktop README strip provenance

- Date: 2026-09-19.
- Asset: `trackmeup-celestial-desktop-strip-v1.png`.
- Dimensions: 2172 × 724 pixels (3:1); PNG, 1,844,138 bytes.
- SHA-256: `3896ce09721a12b83f647e40b87666da61b0a3eb75841f0fef32a91da3a539ba`.
- Method: Codex built-in ImageGen, followed by one targeted ImageGen edit. The selected final PNG was copied into the repository without local pixel edits.
- Human direction: the project owner requested a README strip blending the new celestial controls, a description below the strip, and a mention of optional window snapping.
- Inputs: the initial generation used a text brief describing the project UI; the edit used only that generated panorama. No third-party reference image was supplied to either generation.
- Purpose: an editorial product rendering, not an actual screenshot, astronomical observation, event forecast or pixel-exact representation of the shipped interface. Its sky coordinates, phases, map lighting and clock value are illustrative and are not a common calculated reference instant.
- Visual review: all six views are recognizable; title bars and zoom/projection controls are absent; the Moon–Jupiter conjunction and blue-hour thumbnail were corrected; decorative altitude numbers were removed. The image is displayed responsively with descriptive alternative text and a visible rendering disclosure.
- Classification: TrackMeUp promotional brand artwork, governed by [TRADEMARKS.md](../../../TRADEMARKS.md), separate from the MIT grant and subject to applicable generation service terms. This record does not alter existing in-app artwork attribution.

## Initial generation prompt

```text
Use case: ui-mockup
Asset type: one ultra-wide panoramic product illustration for the TrackMeUp GitHub README, landscape 3:1 strip, ideally 3072 by 1024 pixels.
Primary request: create an exceptionally beautiful, restrained, high-fidelity rendering of TrackMeUp's celestial desktop windows flowing together into one cohesive desktop collage. It is an editorial UI rendering, not a claimed real application capture.
Style: front-on native Windows 11 acrylic interfaces, very light Segoe UI-like typography, translucent smoky navy and charcoal glass, subtle blur of the shared twilight desktop behind the windows, fine pale borders and restrained coral accent #F9665B. Precise, calm, elegant and readable, not futuristic sci-fi.
Composition: fill the full width with a flowing horizontal montage. Window edges align closely in a snap-like collage with tiny clean gaps; reflections, atmosphere and soft shadows blend the groups into one continuous strip. Keep individual controls recognizable, not a row of generic cards. No perspective tilt, no laptop or monitor frame. Hide all title bars, window buttons and large repeated headings. No top toolbars. Small city selectors belong only along the bottoms of the sky, agenda and globe surfaces.
Subjects from left to right, with balanced overlap and no occluded key content:
1. A narrow world-clock glimpse with a Rome architectural watercolor, small text "ROME" and light coral time "20:45", smoothly merging into a compact richly cratered half-lit Moon widget, no cramped lunar captions.
2. The dominant local-sky view: circular all-sky chart with zenith in the center, delicate dotted altitude arcs, subtle constellation lines and a few fine star points over a blue-hour-to-warm-horizon gradient, dark mountains and pine silhouettes low down. Modest Moon and planet markers rather than oversized solar-system objects. A minimal bottom selector with "Rome", and a small row of Moon, Mars and Jupiter thumbnails. No zoom slider or magnification controls.
3. A slim astronomical agenda: four tasteful compact rows with beautiful small illustrations of a lunar quarter, a golden meteor, a pair of planets and a sun on a horizon. Render only these exact short row labels: "Moon phase", "Meteor showers", "Conjunctions", "Blue hour". No invented dates or times, no horoscope predictions. A discreet row of a few tiny zodiac glyphs near its bottom.
4. A large striking Earth globe with physically plausible continents, a graceful current-day/night terminator and faint warm city lights, detailed enough to feel photographic but softly integrated in the acrylic collage. Beneath and slightly behind it, a separate thin panoramic flat world map with matching day/night illumination: clearly an independent flat-map window, never a globe projection switch. Bottom city selector "Rome" on the globe surface only.
Layout fidelity: all windows are independent resizable glass surfaces; chart/map content is edge-to-edge, no nested card around the sky, no repeated timestamps, no visible title bars, no zoom controls, no flat/globe toggle. The shared background and light should visually fuse the elements without turning the functional charts into abstract art.
Text: only the quoted short labels and clock value above, accurate English spelling, light readable type. No main headline, slogan, paragraph, dimension annotation, snap guidelines, arrows, logos, app icon, watermark, signatures or additional text. The README will carry the description outside this image.
Output: a polished wide PNG editorial strip with generous but not empty breathing room, opaque seamless dark background, not a checkerboard. Keep all key objects inside the image and usable when shown at full README width.
```

## Final targeted-edit prompt

```text
Use case: precise-object-edit
Asset type: final TrackMeUp README panoramic UI rendering.
Input image 1 is the EDIT TARGET. Preserve the entire image, its exact wide 3:1 dimensions, acrylic panels, positions, backgrounds, globe and separate flat map, clock time, typography, colors and all other content.
Change ONLY these three small details:
1. In the agenda's bottom row labeled "Blue hour", replace just the warm orange sunrise thumbnail with beautiful deep cobalt and muted violet blue-hour mountains under a gently glowing twilight horizon. Absolutely no Sun disk, no orange sunset. Keep the exact label "Blue hour" and row layout.
2. In the agenda row labeled "Conjunctions", change just the smaller rusty red Mars sphere to a small gray cratered crescent Moon, beside the existing larger Jupiter. This must clearly read as Moon–Jupiter, not Mars–Jupiter. Keep the label and all other row content.
3. Inside the circular local-sky chart, remove only the tiny numeric altitude annotations "90°", "60°" and "30°". Preserve their dotted arcs, all star lines, N/E/S/W cardinal letters, the chart geometry, mountains and planet markers exactly.
Constraints: no other changes, no new text, no new logos, no watermarks, no title bars, no extra controls, no zoom slider, no projection toggle. Do not redraw or restyle the panorama. Retain the original overall sharpness and the beautifully blended atmosphere. This is an illustrative product montage, not an astronomical measurement.
```
