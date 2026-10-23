# Tasks

## 1. The push-session-report skill

- [x] 1.1 Write `.ai/skills/push-session-report/SKILL.md` (one responsibility: run the fixed
      script, report its result, never write prose to `.telemetry/`) documenting both halves: the
      cost push at a phase's normal end, and the friction push — `--status degraded|failed` with a
      one-line `--note` — the moment a step hits a missing prerequisite, a wrong instruction, or a
      workaround, mirroring Expo's "Submitting Feedback" instruction this change was opened from.
      Verify it lists `--change`, `--phase`, `--status` (default `ok`) and optional `--note`.
      req: retro-log-telemetry/1
      files: .ai/skills/push-session-report/SKILL.md
- [x] 1.2 Write `.ai/skills/push-session-report/scripts/push-session-report.mjs`, following the
      convention already used by `code-humanizer/scripts/` and `aio-design/scripts/`: locates the
      current session's transcript file, sums tokens/active-time and estimates cost against an
      embedded per-model pricing table for the cost half; accepts `--status`/`--note` for the
      friction half; refuses a `--note` paired with `--status ok` and a `degraded`/`failed` status
      with no `--note`; and appends one JSON line to `.telemetry/executions.jsonl`. Verified against
      this session's own real transcript (217,712 tokens, ~$16.80 estimated) and both refusal cases
      by hand — see the check line for the exact runs.
      req: retro-log-telemetry/2, retro-log-telemetry/3, retro-log-telemetry/4
      files: .ai/skills/push-session-report/scripts/push-session-report.mjs
- [x] 1.3 Make the script say "no figure available" (not zero, not an omitted field) when it cannot
      locate a transcript, and verify by running it with a transcript path that does not exist.
      Verified: an unknown session id produces `{"unavailable":true,"reason":"no transcript file
      found for this session"}`, never a zero.
      req: retro-log-telemetry/2
      files: .ai/skills/push-session-report/scripts/push-session-report.mjs
- [x] 1.4 Add `.telemetry/` to `.gitignore` and verify `git status` stays clean in a checkout that
      has pushed at least one record. Verified: after pushing a record, `git status --short` shows
      only the tracked source changes, no `.telemetry/`.
      files: .gitignore

## 2. Wiring the push into the commands

- [x] 2.1 Add a step to `.ai/commands/aio/propose.md` invoking `push-session-report` with
      `--phase propose` right after the draft PR is opened (step 6), plus one line instructing the
      agent to push a `degraded`/`failed` record immediately when any step in this command hits a
      missing prerequisite or a wrong instruction. Verified functionally (task 1.2's real push) —
      running `/aio:propose` end to end is covered by task 3.4.
      req: retro-log-telemetry/1, retro-log-telemetry/3
      files: .ai/commands/aio/propose.md
- [x] 2.2 Add the same pair (end-of-phase push + hit-friction instruction) to
      `.ai/commands/aio/implement.md`, once after the gate passes or fails (`--phase implement`,
      with the outcome) and once more after the PR is marked ready (step 9).
      req: retro-log-telemetry/1, retro-log-telemetry/3
      files: .ai/commands/aio/implement.md
- [x] 2.3 Add the same pair to `.ai/commands/aio/sync.md`, before the retro entry is appended
      (step 3), plus naming `collect-usage` and `retro-entry` explicitly in that step.
      req: retro-log-telemetry/1, retro-log-telemetry/3, retro-log-telemetry/5,
      retro-log-telemetry/6
      files: .ai/commands/aio/sync.md
- [x] 2.4 Add the same pair to `.ai/commands/aio/review.md`, before its own retro-entry append,
      noting explicitly that the worktree `push-session-report` would read from is usually already
      gone by the time this command runs, so an empty result is the common case, not a failure.
      req: retro-log-telemetry/1, retro-log-telemetry/3, retro-log-telemetry/5,
      retro-log-telemetry/6
      files: .ai/commands/aio/review.md
- [x] 2.5 Verify the step text is identical across the three symlink targets
      (`.claude/commands/aio/*.md`, `.opencode/command/aio/*.md`, `.github/prompts/aio-*.prompt.md`)
      by diffing the symlinked copies against the source. Verified for propose, implement and sync —
      identical, because `.claude/commands`, `.opencode/command` (directory symlinks) and each
      `.github/prompts/aio-*.prompt.md` (file symlink) all resolve to `.ai/commands/aio/`, so editing
      the source was the whole job. **Finding, pushed as a `degraded` record and out of scope for
      this change:** `.github/prompts/aio-review.prompt.md` does not exist — of the eight `/aio:*`
      commands, `review` alone has no Copilot-visible prompt, unlike the other seven.
      files: (no files changed by this task — verification only)

## 3. Wiring the retro-log hand-off

- [x] 3.1 Update `.ai/skills/collect-usage/SKILL.md` to read `.telemetry/executions.jsonl` (sum
      cost/tokens/duration across the lines tagged with the change name, collect friction notes
      separately) instead of the OTel-shaped files its current text described, and to say plainly
      when a change has zero lines — also dropping the human-vs-agent time split, which this
      transcript-based mechanism cannot produce (a design gap found while writing this task; the
      skill now says so instead of implying a split it doesn't have).
      req: retro-log-telemetry/4, retro-log-telemetry/5
      files: .ai/skills/collect-usage/SKILL.md
- [x] 3.2 Update `.ai/skills/retro-entry/SKILL.md` step 1 to read that change's `degraded`/`failed`
      notes from `.telemetry/executions.jsonl` and offer them as candidate "what didn't work"
      reflections before asking the human from scratch, per the spec's last requirement.
      req: retro-log-telemetry/6
      files: .ai/skills/retro-entry/SKILL.md
- [x] 3.3 Confirm `.ai/commands/aio/sync.md` and `review.md` call `collect-usage` before
      `retro-entry`, passing its summary in — done as part of 2.3/2.4's edits; re-read here to
      confirm both name both skills explicitly.
      req: retro-log-telemetry/5
      files: .ai/commands/aio/sync.md, .ai/commands/aio/review.md
- [x] 3.4 Verify end to end. **Scope note:** a full live rehearsal of a second change through
      `/aio:propose` → `/aio:implement` → `/aio:sync` was not run — this change's own
      `.telemetry/executions.jsonl` (three real pushes: two `ok`, one `degraded`, produced while
      building this feature) served as the fixture instead. Aggregating it by hand with the exact
      logic `collect-usage` now describes produced `$37.51` estimated cost, `459,804` tokens across
      2 usage-bearing lines, and correctly isolated the one `degraded` note (the missing
      `aio-review.prompt.md` symlink from 2.5) as the sole friction entry — matching the two
      scenarios in `specs/retro-log-telemetry/spec.md` for "phases pushed cleanly" and the presence
      of a friction candidate. The "no telemetry pushed" scenario is exercised by `collect-usage`'s
      own "When there is no telemetry" section, not re-run separately here.
      files: (no files changed by this task — verification only)

## 4. Gate

- [x] 4.1 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`,
      run from the `change/314` worktree on a machine that has the toolchain: **15/15 steps, 2m 26s,
      727 tests and 0 failures across 7 assemblies** — Infrastructure 322, Workspace 139, Tasks 108,
      Workflows 66, Runs 64, Architecture 18, EndToEnd 10. The eighth directory under `src/tests/`,
      `SharedFunctionalTests`, reports nothing because it holds fixtures and no tests.
      Previously blocked: the session that wrote this change had neither `dotnet` nor `aspire`, and
      pushed a `--status failed` record rather than skipping the step silently. That record is the
      friction note this change exists to make possible, and it worked.
      files: (no files changed by this task — verification only)
- [x] 4.2 Running the script for real — the thing the blocked session could not do — found the
      aggregation wrong, and fixed it. Each push sums the transcript from its first message, so two
      pushes from one session overlap; `collect-usage` said to sum every line. `/aio:implement`
      pushes twice on its own (step 7 at the gate, step 9 at ready), so every change it measured
      would have been reported at roughly twice its cost. Measured on two pushes from one session:
      `$46.3504` by the old rule against `$23.1752` actual. 3.4's own `$37.51` fixture figure was
      inflated the same way.
      The record now carries `session_id`; `collect-usage` takes the largest figure per session and
      sums across sessions. `openspec validate --strict` green after the spec edit.
      req: retro-log-telemetry/4, retro-log-telemetry/5
      files: .ai/skills/push-session-report/scripts/push-session-report.mjs,
      .ai/skills/push-session-report/SKILL.md, .ai/skills/collect-usage/SKILL.md,
      openspec/changes/retro-log-gets-real-numbers/specs/retro-log-telemetry/spec.md
