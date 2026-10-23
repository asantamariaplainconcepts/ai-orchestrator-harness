# Proposal

## Why

`collect-usage` and `retro-entry` already assume a deterministic pipeline — something that records
what a session cost, and a check for when that recording breaks — but none of it exists in the
repository (#314). The result is silent rather than honest: 88 dated entries in
`docs/process/retro-log.md` and not one carries a cost or time figure, and nothing says telemetry is
missing either, because no command ever names the skill that would say so. What a session cost stays
true only in whichever terminal ran it, gone the moment that terminal closes.

The same silence covers something more valuable than cost: **when a command's own instructions turn
out to be wrong, missing a prerequisite, or need a workaround, nothing records that either.** This
very change hit exactly that case while it was being proposed — `openspec-propose`'s own
`SKILL.md` says `compatibility: Requires openspec CLI`, but the CLI was not installed anywhere in
this environment and no file in the repository says how to install it or under what package name;
the session had to search npm by hand to find `@fission-ai/openspec`. That is precisely the case
Expo's own skill instructions (the material #314 was opened from) name explicitly under
"Submitting Feedback": *"if you encounter errors, misleading or outdated information, report it."*
Today nothing here plays that role — the finding either gets fixed silently or is lost when the
session ends.

The first draft of this change reached for a Claude Code `SessionStart` hook and an OTel Collector.
Revised after review: a hook only fires under Claude Code, and this repository's whole workflow is
built to be tool-neutral — one command prompt, symlinked into three runtimes (`.claude/commands/`,
`.opencode/command/`, `.github/prompts/`). The same pattern this project already uses for OpenSpec
(the `/aio:*` commands wrap the CLI explicitly, in the prompt, rather than expecting a plugin) is
the fix here too: name the push as an explicit step in the command, the way the Expo docs tell an
agent to run a fixed feedback command rather than leaving it to a hook or to memory. Determinism
comes from the step naming one fixed script, not from which runtime fires it.

## What Changes

- Add a skill, `push-session-report`, whose one job is to run a fixed script that appends one line
  to `.telemetry/executions.jsonl`, carrying two independent things:
  - **What it cost** — read mechanically from the session's own transcript file (cost, tokens,
    duration), or "not available" for a runtime that keeps no such file.
  - **What went wrong, if anything** — an `--status` (`ok` / `degraded` / `failed`) and a one-line
    `--note` the calling agent supplies, for a missing prerequisite, an instruction that did not
    match reality, a gate failure, or any workaround it had to invent. `ok` needs no note; anything
    else does.
- Add explicit steps in `.ai/commands/aio/propose.md`, `implement.md`, `sync.md` and `review.md` —
  the single source symlinked into every runtime — that invoke `push-session-report` at the phase
  boundaries those commands already have (PR opened, gate passed or failed, hold applied, retro
  entry written), **and instruct the agent to push a `degraded`/`failed` record with a note the
  moment it hits friction, not only at the phase's normal end.**
- Update `retro-entry` to read that change's `degraded`/`failed` notes and offer them as candidate
  "what didn't work" reflections (a field it already asks a human for), so what an agent already hit
  and reported doesn't have to be independently remembered and retyped.
- Ignore `.telemetry/` in git — it is machine state, like `.harness/data/`, not a source file.
- Update `.ai/commands/aio/sync.md` (step 3) and `.ai/commands/aio/review.md` to name `collect-usage`
  and `retro-entry` as explicit steps, in that order, so a new retro-log entry always carries a real
  figure — read from the pushed `.telemetry/executions.jsonl` lines for that change — or an explicit
  statement that no figure was ever pushed for it.

## Capabilities

### New Capabilities
- `retro-log-telemetry`: what a command's phase cost, and what went wrong while running it, are
  pushed deterministically by an explicit step in the command itself rather than a runtime hook or
  a human's memory — reaching the retro-log entry that closes the change, or that entry saying
  plainly that no figure was pushed, rather than saying nothing.

### Modified Capabilities
(none — no existing spec's requirements change; the `/aio:*` command files and the new skill this
touches are process prompts under `.ai/`, not product code, and carry no spec of their own)

## Impact

- New: `.ai/skills/push-session-report/` (SKILL.md + its bundled script, the convention this
  repo's other skills already use — `code-humanizer/scripts/`, `aio-design/scripts/`), `.telemetry/`
  (gitignored, runtime-written).
- Changed: `.ai/commands/aio/propose.md`, `implement.md`, `sync.md`, `review.md`,
  `.ai/skills/retro-entry/SKILL.md`, `.gitignore`.
- Not touched: anything under `src/`. This is the process this repository already runs on itself,
  not the product it builds — the product's own run-cost telemetry (`worktree-telemetry`) is a
  separate, already-shipped capability and is out of scope here.
- Runtime scope: because the push is a step in the shared command prompt rather than a hook, any
  runtime following `.ai/commands/aio/*.md` triggers it the same way. What differs by runtime is
  only whether a transcript file exists to read cost numbers from — where one doesn't, the pushed
  record says so instead of a number. The `degraded`/`failed` notes are agent-authored either way,
  so they carry across runtimes without that limitation.
