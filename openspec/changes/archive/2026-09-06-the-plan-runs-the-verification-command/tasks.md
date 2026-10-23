## 1. The port

- [x] 1.1 `World/IDeclaredCommandLookup.cs` — the interface and `DeclaredCommandLine`, with the
      `<summary>` naming `ICheckSink` as the shape and saying why null is the only "cannot say"
- [x] 1.2 `Workspace/Features/Folders/DeclaredCommandLookup.cs` — resolves from the folder's rows,
      reusing `TerminalTarget.Where` so a plan step and the Commands tab refuse the same escapes
- [x] 1.3 `WorkspaceModule.cs` — one registration beside `ICheckSink`. **`World.cs` gains no line**

## 2. The plan step

- [x] 2.1 `World/IRunLauncher.cs` — `LaunchStep` gains one optional field at the end, the resolved
      command; absent means exactly today's behaviour
- [x] 2.2 `Agents/RunPlan.cs` — `PlanAction.RunHere` appended as **5**; a resolvable `RunsHere` step
      advances and returns it, an unresolvable one still returns `Unsupported`
- [x] 2.3 `Agents/RunLauncher.cs` — the move is applied in one place so the command's ending can ask
      the plan for the next one; the command runs through `ICommandRunner`, output into the run's
      transcript, zero advances, non-zero and null stop and say the step, the key and the code
- [x] 2.4 `Runs/.../StartRun.cs` — resolve each `RunsHere` step's line where `<n>` is substituted

## 3. The import refuses what it cannot run

- [x] 3.1 `Workflows/Domain/WorkflowStep.cs` — `WorkflowStepKind.RefusedCommand = 5` appended
- [x] 3.2 `Workflows/.../ImportWorkflows.cs` — the three-way classification, the reason on `Detail`,
      and `Refused` on the response
- [x] 3.3 `Workflows/.../WorkflowPlanReader.cs` — `RefusedCommand` is also `RunsHere`, so the run
      stops naming the key rather than typing it at the agent

## 4. The folder declares the verification command

- [x] 4.1 `World/IDeclaredCommandFile.cs` — `Verify` on `FolderCommandDefinition`, documented as the
      fifth independent flag and only ever read from the file
- [x] 4.2 `HarnessCommandFile.cs` — parse `"verify"`, defaulting to false
- [x] 4.3 `.harness/commands.json` — mark `start-the-app`, and **rewrite the first sentence**

## 5. The toggle

- [x] 5.1 `Workflows/.../WorkflowStepForCommand.cs` — one file: `GET` and `POST`
      `/api/workflows/{id:guid}/workflow-step`, `[Requires(...)]` on each request, rows then file
- [x] 5.2 `GetWorkflow.cs` — the two refusals appended to `WorkflowErrors`

## 6. The screen

- [x] 6.1 `contracts.ts` — the response shapes, appended
- [x] 6.2 `queries.ts` — `workflowStep` key, its read and its write, appended
- [x] 6.3 `en.ts` — a fresh `appStep.` block at the end
- [x] 6.4 `ChangeTab.tsx` — **one line changed**, the `"app"` slot, plus the panel component

## 7. Checks

- [x] 7.1 Tests: the plan's three endings, the seam's nulls, the import's three-way split
- [x] 7.2 Mutation proof for every absence assertion
- [x] 7.3 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`,
      with the environment live — report the number
- [x] 7.4 Both themes, and the bundle delta in KB

## 8. What the running application showed

Measured with an AppHost live in this worktree, `aspire start --isolated`, never `run`.

- [x] 8.1 **The liveness question, settled with numbers.** `aspire start` exits **0 in 6-14 s**
      having handed the AppHost to `aspire run --non-interactive … --isolated` at **ppid 1** in its
      own process group. Across five starts the frontend port was `62523 → 63356 → 63889 → 56896 →
      52430` and the dashboard `61961 → 62526 → 63358 → 63891 → 52431` — **never 5000, and never
      twice the same**. Nothing the product would hold about the ending (code `0`, the last line)
      changes when `aspire stop` runs. **Not asserting won.**
- [x] 8.2 **Check 5.** A second `start` on a live app: *"Stopping previous instance (AppHost PID
      28818, CLI PID 28411) … Running instance stopped successfully"*, 9 s, and `aspire ps` shows
      **exactly one** row for this checkout. Repeated twice more, same answer.
- [x] 8.3 **Check 7.** Two checkouts with the app up leave **two** AppHosts, on different ports,
      neither touching the other. Later in the run `aspire ps` showed **six** — one per worktree.
      Life is bounded per checkout, and the number is the number of checkouts.
- [x] 8.4 **Check 2.** Toggle on against the running app: `.harness/workflows.json` gained
      `{"run": "start-the-app", "title": "Start the app"}` and the stored plan gained
      `4 foldercommand | Start the app`, which is what `WorkflowPlanReader` reads as `RunsHere`.
      Toggle off removed both. The tracked file was restored afterwards.
- [x] 8.5 **Check 6 and 8.** `aspire do ci --apphost …` with the environment live: **11/13 steps,
      2m 15s**. The one failing step is `ci-test-server`, and its only failures are the two ledger
      D10 `PhoneTests` — this worktree's directory name needs **197px** in a 390px window. Not
      loosened.
- [x] 8.6 **Check 9.** Both themes at 1440x900, with the app live. `index` **322.92 → 326.62 kB**
      (gzip **77.43 → 78.17**); first screen **178.91 → 179.63 kB** gzip, inside the 200 kB budget.
- [x] 8.7 **The bug the observation found.** On a worktree with no run the panel drew nothing — no
      command, no button — because the command and the toggle were both resolved from the run's
      workflow. That is precisely the worktree somebody opens wanting to start the application, and
      #286 had written the same warning about the gate panel. Fixed: the command comes from the
      folder, the toggle from the plan.
