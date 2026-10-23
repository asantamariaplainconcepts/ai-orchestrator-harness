## Context

ADR-0005 keeps the loop's state in GitHub labels. What it does not place is everything else a change
accumulates — its change name, its pull request, its cost, the friction its phases hit. Today those
are spread over a free-text line on the issue, a gitignored file in a worktree that is deleted at
merge, and nothing.

## Decisions

**A comment, not the body.** The body is the author's and already carries the Controlled Task input
block; GitHub's issue edit is last-writer-wins with no precondition, so a script rewriting the body
would race a person editing it in the browser. A comment only the script writes has one writer.

**Facts in the comment, state in the labels.** Labels are what `gh issue list --label`, the inbox and
`LabelWatcher` filter on in one call; state inside a JSON block would need every body fetched.
The comment carries nothing a transition reads.

**A session's figure is replaced, not added.** Each push re-reads the transcript from its first
message, so the latest figure for a session contains every earlier one. Keying `sessions` by id and
overwriting makes the total a plain sum, and `collect-usage` stops needing a dedupe rule.

**Read-modify-write without a lock.** Two pushes for one issue at the same instant would lose one
phase entry. One issue rides one branch and one agent at a time, so this is accepted rather than
solved; a lost entry is visible as a missing phase, not as a wrong total.

**Failure prints, it does not fall back.** When `gh` cannot write, the script prints
`UNPUBLISHED: <reason>` and the record, and exits 1. A local fallback file would recreate the second
source this change removes.

**Prices mirror `ModelPrices.cs`.** Two tables are a copy, but the script runs where the app does not
(any runtime, any adopted repository), and a figure that disagrees with the app's Telemetry tab is
worse than a duplicated table. The comment above each says to update both.

## Risks

- Issues touched before this change have no `aio:state` comment; `collect-usage` reports them as
  "no telemetry pushed", which is true of the record it can read.
- `.telemetry/executions.jsonl` files left in old checkouts are no longer read. Their figures were
  overcounted, so nothing worth migrating is lost.
