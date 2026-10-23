## ADDED Requirements

### Requirement: The loop's deterministic steps are scripts, one file per step
Every step of the loop that needs no judgement SHALL be one executable file under `.harness/scripts/`
that answers one JSON line: `claim`, `prepare`, `file-overlap`, `gate`, `status`, `evidence`, `merge`
and `cleanup`. The `/aio:*` commands SHALL name the script rather than describe its git or gh calls,
the declared loop SHALL run it as a `bash` node where it is a node, and where the app takes the same
step — preparing a Run's worktree, merging, removing a worktree or its branch — it SHALL run the same
file: the repository's copy, or the copy it carries when the repository has none. Each script SHALL
be runnable by hand from anywhere in the repository, worktrees included.

`status` SHALL move an issue in one edit, removing every other `status:*` label, never removing the
hold, and refusing a state outside the eight. `gate` SHALL stop on the hold, then on the WIP limit read
from `x-harness.wip`, naming the issues holding it, and SHALL stop when the limit cannot be read.

#### Scenario: A move keeps the hold
- **WHEN** a held issue is moved with `status` to another state, without `--hold`
- **THEN** it carries the new state and still carries the hold

#### Scenario: The WIP gate names who holds the room
- **WHEN** the limit is 2 and two other issues are in progress
- **THEN** `gate` answers `go: false` naming both

#### Scenario: The same step by hand and from the app
- **WHEN** a person runs `.harness/scripts/prepare 42` and the app starts a Run for #42
- **THEN** both run the same file and land in the same worktree, prepared the same way
