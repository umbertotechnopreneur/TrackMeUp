# Third-Party Notices

This file is the review index for third-party code and assets distributed or referenced by TrackMeUp.

Component-specific license files remain authoritative.

## Dependencies to Review Before Release

- .NET runtime and Windows SDK dependencies used by project targets.
- WinUI and Windows App SDK packages.
- Data/storage dependencies (for example SQLite provider packages).
- Logging and observability dependencies (for example Serilog sinks and optional Sentry integration).
- Imaging/capture dependencies (for example SkiaSharp and related components).
- CLI dependencies (for example Spectre.Console).
- Reports web stack dependencies under `TrackMeUp.Reports.Web/` (for example Vue, Vuetify, ECharts, Vite, and plugins).

## Assets and Content

Track and document source/license for:

- icons, logos, and branding assets;
- screenshots and sample images used for store/distribution;
- prompt templates and generated sample outputs;
- copied examples or external snippets.

## Release Gate

When adding a dependency or asset, record:

- source URL/repository;
- version or commit;
- license type;
- required notice/attribution;
- redistribution scope in this repository.

The repository MIT license does not relicense third-party material.
