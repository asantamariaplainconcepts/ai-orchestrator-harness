# Review evidence: what the gate measured, gathered for whoever reviews the change

## Why

`aspire do ci` said whether the suite passed and nothing about how much of the change the suite
exercised, whether a test would notice the new lines being wrong, or what the browser saw. A
reviewer — a person, or an agent putting a score on the change — had to take the green line's word
for all three. And reading the change meant two steps, Tests and Evidence, that were each half of
one answer.

## What changes

- **The gate reports, and never enforces, three new measures.** Coverage from every test assembly,
  merged into one report; the CRAP score of every method; and Stryker.NET over each source file the
  change touched, run beside the tests in an isolated copy of the checkout. None of them turns the
  gate red: a floor born on a codebase that has never measured itself is a gate born red.
- **`aspire do review-evidence`** runs the gate, then the tripwires on what it measured — a new
  `crap` tripwire asks for a person when a method the change touches scores above 30 — and gathers
  everything into a gitignored `evidence/` folder at the root: `review-evidence.json` for an agent,
  `index.html` for a person, the reports, and a video and last screenshot of every end-to-end test.
- **The gate leaves the tree it tested clean.** The narrow design captures go to the evidence
  instead of over `docs/design/current/` when the gate runs them, so the tripwires can judge the
  commit afterwards.
- **The Change tab's Tests and Evidence steps are one step, Evidence**, drawing the review evidence
  beside what the declared checks said and what was attached to the issue. The rail is four steps.
- **Review steps are no longer marked.** No "Mark as read", no "N review steps left", no progress,
  and merging waits on nothing but the server: the steps are an order to read in, and the reviewer
  is trusted.

## Impact

- `src/root/AiOrchestrator.AppHost/CISteps/` — coverage, mutation and review-evidence steps.
- `.harness/tripwires/crap.sh`, `.harness/issue-loop.yaml` (tripwires after the gate).
- Workspace module: `GET /api/folders/{id}/evidence` and the files under it, through a new
  `IReviewEvidence` seam, because the workspace probe reads nothing gitignored.
- Frontend: `EvidenceStep`, `ReviewEvidencePanel`; `ChangeTab` loses the Tests step.
- `reviewed:*` labels already on issues are read by nothing; the `review-steps-per-worktree`
  capability is retired.
