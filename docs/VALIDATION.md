# TrackMeUp Manual Validation Guide

Use these checks for behavior and visual acceptance after changing the corresponding product surface. Run destructive checks only with disposable data.

## Screenshot viewer

- Open 16:9, portrait, and ultrawide captures. Confirm that the selected image covers the active viewport at 100%, starts centered, and exposes its overflow with a left-button drag.
- Use the mouse wheel over different image points and confirm that zoom follows the pointer. Verify click-drag, touch, and trackpad navigation through 500%.
- Confirm that the frosted command rail and metadata fade in only while the pointer or keyboard focus is inside the image area.
- Confirm that grouped colored icons expose localized tooltips and that no overflow menu remains in the title bar.
- Open the details sidebar, resize it with drag and keyboard input, and confirm that it never exceeds 50% of the available width. Close and reopen the inspector, including after visiting an empty day, and confirm that the saved open/closed preference returns when captures are available.
- Repeat in light, dark, High Contrast, and with Windows transparency effects disabled. The rail, metadata, sidebar, and full-width filmstrip must remain readable through native Acrylic or its system fallback; the selected screenshot must have no visible frame, border, or internal padding while retaining a clear theme-aware elevation.

## Settings and operational tools

- From App options, open each of the five local-data links. Screen captures and AI, reports, privacy, data retention, and app details must each appear as a separate scrollable page.
- In Extra app details, confirm that all plugins load on entry without Refresh. Each row must have one switch reflecting the saved state; successful changes must persist and failed changes must restore the previous switch state.
- Confirm that Privacy is presented as a title and description followed by a textual link with a right chevron, and that Back returns directly to App options.
- Open Tools and diagnostics from the main menu, enter one focused page, and confirm that Back first returns to the tools overview and then to the player.
- Open the AI provider connection test and confirm that the taller dialog shows the complete fake terminal without clipping.

## World clocks

- With clean settings, open the detached **World clocks** window and confirm that current weather is on by default. Without `TRACKMEUP_OPENWEATHER_API_KEY`, the clocks and astronomy must remain visible while the weather state says setup is required; it must not claim that the user disabled weather and it must not send a provider request.
- Reach the icon-only three-dot options action by keyboard. Its localized tooltip and accessible name must be identical. Activating it must show one full options layer over the clock canvas in the same window; the localized icon-only Back action must return to the clocks and restore focus to the opener.
- In options, turn weather off and on, change always-on-top, add or remove a city, and change the reference city. Each mutation must use the shared application facade, survive a refresh where applicable, preserve the one-to-four-clock bounds, and keep every icon-only action's localized tooltip equal to its accessible name.
- Enter an invalid weather key and confirm that it is rejected without being retained. Enter a plausible key and confirm that it is written only to the fixed Windows user/process environment variable, the password field is cleared, the success state is localized, and the live projection refreshes without restarting TrackMeUp. The key must not appear in settings, logs, history, exception text, command lines, or IPC diagnostics.
- Repeat clocks and options in light, dark, High Contrast, and with Windows transparency effects disabled. Desktop Acrylic must remain visible through transparent theme-aware content or yield to the readable Windows fallback; no opaque full-window veil may hide it.
- Inspect the city image stack through dawn, day, sunset, and night plus clear, cloud, rain, fog, snow, mixed-precipitation, and lightning states. Astronomical, cloud, and lightning backdrops must remain behind the skyline; the scene fade and celestial body must remain above it; rain, fog, snow, and mixed-precipitation foreground layers must remain in front without flattening the compositing order.
- Change to a historical or future reference instant and confirm that current weather is not requested, weather layers disappear, and the linked OpenWeather attribution appears only when provider observations are actually shown.

## Native system messages

- Trigger informational, warning, and error notifications that require acknowledgement. Each must be one owned native Windows message box opened on its owner UI thread, with the standard Windows-localized **OK** action and the appropriate system icon.
- Trigger schedule replacement, retention deletion, installation merge, both atomic-reset gates, and application close. Each must use the native **OK/Cancel** layout with **Cancel** selected by default; pressing Escape, closing the message, or choosing Cancel must leave state unchanged.
- While one message is open, trigger another eligible prompt and confirm that the shared dialog coordinator serializes them. The owner HWND must remain modal and peer windows must regain interaction after every completion or interop failure.
- Confirm that rich surfaces—city picker, pricing, activity calendar, AI connection test, screenshot reprocessing, and storage migration—remain dedicated accessible windows rather than being reduced to a system message.

## Screen captures and AI

- Confirm that the page exposes only Latest screen capture and Open folder for retained captures.
- Confirm that it does not expose a manual Capture screen now action, capture-mode selector, retention controls, or embedded-image label controls.
- Confirm that describing the current context can request a fresh capture only through its explicit consent checkbox.

## Central banners

- Trigger status banners from the screenshot window, tools overview, and each focused tools page. Confirm that one fixed frosted banner overlays the content without moving it.
- Verify the rapid 80 ms fade in and fade out, the smoothly draining 3 px icon-coral line, the automatic close after 10 seconds, and manual close during fade-in.
- Repeatedly close a banner, replace banner A with banner B during fade-out, and unload the host. Each path must dismiss exactly once and a replacement must restart the full countdown.
- Repeat in light, dark, High Contrast, with Windows transparency effects disabled, and with Windows animation effects disabled.

## About window

- Open About in light, dark, and system theme modes. Confirm that the panoramic artwork matches the effective app theme, including after a live Windows theme change in system mode.
- Verify that version, build date, Git commit, product links, diagnostics actions, and the close action remain visible and keyboard accessible at 100%, 150%, and 200% display scaling.

## Search and keyboard shortcuts

### Opening and placement

- Keep the main window focused, press `Ctrl+Shift+P`, and confirm that the fixed-light local snapshot search opens as a narrow, title-free command palette.
- Confirm that focus starts in the vertically centered query field with all existing text selected.
- Move the pointer to each connected monitor and repeat the shortcut. The compact window must be centered in the pointer's monitor work area, use at most 64% of its width, and never exceed 960 logical pixels.
- Confirm that the window cannot be resized, minimized, or maximized, and that clicking outside closes it.

### Search loading and freshness

- Enter three characters: search starts after 250 ms; Enter submits immediately.
- While indexing, existing results remain available and new captures appear after the next update.
- The input-edge gradient flows while the window is active; the progress ring and status remain visible until the query completes.

### Results

- Pause for 700 ms and confirm that the window grows according to the number of results without exceeding 78% of the monitor work-area height.
- Confirm that no result-count label is shown, results are ordered by descending Lucene relevance, and each row shows a compact coral percentage chip normalized against the best hit in the current query.
- Confirm that the virtualized list starts directly beneath the query area.
- Each result must show a compact 260 x 146 snapshot thumbnail with soft resting elevation and a stronger shadow on pointer hover, without a translation-access exception.
- Confirm that the entire image remains visible at its original aspect ratio.
- Confirm that the highlighted matching passage, active window, timestamp, clicks, and available CPU/GPU telemetry are distributed clearly across the row. Unavailable historical telemetry must display an em dash.
- Clear the query or run a query with no matches and confirm that the window returns to its command-palette height.
- Select a result and confirm that the snapshot inspector opens on that exact capture.

### Commands and index rebuild

- Open the application menu and confirm that the primary actions show their `Ctrl+Shift+...` shortcuts and invoke the same commands as their menu items.
- Rebuild the single local search index from Search and OCR settings; the Mica window shows indeterminate progress.
- Cancel preserves the previous index; a successful rebuild restores search after a reported failure.
- Confirm that successful completion reports the indexed document count.
- Deletion or retention removes content from search, including after restart.

## Atomic nuke

> [!WARNING]
> Run the final deletion path only with disposable TrackMeUp data.

- In Tools and diagnostics, scroll to the final Atomic nuke section. Confirm that its warning copy and destructive action remain visible and keyboard accessible.
- Cancel the first warning and verify that nothing changes.
- Repeat by accepting the first warning and cancelling the final warning. Verify again that nothing changes.
- Accept both warnings and confirm that TrackMeUp closes, removes its database, retained screenshots, settings, reports, logs, search indexes, and metadata, disables its startup entry, and relaunches with default settings.
- When screenshots were stored in a custom directory, confirm that TrackMeUp-owned captures are removed while unrelated files in that directory remain intact.
