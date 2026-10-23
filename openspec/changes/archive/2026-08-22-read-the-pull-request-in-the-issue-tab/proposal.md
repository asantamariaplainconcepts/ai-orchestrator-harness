## Why

The pull request is the half of the negotiation this product cannot read. `IPullRequestLookup`
answers with an address and nothing else, so the run header's Open PR button can only send somebody
to GitHub — at the exact moment they are looking at the worktree that has to act on what the review
said. The issue's own thread was put on screen for a measured reason (351,931 characters of body
against 514,934 of comments across this repository, [`task-details`](../../specs/task-details/spec.md)),
and that reason applies unchanged to a pull request.

Two of the run header's four buttons are also answering a question about the directory rather than
about the run, and the worktree screen around them already answers it: a `SplitButton` whose main
press opens the default editor on the same path, and whose menu carries the other editors and
Finder. A worktree with three runs open as tabs draws those two buttons four times.

## What Changes

- **BREAKING** `POST /api/runs/{id:guid}/pull-request` and `OpenRunPullRequest.cs` are removed. The
  pull request stops being an action taken on a run and becomes a fact read about a worktree.
- `IPullRequestLookup` grows from `FindUrl` — one nullable string — to answering the pull request:
  number, title, state, draft, address, body and its conversation. Its current contract collapses
  "no pull request", "no CLI" and "the network said no" into one `null`, with the reason written
  down: the screen's answer to all three was the same disabled button. A tab that draws content
  makes that false, so the answer now separates *there is none* from *it could not be asked*.
- A new query on the Workspace module answers the pull request of a worktree, addressed the way
  `OpenOnDesktop` already addresses one: the folder plus the worktree path it resolves against.
- The worktree screen's Issue tab draws that pull request above the issue — number, title, state,
  link out — with its body rendered as markdown and its conversation in the same shape
  `IssueComments` already draws the issue's.
- The run header loses **VS Code**, **Finder** and **Open PR**, unconditionally: from every run, in
  both habitats (`runs/:runId` and the run tab on a worktree screen), without asking how the run
  reached the screen. It keeps the badge, the task, the path, the cost and Stop/Resume.

## Capabilities

### New Capabilities

None. Reading a branch's pull request is ground `open-a-pull-request` already covers; what changes
is what the answer carries and who asks for it.

### Modified Capabilities

- `open-a-pull-request`: every requirement changes. The lookup stops being keyed to a run and stops
  answering with an address alone; the endpoint moves from a command on a run to a query on a
  folder's worktree; and "could not ask" becomes a distinct answer from "there is none", because a
  signed-out `gh` must not read as a branch with no pull request.

`the-world-seam` names `IPullRequestLookup` in its inventory of vendor seams and keeps doing so —
the seam's shape changes, its membership does not, so there is no requirement to modify. `open-on-desktop`
is untouched: its endpoint is the same one the worktree screen's own button calls, and losing two
callers in a run header is not a change to what it guarantees.

## Impact

**Removed.** `Features/Runs/UseCases/OpenRunPullRequest.cs` and
`AiOrchestrator.Modules.Runs.FunctionalTests/OpenRunPullRequestTests.cs`; the `run.openPr` and
`run.openPrUnavailable` keys in `shared/i18n/en.ts`; the three buttons and their handlers in
`features/runs/RunScreen.tsx`.

**Changed.** `BuildingBlocks/World/IPullRequestLookup.cs` and its implementation in
`Infrastructure` — a wider `gh pr view --json`, still one process call with no state beside it.
`features/folders/IssueTab.tsx` gains the pull request and the branch to ask with, which
`FolderDetailScreen.tsx` already holds as `scope?.branch`. The three shared frontend files —
`contracts.ts`, `queries.ts`, `en.ts` — gain their own blocks, appended, per
`AGENTS.md#what-collides-when-several-agents-run-at-once`.

**Added.** One use case in `Modules/Workspace/Features/Folders/UseCases/`, `Access.FiltersToCaller`
like its neighbours, and functional tests for it covering the three answers a caller can get.

**Not in scope.** No write of any kind: no comment posted, no review submitted, no merge from here.
No cached pull request state and no badges in the rail — the state machine Orca keeps for that
(`getPRForBranch`) was declined in #114 and stays declined. Nothing in the merge gate, whose Checks
tile reads local commands (#188, #189). Not the inline review comments on diff lines: the
conversation is what `gh pr view --json comments` answers, and comments anchored to lines live under
`reviews`.
