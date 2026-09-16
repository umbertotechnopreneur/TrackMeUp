# Preparing a GitHub release

TrackMeUp produces native **x64** and **ARM64** MSIX packages. The `release packages`
workflow prepares both architectures from the same commit and version. Until a
distribution signing identity is configured, all application packages are **unsigned**
and are not installable releases.

## GitHub Actions

- Run **release packages** manually with a version such as `1.2.3` to produce downloadable
  workflow artifacts only.
- Push an approved `vX.Y.Z` tag to generate the same artifacts and attach them to a
  **draft** GitHub Release. The workflow never publishes the draft and refuses to
  replace an existing release for that tag.
- Versions have three numeric components: major `1..65535`, minor and patch `0..65535`,
  with no leading zeros or prerelease suffixes. Both MSIX packages use `X.Y.Z.0`.
- Each archive and its `.zip.sha256` file are retained as workflow artifacts for 30 days.
  Draft Release assets remain available to maintainers independently of that retention.

The workflow pins its actions by commit, grants package jobs read-only repository
access, and gives only the draft-creation job `contents: write`. No signing secrets
or development certificates are used or exported.

## Local preparation

Use PowerShell 7, the .NET SDK selected by `global.json`, Windows SDK build tools,
and Node.js 24.16.0. Run these commands sequentially from the repository root:

```powershell
$releaseVersion = '1.2.3'
foreach ($releasePlatform in @('x64', 'ARM64')) {
    $packageDirectory = "./artifacts/release-packages/$releaseVersion/$releasePlatform"
    pwsh -NoProfile -File ./scripts/TrackMeUp.ps1 -Action PackageMsix -Platform $releasePlatform -ReleaseVersion $releaseVersion -Unsigned -PackageOutputPath $packageDirectory
    if ($LASTEXITCODE -ne 0) { throw "Packaging failed: $releasePlatform" }
    pwsh -NoProfile -File ./scripts/New-ReleaseArchive.ps1 -PackageDirectory $packageDirectory -Version $releaseVersion -Platform $releasePlatform
    if ($LASTEXITCODE -ne 0) { throw "Archive creation failed: $releasePlatform" }
}
```

Explicit packaging directories must be empty. Archive output directories must not
already exist; choose a fresh `-OutputDirectory` for repeat verification. Existing
validated artifacts are never silently overwritten. Local builds share generated
project files, so do not package both architectures concurrently in one checkout.
GitHub matrix jobs use separate checkouts.

Release builds generate their manifest and `BuildInfo.json` under
`TrackMeUp/obj/release/<version>/<platform>/`; they do not advance the local build
counter or rewrite the tracked package manifest. Normal local builds retain their
automatic version increment.

## Archive contents and dependencies

Each `artifacts/releases/<version>/<platform>/TrackMeUp-<version>-<platform>-unsigned.zip`
contains:

- the unsigned application MSIX, including its .NET runtime;
- the matching Microsoft-signed framework packages from the SDK's `Dependencies`
  output, validated against the application manifest's name, publisher, minimum
  version, and architecture;
- `release.json` with package versions and hashes, and `SHA256SUMS.txt` covering the
  payload files;
- `Install.ps1`, preparation instructions, and license/trademark notices.

Only dependencies for the target architecture or neutral architecture are copied.
The current application requires `Microsoft.WindowsAppRuntime.2`; the packaging
scripts consume the SDK's emitted files and do not download an arbitrary latest
runtime. Missing, ambiguous, or untrusted dependencies fail preparation.

`Install.ps1` refuses unsigned archives before making changes. Once a signed release
has been prepared, it verifies package hashes, signatures, architecture, version,
and dependency coverage before registering the app with `Add-AppxPackage`. It never
imports a certificate or changes certificate trust. The helper needs PowerShell 7.

## Completing signing later

Before publishing a public release:

1. Configure the chosen signing provider and its publisher identity. The manifest
   publisher must match the certificate. Keep credentials in the provider/GitHub
   secret mechanism, never command-line arguments or repository files.
2. Add the provider's signing step after package creation and before archive/hash
   generation. Extend archive generation to accept and validate the signed application,
   record `signing: signed`, and remove the unsigned filename/instructions. The current
   archive script deliberately accepts only unsigned preparation packages.
3. Regenerate all hashes after signing; never relabel an existing unsigned ZIP as signed.
   A valid signature does not by itself guarantee immediate SmartScreen reputation.
4. Test installation, upgrade, launch, CLI access, and uninstall on clean Windows
   x64 and ARM64 machines. Cross-compilation does not verify execution on ARM64.
5. Replace the preparation assets in the draft with the validated signed archives,
   review the release notes, and publish explicitly.

Run the script contract tests with:

```powershell
pwsh -NoProfile -File ./scripts/Test-ReleasePackaging.ps1
```

References: [GitHub Releases](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository),
[Microsoft signing options](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options),
and [Add-AppxPackage dependencies](https://learn.microsoft.com/en-us/powershell/module/appx/add-appxpackage).
