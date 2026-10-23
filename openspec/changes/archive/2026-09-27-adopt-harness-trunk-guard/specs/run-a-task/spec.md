## MODIFIED Requirements

### Requirement: A run works in the directory the run row records
A run SHALL always work in a worktree beside the checkout: when the start names a `WorktreeName`, that branch; when it names none, the branch the system derives — `change/<task-key>` with the `#` stripped (`#482` becomes `change/482`) and sanitized with the same rules as the Create worktree dialog (`..` refused, no leading `-`, any character a branch name cannot carry becomes `-`). A derived branch SHALL be ensured, not created: the second run of the same task lands in the worktree the first one made. The checkout itself SHALL NOT be a working directory of a run. The directory a run worked in SHALL be recomposable from what the run wrote down — the folder and the worktree's name — with the slash rule applied, because a branch called `change/80` lives in a directory called `change-80`. `StartRun` SHALL take the path `git worktree add` reported rather than composing one, since git's answer is the authoritative one.

#### Scenario: A start with no worktree named gets one derived
- **WHEN** a start carries no worktree name and the task key is `#482`
- **THEN** the run works in the worktree for `change/482`, ensured beside the checkout

#### Scenario: The second run of a task lands where the first worked
- **WHEN** two runs of the same task start with no worktree named
- **THEN** both work in the same worktree, because a derived branch is ensured rather than created

#### Scenario: The checkout is never the working directory
- **WHEN** any run starts, with or without a worktree named
- **THEN** the agent works in a worktree, never in the folder's checkout

#### Scenario: A branch that cannot be ensured refuses the start
- **WHEN** git refuses to ensure the derived worktree
- **THEN** the start is refused with git's own sentence and no run row is written

#### Scenario: The recorded path is git's, not a composition
- **WHEN** a run starts in a fresh worktree
- **THEN** the path stored is the one git reported

#### Scenario: A branch with a slash resolves to its directory
- **WHEN** anything asks afterwards where a run on `change/80` worked
- **THEN** the answer is the `change-80` directory
