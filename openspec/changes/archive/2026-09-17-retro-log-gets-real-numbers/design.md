# Design

## Context

See proposal.md - Why. Two things `collect-usage` already assumes do not exist anywhere in this
repository or its history: something that captures what a session cost, and a persisted place it
lands that a later step can read instead of a live dashboard. A third thing surfaced during this
change's own review: nothing captures when a command's own instructions were wrong or a prerequisite
was missing — this proposal's own drafting hit that (the `openspec` CLI, undocumented) and nobody
would have known if it hadn't been mentioned in the PR by hand.

A `SessionStart` hook was the first approach considered and rejected in review (see Decisions) in
favour of a skill invoked explicitly from the `.ai/commands/aio/*.md` prompts — the same single
source already symlinked into `.claude/commands/`, `.opencode/command/` and `.github/prompts/`, so a
step written there runs under any of the three runtimes without runtime-specific registration.

## Goals / Non-Goals

**Goals:**
- A session's cost/token/time data is durable past the terminal that produced it, pushed by an
  explicit, fixed step rather than left to whoever is running the command to remember.
- A command's own friction — a missing prerequisite, an instruction that didn't match reality, a
  workaround the agent had to invent — is pushed the same way, as it happens, instead of being fixed
  silently or lost when the session ends.
- The push works the same way regardless of which runtime is executing the command prompt.
- `/aio:sync` and `/aio:review` stop being silent about telemetry: they either report a number (and
  any friction notes) or say plainly that none was pushed.

**Non-Goals:**
- A Claude Code `SessionStart` hook, an OTel Collector, or any other machinery that only one runtime
  can fire. Considered and dropped — see Decisions.
- Measuring opencode or Copilot sessions with real cost numbers. A runtime that keeps no per-session
  transcript still gets a pushed record — it just says no figure was available, same treatment as a
  Claude Code session whose transcript could not be found. Friction notes are unaffected by this,
  since the agent supplies them directly rather than reading them from a transcript.
- Automatically fixing what a friction note reports. Pushing the note is the whole job; deciding
  what to do about a pattern of notes (edit a command, file an issue) is a person's call, made later
  from the log — the same separation `collect-usage` already keeps between measuring and deciding.
- Anything under `src/`. This is `.ai/` and `.gitignore` only.

## Decisions

**A skill invoked from the command prompt, not a `SessionStart` hook.** First draft: register
`.claude/settings.json` with a hook script under `.claude/hooks/`. Rejected on review — a hook is a
Claude Code-only mechanism, and this repository's entire `/aio:*` workflow exists specifically so
one prompt works under three runtimes (AGENTS.md: "the single source, symlinked into
`.claude/commands/`, `.opencode/command/` and `.github/prompts/`, so every runtime sees the same
prompt"). A hook would have measured exactly one of those three. Naming the push as a step inside
the command prompt itself is the same pattern this project already uses for OpenSpec: `/aio:propose`
and `/aio:implement` name `openspec` commands explicitly rather than assuming a plugin is installed.
It is also the mechanism #314 pointed at directly: Expo's own skill instructions tell an agent to
run one fixed feedback command when a condition is met, rather than relying on a hook or on the
agent's memory. The determinism is in the script the step names, not in what fires it.

**One push carries both cost and friction, not two separate skills.** Considered: a second skill
just for "report a problem", mirroring Expo's separate "Submitting Feedback" section. Rejected for
this repository specifically — one append-only file, one script, one step per phase boundary is
simpler to wire into four command files than two, and a friction note naturally belongs beside the
cost of the same phase it happened in (a phase that took three times its usual cost and also logged
a `degraded` note are often the same finding). `--status` defaults to `ok`; supplying `--note`
without `degraded` or `failed` is refused, so a note is never buried in a record that reads as fine.

**Friction notes are agent-authored, not derived.** Unlike cost, there is no file to read a "did
this go wrong" fact from — the calling agent is the one that hit the missing dependency or the wrong
instruction, so it is the one that states it, in the same one-line, no-adjective register the rest
of this repository's check lines already use (AGENTS.md, House rules: "numbers, not adjectives" —
here, "the fact, not a mood").

**The script reads the session's own transcript for cost, not an OTel export.** Claude Code already
writes a JSONL transcript per session (locally, one line per turn, each carrying that turn's token
usage); summing it is a fixed, mechanical read, with no Collector process to run, supervise or
restart. This replaces the first draft's OTel Collector entirely — there is no subprocess this
design needs to keep alive. A runtime with no equivalent transcript file is a case the script
already has to handle (say "not available"), so it costs nothing extra to also cover a Claude Code
session whose transcript cannot be located for some other reason.

**One file, `.telemetry/executions.jsonl`, keyed by change and phase.** The first draft's two files
(`sessions.jsonl` mapping session→change, `usage.jsonl` for the metrics) existed to let an
after-the-fact OTel export be joined against a session id captured separately. With the push
happening inline, at a point where the command already knows its own change name and phase, there is
nothing left to join — one line per push already carries everything a reader needs, cost and
friction alike.

**`.telemetry/` is gitignored, not committed.** It is machine state produced by running sessions,
the same category as `.harness/data/` — committing it would mean merge conflicts on an
append-mostly file with no reviewer able to say whether a line is real.

**`retro-entry` reads friction notes as candidate reflections, not a second question.**
`retro-entry`'s existing step 1 already asks a human for "what worked / what didn't / one change
next time" when not otherwise supplied. This change has it check `.telemetry/executions.jsonl` for
the change's `degraded`/`failed` notes first and offer them as the "what didn't" candidate, so a
finding an agent already reported does not also have to be remembered and retyped by whoever runs
`/aio:sync`.

**`collect-usage` and `retro-entry` are named explicitly in `sync.md` and `review.md`, rather than
left implicit.** The skills already exist and already describe this exact hand-off (`retro-entry`
already says it expects a summary "from `collect-usage`, supplied by the caller"), but neither
command file names either skill today, which is why the hand-off has never once happened in 88
entries. `collect-usage` itself is updated to read `.telemetry/executions.jsonl` (pushed records)
instead of the OTel-shaped files its current text describes.

## Risks / Trade-offs

- **The push is a step in a prompt, so it only happens if the agent executing the command follows
  it** — weaker than a hook's guarantee that it fires regardless. → This is the same trade-off the
  Expo pattern itself makes (an instruction, not an OS-level guarantee), and it is the one this
  repository can actually keep working across three runtimes today; a stronger, runtime-specific
  guarantee is a future change if one runtime's gap turns out to matter in practice.
- **A friction note is exactly as honest as the agent that wrote it** — nothing verifies it, the
  same way nothing verifies a `tasks.md` `req:` annotation today (`implement.md`: "Nothing verifies
  it, a person reviews it like the rest of `tasks.md`"). → Same mitigation: a person reading the log
  is the check, and a wrong note is at worst as costly as a wrong check-line is today.
- **A runtime with no per-session transcript pushes a cost record with no figure**, while its
  friction notes are still real. → The retro-log entry reports these independently: a stated "no
  cost figure" is not read as "nothing happened", because a friction note in the same file can still
  be there.
- **A phase that fails before reaching its push step logs nothing for that phase.** → Acceptable:
  the retro-log's own honesty rule already covers "no figure was pushed" as a stated outcome, not a
  silent one — a crashed phase and a phase whose runtime has no transcript read the same way to a
  person: no number, and a reason, wherever one could be captured.

## Open Questions

(none — the first draft's open question, about the Collector's exact protocol, is moot now that
there is no Collector)
