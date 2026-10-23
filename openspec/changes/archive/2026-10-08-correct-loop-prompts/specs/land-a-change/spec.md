## ADDED Requirements

### Requirement: The gate runs on the commit the merge takes

`/aio:sync` SHALL run the gate on the close-out commit — the branch head after the archive, the retro
entry and the version bump — and SHALL NOT count a gate run on an earlier commit, because the
close-out changes the tree the merge lands. A red step SHALL stop the merge.

#### Scenario: The close-out moved the head

- **WHEN** the gate passed on the implementation and the close-out commit was pushed after it
- **THEN** the gate is run again on the close-out commit before the merge script is called

### Requirement: /aio:ship decides every step that asks a person, or fails it

`/aio:ship` SHALL name, for each step of the commands and skills it runs that asks a person, what
the rule decides in their place or that the step is a failure that halts and holds. It SHALL refuse
an argument that is not an open issue, and a pause a person asks for SHALL stop the run without a
hold.

#### Scenario: An approval nobody can give

- **WHEN** sync's approval gate is reached in a ship whose pull request has no other reviewer
- **THEN** a comment on the pull request records that it lands through ship and what was checked, before the merge

#### Scenario: A rebase conflict in ship

- **WHEN** bringing the branch up to date stops on a conflict
- **THEN** the run halts, applies the hold and comments the conflict
