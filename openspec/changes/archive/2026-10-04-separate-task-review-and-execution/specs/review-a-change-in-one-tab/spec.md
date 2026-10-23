## ADDED Requirements

### Requirement: A task's review entry is not the whole detail

The task detail SHALL open the remote PR review screen as one entry beside
execution, and neither closing the task at the vendor nor merging its
pull request SHALL redirect, replace or absorb the task detail into the
review screen. Reviewing is an intent the person expresses by opening the
entry, not a state the task's lifecycle infers: a merged pull request
SHALL keep the entry — it opens the merged comparison — and a closed task
with an unmerged pull request SHALL keep it too.

#### Scenario: A merged pull request keeps its entry

- **WHEN** a task's pull request has merged
- **THEN** the task detail still offers Review PR, and it opens the merged comparison rather than redirecting the detail

#### Scenario: A closed task with an open pull request

- **WHEN** a task is closed at the vendor and its pull request is still open
- **THEN** the task detail offers both Review PR and Watch execution, each resolving independently
