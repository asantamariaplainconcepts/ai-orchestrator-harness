## Why

Opening a PBI today answers the launch form, not the voyage. `PbiDetailPane` paints `LoopFlow`
beside the body when a live task exists, and `JourneyWalk` only when the mirror row is gone — so a
PBI in mid-loop shows a row of lifecycle chips rather than the loop's stages, and the richest
reading that exists (`PbiWalk`'s `journeyFor`, which already resolves the declared machine against
observed runs) is not the path the detail takes. The position the Connector now serves (#449) is not
drawn either, and the bash stages of the loop produce no runs, so the spine has to mark a stage that
was reached by a script rather than by an agent without inventing a run for it.

## What Changes

- The PBI detail opens **Journey as its centre**, with or without a PR and with or without a
  worktree, replacing the body-first layout of `PbiDetailPane`. Launching an agent is an action of
  the step it belongs to, not the heading of the screen.
- A **vertical spine of stages** is drawn from the declared loop, each stage named by the node the
  loop declares and marked as **reached** (a transitioned run observed it), **current** (the live or
  held position), **held** (the Connector's hold beside the position), or **ahead** (declared, not
  yet observed). A stage reached by a loop script rather than by an agent run is shown as reached
  **without a run**, so the spine distinguishes a stage with evidence from a stage without it.
- Runs are **grouped under their stage** by the run's command word to the node's (the correlation
  `journey.ts` already computes), chronological within the stage; runs whose command word no node
  declares stay off-loop, the way `journeyFor` already draws hand-typed runs.
- The **current stage names the next permitted action or the reason for the block** — the loop's
  own `NEXT_ACTION` for the position, or the hold's own words — read from the Connector's position
  and hold answer, not re-parsed from labels in the UI.
- The journey is **consultable after the worktree is gone**: runs are read by the PBI's full identity
  (source, repository, key) and persist by `WorktreeName`, so the spine, the runs and their facts
  survive `workingPath` and the `PbiLoopCommandButton` that died with it.
- A read of **a PBI's runs by identity** is served rather than filtered client-side, joining the
  run's folder to the repository it names — the same identity join `PbiWalk` does in the browser,
  answered once at the seam. The four connection states (loading, error, absence,
  unattributed) are named as themselves.
- The PR and evidence references the journey shows go to the published object and the issue, not to
  local paths.
- **Tasks hijas** are shown only when a vendor integration actually provides them; none are
  synthesised from runs and no speculative storage is created.

## Capabilities

### New Capabilities

(None. The journey is the read side of capabilities that already exist; the delta is a modification
of how the detail and the run's walk are drawn and served, not a new capability.)

### Modified Capabilities

- `pbi-details`: the detail's body opens the journey as its centre, with or without a worktree, and
  the position the Connector translated is drawn on the spine. New requirements govern the spine's
  stage states, the runs grouped under their stage, the next-action or block reading, and the journey
  that survives the worktree.
- `read-a-run`: a read of a PBI's runs by full identity is served (the identity join `PbiWalk` does
  client-side today, answered once at the seam), and the walk's stages distinguish a stage reached
  by a loop script from a stage reached by an agent run. The existing walk requirements for observed
  position, several runs distinguished, and the four connection states are tightened to the spine.
- `the-narrow-design-set`: the PBI journey's spine is a covered narrow surface, so a 390×844
  light/dark pair is retaken where the product is available and laid out vertically.

## Impact

- **Frontend:** the PBI detail routing (the pane the Overview and the PBIs screen open), `PbiWalk`,
  `PbiDetailPane`, `LoopFlow` and `JourneyWalk` converge on one spine reading; `journey.ts`'s
  `journeyFor` is the single resolution, and the bash-stage-without-a-run distinction is added to
  it. The three shared files (`contracts.ts`, `queries.ts`, `en.ts`) gain the journey's contract,
  query and copy, appended to the PBI screen's block.
- **Backend:** a read of runs by PBI identity (the join the browser does today, served once), behind
  the `read-a-run` schema. No migration: the run row already carries `WorktreeName`, `TaskKey`,
  `folderPath`, cost and timing; nothing is added to the schema, and the journey remains a
  projection read from facts the runs already own.
- **Seam:** the position read is `IPbiSource`'s (#449), the loop declaration is `IDeclaredLoopFile`'s,
  and the run read stays within the Runs module. No module crosses another's schema: the journey is
  composed above the seam from reads each module serves.
- **Scope boundaries (reaffirmed):** this change does not add the Review/Proposal/Evidence tabs of
  the detail (#454/#455) nor the sessions tray (#453); the journey is the centre, the others are
  later.
