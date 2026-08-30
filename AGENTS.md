# TrackMeUp repository instructions

These instructions apply to all changes in this repository.

- Read this file and `.github/copilot-instructions.md` before editing.
- Keep changes scoped, minimal, and reviewable.
- Be economical with verification: run checks primarily once the task is complete, not after every intermediate step. Repeat or add an earlier check only when it is needed to diagnose a failure, unblock the work, or prevent a risky mistake.
- Keep product wording vendor-agnostic: user-facing shared AI features must say "AI provider" ("provider AI" in Italian). Name OpenAI, OpenRouter, Anthropic, or another vendor only when the UI refers to a selected provider or genuinely vendor-specific behavior such as its endpoint, model, or pricing.
- This repository is pre-production: do not add backward-compatibility layers for superseded contracts, persisted artifacts, filenames, or APIs unless explicitly requested. Prefer the clean current design and make migrations explicit.
- Fail fast on invalid input, unsupported state, missing required configuration, and persistence or interop failures; do not silently normalize, ignore, or fall back unless the fallback is part of the documented product behavior.
- Do not preserve legacy code or superseded contracts for compatibility. Remove obsolete code paths, adapters, fallbacks, and persisted settings when a feature is replaced; unsupported legacy input must fail fast.
- Preserve unrelated local changes unless explicitly requested.
- Never create a Git branch or worktree unless the user explicitly asks for it or approves it first.
- Keep WinUI views, code-behind, Spectre commands, prompts, and renderers passive: they may only collect input, bind/render DTOs, and invoke `ITrackMeUpApplication`.
- Every icon-only WinUI button or toggle must have a localized tooltip and the same localized accessible name; never rely on the glyph alone.
- Put application behavior, persistence, OS interop, capture, environment access, HTTP, retention, and startup changes behind `TrackMeUp.Core` application services.
- Do not create a second tracking runtime; use the hashed-installation mutex and same-user named-pipe protocol through the shared facade.
- Do not pass secrets by CLI arguments or persist them in settings, history, IPC diagnostics, or tests.
- Support PowerShell 7 only: invoke every PowerShell command through `pwsh -NoProfile` (the supported equivalent of `--noprofile`); do not use Windows PowerShell 5.1 or bare `powershell`/`pwsh`.
- Avoid PowerShell quoting errors: prefer `pwsh -NoProfile -File <script.ps1>` for scripts and `pwsh -NoProfile -Command '<single-quoted command>'` for short commands; pass arguments as arrays or explicit parameters, do not build nested shell strings, and escape embedded quotes for the receiving command instead of relying on PowerShell interpolation.
- Do not commit credentials, secrets, `.env`, API keys, tokens, or private absolute paths.
- Start every first-party C# source file with `// SPDX-License-Identifier: MIT`; preserve original notices in generated or third-party files.
- Exclude generated artifacts in commits (`bin/`, `obj/`, `artifacts/`, `.vs/`).
- Ignore automatic version metadata changes in `TrackMeUp/build-version.json` and version-only updates in `TrackMeUp/Package.appxmanifest`: do not inspect, restore, report, stage, or commit them unless the user explicitly asks to manage the application version.
- Run build on Windows SDK targets only: x64, x86, ARM64.
- Prefer parser checks for PowerShell before running potentially destructive scripts.
- After every successful commit and push, run the relevant `dotnet clean` (x64 by default) and remove stale test build outputs. When an installer is produced, keep the newly validated installer and delete older generated installer/package artifacts only after resolving and verifying their paths under this repository's `artifacts/` directory.

Project layout:

- `TrackMeUp/` for the WinUI 3 app.
- `scripts/` for repository utility scripts.
- `.github/` for governance, Copilot context, workflows, and tasks.

Suggested checks:

```powershell
git status
dotnet restore .\TrackMeUp\TrackMeUp.csproj
dotnet build .\TrackMeUp\TrackMeUp.csproj -p:Platform=x64
```

License posture:

- Project-authored software and documentation are licensed under the MIT License in `LICENSE` unless a file states otherwise.
- The TrackMeUp name, logos, app icons, and project-authored brand artwork are separate from the MIT grant; follow `TRADEMARKS.md` and asset-specific provenance records.
- Third-party code, data, and assets retain their own licenses and attribution requirements; preserve `THIRD_PARTY_NOTICES.md` and adjacent notices.
