# open-a-pull-request Specification

## Purpose
The pull request a worktree's branch has, and what it says: read for the Issue tab, which draws it above
the issue the worktree exists to resolve. The lookup answers three things and never two — found, none, or
the question could not be put — because a screen that drew "this branch has no pull request" over a
signed-out CLI would be stating a fact nobody established.

Written against the shipped code twice: reverse-engineered when it was a button in the run header that
handed an address to a browser, then rewritten by #235 when that button went and the answer became
something to read.
## Requirements
### Requirement: The pull request is looked up by the worktree's branch, against the folder
The system SHALL expose `GET /api/folders/{id:guid}/pull-request?worktree=<path>`
(`Features/Folders/UseCases/GetWorktreePullRequest.cs`, `Access.FiltersToCaller`), which SHALL look
up the pull request through `IPullRequestLookup` using the **worktree's** branch, and SHALL ask the
question of the folder rather than of the worktree directory: the two are the same repository to the
vendor, and the folder is the path every row carries unconditionally.

It is a **query about a worktree**, not a command on a run. The run-scoped
`POST /api/runs/{id:guid}/pull-request` SHALL NOT exist: a pull request belongs to a branch, several
runs of one issue share that branch, and asking through whichever run happened to be open was the
same mistake #185 corrected for the issue itself. The answer SHALL carry the pull request rather
than only its address — number, title, state, whether it is a draft, its address, its body and its
conversation — because the screen reading it now draws it instead of leaving for it.

#### Scenario: The branch's pull request is returned
- **WHEN** the worktree's branch has an open pull request
- **THEN** its number, title, state, address, body and conversation are returned

#### Scenario: Two runs of one branch get one answer
- **WHEN** a worktree holds several runs of the same issue
- **THEN** the pull request is resolved from the worktree's branch and does not depend on which run is open

### Requirement: The caller's checkout has no branch to look up
A caller asking about the checkout itself SHALL be refused with that reason: the checkout has no
branch of its own to look up, and its pull request is the caller's to make. The Issue tab that draws
this answer is worktree-scoped for the same reason, so the refusal is a guard on the endpoint rather
than a state any screen reaches by ordinary use.

#### Scenario: A checkout run is refused with the reason
- **WHEN** the query names no worktree, or names the checkout path
- **THEN** the response says there is no branch of its own to look up

### Requirement: A branch with no pull request is an ordinary answer, and a missing folder is a 404
A branch with no pull request SHALL be a **success** and not a refusal: "none yet" is the normal state
of a branch before anybody opens one, and a 4xx would make the tab draw an error where nothing is
wrong. It SHALL say there is none in words naming the fix, and the screen SHALL still draw the issue
beside it. A folder the caller does not have SHALL be a not-found.

#### Scenario: No pull request is an answer, not an error
- **WHEN** the branch has no open pull request
- **THEN** the response succeeds, says there is none, and names making one as the fix

#### Scenario: A folder that does not exist is a 404
- **WHEN** the folder id names nothing the caller has
- **THEN** the response is a not-found

### Requirement: "It could not be asked" is a different answer from "there is none"
The lookup SHALL distinguish a branch with no pull request from a question it could not put: no
`gh` on the `PATH`, a CLI that is not signed in, a folder whose vendor this cannot ask, or a network
that refused. Both SHALL be reported in their own words.

The seam collapsed all of them into one `null`, and its own doc gave the reason — the screen's
response to every one of them was the same disabled button, so distinguishing them would have been a
tooltip nobody could act on. That reasoning **dies with the button**: a tab that draws the pull
request would draw "this branch has no pull request" over a `gh` that was merely signed out, which is
a screen inventing a fact. `AGENTS.md` already says what the second answer owes its reader — a
refusal a person can act on carries the remedy, so "run `gh auth login`" is the answer, not "none".

#### Scenario: A signed-out CLI does not read as a branch with no pull request
- **WHEN** the vendor CLI is absent or not signed in
- **THEN** the answer says the question could not be put, and names the fix, rather than saying there is none

#### Scenario: The two answers are distinguishable by the screen
- **WHEN** the Issue tab receives either answer
- **THEN** it can draw a different sentence for each without inspecting a message string

### Requirement: The conversation comes with the pull request and is never held
The pull request's body and its conversation SHALL be read from the vendor at the moment the tab is
opened, and SHALL NOT be held between reads — the same rule `task-details` sets for an issue's thread, for the
same measured reason: the text is read one worktree at a time and would be stale between reads. An
empty conversation SHALL be an ordinary answer. The comments SHALL be the pull request's
conversation; comments anchored to lines of the diff live under the vendor's reviews and are out of
scope here.

The lookup SHALL remain **one process call with no state beside it**. Orca answers the same question
with a state machine over a cache because it keeps a live badge for every worktree on screen at once;
this product asks once, when somebody opens the tab, and #114 declined that cache on those grounds.

#### Scenario: A pull request with no comments answers an empty conversation
- **WHEN** the pull request has no comments
- **THEN** an empty conversation is returned rather than an error

#### Scenario: Nothing is cached between opens
- **WHEN** the tab is opened twice
- **THEN** the vendor is asked again, and no pull request state is held between the two

