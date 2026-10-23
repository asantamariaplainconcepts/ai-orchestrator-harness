# task-inbox Specification

## Purpose
Everything waiting to become a run, from every source, in one list. The rows are what the vendors last
answered, read through the in-memory task snapshot (`read-the-vendors`): the vendor owns the issue and
nothing here stores a copy of it. Reverse-engineered from the shipped code, not negotiated ahead of it.
## Requirements
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

### Requirement: A kind is the vendor's own spelling, and the filter is over kinds the tasks carry
A kind filter SHALL match the vendor's own spelling, SHALL treat a kind containing a comma as one kind,
SHALL list the tasks carrying either of two kinds when two are given, SHALL list nothing for a kind no task
carries, and SHALL treat "all" as the unfiltered list. A promoted `status:` label SHALL NOT be a kind, while
an unrecognised label SHALL be one.

#### Scenario: Two kinds list either
- **WHEN** two kinds are selected
- **THEN** tasks carrying either are listed

#### Scenario: A loop label is not a kind
- **WHEN** a task carries a `status:` label the product promotes
- **THEN** that label is not offered as a kind, while an unrecognised label is

### Requirement: One task is read by key, whatever its state, and the repository disambiguates it
The system SHALL expose `GET /api/tasks/{key}` (`GetTask.cs`, `Access.FiltersToCaller`) as the one read
that ignores the open filter, because a run still needs its issue after the person closes it — closing is
the natural end of the job and exactly when the person re-reads it. A key alone SHALL NOT be treated as
identifying a task: with two repositories in one scope, `#217` is two tasks, so the caller SHALL be able to
name the repository, a mismatch SHALL be a 404 rather than the other task, the match SHALL ignore case, and a
caller that cannot name one SHALL still get a **deterministic** answer — open before closed, then by vendor,
then by URL. A task the snapshot does not carry SHALL be fetched from the vendor by key before the read
answers 404. The response SHALL carry the task's vendor state and `readAt`: when the task was last read
from the vendor — the snapshot's read time, or now for a task fetched by key.

#### Scenario: A key in the named repository is the one returned
- **WHEN** the read names a repository
- **THEN** the task from that repository is returned, and a key absent from it is a 404

#### Scenario: Without a repository the answer is at least deterministic
- **WHEN** two repositories hold the same key and the read names neither
- **THEN** the answer is the same one every time rather than whichever vendor answered first

### Requirement: A task row carries only what the vendor said
Every field of a task row — key, source, title, body, labels, meta, URL, state, whether the body was cut —
SHALL come from the vendor's last answer, with the loop status and the hold derived from its labels. This
product SHALL add no field of its own to a task: there SHALL be no folder attribution and no suggested
workflow on the row, and a screen that needs a task's checkout SHALL resolve it from the task's URL.

#### Scenario: A refresh replaces everything on the row
- **WHEN** the vendor changes a task's title or labels and the snapshot is read again
- **THEN** the row shows the vendor's new values and nothing of the earlier read survives

