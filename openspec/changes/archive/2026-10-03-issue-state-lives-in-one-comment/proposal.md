## Why

The harness around the `/aio:*` loop says the same thing in three places, and the copies have drifted.
Measured on `d450402b`:

| What | Written | True |
| --- | --- | --- |
| WIP cap | `implement.md`, `ship.md`, `waves.md`: `wipLimit` in `.harness/config.json` | `x-harness: { wip: 4 }` in `.harness/issue-loop.yaml`; `config.json` has no such key |
| Hold label | `read-issue`, `set-issue-status`: `holdLabel` | `labels.hold` |
| States | `set-issue-status`, `CONTRIBUTING.md`: nine, without `spec-ready` | the loop writes eight, with `spec-ready`; nothing writes `proposal-review` or `blocked` |
| Claim | `waves.md`: local `wave<n>/<issue>`; `propose.md`: `change/<issue>` pushed at step 5 | ADR-0005 and the loop's `claim` node: `change/<issue>`, pushed first |
| `done` | `sync.md` closes the issue | the loop and `ListTasks.cs` expect `status:done` |

Telemetry is the costlier drift. `push-session-report` appends to `.telemetry/executions.jsonl`
inside the worktree, which is gitignored and which `/aio:sync` deletes; `review.md` already admits
that by the time it runs there is usually nothing to read. The script also miscounts: it sums every
assistant row of the transcript, where Claude Code repeats a message's usage once per content block,
and it prices by exact model id, so `claude-opus-5-5` was never priced. `scripts/collect-corpus.mjs`
reads a `usage.totalCostUsd` field nothing writes, so every corpus cost is unknown. And the adoption
ships the skill without its script.

## What Changes

- **Telemetry moves onto the issue.** `push-session-report` keeps one comment per issue, opened by
  `<!-- aio:state v1 -->`, holding a JSON block: change, pull request, one usage figure per session
  (replaced on each push, never added) and one entry per phase with its status and note. It requires
  `--issue`. `collect-usage` reads it with one command, `issue-state.mjs <issue>`, which also totals it.
- **Usage is counted the way the app counts it**: rows merged by message and request id, keeping the
  maximum, and priced from the same longest-prefix table as `ModelPrices.cs`.
- **The labels stay the state.** The comment holds facts about the change and never which step comes
  next; `gh issue list --label` and the watcher keep filtering on labels (ADR-0005).
- **The prose matches the declaration**: the WIP cap is read from `x-harness.wip`, the hold from
  `labels.hold`, the lifecycle is the eight states the loop writes, `/aio:sync` closes at
  `status:done`, and `set-issue-status` no longer asks a person to confirm a move the command they ran
  already requested.
- **One claim**: `change/<issue>`, pushed before any work — by `/aio:propose` at step 1, and by
  `/aio:waves` when it hands out a wave.
- `collect-corpus.mjs` reads cost from the issue's comment; the adoption ships the two scripts.

## Capabilities

### Modified Capabilities

- `retro-log-telemetry`: the record lives in the issue's `aio:state` comment instead of a file in the
  worktree; usage is merged by message and priced by prefix.
- `issue-lifecycle`: the eight states are named, and the claim is pushed before any work.

## Impact

- `.ai/skills/push-session-report/` (script, new `issue-state.mjs`, SKILL), `collect-usage`,
  `set-issue-status`, `read-issue`.
- `.ai/commands/aio/{propose,implement,sync,review,ship,waves}.md`.
- `CONTRIBUTING.md`, `.harness/issue-loop.yaml` (header comment only), `docs/design/README.md`,
  `.gitignore` (comment), `scripts/collect-corpus.mjs`.
- `HarnessManifest.cs` and the Infrastructure `.csproj`: two embedded scripts.
- Not in scope: the loop's `land` node still merges with `gh pr merge` where `/aio:sync` uses the
  merge endpoint, and the `overlap` node answers `clear: false` while `file-overlap` is unbuilt —
  both are behaviour of the automatic path, and each is its own decision.
