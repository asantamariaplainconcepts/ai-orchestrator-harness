# the-gate Specification

## Purpose
One command that decides whether a change is shippable, and it is the same one from every checkout:
`aspire do ci`. There is no CI service in this repository, so a claim that something passes is a claim about a
command somebody ran on this machine. Reverse-engineered from the shipped code, not negotiated ahead of it.
## Requirements
### Requirement: The gate is one command declared as four empty stages
`CiPipeline` SHALL declare `ci` and the four stages it is made of — `ci-install`, `ci-compile`, `ci-lint`,
`ci-test` — and the stages SHALL be **empty on purpose**: each is a name a resource's own step points at with
`RequiredBySteps`, which keeps the gate's contents beside the resource that produces them. Adding a resource
with a test step SHALL make `ci` run it with nothing in the pipeline file to update.

#### Scenario: A new resource's step joins the gate by pointing at a stage
- **WHEN** a resource declares a step required by `ci-test`
- **THEN** `aspire do ci` runs it without the pipeline declaration changing

#### Scenario: The graph can be printed and a single step run alone
- **WHEN** somebody needs one stage rather than the gate
- **THEN** the step names are guessable from the outside — `ci-<verb>-<resource>` — and one can be run alone

### Requirement: The gate runs the whole solution, and a smaller count is a subset rather than a success

The `ci` test step SHALL run the tests of the solution rather than of a directory, because `dotnet test`
without the solution ran a subset and reported **118 tests as a full run**, missing the 132 that included the
infrastructure and architecture tests — the two that police the module boundaries. The run SHALL print what it
counted.

The test step SHALL run the assemblies the pipeline's own build step produced — `--no-build` against the
solution — rather than rebuilding them, because the stage graph already orders `compile` before `test` and the
step's clock paying two builds measures neither. A step run alone through `aspire do ci-test-server` on a tree
whose build is stale SHALL fail as `dotnet test --no-build` fails, rather than silently rebuilding: the
gate is one command, and its parts are not a second, quieter gate.

#### Scenario: The printed count is the check
- **WHEN** the gate finishes
- **THEN** it names every test assembly it ran and its failure count, and a smaller count reads as a subset

#### Scenario: The test step's clock contains no build
- **WHEN** `ci-test-server` runs after the pipeline's build step has built the solution
- **THEN** the tests execute against those assemblies without an incremental rebuild inside the step's clock

#### Scenario: A stale tree fails rather than rebuilding quietly
- **WHEN** the test step is run alone and the assemblies on disk do not match the sources
- **THEN** the step fails the way `dotnet test --no-build` fails, naming the project, rather than rebuilding it

### Requirement: Warnings are errors, and steps that would race are serialised
Both sides SHALL treat warnings as errors. Where two steps of the gate would race — measured as three
consecutive runs of `aspire do ci` on an unchanged tree going pass, then not — the pipeline SHALL serialise
them rather than leaving the outcome to timing.

#### Scenario: A warning fails the gate
- **WHEN** a build emits a warning
- **THEN** the gate fails

### Requirement: Starting this repository's own app is isolated and says what stops it
The declared start command for this repository's application SHALL be isolated, and SHALL say what stops it, so
an agent that starts the app can stop it again rather than leaving an AppHost behind.

#### Scenario: The declared start line is isolated
- **WHEN** the app is started through the declared command
- **THEN** it runs isolated and the declaration names its stop line

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

