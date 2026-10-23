## 1. Declaration and contracts

- [x] 1.1 Preserve controlled execution across declaration, import, comparison, export and resolution; add migration and round-trip coverage. req: Controlled execution survives the declaration round trip; files: HarnessWorkflowFile.cs, Workflow.cs, WorkflowImportTests.cs.
- [x] 1.2 Define result, candidate, plan snapshot and evidence contracts with strict bounded validation. req: Controlled Agent steps require a correlated result; files: IControlledWorkflow.cs, ControlledWorkflow.cs, ControlledWorkflowTests.cs.

## 2. Execution and evidence

- [x] 2.1 Implement the pure controlled decision engine and refusal cases. req: Gates verify the current clean candidate; files: ControlledPlan.cs, ControlledPlanTests.cs.
- [x] 2.2 Integrate Agent results and declared commands without changing legacy dispatch. req: Controlled plan shape is explicit; files: StartRun.cs, RunLauncher.Controlled.cs.
- [x] 2.3 Persist evidence before dispatch and expose the stored plan and trace. req: Control evidence precedes dispatch; files: ControlEvent.cs, ControlEvidenceSink.cs, GetRunControl.cs.
- [x] 2.4 Preserve Hold/failure endings and refuse unsafe resume or command input. req: A Hold or interruption cannot silently downgrade control; files: Run.cs, ResumeRun.cs, ControlRun.cs, RunReconciliation.cs.

## 3. Inspection and adoption

- [x] 3.1 Show the stored execution graph and evidence in the Run view. req: The stored definition and trace explain the Run; files: RunControlView.tsx, RunScreen.tsx, contracts.ts, queries.ts, en.ts.
- [x] 3.2 Provide a reviewed opt-in plan and document its result contract and limits. req: Controlled plan shape is explicit; files: .harness/workflows.json, .harness/commands.json, .ai/commands/aio/candidate.md, docs/controlled-workflows.md.

## 4. Verification

- [x] 4.1 Cover missing, malformed, stale, duplicate and changed-candidate results with focused tests. req: Controlled Agent steps require a correlated result, Control evidence precedes dispatch; files: ControlledWorkflowTests.cs, ControlledPlanTests.cs, ControlledExecutionTests.cs.
- [x] 4.2 Run the full local gate and observe a real controlled Run and refusal path. req: Gates verify the current clean candidate; files: ControlledExecutionTests.cs, AppShell.tsx. Real OpenCode Run: 4 durable events, gate exit 0, unchanged clean commit, terminal Hold. Real missing-result and manual-stop paths dispatched 0 gates. Full local gate: 775 tests passed, 0 failed, 15 pipeline steps succeeded. Long Folder headings now wrap in a growing header; both previously failing mobile checks pass without changing their assertions.
- [x] 4.3 Check both themes and narrow-width inspection; clean up task-owned runtime resources. req: The stored definition and trace explain the Run; files: RunControlView.tsx. At 390 px, document width was 390 px and graph/trace right edges were 357 px in both themes; keyboard focus used the shared 3 px ring. Temporary Folder and Workflow rows were removed, and the isolated AppHost was stopped. The design skill now follows DESIGN.md, and its legacy validator delegates to the complete local gate rather than the retired token generator.
