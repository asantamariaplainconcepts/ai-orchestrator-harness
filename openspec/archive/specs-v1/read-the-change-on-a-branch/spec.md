# read-the-change-on-a-branch Specification

## Purpose
The change a branch is carrying, opened without anybody navigating a file tree. Every branch of this
repository writes one under `openspec/changes/<name>/`, and it is the first thing a reviewer has to
read — `/aio:propose` stops and sets the hold precisely so that somebody reads it — yet the only way
to reach it from the console was the Files tab, walking the tree by hand and knowing the path.

What this capability answers is deliberately thin: a directory, the Markdown files in it, and three
counts taken off each file — lines, ticked checkboxes out of total, and requirement headers under the
delta's own `## ADDED` and `## MODIFIED` sections. **There is no model of the tool that writes them.**
No requirement graph, no capability index, no mapping of a requirement to the file that implements it.
The day a project keeps its proposals in another shape, there is nothing here to unwind.

Three things it refuses to do, and each is the point. It **chooses nothing**: two unarchived changes
are both answered, because which one a branch is about is not a fact on disk. It treats **absence as a
state**: an artifact a change does not carry is still a row saying so, a branch with no change at all
is a sentence and a success, and a count is null rather than zero for a file that carries no such
thing. And it **never runs the validator** — it shows what a declared command's last run left behind,
which is the only way the screen can say the same thing the terminal did, including when it failed.
## Requirements
### Requirement: A working directory's unarchived changes are read from disk, and none of them is chosen
The product SHALL answer, for one working directory, every directory under `openspec/changes/` that is
not the archive, and SHALL return all of them rather than selecting one.

Which change a branch is "about" is not a fact on disk. A directory holding two unarchived changes is
an ordinary state of a branch that has not finished tidying up, and a product that guessed would be
right most of the time and silently wrong the rest of it.

The path a caller names SHALL be honoured only when it is one of that folder's own worktrees, and an
unmatched path SHALL be not-found rather than forbidden — the rule `WorkingRoot` already holds every
other read to. The checkout is read by naming no path at all.

#### Scenario: One unarchived change is answered with its artifacts
- **WHEN** a working directory holds `openspec/changes/my-change/` beside `openspec/changes/archive/`
- **THEN** the answer names `my-change` and lists the files under it

#### Scenario: Two unarchived changes are both answered and neither is preferred
- **WHEN** a working directory holds two directories under `openspec/changes/` that are not the archive
- **THEN** both are in the answer, in name order, and nothing in the answer marks one as the change

#### Scenario: The archive is never a change
- **WHEN** a working directory holds only `openspec/changes/archive/` with archived changes under it
- **THEN** the answer holds no changes, and no archived change is listed as an unarchived one

#### Scenario: A directory with no changes at all is an answer and not a failure
- **WHEN** a working directory has nothing under `openspec/changes/`
- **THEN** the request succeeds with an empty list, and the screen says so in a sentence

#### Scenario: A path the folder does not own is not found
- **WHEN** a caller names a directory that is not one of this folder's worktrees
- **THEN** the answer is not-found, the same refusal an unknown folder gets

### Requirement: Every artifact is listed with what it weighs, and one that is not there is a state rather than a gap
The product SHALL list each of a change's artifacts with its path and how many lines it has, and SHALL
list the conventional artifact names even when no such file exists, marking those as not present.

A small change legitimately carries no design note. "There is no design note" and "the design note is
missing" are different facts about a change, and a screen that simply omitted the row would say
neither.

The convention is three names — `proposal.md`, `design.md`, `tasks.md`. Everything else found under the
change directory SHALL be listed as it is found. Nothing else about the shape of a change is assumed:
it is a directory holding markdown, and the day a project keeps its proposals in another form, no model
of this one has to be unwound.

#### Scenario: A present artifact carries its line count
- **WHEN** a change holds a `proposal.md` of 131 lines
- **THEN** the row for `proposal.md` says it is present and says 131

#### Scenario: A conventional artifact with no file says so
- **WHEN** a change holds `proposal.md` and `tasks.md` but no `design.md`
- **THEN** there is still a row for `design.md`, marked not present, drawn beside the two that are

#### Scenario: A capability delta is listed under the name it has on disk
- **WHEN** a change holds `specs/read-a-file/spec.md`
- **THEN** it is listed as `specs/read-a-file/spec.md`, after the three conventional names

### Requirement: A delta's added and modified requirements are counted apart
The product SHALL count, for an artifact that carries `## ADDED` or `## MODIFIED` sections, the
requirement headers under each, and SHALL report the two numbers separately.

That is the distinction the delta file itself draws, and it is the one that says whether a change is
widening a contract or altering one already agreed. A single total would read the same for both.

An artifact with no such section SHALL report neither number rather than reporting zero: a
`proposal.md` is not a delta that adds nothing.

#### Scenario: The counts match the file's own headers
- **WHEN** a delta holds a `## ADDED Requirements` section with five `### Requirement:` headers under
  it and no `## MODIFIED` section
- **THEN** it reports five added and zero modified

#### Scenario: A delta that only modifies reports no additions
- **WHEN** a delta holds a `## MODIFIED Requirements` section with one requirement header under it
- **THEN** it reports zero added and one modified

#### Scenario: A file that is not a delta reports no counts at all
- **WHEN** the artifact is a `proposal.md` with ordinary `##` prose headings
- **THEN** it reports neither an added count nor a modified count, and the screen shows only its lines

#### Scenario: A requirement header inside a fenced block is not counted
- **WHEN** a delta quotes a `### Requirement:` line inside a fenced code block
- **THEN** that line is not counted toward either number

### Requirement: A task list is counted from its own checkboxes
The product SHALL report, for an artifact that carries task checkboxes, how many are done out of how
many there are, and SHALL report neither number for an artifact that carries none.

#### Scenario: A change part-way through says how far
- **WHEN** `tasks.md` holds nine checkboxes of which six are ticked
- **THEN** it reports six done of nine, and the number matches the file's own boxes

#### Scenario: An artifact with no checkboxes reports no task count
- **WHEN** the artifact is a `design.md` with no checkboxes in it
- **THEN** it reports no counts, rather than none done of none

### Requirement: The step shows what the declared validation command answered, and never runs it
The product SHALL show, beside a change, the last answer of the working directory's declared validation
command — the command line as declared, what it exited with, and the last line it printed — and SHALL
NOT start it.

This step reports; it does not validate. What is on screen is what a run of the tool left behind, which
is the only way the screen can say the same thing the terminal did, **including when it failed**.

Where no such command is declared, the product SHALL say that in a sentence. Where one is declared but
has never run in this directory, the product SHALL say that instead — they are different facts and a
blank would be neither.

Where the command last ran on a commit the directory has moved past, the product SHALL say so rather
than draw the answer as current, in the same three states this product already uses for a check.

#### Scenario: A passing run is shown with the line that produced it
- **WHEN** the declared validation command last exited 0 on the commit the directory is on
- **THEN** the panel shows the declared command line verbatim, says it passed, and shows the last line
  it printed

#### Scenario: A failing run says it failed and says what it said
- **WHEN** the declared validation command last exited non-zero
- **THEN** the panel says it failed, names the exit code, and shows the last line it printed verbatim

#### Scenario: No declared validation command is a sentence
- **WHEN** the working directory's folder declares no validation command
- **THEN** the panel says none is declared, beside the change it is still showing

#### Scenario: A declared command that never ran here is a different sentence
- **WHEN** the command is declared but has no recorded result in this working directory
- **THEN** the panel says it has not run here, rather than drawing it as passed or failed

#### Scenario: An answer from an older commit is not drawn as current
- **WHEN** the recorded result names a commit the working directory has moved past
- **THEN** the panel says how far it has moved, and does not present the result as being about what is
  in front of the reader

### Requirement: A closed change's result is read from its pull request, not reconstructed locally

When a change's pull request is closed — merged, or closed without merge —
the change's result SHALL be read through the remote pull request reader
(`review-a-remote-pull-request`) against its pinned comparison, and SHALL
NOT be reconstructed from the local worktree: no local diff, file,
OpenSpec-change or evidence reads SHALL serve the closed result. The state
of the worktree — clean, dirty or deleted — SHALL have no effect on what
the closed result shows.

#### Scenario: A merged pull request with a deleted worktree

- **WHEN** a change is merged and its worktree is gone
- **THEN** its result is read from the pull request, and the missing directory changes nothing about it

#### Scenario: A merged pull request with a dirty worktree

- **WHEN** a change is merged and uncommitted changes sit in its worktree
- **THEN** the closed result shows the pull request's comparison, not the uncommitted work

### Requirement: Unpublished local work remains a distinct reading

A change with no pull request, and an open pull request whose worktree
carries work not yet pushed, SHALL keep their local reading: reading what
is true in a working directory now — uncommitted and unpushed work — is a
distinct question from reading a closed result, and it is not reduced by
this change.

#### Scenario: A worktree with unpushed work behind an open pull request

- **WHEN** an open pull request's head is behind its worktree's unpushed commits
- **THEN** the local working diff remains readable, and the pull request review remains readable, as separate surfaces

