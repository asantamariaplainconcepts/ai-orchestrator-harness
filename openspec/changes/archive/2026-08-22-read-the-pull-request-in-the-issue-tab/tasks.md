## 1. The seam

- [x] 1.1 Replace `FindUrl` in `BuildingBlocks/World/IPullRequestLookup.cs` with a `Find` returning
      `PullRequestResult`, and add `PullRequest`, `PullRequestResult` and `PullRequestUnreachable`
      beside it, shaped after `DesktopResult`/`DesktopFailure`. Rewrite the doc comment: the
      paragraph explaining why every failure is one `null` is the reasoning being retired, so it
      says what replaced it and why the button it was written for is gone.
- [x] 1.2 Rewrite `Infrastructure/Git/GhPullRequestLookup.cs` onto
      `gh pr list --head <branch> --state all --limit 1 --json number,title,state,isDraft,url,body,comments`,
      keeping the login-shell `PATH`, `GH_PAGER=cat`, `GIT_TERMINAL_PROMPT=0`, the both-streams-before-wait
      read and the 20-second deadline. Exit 0 with `[]` is `None`; exit 0 with a row is `Of`; every
      non-zero exit and every throw is `Unreachable`, classified where `gh`'s stderr allows and
      `Failed` carrying its words where it does not.
- [x] 1.3 Keep the address check: only an absolute `http(s)` URL survives, since the tab links out
      with it. A row whose URL fails that check is `Failed`, not `None`.
- [x] 1.4 `InfrastructureTests` for the classifier: empty array is none, a row is found, a
      non-zero exit is unreachable and never none, and an unrecognised stderr lands on `Failed`
      with the vendor's text.

## 2. The query

- [x] 2.1 Add `Modules/Workspace/Features/Folders/UseCases/GetWorktreePullRequest.cs` —
      `GET /api/folders/{id:guid}/pull-request?worktree=<path>`, `Access.FiltersToCaller`, routed and
      parameterised exactly like `GetDiff`.
- [x] 2.2 Resolve the folder and the worktree the way its neighbours do; a query naming no worktree
      or naming the checkout is the refusal that says there is no branch of its own to look up.
- [x] 2.3 Map the three seam answers to the wire: found carries the pull request, none says there is
      none and names making one as the fix, unreachable carries the vendor's own words. None is a
      success and not a 4xx — a branch before its first push is the ordinary case.
- [x] 2.4 Functional tests in `AiOrchestrator.Modules.Workspace.FunctionalTests` for all four
      answers: found, none, unreachable, and a folder the caller does not have as a 404.
- [x] 2.5 Register nothing new in `World.cs` — the seam is already registered. Confirm the
      implementation still binds after the interface change, and touch no line in that file that is
      not this block.

## 3. Removing the run's pull request

- [x] 3.1 Delete `Modules/Runs/Features/Runs/UseCases/OpenRunPullRequest.cs` and
      `AiOrchestrator.Modules.Runs.FunctionalTests/OpenRunPullRequestTests.cs`.
- [x] 3.2 Remove the reference in `Modules/Runs/Features/Runs/UseCases/GetRun.cs:402` to the error
      the deleted button raised, and check no other Runs code names the lookup.
- [x] 3.3 Confirm `ArchitectureTests` and `InfrastructureTests` still pass with Runs no longer
      depending on `IPullRequestLookup`.

## 4. The Issue tab

- [x] 4.1 Append the pull request contract to `shared/http/contracts.ts` and its query to
      `shared/http/queries.ts`, in their own block next to this screen's, appending and tidying no
      neighbour.
- [x] 4.2 Add the tab's copy to `shared/i18n/en.ts` in the `folders.issueTab.*` group — the heading,
      the four states, the empty conversation and the link out — in the house voice, and remove
      `run.openPr` and `run.openPrUnavailable`.
- [x] 4.3 Draw the pull request in `features/folders/IssueTab.tsx` **above** `IssueView`: number,
      title, state, draft, link out, then the body through `Prose` in full, then the conversation in
      the shape `IssueComments` uses, with the empty thread as one sentence. Compose the kit, tokens
      only, inline style for layout only.
- [x] 4.4 Pass the worktree's branch from `FolderDetailScreen.tsx` — it already holds `scope?.branch`
      — and draw none/unreachable as their own sentences with the issue still beside them.
- [x] 4.5 Keep the three-state rule: pending is `Loading`, and a worktree still loading never reads
      as a branch with no pull request.

## 5. The run header

- [x] 5.1 Remove the VS Code, Finder and Open PR buttons from `features/runs/RunScreen.tsx`, with
      `openTarget`, the `openPullRequest` mutation, the error span and every import left unused.
- [x] 5.2 Check the `compact` branch and the header's flex layout at a narrow width with one button
      where there were four, and update the comment above the row that says "Compact drops the
      labels, not the buttons" so it describes what is there.
- [x] 5.3 Confirm the header is unchanged in both habitats — `runs/:runId` and the run tab — and that
      `desktopReady` has no reader left.

## 6. The gate and the evidence

- [x] 6.1 Run the product and read the tab for real. Done, against this machine's own `gh`: the
      `change/235` worktree answered PR #236, `OPEN`, draft, 1,571 characters of body, 0 comments; a
      throwaway worktree on an unpushed branch answered `200` with `pullRequest` and `unreachable`
      both null; the checkout answered `409 Folder.NoBranchOfItsOwn`. **`gh` off the `PATH` was not
      driven through the product** — it is covered by the real-CLI test in `InfrastructureTests`
      (a directory that is not a checkout answers unreachable, never absence) and by the stubbed
      functional test, not by the running app.
- [x] 6.2 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`
      green, then `openspec validate "read-the-pull-request-in-the-issue-tab" --strict`.
- [~] 6.3 Narrow width checked at 390: the tab fits its column, and the clipping visible on the
      right is the whole screen's — it reproduces identically on the Runs tab, which this change does
      not touch, so it is a finding for the issue and not this diff. **The dark theme was not
      checked**: `--blink-settings=preferredColorScheme=1` renders byte-identical to light in this
      environment, and `capture.sh` only shoots light and rejects a dark shot. What is verified
      instead is the contract `design-system-compliance` states — no raw colour, no radius, no type
      literal, and nothing reading the theme: the new view uses ten `var(--…)` tokens and no hex.
- [x] 6.4 `docs/design/capture.sh current` — `project-worktree.png` is the screen this change moves,
      since the Issue tab is a worktree's default tab, and it is the only one committed. The run
      re-wrote all ten, and the other nine differ only in this machine's folder name, paths and
      counts: that is #195, and committing them would be the revert-the-other-nine mistake it
      describes. `run-390.png` and `run-terminal.png` also go stale with the header losing three
      buttons, and `capture.sh current` does not shoot a run screen — that is a line for the issue.
