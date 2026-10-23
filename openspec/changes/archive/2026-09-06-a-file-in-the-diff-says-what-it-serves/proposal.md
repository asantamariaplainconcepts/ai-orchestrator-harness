## Why

A diff says how much changed and never why. `DiffTab` lists files with their `+N −M`, and finding out
whether `src/worktree/pull-request.ts` has anything to do with what the change promised means reading
the spec delta in one tab and the file in another and crossing them in your head.

`tasks.md` is the closest thing this repository has to that crossing, and it does not do it: its tasks
describe work. They do not name the requirement they serve or the files they moved.

Reviewing a 25-file diff without knowing which files are the change and which are the scaffolding is
what makes people approve by skimming. Issue #292, epic #288, step 2 of five.

## What Changes

- **`/aio:implement` annotates.** On closing a task in `tasks.md` it writes, on continuation lines
  under that task, which requirements of the delta it touched and which files it moved:

  ```
  - [x] 1.2 Read the pull request in one call
        req: issue-tab/1, issue-tab/2
        files: src/worktree/pull-request.ts
  ```

  The prompt lives in `.ai/commands/aio/implement.md`, the single source symlinked into the three
  runtimes. `ship.md` inherits it by reference and does not repeat it.

- **The server reads it back.** A new query answers, for one working directory, which files of that
  directory's diff an annotation covers and which requirements were claimed for each — plus the
  annotated paths **no file in the diff matched**, which is the annotation going stale out loud
  rather than quietly.

- **The Code step draws it.** Step 2 of the Change tab becomes a file list where a row reads
  `pull-request.ts  [req 1, 2]  +118 −42`, the first file is open with its hunk, and the rest is
  behind `Show N more files`. `DiffTab`'s file row gains the same label.

- **An unlabelled file stays visibly unlabelled.** This is a claim an agent wrote, not a fact derived
  from the paths, and the screen says so: a file nobody annotated gets no label rather than an
  invented one, and that absence is itself information — nobody declared why it is in the diff.

**Not** in this change: verifying the claim (it is prose a person reviews, like the rest of
`tasks.md`), mapping tests to requirements (#293, which consumes this same annotation), and anything
about how the diff is computed.

## Capabilities

### New Capabilities
- `annotate-what-a-file-serves`: the annotation an agent writes into `tasks.md`, how a working
  directory is read for it, how it is matched against that directory's diff, and how a file row says
  what it serves — including what is said when nothing was annotated at all.

### Modified Capabilities

None. Nothing that exists changes its behaviour: the annotation is new prose in a prompt, the query
is new, and both the Code panel and the file row are additive — a working directory with no
annotations draws exactly what it drew before.

## Impact

- `.ai/commands/aio/implement.md` — the annotation instruction, single source for all three runtimes.
- `src/modules/Workspace/.../UseCases/GetAnnotations.cs` — `GET /api/folders/{id:guid}/annotations`,
  `[Requires(WorkspacePermissions.Read)]`, no new permission and no new error code.
- `src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/` — the parse, the match, the
  no-annotations case and the two refusals.
- `src/frontend/shared/http/contracts.ts`, `queries.ts`, `shared/i18n/en.ts` — appended blocks only.
- `src/frontend/features/folders/annotations.ts` (new), `CodeStep.tsx` (new), one line of
  `ChangeTab.tsx`, the file row of `DiffTab.tsx`.
- No migration, no new `World` seam, no new enum value.
