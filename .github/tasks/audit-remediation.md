# Audit remediation (2026-09-02)

Scope: the ten findings against `c99ac63`. Implementation and validation were completed before a separate user request authorized PR preparation on `codex/audit-remediation`; no installation is included.

- [x] 1–3: privacy-filter activity before persistence, honor detail preferences, authorize every visible window intersecting captured pixels.
- [x] 4: download pricing only for the enabled, selected provider.
- [x] 5–7: expire image-independent OCR, make deletion retryable, commit derived-index deletions before success.
- [x] 8: keep stop/disable responsive during remote AI calls and cancel incompatible live work.
- [x] 9: incremental suggestion updates without full-history rebuilds on lookup.
- [x] 10: bound IPC concurrency and initial-frame/response deadlines.
- [x] Regression tests, privacy documentation, README scenario checklist, x64 validation.

Policy: excluded activity produces no stored sample or input aggregates; disabling details retains app identity/counters only. Screenshot privacy rejects the whole capture when an intersecting visible window is excluded or required metadata is unavailable. OCR follows data retention even if its image is gone. Deletion success includes the current searchable projection, not forensic erasure of storage media.

## Implementation and verification

- Search schema 3 aggregates suggestion reference counts in the main Lucene commit. The previous derived indexes are explicitly discarded on upgrade; the obsolete suggestion dependency and its catalog entries are removed.
- Screenshot deletion uses a flushed pending-intent journal, retries absent-file cleanup, and recovers on runtime startup. Successful deletion and retention await index synchronization.
- Live analysis releases the command semaphore while retaining visual-analysis serialization. Pause/disable revoke pending live work; a policy-revoked response is discarded, while ordinary post-response shutdown preserves the existing durable checkpoint behavior.
- IPC permits four concurrent clients (at most 64 MiB of incomplete JSON input buffers) and five-second initial-frame/response-write deadlines.
- Final solution test run: **801 passed, 0 failed, 0 skipped** (Core 474; Search 34; OCR 26; Presentation 172; CLI 95).
- WinUI Release-Unpackaged x64 build: **0 warnings, 0 errors**, with warnings treated as errors.
- `git diff --check`: passed. Test results are generated under `artifacts/audit-remediation-tests/final/`.

Commands (run through PowerShell 7 with `-NoProfile`):

```text
dotnet test TrackMeUp.slnx -c Release-Unpackaged -p:Platform=x64 --no-restore -warnaserror
dotnet build TrackMeUp/TrackMeUp.csproj -c Release-Unpackaged -p:Platform=x64 --no-restore -warnaserror
```

Manual follow-up: validate the multi-monitor visual behavior on Windows using only synthetic content. Automated capture-policy tests use synthetic rectangles/metadata, not real desktop pixels. Enumeration checks cannot make Windows desktop composition atomic; the privacy policy documents that race and conservative blocking. No installer generation or application installation was performed for this remediation. Human review remains required before merging the PR.
