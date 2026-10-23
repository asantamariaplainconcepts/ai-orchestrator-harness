# annotate-what-a-file-serves Specification

## Purpose

Which requirement a file in a diff serves, said by the agent that wrote the file.

A diff says how much changed and never why, and a path does not say what it is for. So the mapping is
not derived here: it is a claim, written into a change's own `tasks.md` as each task is closed, read
back against that working directory's diff, and repeated on the file row. Nothing verifies it — a
person reviews it like the rest of that file.

The absence carries as much as the claim. A file nobody annotated is drawn with no label at all,
because "nobody declared why this is in the diff" is the answer that makes a 25-file change
reviewable instead of skimmable, and a placeholder would answer it falsely.

One format, read by two steps of a review: the Code step labels the diff's rows from it, and the
Tests step maps a test to a requirement from the same lines.

## Requirements
### Requirement: An agent annotates a closed task with what it served and what it moved

When `/aio:implement` closes a task in a change's `tasks.md`, it SHALL write, on continuation lines
under that task, the requirements of the change's spec delta the task served and the files it moved.

The annotation SHALL be written as `req:` and `files:` lines, each on its own line, indented further
than the task's `-`, each carrying a comma-separated list. Either key MAY appear more than once under
one task, and the lists concatenate. A requirement SHALL be named the way the delta names it —
`<capability>/<ordinal>`, the spec folder under the change's `specs/` and the requirement's position
in it. A file SHALL be named as a repository-relative path.

The annotation SHALL belong to a **closed** task only. A task still open has moved nothing yet, so
what it names is a plan and not a claim.

**This is the agent's claim about its own work.** Nothing derives it from the paths, nothing checks
it, and a task the agent could not attribute is left unannotated rather than annotated with a guess.

#### Scenario: A closed task carries both lines

- **WHEN** `/aio:implement` marks a task `- [x]` after implementing it
- **THEN** the task carries a `req:` line naming the delta requirements it served
- **AND** it carries a `files:` line naming the files it moved

#### Scenario: A task the agent cannot attribute

- **WHEN** a task moved files that serve no requirement of this change — scaffolding, a rename, a formatting pass
- **THEN** the task is closed without a `req:` line rather than with an invented one

### Requirement: The annotation is written once and reaches every runtime

The instruction SHALL live in `.ai/commands/aio/implement.md` and nowhere else. The Claude, OpenCode
and Copilot copies SHALL be links to that file rather than second copies of the text, and `/aio:ship`
SHALL inherit the instruction by naming `/aio:implement` rather than repeating it.

**Two runtimes reading two versions of one instruction is two annotation formats**, and the second
one is discovered by a reader who cannot tell why half the files have no label.

#### Scenario: One source

- **WHEN** the annotation instruction changes in `.ai/commands/aio/implement.md`
- **THEN** every runtime's copy of `/aio:implement` reads the new instruction
- **AND** `/aio:ship` carries no separate statement of the format that could disagree with it

### Requirement: A working directory answers which of its changed files an annotation covers

The system SHALL answer, for one working directory, which files of that directory's diff an
annotation covers and which requirements were claimed for each.

The task lists read SHALL be the `tasks.md` of every change under `openspec/changes/` in that
directory, excluding the archive. The answer SHALL name the changes it read, so a reader knows whose
claim it is looking at.

An annotated path SHALL match a changed file when the two are equal, or when the changed path ends
with the annotated path on a `/` boundary — the same suffix rule a declared seam is already matched
by, and for the same reason: the same file is written at different roots in different places, and a
diff answers one root.

Every file named under one task SHALL take every requirement named under that task. A file named
under two tasks SHALL carry both sets, in the order they were first seen, without repeats.

The directory SHALL be the one the caller named, resolved the way every other reading of a working
directory is resolved, and the answer SHALL be refused for a path that is not one of this folder's.

#### Scenario: A file the agent claimed

- **WHEN** a task list annotates `src/worktree/pull-request.ts` with `req: issue-tab/1, issue-tab/2`
- **AND** the directory's diff contains `src/worktree/pull-request.ts`
- **THEN** that file is answered with both requirements, in the order written

#### Scenario: The same file under two tasks

- **WHEN** two closed tasks both name one file, one claiming `a/1` and the other `a/1, b/2`
- **THEN** the file is answered once, carrying `a/1` then `b/2`

#### Scenario: A path that is not this folder's

- **WHEN** the caller names a working directory that is not one of this folder's worktrees
- **THEN** the request is refused rather than answered about some other directory

### Requirement: An annotated path no changed file matches is said out loud

An annotated path that matches no file in the directory's diff SHALL be reported as such, carrying
the requirements claimed for it, rather than dropped.

**A silent drop is how an annotation ages badly.** A file renamed, reverted or never written leaves a
claim that reads as satisfied because nothing contradicts it. Saying it costs a line and is the
reversible direction: a later change can quieten a warning, and silence people have learned to trust
is much harder to withdraw.

#### Scenario: A path that is not in the diff

- **WHEN** a task list annotates `src/gone.ts` and no file in the diff matches it
- **THEN** the answer reports `src/gone.ts` as annotated-and-untouched with its requirements
- **AND** no file of the diff is labelled with them

### Requirement: A directory with no annotations answers nothing and breaks nothing

A working directory whose task lists carry no annotation — every change written before this exists —
SHALL answer no labelled files and no unmatched paths, and the diff SHALL be drawn whole and
unlabelled.

A directory with no change under `openspec/changes/` at all SHALL answer the same way. Neither is an
error, and neither SHALL be drawn as one.

#### Scenario: A task list from before the annotation existed

- **WHEN** the directory holds a change whose `tasks.md` has no `req:` or `files:` line
- **THEN** the change is named among those read
- **AND** no file is labelled, and every file of the diff is still listed with its counts

#### Scenario: No change at all

- **WHEN** the directory has no `openspec/changes/<name>/tasks.md` outside the archive
- **THEN** the answer names no change and labels nothing

### Requirement: A task list that could not be read whole says so

Where a task list was cut short by the read rather than by its author, the answer SHALL say so, and
the screen SHALL say that the annotations may be incomplete rather than presenting them as the whole
claim.

This is the third answer every reader of text somebody wrote already has here — a value, nothing, and
*the text we were handed is not all the text*. It SHALL be spelled the way the existing readers spell
it rather than given a third name.

#### Scenario: A task list past the read's cap

- **WHEN** a `tasks.md` is longer than the working-directory read will return
- **THEN** the answer says the annotations were read from a cut file
- **AND** the screen says the list may be incomplete instead of showing it as complete

### Requirement: The Code step lists the changed files with what each serves

Step 2 of the Change tab SHALL list the files the working directory changed, each row carrying its
path, what it serves where anything was claimed for it, and its `+N −M`.

The first file SHALL be open with its patch. The rest of the list SHALL be collapsed behind one
control naming how many are hidden, so a 25-file change opens as four rows and `Show 21 more files`
rather than as twenty-five.

The step SHALL also say how many of the files in the diff ended up labelled, and SHALL list any
annotated path the diff does not contain.

#### Scenario: A 25-file change

- **WHEN** the working directory's diff has 25 files
- **THEN** four rows are drawn and one control offers the remaining 21
- **AND** the first row is open with its patch

#### Scenario: What was labelled

- **WHEN** 9 of the 25 files carry a claim
- **THEN** the step says how many of the 25 were labelled

### Requirement: A file row never carries a label nobody wrote

A file row SHALL carry a label only where a task list claimed something for that file. A file nobody
annotated SHALL be drawn with no label at all — not with an empty one, not with a guess from its
path, and not with a label inferred from a neighbouring file.

**The absence is information.** It says nobody declared why this file is in the diff, which is the
question the step exists to answer, and a placeholder in its place would answer it falsely.

The same label SHALL appear on the file row of the Review tab, which reads the same answer for the
same directory.

#### Scenario: An unannotated file

- **WHEN** a file of the diff is named by no task list
- **THEN** its row carries its path and its counts and no label

#### Scenario: The Review tab

- **WHEN** the same directory is read from the Review tab
- **THEN** an annotated file's row carries the same label it carries in the Code step

