# Halve the gate's test step: share the host, unserialise Runs, stop rebuilding what build built

## Why

`aspire do ci` took **399.9s** on a clean checkout, and **377.5s of it was `ci-test-server`**. The
gate blocks every transition of the loop — propose, implement, sync each pay it — so six minutes of
waiting per transition is the loop's own pace. The measurement (#388) traced the cost to three
places: every functional test boots a whole `WebApplicationFactory` (~2.4s floor, 100 boots in
Runs alone), Runs.FunctionalTests runs its ten classes as one serial xUnit collection (243s of test
time that cannot parallelise), and the test step rebuilds a solution that `ci-build-server` had
already built in the same step graph, inside its own clock.

## What Changes

- **`ci-test-server` stops rebuilding.** `TestStep` passes `--no-restore` today but not
  `--no-build`; it will pass `--no-build` and depend on `ci-build-server` having produced the same
  bits — which the step graph already orders.
- **A functional-test class shares one host where its tests use the same world.** The per-test
  `Host(world)` pattern (`DispatchRecoveryTests.cs:42` builds a derived factory per test) becomes a
  per-class — or shared — host where the test's substitutions do not require a fresh process, with
  the fixture's real-SQLite value untouched.
- **The Runs collection parallelises.** The one-database-plus-`ResetDatabase` design that forces
  `[Collection(RunsCollection.Name)]` on all ten classes is partitioned so independent classes run
  concurrently — several databases, several fixtures — rather than serially.
- **No test is deleted** and none is converted away from the real database. The restart tests and
  real `Task.Delay`s keep their cost; the boot floor and the serial queue are what go.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `the-gate`: one requirement gains behaviour — the test step runs what the build step built rather
  than rebuilding it, and the step's clock stops including a build. The requirement that the gate
  runs the whole solution and prints what it counted is unchanged.

## Impact

- `src/root/AiOrchestrator.AppHost/CISteps/TestStep.cs` — `--no-build`, one line plus its
  dependency on the build step already present in the stage graph.
- `src/tests/AiOrchestrator.SharedFunctionalTests/ApiServiceFixtureBase.cs` — host-sharing support
  for derived factories; the SQLite-file-per-fixture model stays.
- `src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/` — `RunsFixture.cs` collection
  partitioning; `DispatchRecoveryTests.cs`, `ControlledExecutionTests.cs` and the other classes in
  the collection move to the partitioned fixtures.
- No product code. No World seam. No frontend.
