# TrackMeUp repository instructions

These instructions apply to all changes in this repository.

- Read this file and `.github/copilot-instructions.md` before editing.
- Keep changes scoped, minimal, and reviewable.
- Preserve unrelated local changes unless explicitly requested.
- Keep WinUI views, code-behind, Spectre commands, prompts, and renderers passive: they may only collect input, bind/render DTOs, and invoke `ITrackMeUpApplication`.
- Put application behavior, persistence, OS interop, capture, environment access, HTTP, retention, and startup changes behind `TrackMeUp.Core` application services.
- Do not create a second tracking runtime; use the hashed-installation mutex and same-user named-pipe protocol through the shared facade.
- Do not pass secrets by CLI arguments or persist them in settings, history, IPC diagnostics, or tests.
- Use `pwsh -NoProfile` for PowerShell commands and script validation.
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
