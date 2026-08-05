# Partner Center payload

This directory is reserved for the exact metadata payload returned by Partner Center for TrackMeUp.

Do not create this file by guessing the Microsoft schema. After the first Store association, retrieve the current submission with the Microsoft Store Developer CLI and save the response as `metadata.json`. Keep the payload versioned and review changes together with [`../listing.json`](../listing.json).

The payload may contain Store-specific identifiers and submission structure, but it must not contain Partner Center credentials, client secrets, access tokens, or unrelated private data.
