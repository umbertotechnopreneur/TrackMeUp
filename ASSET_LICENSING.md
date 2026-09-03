# Asset Licensing and Provenance

This record maps non-code assets in the TrackMeUp repository. It is a release
review aid, not legal advice, and does not establish ownership by itself.

## MIT-licensed material

Project-authored software and documentation are licensed under the root
[MIT License](LICENSE), unless a file-specific notice says otherwise. This
includes project-authored scripts, Markdown documentation, and metadata used to
build or describe assets; it does not automatically change the license of the
images or data those files process.

## TrackMeUp Brand Assets

The TrackMeUp name, wordmarks, logos, app icons, and branded marketing artwork
are outside the MIT grant and are governed by
[`TRADEMARKS.md`](TRADEMARKS.md). The following visual files are treated as
TrackMeUp Brand Assets:

- PNG artwork under `design/branding/`, including the Recall Timeline and
  Atomic Nuke banners and the icon reference;
- TrackMeUp logo, icon, splash-screen, Store, lock-screen, badge, square, and
  wide-logo image derivatives under `TrackMeUp/Assets/`; and
- `TrackMeUp/Assets/TrackMeUpIcon.ico`.

This classification defines license scope only. It does not replace source and
rights verification for each asset. Forks and independently published builds
must replace these assets unless separate permission has been granted.

## World-clock data and artwork

`TrackMeUp/Assets/WorldClocks/` contains GeoNames-derived city data, licensed
under CC BY 4.0, together with TrackMeUp-directed Urban Wash city artwork and
atmosphere overlays. The project artwork is outside the repository's MIT grant;
it is not a Wikimedia derivative set. Exact source manifests, transformations,
provenance, and checksums are distributed in
[`ATTRIBUTION.md`](TrackMeUp/Assets/WorldClocks/ATTRIBUTION.md),
[`ATTRIBUTION.json`](TrackMeUp/Assets/WorldClocks/ATTRIBUTION.json), and the
adjacent provenance records. Preserve the GeoNames attribution when
redistributing the catalog data.

`TrackMeUp/Assets/WorldClocks/ThirdParty/OpenWeather/ow_logo.svg` is the official
OpenWeather provider mark included solely for visible linked weather
attribution. The exact official source and SHA-256 are recorded in
[`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md) and
[`IP_PROVENANCE.md`](IP_PROVENANCE.md). The mark and optional OpenWeather
observations are third-party provider material outside the repository MIT grant
and outside TrackMeUp Brand Assets. A person supplying an OpenWeather API key is
responsible for the terms of the selected provider plan.

Third-party package assets and embedded web code retain the terms recorded in
[`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md) and its linked generated
notice bundle.

## First-party AI-generated asset record

The project owner has confirmed that the first-party TrackMeUp visual assets
listed in
[`AI_ASSET_PROVENANCE.md`](design/branding/AI_ASSET_PROVENANCE.md) were created
with AI-assisted workflows run under his control, were selected and reviewed by
him, and are authorized for publication in the public source repository and
official TrackMeUp binaries. That dated declaration covers the application
identity family, Atomic Nuke and Recall Timeline artwork, the screenshot
placeholder, premium celestial images, the original world-clock watercolor
pilot, the manifest-defined Urban Wash catalog, and its 11 atmosphere overlays.

The declaration does not alter the separate source and license records for
GeoNames, packages, fonts, or other third-party material. The exact Urban Wash
masters, runtime derivatives, overlays, prompts, transformations, and checksums
remain bound to their adjacent manifests and provenance records.

## Publication status

On 2026-08-30 the project owner explicitly authorized public publication of the
generated Urban Wash city artwork and atmosphere overlays after reviewing the
selected style and confirming the applicable ImageGen publication scope. These
assets remain reserved TrackMeUp project artwork outside the repository's MIT
grant; publication does not grant downstream trademark or brand rights.
