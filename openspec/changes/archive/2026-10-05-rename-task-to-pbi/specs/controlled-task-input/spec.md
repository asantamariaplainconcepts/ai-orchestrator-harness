# controlled-task-input Delta

## MODIFIED Requirements

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
