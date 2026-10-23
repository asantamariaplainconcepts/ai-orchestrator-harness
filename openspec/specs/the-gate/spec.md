# the-gate Specification

## Purpose
One command decides whether a change is shippable, and it is the same from every checkout and in the
loop: `aspire do ci`. There is no CI service in this repository, so a claim that something passes is a
claim about a command somebody ran. Reverse-engineered from the shipped code
(`src/root/AiOrchestrator.AppHost/CISteps/`, `.aio/gate`), not negotiated ahead of it.
## Requirements
### Requirement: The gate is one command made of four stages
`aspire do ci` SHALL depend on four stages, each filled by the steps of the resources that point at it:
`ci-install` (`dotnet restore` of the solution, the tool restore, `pnpm install`), `ci-compile`
(`dotnet build`, `pnpm build`), `ci-lint` (`pnpm typecheck`, `eslint --max-warnings=0`) and `ci-test`
(the tests, coverage and mutation). Any step failing SHALL fail the command, and warnings SHALL be
errors on both sides. The frontend's build and typecheck SHALL run after the server's build, because
they generate the contract from the OpenAPI document it writes.

#### Scenario: A warning fails the gate
- **WHEN** a build emits a warning
- **THEN** `aspire do ci` fails

### Requirement: The tests are the whole solution, counted
The test step SHALL run `dotnet test --no-build` on `src/AiOrchestrator.slnx`, never a directory, and
SHALL write what it counted to `src/artifacts/ci-summary.json`: every test assembly, the tests passed,
failed and skipped. A smaller count than the last run is a subset, not a success.

#### Scenario: The printed count is the check
- **WHEN** the gate finishes
- **THEN** it names how many test assemblies it ran and how many tests passed and failed

### Requirement: Coverage and mutation are reported, never enforced
The gate SHALL leave the coverage report with the CRAP score of every method, and a mutation score from
Stryker over the `.cs` files changed since the merge-base with the trunk (`MUTATION_SINCE` overrides
it), within a 10-minute budget. Neither SHALL turn the gate red; what was not measured SHALL be
recorded as not measured.

#### Scenario: A changed project with tests
- **WHEN** a `.cs` file changed since `MUTATION_SINCE` in a project a test project references
- **THEN** Stryker mutates it in a copy of the checkout under the resolved temporary directory, and the step logs `<project>: mutation score <n>%` and writes the project's report beside `summary.json`

#### Scenario: Stryker cannot measure a project
- **WHEN** Stryker exits non-zero on a project
- **THEN** the summary records it as not measured and the gate still passes

### Requirement: The loop's gate is the same command
`.aio/gate [issue]` SHALL run `aspire do ci` on the checkout it is in and answer one JSON
line `{outcome, go, why}`: `outcome: green` and exit 0 when it passed, `red` and exit 1 when it did not. The why SHALL carry the steps, the test
assemblies and the tests counted, and the path of the full log outside the tree; a failure SHALL carry
the first failing line. The issue number SHALL be accepted and unused: what the gate tests is the branch
checked out. It SHALL start `aspire do ci` without `AIO_DONE_FILE`, the end record of the agent run it may
have been started from, so no test it runs can write that run's record (#590).

#### Scenario: A red gate in verifying
- **WHEN** a test fails on the PBI's branch
- **THEN** `gate` answers `outcome: red` with the failing step, and the engine holds the PBI with that why

#### Scenario: The gate started from inside an agent run
- **WHEN** `gate` runs with `AIO_DONE_FILE` naming a run's end record
- **THEN** `aspire do ci` starts without the variable, and the record is unchanged

