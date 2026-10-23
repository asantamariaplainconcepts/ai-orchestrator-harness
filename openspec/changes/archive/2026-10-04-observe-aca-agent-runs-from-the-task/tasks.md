## 1. Grounding

- [x] 1.1 Register the SHA; confirm #433's surfaces and identity join as implemented; inspect PR #408's `infra/pty-demo` mechanics (resume fencing, probe-as-script, PIM activation) and name what survives promotion — do not import blindly.
      files: infra/pty-demo/backends.js, src/shared/AiOrchestrator.Infrastructure/RemoteSessions/AcaAgentSessions.cs
      — Worked from the master that carries #408 (merged before implementation started, per the
      person's decision). What survives: resume-as-the-connect-door's-own-step (a no-op on a
      running sandbox), the Data Owner role as the reason a refusal is unreachable rather than
      guessed. What did not: the resume-on-every-connection habit and the probe shell. Registered
      on the issue (#5980618985).
- [x] 1.2 Decide the run-metadata contract: does a run carry environment and session id today? If not, declare the contracts extension and its migration (module's schema ownership claimed) in the issue before writing it.
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Domain/Run.cs, src/modules/Runs/AiOrchestrator.Modules.Runs/Persistence/Migrations/*_RunNamesItsRemoteEnvironment.cs
      — `SessionId` existed (A8); the environment did not. `Run.RemoteEnvironment` added with its
      migration `RunNamesItsRemoteEnvironment`, this branch owning Runs' migrations (no other
      change active did). Declared on the issue before the migration was written.
- [x] 1.3 Define the World contract for a remote session adapter (read door / connect door) and the permissions that name them, both server-enforced.
      files: src/shared/AiOrchestrator.BuildingBlocks/World/IRemoteAgentSessions.cs
      — `IRemoteAgentSessions`: `Read` is effect-free by contract, `Connect` is the only door that
      may activate. Permissions: the read door rides `runs:read` (Member); the connect door is
      `runs:connect-remote`, named by `ConnectRemoteSession.Command` and held by Admin through the
      table's rule — Admin = all, never granted explicitly (the architecture test that taught
      this is part of the record).

## 2. The adapter

- [x] 2.1 Implement the ACA adapter behind World in Infrastructure, in its own registration block; the query door reads status and history without resume, provision or spawn.
      req: observe-a-remote-agent-session/2
      files: src/shared/AiOrchestrator.Infrastructure/RemoteSessions/AcaAgentSessions.cs, src/shared/AiOrchestrator.Infrastructure/World.cs
      — Registered in its own block in `World.cs`. Measured at the CLI boundary the adapter
      exposes: reading a stopped environment issues exactly one `sandbox list` and zero
      resumes/execs (`AcaAgentSessionsTests.Reading_a_stopped_environment_issues_no_resume`).
- [x] 2.2 The connect door: server-side authorization, explicit confirmation naming possible activation cost, resume only after both; refused/unconfirmed connections activate nothing.
      req: observe-a-remote-agent-session/2
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/UseCases/GetRemoteSession.cs, src/frontend/features/runs/RemoteSessionObservation.tsx
      — `ConnectRemoteSession` requires `runs:connect-remote` server-side; the screen offers the
      act and shows the cost sentence before the person confirms. Measured: pressing "Connect to
      the session" posts nothing until the confirmation (`RemoteSessionObservationTests` counts
      posted bodies — zero after the offer).
- [x] 2.3 Session resolution from the run's own metadata; a lost session after resume declares absence — no substitute agent, no faked continuity.
      req: observe-a-remote-agent-session/3
      files: src/shared/AiOrchestrator.Infrastructure/RemoteSessions/AcaAgentSessions.cs
      — The connect door asks the resumed environment for the session the run named; absence is
      answered as absence and no `shell` is ever invoked to fill the terminal
      (`A_session_missing_after_the_resume_is_absence_not_a_substitute`).
- [x] 2.4 The four states — disconnected, stopped, absent, finished — named with `en.ts` copy (owner block); actionable errors; no credentials or secret URLs in UI or logs.
      req: observe-a-remote-agent-session/4
      files: src/frontend/features/runs/RemoteSessionObservation.tsx, src/frontend/shared/i18n/en.ts
      — The environment's state and the session's are separate sentences — `environment
      stopped` beside `session not known` — and a refused read is a sentence in the row, not a
      blank (`A_refused_read_is_a_sentence_not_a_blank`). No URL, token or credential crosses to
      the UI; the adapter's logs name calls, never secrets.
- [x] 2.5 Persisted history readable after the environment is gone; unrecoverable parts declared.
      req: observe-a-remote-agent-session/4
      files: src/shared/AiOrchestrator.Infrastructure/RemoteSessions/AcaAgentSessions.cs
      — The reading carries `HistoryDigest` from what was persisted, and `Removed` environments
      still answer their reading with the digest they kept; nothing is recreated to answer
      (`An_environment_the_list_does_not_name_is_removed`).

## 3. Verification

- [x] 3.1 Unit/adapter tests: effect-free query door measured at the adapter boundary (test double counts resume/provision/spawn calls: zero), authorization refusals, session-loss honesty, same-key-across-repositories.
      files: src/tests/AiOrchestrator.InfrastructureTests/AcaAgentSessionsTests.cs
      — Eight tests: the zero-resume read, the running environment's session question, refusal
      as unreachable, removal, the resume-and-report connect and its failed twin, the
      session-absent-after-resume answer, and the no-session case. The same-key rule rides the
      runs table's own identity — the join was never by path here.
- [x] 3.2 Frontend tests: task detail opens the run's session; four states; stale answers dropped; no local checkout required.
      files: src/tests/AiOrchestrator.EndToEndTests/RemoteSessionObservationTests.cs
      — Four tests against the real SPA: the row exists and states are separate sentences, a
      local run draws no row, a stopped environment offers the act with its cost named and posts
      nothing until confirmed, and a refused read draws the sentence.
- [x] 3.3 Local regression: local terminal, transcript and session paths unchanged (full existing suite green).
      — The full suite green is the regression: 45 e2e (including every terminal and transcript
      flight from before), 603 tests overall, and `RunScreen`'s own behaviour untouched — the
      observation row is a line beside the header, added, not a change to any existing pane.
- [x] 3.4 Verify 390×844 both themes, keyboard reachable.
      files: src/artifacts/design/current/, src/artifacts/playwright/design/
      — NarrowSet 9/9 and the 22-image both-theme batch against this change's commit; the row's
      controls are the kit's `Button`, the control every screen's reachability already rides on.
- [x] 3.5 Run the gate over the correct tree; register SHA, counts, calls. Keep the spec delta true; `openspec validate observe-aca-agent-runs-from-the-task --strict`.
      — `aspire do ci` green: ArchitectureTests 18, EndToEndTests 45, Workspace functional 281,
      Runs functional 185, Server functional 123, shared 53 — 603 in the whole run, zero
      failures. `openspec validate observe-aca-agent-runs-from-the-task --strict` valid. The
      deltas say what shipped: the two doors, the honest resume, the four states.
- [ ] 3.6 The real Azure proof: only in an authorized environment, cost and scope explicit, resources activated and final state registered — not during propose, and never as part of this change's default verification.
      — Open by design: this tree measures the adapter at its own boundary (the double), the
      doors' authorization and the screen's confirmation. The real data-plane proof runs where
      the Data Owner role lives, on the person's explicit go — and its result is a finding for
      the issue, not a checkbox here.
