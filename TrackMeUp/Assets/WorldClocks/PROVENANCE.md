# Urban Wash world-clock artwork provenance

- Style: `urban-wash-v1`.
- Scope: 101 cities, two independently generated seasonal variants, 202 packaged assets.
- Generation mode: built-in OpenAI ImageGen, one call per distinct master; rejected outputs were not promoted.
- Direction: transparent editorial architectural watercolor with landmark clusters outside the central celestial-orb safe area.
- Atmosphere layers: the separately generated `Overlays/` tree is preserved and composed at runtime; source-backed weather layers are not inferred from the clock.
- Source manifest SHA-256: `43e2945d3edad4e09892c6f07abc1bbc206d116e89617d2f13f467e87d4a8797` (`SOURCE-MANIFEST.json`).
- Intermediate WebP manifest SHA-256: `40a6aa305d2d3a8f39f471721e6a57001f3da780764d3265b9c442b285dd4231` (`RUNTIME-ASSET-MANIFEST.json`).
- Intermediate WebP transformation: Scaled and center-cropped to 1280x720 alpha WebP with FFmpeg/libwebp quality 82, compression level 4.
- Packaged PNG manifest SHA-256: `eb83ec307119812093fb09a07dc810851ddc4443a89439ae3af2879efeb5735d` (`PACKAGED-ASSET-MANIFEST.json`).
- Packaged PNG transformation: Decoded the reviewed 1280x720 alpha WebP runtime derivative and encoded a lossless 1280x720 RGBA PNG with FFmpeg/png, compression level 9, mixed prediction.
- Packaged toolchain: ffmpeg version 8.1.2-full_build-www.gyan.dev Copyright (c) 2000-2026 the FFmpeg developers; encoder `png`.

## Publication authorization

The images are reserved TrackMeUp project artwork and are outside the repository MIT grant.
On 2026-08-30 the project owner authorized public publication of the complete generated
asset set and accepted the applicable ImageGen service terms in that context. This
authorization does not place the artwork under the repository MIT license. The checksums
in the attribution and packaged manifest bind this record to the exact published files.
