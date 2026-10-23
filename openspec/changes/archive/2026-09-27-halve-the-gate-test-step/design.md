# Design: halve the gate's test step

## Context

#388 measured the gate: 399.9s total, 377.5s in `ci-test-server`. The distribution per assembly
(runs solo, `--no-build`) is Runs.FunctionalTests 251.5s, Server.FunctionalTests 134.0s,
InfrastructureTests 88.7s, everything else under 9s. Two structural costs dominate: the per-test
`WebApplicationFactory` boot (~2.4s floor × 100 boots in Runs), and the one serial xUnit collection
(`RunsCollection`) holding all ten Runs classes — 243s of test time that xUnit cannot parallelise
because they share one fixture, one database file and a `ResetDatabase` between tests.

The step itself also pays a rebuild: `TestStep.Run` invokes `dotnet test` with `--no-restore` but
without `--no-build`, so the step's clock includes an incremental build of the solution — which the
step graph already ran (`DependsOnSteps` includes `BuildStep(...)` and the `compile` stage,
DotNetSteps.cs:101). The inventory half of the step already runs `--no-build --list-tests`, so the
pattern is established in this file.

The fixture is deliberately real: `ApiServiceFixtureBase` boots the actual Server against a SQLite
file, no in-memory substitutes, because the two worst bugs in this codebase only appeared on a real
database (its own header, lines 13–28). That value is a constraint, not a cost to remove.

## Goals / Non-Goals

**Goals:**

- `ci-test-server` under **190s** on a clean checkout (the Steps Summary number), Runs
  assembly under **125s** solo, with **zero tests deleted** and the same printed count.
- The test step's clock contains no build.
- Runs classes that do not share mutable state run concurrently.

**Non-Goals:**

- In-memory or substituted databases; the SQLite file stays.
- Speeding EndToEndTests/Playwright or the mutation step — outside the measured clock.
- Deleting or weakening the restart tests (`Every_dispatch_state_survives_host_restart`) or the
  real-delay tests — their slowness is the price of what they prove.
- Chasing the absolute minimum after 190s; past that the step stops being the gate's bottleneck.

## Decisions

### D1 — `TestStep` passes `--no-build`

The step already depends on the build step and the whole `compile` stage, and its own inventory
phase already runs `dotnet test --no-build --list-tests`. Adding `--no-build` to the main run
reuses the same bits the gate just built, in the same configuration (no configuration argument
exists today for the same reason DotNetSteps.cs:61 gives: the gate builds what AGENTS.md tells a
person to build). Alternative rejected: sharing a common `dotnet test` argument list constant — no
other caller needs it.

One risk the step must keep honest: with `--no-build`, a stale `bin/` from a previous run on a
dirty tree could be tested. The gate's contract is a clean-checkout measure, and the pipeline
already orders build-then-test, so the build in the same run refreshes those outputs. This is the
same trust the inventory listing already extends.

### D2 — Host-per-class where the world does not demand a fresh process

The pattern in `DispatchRecoveryTests` builds a derived factory per test
(`fixture.WithWebHostBuilder` wrapping a fresh `World`). The boot is the cost, and most tests in a
class configure a single small substitution set. The design: a class-level host construction helper
that builds the derived factory **once per class** when the test's substitutions are
class-invariant, with the per-test `Host(world)` form remaining available for the tests whose
`World` genuinely differs per case (concurrent-duplicate races, interceptor-injection failure
tests).

Which classes get it is decided per class by what its tests actually substitute — the tasks name
the candidates (`DispatchRecoveryTests` first: 146.9s / 24 tests) rather than promising a
mechanical sweep. Each conversion keeps its test assertions byte-for-byte; only the host
construction moves.

The fixture itself needs one addition: today `ApiServiceFixtureBase` has no supported way to hand
out a shared derived host; the per-class helper lives beside it in SharedFunctionalTests so all
modules can use it.

### D3 — Partition the Runs collection by fixture, not by attribute

`[Collection(RunsCollection.Name)]` on all ten classes serialises them against one
`RunsFixture` — one database, `ResetDatabase` between tests. But the classes do not all share
state: `ScreenFrameTests`, `StartRunValidatorTests`, `RunStepsTests`, `FrameTests` are pure and
touch no database; `AppendTests`, `RetentionTests`, `ReconciliationTests` share the database but
not each other's rows.

The design: more than one collection, each with its own `RunsFixture` (its own SQLite file) —
xUnit runs collections in parallel by default, and `MaxParallelThreads`/default parallelism
applies. Classes that genuinely need serial access to one database stay in the same collection;
classes that don't move to their own. The `ResetDatabase` between tests stays exactly as is —
per-collection now, which is the entire point.

Alternative rejected: one collection with `[CollectionDefinition(DisableParallelization = true)]`
removed — that inverts the safety; the collection exists to serialize, and removing the attribute
without splitting the fixture races every test against one `ResetDatabase`.

Alternative rejected: a shared database with schema-level isolation per class — invented
complexity the fixture's own header warns against.

## Risks / Trade-offs

- **[A shared host leaks state between tests]** → the per-class helper is opt-in per class; tests
  whose `World` differs per case keep the per-test factory. A leak shows up as a cross-test failure
  in the same class, which is the failure mode the per-test boot was paying to avoid — so the
  conversion moves only classes whose assertions the run already makes per-test (database state is
  reset by `ResetDatabase`, not by the boot).
- **[Parallel collections contend on CPU and go slower than serial]** → Runs solo is 251s against
  Server's 134s and Infrastructure's 89s, which already run in parallel under the step; the measure
  after partitioning is the assembly's own clock, and the tasks include a before/after number.
- **[`--no-build` tests a stale assembly]** → the step graph runs build before test in the same
  invocation; the inventory phase already extends the same trust. A person running the step alone
  via `aspire do ci-test-server` after editing code gets the same behaviour `dotnet test
  --no-build` always had.
- **[xUnit collection parallelism changes test ordering assumptions]** → the tests already assume
  `ResetDatabase` isolation; ordering was never a guarantee. The reconciliation tests that wait on
  timers keep their own real delays either way.

## Migration Plan

1. `TestStep` `--no-build` lands alone first — one line, mechanically verifiable in the step's
   clock, no test changes. On any red gate it reverts independently of everything else.
2. The per-class host helper lands in SharedFunctionalTests with `DispatchRecoveryTests` converted
   as the pilot, measured before/after.
3. Collection partitioning lands last, per-partition, re-running the Runs assembly solo between
   partitions.

Rollback at each step is `git revert` of that step alone; no data, schema or product change exists
to migrate.

## Open Questions

- None blocking. The exact set of classes that move collections is decided during implementation
  from what each class's tests actually share, named in the tasks, with the measure after each
  move.
