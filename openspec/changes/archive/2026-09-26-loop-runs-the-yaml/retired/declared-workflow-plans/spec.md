## REMOVED Requirements

### Requirement: Importing is a deliberate act that replaces the plan and keeps the skips
**Reason**: `.harness/workflows.json` stops being a Run's plan; the loop in
`.harness/issue-loop.yaml` is (#365). The Workflows module — Postgres plans, import, export — is
deleted, and with the file unread it cannot fall out of step with the rows.
**Migration**: a step's skip becomes a node's `when` in the loop; the loop editor (#363) edits it.

### Requirement: Exporting writes the plans back without the bookends
**Reason**: there are no plan rows to export; the loop file is written in place by `IDeclaredLoopFile`
(#363), which already keeps every comment and line it did not touch.
**Migration**: none — the write path already exists.

### Requirement: The source answer is one of four states, with the reason and the path
**Reason**: the question "are the rows the ones the file declares" has no rows to ask about; the
loop of record is the file, read at load and refused when invalid. Nothing can silently part from
it because nothing copies it.
**Migration**: `GET /api/workflows/source` is deleted with the module.

### Requirement: The declared file is read for people and fails closed
**Reason**: `IDeclaredWorkflowFile` and `HarnessWorkflowFile` are deleted with the file they read.
The loop file's reading terms live in `run-the-declared-loop`.
**Migration**: none — `.harness/workflows.json` is deleted from this checkout, and #356 closes
unfixed because the file it describes stops existing.

### Requirement: Controlled execution survives the declaration round trip
**Reason**: a controlled Run's plan is the declared loop itself, not a plan imported from
`workflows.json`; the round trip that had to preserve a `controlled` flag has no second medium to
carry it across. The controlled execution contract is untouched (see
`controlled-workflow-execution`).
**Migration**: a transition that used to name a declared workflow now names a `command` node —
what a machine's `startRun` named becomes the loop's `command` node, whose shape the controlled
admission already demands.
