# Adding missing AI screenshot descriptions

Design proposal, originally dated August 15, 2026.

Help people describe saved screenshots they skipped when AI was off, without
recapturing their screen. They should see the scope and estimated cost, confirm
the work, and be able to pause and resume it.

This document records the original proposal, including options that may differ
from the shipped feature. Reprocessing now has application contracts in
[Contracts.cs](../TrackMeUp.Core/Application/Contracts.cs); the original
"not implemented" status no longer applies. Use the current code and in-app
controls to establish what is available today.

## Proposed experience

1. From the activity calendar, choose **Add missing AI descriptions**. Start with
   the selected day, even if that day has screenshots but no activity summary.
2. Preview the saved images that need descriptions. Do this work only when the
   action is selected, so opening the calendar stays fast.
3. Show the selected AI provider and model, eligible images, missing files,
   privacy exclusions, remaining daily allowance, and estimated maximum cost.
4. Ask for confirmation before sending anything to the AI provider.
5. Show progress, outcomes, and **Pause** or **Resume** throughout the job.

The original proposal also includes inclusive date ranges, day/month shortcuts,
and manual/scheduled capture filters. These are design options, not a claim that
all filters are implemented.

### Make the count clear

A capture from several monitors contains several screenshots but uses one shared
AI description. Always distinguish images from captures and expected requests:

> **42 screenshots need AI descriptions**
>
> 28 captures, requiring up to 28 AI provider requests.
>
> 39 screenshots eligible · 2 files missing · 1 excluded by privacy rules.
>
> Daily allowance remaining: 12 of 20 requests.

Label the action with its scope, such as **Start for 39 screenshots**. If the
daily allowance cannot cover the whole job, show how many captures can be handled
today and explain that the rest will pause.

## Progress, pause, and resume

Show completed, total, and remaining counts for both captures and screenshots,
plus successful, skipped, and failed items. Progress must be measurable once the
preview is ready. A multi-monitor capture advances the capture count by one and
the image count by the number of screenshots it contains.

Show the current capture's time, source, and monitor count without exposing file
paths. Use clear states: **Running**, **Pausing**, **Paused**, **Daily limit
reached**, and **Completed**.

Pausing stops new requests. A request already sent finishes and its result is
saved before the job pauses. Resume starts with the next pending capture.
Shutdown may cancel a network request, but cannot guarantee that the provider
will not charge for work it already accepted.

When the daily limit is reached, leave pending items pending, preserve progress,
and show the next local-midnight reset. Manual resume is the proposed default;
automatic resume remains a separate product decision. Failed requests also
require an explicit retry in the proposed first version.

## Privacy and cost controls

- The preview can count images with AI off, but starting requires AI to be on,
  a valid provider/model with image support, and an available provider key.
- Only use readable TrackMeUp-owned images under the configured capture folder.
  Recheck files, current privacy rules, existing descriptions, and allowance
  before each request. Skip missing files with a clear reason.
- Apply privacy rules to the saved capture context. If the required context
  cannot be reconstructed, skip the capture rather than sending it.
- Use saved activity, sensor readings, and OCR text. Do not substitute the
  current desktop or current hardware readings for historical context. Older
  retained images may contain a baked-in label if their originals were deleted.
- Keep keys, prompts, OCR content, and private paths out of progress data, logs,
  and diagnostic messages. Show aggregate reasons in the UI.
- Respect the same daily cost controls as live analysis, without a force option.
  The proposal counts visual requests, including failed attempts and AI-assisted
  OCR refinement; connection tests are excluded. Confirm this rule against the
  current shared cost policy before changing quota behavior.

## Implementation essentials

Keep UI code limited to input and display. Preview, start, status, pause, and
resume go through `ITrackMeUpApplication`; Core owns files, storage, privacy,
provider calls, and job recovery.

| Concern | Required behavior |
| --- | --- |
| Confirmed scope | Freeze the capture/image list behind a short-lived plan ID. Reject expired plans or a changed provider, model, or endpoint. Never add new captures after confirmation. |
| Runtime ownership | Allow one unfinished job and one worker in the existing tracking runtime. Starting returns promptly; closing the progress view stops its status polling. |
| Live capture | Use one shared visual-analysis queue. Live/manual work takes priority between historical captures; allow at most one provider request at a time. Keep slow calls outside the global settings-change lock. |
| Historical analysis | Build a dedicated historical input from capture IDs, saved activity, telemetry, and OCR. Reuse common description logic with origin `snapshot.reprocess`; do not loop over the live-capture entry point. |
| Result storage | Link each successful description to its screenshot identities. Keep result and image links in one transaction; checkpoint every item and reconcile interrupted work on restart. |
| Recovery | Check for an existing description before sending. Reuse the original capture ID for correlation and give each provider attempt a distinct attempt ID. Do not repeat a request whose result was already saved. |
| Efficient preview | Enumerate files off the UI thread, deduplicate raw/stored images, and read metadata in batches. Avoid loading a full gallery or querying once per file. |
| Dialog flow | Close the calendar before opening progress within the same modal session. Do not acquire the dialog queue twice or block the main window indefinitely. |

The original storage design uses `ai_analysis_artifacts` for description/image
links and `ai_reprocess_jobs` / `ai_reprocess_job_items` for saved progress.
Index by capture time, image identity, and the next pending item; enforce one
unfinished job with a unique active slot. Any schema change must be explicit and
reject structurally invalid data. Exact schema and request types belong in code.

For progress, completed equals successful plus skipped plus failed; remaining
equals total minus completed. Reclassification may skip a confirmed item, but
must never increase the confirmed total.

## Accessibility

Support all ten app languages, keyboard use, screen readers, High Contrast, and
Windows transparency settings. Icon-only actions need matching localized
tooltips and accessible names. Announce meaningful state changes without reading
every progress update; communicate outcomes through text as well as color.

Give the progress bar an accessible completed/total/remaining label. Start focus
at the preview summary and safe cancel action. Escape and Close must follow the
same approved pause behavior. Use the existing Mica/Acrylic styling without
nested decorative panels.

## Optional provider spending view

An OpenAI-specific spending view is separate from reprocessing. It would require
its own opt-in and administrative credential, never reuse the analysis key, and
show spending for an explicit period and project scope. Keep only timestamped
monetary totals; do not retain administrative responses or credentials.

Call this **OpenAI spending**, not TrackMeUp spending unless the scope is
verified. Keep provider spending, TrackMeUp's daily request allowance, and a
user-defined budget distinct. Do not promise a remaining account balance or use
undocumented dashboard endpoints. Verify the current official API before
implementing this optional feature.

## Acceptance checklist

- Accurate counts for empty days, single images, multi-monitor captures, date
  boundaries, and manual/scheduled filters; no provider call before confirmation.
- Clear handling of missing files, missing historical context, privacy changes,
  disabled AI, invalid configuration, expired plans, and exhausted allowance.
- Responsive UI and live capture while a slow or failing provider is active.
- Pause/resume and restart recovery preserve progress and avoid duplicate saved
  descriptions; a new live capture stays outside the confirmed batch.
- Result storage, image links, and interrupted checkpoints recover consistently.
- Accessible progress, localized outcomes, and no secrets in UI, storage, or logs.

## Delivery and open decisions

The proposed sequence is preview and counting, then one-item processing with
saved progress, then full batches with live-capture priority and restart recovery.
Evaluate provider spending separately.

Before extending the current feature, settle these choices:

| Decision | Original recommendation |
| --- | --- |
| Initial date range | Selected day rather than the displayed month. |
| Closing a running job | Pause after the current request rather than continue unseen. |
| Return after closing | Choose whether to reopen the calendar or return to the main window. |
| Multi-monitor descriptions | One shared description per capture. |
| Failed requests | Explicit retry; no automatic retry in the first version. |
| Daily limit | Resume manually after reset. |
| Job history | Decide how long failed and completed job summaries should remain. |
| Provider spending | Separate optional work, not a dependency of reprocessing. |
