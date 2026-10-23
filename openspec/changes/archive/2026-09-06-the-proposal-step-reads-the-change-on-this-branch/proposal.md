## Why

**The product does not know its own repository writes proposals.** Every branch here carries a change
under `openspec/changes/<name>/` — a `proposal.md`, a `design.md`, a `tasks.md` and one or more
`specs/<capability>/spec.md` — and the only way to read one from the console is the Files tab, walking
the tree by hand and knowing the path. It is literally the first thing a reviewer has to look at:
`/aio:propose` stops and sets `status:holding` **so that somebody reads that**, and the screen that
asks for it does not show it.

#289 drew the rail of five steps at `31d1f24` and left five one-line panel slots, each naming the
ticket that fills it. #290 landed the ticks at `71ed8a2`. This is the first of the five panels.

Issue: https://github.com/asantamariaplainconcepts/ai-orchestrator-harness/issues/291 — `enhancement`,
under epic
[#288](https://github.com/asantamariaplainconcepts/ai-orchestrator-harness/issues/288). Blocked by
**#289** (`31d1f24`) and **#290** (`71ed8a2`), both merged and read on this branch.

Measured on this branch at `71ed8a2`, not remembered:

| | |
| --- | --- |
| the five panel slots | `ChangeTab.tsx:452-456` — **not** 338-342, which is where they were before #290 |
| unarchived changes here | `openspec/changes/` holds `archive/` and nothing else on a clean branch |
| archived changes | **6** under `openspec/changes/archive/` |
| capability specs | **40** under `openspec/specs/` |
| a real delta, hand-counted | `2026-09-05-a-tick-expires-when-the-branch-moves/specs/review-steps-per-worktree/spec.md` — `## ADDED Requirements` with **5** `### Requirement:` headers under it, and no `## MODIFIED` section: **5 added · 0 modified** |
| its sibling delta | `specs/review-a-change-in-one-tab/spec.md` — **0 added · 1 modified** |
| its `tasks.md` | 44 lines, **17 of 17 done** |
| the file reader that already exists | `IWorkspaceProbe.Files(path)` and `.Contents(path, relative)`, both already used by `GetFolder` and `GetFile` |
| `openspec validate --strict` with no item | **exit 1**, *"Nothing to validate"* — the command a screen shows must name something |
| what a declared command already records | `FolderCommand.lastCheck` — `exitCode`, `commit`, `since`, and `summary`, *"the last line it printed, verbatim"* |
| `.harness/commands.json` today | 5 commands, 3 of them `gate: true` |
| frontend bundle before | `index-*.js` 322.92 kB raw / **77.43 kB gzip**; first screen 178.91 kB gzip of a 200.00 kB budget |

## What Changes

- **A worktree's own change is read from disk and shown in the Proposal step.** One new query,
  `GET /api/folders/{id}/openspec-change`, answering with the change directories under
  `openspec/changes/` that are not `archive/`, each with its artifacts and what each artifact weighs.
- **Nothing is chosen for the reader.** A directory with two unarchived changes shows **both**. The
  ticket's own words — *"si hay más de uno, se enseñan los dos antes que adivinar"* — and it is also
  the only honest answer: which one this branch is about is not a fact on disk.
- **A worktree with no change says so in a sentence, and it is not an error.** An empty list is a
  200, not a 404: a branch that has not written a proposal yet is an ordinary state of a branch.
- **`not present` is a state, not a gap.** A change with no `design.md` says `design.md · not present`
  rather than omitting the row — a small change legitimately has no design note, and that is a
  different fact from one being missing by accident.
- **The delta counts added and modified apart**, because that is the distinction the delta file itself
  makes with its `## ADDED` / `## MODIFIED` headers, and it is the one that says whether a change is
  widening a contract or altering it. The number under each heading is the count of
  `### Requirement:` headers below it.
- **A task list is counted from its own checkboxes** — `6 of 9 done` is `- [x]` against `- [x]` plus
  `- [ ]`, and it is reported only for a file that has any.
- **`openspec validate --strict` is a declared command, not something this step runs.** The Seam asked
  for the choice and said the first answer is cheaper; it is, and it is also the reversible one. The
  product already runs declared commands and already records how each ended, per working directory,
  including the last line it printed — so the panel shows what the terminal said by **reading a row
  that already exists**, and adds no way for a screen to start a process. This repository declares the
  command in its own `.harness/commands.json`; a project that declares none is told so.
- **No new `World` seam and no new probe method.** `IWorkspaceProbe.Files` and `.Contents` already read
  a worktree, and `GetFolder` already calls the first of them for the same directory.
- **No new permission and no new error code.** Reading a change is reading the folder:
  `workspace:read`, the permission `GetReviewSteps` and `GetFile` already require.
- **No migration, no stored row, no cache.** Everything here is read off disk on request, which is the
  argument `GetFolder` already makes about a position: a stored answer is wrong the moment anybody
  edits a file.

Not breaking on the wire: one new route, one new contract type, one new query key. No existing field
changes shape.

## Capabilities

### New Capabilities
- `read-the-change-on-a-branch`: the unarchived change directories of one working directory, each
  artifact with what it weighs, an artifact that is not there said as a state, and the declared
  validation command's own last answer shown verbatim beside them.

### Modified Capabilities
- None. A new capability rather than an extension of `folder-commands`, which #295 is also writing
  into: a new file cannot conflict with a sibling's archive, and this change adds no requirement about
  what a declared command *is* — it only reads one that has already run.

## Impact

- `src/modules/Workspace/.../Features/Folders/UseCases/GetOpenSpecChange.cs` — **new**, one file. Uses
  `WorkingRoot.Resolve`, `IWorkspaceProbe.Files` and `.Contents`; adds nothing to `FolderErrors`.
- `src/frontend/shared/http/contracts.ts` — three new interfaces, appended. Nothing existing changes.
- `src/frontend/shared/http/queries.ts` — one key (`openspecChange`) and one query hook.
- `src/frontend/features/folders/ProposalStep.tsx` — **new**, the panel itself.
- `src/frontend/features/folders/ChangeTab.tsx` — **one line**, `452`, plus its import. The other four
  slots are byte-identical.
- `src/frontend/shared/i18n/en.ts` — a new `proposal.*` block at the end.
- `.harness/commands.json` — **one entry**, appended as its own block, `openspec-validate`, no `gate`.
- `src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/` — a new test file, and `StubProbe`
  gains a settable tree so `Files` and `Contents` can answer something.

## Assumptions recorded

Written unattended, with nobody to ask. Each is a decision taken here that a person may overturn.

1. **`openspec validate` is a declared command, and the declared line is `--all` rather than the
   change's name.** A declared command is a fixed string in a file that lives on a branch; the change's
   name is different on every branch, so a named form could not be declared once. `--all --strict
   --no-interactive` is the whole-tree form, it is non-interactive, and it includes this branch's own
   change. The panel shows the declared line verbatim, so the screen never displays a command that
   would exit 1 the way a bare `openspec validate --strict` does.
2. **The entry is not `gate: true`.** Whether validation belongs in the merge verdict is a decision
   about the verdict, which #289 has just rewritten; adding a fourth gate command from here would
   change `gateCommands(detail)` for every surface that reads it. Adding the flag later is one word.
3. **The command is found by a conventional key, `openspec-validate`.** The alternative is matching on
   the command line's text, which would make the product recognise a tool by name. A key is a
   convention a project opts into, and a project that does not is told there is none.
4. **The panel does not offer to run it.** Running a declared command is `FolderDetailScreen`'s, which
   owns terminal opening and which #290 was the last ticket permitted to edit. Reading the row costs
   nothing and adds no request; a Run control is a later widening, not a withdrawal.
5. **`Open in editor` opens the working directory**, which is the desktop action that already exists —
   `OpenOnDesktop` resolves a worktree path against the folder's own worktrees and opens *that*. It
   cannot open one file, and teaching it to would be a new capability (an arbitrary path reaching a
   process spawn) with its own security argument. The button's copy says what it does.
6. **Three artifact names are a convention, not a model.** `proposal.md`, `design.md` and `tasks.md`
   are listed even when absent, so `not present` can be said; everything else in the directory is
   listed as it is found. Nothing else about OpenSpec is known here — no requirement model, no
   capability graph, no mapping of requirements to files, which is #292's.
7. **A delta section this product does not name is not counted.** `## ADDED` and `## MODIFIED` are the
   two the ticket names and the two the counts report; a `## REMOVED` or `## RENAMED` section is not
   summarised. The file is one click from the reader beside the list, which is where the whole truth is.
8. **The tree read is `IWorkspaceProbe.Files`, which caps at 2 000 entries** (`GitWorkspaceProbe.cs:29`).
   A repository large enough to exhaust that cap before `openspec/` could report no change where there
   is one. Reported to the ledger rather than fixed here: the fix is a pathspec on the probe, which is
   a change to a seam three other slices share.
