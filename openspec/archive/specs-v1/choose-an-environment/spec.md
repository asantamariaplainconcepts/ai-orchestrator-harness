# choose-an-environment Specification

## Purpose
TBD - created by archiving change define-session-and-environment-seams. Update Purpose after archive.
## Requirements
### Requirement: An environment is a separate seam from the session, and local is real
The system SHALL expose a seam `IEnvironment` in `BuildingBlocks/World/` separate from `ISession`, answering where a run executes. The first implementation SHALL be a real local environment — `LocalEnvironment` — not a stub or a simulation. A run names its environment on the `Run` row, the same column the remote (ACA) environment already names; local is the default a folder declares.

#### Scenario: A folder's default environment is local
- **WHEN** a folder declares no environment override
- **THEN** a run the folder starts executes on the local environment, and that is named on the run

#### Scenario: The local environment is real
- **WHEN** a run executes on the local environment
- **THEN** a real local process backs it, and the seam is not a stub that pretends execution

### Requirement: A run may override its environment among the available ones
A run SHALL be able to name an environment different from the folder's default among the environments available on this machine, carried on `RunLaunch` and written by `StartRun`. The available environments SHALL be enumerable so a screen can offer them; an environment that is not available SHALL be refused with an explanation, never silently simulated as the default.

#### Scenario: A run overrides the default environment
- **WHEN** a run names an environment the folder did not default to, and it is available
- **THEN** the run executes on the named environment

#### Scenario: An unavailable environment is refused
- **WHEN** a run names an environment that is not available on this machine
- **THEN** the start is refused with a sentence the caller can show, and the run is not started on a substitute

### Requirement: The local environment owns the worktree on the launcher's behalf, not as a navigation destination
The local environment SHALL create, reuse and retire the git worktree a run needs, through `IWorktreeManager`, as the loop's launcher directs. The worktree SHALL NOT be a navigation destination a screen carries (`?worktree=`), and the manual worktree UI (`POST /api/folders/{id}/worktrees`) SHALL NOT be exposed. Retiring a worktree SHALL NOT delete pending work; it follows the loop, and the journey a worktree held survives its removal in the run's transcript, commits and pull request.

#### Scenario: A run starts in a worktree the environment manages
- **WHEN** a run is started and a worktree is required
- **THEN** the launcher ensures the worktree through `IWorktreeManager`, and the screen never carries a `?worktree=` parameter to name it

#### Scenario: Retiring a worktree does not delete pending work
- **WHEN** a worktree is retired per the loop
- **THEN** the transcript, commits and the open pull request remain, and closing the view that held the worktree does not delete the journey

### Requirement: Unimplemented environments are not shown as if they worked
The seam SHALL only enumerate environments this machine actually has an implementation for. Docker and Kata environments SHALL be documented as future extensions, and ACA SHALL be separate and reserved to previews. The system SHALL NOT offer an environment it cannot run on as if it were available.

#### Scenario: A machine with only a local environment offers only local
- **WHEN** the available environments are enumerated on a machine with no cloud adapter
- **THEN** the local environment is the only one offered, and no Docker, Kata or ACA environment is shown as working

#### Scenario: Future environments are documented, not enabled
- **WHEN** the seam's documentation is read
- **THEN** Docker and Kata are named as future extensions and ACA as a preview-only separation, and none are offered until an implementation exists
