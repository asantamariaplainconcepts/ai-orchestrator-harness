## Context

Step 3 of the Change tab is a placeholder that names this ticket. Above it, the fact line already
carries one phrase about the declared checks — `3 checks passed on 6d63601`, or one of the five other
things `facts.ts:checks` can say. That phrase is correct and it is all there is: it does not
distinguish a change that brought tests from one that did not, and it cannot distinguish a check that
was never declared from one that failed.

Three things already exist that this step is built out of, and none of them is rebuilt here.

- **#292's annotation.** `/aio:implement` writes `req:` and `files:` continuation lines under a
  closed task in `tasks.md`, and `GET /api/folders/{id:guid}/annotations` reads them back matched
  against that directory's diff. Its own doc comment says it has two readers: the Code step, which
  labels file rows, and this one, which maps a test to a requirement *from the same lines*.
- **`gateCommands`** (`MergeGate.tsx`) — the frontend's only answer to "is this a check", exported
  for exactly this. A command is a check because somebody ticked a box (#167), never because its
  kind or its name looked like one.
- **`FolderCommand.lastCheck`** — `commit`, `exitCode`, `startedAt`, `finishedAt`, `summary` and
  `since`. Four answers live in `since`: `0` is this commit, `>0` is that many behind, `null` is not
  comparable at all, and no row at all is never run here.

**There is no frontend test framework in this repository** (ledger D19: zero `*.test.*` or `*.spec.*`
outside `node_modules`, no runner in `src/frontend/package.json`). That is the single fact that
shapes this change most, and it cuts twice: it decides where the reading lives, and it is the honest
answer this panel will draw over most of this repository's own diffs.

## Goals / Non-Goals

**Goals**

- Say how many of the requirements this change claimed have a test claiming them, and name the ones
  that do not.
- Draw a declared check as it last ran *in this directory*: how it ended, how long it took, how long
  ago, and whether that ending is still about this commit.
- Make `not declared` a state with its own words and its own ink — not a tick, not a blank.
- Add no new route, query key, permission, error code, enum value, migration or `World` seam.

**Non-Goals**

- **No judgement of coverage.** No threshold, no percentage, no colour for "under-tested". #281
  decided this and #293 inherits it: two numbers side by side, and the reader decides.
- **No runner output is parsed.** `lastCheck.summary` is the last line a command printed and it is
  shown verbatim. If `dotnet test` printed `128 passed`, that is what the row says, and the product
  has not claimed to know what 128 means.
- **The step is not marked by the machine.** Marking is #290's, and it is the person's.
- **Nothing is run on open.** `Re-run` is a button somebody presses.

## Decisions

### Which requirement a test covers comes from the annotation, and the count of new tests does too

The ticket's Seam offers two sources for `3 new`: the diff (files under `src/tests/` with added
lines) or the annotation. **The annotation**, and the ticket calls that answer *más honesta* itself.

A file under a test path with added lines is a file that changed; it is not a test that covers
anything. Deriving the count from the diff and the mapping from the annotation would put two numbers
on one line that disagree the moment somebody reformats a test — a test file touched and not
annotated would raise the count without raising the coverage, and the panel would be reporting
motion as proof.

So one source for both: **a test is a `files:` entry whose name says it is a test, and its
requirements are its task's `req:`.** A change that annotated nothing honestly reads zero, and zero
is the answer this repository's own history says to expect.

### A test file is recognised by its **name**, and the rule is on screen

Nothing declares that a file is a test. There is no field, no manifest, no marker — so the only
answer available is the path, and the choice is which part of it.

**Measured on this repository:** 67 files carry a `[Fact]` or a `[Theory]`, and all 67 are named
`*Tests.cs`. There are exactly 67 files named `*Tests.cs`, so the name rule is a perfect classifier
here — no false positive and no false negative in 67 of 67.

The directory rule — *anything under a `tests` segment* — was the obvious alternative and it is
measurably worse: `src/tests/` holds 8 `.csproj` files, 8 fixtures, `SqlRecorder.cs`,
`opencode-raw.txt` and `Modules.cs`, none of which is a test, and #292's own `tasks.md` annotates
`WorkspaceFixture.cs` under its proof task. The directory rule would call that a test and read one
task's proof as two. **Over-counting tests makes a change look better tested than it is**, which is
the direction this repository refuses; the name rule's failure mode is the other one, and it is
visible — the file still appears, unlisted, in the Code step's row.

The rule covers four ecosystems rather than this one, because the panel is drawn over whatever
repository the folder points at: a name containing `.test.` or `.spec.`, a base name starting
`test_`/`test-`, a base name ending `_test`/`_tests`/`-test`/`-tests`/`Test`/`Tests`, or a
`__tests__` path segment. **Every suffix carries a boundary** — a separator or the capital `T` — and
that is not decoration: matching a bare `test` suffix calls `latest.ts`, `contest.cs` and
`protest.py` tests, three false positives found by writing them down. It is drawn
on the screen next to the count, always — the same thing #294 did with `evidence-{issue}-*`, and for
the same reason: a rule nobody can read looks like a fault the first time it catches nothing.

### The reading is on the server, in C#, with committed tests

#292 put its parse in C# deliberately and wrote down why: **there is no frontend test runner**, so a
parser in TypeScript is a parser nothing can pin. The same reasoning applies to the two things this
change decides — *is this a test* and *which requirements have one* — and they are the two the whole
step rests on.

So `TestClaims.cs` sits beside `TaskAnnotations.cs` in the Workspace module and is exercised by a
committed functional test file. What is left on the client is arithmetic over the answer and markup.

**`GetAnnotations` is extended rather than joined by a second endpoint.** Its own doc says the
matching happens once, on the server, because two surfaces draw these labels and "matching twice is
two rules to disagree". A second route would re-read the same tree, the same file contents and the
same diff to answer the same question a second way. Two fields are appended:

- `AnnotatedFile.IsTest` — the rule above, applied to the path the annotation named.
- `Response.Requirements` — every requirement id claimed anywhere in this directory's annotation,
  with the test paths claiming it. Empty list of tests is the uncovered case, and it is a row rather
  than an omission.

Both are additive. `CodeStep` reads neither and draws exactly as it does today.

### The denominator is the requirements **claimed**, not the requirements of the delta

The ticket's number is *cuántos de los N requisitos tienen test*. The honest N available here is the
set of requirement ids the change's own `tasks.md` named — because that is what the annotation
carries, and #292 was explicit that an id is taken verbatim and nothing resolves it against a spec
folder.

A requirement in the spec delta that **no task annotated at all** therefore does not appear. That is
a real gap and the panel says so in words rather than presenting a claimed count as the change's
count: the copy reads *of the 3 requirements this change's tasks claim* and never *of the 3
requirements in this change*.

Reading the delta itself would mean counting `### Requirement` headers under
`openspec/changes/<name>/specs/<capability>/spec.md` — a second parse of the OpenSpec artifacts,
while **#291 is in flight doing exactly that** for the Proposal step. Widening the denominator to the
delta once #291's read exists is additive and takes nothing off the screen, which is why this is the
reversible direction. It is recorded as a debt rather than guessed at.

### `not declared` and `not run here` are two silences, and neither is a tick

`facts.ts:checks` already draws these as two phrases — `no checks declared` and `checks not run
here` — and only the first has a fix the reader can act on. The step keeps both, per check and for
the set, and gives them the muted ink: not the success ink, and not an empty cell.

A blank is the failure mode this ticket names. `AGENTS.md`'s 118-of-132 story is a count that could
not tell an absent runner from a silent one, and a table cell left empty is that same count with
better typography.

### The check's sentence is `CheckLine`, unchanged; only the two numbers are new

`CommandsTab.CheckLine` draws how a check ended, whether that ending is still about this commit, and
the last line the command printed — with the tone rule that goes with them. **#291 exported it and
reuses it** for the Proposal step's validation row, so the precedent for a step panel drawing a
declared check already exists, and this step follows it rather than composing a second version.

Restating those five phrases would be a second vocabulary for one fact and a second copy of the
`since` rule. What this step adds is the pair the ticket asks for by name and that sentence has no
room for: **how long it took, and how long ago it finished.**

The one thing `CheckLine` deliberately draws as nothing is the check that never finished, and this is
the step that cannot afford a blank there — so the two silences are the panel's own, in the words
`folders.check.never` and `folders.check.unfinished` already carry. Three states, three sentences,
which is the shape #291's row uses too.

Only `since === 0` is a pass about this commit — the rule #147 established and #290 echoed for a
mark.

### Colour carries nothing alone

The 700 ramp is defined once in `:root` and never redefined under `.dark`: `--pc-success-700` is
5.31:1 in light and 3.34:1 in dark, `--pc-error-700` 5.36 and 3.31. **There is no themed
"success text" token in this design system**, which is a design-system decision and not this
panel's to make.

So this step draws no ink of its own for a check at all: `CheckLine` owns those tokens and the rule
that picks them, and the rest of the panel is `--muted-foreground` and `--pc-warning-700` where
`CodeStep` already uses them. **Every state is in words as well as in colour** — `exit 1` is the
text, red is the decoration, and a reader who cannot resolve the red still reads the exit code. No
fix for the ramp is invented here; it has to be decided once, across every surface using these
tokens.

### The panel is one slot of `ChangeTab.tsx`, and it is the last one

The five step slots are five separate conditionals on purpose, so that five concurrent tickets edit
five different lines. Only the `"tests"` one changes; they are not folded into a table.

**Being the last of the five has one consequence.** `Todo` — the placeholder a step drew before its
ticket landed — says in its own doc comment that *every one of these disappears in #291–#295, and
the line that renders it goes with it*. With this slot filled it has no caller, and `tsc` refuses an
unread local, so it is deleted here. A rebase that merged without a single conflict still broke the
build: exactly the case the standing policy warns about, and the reason the typecheck comes first.

Its `todo` field on the `steps` table and the five `change.step.*.todo` strings are dead with it and
are **left in place and reported**: they are #289's table and #289's block of `en.ts`, nothing draws
them, and sweeping another ticket's file for tidiness mid-wave buys no behaviour.

### `Re-run` is the screen's existing action, not a second way to run a command

`ChangeTab` already takes `onRunChecks`, which loops `gateCommands` and starts each through the one
call the Commands tab's own Run button makes. Running **one** command from inside the panel would
need a new prop threaded from `FolderDetailScreen`, and that file is another ticket's this wave.

So the panel's `Re-run` is that same action, with that same copy — `change.recheck` /
`change.rechecking`, one wording for one thing — placed where somebody reading the check rows will
reach for it. It re-runs every declared check, which is what the header's button has always done, and
the section it sits in is named for the whole set rather than for one command. That the step cannot
re-run only the suite is a debt, not a silence.

## Risks / Trade-offs

- **The name rule is exact here and not everywhere.** A repository whose tests are
  `src/tests/Modules.cs` gets a panel reading zero. Mitigated by drawing the rule on screen, and by
  the direction of the error: under-counting is visible and over-counting is not.
- **`128 passed` is a command's last printed line, and a command can print anything.** The row shows
  it verbatim and attributes it — the product has not read it. A runner that prints a progress bar
  last will put a progress bar there, which is honest and ugly.
- **The claimed denominator understates the gap** when a change annotated only some of its
  requirements. Named in the copy, recorded as a debt, and widened by #291's read later.
- **`Re-run` runs more than the row it sits beside.** One action, one wording, and a section heading
  that says "checks" rather than a command name.
- **Nothing here has frontend coverage.** The markup, the arithmetic in `testClaims.ts` and the ink are
  argued and observed, not pinned — D19 again. What is pinned is the recognition rule and the
  roll-up, in C#.

## Migration Plan

None. Two additive fields on a query nothing persists, one new pure module on each side, and one
line of an existing component. A directory with no annotation and no declared check draws two
sentences saying so, which is strictly more than the placeholder it replaces.

## Open Questions

None open — the two that were are decided above and recorded as assumptions: the denominator is what
`tasks.md` claimed (widened by #291 later), and `Re-run` is the screen's one existing run action
rather than a per-command one that would need `FolderDetailScreen`.
