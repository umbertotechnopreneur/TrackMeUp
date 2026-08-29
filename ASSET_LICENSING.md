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

## Third-party data and media

`TrackMeUp/Assets/WorldClocks/` contains GeoNames-derived data and Wikimedia
Commons skyline derivatives. Those files are not relicensed as MIT. Exact
sources, authors, licenses, transformations, and checksums are recorded in
[`ATTRIBUTION.md`](TrackMeUp/Assets/WorldClocks/ATTRIBUTION.md) and
[`ATTRIBUTION.json`](TrackMeUp/Assets/WorldClocks/ATTRIBUTION.json). Preserve
the applicable attribution and ShareAlike requirements when redistributing
them.

Third-party package assets and embedded web code retain the terms recorded in
[`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md) and its linked generated
notice bundle.

## Provenance still requiring confirmation

Do not represent the following files as cleared for public source or binary
redistribution until their source and applicable service or license terms have
been recorded or the files have been replaced:

- `design/branding/trackmeup-icon-reference.png` and its generated application
  identity derivatives under `TrackMeUp/Assets/`, as recorded in
  [`ICON_PROVENANCE.md`](design/branding/ICON_PROVENANCE.md);
- the two PNG files under `design/branding/atomic-nuke/output/`, as recorded in
  their adjacent [`PROVENANCE.md`](design/branding/atomic-nuke/PROVENANCE.md);
- `TrackMeUp/Assets/TrackMeUpSnapshotPlaceholder.png`;
- `TrackMeUp/Assets/Celestial/sun-premium.png` and
  `TrackMeUp/Assets/Celestial/moon-premium.png`; and
- the Recall Timeline images until the image-generation service terms and
  human visual review required by their adjacent `PROVENANCE.md` are confirmed.

Git history records when these files entered the repository, but commit
authorship alone is not proof of their upstream source or redistribution
rights. Publication review must resolve every item above explicitly.
