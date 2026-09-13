# Windows contributor setup

This guide takes a clean Windows clone through the report build, an x64 solution build, and automated tests. It does not require launching TrackMeUp, installing a package, enabling tracking, or configuring an AI provider.

Read [CONTRIBUTING.md](../CONTRIBUTING.md), [AGENTS.md](../AGENTS.md), and the [repository instructions](../.github/copilot-instructions.md) before editing. Run the commands below from the repository root unless a step says otherwise.

## Prepare the Windows tools

| Component | Contributor requirement |
| --- | --- |
| Windows | Use an x64 Windows 11 development machine for this path. The app declares Windows 10 build 17763 as its minimum and targets Windows API build 19041; those project values are not a statement that every older Windows edition remains supported by the development tools. Consult Microsoft's [.NET Windows support table](https://learn.microsoft.com/en-us/dotnet/core/install/windows#supported-versions) for supported hosts. |
| Git | Install Git for Windows and make `git` available on `PATH`. |
| PowerShell | Install PowerShell 7 and make `pwsh` available on `PATH`. Every repository PowerShell invocation must use `-NoProfile`. Windows PowerShell 5.1 is unsupported. |
| .NET SDK | Install the **SDK**, not only a runtime. [global.json](../global.json) requests `10.0.400` with `latestPatch` roll-forward and prereleases disabled: a compatible stable `10.0.4xx` SDK must be installed. A different .NET major version or feature band alone does not satisfy that selection. |
| Windows/WinUI tools | For the standard Visual Studio setup, use an updated Visual Studio 2026 installation compatible with the selected SDK and select **WinUI application development**, including its C# and Windows SDK components. Microsoft documents the workload in its [WinUI setup guide](https://learn.microsoft.com/en-us/windows/apps/get-started/start-here#set-up-your-development-environment). The repository does not prescribe a `dotnet workload install` command or require a MAUI workload. |
| Node.js and npm | Use **Node.js 24.16.0**, the runtime pinned by [CI](../.github/workflows/build.yml), with its bundled npm. The [report package](../TrackMeUp.Reports.Web/package.json) declares the supported engine ranges; the pinned CI version gives the reproducible contributor path. Node 20 is unsupported by the current report test toolchain. |
| Network access | Initial restore needs access to the configured NuGet and npm registries. No AI-provider account or API key is required for the build or automated tests. |

The app's [project file](../TrackMeUp/TrackMeUp.csproj) pins its Windows App SDK and Windows SDK BuildTools NuGet dependencies. Restore those versions; do not replace project references or retarget Windows to repair a missing local tool installation. The Windows SDK BuildTools package version is distinct from the project's target API version and minimum OS version.

Developer Mode is relevant to local package deployment/debugging. Configure it only when performing that separate validation, following the Microsoft WinUI setup guide.

## Clone and check the environment

From a folder where you keep source repositories:

```powershell
pwsh -NoProfile -Command 'git clone https://github.com/umbertotechnopreneur/TrackMeUp.git'
```

Open a terminal in the resulting `TrackMeUp` folder, containing `TrackMeUp.slnx` and `global.json`. Check the tools from that folder so .NET SDK selection uses the repository's settings:

```powershell
pwsh -NoProfile -Command 'git --version'
pwsh -NoProfile -Command '$PSVersionTable.PSVersion'
pwsh -NoProfile -Command 'dotnet --list-sdks'
pwsh -NoProfile -Command 'dotnet --version'
pwsh -NoProfile -Command 'node --version'
pwsh -NoProfile -Command 'npm --version'
pwsh -NoProfile -File ./scripts/TrackMeUp.ps1 -Action Preflight
```

Expect PowerShell 7, the selected stable `10.0.4xx` SDK, and Node `v24.16.0`. Stop at a failed command and resolve it before continuing.

`Preflight` checks PowerShell 7, the presence of `pwsh`, `git`, and `dotnet`, and four required repository files. It does **not** validate the selected .NET SDK, Node/npm, installed Windows workloads, OCR languages, or signing certificates. Its implementation is in [scripts/TrackMeUp.ps1](../scripts/TrackMeUp.ps1).

Enable the repository hook once per clone:

```powershell
pwsh -NoProfile -File ./scripts/Install-GitHooks.ps1
```

An existing custom hook configuration causes an explicit error. Integrate the formatter with those hooks before changing their configuration; do not bypass the check.

## Validate a clean x64 checkout

Run this sequence once after setup, or once the changes being validated are complete. It uses the same Release-Unpackaged configuration and analyzer settings as the [release CI job](../.github/workflows/build.yml).

First check the source contracts and build the report distribution:

```powershell
pwsh -NoProfile -File ./scripts/Test-SourceLicenseHeaders.ps1
pwsh -NoProfile -File ./scripts/Format-Code.ps1 -Verify
pwsh -NoProfile -File ./scripts/Test-FormattingHooks.ps1
pwsh -NoProfile -File ./scripts/TrackMeUp.ps1 -Action BuildReports
pwsh -NoProfile -Command 'git status --short --untracked-files=all -- TrackMeUp.Reports.Web/dist'
```

`BuildReports` runs `npm ci` and `npm run build`. The build includes TypeScript checking, Vitest tests, production bundling, third-party notices, and production-output validation. A clean clone should produce no changes in the final `git status` command. The distribution is tracked because it ships with the desktop app.

Then restore, build, and test x64:

```powershell
pwsh -NoProfile -Command 'dotnet restore ./TrackMeUp.slnx -p:Configuration=Release-Unpackaged -p:Platform=x64'
pwsh -NoProfile -Command 'dotnet build ./TrackMeUp.slnx --configuration Release-Unpackaged -p:Platform=x64 --no-restore -p:ContinuousIntegrationBuild=true -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true -p:TreatWarningsAsErrors=true'
pwsh -NoProfile -Command 'dotnet test ./TrackMeUp.slnx --configuration Release-Unpackaged -p:Platform=x64 --no-build --no-restore -m:1'
```

Success means the commands finish with exit code zero, the build has no warnings/errors, and every test project reports a passing result. This establishes automated build/test evidence for the checked commit and machine. CI also restores and builds x86 and ARM64; it executes the release test suite on x64.

The repository utility offers shorter everyday equivalents:

```powershell
pwsh -NoProfile -File ./scripts/TrackMeUp.ps1 -Action Restore
pwsh -NoProfile -File ./scripts/TrackMeUp.ps1 -Action Build -Configuration Debug-Unpackaged -Platform x64 -WarnAsError
pwsh -NoProfile -File ./scripts/TrackMeUp.ps1 -Action Test -Configuration Debug-Unpackaged -Platform x64 -WarnAsError
```

These are an alternative development loop, not additional required runs after the Release validation above. `Restore` restores the solution; `Build` and `Test` invoke the corresponding solution commands. Their configuration defaults to `Debug-Unpackaged`; only `Debug-Unpackaged` and `Release-Unpackaged` are supported by those actions. `Test` can rebuild, and `-SkipRestore` does not apply to these Build/Test actions.

The direct app build documented in the repository rules is also supported:

```powershell
pwsh -NoProfile -Command 'dotnet build ./TrackMeUp/TrackMeUp.csproj -p:Platform=x64'
```

That command builds the app project's default `Debug` configuration. It does not replace the solution tests or demonstrate deployment. Use x64, x86, or ARM64 explicitly; do not build WinUI as AnyCPU.

## Before submitting C# changes

Run the formatter and then its read-only verification:

```powershell
pwsh -NoProfile -File ./scripts/Format-Code.ps1
pwsh -NoProfile -File ./scripts/Format-Code.ps1 -Verify
```

The [.editorconfig](../.editorconfig) and [.gitattributes](../.gitattributes) policy is four-space C# indentation, LF line endings, a final newline, and no trailing whitespace. The pre-commit hook formats the staged snapshot. Fully staged files receive those corrections in the working copy; partially staged files preserve their unstaged bytes. A staged `.editorconfig` change applies its rules to indexed C# sources. Formatting does not require NuGet restore.

Preserve unrelated edits. Keep `bin/`, `obj/`, `artifacts/`, and `.vs/` out of commits. Follow the repository's version-metadata exclusion rather than inspecting or staging automatic version updates. After a successful commit and push, perform the applicable x64 `dotnet clean` and remove stale test build outputs as required by [AGENTS.md](../AGENTS.md); resolve and verify any recursive-cleanup paths first.

## Troubleshoot the failing stage

| Failure | Action |
| --- | --- |
| `pwsh`, `git`, `dotnet`, `node`, or `npm` is missing | Install the corresponding tool, reopen the terminal, and rerun its version command from the repository root. Passing Preflight does not establish Node/npm availability. |
| .NET reports that the SDK in `global.json` cannot be found, or `NETSDK1045` | Compare `dotnet --list-sdks` with `global.json`; install a stable compatible `10.0.4xx` SDK and check which `dotnet` is on `PATH`. A runtime-only installation is insufficient. Update the IDE if its MSBuild does not support the SDK. Do not edit `global.json` simply to use an older installation. |
| Windows SDK, XAML compiler, packaging target, or WinUI component is missing | Repair the Visual Studio WinUI workload/Windows SDK components identified in the error and restore the solution again. Check [the app project](../TrackMeUp/TrackMeUp.csproj) for its actual targets. Restart the IDE after changing installed components. |
| Unsupported solution configuration or platform | Use `Debug-Unpackaged` or `Release-Unpackaged` for the solution and x64/x86/ARM64 for the platform. `Debug` and `Release` are app-project MSIX configurations, not solution configurations. |
| NuGet or npm restore fails | Check the first registry/network/proxy/certificate error and the configured package sources. Keep the checked-in lockfile; do not replace `npm ci` with a dependency update to mask a clean-restore failure. |
| A report entry point/production notice is missing, or CI says the tracked distribution is stale | Use Node 24.16.0, ensure report source files use LF as required by `.gitattributes`, and rerun `BuildReports`. Changing Git attributes does not rewrite existing working files; mixed line endings can change Vue's generated scope IDs and bundle hashes. The app build checks required files exist; CI additionally compares a clean rebuild with tracked `dist/`. Review generated changes together with the report source/lockfile changes that caused them. An unexplained difference on a clean clone should be reported with the commit and tool versions. |
| Formatting verification or the pre-commit hook fails | Run `Format-Code.ps1`, review the changes, then run `-Verify`. Check LF and staged `.editorconfig` rules. Resolve the failure without bypassing the hook; preserve unrelated unstaged edits. |
| Tests cannot find assemblies after cleaning | Run the matching configuration/platform build before using `--no-build`. Solution `Release-Unpackaged` maps supporting/test projects to their `Release` builds; avoid guessing a test DLL path from the app's output directory. |
| Windows OCR reports an unavailable language | Install the selected Windows OCR language capability, then reopen the app and select an available recognizer. Display language and OCR language are independent; Vietnamese UI support does not imply a Vietnamese Windows OCR recognizer. Explicit unsupported OCR languages fail instead of silently selecting another. |
| OCR engine initialization fails in an unpackaged run | Validate OCR using an installed MSIX with package identity and the selected recognizer installed. See [TrackMeUp.Ocr](../TrackMeUp.Ocr/README.md). An unpackaged UI smoke test cannot establish packaged OCR support. |
| Package signing fails or an MSIX cannot be installed | Check the signing tools, certificate private key, validity, publisher match, and required trust on the target machine. See the separate packaging section below. Compilation success does not verify those prerequisites. |

Windows exposes installed recognizers through `OcrEngine.AvailableRecognizerLanguages`; OCR capability installation is documented under Microsoft's [language Features on Demand](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/features-on-demand-language-fod?view=windows-11). OCR is optional and is not a prerequisite for compiling the solution.

## Keep validation evidence separate

| Evidence | What to record |
| --- | --- |
| Build and automated tests | Commit, Windows/tool versions, exact configuration/platform and commands, exit results, and test summaries. No app launch or installation is implied. |
| Unpackaged runtime | The published artifact used, its architecture, and the UI/CLI scenario actually exercised. A successful publish alone is not a successful launch. |
| Installed package | The exact signed package, successful deployment, and the scenario exercised through that installed app. Include OCR language/package-identity evidence when testing OCR. A signed archive alone is not an installation test. |

For a separate unpackaged-artifact task:

```powershell
pwsh -NoProfile -File ./scripts/TrackMeUp.ps1 -Action PublishUnpackaged -Platform x64
```

This publishes `Release-Unpackaged` to `artifacts/unpackaged/x64/`, using the [x64 publish profile](../TrackMeUp/Properties/PublishProfiles/win-x64.pubxml). It replaces that generated output directory and includes the self-contained .NET/Windows App SDK deployment. It does not launch the executable. Build reports first if their sources changed; this action does not run `BuildReports`.

For a separate local MSIX packaging task:

```powershell
pwsh -NoProfile -File ./scripts/TrackMeUp.ps1 -Action PackageMsix -Platform x64
```

`PackageMsix` rebuilds reports, resolves a certificate, restores/cleans/publishes the packaged `Release` configuration, and checks that the archive contains a signature and its required report assets. That archive check does not verify certificate trust on another machine. It writes under `artifacts/packages/`. `CreateInstaller` creates the final installer under `artifacts/installers/` and normally runs the packaging step first.

These packaging actions change certificate state: by default the script creates or reuses the current-user `TrackMeUp Test Signing` certificate with subject `CN=umber`, exports its public certificate under `artifacts/certificates/`, and trusts that test certificate in the current-user stores. A supplied `-PackageCertificateThumbprint` must identify a certificate with a private key in `Cert:\CurrentUser\My`. Its subject must match the package publisher. Use the existing test path only for local sideloading; distribution requires an appropriate signing certificate. Microsoft explains the [certificate and publisher requirements](https://learn.microsoft.com/en-us/windows/msix/package/create-certificate-package-signing).

Package creation does not install or launch TrackMeUp. Perform those steps only for an intended runtime/deployment validation and record them separately using [docs/VALIDATION.md](VALIDATION.md). For documentation-only contributions, review the commands and links without installing or launching the app.

When reporting failures, include the stage, sanitized error, commit, and tool versions. Keep credentials, tokens, personal activity data, and private machine paths out of issue bodies and attached logs.
