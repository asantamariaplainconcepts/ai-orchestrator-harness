# Design

Two decisions the reading forced, one boundary the spec already drew.

## 1. The summary is written by the step that ran the tests, not parsed from logs by the gate

`StepCommand` already drains both pipes line by line; `dotnet test` prints one `Passed!`/`Failed!`
line per assembly with explicit counts. The test step gains a line-capture callback (a list beside
the logger, not a second reader), and after a green run parses those lines into the summary:
assembly name and pass/fail/skip totals. Test **names** are not in that output — they come from
`dotnet test ... --list-tests` invoked once per summary write, on the already-built tree, inside the
same step: the tree is there, the resolution is minute, and it is exactly the inventory the
manifest's digest covers. `ci-summary.json` lands beside the solution (`artifacts/ci-summary.json`,
gitignored), written only when the whole test stage reported success — a partial write from a
half-run would be a summary lying about what ran.

Why JSON beside the solution and not structured log scraping by the gate: the gate
(`RunLauncher.Controlled` in the Server) and the pipeline (the AppHost) are different processes
with no shared transport; a file both can read is the seam. And a person opening the worktree can
read the same file the gate read — the summary is not gate-private knowledge.

## 2. Manifest verdicts are computed from coverage, not asserted by exit code

`exit 0` stays necessary and stops being sufficient. The manifest's verdict logic, in order:
no exit (the command never reported) → `inconclusive:exit-unknown`; exit ≠ 0 → `fail`; no summary
file → `inconclusive:no-summary`; summary declares fewer assemblies than the solution's test
projects (counted from the `.slnx` in the working directory — the tree itself, not a config
number) → `inconclusive:assemblies-missing`, naming the missing ones; otherwise `pass` with the
inventory attached. The counts check of `pass` covers the epic's own sentence: a command that
started, or zero tests, is not a passed gate — and a green partial run reads as exactly what it
was.

The baseline comparison runs after the verdict, and only on `pass` — a failing or inconclusive
gate has nothing to diff. Baseline absent (first run, or the merge-base's check run was never
written) → the manifest carries `baseline: absent` and stays `pass`: the gate verified today's
candidate; what it cannot do is describe novelty, and saying so in the manifest is honest without
failing work that ran full coverage. The diff, when a baseline exists, is names added and names
lost plus both digests — computed once, stored in the manifest, projected to the issue as one
comment keyed on (run, step).

## 3. Check runs as the trunk's memory, read by merge-base

`IPullRequestLookup` gains two calls: `PublishInventory(sha, inventory)` (a check run named
`test-inventory`, conclusion `success`, output carrying the JSON) and `ReadInventory(sha)` —
and the gate resolves its baseline as `ReadInventory(merge-base(candidate, origin/master))`,
computed with git in the working directory it already has. The writer is whoever completes a
verified merge (#337's effect); nothing in this change publishes inventories on its own beyond a
manual path the CLI exposes for bootstrapping the first one. GitHub check runs are append-only
per SHA — exactly the durability the migration-snapshot pattern lacks — and reading by commit
means two parallel candidates each diff against the same trunk fact without coordinating.

## What this deliberately does not decide

- **No publication of artifacts** (screens, reports beyond this comment — ACP-09).
- **No merge decision** — #337 reads these manifests; this change records them.
- **No flaky rerun policy** — a `fail` is a `fail`, as everywhere else in this loop.
