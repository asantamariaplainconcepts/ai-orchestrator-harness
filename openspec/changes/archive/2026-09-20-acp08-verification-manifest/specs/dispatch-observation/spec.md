## ADDED Requirements

### Requirement: Verification evidence projects to the issue it reviews
A passing gate's test inventory diff SHALL be published to the task's own issue as one typed
attention comment — reason `verification-summary`, the added and lost test names, both digests —
through the World comment-writer seam, once per run-and-step. The comment is a projection: the
manifest in the Run's trace is the fact a later reader or queue groups on, and the comment says
what a person skimming the issue needs to know about what the candidate's gate covered. A failed
or unknown comment write SHALL be logged and not retried within the tick.

#### Scenario: One comment per gate
- **WHEN** the same run's gate step is observed again
- **THEN** no second diff comment is published for the same run and step

#### Scenario: The comment names the novelty
- **WHEN** a candidate adds two tests and loses one
- **THEN** the comment names both additions and the loss, with the digest change
