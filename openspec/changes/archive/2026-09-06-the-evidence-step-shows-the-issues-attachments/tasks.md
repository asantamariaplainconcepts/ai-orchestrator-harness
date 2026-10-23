## 1. The seam: one read on `ITaskSource`

- [x] 1.1 Add `ReadAttachment(string address, CancellationToken ct = default)` to
      `BuildingBlocks/World/ITaskSource.cs`, beside the six reads already there, with the doc comment
      saying why it exists (a vendor asset is served only to a request carrying the vendor's token,
      measured 200/99,670 bytes with one and 404 without — #275) and why it is on this interface
      rather than a sibling seam (the credential is this reader's).
- [x] 1.2 Add `AttachmentRead`, `AttachmentContent` and `enum AttachmentAnswer { Read = 1, Gone = 2,
      Unreachable = 3, TooLarge = 4 }` in the same file, beside `SourcedTask`. Document why *gone*
      and *unreachable* are two answers and not one.
- [x] 1.3 Implement it in `Infrastructure/TaskSources/GitHubTaskSource.cs`: resolve the credential the
      way every other call in that file does, GET the address, map 404/410 to `Gone`, any transport
      failure or non-success to `Unreachable`, a body over the bound to `TooLarge`, and a success to
      `Read` with its content type and base64 bytes.
- [x] 1.4 Answer `Unreachable` explicitly in `AzureDevOpsTaskSource`, `JiraTaskSource` and
      `LinearTaskSource`, one line and one sentence each — not a default nobody chose.
- [x] 1.5 Implement it in `StubTaskSource` (`Modules.Tasks.FunctionalTests/TasksFixture.cs`) so a test
      can hand back each of the four answers.

## 2. The use case: bytes for one attachment of one task

- [x] 2.1 Write `Modules/Tasks/Features/Inbox/UseCases/GetTaskAttachmentBytes.cs` — one file, route
      `GET /api/tasks/{key}/attachments/bytes`, `[Requires(Access.FiltersToCaller)]`, `ErrorOr<T>`
      through `ApiResults.Problem`, following `GetTaskComments.cs` for the key/repository/source
      lookup.
- [x] 2.2 Clear the untrusted address against the row's **own mirrored body**: parse the asset
      addresses out of it and refuse anything not in that set with `Error.NotFound` — not Forbidden,
      so probing addresses teaches nothing.
- [x] 2.3 Answer `Response(AttachmentBytes? Attachment, string? Unreachable)` — the vendor's failures
      are a field on a 200, as `GetWorktreePullRequest` established, so no error code is minted.
- [x] 2.4 Functional tests in `Modules.Tasks.FunctionalTests`: bytes come back for an address the body
      names; an address the body does not name is 404; each of `Gone`, `Unreachable` and `TooLarge`
      arrives as its own word; an unknown task key is 404.

## 3. The parser: `features/folders/evidence.ts`

- [x] 3.1 Write `parseAttachments(body, truncated)` returning `ReadFromBody<Attachment[]>`, importing
      `unreadable` / `isUnreadable` / `ReadFromBody` from `seam.ts` rather than re-declaring them.
      Match markdown images, markdown links and `<img>` tags whose address is a vendor asset; the
      name is the alt text; an empty alt is still an attachment.
- [x] 3.2 Write `matchesIssue(name, issueNumber)` — `evidence-<n>-` prefix, case-insensitive, the
      separator part of the rule so `#23` does not match `evidence-235-…`.
- [x] 3.3 Write `splitAttachments(list, issueNumber)` returning `{ matched, other }` so the count and
      the two lists are decided once.
- [x] 3.4 Model provenance as a union: `{ kind: "issue"; issue: string }` and `{ kind: "run-step";
      step: string; at: string }`. Document that nothing produces the second until #278 lands, and
      that inferring it from a filename is the thing this must not do.

## 4. The panel: `features/folders/EvidencePanel.tsx`

- [x] 4.1 Draw the three reads distinctly: the list, "this issue has no attachments" **naming the
      convention**, and "the mirror holds only part of this issue" — and never the second when the
      truth is the third.
- [x] 4.2 Draw the matched count, the rule sentence, the matched rows, and the *other attachments*
      block apart.
- [x] 4.3 Each row: name, provenance in words with its own glyph, the preview, and `Open full size`.
      No state carried by colour alone; every value a token; every string through `t()`.
- [x] 4.4 Each preview fetches its own bytes through the query and renders a `data:` URL; `Gone`,
      `Unreachable` and `TooLarge` each render their own sentence in the row's place, never a broken
      image.

## 5. The wiring

- [x] 5.1 `contracts.ts` — `TaskAttachmentBytes` beside the task shapes, and `mirroredAt?: string` on
      `TaskDetail` (optional: absent means the row says only which issue it is attached to).
- [x] 5.2 `queries.ts` — `useTaskAttachmentBytes`, keyed `["tasks", "by-key", key, repository, source,
      "evidence", url]` so it sits under the prefix a task's entries already share.
- [x] 5.3 `GetTask.cs` — project `MirroredAt` into the response. Additive and optional; no migration,
      the column exists.
- [x] 5.4 `en.ts` — one fresh `evidence.` block appended at the **end**. Do not touch `// Change 133`;
      `gate.*` no longer exists.
- [x] 5.5 `ChangeTab.tsx` — replace **only** the `{shown.id === "evidence" ? <Todo … /> : null}` line.
      Leave the other four slots byte-identical and do not turn them into a table.

## 6. The gate, the themes and the evidence

- [x] 6.1 `pnpm typecheck` and `pnpm lint`, then the full
      `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`.
- [x] 6.2 Measure the bundle before and after and report the growth in KB.
- [x] 6.3 For every absence assertion, give it a positive anchor from the same render, then prove it:
      force the branch, re-run, require it to FAIL, revert, empty diff, re-run green.
- [x] 6.4 Start the AppHost and look at the panel in both themes; say plainly what was and was not
      observed.

## 7. Close out

- [x] 7.1 Rebase onto the PR branch's tip, typecheck first, then re-run the gate in full.
- [x] 7.2 `openspec archive` non-interactively, fill the new spec's `## Purpose` by hand, and
      `openspec validate --specs --strict --no-interactive`.
