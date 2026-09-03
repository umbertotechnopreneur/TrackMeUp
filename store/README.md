# Microsoft Store listing

This folder contains the versioned source for the TrackMeUp Microsoft Store listing.

The editorial source of truth is [`listing.json`](listing.json). It keeps the product name, localized copy, public links, and the list of screenshots in Git so a listing change can be reviewed like any other product change.

## Current listing

- Product: TrackMeUp
- Category: Productivity
- Source code: <https://github.com/umbertotechnopreneur/TrackMeUp>
- Publisher website: <https://umbertogiacobbi.biz/trackmeup/?utm_source=microsoft_store&utm_medium=referral&utm_campaign=trackmeup&utm_content=publisher_website>
- Privacy policy: <https://github.com/umbertotechnopreneur/TrackMeUp/blob/main/docs/PRIVACY.md>
- Support: <https://github.com/umbertotechnopreneur/TrackMeUp/issues>

The listing copy is available in English (`en-US`), Italian (`it-IT`), French (`fr-FR`), German (`de-DE`), Spanish (`es-ES`), Simplified Chinese (`zh-Hans`), Vietnamese (`vi-VN`), Korean (`ko-KR`), European Portuguese (`pt-PT`), and Brazilian Portuguese (`pt-BR`). It intentionally explains that TrackMeUp is an internal tool we use ourselves, that it is a working product rather than an MVP, and that its MIT-licensed open-source code, local-first design, and optional AI and screen-capture features are part of the product promise.

## Screenshots

Put approved Store screenshots in [`screenshots/`](screenshots/). Add each committed file to the `screenshots.items` array in `listing.json` with its locale, caption, and purpose. Keep screenshots free of personal data, API keys, private URLs, and customer information.

The screenshots are not enabled in the listing yet. This is deliberate: the app should be captured in a stable, representative state before the first Store submission.

## Microsoft Store publishing

The repository has a validation-only workflow in [`.github/workflows/store-listing.yml`](../.github/workflows/store-listing.yml). It validates the editorial source on pull requests, pushes to `main`, and manual dispatches. It never authenticates to Partner Center and cannot publish a submission.

Microsoft requires the first Store submission to be created in Partner Center. After the app has a Store product ID, retrieve the current submission metadata with the Microsoft Store Developer CLI and save the exact response as `partner-center/metadata.json`. That payload is account-specific and should not be guessed or replaced with a hand-written approximation.

Until a separately reviewed publication workflow exists, update and submit Store metadata manually in Partner Center. Before adding automation:

1. Put the real non-secret Store product ID in `publishing.partnerCenterProductId` in `listing.json`.
2. Review and commit both `listing.json` and the exact Partner Center metadata payload.
3. Confirm the chosen Microsoft Store API operation mutates only the intended draft modules and cannot publish an unrelated package or draft state.
4. Configure a `microsoft-store` GitHub environment with required reviewers, prevent self-review, and restrict deployment to `main`; verify those rules through the GitHub API before allowing a publication job to run.
5. Use environment-scoped credentials without placing secrets in process arguments, and require an explicit manual confirmation that names the full submission effect.

Microsoft certification still happens after a submission is sent. The current repository workflow deliberately stops before that boundary.
