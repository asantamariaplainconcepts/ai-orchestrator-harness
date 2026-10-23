## MODIFIED Requirements

### Requirement: A worktree's task is resolved from its runs first and its branch second
A worktree's task SHALL be resolved from its runs first and its branch second — and the merge's
admission SHALL use that resolution: the task names the grant the merge requires, and the
grant's candidate names the head the merge expects. A merge without a live grant for the resolved
task SHALL be refused typed, and a candidate that moved after approval SHALL never merge on the
old expectation.

#### Scenario: The worktree's task has a grant binding its head
- **WHEN** the worktree's task carries a live grant whose candidate equals the pull request's
  observed head
- **THEN** the merge proceeds with that head as its expectation

#### Scenario: The grant is missing, expired or revoked
- **WHEN** the worktree's resolved task carries no live grant
- **THEN** the merge refuses typed — `grant-missing`, `grant-expired` or `grant-revoked` — naming
  the approve-candidate operation as the remedy, and nothing merges

#### Scenario: The candidate changed after approval
- **WHEN** the pull request's observed head differs from the live grant's candidate
- **THEN** the merge refuses typed — `candidate-changed` with both heads named — and the remedy is
  re-approval of the new candidate, never a merge of the old expectation

## ADDED Requirements

### Requirement: The merge's effect is idempotent and confirmed by observation
A squash-merge SHALL carry the expected head: the pull request's head read before the request,
refused typed on divergence, merged at the vendor, and confirmed after by reading the pull
request back. A lost merge response SHALL be settled by observation — a re-read answering
`MERGED` with the recorded head means it landed — and never by requesting the merge again. The
mechanical half of `/aio:sync` SHALL travel this same effect as the screen's button does: one
door, the person's own steps around it unchanged.

#### Scenario: Response lost, merge already landed
- **WHEN** the squash response was lost and the pull request re-reads `MERGED` with the expected
  head
- **THEN** the merge is confirmed by observation, and no second merge is requested

#### Scenario: The head moved before the request
- **WHEN** the pull request's head read differs from the expected head before the merge request
- **THEN** the merge refuses typed `candidate-changed`, nothing is merged, and the remedy names
  re-approval

#### Scenario: The vendor refuses
- **WHEN** GitHub refuses the squash in its own words — protection, review, conflict
- **THEN** the refusal passes through as the vendor said it, typed as ours only where the reason
  is ours

### Requirement: The close is a named, resumable effect
The merge's close — stop environment, remove worktree, drop branch — SHALL retain its resumable
semantics, and the whole merge effect SHALL be journaled: recorded before the merge request,
settled after the response or observation, with a derived effectId so re-entry resumes rather
than repeats. "Merge confirmed, cleanup pending" SHALL be a query the journal answers, not a
memory the person or the prompt holds.

#### Scenario: A retry resumes where it stopped
- **WHEN** the merge landed and the cleanup failed, and the person retries
- **THEN** the re-entry reads the pull request as merged, resumes the cleanup, and merges nothing
  a second time

#### Scenario: The journal names what finished
- **WHEN** the cleanup is partially done
- **THEN** the journal's record settles to the state that names what happened, and a pending
  cleanup is visible as pending
