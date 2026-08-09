# Contributing to TrackMeUp

Thank you for helping improve TrackMeUp.

TrackMeUp is a local-first Windows product and still evolving. Small, focused changes with clear validation are easier to review and merge.

## Before Opening an Issue or Pull Request

- Read `README.md` and `docs/PRIVACY.md`.
- Read `AGENTS.md` and `.github/copilot-instructions.md` for repository guardrails.
- Search existing issues and pull requests before opening a new one.
- Keep credentials, API keys, tokens, personal data, and private local paths out of commits, logs, screenshots, and issue reports.
- Use `SECURITY.md` for vulnerabilities; do not publish exploitable details in public issues.
- For larger architectural changes, open an issue first.

## Development Setup

Use PowerShell 7.

```powershell
pwsh -NoProfile -Command "dotnet restore .\TrackMeUp.slnx"
pwsh -NoProfile -Command "dotnet build .\TrackMeUp.slnx -p:Platform=x64 -warnaserror"
pwsh -NoProfile -Command "dotnet test .\TrackMeUp.slnx -p:Platform=x64 -warnaserror"
```

You can also use the repository script entrypoint:

```powershell
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action Preflight
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action Build -Platform x64 -WarnAsError
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action Test -Platform x64 -WarnAsError
```

## Development Rules

- Keep dependency direction clean: presentation surfaces stay passive and application behavior lives in `TrackMeUp.Core` services.
- Fail fast on invalid input and unsupported state; do not add silent fallbacks unless behavior is explicitly documented.
- Do not add backward-compatibility layers for superseded contracts unless explicitly requested.
- Do not create a second tracking runtime; use the existing mutex and named-pipe ownership flow.
- Never pass secrets by command arguments or persist them in settings/history/logs/diagnostics.
- Keep changes scoped and avoid unrelated formatting churn.

## Pull Request Expectations

Describe:

- what changed;
- why it changed;
- which project/layer owns the behavior;
- how it was validated;
- known limitations or follow-up work.

Keep each PR focused. Do not include build output, generated artifacts, or unrelated edits.

## AI-Assisted Contributions

AI tools may assist with drafts, tests, refactors, and implementation proposals. Human contributors remain responsible for correctness, security, and licensing.

For material AI-assisted contributions, disclose usage and summarize your review/validation in the PR. See `AI_CONTRIBUTION_POLICY.md`.

## License and Provenance

By contributing, you confirm you have the right to submit material under the repository `LICENSE` (MIT) and that your contribution does not contain confidential or license-incompatible content.

Record third-party code, assets, and generated material in `THIRD_PARTY_NOTICES.md` or companion provenance records.