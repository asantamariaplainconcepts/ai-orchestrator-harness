## MODIFIED Requirements

### Requirement: Every screen fits a phone, and the first tab is reachable without dragging
The folders screen, a folder's overview, a folder's review tab and a run route SHALL each fit a phone
viewport, and the first tab of a tab strip SHALL be reachable without dragging the strip.

A worktree's Change tab at phone width SHALL put its steps' content within one scroll of the top of
the tab: the header, the step rail and the step panel lay out in one scrollable column, and no
fixed-height block between the top of the tab and the step panel SHALL stand taller than the
viewport so that the panel is unreachable.

#### Scenario: A run route fits a phone
- **WHEN** the run screen is rendered at phone width
- **THEN** it fits without horizontal overflow

#### Scenario: The first tab is not behind a drag
- **WHEN** a tab strip is wider than the viewport
- **THEN** its first tab is reachable without dragging

#### Scenario: A Change step panel is reachable by scroll
- **WHEN** the Change tab is rendered at 390 px and a step is selected
- **THEN** the first row of that step's panel can be brought into the viewport by scrolling, and
  the tab overflows vertically rather than clipping the panel below an unscrollable fold
