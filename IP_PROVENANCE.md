# Intellectual Property Provenance

This record supports open-source publication and release review. It is not legal advice.

The project owner and contributors are responsible for selecting, reviewing,
integrating, testing, and publishing repository material under the applicable
license.

Project-authored software and documentation are made available under the
[MIT License](LICENSE) unless a file states otherwise. The TrackMeUp name,
logos, app icons, and project-authored brand artwork are outside that MIT grant
and are governed by [`TRADEMARKS.md`](TRADEMARKS.md) and asset-specific
provenance records. Third-party code, data, and assets retain their own license
terms; inclusion in this repository does not relicense them as TrackMeUp
material.

## Provenance Rules

- Third-party code, libraries, fonts, images, screenshots, prompts, examples, and generated assets require documented source and license.
- Record attribution and redistribution scope in `THIRD_PARTY_NOTICES.md` or alongside the asset.
- Distinguish MIT-licensed project material, reserved TrackMeUp Brand Assets, and third-party material in provenance records.
- Keep provenance notes updated when dependencies or assets change.

## Third-Party Service Data and Marks

Optional current world-clock observations are returned directly by OpenWeather
under the API key and plan selected by the person running TrackMeUp. Those
observations are provider material, not project-authored content, and are not
covered by the repository MIT License. The person supplying the key remains
responsible for the applicable service, data, attribution, redistribution, and
plan terms.

The bundled attribution mark at
`TrackMeUp/Assets/WorldClocks/ThirdParty/OpenWeather/ow_logo.svg` is the official
OpenWeather SVG from
<https://openweathermap.org/payload/api/media/file/ow_logo.svg>, with SHA-256
`fd0ad613ebcdb5f013df98bf75603c83fe1f3f0a5f677118b99557da8ac9281c`.
It remains third-party provider artwork and is included solely for visible
linked attribution; inclusion does not transfer ownership or place the mark
under the MIT License. Preserve this provenance and the required attribution
when redistributing weather-enabled binaries.

## AI-Assisted Material

AI tools can assist drafts and implementation, but a human contributor must review and understand the final result before submission.

Do not submit material copied from proprietary sources or material with unverifiable rights.

The current first-party TrackMeUp visual assets have a dated project-owner
generation, review, and publication declaration in
[`design/branding/AI_ASSET_PROVENANCE.md`](design/branding/AI_ASSET_PROVENANCE.md).
That declaration does not cover or relicense separately attributed third-party
data, software, fonts, or media.

## Pre-Publication Check

Before release/publication, confirm:

- no credentials, tokens, private endpoints, or personal data are committed;
- dependency and asset licenses are compatible with the intended source and binary distribution;
- redistributed forks replace reserved TrackMeUp Brand Assets unless separate permission has been granted;
- generated assets have documented provenance;
- the final tracked file list has been reviewed.
