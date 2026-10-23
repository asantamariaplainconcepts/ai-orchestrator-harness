## REMOVED Requirements

### Requirement: The narrow set covers the Change tab and each of its five steps, in both themes
**Reason**: the Change tab has four steps; Tests is part of Evidence.
**Migration**: restated below for four steps; `change-tests-390.png` and its dark counterpart are
deleted from `docs/design/current/`.

## ADDED Requirements

### Requirement: The narrow set covers the Change tab and each of its four steps, in both themes

The narrow design set SHALL hold one committed capture of the `Change` tab at 390×844 in the light
theme and one in the dark theme, and one further pair for each step of the tab that draws a different
panel.

The tab's resting step SHALL be the tab's own file rather than a second file of its own name.

Every capture SHALL be produced by the same run that measures the page. When the gate runs the
suite, the captures SHALL go to the review evidence rather than over the committed record; retaking
the record is running the suite outside the gate.

#### Scenario: The files the set holds
- **WHEN** the narrow set is retaken for the `Change` tab and its four steps
- **THEN** eight files exist: `change-390.png`, `change-code-390.png`, `change-evidence-390.png`,
  `change-app-390.png`, and a `-dark` counterpart of each

#### Scenario: Inside the gate
- **WHEN** `aspire do ci` runs the suite
- **THEN** the captures land in `src/artifacts/playwright/design/` and `docs/design/current/` is untouched
