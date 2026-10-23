## Context

`ChangeTab` is a `display: flex` column — header card, step rail, step panel, footer — under a
parent (`FolderDetailScreen`) that stops being a scroller when the tab fills the height
(`overflow: hidden` for `change`, `files` and `overview`), so that `flex: 1` means "the rest of the
screen". That is right on a desktop window: the panel gets the room left over and scrolls itself
(`overflow: auto` on the panel's own root).

At 390×844 the room left over is negative. Three flex-`none` blocks sit above the panel — the
header card (~150 px once its title, facts and two buttons have wrapped), the rail (six or seven
`1 1 180px` boxes, so four rows at phone width) and the footer (~40 px) — and together they ask for
more than 844 px, so the panel's `flex: 1` resolves to a height near zero and everything below stays
outside the window. The document cannot scroll past it because every ancestor clips
(`overflow: hidden`), which is why `scrollIntoView({block: 'start'})` on a panel row moved the view
about 48 px and gave up: the only scroller on the chain was the panel the rows were already inside.
Measured 2026-10-03 against `f5fa9a4d` (#427).

The codebase already holds the convention for this problem: `useNarrowContainer(width)`, a
ResizeObserver on the screen's own element, because a pane has no business knowing whether the
projects rail is open (`TasksScreen`'s `filtersAbove`, `ProposalStep` and `CodeStep`'s
`columnsAbove`).

`NarrowSetTests` photographs this tab and asserts no control ends past the right edge, but it frames
every capture with `FrameTheTab` — which scrolls the rail's parent to the top — and never asks
whether the panel below the fold is reachable. That is why nothing was red.

## Goals / Non-Goals

**Goals:**

- At 390 px, each step's panel content is reachable by scrolling, in both themes.
- The narrow set asserts reachability, so a regression here is red rather than re-found by a person
  on a phone.
- Above the narrow threshold, the Change tab's layout is pixel-for-pixel unchanged.

**Non-Goals:**

- No redesign of the tab at desktop widths — which steps exist, their order, their panels' internal
  layout.
- No change to how the panel scrolls its own content at wide widths.
- No new capture files in the narrow set: the existing twelve stay the record; the assertion rides
  the run that already takes them.

## Decisions

**The column itself becomes the scroller when the tab is narrow.** Rather than shrinking the
non-scrolling blocks (smaller rail boxes, a collapsed header), the tab measures its own root with
`useNarrowContainer` — the established convention, same as `ProposalStep`'s `columnsAbove` — and
switches to one layout: the root stays a column but stops demanding `flex: 1` on the panel, the
ancestors' `overflow: hidden` keeps clipping only horizontally, and the whole column — header, rail,
panel, footer — scrolls together. Shrinkage was rejected because every block it would save already
carries information the reader needs at that width (the rail's hints, the header's facts); the
scroll is the one remedy that gives every block all its room.

- Threshold **560 px** of the tab's own element. The rail's boxes wrap at 180 px each, so the width
  at which the column genuinely runs out is where two boxes stop fitting beside each other
  (~400 px) — measured on the `AppFixture.Phone` viewport of 390 px — and 560 px is the point where
  that has already happened beyond doubt.
- The panel keeps `overflow: auto` and its fixed header row when wide; when narrow, its scroll
  moves to the column and the panel becomes `overflow: visible` — one scroller on the chain, not
  two fighting. Nested scrollers is what made `scrollIntoView` lose the race the issue measures.

**The assertion goes in `NarrowSetTests`, not a new file.** For each photographed step, after the
capture, the run scrolls the panel's first row into view through the column's scroller and asserts
the row's bounding rect intersects the viewport — Playwright's `ScrollIntoViewIfNeeded` on the
element, then a rect read. It asserts reachability, not position: the distance scrolled is layout's
business, and the assertion passes whichever block is tallest.

## Risks / Trade-offs

- [The footer's Previous/Next buttons ride inside the scroll, so at narrow width they sit at the
  bottom of the content, not of the screen] → Accepted: moving between steps is also tapping the
  rail, which stays one scroll away, and docking the footer to the viewport bottom would reintroduce
  the very fixed-height block this change removes.
- [`useNarrowContainer` re-renders on resize] → The hook is already the convention in six screens;
  the tab reading it adds no new observers beyond one.
- [The reachability assertion depends on the panel's first row existing] → Each step panel already
  draws a header row unconditionally (`shown.label` and `shown.hint`), so the row to bring into view
  is the panel's own header — present even while a step's data is still loading.

## Open Questions

None — measured and decided above.
