## REMOVED Requirements

### Requirement: review-evidence gathers what the gate measured
**Reason**: No loop state, agent command or workflow ran `aspire do review-evidence`. Since #746, `quality` measures the change in `verifying` and publishes it on the issue, and since #758 it also asks the three questions only the tripwires asked (#758).
**Migration**: Read the `aio:quality` comment on the issue, or the Review tab. Run `.harness/aio/quality <issue>` by hand to measure again.
