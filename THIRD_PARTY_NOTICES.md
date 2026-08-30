# Third-Party Notices

TrackMeUp's project-authored software and documentation are governed by the
[MIT License](LICENSE), unless a file states otherwise. The TrackMeUp name and
brand assets are governed separately by [the trademark and brand policy](TRADEMARKS.md).
Each third-party component, data set, or asset retains its own license terms;
this inventory does not relicense third-party material as MIT.

This file inventories the direct `PackageReference` dependencies declared in
tracked `.csproj` files, the production dependency closure used to build the
tracked web report bundle, and distributed world-clock data/media assets.

- NuGet scope: direct dependencies declared in tracked project files as of 2026-08-29. Transitive NuGet packages are not included yet.
- Web scope: all production packages resolved by `TrackMeUp.Reports.Web/package-lock.json`; development-only packages are excluded.
- Source of truth: repository `*.csproj` files, official NuGet package metadata, the npm lockfile, and installed package license/notice files.
- Special cases: packages that publish a bundled license file instead of a NuGet SPDX expression are called out explicitly below.
- Binary-release boundary: before distributing a self-contained installer, generate and package notices for the complete published runtime closure, including transitive NuGet/runtime/native components. This source inventory alone is not a complete binary-distribution notice bundle.

## Summary

- Runtime dependencies: 21 unique packages.
- Test-only dependencies: 4 unique packages.
- Web report production dependencies: 29 unique packages.
- Open-source licenses observed: `MIT`, `Apache-2.0`, `BSD-2-Clause`, `BSD-3-Clause`, `ISC`, `0BSD`, plus the separately recorded Creative Commons/public-domain asset terms.
- Additional Microsoft package terms observed: `Microsoft.WindowsAppSDK`, `Microsoft.Windows.SDK.BuildTools`.

## Runtime Dependencies

| Package | Badges | License / terms | Used by |
| --- | --- | --- | --- |
| [Lucene.Net](https://www.nuget.org/packages/Lucene.Net/4.8.0-beta00018) | [![NuGet](https://img.shields.io/nuget/v/Lucene.Net?label=NuGet)](https://www.nuget.org/packages/Lucene.Net/4.8.0-beta00018) [![License](https://img.shields.io/badge/license-Apache-2.0-orange)](https://licenses.nuget.org/Apache-2.0) | [Apache-2.0](https://licenses.nuget.org/Apache-2.0) (embedded LICENSE.txt) | TrackMeUp.Search |
| [Lucene.Net.Analysis.Common](https://www.nuget.org/packages/Lucene.Net.Analysis.Common/4.8.0-beta00018) | [![NuGet](https://img.shields.io/nuget/v/Lucene.Net.Analysis.Common?label=NuGet)](https://www.nuget.org/packages/Lucene.Net.Analysis.Common/4.8.0-beta00018) [![License](https://img.shields.io/badge/license-Apache-2.0-orange)](https://licenses.nuget.org/Apache-2.0) | [Apache-2.0](https://licenses.nuget.org/Apache-2.0) (embedded LICENSE.txt) | TrackMeUp.Search |
| [Lucene.Net.Suggest](https://www.nuget.org/packages/Lucene.Net.Suggest/4.8.0-beta00018) | [![NuGet](https://img.shields.io/nuget/v/Lucene.Net.Suggest?label=NuGet)](https://www.nuget.org/packages/Lucene.Net.Suggest/4.8.0-beta00018) [![License](https://img.shields.io/badge/license-Apache-2.0-orange)](https://licenses.nuget.org/Apache-2.0) | [Apache-2.0](https://licenses.nuget.org/Apache-2.0) (embedded LICENSE.txt) | TrackMeUp.Search |
| [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite/10.0.10) | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Data.Sqlite?label=NuGet)](https://www.nuget.org/packages/Microsoft.Data.Sqlite/10.0.10) [![License](https://img.shields.io/badge/license-MIT-green)](https://licenses.nuget.org/MIT) | [MIT](https://licenses.nuget.org/MIT) | TrackMeUp.Core |
| [Microsoft.Extensions.DependencyInjection](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection/10.0.10) | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Extensions.DependencyInjection?label=NuGet)](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection/10.0.10) [![License](https://img.shields.io/badge/license-MIT-green)](https://licenses.nuget.org/MIT) | [MIT](https://licenses.nuget.org/MIT) | TrackMeUp |
| [Microsoft.Extensions.Logging](https://www.nuget.org/packages/Microsoft.Extensions.Logging/10.0.10) | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Extensions.Logging?label=NuGet)](https://www.nuget.org/packages/Microsoft.Extensions.Logging/10.0.10) [![License](https://img.shields.io/badge/license-MIT-green)](https://licenses.nuget.org/MIT) | [MIT](https://licenses.nuget.org/MIT) | TrackMeUp |
| [Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions/10.0.10) | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Extensions.Logging.Abstractions?label=NuGet)](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions/10.0.10) [![License](https://img.shields.io/badge/license-MIT-green)](https://licenses.nuget.org/MIT) | [MIT](https://licenses.nuget.org/MIT) | TrackMeUp.Core |
| [Microsoft.Windows.SDK.BuildTools](https://www.nuget.org/packages/Microsoft.Windows.SDK.BuildTools/10.0.28000.2526) | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Windows.SDK.BuildTools?label=NuGet)](https://www.nuget.org/packages/Microsoft.Windows.SDK.BuildTools/10.0.28000.2526) [![License](https://img.shields.io/badge/license-Microsoft%20Windows%20SDK%20license%20terms-blue)](https://aka.ms/WinSDKLicenseURL) | [Microsoft Windows SDK license terms](https://aka.ms/WinSDKLicenseURL) (nuspec licenseUrl) | TrackMeUp |
| [Microsoft.WindowsAppSDK](https://www.nuget.org/packages/Microsoft.WindowsAppSDK/2.3.1) | [![NuGet](https://img.shields.io/nuget/v/Microsoft.WindowsAppSDK?label=NuGet)](https://www.nuget.org/packages/Microsoft.WindowsAppSDK/2.3.1) [![License](https://img.shields.io/badge/license-Microsoft%20Software%20License%20Terms-blue)](https://www.nuget.org/packages/Microsoft.WindowsAppSDK/2.3.1) | [Microsoft Software License Terms](https://www.nuget.org/packages/Microsoft.WindowsAppSDK/2.3.1) (bundled license.txt) | TrackMeUp |
| [Sentry.Extensions.Logging](https://www.nuget.org/packages/Sentry.Extensions.Logging/6.7.0) | [![NuGet](https://img.shields.io/nuget/v/Sentry.Extensions.Logging?label=NuGet)](https://www.nuget.org/packages/Sentry.Extensions.Logging/6.7.0) [![License](https://img.shields.io/badge/license-MIT-green)](https://licenses.nuget.org/MIT) | [MIT](https://licenses.nuget.org/MIT) | TrackMeUp |
| [Serilog](https://www.nuget.org/packages/Serilog/4.4.0) | [![NuGet](https://img.shields.io/nuget/v/Serilog?label=NuGet)](https://www.nuget.org/packages/Serilog/4.4.0) [![License](https://img.shields.io/badge/license-Apache-2.0-orange)](https://licenses.nuget.org/Apache-2.0) | [Apache-2.0](https://licenses.nuget.org/Apache-2.0) | TrackMeUp |
| [Serilog.Extensions.Logging](https://www.nuget.org/packages/Serilog.Extensions.Logging/10.0.0) | [![NuGet](https://img.shields.io/nuget/v/Serilog.Extensions.Logging?label=NuGet)](https://www.nuget.org/packages/Serilog.Extensions.Logging/10.0.0) [![License](https://img.shields.io/badge/license-Apache-2.0-orange)](https://licenses.nuget.org/Apache-2.0) | [Apache-2.0](https://licenses.nuget.org/Apache-2.0) | TrackMeUp |
| [Serilog.Sinks.Console](https://www.nuget.org/packages/Serilog.Sinks.Console/6.1.1) | [![NuGet](https://img.shields.io/nuget/v/Serilog.Sinks.Console?label=NuGet)](https://www.nuget.org/packages/Serilog.Sinks.Console/6.1.1) [![License](https://img.shields.io/badge/license-Apache-2.0-orange)](https://licenses.nuget.org/Apache-2.0) | [Apache-2.0](https://licenses.nuget.org/Apache-2.0) | TrackMeUp |
| [Serilog.Sinks.File](https://www.nuget.org/packages/Serilog.Sinks.File/7.0.0) | [![NuGet](https://img.shields.io/nuget/v/Serilog.Sinks.File?label=NuGet)](https://www.nuget.org/packages/Serilog.Sinks.File/7.0.0) [![License](https://img.shields.io/badge/license-Apache-2.0-orange)](https://licenses.nuget.org/Apache-2.0) | [Apache-2.0](https://licenses.nuget.org/Apache-2.0) | TrackMeUp |
| [SkiaSharp](https://www.nuget.org/packages/SkiaSharp/4.151.0) | [![NuGet](https://img.shields.io/nuget/v/SkiaSharp?label=NuGet)](https://www.nuget.org/packages/SkiaSharp/4.151.0) [![License](https://img.shields.io/badge/license-MIT-green)](https://licenses.nuget.org/MIT) | [MIT](https://licenses.nuget.org/MIT) | TrackMeUp.Core |
| [SkiaSharp.NativeAssets.Win32](https://www.nuget.org/packages/SkiaSharp.NativeAssets.Win32/4.151.0) | [![NuGet](https://img.shields.io/nuget/v/SkiaSharp.NativeAssets.Win32?label=NuGet)](https://www.nuget.org/packages/SkiaSharp.NativeAssets.Win32/4.151.0) [![License](https://img.shields.io/badge/license-MIT-green)](https://licenses.nuget.org/MIT) | [MIT](https://licenses.nuget.org/MIT) | TrackMeUp.Core |
| [Spectre.Console](https://www.nuget.org/packages/Spectre.Console/0.57.2) | [![NuGet](https://img.shields.io/nuget/v/Spectre.Console?label=NuGet)](https://www.nuget.org/packages/Spectre.Console/0.57.2) [![License](https://img.shields.io/badge/license-MIT-green)](https://licenses.nuget.org/MIT) | [MIT](https://licenses.nuget.org/MIT) | TrackMeUp.Cli |
| [SQLitePCLRaw.lib.e_sqlite3](https://www.nuget.org/packages/SQLitePCLRaw.lib.e_sqlite3/2.1.12) | [![NuGet](https://img.shields.io/nuget/v/SQLitePCLRaw.lib.e_sqlite3?label=NuGet)](https://www.nuget.org/packages/SQLitePCLRaw.lib.e_sqlite3/2.1.12) [![License](https://img.shields.io/badge/license-Apache-2.0-orange)](https://licenses.nuget.org/Apache-2.0) | [Apache-2.0](https://licenses.nuget.org/Apache-2.0) | TrackMeUp.Core |
| [System.Diagnostics.PerformanceCounter](https://www.nuget.org/packages/System.Diagnostics.PerformanceCounter/10.0.10) | [![NuGet](https://img.shields.io/nuget/v/System.Diagnostics.PerformanceCounter?label=NuGet)](https://www.nuget.org/packages/System.Diagnostics.PerformanceCounter/10.0.10) [![License](https://img.shields.io/badge/license-MIT-green)](https://licenses.nuget.org/MIT) | [MIT](https://licenses.nuget.org/MIT) | TrackMeUp.Core |
| [System.Drawing.Common](https://www.nuget.org/packages/System.Drawing.Common/10.0.10) | [![NuGet](https://img.shields.io/nuget/v/System.Drawing.Common?label=NuGet)](https://www.nuget.org/packages/System.Drawing.Common/10.0.10) [![License](https://img.shields.io/badge/license-MIT-green)](https://licenses.nuget.org/MIT) | [MIT](https://licenses.nuget.org/MIT) | TrackMeUp.Core |
| [System.Management](https://www.nuget.org/packages/System.Management/10.0.10) | [![NuGet](https://img.shields.io/nuget/v/System.Management?label=NuGet)](https://www.nuget.org/packages/System.Management/10.0.10) [![License](https://img.shields.io/badge/license-MIT-green)](https://licenses.nuget.org/MIT) | [MIT](https://licenses.nuget.org/MIT) | TrackMeUp.Core |

## Test-Only Dependencies

| Package | Badges | License / terms | Used by |
| --- | --- | --- | --- |
| [Microsoft.NET.Test.Sdk](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/18.8.1) | [![NuGet](https://img.shields.io/nuget/v/Microsoft.NET.Test.Sdk?label=NuGet)](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/18.8.1) [![License](https://img.shields.io/badge/license-MIT-green)](https://licenses.nuget.org/MIT) | [MIT](https://licenses.nuget.org/MIT) | TrackMeUp.Cli.Tests, TrackMeUp.Core.Tests, TrackMeUp.Ocr.Tests, TrackMeUp.Presentation.Tests, TrackMeUp.Search.Tests |
| [Spectre.Console.Testing](https://www.nuget.org/packages/Spectre.Console.Testing/0.57.2) | [![NuGet](https://img.shields.io/nuget/v/Spectre.Console.Testing?label=NuGet)](https://www.nuget.org/packages/Spectre.Console.Testing/0.57.2) [![License](https://img.shields.io/badge/license-MIT-green)](https://licenses.nuget.org/MIT) | [MIT](https://licenses.nuget.org/MIT) | TrackMeUp.Cli.Tests |
| [xunit](https://www.nuget.org/packages/xunit/2.9.3) | [![NuGet](https://img.shields.io/nuget/v/xunit?label=NuGet)](https://www.nuget.org/packages/xunit/2.9.3) [![License](https://img.shields.io/badge/license-Apache-2.0-orange)](https://licenses.nuget.org/Apache-2.0) | [Apache-2.0](https://licenses.nuget.org/Apache-2.0) | TrackMeUp.Cli.Tests, TrackMeUp.Core.Tests, TrackMeUp.Ocr.Tests, TrackMeUp.Presentation.Tests, TrackMeUp.Search.Tests |
| [xunit.runner.visualstudio](https://www.nuget.org/packages/xunit.runner.visualstudio/3.1.5) | [![NuGet](https://img.shields.io/nuget/v/xunit.runner.visualstudio?label=NuGet)](https://www.nuget.org/packages/xunit.runner.visualstudio/3.1.5) [![License](https://img.shields.io/badge/license-Apache-2.0-orange)](https://licenses.nuget.org/Apache-2.0) | [Apache-2.0](https://licenses.nuget.org/Apache-2.0) | TrackMeUp.Cli.Tests, TrackMeUp.Core.Tests, TrackMeUp.Ocr.Tests, TrackMeUp.Presentation.Tests, TrackMeUp.Search.Tests |

## Embedded web report production bundle

The tracked `TrackMeUp.Reports.Web/dist` output is packaged with the Windows
application. Its complete 29-package production dependency closure contains:

| Declared license | Package count |
| --- | ---: |
| `MIT` | 22 |
| `Apache-2.0` | 2 |
| `BSD-2-Clause` | 1 |
| `BSD-3-Clause` | 2 |
| `ISC` | 1 |
| `0BSD` | 1 |

Exact package names, versions, declared licenses, copyright/license texts, and
the ECharts `NOTICE` are generated into
[`TrackMeUp.Reports.Web/dist/THIRD_PARTY_NOTICES.md`](TrackMeUp.Reports.Web/dist/THIRD_PARTY_NOTICES.md).
Regenerate that file deterministically with `npm run notices:production` from
`TrackMeUp.Reports.Web`; the generator fails when the lockfile, installed
package metadata, or required license/notice files are unavailable or
inconsistent.

## Distributed world-clock data, weather, and media

- Capital-city coordinates, population, and IANA time zones are derived from [GeoNames cities15000](https://download.geonames.org/export/dump/), licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).
- Optional current observations are supplied directly by OpenWeather under the API key and plan selected by the person running TrackMeUp. OpenWeather data is provider material, is not covered by the repository MIT License, and remains subject to the provider's applicable service, data, attribution, redistribution, and plan terms. The application preserves visible linked provider attribution whenever that weather is shown.
- `TrackMeUp/Assets/WorldClocks/ThirdParty/OpenWeather/ow_logo.svg` is the official OpenWeather attribution mark downloaded from [OpenWeather's published SVG](https://openweathermap.org/payload/api/media/file/ow_logo.svg). Its SHA-256 is `fd0ad613ebcdb5f013df98bf75603c83fe1f3f0a5f677118b99557da8ac9281c`. The mark is third-party provider artwork, not TrackMeUp-authored material, TrackMeUp Brand Assets, or MIT-licensed repository artwork; preserve it unchanged with its attribution when redistributing weather-enabled binaries.
- The bundled seasonal Urban Wash skyline files and composable atmosphere overlays are TrackMeUp-directed project artwork, not third-party Wikimedia derivatives. They are outside the repository MIT grant and were explicitly authorized by the project owner for public publication on 2026-08-30, as recorded in `TrackMeUp/Assets/WorldClocks/PROVENANCE.md`, `TrackMeUp/Assets/WorldClocks/Overlays/PROVENANCE.md`, and `ASSET_LICENSING.md`. Exact skyline SHA-256 values are distributed in `ATTRIBUTION.md`, `ATTRIBUTION.json`, and the SQLite catalog.

Other repository artwork is mapped in [`ASSET_LICENSING.md`](ASSET_LICENSING.md).
That record distinguishes owner-attested first-party assets from third-party
media while preserving the separate license scope of each category.

## Notes

- The three `Lucene.Net*` packages publish an embedded `LICENSE.txt`; this inventory resolves them as `Apache-2.0` from that bundled file.
- `Microsoft.WindowsAppSDK` publishes a bundled `license.txt` with Microsoft software license terms, not an OSS SPDX expression.
- `Microsoft.Windows.SDK.BuildTools` does not publish an SPDX license expression in NuGet metadata; this inventory links the package's official Windows SDK license URL.
