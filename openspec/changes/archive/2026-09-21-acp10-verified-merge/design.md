# Design

Three decisions, each against code that already exists.

## 1. The expected head rides the seam's own find-then-merge, not a new protocol

`Squash(path, number, expectedHead)` stays a seam method: the implementation reads the PR's
current head first (the `Fields` list grows `headRefOid`), refuses typed on divergence, and only
then asks GitHub to merge — a window remains between that read and GitHub's write, because the
vendor offers no expected-SHA parameter for squash, and closing it further is post-merge
observation: the PR re-read says `MERGED` with which head landed; an uncertain result after a
lost response is settled the same way, never by requesting again. This is the same
read-verify-observe discipline #329 built for dispatch, applied six layers up at the vendor
boundary — and it is the honest reading of what the provider actually guarantees: not atomicity,
but a queryable fact.

The `MergeChange` layer does not duplicate the check: it resolves the grant's candidate SHA and
passes it as the expected head — the worktree's observed head equality against the grant is
checked once, in the seam, where the two reads are closest together.

## 2. The grant admission lives in MergeChange, one guarded step before the merge

Order of the handler's existing guards — archived, clean, PR exists — then the new one: resolve
the worktree's task (its own branch-to-task resolution, `GetOpenSpecChange`'s neighbours), read
the registry grants for that task canonically, and require one grant whose `CandidateOid` is the
PR's observed head. Refusals are typed with remedies, not sentences: `grant-missing` (approve the
candidate: `AiOrchestrator.Control approve-candidate ...`), `grant-expired`,
`grant-revoked`, `candidate-changed` (re-approve or re-verify). The folder without a registry
pin is grandfathered: it has no grants, so no merge — the same opt-in the claims carry, and
`/aio:sync`'s prompt gains the approve-candidate step in its person-half.

Why in `MergeChange` and not a gate in the prompt: the guard must hold for the button and the
prompt alike, and the button is the path a tired person presses at 1am. One door, code-side.

## 3. The close is idempotent because its operations are already resumable — the journal only names them

What already resumes today: the `MergeChange` handler re-enters on retry (`alreadyMerged` read,
worktree row kept on removal failure). What does not: knowing that from outside the handler. The
journal entry (`operation: merge`, effectId derived from task identity + `merge`, state
pending/started/failed/unknown) wraps the effect — recorded before the merge request, completed
after the response or observation — so `/aio:sync`'s prose narrative becomes the journal's
typed facts plus the person's own steps (hold-release, archive-before-merge, the retro). The
cleanup steps after merge — stop environment, remove worktree, drop branch — retain their
existing resume semantics; the journal names what finished so "merge confirmed, cleanup pending"
is a query, not a memory.

## What this deliberately does not decide

- **No closure automation** — issue-close and milestone-close remain the person's steps in
  `/aio:sync`; the journal records the merge fact they act on.
- **No branch-protection override, no LLM judgment** — vendor refusal texts pass through, typed
  only at our own refusals.
