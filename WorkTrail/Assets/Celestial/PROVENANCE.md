# Celestial photographic assets

These bundled NASA observations replace the former AI-generated celestial
artwork. They represent the Sun and Moon in the world-clock panel; their
positions and the Moon's displayed phase are calculated by the application.
They are static reference photographs, not live observations of the selected
date. Sources and usage guidance were checked on 2026-09-15.

## Sun

- File: `sun-sdo.jpg`, 512 x 512 pixels.
- Source: [Quiet Corona and Upper Transition Region of the Sun](https://www.nasa.gov/image-article/quiet-corona-upper-transition-region-of-sun/).
- Download: [NASA's 512-pixel JPEG](https://www.nasa.gov/wp-content/uploads/2023/03/latest_4096_0171.jpg?w=512).
- Observation: 2013-12-31, Solar Dynamics Observatory / Atmospheric Imaging Assembly, 171 angstrom extreme ultraviolet light. The gold color is the source's scientific color mapping, not visible-light color.
- Credit: NASA/SDO. Courtesy of NASA/SDO and the AIA, EVE, and HMI science teams.
- SHA-256: `9b67b7d3fa1dc5b1e82af6e2bf742235df3e6a8d9f02cfb8c354a7110cac9170`.
- Approximate circular disk frame in the source: `(51, 51, 410, 410)` pixels, expressed as `(left, top, width, height)`.

## Moon

- File: `moon-apollo11.jpg`, 512 x 514 pixels.
- Source: [Full Moon Photographed From Apollo 11 Spacecraft](https://www.nasa.gov/image-article/full-moon-photographed-from-apollo-11-spacecraft/).
- Download: [NASA's 512-pixel JPEG](https://www.nasa.gov/wp-content/uploads/2023/03/as11-44-6667.jpg?w=512).
- Observation: Apollo 11 photograph `AS11-44-6667`, taken during the return journey from the Moon in 1969.
- Credit: NASA.
- SHA-256: `b17af75b7f9454c978dcbeb03f9fcef5e10e36ca3016c4f66af7df538a28f128`.
- Approximate circular disk frame in the source: `(79, 47, 368, 368)` pixels, expressed as `(left, top, width, height)`.

## Processing and use

The files are the JPEG renditions served by NASA at the linked URLs. No local
pixel edits, recoloring, or AI generation were applied. The presentation clips
the source framing to circular markers without stretching their proportions;
the calculated lunar-phase shadow is a separate presentation layer.

These NASA-only credited images are public-domain U.S. government material,
outside the repository's MIT grant and outside TrackMeUp Brand Assets.
[NASA's media guidelines](https://www.nasa.gov/nasa-brand-center/images-and-media/)
allow factual use without implied endorsement and request source credit. The
[SDO image permissions](https://sdo.gsfc.nasa.gov/gallery/copyright/) confirm
that SDO imagery has no copyright restrictions unless individually noted and
request the science-team attribution preserved above. Neither selected source
identifies a third-party copyright holder. No NASA logo or identifiable person
appears in either photograph. Preserve these credits when redistributing the
assets; their use does not imply NASA endorsement of TrackMeUp.
