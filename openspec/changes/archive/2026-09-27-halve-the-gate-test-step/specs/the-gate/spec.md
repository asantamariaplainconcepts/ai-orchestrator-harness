# the-gate delta: the test step runs what the build step built

## MODIFIED Requirements

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
