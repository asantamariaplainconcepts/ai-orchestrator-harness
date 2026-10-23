# Tasks

- [x] 1. Collect coverage in the gate's test step and merge it with ReportGenerator; write CRAP per method to `crap.tsv`
- [x] 2. Add the `crap` tripwire and run the tripwires after the gate in `issue-loop.yaml`
- [x] 3. Run Stryker.NET beside the tests over the changed files, each project in its own copy, within a budget, reported only
- [x] 4. Add `aspire do review-evidence`: the gate, the tripwires, and the `evidence/` folder with its JSON and dashboard
- [x] 5. Record a video and a last screenshot of every end-to-end test; send the narrow design captures to the evidence inside the gate
- [x] 6. Serve a worktree's `evidence/` through `IReviewEvidence`, sandboxing its HTML
- [x] 7. Merge the Change tab's Tests and Evidence steps into one Evidence step that draws the review evidence
- [x] 8. Remove per-step marks, the steps-left count and the merge precondition
- [x] 9. Retake the narrow design set for the four steps
