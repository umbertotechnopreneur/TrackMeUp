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

Screenshots aren't enabled in the listing yet.

## Validation

The [Store listing workflow](../.github/workflows/store-listing.yml) checks these files on pull requests, pushes to `main`, and manual runs. It does not sign in to Partner Center or publish anything.

Keep public listing content and approved screenshots here. Internal submission procedures, commercial planning and Store account exports belong in the owner's private MeUp notes, outside Git.
