# TrackMeUp repository instructions

These instructions apply to all changes in this repository.

## Shared delivery workflow

- For documentation-only or repository-instruction-only changes, commit and push directly on the current branch, including `main`, without creating a branch or opening a pull request. The owner authorizes using existing administrator bypass rights for this exception; do not change repository protection settings. Include `[skip ci]` in the commit message unless the owner explicitly requests CI.
- For all other changes, keep `main` protected. Make changes on a focused branch, open a pull request, and use squash merge only after required checks and conversations are resolved. Do not bypass branch protections, required checks, or review requirements for these changes. Delete the branch after a successful merge.
- Create portable release artifacts only through GitHub Actions. Create an annotated `v<version>` tag only after the matching source version is on `main`; never build, sign, upload, or publish release artifacts locally.
- Preserve unrelated working-tree changes. Never commit credentials, tokens, local data, logs, generated artifacts, or private machine paths.

## Context and token efficiency

- Do not read more than five files for a task without explicit user approval. If additional context is needed, ask first; this limit prevents whole-repository reading.
- Warn the user before an operation that could theoretically consume a large number of tokens, including broad repository reads, unbounded searches, or large output dumps.

- Read only task-relevant files and documentation sections; expand scope when dependencies or uncertainty require it.
- Reuse context already read. Re-read only when files changed, context is missing, or fresh evidence is needed.
- Start with scoped `rg` searches, then read relevant excerpts. Exclude generated files and bound command output; retrieve more only when needed.
- Batch independent read-only queries. Avoid repeated repository-wide scans or full file and log dumps.
- Make the smallest complete change; avoid unrelated refactoring, cleanup, documentation churn, or speculative abstractions.
- Use subagents only when explicitly requested.
- Keep progress updates focused on findings or blockers; report results, verification status, and remaining work concisely.

## Working practices

- Keep repository-wide rules in `AGENTS.md`; `.github/copilot-instructions.md` points here. No additional Copilot instruction read is needed when these rules are already in context.
- For C# work, enable the repository pre-commit formatter with `pwsh -NoProfile -File ./scripts/Install-GitHooks.ps1` once per clone. Before completing C# changes, run `pwsh -NoProfile -File ./scripts/Format-Code.ps1` and its `-Verify` mode; do not defer whitespace failures to CI or bypass the hook.
- Ask the user for explicit approval before running tests or triggering test suites. A code-change request does not authorize tests. Do not include unapproved tests in command chains. Keep required CI checks intact; ask before a push that would trigger them.
- Prefer scoped source and diff review. Run approved tests and other relevant checks once after the final change; repeat only after relevant edits, to diagnose failures, or to prevent a concrete risk, and obtain approval again before another test run. For documentation-only or instruction-only changes, inspect the scoped diff and staging; skip builds, tests, formatter runs, hook installation, and cleanup. Existing hooks and required CI checks remain unchanged.
- Keep product wording vendor-agnostic: user-facing shared AI features must say "AI provider" ("provider AI" in Italian). Name OpenAI, OpenRouter, Anthropic, or another vendor only when the UI refers to a selected provider or genuinely vendor-specific behavior such as its endpoint, model, or pricing.
- This repository is pre-production: do not add or retain backward-compatibility layers for superseded contracts, persisted artifacts, filenames, or APIs unless explicitly requested. Remove obsolete paths, adapters, fallbacks, and settings when replacing a feature; make migrations explicit and reject unsupported legacy input.
- Fail fast on invalid input, unsupported state, missing required configuration, and persistence or interop failures; do not silently normalize, ignore, or fall back unless the fallback is part of the documented product behavior.
- Never create a Git branch or worktree unless the user explicitly asks for it or approves it first.
- When creating a pull request, assign it to `umbertotechnopreneur` and apply the existing repository labels that match its scope.
- Keep WinUI views, code-behind, Spectre commands, prompts, and renderers passive: they may only collect input, bind/render DTOs, and invoke `ITrackMeUpApplication`.
- Presentation code must not construct infrastructure services or perform I/O, process, registry, environment, HTTP, capture, hook, or persistence operations.
- Every icon-only WinUI button or toggle must have a localized tooltip and the same localized accessible name; never rely on the glyph alone.
- Put application behavior, persistence, OS interop, capture, environment access, HTTP, retention, and startup changes behind `TrackMeUp.Core` application services.
- Do not create a second tracking runtime; use the hashed-installation mutex and same-user versioned named-pipe protocol through the shared facade. Serialize persistence mutations in the application layer.
- Use the environment-variable secret flow. Do not pass secrets by CLI arguments or persist them in settings, history, logs, IPC diagnostics, or tests.
- Support PowerShell 7 only: invoke every PowerShell command through `pwsh -NoProfile` (the supported equivalent of `--noprofile`); do not use Windows PowerShell 5.1 or bare `powershell`/`pwsh`.
- Avoid PowerShell quoting errors: prefer `pwsh -NoProfile -File <script.ps1>` for scripts and `pwsh -NoProfile -Command '<single-quoted command>'` for short commands; pass arguments as arrays or explicit parameters, do not build nested shell strings, and escape embedded quotes for the receiving command instead of relying on PowerShell interpolation.
- Start every first-party C# source file with `// SPDX-License-Identifier: MIT`; preserve original notices in generated or third-party files.
- Commit exclusions include `.env`, `bin/`, `obj/`, `artifacts/`, and `.vs/`.
- Ignore automatic version metadata changes in `TrackMeUp/build-version.json` and version-only updates in `TrackMeUp/Package.appxmanifest`: do not inspect, restore, report, stage, or commit them unless the user explicitly asks to manage the application version.
- Run build on supported Windows SDK targets only: x64 and ARM64.
- Prefer parser checks and dry runs before executing potentially destructive PowerShell scripts.
- If this task generated build or test output, clean it once after its last use and before handoff with the relevant `dotnet clean` (x64 by default). Commit or push alone does not require cleanup; preserve unrelated artifacts. When producing an installer, preserve the newly validated installer and remove older generated packages only after resolving and verifying their paths under this repository's `artifacts/` directory.

## Code and UI guidance

- Add XML documentation to public/protected methods and brief inline comments for critical I/O, process/OS interop, and external-call paths. Explain failure behavior and any fallback in service/monitoring logic and exception/guard clauses.
- Keep UI strings separate from business logic. When behavior changes, include at least one unit/integration scenario checklist entry in `README`.
- For Screenshot UI work, use reusable components and keep data/business logic in models/services. Avoid duplicate large titles and card wrappers around controls; emphasize a translucent Mica/Acrylic look.
- Track active work in `.github/tasks/todo.md` and completed work in `.github/tasks/archive.md`.

Project layout:

- `TrackMeUp/` for the Windows-first WinUI 3 app (`net10.0-windows10.0.19041.0`, x64/ARM64).
- `scripts/` for repository utility scripts.
- `.github/` for governance, Copilot context, workflows, and tasks.

Suggested checks:

```powershell
git status
dotnet restore .\TrackMeUp\TrackMeUp.csproj
dotnet build .\TrackMeUp\TrackMeUp.csproj -p:Platform=x64
```

License posture:

- Project-authored software and documentation are licensed under the MIT License in `LICENSE` unless a file states otherwise. Keep the canonical MIT text; do not add distribution or commercial-use restrictions.
- The TrackMeUp name, logos, app icons, and project-authored brand artwork are separate from the MIT grant; follow `TRADEMARKS.md` and asset-specific provenance records.
- Third-party code, data, and assets retain their own licenses and attribution requirements; preserve `THIRD_PARTY_NOTICES.md` and adjacent notices.
