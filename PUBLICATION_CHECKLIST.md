# Public Release Checklist

Complete this checklist before promoting the repository or publishing a public release.

## Required Before Publication

- [ ] Run a secret scan on current files and repository history.
- [ ] Confirm no AI provider keys, tokens, private endpoints, or credentials are tracked.
- [ ] Confirm key flow uses environment variables and no command-line secret arguments.
- [ ] Review screenshots, reports, and sample artifacts for personal or confidential data.
- [ ] Audit dependencies and update `THIRD_PARTY_NOTICES.md`.
- [ ] Confirm every project-facing surface consistently says `source-available`, not `open source` or `MIT`.
- [ ] Confirm `LICENSE`, `NOTICE.md`, contribution terms, Store copy, and provenance records reflect the intended distribution.
- [ ] Review tracked files for private paths, local dumps, and generated artifacts.
- [ ] Validate restore/build/test with PowerShell 7.
- [ ] Verify README and governance docs links (`CONTRIBUTING.md`, `SECURITY.md`, `SUPPORT.md`).

## Validation Commands

```powershell
pwsh -NoProfile -Command "dotnet restore .\TrackMeUp.slnx"
pwsh -NoProfile -Command "dotnet build .\TrackMeUp.slnx -p:Platform=x64 -warnaserror"
pwsh -NoProfile -Command "dotnet test .\TrackMeUp.slnx -p:Platform=x64 -warnaserror"
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action Preflight
```

## After Publication

- [ ] Enable dependency and secret scanning in the repository host.
- [ ] Protect the default branch and require pull-request review.
- [ ] Publish release notes with clear validation scope and known limitations.
