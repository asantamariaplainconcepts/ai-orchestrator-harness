## Why

`/aio:refine` only asks for a comment when it edits the issue body or leaves a gap for a person
(`.ai/commands/aio/refine.md`, lines 33 and 35). A run that ends in `done ready` without editing
anything leaves nothing on the issue: its verdict lives only in the loop's journal and the run's
memory, and `gh issue view <n> --comments` is empty (#575). Whoever opens the issue has to guess how it
got to `status:ready-for-proposal`. And in the instance's default permission mode, `allow-list`,
`gh issue comment` is not on the allow list, so even the two comments asked for today ask a question
an unattended run cannot answer.

## What Changes

- `/aio:refine` (this repository's `.ai/commands/aio/refine.md` and the package's
  `harness/commands/refine.md`) ends every run, `ready` or `gaps`, with exactly one closing comment on
  the issue, posted before `aio done`: what it closed by editing the body, what is left for the person
  who opened it and what would close it, or that nothing needed closing. The two separate comments of
  today fold into that one, so a `gaps` run still leaves one comment, not two.
- `.claude/settings.json` allows `gh issue comment`, so the closing comment goes out in `allow-list`
  without a prompt. The package embeds this file, so installed repositories get the same line.
- `AgentCommandTests` checks that `refine.md` posts its comment with `gh issue comment` before its End,
  and that the allow list names it.

Not changed: how the engine moves the issue between states, the Definition of Ready, the other
commands, and `blocked` runs (they stop before a verdict and say why in `--reason`).

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `agent-commands-and-hooks`: the refiner leaves one closing comment on every run that reaches a
  verdict, and the allow list gains `gh issue comment`.

## Impact

`.ai/commands/aio/refine.md`, `harness/commands/refine.md`, `.claude/settings.json`,
`src/tests/AiOrchestrator.Modules.Loop.FunctionalTests/AgentCommandTests.cs`. None of the five ports;
no module code.
