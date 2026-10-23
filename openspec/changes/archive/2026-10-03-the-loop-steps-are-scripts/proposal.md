## Why

The loop's mechanical steps were written in three places: prose a person or agent follows in
`.ai/commands/aio/`, `bash` nodes in `.harness/issue-loop.yaml`, and C# the app runs. `claim`,
`merge` and `file-overlap` already showed the shape that works: one script under `.harness/scripts/`,
named by the commands, run by the loop and by the app. Five steps were still copies:

- preparing a run's worktree — C# in `GitWorktreeManager.Ensure`, `git worktree add` in the prose;
- removing it — C# in `Remove`/`RemoveBranch`, two hand-typed commands in `sync.md`, and nobody
  dropping the claim record `refs/aio/claims/<n>`;
- moving a status label with the hold in one edit — re-described by every command;
- the hold and WIP gates of `/aio:implement` — prose asking an agent to count;
- collecting and publishing screen evidence — 25 lines of bash pasted into `implement.md`.

## What Changes

- **`prepare <issue|branch> [base]`** — the worktree, fetched trunk, resumed, merged up; refusals as
  before. `GitWorktreeManager.Ensure` runs it.
- **`cleanup <issue|branch|path> [--force] [--worktree-only|--branch-only]`** — removes the worktree
  without force unless asked, deletes the branch only when nothing is lost (every commit on a remote,
  or a merged pull request with exactly this head), and drops the claim record.
  `GitWorktreeManager.Remove` and `RemoveBranch` run it.
- **`status <issue> <state> [--hold] [--close]`** — one edit, every other `status:*` removed, the hold
  never removed, unknown labels created.
- **`gate <issue>`** — the hold, then `x-harness.wip`, naming who holds the room.
- **`evidence <issue>`** — copy, rename, publish with the SHA; its `check` field is the check line.
- `HarnessScript` runs a script for the app: the repository's copy, or the carried one written to a
  temporary file. `Squash` moves onto it.
- The commands name the scripts; `AGENTS.md` lists them; all ship with the adoption.

## Capabilities

### Modified Capabilities

- `issue-lifecycle`: the loop's deterministic steps are scripts, one per step.
- `land-a-change`: removing the local branch checks its own safety.

## Impact

- `.harness/scripts/{prepare,cleanup,status,gate,evidence}`; `HarnessScript.cs`;
  `GitWorktreeManager`, `GhPullRequestLookup.Squash`, `IWorktreeManager` docs; manifest and `.csproj`.
- `.ai/commands/aio/{implement,propose,refine,sync,waves}.md`, `set-issue-status`, `AGENTS.md`.
- `status` and `gate` are GitHub-only; the app keeps moving labels and counting WIP through its own
  task sources, which also serve Jira, Linear and Azure DevOps.
