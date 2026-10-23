## Why

The narrow design set — `docs/design/current/*-390.png` — is the record #217 argued its three defects
from, and it reaches no worktree surface at all: `folders`, `tasks`, `run` and `project-*`, and
nothing else. This epic has just replaced seven worktree tabs and a five-tile verdict with one tab
holding five ordered steps, and nothing in the repository has looked at it below 1280.

Worse, those twelve PNGs had **no producer in the tree**. `capture.sh` takes the 1440 set with
headless Chrome, and Chrome cannot render this width on macOS: given `--window-size=390,844` it lays
the page out at **500×757** and scales the result into a 390px PNG, so every number read off such a
shot is a number about a 500px layout — measured, a page printing `innerWidth` reports 500 under both
`--headless` and `--headless=old`. `capture.sh current` also rejects a dark shot outright. So the
narrow set was taken by hand and could not be retaken, which is how a record that argues about
"today" ends up arguing about a screen that is no longer there.

Issue #297, epic #288. Last of the ten, because measuring an empty container says nothing: all five
step panels had to land first.

## What Changes

- **The Change tab and its five steps enter the narrow set**, in both themes, produced by a test in
  `AiOrchestrator.EndToEndTests` — ten files, one per step per theme, and the tab's resting step is
  the tab's own file rather than a sixth duplicate.

- **The instrument is calibrated in the same run.** The page is asked its own `innerWidth` and it
  must answer **390**. That assertion is what separates this set from anything Chrome can take: with
  the viewport removed from the fixture it answers 1280, and every capture beneath it becomes a
  picture of a width nobody has.

- **The theme is switched through the header's own control**, which below 520px lives inside the
  overflow menu rather than on the header — so a driver clicking the header's theme button finds
  nothing at this width. The class is read off the page before and after, because `useTheme` reads
  `matchMedia` and `localStorage` and Chrome's `--blink-settings=preferredColorScheme` moves neither.

- **Retaking the set becomes a step of `/aio:implement`** rather than something to remember, beside
  the annotation step #292 added — one command, named, with the filter that runs it alone.

- **The captures are asserted on where things end, never on what they say.** Nothing compares a
  screenshot, so nothing here fails because a label was reworded; the content of a capture is
  whichever change happens to be open on the machine, which is a fact about the machine and not
  about the layout.

**Not** in this change: any layout fix. This is net, not product. What the set found leaves as its
own issue with the capture that shows it — four findings, none of them fixed here, all of them
reported with their numbers.

## Capabilities

### New Capabilities

- `the-narrow-design-set`: which surfaces the 390px record covers, what produces it, how the width is
  proved rather than assumed, how the theme is reached at a width where the control has moved, and
  what happens to a surface the set finds does not fit.

### Modified Capabilities

None. No product behaviour changes: no route, no query key, no error code, no migration, no i18n key,
and nothing under `src/frontend/features/` or `src/modules/` is touched.

## Impact

- `src/tests/AiOrchestrator.EndToEndTests/NarrowSetTests.cs` (new) — the capture, the width
  assertion, the theme switch and the per-step overflow measurement. One test, ten files.
- `src/tests/AiOrchestrator.EndToEndTests/PhoneTests.cs` — `MeasureOverflow`, its three records and
  two helpers widened from private to `internal`, so the new file measures with the same script
  rather than a second copy of the rule. No behaviour change and no assertion touched.
- `docs/design/current/` — ten new PNGs, `change-390.png` and four `change-<step>-390.png` in each
  theme.
- `docs/design/README.md` — what the narrow set covers now, and the command that retakes it.
- `.ai/commands/aio/implement.md` — retaking the set as a step, appended beside #292's annotation
  step. One file, symlinked to three runtimes.
