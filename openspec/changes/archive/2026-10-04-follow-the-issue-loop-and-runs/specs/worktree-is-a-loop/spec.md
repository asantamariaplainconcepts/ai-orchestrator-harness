## ADDED Requirements

### Requirement: A task's walk is answerable without a worktree

The question a worktree's journey header answers — where this loop stands,
what the agent is doing, what has been walked — SHALL also be answerable
from the task itself, on the task detail, for the task's own identity:
the walk is grouped by the task's runs across the directories they worked
in, not only by the one directory a worktree names. The worktree's header
keeps its reading and its screen; nothing here moves or duplicates it.
What the task-side walk draws SHALL be the same reading — `journeyFor`'s
declared-machine walk where the directory carries the loop, the observed
command words where it does not — and the lifecycle label the task
carries SHALL NOT be read as a position on the machine.

#### Scenario: A task between directories

- **WHEN** a task's runs worked in two directories, one still present and one gone
- **THEN** the task's walk shows both runs' facts, the present directory's declaration marking the live position and the gone directory's runs as walked history

#### Scenario: The worktree's header is unchanged

- **WHEN** a worktree for the same task is open on its own screen
- **THEN** its journey header answers as it does today, and the task detail's walk does not replace or redirect it
