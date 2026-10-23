## ADDED Requirements

### Requirement: The lifecycle is the eight states the loop writes
The documented lifecycle — `CONTRIBUTING.md` and the `set-issue-status` skill — SHALL name exactly the
`status:*` states the declared loop and the commands write and the app reads: `backlog`,
`ready-for-proposal` (the entry label a person applies), `needs-refinement`, `spec-ready`,
`ready-for-implementation`, `in-progress`, `code-review` and `done`, with `needs-refinement` beside
`spec-ready` rather than before it. It SHALL NOT document a state nothing writes. `/aio:sync` SHALL
move the issue to `status:done` when it closes it, so the label agrees with the closed issue.

The WIP cap and the hold label SHALL be named in the prose where the harness reads them —
`x-harness.wip` in `.harness/issue-loop.yaml` and `labels.hold` in `.harness/config.json` — and no
prompt or skill SHALL name a key neither file declares.

#### Scenario: A merged change reads as done
- **WHEN** `/aio:sync` merges a change and closes its issue
- **THEN** the issue carries `status:done`

#### Scenario: A prompt points at the file that holds the number
- **WHEN** `/aio:implement`, `/aio:ship` or `/aio:waves` checks the WIP cap
- **THEN** it reads `x-harness.wip` in `.harness/issue-loop.yaml`, the same line the watcher reads

### Requirement: The claim is the pushed `change/<issue>` branch, taken before any work
Every path that starts work on an issue — `/aio:propose`, `/aio:waves` and the loop's `claim` node —
SHALL claim it through `.harness/scripts/claim` **before** writing anything on the branch. The script
SHALL create `change/<issue>` at origin with an empty commit of its own on top of the base, made
without a checkout, so that no other clone can push the same commit; and SHALL count the claim as won
only when its push **created** the ref (`*` in `git push --porcelain`). It SHALL remember its claim
commit in `refs/aio/claims/<issue>`, and a later run from the same clone SHALL count the claim as its
own when origin's branch contains that commit. Any other existing `change/<issue>` SHALL be a lost
claim, and the caller SHALL stop. No command SHALL claim with a differently named branch or with a
branch that exists only locally.

#### Scenario: Two clones claim from the same trunk
- **WHEN** two clones that just fetched the same trunk claim `change/<issue>`
- **THEN** one creates it and holds the issue, and the other's push is rejected and it stops

#### Scenario: A rerun recognises its own claim
- **WHEN** `/aio:propose` failed after claiming, and runs again from the same clone
- **THEN** the claim script answers that the branch already carries this clone's claim, and work goes on

#### Scenario: A wave hands out an issue
- **WHEN** `/aio:waves` claims an issue for a worktree
- **THEN** the worktree is on `change/<issue>` from origin, which `/aio:propose` then uses as it is
