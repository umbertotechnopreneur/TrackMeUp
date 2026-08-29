# Microsoft Store listing

This folder contains the versioned source for the TrackMeUp Microsoft Store listing.

The editorial source of truth is [`listing.json`](listing.json). It keeps the product name, localized copy, public links, and the list of screenshots in Git so a listing change can be reviewed like any other product change.

## Current listing

- Product: TrackMeUp
- Category: Productivity
- Source code: <https://github.com/umbertotechnopreneur/TrackMeUp>
- Publisher website: <https://www.umbertogiacobbi.biz/>
- Privacy policy: <https://github.com/umbertotechnopreneur/TrackMeUp/blob/main/docs/PRIVACY.md>
- Support: <https://github.com/umbertotechnopreneur/TrackMeUp/issues>

The listing copy is available in English (`en-US`), Italian (`it-IT`), French (`fr-FR`), German (`de-DE`), Spanish (`es-ES`), Simplified Chinese (`zh-Hans`), Vietnamese (`vi-VN`), Korean (`ko-KR`), European Portuguese (`pt-PT`), and Brazilian Portuguese (`pt-BR`). It intentionally explains that TrackMeUp is an internal tool we use ourselves, that it is a working product rather than an MVP, and that its MIT-licensed open-source code, local-first design, and optional AI and screen-capture features are part of the product promise.

## Screenshots

Put approved Store screenshots in [`screenshots/`](screenshots/). Add each committed file to the `screenshots.items` array in `listing.json` with its locale, caption, and purpose. Keep screenshots free of personal data, API keys, private URLs, and customer information.

The screenshots are not enabled in the listing yet. This is deliberate: the app should be captured in a stable, representative state before the first Store submission.

## Microsoft Store publishing

The repository has a validation workflow in [`.github/workflows/store-listing.yml`](../.github/workflows/store-listing.yml). It validates the editorial source on pull requests and pushes to `main`.

Microsoft requires the first Store submission to be created in Partner Center. After the app has a Store product ID, retrieve the current submission metadata with the Microsoft Store Developer CLI and save the exact response as `partner-center/metadata.json`. That payload is account-specific and should not be guessed or replaced with a hand-written approximation.

To enable automatic metadata publication after that bootstrap:

1. Add the Partner Center credentials to GitHub Actions secrets:
   `AZURE_AD_TENANT_ID`, `AZURE_AD_APPLICATION_CLIENT_ID`, `AZURE_AD_APPLICATION_SECRET`, and `SELLER_ID`.
2. Add the non-secret repository variable `STORE_PRODUCT_ID`.
3. Set the non-secret repository variable `STORE_AUTOPUBLISH` to `true`.
4. Review and commit both `listing.json` and the Partner Center metadata payload.

The publish job remains off until `STORE_AUTOPUBLISH` is explicitly enabled. A manual `workflow_dispatch` with the `publish` option is also available for the first controlled test. Microsoft certification still happens after the submission is sent.
