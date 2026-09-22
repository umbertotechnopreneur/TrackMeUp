# Lessons

These are current, durable engineering lessons for WorkTrail. Task-specific commands, temporary package details, and superseded pre-rebrand advice belong in neither this file nor the active task list.

## Startup and runtime

- Anything constructed before `MainWindow` can prevent both the window and the tray from existing. Changes to localization, persistence, search, or runtime composition need a cold-launch check in addition to unit or build validation.
- A Start-menu activation may be redirected to an existing background instance. Preserve activation requests during startup and explicitly show and foreground a hidden main window; `Activate()` alone is not a reliable visibility transition.
- Register and verify the notification-area icon before hiding the window. Recover after Explorer recreation, and restore the main window on registration failure so the application cannot become unreachable.
- Keep one runtime owner per installation through the shared mutex/named-pipe facade. A newly built executable may route into an older running instance, so source or package validation is not installed-runtime evidence.

## Contracts, access, and data integrity

- Keep application operations typed and versioned across local, CLI, and IPC callers. Update the catalog, request/response contracts, dispatch, client, and test doubles as one change; unsupported versions should fail explicitly.
- Enforce feature access and destructive-operation authorization inside Core, not only in UI controls. Debug simulation must be non-persisted and absent from release contracts.
- Serialize persistence mutations and commit related data, ledger, and search invalidation atomically. Report post-commit cleanup failures without claiming that durable work was rolled back.
- Treat legacy storage as an explicit migration: preserve provenance and timestamps, reject unknown layouts, and remove obsolete compatibility paths once the migration contract is replaced.
- Export to temporary destinations and replace atomically. Protect spreadsheet formulas, bound previews and AI input, and keep ordinary export local unless the user explicitly requests an AI summary.

## UI, localization, and search

- Strict localization is a startup dependency. Keep key and placeholder parity across every supported locale and retain a recoverable diagnostic path when catalog validation fails.
- Keep WinUI surfaces passive and application-facade driven. Static contracts and automated checks do not replace installed checks for visibility, activation, theme, high contrast, DPI, touch, and window restoration.
- Classify transient dialogs separately from restorable workspace windows; stale visibility state must not reopen dialogs or strand hidden owners at startup.
- For multilingual search, preserve literal paths, filenames, addresses, and URLs; prefer longest phrases and the best synonym variant within a deterministic expansion budget. Catalog-only synonym updates should not force an index rebuild.

## Providers, hardware, and delivery

- Keep provider secrets in the environment-variable flow, return only masked presence, and never place secret values in state, IPC diagnostics, history, or logs.
- Treat optional hardware helpers as explicit, consented dependencies. Pin and verify their signed payloads, keep unsupported architectures excluded, and report cancellation, elevation, or restart requirements clearly.
- WorkTrail ships on Windows 11 as MSIX for x64 and ARM64. Package signature and payload equality prove packaging integrity, not successful startup or user-visible behavior; verify those stages separately and report only what was observed.
- Keep private commercial, Store-account, roadmap, and design material outside the public repository. Public docs should contain current behavior, limitations, licenses, attribution, setup, and reproducible technical guidance.
