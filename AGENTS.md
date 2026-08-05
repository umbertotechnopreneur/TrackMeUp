# TrackMeUp repository instructions

These instructions apply to all changes in this repository.

- Read this file and `.github/copilot-instructions.md` before editing.
- Keep changes scoped, minimal, and reviewable.
- This repository is pre-production: do not add backward-compatibility layers for superseded contracts, persisted artifacts, filenames, or APIs unless explicitly requested. Prefer the clean current design and make migrations explicit.
- Fail fast on invalid input, unsupported state, missing required configuration, and persistence or interop failures; do not silently normalize, ignore, or fall back unless the fallback is part of the documented product behavior.
- Preserve unrelated local changes unless explicitly requested.
- Keep WinUI views, code-behind, Spectre commands, prompts, and renderers passive: they may only collect input, bind/render DTOs, and invoke `ITrackMeUpApplication`.
- Put application behavior, persistence, OS interop, capture, environment access, HTTP, retention, and startup changes behind `TrackMeUp.Core` application services.
- Do not create a second tracking runtime; use the hashed-installation mutex and same-user named-pipe protocol through the shared facade.
- Do not pass secrets by CLI arguments or persist them in settings, history, IPC diagnostics, or tests.
- Support PowerShell 7 only: invoke every PowerShell command through `pwsh -NoProfile` (the supported equivalent of `--noprofile`); do not use Windows PowerShell 5.1 or bare `powershell`/`pwsh`.
- Avoid PowerShell quoting errors: prefer `pwsh -NoProfile -File <script.ps1>` for scripts and `pwsh -NoProfile -Command '<single-quoted command>'` for short commands; pass arguments as arrays or explicit parameters, do not build nested shell strings, and escape embedded quotes for the receiving command instead of relying on PowerShell interpolation.
- Do not commit credentials, secrets, `.env`, API keys, tokens, or private absolute paths.
- Exclude generated artifacts in commits (`bin/`, `obj/`, `artifacts/`, `.vs/`).
- Run build on Windows SDK targets only: x64, x86, ARM64.
- Prefer parser checks for PowerShell before running potentially destructive scripts.

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

- This repo uses the MIT license; default project artifacts are intended for redistribution under the same terms.
