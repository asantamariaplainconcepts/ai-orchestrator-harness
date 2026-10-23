# Delta: dispatch-observation

## ADDED Requirements

### Requirement: The facts a claim owes are read where the claim lives
A dispatch-mode tick SHALL read the OpenSpec change a `ready-for-implementation` claim owes from
the claim's working path — the worktree `change/<n>` when the checkout carries one, the primary
checkout otherwise — and SHALL fall back to the checkout's `origin/change/<n>` ref (read through
git, never a checkout) when the path it reads holds no change. A change found in either place
satisfies the fact; the refusal SHALL stand only when neither place holds one. The branch fact is
already read against local and remote refs and SHALL NOT change. The loop file itself SHALL still
be read from the checkout, never a worktree's copy (#365's rule is untouched).

#### Scenario: A proposal made in the worktree passes the gate
- **WHEN** a `ready-for-implementation` task's change exists under the worktree `change/<n>` and not
  under the primary checkout
- **THEN** the tick finds the change and dispatches, rather than refusing with
  `no OpenSpec change in openspec/changes/`

#### Scenario: A proposal made on another machine passes the gate
- **WHEN** the worktree `change/<n>` does not exist locally but `origin/change/<n>` holds the
  change's `openspec/changes/<name>/tasks.md`
- **THEN** the tick finds the change through the ref and dispatches

#### Scenario: No change anywhere still refuses
- **WHEN** neither the working path nor the local and remote `change/<n>` refs hold a change
- **THEN** the tick refuses, and the refusal names `/aio:propose` as the remedy
