# which-requirements-have-a-test Specification

## Purpose

Say which of a change's requirements a test claims to cover, and which declared checks are silent
about it — as three facts rather than one count.

The Checks reading says how many declared commands passed and on which commit, and that is all it
says: a change with no new test draws it exactly as a change with fifteen, and a folder that declared
no lint draws it exactly as one whose lint failed. This capability separates what the declared checks
last said in this directory, what the change's own task list claimed a test for, and what was never
declared at all — because a count that cannot tell an absent check from a silent one is the mistake
this repository has already written down as 118 tests reported as a full run of 132.

**It judges nothing and derives nothing.** There is no threshold, no percentage and no tone meaning
"under-tested"; which requirement a test covers is the claim its author wrote into `tasks.md`, not a
finding; and no runner's output is parsed — a declared command returns an exit code, and its last
line is quoted rather than read. The one thing read from a path is whether a file is a test, because
nothing in a repository declares that, and the rule that reads it is drawn on the screen beside the
count it produces.

## Requirements
### Requirement: An annotated file is recognised as a test by its name

The system SHALL decide whether an annotated path is a test from the path alone, because nothing in a
repository declares that a file is one.

A path SHALL be a test when its last segment contains `.test.` or `.spec.`, or when its base name
begins `test_` or `test-`, or ends `_test`, `_tests`, `-test`, `-tests`, `Test` or `Tests`, or when
any segment of the path is `__tests__`.

Each suffix SHALL carry a boundary — a separator, or the capital `T` this repository's own
convention writes. A bare `test` suffix matched without one calls `latest.ts`, `contest.cs` and
`protest.py` tests.

The rule SHALL be **the file's name and not its directory**. Measured on this repository, 67 files
carry a test attribute and all 67 are named `*Tests.cs`, and no file so named carries none — while a
`tests` directory here also holds project files, fixtures and a recorded transcript, none of which is
a test. **Over-counting tests makes a change look better tested than it is**, and the name rule's
error runs the other way, where the file is still visible as an ordinary row of the diff.

#### Scenario: A .NET test file

- **WHEN** an annotation names `src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/AnnotationTests.cs`
- **THEN** it is a test

#### Scenario: A fixture beside the tests is not a test

- **WHEN** an annotation names `src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/WorkspaceFixture.cs`
- **THEN** it is not a test, and it is still answered as an annotated file of the diff

#### Scenario: Another ecosystem's spelling

- **WHEN** an annotation names `src/frontend/features/folders/facts.test.ts`, `worktree/paths_test.go`, `tools/test_paths.py`, or `src/__tests__/helpers.ts`
- **THEN** each is a test

#### Scenario: A word that merely ends in those letters

- **WHEN** an annotation names `src/shared/latest.ts`, `src/entries/contest.cs`, or `src/protest.py`
- **THEN** none of them is a test

### Requirement: A working directory answers which requirements its annotation claims a test for

The system SHALL answer, for one working directory, every requirement id its annotation claimed
anywhere, and for each the annotated test paths that claimed it.

The ids SHALL be taken **verbatim**, as `<capability>/<ordinal>`, without being resolved against any
spec folder — the same claim the annotation already carries, repeated and not checked.

A requirement claimed only by files that are not tests SHALL be answered with an empty list of tests
rather than omitted. **The requirement nothing tested is the answer the step exists for**, and an
omission would leave it indistinguishable from a requirement nobody mentioned.

The requirements SHALL be answered for annotated paths the diff matched and for annotated paths it
did not, together — a test file claimed by a task and then reverted still leaves a claim, and
dropping it would read as satisfied.

#### Scenario: A test claims a requirement

- **WHEN** a closed task carries `req: issue-tab/1` and `files: src/worktree/pull-request.ts, src/tests/PullRequestTests.cs`
- **THEN** `issue-tab/1` is answered with `src/tests/PullRequestTests.cs` as its test
- **AND** `src/worktree/pull-request.ts` is answered as an annotated file that is not a test

#### Scenario: A requirement nobody tested

- **WHEN** a closed task carries `req: issue-tab/2` and names only files that are not tests
- **THEN** `issue-tab/2` is answered with no tests, rather than left out of the answer

#### Scenario: Nothing was annotated

- **WHEN** a working directory has no change in progress, or one whose tasks annotated nothing
- **THEN** no requirements are answered, and the directory, its diff and its changes are still answered

### Requirement: The Tests step says how many of the claimed requirements have a test

The step SHALL state, as a fraction, how many of the requirements this directory's annotation claimed
have at least one test claiming them.

The fraction's denominator SHALL be named in the copy as **what the change's tasks claimed**, never
as what the change's spec delta contains. A requirement of the delta that no task annotated does not
appear in the annotation and therefore cannot appear here, and stating a claimed count as the
change's count would be the screen asserting something nobody established.

Every requirement without a test SHALL be listed by its id, one line each, rather than only counted.

The step SHALL judge nothing about the number: no threshold, no percentage, and no colour meaning
"under-tested". Two numbers side by side, and the reader decides.

#### Scenario: Some requirements have a test

- **WHEN** three requirements are claimed and one of them is claimed by a test
- **THEN** the step reads that 1 of the 3 requirements the change's tasks claim has a test
- **AND** the other two are listed by id

#### Scenario: Nothing annotated a test

- **WHEN** a change annotated files and none of them is a test
- **THEN** the step says zero have a test, and lists every claimed requirement
- **AND** it does not infer a test from any path in the diff

#### Scenario: The recognition rule is readable

- **WHEN** the step is drawn, whether or not any test was recognised
- **THEN** the rule by which a file is taken for a test is on the screen beside the count

### Requirement: The Tests step lists the tests this change added, with what each claims to cover

The step SHALL list every annotated test path, each with the requirement ids its task claimed, and
SHALL state how many there are.

A test path the diff did not match SHALL be listed and marked as claimed but absent, in the words the
Code step already uses for an annotation that stopped applying.

The list SHALL be the agent's claim and SHALL be attributed as one. Nothing verifies that a named
test covers the requirement named beside it; a person reviews it as they review the rest of
`tasks.md`.

#### Scenario: Two tests, one requirement each

- **WHEN** two annotated test files claim one requirement each
- **THEN** the step lists both with their ids and states that two tests were annotated

#### Scenario: A test that is no longer in the diff

- **WHEN** an annotated test path matches no file of the diff
- **THEN** it is listed as claimed but not in this diff, rather than counted as a test this change added

### Requirement: The Tests step draws each declared check as it last ran in this directory

The step SHALL draw, for every command the folder declared as a check, how it ended, how long it
took, how long ago it finished, and the last line it printed.

How it ended SHALL be the exit code: `exit 0`, `exit {code}` for anything else, and *did not finish*
where the command has a start and no end — a run that was killed or whose session was lost did not
pass, and drawing it as one is the failure the record exists to prevent.

The last line SHALL be shown **verbatim and unparsed**. No runner's output is interpreted: a
declared command returns an exit code, and what it printed is attributed to it rather than read.

A check that failed SHALL carry its exit code in the text and not only in its colour, so that the
state survives a reader who cannot resolve the ink.

The set of checks SHALL be the commands somebody declared as checks and no others, resolved through
the one answer the frontend has to that question.

#### Scenario: A check that passed

- **WHEN** a declared check finished with exit code 0 in this directory
- **THEN** the row reads `exit 0`, how long it took in seconds, how long ago it finished, and the last line it printed

#### Scenario: A check that failed

- **WHEN** a declared check finished with a non-zero exit code
- **THEN** the row states that exit code in its text and is drawn in the failing ink

#### Scenario: A check that was killed

- **WHEN** a check has a start time and no finish time recorded
- **THEN** the row says it did not finish, and does not show an exit code

### Requirement: A check recorded on another commit says so, in four answers

The step SHALL state whether a check's ending is still about the commit this directory is on, using
the four answers the record already carries: on this commit, that many commits behind, on a commit
this repository no longer has, and never run here.

Only *on this commit* SHALL be drawn as a check that is about what is in the directory now. A pass
recorded three commits ago is not a pass for the current state, and a row showing only the code
invites somebody to approve on it.

#### Scenario: A pass on an earlier commit

- **WHEN** a check passed and the directory has moved two commits since
- **THEN** the row says it ran two commits ago, alongside its exit code

#### Scenario: A commit that is gone

- **WHEN** the commit a check ran on can no longer be compared against the directory's head
- **THEN** the row says so, rather than rounding it to this commit

### Requirement: Nothing declared and nothing run are two states, and neither is a tick

The step SHALL draw a folder that declared no checks as `not declared`, in its own words, in the
muted ink, and SHALL name what somebody would do about it.

The step SHALL draw a declared check that has never run in this directory as its own second state,
distinct from the first.

Neither SHALL be drawn as a pass, and neither SHALL be drawn as an empty space. **A count that cannot
tell an absent check from a silent one is the failure this step exists to end** — this repository has
a written record of 118 tests reported as a full run when there were 132.

#### Scenario: A folder with no checks declared

- **WHEN** no command of the folder is marked as a check
- **THEN** the step says no checks are declared and names where a command is marked as one
- **AND** the tests this change annotated are still listed above it

#### Scenario: A declared check nobody has run here

- **WHEN** a folder declares a check with no record in this working directory
- **THEN** its row says it has not run here, and is not drawn as either a pass or a blank

### Requirement: The Tests step marks nothing and runs nothing by itself

The step SHALL not mark itself, or any other step, as done. What is done is the reviewer's own
judgement, recorded when they say so, and a tick the screen awarded itself would be worse than no
tick at all.

The step SHALL run no command on being opened. Re-running the declared checks SHALL be an action a
person takes, and SHALL be the same action the screen already offers for it rather than a second way
to start a declared command.

#### Scenario: Every check passes

- **WHEN** every declared check passed on this commit and every claimed requirement has a test
- **THEN** the step is still not marked done, and the footer's count is unchanged

#### Scenario: Opening the step

- **WHEN** somebody opens the Tests step
- **THEN** nothing is started, and the rows show what was last recorded in this directory

