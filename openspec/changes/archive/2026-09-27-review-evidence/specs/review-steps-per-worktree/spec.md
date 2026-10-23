## REMOVED Requirements

### Requirement: A marked review step is stored against the working directory and the commit it was marked on
**Reason**: review steps are no longer marked. The Change tab's steps are an order to read a change
in, not a checklist to complete: the reviewer is trusted to have read what they merge, and a row of
ticks proved only that somebody clicked them.
**Migration**: `reviewed:*` labels already on issues are left where they are and read by nothing.

### Requirement: The marks say a person looked, and nothing else
**Reason**: there are no marks.
**Migration**: none.

### Requirement: The marks are read from the task's labels
**Reason**: there are no marks to read; the tab carries no count and the footer no progress.
**Migration**: none.

### Requirement: Marking and unmarking go through the task-label write path
**Reason**: there is no marking; the task-label write path itself is unchanged.
**Migration**: none.
