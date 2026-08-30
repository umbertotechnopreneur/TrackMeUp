# World clock watercolor pilot v1

Status: design pilot; not packaged and not referenced by the runtime catalog.

Generated on 2026-08-30 with the built-in OpenAI image-generation tool. The ten selected outputs were generated from text prompts only; no external photographs or reference images were supplied. They establish the approved visual standard for the full 101-city replacement before the runtime catalog is changed.

## Selected assets

| Asset | Dimensions | Format | SHA-256 |
| --- | ---: | --- | --- |
| `paris-summer-v1.png` | 1672 x 941 | RGBA PNG | `7f854ff2dec9238d9e70963ca8fa92c4778b2d7002d7aacd2238074998eb8a13` |
| `paris-winter-v1.png` | 1672 x 941 | RGBA PNG | `9c79722b09a7c417c0421eb7ab1cfaa6a5ae23eb0d5038cf112f70a694e5e873` |
| `rome-summer-v1.png` | 1672 x 941 | RGBA PNG | `794c973ceccfb99d770aff2fac93f92252bad07f5ddff4b148f24ba17860dad1` |
| `rome-winter-v1.png` | 1672 x 941 | RGBA PNG | `3c53d84c62744b51a863b9d223c62006d1fcc1f86108648ab8edbd1c3d191c36` |
| `ho-chi-minh-city-summer-v1.png` | 1672 x 941 | RGBA PNG | `973c16e8da57e14bfe684f09a709c34b81edd45f8ce2e5a2d203a58863690419` |
| `ho-chi-minh-city-winter-v1.png` | 1672 x 941 | RGBA PNG | `81c20e59fa9b6462023544ab72759e0f505e64d2fcd3d541029e0ad68e928bbf` |
| `london-summer-v1.png` | 1672 x 941 | RGBA PNG | `09b218ab8591cca1c0d28c6e7505c79f4ef5337954b445cd1bef2fbe8b2611e7` |
| `london-winter-v1.png` | 1672 x 941 | RGBA PNG | `7a996a5bde5c54c3f8ad53005d440183da9f645e467277321692e775ab57f4c5` |
| `tokyo-summer-v1.png` | 1672 x 941 | RGBA PNG | `4356d2de99f93cae290e83b915064a2d923a2085769dc0c93d791aff9f7e9c8c` |
| `tokyo-winter-v1.png` | 1672 x 941 | RGBA PNG | `6cf99fa8e0a65dc67d0eb78ed91a1fff873d830913a71da1c806eb4ea0babb0b` |

All ten files were checked with FFmpeg: the alpha channel ranges from 0 to 255, so the empty backdrop is genuinely transparent rather than a baked checkerboard.

## Prompt specification

Common direction:

> A panoramic, hand-painted architectural watercolor for a light WinUI world-clock column. Exact 16:9 landscape; airy travel-sketch quality; restrained graphite underdrawing; broad translucent pigment masses; softly feathered edges; recognizable but not photographic. Keep the central area quiet for a large circular UI overlay, with the urban mass mostly in the lower 65% and the primary landmark on a lateral third. Real RGBA transparency outside the skyline: no sky, paper rectangle, checkerboard, border, text, people, vehicles, flags, logos, sun, moon, weather icon, signature, or watermark.

Asset-specific direction:

- Paris summer: Eiffel Tower slightly left of center, low Haussmann rooftops and Seine; warm limestone, muted cerulean, sage and dusty roof accents; clear summer atmosphere.
- Paris winter: Eiffel Tower on the left lateral third, Haussmann rooftops and Seine; cool limestone, slate blue and gray-violet, bare deciduous branches and restrained snow on roofs; no active snowfall.
- Rome summer: Colosseum dominant on the left, layered warm roofs, ruins, umbrella pines and a secondary distant dome; travertine, terracotta, olive and cypress; Mediterranean summer light.
- Rome winter: Colosseum dominant on the left lateral third, low roofs, ruins, umbrella pines, cypresses and a secondary distant dome; cooler travertine, muted umber and powder blue; mild Roman winter with evergreens retained and only a rare light dusting on roof edges.
- Ho Chi Minh City summer: Landmark 81 and Bitexco on opposite lateral thirds with a low Saigon River wash; jade, turquoise and warm concrete; humid tropical atmosphere.
- Ho Chi Minh City winter: the technical winter id represents the warm dry season; Landmark 81, Bitexco and tropical foliage remain; celadon, sand and muted teal with no cold cues.
- London summer: Elizabeth Tower on the left lateral third and Tower Bridge low on the right; Portland stone, muted cerulean, sage and restrained brick accents.
- London winter: the same landmark hierarchy in slate, soft stone gray and blue-violet, with bare branches and restrained frost but no active snowfall.
- Tokyo summer: Tokyo Tower on the right lateral third and a secondary Mount Fuji on the left; muted indigo, sea green and minimal vermilion.
- Tokyo winter: Tokyo Tower remains dominant, with sumi, Prussian blue and pale gray; snow is restricted to distant Mount Fuji and there is no falling snow.

Two edit-based winter drafts were rejected because the generated files flattened a checkerboard into an opaque RGB background. They are not included in this pilot.
