# Partner Center metadata

Save the exact metadata returned by Partner Center for TrackMeUp in this folder.

After linking the app to the Store for the first time, fetch the current submission with the Microsoft Store Developer CLI and save the response as `metadata.json`. Don't guess the file format or create it by hand. Keep it in Git and review changes alongside [`listing.json`](../listing.json).

The file can include Store IDs and submission details. It must not include Partner Center credentials, client secrets, access tokens, or unrelated private data.
