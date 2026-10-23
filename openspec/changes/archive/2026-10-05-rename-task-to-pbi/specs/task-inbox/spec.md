# task-inbox Delta

## MODIFIED Requirements

### Requirement: The inbox is filtered on the server, because the counts have to agree with the list
This capability is superseded by `pbi-inbox` (#448). Every requirement of `task-inbox` SHALL live on
in `pbi-inbox` at `GET /api/pbis` with the behaviour unchanged and the vocabulary renamed from task
to PBI. Nothing new SHALL be added to this capability: it exists only to name its replacement.

#### Scenario: A source filter narrows the list and leaves every count alone
- **WHEN** the list is filtered to one source
- **THEN** the rows narrow and every count stays over the whole set

#### Scenario: The list keeps the vendor's order
- **WHEN** a vendor answers its issues most recently updated first
- **THEN** the inbox lists them in that order

#### Scenario: A pull request counts and filters apart from an issue
- **WHEN** the source chips are drawn
- **THEN** pull requests are their own source, counted and filtered separately

#### Scenario: A closed task leaves the inbox
- **WHEN** the vendor reports a task as closed
- **THEN** it is not in the inbox list
