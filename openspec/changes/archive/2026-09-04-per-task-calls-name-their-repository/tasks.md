## 1. The one resolution

- [x] 1.1 Add `Features/Inbox/TaskRowLookup.cs`: given a key, an optional repository and an optional
      source, it filters `TasksDbContext.Tasks` on the stored `Repository` column and on `Source`
      when either is given, and applies `GetTask`'s existing order — open before closed, then
      source, then URL — so a caller that names neither still gets the same row twice.
- [x] 1.2 Normalise the caller's repository in one place: `VendorRepository.Of(input) ?? input`,
      lower-cased, so a full issue URL and a bare `Owner/Name` are the same value as the column.
- [x] 1.3 Point `GetTask.cs` at it and delete its in-handler candidate list, keeping its 404 message
      and its response mapping untouched.

## 2. The three reads

- [x] 2.1 `GetTaskComments.cs`: `?repository=` and `?source=` on the route, both optional, onto the
      `Query`, resolved through `TaskRowLookup`. An unknown source is refused the way the writes
      refuse one.
- [x] 2.2 `GetTaskLabels.cs`: the same.
- [x] 2.3 `GetTaskAssignees.cs`: the same.

## 3. The four writes

- [x] 3.1 `SetTaskLabels.cs`: take `repository` in the body, **use** the `Source` already there, and
      resolve through `TaskRowLookup`. The comment at the `row.Url` call site stops being a claim
      the lookup above it contradicts.
- [x] 3.2 `SetTaskAssignees.cs`: the same.
- [x] 3.3 `SetTaskStatus.cs`: take `repository` in the body and resolve through `TaskRowLookup`,
      replacing the `(Source, Key)` filter.
- [x] 3.4 `SetTaskHold.cs`: the same.

## 4. The browser stops crossing repositories

- [x] 4.1 `shared/http/queries.ts`: `useTaskComments`, `useTaskLabels`, `useTaskAssignees` take the
      repository and the source, and carry the repository in the cache key **before** the leaf, so
      the `["tasks"]` and `["tasks","by-key",key]` prefix invalidations keep matching.
- [x] 4.2 `shared/http/queries.ts`: `useSetTaskLabels`, `useSetTaskAssignees`, `useSetTaskStatus`,
      `useSetTaskHold` send the repository in their body, and their optimistic patches and
      rollbacks address the same key shape.
- [x] 4.3 `shared/tasks/holds.ts`: `HeldTask` gains the repository, and `identity()` includes it —
      two repositories' `#272` must not be one pending release.

## 5. The callers pass what they hold

- [x] 5.1 `features/tasks/TasksScreen.tsx`: the detail panel's assignees and labels, and the board's
      move, pass the row's repository — the row carries `url`.
- [x] 5.2 `features/runs/RunScreen.tsx`: `IssueComments` takes the repository as a prop; both call
      sites pass it (the tasks panel from the row, the run tab from the worktree's remote).
- [x] 5.3 `features/folders/MergeGate.tsx:325`: `useTask` names the repository through
      `repoPath(remoteUrl)`, the way `IssueTab.tsx:67` already does.
- [x] 5.4 The five screens that release a hold pass the repository with the key and the source.

## 6. Prove it against two repositories

- [x] 6.1 Tasks functional tests: mirror two rows sharing a key and differing only in repository,
      and assert each read answers its own row, a repository the key is absent from is a 404, and a
      write lands on the named row only. Today the same test passes with one row and proves nothing.
- [x] 6.2 A test that pins the repository-less answer: two rows, no repository named, the same row
      twice — the order `GetTask` had, still holding after the move.
- [x] 6.3 A test that an issue and the pull request sharing a number are told apart by source.
- [x] 6.4 Frontend: the cache key carries the repository, and every invalidation that used to name
      the full key now names the `["tasks","by-key",key]` prefix instead, so it still reaches the
      entry it is about. **Not covered by a test, and not for want of writing one**: this repository
      has no frontend test harness — `src/frontend/package.json` declares `dev`, `build`, `preview`,
      `lint`, `typecheck` and `format:check`, and the gate runs no frontend tests — so the browser
      half rests on `tsc` and on the live check in 7.2. Adding vitest is its own change.

## 7. The gate, and the check the issue asks for

- [x] 7.1 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`
      green, with the full assembly and test count in the report.
- [x] 7.2 Against the running app with both repositories mirrored: `#272`'s comments differ per
      repository, a mismatch is a 404, and a label written naming one repository appears on that
      issue at GitHub and not on the other's. Numbers in the issue's check line, not adjectives.
- [x] 7.3 `openspec validate "per-task-calls-name-their-repository" --strict`.
