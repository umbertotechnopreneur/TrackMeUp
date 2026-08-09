# Recall Timeline banner

This is the selected TrackMeUp README-banner direction.

The composition reads from left to right: older visual memories begin blurred and translucent, then folders, pictures, documents, and web pages become progressively sharper. The coral retrieval point lifts one older page out of the timeline to communicate that TrackMeUp found a half-remembered moment from days earlier.

## Final assets

All files under `output/` are PNG truecolor RGBA with 8 bits per channel (32 bits total) and an embedded sRGB profile.

- `*-master-3840x1280.png`: large 3:1 master banners.
- `*-readme-2400x800.png`: GitHub README banners.
- `*-mark-*-1024x1024.png`: square symbol crops.
- `trackmeup-wordmark-*-transparent-2400x600.png`: transparent wordmarks for dark or light backgrounds.
- `trackmeup-recall-timeline-theme-pair-preview-2400x1600.png`: stacked comparison preview.
- `manifest.json`: dimensions, PNG header values, and SHA-256 checksums.

The `source/` images preserve the selected generated artwork. `render_assets.py` performs deterministic center cropping, restrained enlargement sharpening, exact `TrackMeUp` typography, themed exports, sRGB embedding, and RGBA validation.

## GitHub README integration

After owner approval, the root README can use automatic light/dark selection:

```html
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="design/branding/recall-timeline/output/trackmeup-recall-timeline-banner-theme-dark-readme-2400x800.png">
  <img src="design/branding/recall-timeline/output/trackmeup-recall-timeline-banner-theme-light-readme-2400x800.png"
       alt="TrackMeUp: a visual timeline retrieves a page from an earlier workday">
</picture>
```

The repository root README is intentionally not changed by this asset pass.

## Rebuild

From the repository root on Windows:

```powershell
pwsh -NoProfile -Command 'python .\design\branding\recall-timeline\render_assets.py'
```

The renderer uses Pillow and the Windows Bahnschrift font. It fails fast if the expected font or source images are unavailable.
