## ADDED Requirements

### Requirement: The journey's done step offers the merge with state and block motive

The `done` step of the PBI journey (`PbiSpine`) SHALL present the merge as its action — the same
operation the loop's `merge` node and `/aio:sync` run — offered by the step itself rather than by a
form above the pane. The step SHALL state its state and, where the merge is blocked, the motive for
the block drawn from the admission's typed refusals: a held PBI names the hold, a missing or moved
grant names the grant, an unarchived change names the archive, a dirty tree names the tree. The
launch SHALL be addressed by the PBI's identity (source, repository, key) and the folder, with the
worktree as the launcher's resolved detail; the step SHALL NOT require a `?worktree=` the screen
carries.

The merge's confirmed result — landed, resumed, branch left behind — SHALL be drawn on the step from
the same response the worktree's `ChangeTab` reads, and the server's refusals SHALL pass through in
the vendor's own words. A step whose PBI has no pull request or no worktree in flight SHALL name the
absence rather than offering a launch to nothing, the way the existing merge dialog names a missing
PR. The worktree's `ChangeTab` SHALL keep its merge control; this change composes the same confirmed-
merged renderer into the step rather than moving it.

#### Scenario: The done step offers the merge for a PBI ready to land

- **WHEN** the journey reaches the `done` step for a PBI whose PR is open, whose worktree is in
  flight, whose change is archived, whose tree is clean and which carries no hold
- **THEN** the step offers the merge as its action, the launch is addressed by the PBI's identity,
  and the worktree's path and branch are the launcher's detail

#### Scenario: The done step states the motive for a block

- **WHEN** the PBI the step is for carries an active hold, or its grant is missing, or its change is
  not archived, or its tree is dirty
- **THEN** the step states which block it is, in the admission's typed words, and the launch is
  still reachable rather than disabled — the refusal comes when it is taken, the way the merge-gate
  spec requires

#### Scenario: A PBI with no worktree or PR in flight names the absence

- **WHEN** the `done` step is drawn for a PBI whose folder holds no worktree for its identity, or
  whose branch has no pull request
- **THEN** the step names the absence rather than offering a launch to nothing

#### Scenario: The worktree's ChangeTab keeps its merge control

- **WHEN** the worktree's `ChangeTab` is open for the same PBI
- **THEN** its merge control behaves as it did, and the journey's step is a second surface for the
  same operation rather than a replacement

#### Scenario: The merge's result is drawn on the step

- **WHEN** the merge lands, resumes or leaves a branch behind
- **THEN** the step draws the same result words the worktree's dialog draws, from the same response,
  and the step's state advances with the journey
