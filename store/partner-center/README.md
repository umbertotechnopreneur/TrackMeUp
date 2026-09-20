# Partner Center metadata

The optional local path `store/partner-center/metadata.json` is reserved by the listing configuration. Raw account and submission exports are not public repository content; this file is ignored by Git.

Keep public listing text in [listing.json](../listing.json). Keep submission procedures and internal account records in the owner's private MeUp notes. Never save credentials, client secrets or access tokens in documentation.

The current [Store listing workflow](../../.github/workflows/store-listing.yml) validates repository files only. It does not sign in to Partner Center or publish a submission.
