## ADDED Requirements

### Requirement: The detail offers review and execution as two independent entries

The task detail SHALL offer **Review PR** and **Watch execution** as two
independent entries, resolved separately from the task's own identity —
source, host, repository and key together. Either entry SHALL be available
whenever its own destination resolves, regardless of the other's state: a
task with an open PR and an active run offers both at once, a task with
only a run offers only execution, and a task with only a PR offers only
review. What the entries open SHALL be the surfaces the product already
has — the remote PR review screen and the run's own tab — rather than new
destinations invented for this panel.

#### Scenario: Both entries on one task

- **WHEN** a task has an open pull request and a run that has not finished
- **THEN** the detail shows Review PR and Watch execution side by side, and each opens its own destination

#### Scenario: Execution without a pull request

- **WHEN** a task has a run and no pull request
- **THEN** Watch execution is offered and Review PR is not, and no pull-request destination is invented

#### Scenario: Review without a run

- **WHEN** a task has a pull request and neither this folder nor any other holds a run for it
- **THEN** Review PR is offered and Watch execution is not, and the absence is named rather than shown as an error

### Requirement: Destinations are resolved by full identity, never by the first match

Each entry SHALL resolve its destination from the task's full identity —
source, host, repository and key together — and SHALL NOT offer a
destination belonging to another repository's task that happens to carry
the same number, nor one inferred from a reused branch or the first run
with a matching key. Where several runs exist for one task, the entry
SHALL distinguish runs that have not finished from runs that have, SHALL
name each candidate — its agent, where it works, and how long since its
last activity — and SHALL let the person choose; the first match SHALL
NOT be selected in silence.

#### Scenario: Two repositories, one number

- **WHEN** two repositories in one scope each hold a task numbered 12, and one of them has runs
- **THEN** only that task's runs are offered as destinations, and the other repository's runs are not

#### Scenario: Several runs for one task

- **WHEN** a task has one run that has not finished and two that have
- **THEN** the unfinished run is listed first and each run is named, and opening one is the person's choice

### Requirement: Loading, absence, error and ambiguity are four named states

Each entry SHALL name its state, and the four SHALL NOT collapse into one
another: **loading** while the answer is on its way, **confirmed absence**
when the query answered and there is nothing, **error** when the query
failed — with the failure's own words — and **ambiguous** when the answer
cannot be attributed to this task's identity. An error SHALL NOT be shown
as absence, and absence SHALL NOT be shown as error. A failed runs lookup
SHALL NOT start a run: dispatch is a controlled decision governed by
`dispatch-observation`, and navigation is not a fallback path into it.

#### Scenario: A failed lookup is an error, not an absence

- **WHEN** the runs query refuses or fails while the detail is open
- **THEN** the entry shows the error and Watch execution does not become a confirmed absence, and no run is started

#### Scenario: A stale answer does not replace the destination

- **WHEN** an answer arrives for a task the person has already navigated away from
- **THEN** it is dropped rather than replacing the destination of the task now on screen

### Requirement: The lifecycle does not redirect the detail

Closing the task SHALL NOT remove nor redirect its entries, and merging
the pull request SHALL NOT collapse the detail into the PR view. The
entries the detail offers SHALL follow what the task's identity resolves
to — what its runs and its pull request are — and not what its lifecycle
labels say. A closed task with a run that has not finished SHALL keep
Watch execution available and the run SHALL remain reachable and active.

#### Scenario: A closed task with a live run

- **WHEN** a task is closed at the vendor while a run for it has not finished
- **THEN** Watch execution stays available, and the run it opens keeps its own state

#### Scenario: Merge does not absorb the detail

- **WHEN** the pull request of a task merges
- **THEN** the detail keeps both entries, each opening its own destination
