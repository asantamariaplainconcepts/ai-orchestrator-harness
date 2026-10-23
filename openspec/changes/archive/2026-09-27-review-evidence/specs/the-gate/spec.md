## ADDED Requirements

### Requirement: The gate reports coverage and CRAP, and enforces neither
The test step SHALL collect coverage from every test assembly, and a step after it SHALL merge the
results into one report and write the CRAP score of every method — `c² · (1 − cov)³ + c` — to
`crap.tsv`, headed by the commit it measured. No figure SHALL fail the gate; the step SHALL fail only
when it cannot produce the report, because a report that silently stops being written is one a
review goes on trusting.

#### Scenario: A green run leaves the report
- **WHEN** `aspire do ci` passes
- **THEN** `src/artifacts/coverage/index.html` and `crap.tsv` exist, and `crap.tsv` names the commit

#### Scenario: A low figure is not a red gate
- **WHEN** coverage is low or a method scores above 30
- **THEN** the gate still passes and says so in its log

### Requirement: Mutation testing runs beside the tests, over the changed files, and never fails the gate
The gate SHALL run Stryker.NET over each source file changed since the merge-base with the trunk,
starting after `install` and running beside the build and the tests. Each project SHALL be mutated in
its own copy of the checkout, because Stryker writes mutants into `bin/` and sharing the checkout
would test the gate against a mutated assembly. Whatever it cannot measure — Stryker failing, a
budget running out, no trunk — SHALL be recorded as not measured, with the reason, and SHALL NOT fail
the gate.

#### Scenario: A change with no source files
- **WHEN** nothing but documentation changed since the base
- **THEN** the mutation summary says nothing was mutated, and costs nothing

#### Scenario: Over budget
- **WHEN** the mutation runs exceed `MUTATION_BUDGET_MINUTES`
- **THEN** they are cancelled and recorded as not measured, and the gate carries on

### Requirement: Review evidence is gathered after the gate into one folder
`aspire do review-evidence` SHALL run the gate, then `.harness/tripwires/all.sh` on what it measured,
then write a gitignored `evidence/` folder at the root of the checkout, rebuilt whole: a JSON file
for an agent, a page drawn from the same JSON for a person, and the reports and end-to-end media it
links to. Every section SHALL say whether it was measured. A tripwire asking for a person SHALL be a
finding in the evidence, not a failure of the command.

#### Scenario: The same evidence for a person and an agent
- **WHEN** the command finishes
- **THEN** `evidence/index.html` draws the JSON embedded in it, which is `evidence/review-evidence.json`

### Requirement: The gate leaves the tree it tested as it found it
Nothing the gate runs SHALL write a tracked file. The end-to-end suite's narrow design captures SHALL
go to the evidence when the gate runs them, because a tree left dirty makes the tripwires, which
judge a commit, refuse to judge anything.

#### Scenario: Tripwires after the gate
- **WHEN** `aspire do review-evidence` runs the tripwires after the gate
- **THEN** the tree is clean and each tripwire gives a verdict rather than "could not tell"
