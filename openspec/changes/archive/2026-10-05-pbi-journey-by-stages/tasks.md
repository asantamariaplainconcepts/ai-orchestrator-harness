## 1. Backend — a read of a PBI's runs by identity

- [x] 1.1 Add the use case that reads a PBI's runs by full identity (source, repository, key), joining the run's `folderPath` to the repository the PBI's address names — served by the Runs module beside `ListRuns`, reading the same schema. One file (route, request, response, command, validator, handler, nested and `internal`), with the permission it requires declared.
      req: read-a-run/1, read-a-run/2
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/UseCases/GetRunsByPbi.cs
- [x] 1.2 Map the read's error, absence, unattributed and loading states to RFC 7807 answers through `ApiResults.Problem` and `GlobalExceptionHandler`, the way the existing runs reads do; an unattributed PBI is a named answer, not a 500.
      req: read-a-run/1
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/UseCases/GetRunsByPbi.cs
- [ ] 1.3 Verify the join against a PBI whose runs worked in several directories (one gone) and against two repositories carrying the same key — the gone directory's runs are not lost, and the other repository's runs are not offered.

## 2. Frontend — the journey spine as the detail's centre

- [x] 2.1 Converge `PbiDetailPane`, `PbiWalk`, `LoopFlow` and `JourneyWalk` on one spine component driven by `journeyFor`'s resolution, reading the declared loop (`IDeclaredLoopFile` via the existing `useFileContents` path) and the PBI's runs by identity (the read from 1.1). No second interpretation of the loop, no `PbiStatus` list standing in for the declared nodes.
      req: read-a-run/1
      files: src/frontend/features/folders/PbiSpine.tsx, src/frontend/features/folders/PbiDetailPane.tsx, src/frontend/features/pbis/PbiWalk.tsx, src/frontend/features/folders/journey.ts
- [x] 2.2 Draw the spine's node states — `reached`, `current`, `held`, `ahead` — from the Connector's position (#449) and the runs' transitions, with the runs' transitions as walked history where the label and the dispatch disagree.
      req: read-a-run/1
      files: src/frontend/features/folders/journey.ts, src/frontend/features/folders/PbiSpine.tsx
- [x] 2.3 Mark a `bash` stage the harness reached as reached with no run card under it, and a stage an agent run reached with that run's card beneath it; draw hand-typed runs (command word no node declares) off-loop beside the spine, the way `journeyFor` already draws `handRuns`.
      req: read-a-run/2
      files: src/frontend/features/folders/journey.ts, src/frontend/features/folders/PbiSpine.tsx
- [x] 2.4 Group runs under their stage by command-word correlation, chronological within the stage; distinguish unfinished from finished runs within a stage (unfinished leads), with each run opening its existing terminal or transcript.
      req: read-a-run/1
      files: src/frontend/features/folders/journey.ts, src/frontend/features/folders/PbiSpine.tsx
- [x] 2.5 Name the current stage's next permitted action or the reason for the block from the Connector's position and hold and the loop's declared next action — not re-parsed from labels; a held stage names the hold with its own words.
      req: pbi-position-connector/1
      files: src/frontend/features/folders/PbiSpine.tsx, src/frontend/shared/i18n/en.ts
- [x] 2.6 Make the launch form an action of the step the loop is on, not a form that dominates the heading; the issue body and thread are drawn beneath or beside the spine.
      files: src/frontend/features/folders/PbiDetailPane.tsx
- [ ] 2.7 Draw the PR and evidence references on the journey as links to the published object and the issue respectively, not to local paths; a reference that resolves no destination is named rather than offered as a link to nothing.

## 3. Frontend — the journey survives the worktree

- [x] 3.1 Draw the spine and its runs from the runs read by identity (1.1) and the loop declaration read from the checkout, so a PBI whose worktree is gone answers through the same path as one whose worktree is live.
      req: read-a-run/2
      files: src/frontend/features/folders/PbiSpine.tsx, src/frontend/features/pbis/PbiWalk.tsx
- [x] 3.2 Stop drawing `workingPath` and `PbiLoopCommandButton` when the worktree is gone; name the live frame of a gone process unrecoverable rather than invented.
      files: src/frontend/features/folders/PbiDetailPane.tsx, src/frontend/features/folders/PbiSpine.tsx
- [x] 3.3 Distinguish "the vendor has no data" from "there is no persisted history" in the empty states.
      req: read-a-run/1
      files: src/frontend/features/folders/PbiSpine.tsx, src/frontend/shared/i18n/en.ts

## 4. Frontend — tasks hijas only when a vendor provides them

- [x] 4.1 Draw tasks hijas only when a vendor integration provides them through the seam; synthesise none from runs, create no speculative storage. A vendor that provides none draws nothing for them.

## 5. Shared frontend files — contracts, queries, copy

- [x] 5.1 Append the journey's contract to `contracts.ts` (the runs-by-identity read's shape and the spine's node states), appended to the PBI screen's block, not reordered.
      files: src/frontend/shared/http/contracts.ts
- [x] 5.2 Append the runs-by-identity query to `queries.ts`, appended to the PBI screen's block.
      files: src/frontend/shared/http/queries.ts
- [x] 5.3 Append the journey's copy to `en.ts` (stage states, next-action, the hold, the four connection states, the closed-journey naming), grouped by the PBI screen's block and not alphabetical.
      files: src/frontend/shared/i18n/en.ts

## 6. Narrow design set — the journey spine at 390px

- [ ] 6.1 Add the PBI journey's spine as a covered narrow surface: a 390×844 light/dark pair asserting no required control ends past the right edge and the document does not scroll sideways, with the current stage's first panel row reachable by scrolling the application's own scroller.
- [ ] 6.2 Run the narrow capture where the product is available; stage captures in ignored artifacts under the `evidence-<n>-` convention, tracked design captures untouched.

## 7. Gate and check

- [x] 7.1 Run `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj` until every test assembly in `src/AiOrchestrator.slnx` passes with zero failures; record the count observed.
- [ ] 7.2 Open a PBI in progress with several runs and a failure, one without a PR, and one closed whose worktree was removed; verify the facts visible, the next-action or block reading, and the return from a transcript, in both themes and narrow width. Record what was observed in the check.
