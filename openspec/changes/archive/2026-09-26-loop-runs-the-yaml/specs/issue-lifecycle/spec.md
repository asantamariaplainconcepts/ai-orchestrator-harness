## MODIFIED Requirements

### Requirement: The WIP limit and the label names are configuration the loop reads
The loop's tunables SHALL be declared by the loop itself: the WIP limit in
`.harness/issue-loop.yaml`'s workflow-level `x-harness` (`x-harness: { wip: 4 }`), the label names
remaining in `.harness/config.json`. The implement gate SHALL enforce the limit by naming the
issues currently holding it. The limit SHALL be a person's decision recorded in the declaration
rather than a constant in a prompt.

Both the automatic path and the written gate SHALL read the same declaration, so they cannot
enforce two different numbers, and a limit the harness cannot read SHALL stop a launch rather than
permit one.

#### Scenario: The WIP gate names what is in the way
- **WHEN** the limit is reached and another implement starts
- **THEN** it refuses and names the issues occupying the limit

#### Scenario: One number, two enforcements
- **WHEN** the automatic path and a person following `implement` both check the limit
- **THEN** both read the loop's `x-harness.wip` and reach the same answer

#### Scenario: An unreadable limit stops a launch
- **WHEN** the loop declares no `wip` under `x-harness`, or names one out of bounds
- **THEN** the automatic path starts nothing for that Folder rather than starting without a limit
