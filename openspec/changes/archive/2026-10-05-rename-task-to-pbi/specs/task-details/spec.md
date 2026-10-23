# task-details Delta

## MODIFIED Requirements

### Requirement: The comment thread is read from the vendor, never held
This capability is superseded by `pbi-details` (#448). Every requirement of `task-details` SHALL
live on in `pbi-details` with the behaviour unchanged and the vocabulary renamed from task to PBI,
answering at `/api/pbis/{key}/…`. Nothing new SHALL be added to this capability: it exists only to
name its replacement.

#### Scenario: An issue with no comments answers an empty thread
- **WHEN** the task has no comments
- **THEN** an empty thread is returned rather than an error

#### Scenario: A key no vendor has says so
- **WHEN** the key is not in the snapshot and the vendor has no such task
- **THEN** the response says so rather than answering nothing
