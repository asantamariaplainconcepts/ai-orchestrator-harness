# open-on-desktop Specification

## Purpose
The four buttons that hand a folder to something else on this machine — an editor, the file manager, a
real terminal, a terminal with the agent already in it. One slice for all four, because they differ only
in which method of the seam they call. Reverse-engineered from the shipped code, not negotiated ahead of
it.

## Requirements
### Requirement: Four actions, one slice, one resolved directory
The system SHALL expose `POST /api/folders/{id:guid}/open/{action}`
(`Features/Folders/UseCases/OpenOnDesktop.cs`, `workspace:open-on-desktop`) covering "Open in VS Code",
"Reveal in Finder", "Terminal here" and "Terminal + Claude Code" as one slice, since they share resolving
the folder, choosing between the checkout and a worktree, and turning a typed failure into a sentence.
The directory SHALL be resolved from the folder and its worktrees rather than taken from the caller.

#### Scenario: The action decides the seam method and nothing else
- **WHEN** each of the four actions is invoked for the same worktree
- **THEN** each calls its own `IDesktopIntegration` method against the same resolved directory

### Requirement: Each failure gets its own words
A failure SHALL be reported in words a reader can act on: "the folder is gone" and "no editor is
installed" call for different actions, and a single "could not open" would tell them neither.

#### Scenario: A missing editor is not the same sentence as a missing folder
- **WHEN** the editor is not installed, **THEN** the response says that
- **WHEN** the directory no longer exists, **THEN** the response says that instead

### Requirement: Which editor to open is a preference, held to a safe shape
The editor this opens SHALL come from the saved preference, falling back to what `DesktopIntegration` has
always read, and the value SHALL be held to a shape that cannot reach a shell wherever it was set from.

#### Scenario: The saved editor is the one used
- **WHEN** an editor has been chosen in the "Open in" menu
- **THEN** subsequent opens use it without asking again
