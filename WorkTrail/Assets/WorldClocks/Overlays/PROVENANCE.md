# World clock Urban Wash overlays

Generated on 2026-08-30 with the built-in image-generation tool. These assets are independent atmosphere layers for the transparent Urban Wash capital-city skylines; they are not city images. The runtime composes the time-of-day backdrops from local astronomical events. Weather foregrounds remain dormant until a source-backed current observation is available.

The final selected images were generated from text prompts only. Three existing Tokyo, London, and Ho Chi Minh City skyline images were used to review style, composition, and layering behavior. They were not copied into these outputs.

## Layering contract

- Render assets from `Backdrops/` behind the skyline.
- Render assets from `Foregrounds/` above the skyline.
- Cloud time-of-day layers are alternatives, not a stack.
- `Backdrops/stars.png` may be combined with `Backdrops/clouds-night.png`.
- `Backdrops/lightning.png` is an occasional storm accent rather than a persistent sky.
- `Foregrounds/rain.png`, `Foregrounds/fog.png`, and `Foregrounds/snow.png` remain independent so weather can be composed without duplicating a skyline.
- Sunrise and sunset windows are resolved locally from each clock's astronomical events. They may add `golden-hour.png`; day and night fall back to the matching cloud layer, with `stars.png` behind night clouds.
- Do not infer rain, fog, snow, lightning, or aurora from season, city, or local time. Those layers require a real current observation or an explicit decorative option.

## Technical validation

Every selected asset is a 1672 x 941 RGBA PNG. FFmpeg alpha extraction reported `YMIN=0` for every file, confirming fully transparent pixels.

| Asset | Role | SHA-256 |
| --- | --- | --- |
| `Backdrops/clouds-day.png` | Neutral cloudy daylight | `ffa6f227a33966116dcfc83bc7c838acfee8cc071ac9cd474f47cd347ddbec6a` |
| `Backdrops/clouds-dawn.png` | Blue-hour dawn clouds | `c16ba677b67dcbfea6e5667d5d73e6dbaa575352565b27987c4fb33de366f80b` |
| `Backdrops/clouds-sunset.png` | Sunset clouds | `7257b2ca05b26667cee3c20e442f81b2b01e27c0dc14eab0556dd4b6881ca782` |
| `Backdrops/clouds-night.png` | Night clouds without embedded stars | `2553a3a22b66b50d88f3eaa0850138efd2fd1e88922db3e9f6c38f2ada38fc69` |
| `Backdrops/stars.png` | Starry watercolor night wash | `c947bdd98c86961e3d43dbf127d35526e9b88fba432d95515989144a778344eb` |
| `Backdrops/lightning.png` | Occasional storm flash | `74b3d0698db64744bdf4f7908d6bc9e6cf36249e1c2262b127b58f6148daeafd` |
| `Backdrops/golden-hour.png` | Warm afterglow and light rays | `98a2eb0852c4f972840156be0286551139f0fcc166bc0749ffa7b98cf795ba2a` |
| `Backdrops/aurora.png` | Optional high-latitude night accent | `9da5a92be65aefc6bb5c3b3b7bbb653df29c1345b39bdc8c9ed567107eee15c7` |
| `Foregrounds/rain.png` | Sparse diagonal rain marks | `4e9788d72203c14418d75005d78cabc51922f5fa9882ac36eccfa067d6c00c82` |
| `Foregrounds/fog.png` | Layered lower and middle mist, alpha reduced for skyline legibility | `a8214e31d0f3bb4d470cd35cd48749c123e79c401f1ac3addefc3c08030ec60a` |
| `Foregrounds/snow.png` | Sparse tiny snow particles | `0dd05952a2c93f8eab05296893fb5cb5132ae27508e6b2c62a1925f677021648` |

## Prompt set

Common direction: create an independent 16:9 atmosphere layer on a genuinely transparent canvas, using premium hand-painted watercolor, translucent pigment, delicate granulation, softly feathered wet edges, low visual noise, and no city, architecture, skyline, ground, text, icon, border, or watermark. Keep the central upper area comparatively calm for the celestial UI element and dissolve every outer edge into alpha.

Asset-specific direction:

- Day clouds: cool pearl-gray, desaturated blue-gray, and faint lavender broken cloud banks.
- Dawn clouds: pale blush, apricot, lavender-blue, and cool early-morning mist.
- Sunset clouds: muted coral, dusty rose, amber, violet, and restrained horizon glow.
- Night clouds: deep indigo, muted navy, lavender-blue, and subtle silvered edges, without stars.
- Stars: irregular warm-white and cool-white points in a feathered indigo watercolor wash.
- Lightning: an upper-third blue-gray storm wash with a slender lateral branching flash.
- Golden hour: peach, amber, dusty-rose wisps, lower afterglow, and soft rays without a sun disc.
- Aurora: muted emerald, teal, and faint violet translucent curtains with an open center.
- Rain: separate hairline diagonal blue-gray marks over a mostly transparent canvas.
- Fog: broad pearly mist bands across the lower and middle field with transparent openings.
- Snow: tiny white and pale-blue dots and short windblown dashes over a mostly transparent canvas.

Reference-driven and early draft outputs that flattened a transparency grid, lacked alpha, or produced overly dense rain or snow were rejected and are not included.

## Publication authorization

On 2026-08-30 the project owner explicitly authorized public publication of
these 11 generated overlays and confirmed the applicable ImageGen publication
scope. They remain reserved TrackMeUp project artwork outside the repository
MIT grant.
