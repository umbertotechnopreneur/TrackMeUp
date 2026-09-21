# TrackMeUp world-map texture provenance

- Assets: `world-map-day.png` and `world-map-night.png`.
- Source mode: supplied by the product owner on 2026-09-04 for incorporation into the World Clock panel.
- Purpose: equirectangular day and night textures blended at runtime from the calculated solar altitude; the application adds the twilight tint, Sun, Moon, lunar phase, and selected-city markers.
- Processing: mechanically downscaled to 2048 x 1024 pixels with high-quality bicubic interpolation; no static terminator, celestial marker, label, UI chrome, or watermark was added.
- Day source SHA-256: `aff520a4557744d584efb1f70a0589651294f276da1dbc3cf76aca00ef42cb39`.
- Day packaged SHA-256: `7bbdc4dcdda28a9262d522e1b4e8ace0d367aa7875b09a4958daf8cf8a333045`.
- Night source SHA-256: `98e5f6432fd6e8af2723431795eed830962a727e86a7532526b833f48b47c3b1`.
- Night packaged SHA-256: `2df9c6330af6251090860e8211195e5b38df45e17c63366b0d25ae02cb7bfad0`.

The upstream author and license were not supplied with the images. Keep these assets outside the repository MIT grant and confirm redistribution clearance before a public release.
