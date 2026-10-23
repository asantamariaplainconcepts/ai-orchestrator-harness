## MODIFIED Requirements

### Requirement: A narrow capture asserts where things end, never what they say

A narrow capture SHALL assert that no required control ends past the window's right edge and that the document does not scroll sideways. It SHALL NOT compare against a stored image or assert machine-dependent panel text.

Widths of names supplied by the machine SHALL not be asserted as layout facts. The run SHALL assert that photographed steps draw different screens. Copy-only changes SHALL not fail layout assertions; their new pictures SHALL be artifacts, not implicit documentation commits.

A narrow capture of the Change tab SHALL also assert that the step it photographs is reachable: the
first row of the selected step's panel SHALL be brought into the viewport by scrolling the
application's own scroller, and a panel that no scroll of the page reaches SHALL fail the run.

#### Scenario: A control off the edge
- **WHEN** a required button ends outside the window and outside an intentional horizontal scroller
- **THEN** the run fails with the element, text and measured position

#### Scenario: A label is reworded
- **WHEN** copy changes without changing layout
- **THEN** layout assertions still pass and a routine retake writes the new picture only to artifacts

#### Scenario: Two steps drawing the same screen
- **WHEN** two photographed steps read identically
- **THEN** the run fails because the capture sequence did not show distinct panels

#### Scenario: A step panel below an unscrollable fold
- **WHEN** the change tab is photographed at 390 px and the selected step's first panel row cannot
  be brought into the viewport by scrolling
- **THEN** the run fails naming the step, rather than writing a capture whose subject never reached
  the screen
