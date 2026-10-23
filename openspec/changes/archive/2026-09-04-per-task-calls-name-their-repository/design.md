## Context

`decb2e3` made `(Source, Repository, Key)` the identity of a mirrored task and left every per-task
call resolving its row the way it did when a key was unique. That was safe while the mirror could
only hold one row per number; it stopped being safe in the same commit that made two rows possible.
19 of this repository's 24 open issues share a number with the other repository in the scope.

`GetTask` is the one call that already does this correctly, and it does it in its own handler: it
loads every candidate for the key, orders them, and picks by comparing `VendorRepository.Of(row.Url)`
against what the caller named. This change takes that rule out of `GetTask` and gives it to the
seven, rather than writing an eighth version of it.

## Goals / Non-Goals

**Goals:**

- One place decides which row a key means, read by all eight calls.
- A caller that names a repository gets that repository's row or a 404 — never the other one.
- A caller that names none keeps working and keeps getting the same answer twice.
- The frontend's caches stop crossing repositories, which the server alone cannot prevent.

**Non-Goals:**

- No schema change. `Repository` is already a column and this change must not need a migration —
  if it does, it has left its seam.
- No change to identity, reconciliation or pruning. That is `decb2e3`'s and it works.
- No change to what a key looks like. `#272` stays what the screens print and what the loop's
  commands pass around.

## Decisions

**One resolution in one file, in `Features/Inbox/` beside `SyncNudge.cs`.** Not a `.Where(…)` copied
into seven handlers: seven copies is seven chances to fix six of them, and the fact that this defect
is exactly the residue of a previous fix that reached six places out of thirteen is the argument.
`GetTask` calls it too, so the rule cannot be right in one file and stale in another — the same
reasoning `TaskIdentity` was created with, for the sync's two comparisons.

**Match on the stored `Repository` column, not on `VendorRepository.Of(row.Url)`.** `GetTask` reads
the URL because it was written before the column existed. The column is what the unique index uses,
it is folded to lower case on the way in, and matching on it translates to SQL — so the lookup stays
one indexed query instead of loading every row with that key and filtering in memory. The caller's
input is normalised the same way (`VendorRepository.Of(input) ?? input`, lower-cased), so a full
issue URL and a bare `Owner/Name` arrive as the same value. Two readings of one fact become one.

**Optional, not required.** Making the repository mandatory would refuse every existing caller,
including the ones that genuinely cannot name it, and "a caller that cannot name one still gets a
deterministic answer" is a requirement `task-inbox` already carries for `GetTask`. So the parameter
is optional everywhere, and absent it the shared resolution applies `GetTask`'s existing order —
open before closed, then source, then URL — which is moved rather than rewritten.

**The source narrows too, and the three reads gain it.** An issue and the pull request that closes
it share a repository *and* a number, and differ only by source; the ordering rule alone would hand
a pull request's comment panel the issue's thread. The two label/assignee writes already receive a
`Source` in their body and ignore it — this change uses it — and the three reads gain an optional
`?source=`, parsed the way the writes parse theirs, with the same refusal for a vendor this app does
not read. The callers have it: every inbox row carries `source`.

**The repository goes into the query cache key, before the leaf.** `["tasks", "by-key", key,
repository ?? "", "comments"]`, so the existing prefix invalidations on `["tasks"]` and
`["tasks", "by-key", key]` keep matching everything they matched before. `useTask` already keys this
way and says why: a cache keyed on the number alone serves one repository's answer to the other's
screen long after the server has stopped.

**`MergeGate` names it as well.** It reads a task by key to parse the Seam a worktree's diff is
compared against, with the folder's remote in hand — the same thing `IssueTab.tsx:67` already passes
through `repoPath(remoteUrl)`. A Seam tile comparing a diff against another repository's declaration
is a wrong answer that looks like a considered one.

## Risks / Trade-offs

**A caller names a repository the mirror does not have that key in → a 404 where it used to answer.**
That is the point of the change, and it is the behaviour `GetTask` already has. The callers being
given a repository are the ones that hold the row it came from, so the mismatch means the row moved
or the sync has not run — which is what the 404's message already says.

**The cache key shape changes, so every task-detail entry is a miss on first render after this
ships.** One refetch per open panel, once. The alternative — keeping the old shape and adding the
repository only to the request — is the bug in the browser instead of the server.

**Moving `GetTask`'s ordering could change which row a repository-less caller gets.** It must not:
the order is moved verbatim, and its own tests pin it. The one deliberate difference is where the
repository is read from — the column rather than the URL — which is the same value for every row the
sync wrote, and the column is authoritative for rows whose URL is absent.

**Seven files, one shared helper, and a change that touches five frontend screens.** It is wide and
shallow: no schema, no domain, no seam. The width is the defect's, not the fix's.

## Migration Plan

None. No schema change, no data change, no rollout order. The endpoints stay backwards compatible —
a caller that sends neither parameter behaves as it does today, deterministically.

## Open Questions

None blocking. The `Source` question — drop the ignored parameter or start using it — is decided
above in favour of using it, because two vendors may use the same key and an issue and its pull
request always do.
