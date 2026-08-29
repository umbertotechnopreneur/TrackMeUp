# Public Release Checklist

Complete this checklist before promoting the repository or publishing a public release.

## Required Before Publication

- [ ] Run a secret scan on current files and repository history.
- [ ] Confirm no AI provider keys, tokens, private endpoints, or credentials are tracked.
- [ ] Confirm the tracked Store workflow remains validation-only and contains no Partner Center credentials, mutation, or publish command.
- [ ] Before adding Store automation, verify the exact submission scope, create a protected `microsoft-store` environment with required reviewers and no self-review, restrict it to `main`, and keep credentials out of process arguments.
- [ ] Review screenshots, reports, and sample artifacts for personal or confidential data.
- [ ] Audit direct and transitive dependencies and update `THIRD_PARTY_NOTICES.md` plus any binary notice bundle.
- [ ] Confirm every project-facing surface consistently describes project-authored software and documentation as open source under the MIT License; remove obsolete source-available restrictions.
- [ ] Confirm `LICENSE` contains the canonical MIT text and that `NOTICE.md`, contribution terms, Store copy, and provenance records reflect the same grant.
- [ ] Confirm the TrackMeUp name, logos, app icons, and brand artwork are identified separately under `TRADEMARKS.md`, without adding restrictions to MIT-licensed material.
- [ ] Confirm redistributed packages include the MIT copyright/license notice and every required third-party license or attribution notice.
- [ ] Verify privacy copy covers explicit archive export, AI provider requests, Windows screenshot/log sharing, and optional diagnostics without claiming that data can never leave the PC.
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
- [ ] Confirm the repository host detects the root license as MIT.
- [ ] Publish release notes with clear validation scope and known limitations.
