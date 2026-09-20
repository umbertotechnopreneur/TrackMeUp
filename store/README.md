# Microsoft Store listing

This folder holds the text, links, and screenshot list for TrackMeUp's Microsoft Store page.

Edit [`listing.json`](listing.json) when updating the listing. It keeps the product name, translations, public links, and screenshots together in Git, so I can review each change before it reaches the Store.

## Current listing

- Product: TrackMeUp
- Category: Productivity
- Source code: <https://github.com/umbertotechnopreneur/TrackMeUp>
- Publisher website: <https://umbertogiacobbi.biz/trackmeup/?utm_source=microsoft_store&utm_medium=referral&utm_campaign=trackmeup&utm_content=publisher_website>
- Privacy policy: <https://github.com/umbertotechnopreneur/TrackMeUp/blob/main/docs/PRIVACY.md>
- Support: <https://github.com/umbertotechnopreneur/TrackMeUp/issues>

The listing is available in English (`en-US`), Italian (`it-IT`), French (`fr-FR`), German (`de-DE`), Spanish (`es-ES`), Simplified Chinese (`zh-Hans`), Vietnamese (`vi-VN`), Korean (`ko-KR`), European Portuguese (`pt-PT`), and Brazilian Portuguese (`pt-BR`). Keep the copy focused on tracking time and finding past activity. Cover the MIT-licensed source code, history stored on your PC by default, and optional AI and screenshots. When describing personal use, write in the maintainer's voice: "I use TrackMeUp."

## Screenshots

Put approved Store screenshots in [`screenshots/`](screenshots/). Add each committed file to the `screenshots.items` array in `listing.json` with its locale, caption, and purpose. Keep screenshots free of personal data, API keys, private URLs, and customer information.

Screenshots aren't enabled in the listing yet. Before the first submission, I need images that show the app as people will use it.

## Microsoft Store publishing

The [Store listing workflow](../.github/workflows/store-listing.yml) checks these files on pull requests, pushes to `main`, and manual runs. It doesn't sign in to Partner Center and can't publish anything.

Create the first Store submission in Partner Center, as Microsoft requires. Once the app has a Store product ID, use the Microsoft Store Developer CLI to fetch the current submission metadata. Save the exact response as `partner-center/metadata.json`. These details belong to the Store account; don't guess them or write a substitute by hand.

For now, update and submit the listing manually in Partner Center. Any publishing workflow needs a separate review. Before adding automation:

1. Put the real non-secret Store product ID in `publishing.partnerCenterProductId` in `listing.json`.
2. Review and commit both `listing.json` and the exact metadata returned by Partner Center.
3. Check that the chosen Microsoft Store API operation changes only the intended parts of the draft and can't publish an unrelated package or other draft changes.
4. Set up a `microsoft-store` GitHub environment with required reviewers, no self-review, and deployment restricted to `main`. Verify these rules through the GitHub API before allowing a publishing job to run.
5. Store credentials in that GitHub environment and keep secrets out of process arguments. Require manual confirmation that clearly describes everything the submission will do.

Microsoft still needs to certify the app after submission. The current repository workflow only checks files; it doesn't send a submission.
