## Context

`ChangeTab.tsx` landed at `31d1f24` (#289) and gained its ticks at `71ed8a2` (#290). The Proposal step
is the first of its five, and today it draws one sentence naming this ticket
(`change.step.proposal.todo`, `en.ts:1014`).

Four things on the branch decide most of this design, and all four already exist:

- **`IWorkspaceProbe.Files(path)`** returns the tracked-plus-untracked tree of one directory, already
  ordered depth-first, with directories included. `GetFolder.cs:205` already calls it for exactly the
  working directory this panel is scoped to. Its cap is `GitWorkspaceProbe.cs:29`,
  `MaxTreeEntries = 2_000`.
- **`IWorkspaceProbe.Contents(path, relativePath)`** reads one file as text and answers `null` when git
  does not list that path in that directory — *"Null is the security answer, not a missing-file
  answer"*. `GetFile` is built on that sentence, and so is this.
- **`WorkingRoot.Resolve`** (`WorkingRoot.cs:25`) is the shared rule for which directory a read is
  about: a named worktree is honoured **only when it is one of this folder's**, and an unmatched one is
  `NotFound` rather than forbidden. It does **not** fall back to the checkout the way `GetFolder` does,
  so the checkout is read by omitting the parameter — measured by #290, and it applies to every slice
  that resolves this way.
- **`FolderCommand.lastCheck`** is already on the wire inside `FolderDetail`, already scoped to the
  working directory on screen (`GetFolder.cs:214-217` filters `CheckResults` by `WorkingPath`), and
  already carries `exitCode`, `commit`, `since` and `summary` — *"the last line it printed,
  verbatim. The server does not parse what a command means."*

So there is nothing here that needs inventing, and one thing that needs deciding: whether
`openspec validate --strict` is a declared command or something this step runs.

## Goals / Non-Goals

**Goals:**

- The change on this branch opens without anybody navigating a file tree.
- Every artifact is listed with what it weighs, and an artifact that is not there is **said**.
- The delta's added and modified requirements are counted apart, and the numbers match hand-counting
  the file's own headers.
- A worktree with no change, and a worktree with two, are both ordinary answers.
- What `openspec validate` said is on screen, including when it failed, and the product did not run it
  to find out.
- No new `World` seam, no new probe method, no migration, no new permission, no new error code.

**Non-Goals:**

- **Not an OpenSpec model.** No requirement graph, no capability index, no notion of what a change
  *means*. A directory, some markdown files, some counts of lines and headers and checkboxes.
- **Not a mapping of requirements to files.** That is #292, running beside this one.
- **Not an editor.** Nothing here writes a file.
- **Not a validator.** Nothing here runs `openspec`. It reads what a run of it left behind.
- **Not the other four panels**, and not the four one-line slots beside this one at
  `ChangeTab.tsx:453-456`.

## Decisions

### The read is one query over the existing probe, and it lives in a use case

`GET /api/folders/{id:guid}/openspec-change?worktree=<path>` → `{ workingPath, directory, changes[] }`.

**Why a use case and not the frontend.** A change is four to eight files. Reading them from the browser
would be four to eight `useFileContents` calls whose count depends on the answer to the first, which
React's hook rules forbid outright. The server reads them in one pass, counts, and sends numbers.

**Why `Files` and not a new probe method.** `Files` already answers "what is in this directory", and
the Seam is explicit that no new seam is needed. Filtering its answer for `openspec/changes/<x>/…`
costs one `Where`. The alternative — an optional pathspec parameter on `IWorkspaceProbe.Files` —
would be cheaper at runtime and would remove the 2 000-entry cap from this read, but it touches an
interface two implementations and a stub share while three sibling tickets are in flight. Recorded as
a debt instead; see Risks.

**Why nothing is cached.** The same argument `GetFolder` makes about a position: a stored answer is
wrong the moment somebody edits the file, and the cost is one `ls-files` plus one read per artifact for
the one directory on screen.

### `openspec validate --strict` is a declared command — the Seam's question, answered

The Seam asked whether validation is a `.harness/commands.json` command or something this step runs,
and said *"son dos respuestas distintas y la primera es más barata"*. It is, and it is also the
reversible one.

**What the product already has.** A declared command runs in a terminal a person opened, and
`CheckResult` records, per working directory, what it exited with, which commit it was on, how far the
directory has moved since, and the last line it printed. `CommandsTab.checkLine` already turns that
into a sentence, and the Change tab's own footer already reads the same rows through
`gateCommands(detail)`. Showing validation is therefore **reading a field of a prop the panel already
receives** — zero requests, zero new seam, zero new state.

**What running it here would cost.** A second way to start a process, a second place deciding what a
command's exit code means, and a step panel reaching for `useOpenTerminal` — which `CommandsTab`'s own
shape (callbacks up, never mutations down) says not to do. It is also the irreversible direction: a
panel that spawns is hard to take back.

**Why the declared line is `--all` and not the change's name.** Measured: `openspec validate --strict`
with no item exits **1** with *"Nothing to validate"*. The correct named form is
`openspec validate <name> --strict` — but the name is different on every branch and a declared command
is one fixed string in one file. `openspec validate --all --strict --no-interactive` is the whole-tree
form, is non-interactive, and includes this branch's own change. The panel prints the declared line
**verbatim**, so what is on screen is what a person would type, and it is not a command that fails
because of how it was called.

**How the panel finds it: a conventional key.** `commands.find(c => c.key === "openspec-validate")`.
Matching on the text of the command line instead would make the product recognise a tool by its name,
which is the modelling this ticket forbids. A key is something a project opts into; a project that has
not is told *"no validation command is declared here"*, which is the same shape as every other absence
on this panel.

**And `.harness/commands.json` is a seed, not a live source.** `ImportCommands` is the only thing that
reads it, a person has to press it, and it reads **the checkout, never a worktree**. So adding the
entry to this repository's file changes nothing until somebody imports — which is why the panel's
answer for "no such command" is a first-class sentence rather than an edge case.

### `not present` is a row, not a missing row

Three names are listed whether or not they exist: `proposal.md`, `design.md`, `tasks.md`. Everything
else found under the change directory is listed as found, ordinal, after them.

This is the one piece of convention in the slice, and it is written in one place with a comment saying
it is a convention. It is what makes *"un change pequeño no lleva design note"* sayable at all: a row
with `not present` beside it is a fact; an absent row is a screen that forgot.

Against listing every conventional name including `specs/**`: a change's capabilities are not knowable
in advance, and a placeholder row for a file whose name cannot be guessed would say nothing.

### The counts are three readings of markdown, and none of them knows what a requirement is

- **`lines`** — how many lines the file has. Every artifact gets one.
- **`tasksDone` / `tasksTotal`** — `- [x]` against `- [x]` plus `- [ ]`, at the start of a line,
  allowing leading whitespace. Both `null` for a file with no checkboxes, which is how `proposal.md`
  reports no task count rather than `0 of 0`.
- **`added` / `modified`** — inside a `## ADDED …` or `## MODIFIED …` section, the number of `###`
  headers. Both `null` for a file with no such section.

**Null and zero are different, and it matters here.** `0 added · 1 modified` is a real delta that
modifies one requirement; `null` is a file that is not a delta at all. A single number could not tell
`proposal.md` from a delta that happens to add nothing.

**The `^` anchor is load-bearing.** A `### Requirement:` quoted inside a fenced block would otherwise
be counted — the same trap the baseline count of `openspec/specs/` documents. Fenced blocks are
skipped by tracking ``` runs, which is what `check.ts:45`'s `FENCE` already does for the issue body.

### The panel: a list on the left, the chosen artifact rendered on the right

`ProposalStep.tsx`, its own file, so `ChangeTab.tsx` gains **one line** and its import. The reader is
`Prose` (`shared/ui/markdown.tsx`), which is `lazy()` behind a boundary precisely so that 47.54 kB gzip
of remark is not in the chunk every screen waits for — a static `react-markdown` import here would put
it back and blow the 200 kB first-screen budget.

The contents come from `useFileContents`, the hook `FilesTab` already uses, against the artifact's own
repo-relative path. `GetFile` clears that path through git, so this panel never reasons about traversal.

Two changes shown at once are two sections stacked, each with its own artifact list, each labelled with
its directory name. No selection between them, and no "primary".

**Colour carries nothing alone.** Following #289's and #290's measurement — the raw `--pc-*` ramp is
defined once in `:root` and is the same in both themes, and `--pc-success-700` is 3.34:1 in dark —
every state on this panel is words first. A failed validation says *"failed"*; the tone beside it is a
second signal, not the signal.

## Risks / Trade-offs

- **[The tree cap]** `Files` stops at 2 000 entries in ordinal order. A repository with more than that
  before `openspec/` would report no change where there is one — an absence that lies, which is exactly
  what this ticket says an absence must not be. → This repository lists **749**, so it is not reachable
  here. Reported to the ledger; the fix is an optional pathspec on `IWorkspaceProbe.Files`, which is a
  shared seam and not this ticket's to move while three siblings are in flight.
- **[A stale validation]** The declared command's row can be older than the working directory's commit.
  → `lastCheck.since` already answers that in three states, and the panel says which — the same
  sentence `CommandsTab.checkLine` and `changeStep.*` already use. A pass recorded three commits ago is
  drawn as a pass on an older commit, never as a pass.
- **[Two changes, and the reader picks wrong]** Showing both is the honest answer, but it is also more
  to read. → Both are labelled with their directory name, and neither is styled as the answer.
- **[`--all` validates more than this change]** A failure could come from a spec this branch never
  touched. → That is what `openspec` said, which is what the step promises to show, and the command
  line is printed verbatim so the reader can see what was asked.
- **[The declared command may not be imported]** `.harness/commands.json` is a seed a person presses.
  → The absent case is a sentence, not a blank.

## Migration Plan

None. No schema, no stored row, no persisted shape. Removing the change removes one route, one file and
one line of `ChangeTab.tsx`, and the `.harness/commands.json` entry is inert on its own.

## Open Questions

- Should validation be a **gate** command, so a change that does not validate cannot be merged? Not
  decided here: #289 has just rewritten what the verdict means and #296 owns the merge action. Adding
  `"gate": true` to the declared entry is one word when somebody wants it.
- Should `Open in editor` be able to open **one artifact** rather than the working directory? It would
  need `OpenOnDesktop` to accept a file path, which is a larger capability than any button on this
  screen has today. Left alone; the copy says what the button does.
