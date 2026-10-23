## 1. The drain

- [x] 1.1 Filter the Runs rows in `AttentionQueue.Read` by the mirror's open task keys — a row
      whose key the open set does not carry does not become a `Row`. Keep the mirror-held half and the
      unknown-source rule exactly as they are.
      req: attention-queue/1
      files: src/root/AiOrchestrator.Server/Attention/AttentionQueue.cs
- [x] 1.2 Treat an unreadable tasks half as a stated unknown that does not drain (every Runs row
      stays), and an empty-but-readable open set as a drain.
      req: attention-queue/1
      files: src/root/AiOrchestrator.Server/Attention/AttentionQueue.cs

## 2. Tests

- [x] 2.1 Server-side functional tests of the composition: a Runs row whose task is open stays; a
      Runs row whose task is closed or missing from the mirror drains; the mirror-held row for an open
      held task still appears.
      req: attention-queue/1
      files: src/tests/AiOrchestrator.Server.FunctionalTests/AttentionQueueDrainTests.cs
- [x] 2.2 A test that says the unreadable-mirror case keeps every Runs row and states the unknown.
      req: attention-queue/1
      files: src/tests/AiOrchestrator.Server.FunctionalTests/AttentionQueueDrainTests.cs

## 3. Verification

- [x] 3.1 Run `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`
      and `openspec validate "drain-attention-queue" --strict`, and observe the queue against the real
      machine's data (19 historical failures over closed tasks → zero of those rows).
      req: attention-queue/1
      files: docs/design/current/tasks.png

> 3.1 observed: `aspire do ci` counted 929 passed / 1 failed — the failure is
> `JournalApiTests.Outcome_is_idempotent_but_cannot_rewrite_history_or_clear_unknown`, a 100 s
> HttpClient hang on GET `/api/coordination/dispatch-intents` that reproduces identically on clean
> `origin/master` and in the aspire log of 24 Sep (cli_20260924T201618), before this branch existed;
> it is unrelated to this diff (which touches `AttentionQueue.cs` and its test only). The drain's own
> five tests pass. `openspec validate --strict` passes. Reality: the machine's database carries 34
> failed runs, every one over a Closed task except #185 (Open) — the queue went from 30 rows to 3,
> photographed in the committed retake of `docs/design/current/tasks.png`.
