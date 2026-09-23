# Urban Wash world-clock artwork provenance

- Style: `urban-wash-v1`.
- Scope: 206 cities, two independently generated seasonal variants, 412 packaged assets.
- Generation mode: built-in OpenAI ImageGen, one call per distinct master; rejected outputs were not promoted.
- Direction: transparent editorial architectural watercolor with landmark clusters outside the central celestial-orb safe area.
- Atmosphere layers: the separately generated `Overlays/` tree is preserved and composed at runtime; source-backed weather layers are not inferred from the clock.
- Source manifest SHA-256: `71d39051f51ba6555879c3b882ae47d4dcf68074df7a05c420373b2a0e8c6136` (`SOURCE-MANIFEST.json`).
- Intermediate WebP manifest SHA-256: `a63b6c266ed678a97ad814598f278685e6e57b930711bc0a97304cb574311843` (`RUNTIME-ASSET-MANIFEST.json`).
- Intermediate WebP transformation: Scaled and center-cropped to 1280x720 alpha WebP with FFmpeg/libwebp quality 82, compression level 4.
- Packaged PNG manifest SHA-256: `34c22f1501e949bc014af2d8190f5a37b49289c25316c317d001b16ccf823696` (`PACKAGED-ASSET-MANIFEST.json`).
- Packaged PNG transformation: Decoded the reviewed 1280x720 alpha WebP runtime derivative and encoded a lossless 1280x720 RGBA PNG with FFmpeg/png, compression level 9, mixed prediction.
- Packaged toolchain: ffmpeg version 9.0.1-full_build-www.gyan.dev Copyright (c) 2000-2026 the FFmpeg developers; encoder `png`.

## Publication authorization

The images are reserved WorkTrail project artwork and are outside the repository MIT grant.
On 2026-08-30 the project owner authorized public publication of the complete generated
asset set and accepted the applicable ImageGen service terms in that context. This
authorization does not place the artwork under the repository MIT license. The checksums
in the attribution and packaged manifest bind this record to the exact published files.
