## ADDED Requirements

### Requirement: The PBI journey's spine is a covered narrow surface

The PBI journey's spine SHALL be a covered narrow surface: a 390×844 light/dark pair SHALL be produced
where the product is available, asserting that no required control ends past the window's right edge
and that the document does not scroll sideways at the phone width. The spine draws vertically at 390px,
its stages stack, and a run card under a stage SHALL be brought into the viewport by scrolling the
application's own scroller rather than the document.

A narrow capture of the journey SHALL assert that the current stage's first panel row is reachable
by scrolling, the way the Change tab's covered step is asserted. The capture SHALL NOT compare
against a stored image or assert machine-dependent panel text.

#### Scenario: The journey at 390px in both themes

- **WHEN** the PBI journey is retaken at the phone width
- **THEN** a 390×844 light pair and a 390×844 dark pair are produced, each from the same run that proves the page's width and theme

#### Scenario: A control off the edge at 390px

- **WHEN** a required control on the journey ends outside the window at 390px
- **THEN** the run fails with the element, text and measured position

#### Scenario: A stage panel below an unscrollable fold

- **WHEN** the journey is photographed at 390px and the current stage's first panel row cannot be brought into the viewport by scrolling
- **THEN** the run fails naming the stage, rather than writing a capture whose subject never reached the screen
