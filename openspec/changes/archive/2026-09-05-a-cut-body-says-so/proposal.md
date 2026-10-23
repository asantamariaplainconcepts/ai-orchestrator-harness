## Why

The mirror stores 4,000 characters of an issue body and two readers parse that version believing it
is whole. `SourcedText.Trim` cuts at `limit = 4_000`; `parseSeam` (#149) looks for `## Seam` and
`parseCheck` (#185) looks for `## Check` / `## El check`, and in a long issue both sections fall past
the cut. Both return `null`, and `null` in both is documented as *"this issue declares no seam"* and
*"this issue does not say how anybody would know it is done"* — which is not what happened. What
happened is that nobody read that far.

Issue: https://github.com/asantamariaplainconcepts/ai-orchestrator-harness/issues/283 — a `bug`,
`status:ready-for-proposal`, under epic
[#288](https://github.com/asantamariaplainconcepts/ai-orchestrator-harness/issues/288). No blockers.

Measured on this machine on 5 September 2026, over the 60 most recent issues of this repository
(`gh issue list --limit 60 --state all`):

| | |
| --- | --- |
| bodies longer than 4,000 characters | **30 of 60** |
| the longest body | **18,348** (#231) |
| #275 | **13,121** real, 4,001 in the mirror |
| what the column already admits | `HasMaxLength(20000)` |
| bodies that fit whole in 20,000 | **60 of 60** |

So half of this repository's own issues are read half-way, the column is not what limits, and the two
screens that depend on it have been green for months saying the opposite of what is true.

## What Changes

- `SourcedText.Trim` cuts at **20,000** rather than 4,000 — what the column already admits — and
  **answers whether it cut**, not just the text. The surrogate-pair guard the method already carries
  is unchanged and still applies at the new offset.
- `SourcedTask` carries the fact; all four vendor readers pass it through unchanged in behaviour.
- `MirroredTask` gains a `BodyTruncated` column and its migration. **This change owns the Tasks
  module's migration slot** for epic #288.
- `TaskItem` and `TaskDetail` gain an **optional** `bodyTruncated` field — absent means "not
  truncated", so every existing reader behaves exactly as it does today.
- `parseSeam` and `parseCheck` gain a **third state**, `unreadable`, distinct from `null` ("no such
  section") and from a value. One named, reusable shape — `ReadFromBody<T> = T | null | Unreadable`
  — shared by both, not a boolean per parser.
- The Seam tile draws its existing *Unknown* for `unreadable` with its own reason, and the check list
  says the body could not be read whole rather than that the issue has no check.

Not breaking: the frontend field is optional and the parsers' truncation argument defaults to
`false`, so a caller that says nothing gets today's behaviour.

## Capabilities

### New Capabilities
- `mirrored-body-completeness`: the mirror declares when it cut an issue body, that fact travels to
  every reader of the body, and a reader that was handed an incomplete body says so instead of
  answering as if the text were whole.

### Modified Capabilities

None. The behaviour the existing specs describe is unchanged: `sync-the-mirror` still mirrors,
`task-details` still reads one task, `merge-gate` still draws five conditions and its *Unknown*, and
`task-inbox` still lists. What is new is a fact none of them had — and it is a capability of its own
because it is one line drawn once and read in four places, which is exactly what a shared spec is
for.

## Impact

- `src/shared/AiOrchestrator.Infrastructure/TaskSources/SourcedText.cs` — the limit, and `Trim`
  returning whether it cut. Shared by the four vendor readers on purpose, so it is touched once.
- `src/shared/AiOrchestrator.BuildingBlocks/World/ITaskSource.cs` — `SourcedTask` carries the fact.
- `src/shared/AiOrchestrator.Infrastructure/TaskSources/{GitHub,Jira,AzureDevOps,Linear}TaskSource.cs`
  — each reads `Trim`'s new answer.
- `src/modules/Tasks/…/Domain/MirroredTask.cs`, `Persistence/TasksDbContext.cs` and one migration.
- `src/modules/Tasks/…/Features/Inbox/UseCases/{ListTasks,GetTask}.cs` — the field on the wire.
- `src/frontend/shared/http/contracts.ts` — `TaskItem` and `TaskDetail`.
- `src/frontend/features/folders/seam.ts` and `check.ts` — the third state, and the shape it is
  written in.
- `src/frontend/features/folders/MergeGate.tsx` and `IssueTab.tsx` — they draw it.
- `src/frontend/shared/i18n/en.ts` — two lines, beside each screen's own block.
- `src/tests/AiOrchestrator.InfrastructureTests/` — `Trim` at the new limit, the flag, and the
  emoji at the cut.

## Assumptions recorded

This change was written unattended, with nobody to ask. Each of these is a decision taken here and a
thing a person may overturn.

1. **The limit is 20,000, not larger and not unlimited.** `HasMaxLength(20000)` already admits it and
   the longest of the 60 most recent issues measured here is 18,348 — a margin of 1,652 characters.
   The ticket's own figures were 32 over the limit and 18,396 as the longest; re-measured today the
   set has moved to 30 and 18,348, because issues #289–#298 were created since it was written. The
   conclusion is the same one and the numbers reported are the measured ones.
2. **The truncation fact travels as an additive optional field**, `bodyTruncated?: boolean` on
   `TaskItem`/`TaskDetail`, whose absence means "not truncated". The reversible direction: nothing
   already on a screen is withdrawn, and a later slice can widen it.
3. **The third state is one shared named shape, not a per-parser boolean.** `ReadFromBody<T>` and the
   `unreadable` sentinel live beside `sectionLines` in `seam.ts`, which is already the shared home
   both parsers read from. Later tickets extend the same distinction rather than inventing a second.
4. **A section that runs to the end of a cut body is also `unreadable`.** Not only a missing section:
   a `## El check` that starts at character 19,900 would otherwise render one line of six as if it
   were the whole list. This is the same fact — "the text we were given is not all the text" — and it
   costs one boolean out of the section scan.
5. **`Trim` returns a two-field value rather than an `out` parameter.** Four callers, and a value is
   what the record they build takes; an `out bool` at four call sites is four chances to drop it.
