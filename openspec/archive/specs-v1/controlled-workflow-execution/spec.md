# controlled-workflow-execution Specification

## Purpose
Opt-in controlled execution for declared Workflow plans. The existing Run plan advances a step the
moment its Agent goes idle — an execution boundary, not proof that a candidate exists or that a gate
accepted it. A controlled plan requires a correlated Agent result before advancing, verifies candidate
commits independently before a gate runs against them, and persists the resolved plan and its decisions
before dispatching the next one, so a Run's execution graph is inspectable rather than inferred from
prose logs. Holds stay terminal for automatic execution; nothing here retries, merges, relabels an
issue or introduces a cross-machine scheduler.

## Requirements
### Requirement: Controlled plan shape is explicit
Version 1 SHALL accept one Agent step followed by resolved one-shot gate commands and a final Hold. It SHALL refuse other shapes before writing a Run. Each step SHALL have a recorded 1800-second deadline. Before checking the candidate or starting a gate, the harness SHALL stop and join the Agent process.

#### Scenario: An unresolved gate is refused
- **WHEN** a controlled plan contains an unresolved or long-running command
- **THEN** no Run or Agent starts

### Requirement: Controlled Agent steps require a correlated result
A controlled Agent step SHALL advance only after its result identifies the current Run, step and challenge. Accepted outcomes SHALL be candidate, needs_input, blocked and failed. Missing, malformed, oversized, unknown or stale results SHALL stop automatic dispatch with a recorded reason. Idle alone SHALL NOT prove success.

#### Scenario: Idle without a result
- **WHEN** an Agent becomes idle without a valid result
- **THEN** no next step executes and the Run records why it is held

#### Scenario: A stale result is rejected
- **WHEN** the result belongs to another Run or step
- **THEN** it cannot advance the current plan

### Requirement: Gates verify the current clean candidate
A gate SHALL observe the candidate before and after its command, and SHALL refuse a candidate
that changed, is unclean, or cannot be read. The command's exit code SHALL be necessary and
SHALL stop being sufficient: on completion the gate SHALL record a typed verification manifest
binding run, step, candidate HEAD, tree, base, spec pin, resolved command, exit code, the test
inventory the run covered (assemblies, counts, test names, digest of the name set), and a
verdict — `pass`, `fail`, or `inconclusive`. A run with no exit, no summary, or fewer assemblies
than the solution declares SHALL be `inconclusive` naming what is missing, never `pass`.

#### Scenario: A clean pass carries its coverage
- **WHEN** a gate exits zero on an unchanged clean candidate with a complete summary
- **THEN** the manifest records `pass` with the inventory it covered, bound to the candidate's HEAD

#### Scenario: A partial run is inconclusive, not green
- **WHEN** the summary names fewer assemblies than the solution's test projects
- **THEN** the manifest records `inconclusive` naming them, and nothing treats the gate as
  passed

#### Scenario: A zero-test run is inconclusive
- **WHEN** the summary reports zero tests across the assemblies it declares
- **THEN** the manifest records `inconclusive` saying so

### Requirement: The test inventory is comparable across candidates by the trunk
A gate's manifest SHALL be comparable to a baseline: the test inventory published as a check run
on the merged commit (`test-inventory`), read back for a candidate's merge-base, and diffed —
names added, names lost, digest changed. The baseline SHALL live in the trunk by SHA, never in a
committed summary file; a missing baseline is stated in the manifest as absent, and never
invented or treated as an empty inventory. The diff of a passing gate SHALL be projected to the
task's issue as one typed attention comment; the manifest is the fact, the comment the
projection.

#### Scenario: A candidate adds tests
- **WHEN** a passing gate's inventory is diffed against its merge-base baseline
- **THEN** the manifest and the issue comment name the added tests, the lost tests, and both
  digests

#### Scenario: No baseline yet
- **WHEN** the merge-base carries no published inventory
- **THEN** the verdict stays what the gate proved, the manifest says the baseline is absent, and
  nothing inventories on the baseline's behalf

#### Scenario: Two candidates against one trunk
- **WHEN** two parallel candidates each diff against their merge-base
- **THEN** each reads the same trunk fact by its own merge-base, with nothing to coordinate

### Requirement: Control evidence precedes dispatch
The system SHALL persist the resolved plan and ordered immutable observations and decisions for each controlled Run. A failure to persist required evidence SHALL stop dispatch. A duplicate completion SHALL NOT repeat an effect. Legacy transcript retention SHALL NOT delete control evidence.

#### Scenario: Evidence cannot be saved
- **WHEN** persistence fails before a transition
- **THEN** no next command is sent and the failure is reported

### Requirement: A Hold or interruption cannot silently downgrade control
A controlled Hold SHALL stop automatic execution. An interrupted controlled Run SHALL retain its prior evidence and SHALL NOT automatically replay its steps. Generic conversational resume SHALL refuse a controlled Run unless it can preserve the execution contract.

A controlled Hold SHALL end the Run as `held`, with a finished timestamp and no connected Agent. Legacy `waiting` SHALL retain its live meaning.

#### Scenario: A held Agent reports idle again
- **WHEN** another idle signal arrives after the controlled plan held
- **THEN** no step is dispatched

### Requirement: The stored definition and trace explain the Run
The system SHALL expose a controlled Run's stored plan, definition hash and ordered execution evidence through a read endpoint and the Run screen. The view SHALL distinguish execution, validation failure and Hold, and SHALL identify outcomes, reasons, observed commits and exit codes without parsing prose logs.

The Run's dispatch SHALL also carry the decision that created it: the loop facts and the machine
snapshot that decided them, stored with the dispatch at admission — never re-read from the live
declaration afterwards. The read endpoint SHALL expose that decision as evaluated facts: the
recognised state, each guard with the value it held, and the effect that fired. Editing the
declaration SHALL NOT rewrite a finished Run's snapshot, exactly as it does not rewrite its
definition.

#### Scenario: A finished Run is inspected
- **WHEN** the Workflow has changed since a Run ended
- **THEN** inspection still shows that Run's original definition and path

#### Scenario: The Run's authorising decision is inspectable
- **WHEN** a Run the loop started is inspected
- **THEN** the answer names the facts it was decided on, the state they recognised, the guards
  with their held values, and the effect that fired — read from the dispatch's snapshot, not
  from the live declaration

#### Scenario: The declaration changes after the Run ended
- **WHEN** `.harness/lifecycle.json` is edited after a Run ended
- **THEN** the Run's reported decision is unchanged
