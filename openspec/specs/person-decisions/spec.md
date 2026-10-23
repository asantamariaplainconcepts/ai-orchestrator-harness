# person-decisions Specification

## Purpose
The decisions a person takes from the PBI's screen — release a hold, move a PBI the loop does not
run, stop a run, and the verdicts of the Proposal and Review tabs — go through the same engine that
takes the loop's own, between its passes, and land in the same journal. A person and the engine never
race over a label. Reverse-engineered from the shipped code
(`src/modules/Loop/AiOrchestrator.Modules.Loop/Features/` and `Engine/Planner.cs`), not negotiated ahead
of it.

## Requirements
### Requirement: A decision is applied by the engine, between passes
Every person decision SHALL find the folder of the PBI's repository (404 `Pbi.NoFolder`), read its
loop (409 `Loop.Unreadable`), find the vendor's adapter (404 `Pbi.NoAdapter`) and read the PBI now
(404 `Pbi.NotFound`), then ask the planner and apply its answer through the engine, waiting for a pass
in progress to finish. A decision the planner refuses SHALL be 409 `Pbi.NothingToDo` with "Nothing was
changed: <why>". A write the vendor did not keep SHALL be 409 `Pbi.VendorRefused`, and a vendor that
did not answer 409 `Pbi.VendorUnread`.

#### Scenario: Releasing a PBI that is not held
- **WHEN** `POST /api/pbi/release` names a PBI without the hold
- **THEN** the answer is 409 `Pbi.NothingToDo` saying it is not in hold, and nothing is written

### Requirement: Release hold goes where the hold leads, or where the person chooses
`POST /api/pbi/release {repository, id, to?}` SHALL release a held PBI to the `next` of the engine's
latest hold of it from its current state; when that hold leads nowhere, to the `to` the person chose,
which SHALL be a state the loop declares or an undeclared target of it. It SHALL take the hold off and
move the label, journalled as "a person released the hold". While a run of the PBI has not ended, it
SHALL change nothing and answer 409 `Pbi.NothingToDo` with "a run of this PBI is still stopping;
release it once it ends", so a stopped run's end cannot race the release.

#### Scenario: A red gate is released to a chosen state
- **WHEN** a PBI held in `verifying` by a red gate is released with `to: in-progress`
- **THEN** the hold comes off, the PBI moves to `in-progress`, and the journal says a person released it

#### Scenario: A hold with a next needs no choice
- **WHEN** a PBI held in `proposal-review` with `next: in-progress` is released without `to`
- **THEN** it moves to `in-progress`

#### Scenario: A release waits for the run to end
- **WHEN** a held PBI is released while its run has not ended
- **THEN** the answer is 409 saying a run of this PBI is still stopping, and nothing is written

### Requirement: Move to only from where the loop does nothing
`POST /api/pbi/move {repository, id, to}` SHALL move a PBI that has no state, or is in a state the loop
does not declare, to a known state other than its own, journalled as "a person moved it from <state>".
From a declared state it SHALL be refused, because the loop moves the PBI from there.

#### Scenario: A refined PBI is let into the loop again
- **WHEN** a PBI in `needs-refinement` is moved to `ready-for-proposal`
- **THEN** its label changes and the next pass runs `/aio:refine` on it

### Requirement: Stop run ends a live run as failed
`POST /api/runs/{runId}/stop` SHALL kill a live run's session and answer 204, or 409 `Run.NotLive`
when the run is not live or its session has not started. A loop run stopped this way SHALL end with the
failure `Stopped` and the reason "Stopped by a person.", and the engine SHALL hold the PBI on its next
pass, unless the journal released the PBI after the run was asked for: then that pass SHALL write a
`ran` line with the failure and leave the PBI released. A manual run stopped this way SHALL end with
no failure.

#### Scenario: A stopped run holds its PBI
- **WHEN** a person stops the coder's live run in `in-progress`
- **THEN** the run ends failed with "Stopped by a person." and the PBI is held with that reason

#### Scenario: A stopped run released by a person is not held again
- **WHEN** a person stops a run, releases its PBI, and the next pass reads the run's end
- **THEN** the journal says `ran` with "the run failed (Stopped): Stopped by a person. The hold was
  released after the stop, so it stays off", and the PBI stays released

### Requirement: The Proposal's request for changes sends the PBI to its state's back
`POST /api/pbi/proposal/request-changes {repository, id, body}` SHALL require a body ("Say what has to
change."), publish it on the pull request as a review of kind comment — GitHub refuses a request for
changes from the pull request's author, which is the account the loop acts as — and then, only from a
state whose loop entry declares `tab: proposal` and a `back`, send the PBI to that `back`: released when
held, moved when not. Elsewhere the review stands, nothing moves, and `unmoved` says why. No state name
is written in the code; in this repository the state is `proposal-review` and its `back` is `spec-ready`.

#### Scenario: Changes asked on a held proposal
- **WHEN** a person requests changes on a PBI held in `proposal-review`
- **THEN** the review is on the pull request, the hold comes off, and the PBI is in `spec-ready`

#### Scenario: Request changes on the proposal in a loop with other names
- **WHEN** a loop's `critique` declares `back: sketch` and `tab: proposal`, and a person requests changes on a PBI in `critique`
- **THEN** the PBI goes to `sketch`, released when held and moved when not

### Requirement: Review's verdicts are written at the vendor first
`POST /api/pbi/review/approve {repository, id, body?}` SHALL submit an approving review of the pull
request and then, when the PBI is held and its hold leads to a state, release it there.
`POST /api/pbi/review/request-changes {repository, id, body}` SHALL require a body, submit a review
requesting changes, copy it and the open threads to the issue as a comment, and release a held PBI to
its state's `back` (`in-progress` from `code-review` and `merge-review` in this repository). A state
without `back` SHALL move nothing, and `unmoved` SHALL say it declares none. A verdict the vendor kept as a comment because the person reviewing wrote the pull
request SHALL move the loop the same way; the journal line SHALL end with ", written as a comment: " and
the vendor's words, and the answer SHALL carry them in `asComment`. A review the vendor refuses SHALL be
409 `Review.VendorRefused` in the vendor's words, and the loop SHALL NOT move. A loop that does not move after a review it kept SHALL NOT be an error: the
answer `{review, line, unmoved, note, asComment}` SHALL say why in `unmoved`.

#### Scenario: The author approving their own pull request
- **WHEN** the person viewing is the account that opened the pull request and presses Approve on a PBI held in `merge-review`
- **THEN** the approval is a comment on the pull request, the PBI goes on to `retro`, and the journal line and the Review tab say it went as a comment because GitHub refused "Can not approve your own pull request"

#### Scenario: The author requesting changes on their own pull request
- **WHEN** the same person presses Request changes on a held PBI
- **THEN** the review is a comment on the pull request, copied to the issue, and the PBI is back in `in-progress`

#### Scenario: GitHub refuses a review for another reason
- **WHEN** GitHub refuses an approval and the reason is not the author's own pull request
- **THEN** the answer is 409 `Review.VendorRefused` in GitHub's words, and the PBI stays held

#### Scenario: Changes requested on a PBI the loop is working
- **WHEN** a person requests changes on a PBI in `code-review` that is not held
- **THEN** the review is on the pull request and copied to the issue, and `unmoved` says the loop is at work

#### Scenario: Request changes on the pull request in a loop with other names
- **WHEN** a loop names its states `sketch`, `critique`, `build`, `check` and `judge`, `judge` declares `back: build` and `check` declares none, and a person requests changes on a PBI held in `judge`
- **THEN** the PBI goes to `build`, and one held in `check` stays where it is
