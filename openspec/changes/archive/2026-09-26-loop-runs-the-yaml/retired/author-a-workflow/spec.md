## REMOVED Requirements

### Requirement: A workflow is created with its bookends already in place
**Reason**: the Workflows module and its endpoints are deleted (#365); a Run's plan is the declared
loop, not a stored workflow with fixed bookends.
**Migration**: none — the endpoints and rows are removed with the module.

### Requirement: Starting from a copy takes the commands and the holds
**Reason**: with no stored workflows there is nothing to copy; a loop is copied by copying the file,
which is a reviewable diff.
**Migration**: none.

### Requirement: The list is grouped by scope and says when a project workflow shadows a general one
**Reason**: with no stored workflows there is no list to group; one checkout declares one loop.
**Migration**: `GET /api/workflows` is deleted with the module.

### Requirement: One workflow is read with the palette to insert from
**Reason**: the step editor is deleted with the module; the loop editor of #363 — which edits
`.harness/issue-loop.yaml` keeping every comment — is what remains on the AI workflows tab.
**Migration**: `GET /api/workflows/{id}` is deleted; `PUT /api/workflows/loop` remains.

### Requirement: The four step edits are one slice, and the domain's rules are refused in words
**Reason**: there are no stored steps to edit; the loop's nodes are edited in the loop editor, and
an invalid loop is refused at load (see `run-the-declared-loop`).
**Migration**: none.

### Requirement: A rename changes the display name and nothing else
**Reason**: no stored workflows remain to rename.
**Migration**: none.

### Requirement: Removing a workflow deletes a row, and a run using it refuses the removal
**Reason**: no stored workflows remain to remove.
**Migration**: none.
