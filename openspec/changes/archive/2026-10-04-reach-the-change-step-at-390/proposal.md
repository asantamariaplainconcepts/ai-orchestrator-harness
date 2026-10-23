## Why

A 390×844 the Change tab draws the header, the change card and the step rail, and those three end
near y=844 with the step panel below them, outside the window, with no scroll that reaches it. The
person on a real phone cannot read a single step of the change they are meant to review — measured
2026-10-03 against `f5fa9a4d`, `scrollIntoView({block: 'start'})` on a panel row moved the view
about 48 px and the panel stayed off screen (#427).

The narrow set photographs these steps but does not assert their reachability, so nothing was red.

## What Changes

- The Change tab's column becomes a scroller below the rail when its container is phone-narrow:
  header, rail and one step panel lay out in one scrollable column, so every step's content is
  reachable by scrolling.
- The step panel keeps its own internal scroll at wide widths; nothing above 900 px changes.
- The narrow set gains an assertion — not a new photograph — that each step's first panel row is
  reachable by scroll at 390 px, in both themes.

## Capabilities

### New Capabilities

- None. The behaviour belongs to an existing capability.

### Modified Capabilities

- `app-shell-and-navigation`: its requirement that every screen fits a phone gains the Change tab's
  step panels as surfaces that must be reachable, not merely present.
- `the-narrow-design-set`: the requirement that a capture asserts where things end gains the
  converse assertion — that what a step draws below the fold is reachable by scroll.

## Impact

- `src/frontend/features/folders/ChangeTab.tsx` — the column layout of header, rail and panel.
- `src/tests/AiOrchestrator.EndToEndTests/NarrowSetTests.cs` — one reachability assertion per
  photographed step, both themes.
- No seam, no contract, no database.
