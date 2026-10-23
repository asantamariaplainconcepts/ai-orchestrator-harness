# controlled-task-input Specification

## Purpose
TBD - created by archiving change validate-controlled-task-input. Update Purpose after archive.
## Requirements
### Requirement: One opt-in grammar covers every Task body origin
This capability is superseded by `controlled-pbi-input` (#448). Every requirement of
`controlled-task-input` SHALL live on in `controlled-pbi-input` with the behaviour unchanged and the
vocabulary renamed from task to PBI, and the grammar's stored marker `### Task input` SHALL keep its
historical spelling in existing bodies. Nothing new SHALL be added to this capability: it exists only
to name its replacement.

#### Scenario: A later edit removes acceptance
- **WHEN** a previously valid body is edited to contain an empty acceptance array
- **THEN** reassessment reports an acceptance deficit rather than reusing the previous result

#### Scenario: Historical prose names issue numbers
- **WHEN** a legacy body mentions #262 and #263 without a structured block
- **THEN** it stays legacy and no dependencies are inferred

### Requirement: The structured input contains a checkable shape
The system SHALL require observation, goal, unique stable acceptance IDs with text and checks, a module and literal included paths, explicit excluded paths and scope exclusions, typed dependencies, open decisions, change ID and evidence references. Contradictory Seam declarations SHALL be invalid. Epics and open decisions SHALL not pass implementation input assessment.

#### Scenario: Seam ownership contradicts exclusion
- **WHEN** an included path overlaps an excluded path or a separate prose Seam is also declared
- **THEN** a deficit explains how to remove the contradiction

#### Scenario: Aggregate epic
- **WHEN** taskKind is epic
- **THEN** the assessment blocks implementation and instructs the caller to select a child implementation Task

### Requirement: Dependency identity is explicit and resolution is not guessed
Every dependency SHALL have a supported relationship and full provider, host, repository and key identity. Bare prose numbers SHALL NOT become dependencies. Self-dependencies SHALL report a cycle; other blocking dependencies SHALL remain unresolved until an external authoritative consumer resolves them. Unsupported providers or hosts SHALL report unsupported.

#### Scenario: Missing dependency has no local proof
- **WHEN** a dependency names a nonexistent or unobserved issue
- **THEN** the pure validator reports unresolved with a remedy to resolve it at the provider and performs no network lookup

#### Scenario: Same issue number in another repository
- **WHEN** a reference has the same key and a different repository
- **THEN** it is not classified as a self-reference

### Requirement: Assessment is authorized but does not grant execution authority
The system SHALL expose a read-only assessment DTO through `POST /api/tasks/input/validate` behind the repository's authorization pipeline. Deficits SHALL carry stable codes, field paths and remedies. Assessment SHALL identify its body digest and non-authoritative snapshot semantics; no state, label or execution effect SHALL occur. Legacy paths and held lifecycle work SHALL remain unchanged.

#### Scenario: Valid shape
- **WHEN** an authorized caller supplies a valid implementation input without blocking dependencies or decisions
- **THEN** the response is valid-input, not approval, readiness, or dispatch authorization

#### Scenario: Templates keep one source of truth
- **WHEN** a contributor uses the issue form and PR template
- **THEN** the issue holds the shape and the PR references Task identity, change ID, acceptance IDs and evidence without copying the specification

