# Urban Wash world-clock artwork provenance

- Style: `urban-wash-v1`.
- Scope: 189 cities, two independently generated seasonal variants, 378 packaged assets.
- Generation mode: built-in OpenAI ImageGen, one call per distinct master; rejected outputs were not promoted.
- Direction: transparent editorial architectural watercolor with landmark clusters outside the central celestial-orb safe area.
- Atmosphere layers: the separately generated `Overlays/` tree is preserved and composed at runtime; source-backed weather layers are not inferred from the clock.
- Source manifest SHA-256: `e278a120ee8c036eef06638395c53ad6bb3cba6bbb9360495dd54c9e9c202372` (`SOURCE-MANIFEST.json`).
- Intermediate WebP manifest SHA-256: `87b7cb7799c820d4800891f064fa75b74f9c770865013a6c9b53f6761a2a5abb` (`RUNTIME-ASSET-MANIFEST.json`).
- Intermediate WebP transformation: Scaled and center-cropped to 1280x720 alpha WebP with FFmpeg/libwebp quality 82, compression level 4.
- Packaged PNG manifest SHA-256: `46be0c18aeeed87c666d30947f47b92a16283bf616328aefdc799b58a6c765ce` (`PACKAGED-ASSET-MANIFEST.json`).
- Packaged PNG transformation: Decoded the reviewed 1280x720 alpha WebP runtime derivative and encoded a lossless 1280x720 RGBA PNG with FFmpeg/png, compression level 9, mixed prediction.
- Packaged toolchain: ffmpeg version 8.1.2-full_build-www.gyan.dev Copyright (c) 2000-2026 the FFmpeg developers; encoder `png`.

## Publication authorization

The images are reserved TrackMeUp project artwork and are outside the repository MIT grant.
On 2026-08-30 the project owner authorized public publication of the complete generated
asset set and accepted the applicable ImageGen service terms in that context. This
authorization does not place the artwork under the repository MIT license. The checksums
in the attribution and packaged manifest bind this record to the exact published files.
