# Why

`MergeChange` squash-merges blind: `Squash(path, number)` with no expected head, no grant check, no typed outcomes — a PR whose head changed after approval merges anyway, because nothing in the path knows what was approved. The grant of #332 records exactly the SHA a person approved, and nothing consults it. The prompt's `/aio:sync` runs its own merge narrative in prose; if its session dies mid-close, nobody — including a restarted agent — knows what already landed.

# What Changes

- **`Squash` takes the expected head.** `IPullRequestMerge.Squash(path, number, expectedHead)`: the PR's tracked head is read before the request (diverge → `candidate-changed(observed)`, nothing merged), and the merge request itself is GitHub's, whose response can be lost — confirmation is by observation, exactly as #329's dispatch: after an uncertain squash, the PR is re-read; `MERGED` plus the recorded head means it landed, and a second merge never happens.
- **The merge gate is the grant.** `MergeChange` grows the admission: resolve the task from the worktree, read its live grant from the registry (`Grants(task, now)` of #332), and require the worktree's PR head — observed before the request — to equal the grant's candidate. Grant missing/expired/revoked or candidate changed: a typed refusal naming the remedy (re-approve or re-sync), cero merge. The UI's button and the prompt's `/aio:sync` travel the same call with the same preconditions — one door, as ACP-07 put it.
- **The close is an idempotent effect in the journal.** `MergeChange` records a `merge` operation through the Coordination module's journal (namespace extension of #329's dispatch intents): effectId derived from task+`merge`, so a retry re-enters and resumes; the response carries what landed and what is still pending, already the shape of the queue today (`published`, `published+BranchNotRemoved`, ...)
- **Branch protection rule noted, not coded:** where the gate cannot verify branch protection, it says so — the typed refusal carries the vendor's own words.

# Capabilities

### Modified Capabilities

- `merge-gate`: mechanical preconditions now include the grant's content binding.
- `controlled-workflow-authority`: the approval's consumption — what a grant authorizes and what merging without one refuses.

# Impact

- **Code:** `IPullRequestMerge.Squash` + `GhPullRequestLookup` (expected head, observation-confirm), `MergeChange` (grant admission + typed outcomes + journal entry), `PromptForRun`/frontend merge-button flow unchanged (same endpoint, now safer), `.ai/commands/aio/sync.md` rewritten to consume the endpoint for the mechanical half.
- **Specs:** deltas for the two named capabilities.
- **No migration** beyond what the Coordination journal already carries (operation vocabulary: `merge`).
- **Not in scope:** publishing evidence (#336's comment stays), LLM-judged merges, branch-protection overrides, `ship` as global approval.
