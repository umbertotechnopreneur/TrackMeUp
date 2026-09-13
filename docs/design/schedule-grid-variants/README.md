# Screenshot schedule grid variants

Four ImageGen explorations for the same WinUI 3 screenshot-schedule dialog. All variants retain seven days, 15-minute precision, the dark Mica surface, and fixed cancel/start actions.

| Variant | File | Selection model |
| --- | --- | --- |
| Compact heatmap | `01-compact-heatmap.png` | One compressed weekly matrix with time landmarks |
| Responsive day cards | `02-responsive-day-cards.png` | Seven independent day cards with 2×2 quarter-hour cells; selected for implementation |
| Continuous hour matrix | `03-continuous-hour-matrix.png` | A 24-row matrix with quarter-hour subdivisions and continuous active bands |
| Day-part matrix | `04-day-part-matrix.png` | A weekly grid grouped into night, morning, afternoon, and evening |

## Shared generation direction

Create a high-fidelity dark Mica WinUI 3 scheduling dialog inspired by the current TrackMeUp surface. Keep the weekly schedule as the primary grid interaction, preserve 15-minute precision, make all text and actions legible, tint weekends with restrained coral, and show active periods in cyan. The complete dialog must remain visible and usable when resized; avoid a 96-row wall, browser chrome, card wrappers around every control, neon styling, and cropped content.

## Variant prompts

- Compact heatmap: compress the week into one matrix with day columns, time landmarks, subtle quarter-hour ticks, and rounded active bars.
- Responsive day cards: give each day a compact vertical 24-hour grid made from 2×2 quarter-hour cells, a localized day heading, a live selected-range summary, weekend tinting, and a 7/4/2/1-column responsive reflow.
- Continuous hour matrix: use 24 hourly rows, four subtle quarter-hour segments per hour, and continuous active bands labeled with their range.
- Day-part matrix: group the true selection grid into night, morning, afternoon, and evening bands, with selected-range chips and an explicit legend.
