## MODIFIED Requirements

### Requirement: Gates verify the current clean candidate
A gate SHALL observe the candidate before and after its command, and SHALL refuse a candidate that
changed, is unclean, or cannot be read, exactly as before. The command's exit code SHALL remain
necessary and SHALL stop being sufficient: on completion the gate SHALL record, through the
control evidence sink, a typed **verification manifest** binding run, step, task identity,
candidate HEAD, tree and base, spec pin, resolved command, exit code, the test inventory the run
covered (assemblies, counts per outcome, full test names, digest of the name set), and a verdict
— `pass`, `fail`, or `inconclusive`. A run with no exit, no summary, or fewer assemblies than the
solution declares SHALL be `inconclusive` naming what is missing, never `pass`. The verdict and
its bindings are recorded at the gate, with the candidate present, not reconstructed later.

#### Scenario: A clean pass carries its coverage
- **WHEN** a gate exits zero on an unchanged clean candidate with a complete summary
- **THEN** the manifest records `pass` with the inventory it covered, bound to the candidate's HEAD

#### Scenario: A partial run is inconclusive, not green
- **WHEN** the summary names fewer assemblies than the solution's test projects
- **THEN** the manifest records `inconclusive:assemblies-missing` naming them, and nothing treats
  the gate as passed

#### Scenario: A zero-test run is inconclusive
- **WHEN** the summary reports zero tests across the assemblies it declares
- **THEN** the manifest records `inconclusive` saying so

### Requirement: The stored definition and trace explain the Run
The stored definition SHALL pin the plan and policy the Run executed with, and the recorded trace
SHALL explain each step, as before. Verification manifests are trace residents: one per gate step,
addressable by run and step, carrying their own bindings and verdicts, so a reading of the trace
answers not only what ran but what it covered — and what it did not.

#### Scenario: Asking what a gate covered
- **WHEN** a reader reads the Run's trace for the gate step
- **THEN** the manifest answers with assemblies, counts, test names and verdict bound to the
  candidate's HEAD

## ADDED Requirements

### Requirement: The test inventory is comparable across candidates by the trunk
A gate's manifest SHALL be comparable to a baseline: the test inventory published as a check run
on the merged commit (`test-inventory`), read back for a candidate's merge-base, and diffed —
names added, names lost, digest changed. The baseline SHALL live in the trunk by SHA, never in a
committed summary file; a missing baseline is stated in the manifest as absent, and never invented
or treated as an empty inventory. The diff of a passing gate SHALL be projected to the task's
issue as one typed attention comment; the manifest is the fact, the comment the projection.

#### Scenario: A candidate adds tests
- **WHEN** a passing gate's inventory is diffed against its merge-base baseline
- **THEN** the manifest and the issue comment name the added tests, the lost tests, and both
  digests

#### Scenario: No baseline yet
- **WHEN** the merge-base carries no published inventory
- **THEN** the verdict stays what the gate proved, the manifest says `baseline: absent`, and
  nothing inventories on the baseline's behalf

#### Scenario: Two candidates against one trunk
- **WHEN** two parallel candidates each diff against their merge-base
- **THEN** each reads the same trunk fact by its own merge-base, with nothing to coordinate
