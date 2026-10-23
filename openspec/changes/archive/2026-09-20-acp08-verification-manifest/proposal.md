# Why

The controlled gate today proves exit 0 and an unchanged clean candidate — and stops there. A pipeline that ran 3 of 8 test assemblies passes; nobody sees which tests existed, which ones the candidate added, or what the gate's "pass" actually covered. The grant of #332 binds approvals to a "verification manifest digest" that nothing produces, and the merge boundary of #337 will need exactly that manifest to decide. Verification that cannot say what it covered is a signature without a document.

# What Changes

- **The pipeline leaves a structured summary.** `dotnet test` steps in the CI pipeline capture their own output lines and write `ci-summary.json` beside the solution when the test stage finishes: every assembly it ran, its pass/fail/skip counts, and the full name of every test. A person can read it; the gate consumes it.
- **The gate records a `VerificationManifest`.** When a controlled step finishes (the path `RunLauncher.Controlled` already walks), it records — through the existing evidence sink — a typed manifest: run/step/task, candidate HEAD/tree/base, spec pin, resolved command, exit code, the **test inventory** (assemblies, counts, test names, digest of the name set), and a verdict: `pass` (exit 0, clean, all assemblies the summary declares), `fail`, or `inconclusive` (no summary, fewer assemblies than the pipeline declared, or a command that never reported). An assembly count read from the actual summary, never a number in a file.
- **The baseline lives in the trunk, by SHA.** The gate (or the person's sync) publishes the test inventory as a **check run** on the merged commit; the next candidate's gate reads the baseline of its merge-base and computes the diff: tests added, tests lost, digest changed. No `ci-summary.json` committed to the tree — two branches regenerating one committed file is the migration-snapshot collision pattern AGENTS.md names.
- **The diff projects to the task's issue.** One typed attention comment — "2 new tests: X, Y; baseline digest changed" — through the seam #328 defined. The manifest is the fact; the comment is the projection, as everywhere else.

# Capabilities

### Modified Capabilities

- `controlled-workflow-execution`: gates carry verification manifests; inconclusive is a first-class verdict.
- `dispatch-observation`: the publication of test-inventory diffs through the attention seam.

# Impact

- **Code:** `StepCommand`/`DotNetSteps` (capture + summary write, gated on the test stage), `RunLauncher.Controlled` (manifest recording after each gate), `VerificationManifest` record in BuildingBlocks/World, `IPullRequestLookup` (+check-run read/write seam), `GitHubAttentionCommentWriter` (the diff projection, new reason code), tests across InfrastructureTests (summary parsing) and Server.FunctionalTests.
- **Specs:** deltas for the two named capabilities.
- **No schema migration:** the manifest travels as a `ControlTraceEntry` kind plus typed JSON in the evidence the Run already persists.
- **Not in scope:** publishing visual artifacts (ACP-09); deciding the merge (ACP-10 reads these manifests); hermetic environments or adversarial mutation detection — declared limits.
