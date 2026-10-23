# controlled-pbi-input Specification

## Purpose
TBD - created by archiving change rename-task-to-pbi. Update Purpose after archive.
## Requirements
### Requirement: One opt-in grammar covers every PBI body origin
The system SHALL assess a version 1 JSON block under `### Task input` identically whether authored
through the GitHub form, API or manual edit. The block's marker keeps its historical spelling
`### Task input` — it is a format identifier already carried on existing archived issues, and this
change renames the product's vocabulary, not the stored bodies. Missing markers SHALL report legacy,
not ready. Duplicate fields, blocks, unknown fields, malformed JSON and incomplete bodies SHALL produce
actionable deficits.

#### Scenario: A later edit removes acceptance
- **WHEN** a previously valid body is edited to contain an empty acceptance array
- **THEN** reassessment reports an acceptance deficit rather than reusing the previous result

#### Scenario: Historical prose names issue numbers
- **WHEN** a legacy body mentions #262 and #263 without a structured block
- **THEN** it stays legacy and no dependencies are inferred

### Requirement: The structured input contains a checkable shape
The system SHALL require observation, goal, unique stable acceptance IDs with text and checks, a module
and literal included paths, explicit excluded paths and scope exclusions, typed dependencies, open
decisions, change ID and evidence references. Contradictory Seam declarations SHALL be invalid. Epics
and open decisions SHALL not pass implementation input assessment.

#### Scenario: Seam ownership contradicts exclusion
- **WHEN** an included path overlaps an excluded path or a separate prose Seam is also declared
- **THEN** a deficit explains how to remove the contradiction

#### Scenario: Aggregate epic
- **WHEN** the input names an epic scope
- **THEN** the assessment blocks implementation and instructs the caller to select a child
  implementation PBI

### Requirement: Dependency identity is explicit and resolution is not guessed
Every dependency SHALL have a supported relationship and full provider, host, repository and key
identity. Bare prose numbers SHALL NOT become dependencies. Self-dependencies SHALL report a cycle;
other blocking dependencies SHALL remain unresolved until an external authoritative consumer resolves
them. Unsupported providers or hosts SHALL report unsupported.

#### Scenario: Missing dependency has no local proof
- **WHEN** a dependency names a nonexistent or unobserved issue
- **THEN** the pure validator reports unresolved with a remedy to resolve it at the provider and
  performs no network lookup

#### Scenario: Same issue number in another repository
- **WHEN** a reference has the same key and a different repository
- **THEN** it is not classified as a self-reference

### Requirement: Assessment is authorized but does not grant execution authority
The system SHALL expose a read-only assessment DTO through `POST /api/pbis/input/validate` behind the
repository's authorization pipeline. Deficits SHALL carry stable codes, field paths and remedies.
Assessment SHALL identify its body digest and non-authoritative snapshot semantics; no state, label
or execution effect SHALL occur. Legacy paths and held lifecycle work SHALL remain unchanged.

#### Scenario: Valid shape
- **WHEN** an authorized caller supplies a valid implementation input without blocking dependencies or
  decisions
- **THEN** the response is valid-input, not approval, readiness, or dispatch authorization

#### Scenario: Templates keep one source of truth
- **WHEN** a contributor uses the issue form and PR template
- **THEN** the issue holds the shape and the PR references PBI identity, change ID, acceptance IDs and
  evidence without copying the specification

