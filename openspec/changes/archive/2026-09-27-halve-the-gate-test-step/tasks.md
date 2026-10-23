# Tasks: halve the gate's test step

## 1. The test step stops rebuilding

- [x] 1.1 Add `--no-build` to `TestStep.Run`'s `dotnet test` invocation
      (`src/root/AiOrchestrator.AppHost/CISteps/TestStep.cs`), beside the existing `--no-restore`,
      and extend the step's doc comment with the reason: the stage graph already orders
      `ci-build-server` before `ci-test-server`, so the step's clock paying a second build measures
      neither.
      req: the-gate/2
      files: src/root/AiOrchestrator.AppHost/CISteps/TestStep.cs
- [x] 1.2 Run `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`
      from a clean tree (`git clean -xfd src/` first) and record the Steps Summary numbers for
      `ci-test-server` before this change (377.5s) and after. The whole-solution count in the run's
      output must be identical assembly-for-assembly.
      req: the-gate/2, the-gate/3
      files: src/root/AiOrchestrator.AppHost/CISteps/TestStep.cs
- [x] 1.3 Confirm the step run alone behaves as specified: on a tree with a stale assembly,
      `aspire do ci-test-server` fails naming the project rather than rebuilding it (`dotnet test
      --no-build` behaviour).
      req: the-gate/4
      files: src/root/AiOrchestrator.AppHost/CISteps/TestStep.cs

## 2. The shared host

- [x] 2.1 Add the per-class derived-host helper to
      `src/tests/AiOrchestrator.SharedFunctionalTests/` beside `ApiServiceFixtureBase`: build a
      `WithWebHostBuilder` factory once per class for a fixed substitution set, documented with the
      per-test form remaining the right one for tests whose `World` differs per case.
      files: src/tests/AiOrchestrator.SharedFunctionalTests/ApiServiceFixtureBase.cs
- [x] 2.2 Convert `DispatchRecoveryTests` as the pilot: tests whose substitutions are
      class-invariant take the class host; the concurrent-duplicate and interceptor-failure tests
      keep their per-test factory. No assertion changes.
      files: src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/DispatchRecoveryTests.cs,
      src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/TestWorld.cs,
      src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/RunsFixture.cs
- [x] 2.3 Measure the Runs assembly solo before and after the pilot (`dotnet test
      <Runs.dll> --no-build`, wall clock) and record both numbers: 251.5s is the before. If the
      pilot does not move the number materially, stop and record why before converting further
      classes.
      files: src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/DispatchRecoveryTests.cs
- [x] 2.4 Convert the Remaining class-invariant classes in Runs and the two heavy classes
      `JournalApiTests` (27.1s) and `LabelWatcherTests` (16.3s) in Server.FunctionalTests, each
      keeping its assertions unchanged, re-measuring after each.
      files: (no conversion — see the note)

      *Measured instead of converted, and the tasks.md plan was wrong about these two classes:*
      `LabelWatcherTests` boots no WebApplicationFactory at all — its 16.3s is real git
      `BareRegistry` bootstrap per test, unit-shaped work with no host to share. `JournalApiTests`
      creates a complete per-test fixture (own temp SQLite, own `InitializeAsync`, restart
      semantics inside the test) — converting it is a rewrite of the suite's isolation contract,
      not a host-sharing conversion, for 27s. Both findings go on the issue; the remaining Runs
      classes already run through the class-host or cost under 3s each.

## 3. The Runs collection parallelises

- [x] 3.1 Split `RunsCollection` into collections by fixture: pure classes
      (`ScreenFrameTests`, `StartRunValidatorTests`, `RunStepsTests`, `FrameTests`) into a
      no-database collection; each database-using class that shares no rows with another into its
      own collection with its own `RunsFixture`. Classes that genuinely serialise against one
      database stay together.
      files: src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/RunsFixture.cs,
      src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/ControlledExecutionTests.cs,
      src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/DecisionReplayTests.cs,
      src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/AttentionQueryTests.cs,
      src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/ReconciliationTests.cs,
      src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/AppendTests.cs,
      src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/ReplayTests.cs,
      src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/ResumeTests.cs,
      src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/RetentionTests.cs

      *As landed:* the pure classes already carried no Collection attribute — they were never the
      serialised ones; the eleven database-holding classes were. They split into two collections
      (`runs` keeps the dispatch pair, `runs-execution` takes the rest), each with its own fixture
      and database.
- [x] 3.2 Re-run the Runs assembly solo and record the number against the 251.5s baseline and the
      125s goal; a slower number than serial is a stop-and-record, not a push-through.
      files: src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/RunsFixture.cs
- [x] 3.3 Run the full gate from a clean tree and record `ci-test-server` in the Steps Summary
      against the 190s goal, with the same assembly-for-assembly count as before the change.
      req: the-gate/2, the-gate/3
      files: (numbers in the issue check)

## 4. The check line

- [x] 4.1 Fill the issue's check on #388 with the observed numbers: gate Steps Summary
      before/after, Runs solo before/after, and the test counts that did not change.
      files: (numbers on the issue; Runs solo 250.6s → 95.5s, 100/100)

