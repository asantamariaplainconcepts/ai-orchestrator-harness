## 1. The strip

- [x] 1.1 Rename `issue` to `change` in the fixed arm of the `Tab` union, leaving the dynamic arm (`term:`, `run:`) intact, contiguous and in place for `change/264`
- [x] 1.2 Rescope `diff` and `commands` from `both` to `checkout` — not delete the rows — and say in the table's doc comment why the checkout keeps them
- [x] 1.3 Move `chat` above `telemetry` so the worktree strip reads `Change · Runs · Files · Chat · Telemetry`, and confirm the checkout's eight are unchanged by the move
- [x] 1.4 Point `homeFor(worktree)` at `change`, and add `change` to `fillsHeight`
- [x] 1.5 Remove the `IssueTab` render site and its import; leave `IssueTab.tsx` itself unedited

## 2. The facts, in one line

- [x] 2.1 In `MergeGate.tsx`, replace the five `Condition` tiles with six `Fact` readings — base, merge, checks, tree, seam and the pull request — each a mono phrase and a tone
- [x] 2.2 Move the pull request read here: `useWorktreePullRequest` deliberately, with its three answers drawn as three
- [x] 2.3 Export `PullRequestView`'s reason-to-key mapper rather than writing a second table of six reasons
- [x] 2.4 Keep #283's third state: `unreadable` from `parseSeam` is its own sentence, distinct from `null`
- [x] 2.5 Delete `verdict()`, the two headers, the disclosure, `tilesAbove` and `useNarrowContainer` from this file — and leave `gateCommands` exactly as it is, exported, for #293
- [x] 2.6 Colour only what is not ordinary, so there is no green in the row and the words carry every fact

## 3. The tab

- [x] 3.1 New `ChangeTab.tsx`: header (issue identity, the fact line, one actions slot), rail of five, one panel slot per step, footer
- [x] 3.2 Write the five panel slots as five separate one-line branches, one per step id, so #291–#295 each replace their own line and never the same one
- [x] 3.3 Put "which steps are done" behind one function, `stepsDone()`, read by both the tab counter and the footer — the single line #290 replaces
- [x] 3.4 Mount it from `FolderDetailScreen.tsx` in place of the `MergeGate` render site, keeping `onRunChecks` looping `gateCommands`
- [x] 3.5 One optional `tone` on `TabButton`'s count; absent is exactly today's grey

## 4. The copy

- [x] 4.1 Replace the whole `gate.*` block with a `change.*` block; never run Prettier on `en.ts`
- [x] 4.2 Rename the tab label key and check no other file reads a `gate.*` key

## 5. The check the issue asked for

- [x] 5.1 Name all five old facts and say which, if any, the mono row lost
- [x] 5.2 Say what the row draws for a worktree with no pull request, no trunk and a dirty tree — the three unknowns, without inventing one and without a tick
- [x] 5.3 Prove the strip assertions are not vacuous: force a loading branch on, require the absence readings to fail, revert, confirm an empty diff
- [x] 5.4 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj` — full assembly count, zero failures
- [x] 5.5 Report the bundle before and after, in KB
- [x] 5.6 Report whether `pnpm format:check` and `dotnet format --verify-no-changes` moved
- [x] 5.7 Rebase onto `epic/288`, typecheck first, then re-run the gate in full
- [x] 5.8 Archive, sync the specs, and verify `merge-gate`'s requirement count before and after the fold
