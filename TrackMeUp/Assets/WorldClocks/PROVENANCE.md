# Urban Wash world-clock artwork provenance

- Style: `urban-wash-v1`.
- Scope: 156 cities, two independently generated seasonal variants, 312 packaged assets.
- Generation mode: built-in OpenAI ImageGen, one call per distinct master; rejected outputs were not promoted.
- Direction: transparent editorial architectural watercolor with landmark clusters outside the central celestial-orb safe area.
- Atmosphere layers: the separately generated `Overlays/` tree is preserved and composed at runtime; source-backed weather layers are not inferred from the clock.
- Source manifest SHA-256: `a90acc76e8472e04b3f81a6a100f59348ab4e32720273a48f9d80783c80c8101` (`SOURCE-MANIFEST.json`).
- Intermediate WebP manifest SHA-256: `f2acce3b97f0c736dc01f0e94787a25d2154931cc4751c669ab3032e32929986` (`RUNTIME-ASSET-MANIFEST.json`).
- Intermediate WebP transformation: Scaled and center-cropped to 1280x720 alpha WebP with FFmpeg/libwebp quality 82, compression level 4.
- Packaged PNG manifest SHA-256: `0a4b313c39f1401a206bae244fec5eafadabb1d9125736db999242b1577ed5fb` (`PACKAGED-ASSET-MANIFEST.json`).
- Packaged PNG transformation: Decoded the reviewed 1280x720 alpha WebP runtime derivative and encoded a lossless 1280x720 RGBA PNG with FFmpeg/png, compression level 9, mixed prediction.
- Packaged toolchain: ffmpeg version 8.1.2-full_build-www.gyan.dev Copyright (c) 2000-2026 the FFmpeg developers; encoder `png`.

## Publication authorization

The images are reserved TrackMeUp project artwork and are outside the repository MIT grant.
On 2026-08-30 the project owner authorized public publication of the complete generated
asset set and accepted the applicable ImageGen service terms in that context. This
authorization does not place the artwork under the repository MIT license. The checksums
in the attribution and packaged manifest bind this record to the exact published files.
