# run-the-declared-loop Delta

## MODIFIED Requirements

### Requirement: Resumption checks the previous node's facts, not its label alone
A new Run SHALL resume at the node after the one whose `x-harness.status` is the issue's current
label, or after a released approval — and before resuming it SHALL verify, as a fact, that the
preceding node left what it must leave. For `implement` that fact is: the OpenSpec change exists
and its branch exists. Where the fact is missing, the Run SHALL not start and SHALL say what is
missing, in the words of #361's remedy — because a label set by hand is not proof the step ran.

A work step whose completion moves the issue onward SHALL declare the label it moves: a node that
completes work and moves no label leaves the issue where it was found, and the next Run resumes at
the same node — a loop, not a progression. The declared loop's `refine` node SHALL move
`spec-ready`, and an issue carrying it SHALL resume at `spec-ready`'s dependents — the path
`overlap`, `claim`, `propose — whose first command node seats the agent on `/aio:propose`. The
`claim` node SHALL move no label: the claim within this machine is the live Run (`LiveRunKeys`),
between checkouts the branch, and a label claiming nothing collides with the node that does the
work (`implement` moves `in-progress`).

#### Scenario: A hand-applied label does not resume the loop
- **WHEN** an issue carries `status:ready-for-implementation` set by hand, with no OpenSpec change
  and no branch
- **THEN** no Run starts for it, and the refusal names what is missing

#### Scenario: A released approval resumes the next node
- **WHEN** an approval was released and the run resumes
- **THEN** it begins at the node after the approval with its facts verified, not at the loop's start

#### Scenario: A completed refine moves the issue to spec-ready, and the next run proposes
- **WHEN** an issue's refine run ends and the run moves its label to `status:spec-ready`
- **THEN** the next Run for it resumes at `spec-ready`'s dependents and seats the agent on
  `/aio:propose`, not on `/aio:refine` again

#### Scenario: A work node that moves no label is replaced, not routed around
- **WHEN** a declared work step completes and declares no `x-harness.status`
- **THEN** the declaration is refused by review — the loop's progression is its declared statuses,
  because a step that completes silently is entered again by the next Run

#### Scenario: The claim is not a label
- **WHEN** an issue carries `status:in-progress` set by the `implement` step and no other node moves it
- **THEN** the issue resumes at `implement`'s dependents, because the live Run and the branch are the
  claim and no earlier node's label stands in their way
