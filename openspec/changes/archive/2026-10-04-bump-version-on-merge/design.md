## Context

Measured at propose time, master 56ba512e.

- `src/Directory.Build.props` line 13: `<Version>0.2.2</Version>` — the single source, per the
  `read-product-version` capability. The capability's spec was folded from change
  `single-version-source` and its purpose section still reads `TBD`.
- The last tag is `v0.1.3` (27 September, the only release; `gh release list` shows exactly one).
  Five squash-merges have landed since — #431, #432, #433, #434 epic plus PR #408 — no tag, no
  release, and `0.2.2` itself was written into the props at some point without one either.
- `.harness/scripts/merge` is the one merge: squash-merge via `gh pr merge --squash
  --match-head-commit`, branch deleted at the remote, one JSON line answered —
  `{merged, observed, branchDeleted, why}`. Three callers: `/aio:sync`, the loop's `merge` node,
  the app's merge button (`GhPullRequestLookup.Squash` runs this file).
- `/aio:sync`'s close-out (`.ai/commands/aio/sync.md` steps 3-5) is the edit that carries the
  archived change and the retro entry into the squash commit. That edit is where a version bump
  belongs — riding the same tandem commit rather than a(nother) commit nobody asked for.
- No CI: the repository has no `.github/workflows`, deliberately (AGENTS.md). A release created
  by a workflow would be a second path to a shared state. The script the person already runs is
  the only path there is.

## Goals / Non-Goals

**Goals:**

- Every merge to master ends with a tag and a release that name it, produced by the same command
  that merged it — no remembering, no second ceremony.
- The tag points at a master whose `Directory.Build.props` already says the tag's number.
- The initial release of the current state — `v0.2.2` over master as it stands — comes out of
  the same script, not a one-off.
- Patch by default; minor when the PR's title is feature-shaped (`feat`/`feature` prefix).

**Non-Goals:**

- Major versions, calendars, or any schedule: the merge is the clock.
- Build artifacts attached to releases — the pkg exists, and attaching it is its own ticket.
- CI. The repository has none by decision; this adds none.
- Changing how the version is declared — MSBuild stays the single source (`read-product-version`).
- Rewriting tags. A moved tag is rewritten history; the script refuses rather than fixes.

## Decisions

- **The release rides the merge, not the sync.** `.harness/scripts/merge` calls `release` after
  its own success, because merge is the one path all three callers walk. `/aio:sync` could have
  owned it, but the loop's `merge` node and the app's button do not pass through `/aio:sync`,
  and a release only some merges produce is the status quo with extra steps.
- **The bump rides the sync close-out, not the merge script.** The version that a tag names has
  to be in the source the tag points at, and the only edit that composes with the squash-merge
  is the close-out — the same edit that carries the archived change and the retro (`sync.md`
  steps 3-4). A script mutating `Directory.Build.props` on master after the merge would create a
  second commit on master that nobody approved.
- **`release` is a script, not prose.** "What does not need judgement is a script" (AGENTS.md).
  Reading the props, computing the previous tag, gathering PR titles, creating the tag and the
  release — all deterministic, all one JSON line at the end.
- **Idempotent by naming.** An existing tag or release is answered, not touched — ADR-0005's
  line about history: a tag that moved is history rewritten.
- **Tags as shared state, created locally.** The person runs the merge; the merge runs the
  release. `gh` already holds the credentials; nothing new is granted.

## Risks / Trade-offs

- **A merge whose close-out forgot the bump.** The tag then names the previous version's number
  over new work — the script says what it found rather than inventing a number on master the
  person never approved. The failure is visible in the JSON line's `why`.
- **PR titles in two languages.** The epic's own titles are Spanish; `feat`/`feature` prefixes
  decide minor, everything else patch — a Spanish-titled feature lands a patch, which is the
  conservative direction, and the caller can override with an argument.
- **The app's button runs a copy.** `GhPullRequestLookup.Squash` runs "this file or the copy the
  app carries" — the copy must learn the release step with the next app build, and until then
  the button's merges release nothing the button's copy doesn't know how to. Said, not hidden.

## Alternatives rejected

- A GitHub workflow on push to master: a second path to shared state, and the repository has
  none by decision.
- Release-please / semantic-release: heavyweight conventions for a one-command loop that
  already knows when it merged.
- Bumping inside the merge script post-merge: creates commits on master nobody approved.

## Verification plan

1. `release --settle` over the current master produces `v0.2.2` (tag + release) or says why it
   will not; `GET /api/version` on a build of that master answers 0.2.2.
2. A small PR merged through `/aio:sync` with its close-out bump: tag exists on the squash
   commit, the release carries the PR's title, `released` names both.
3. Re-running `release` for the same version: the existing tag is named, nothing duplicated.
4. `merge`'s JSON line still parses for all three callers — the new field is additive.
