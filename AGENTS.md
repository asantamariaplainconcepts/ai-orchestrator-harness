# AGENTS.md

Instructions for any AI coding agent in this repository; `CLAUDE.md` points here. This file is a
router: it says where things are and how we work, and links to the one document that says the rest.

**Nothing outside this process is faked.** A run spawns a real `claude` in a real pseudo-terminal in
a real worktree, and the PBIs are real GitHub issues read through `gh`. The vocabulary and the eight
principles in [README.md](README.md) are the rules of this repository as much as of the product.

| You need… | Look in |
| --- | --- |
| What the product is, its words and principles, the loop format | [README.md](README.md) |
| How the code is arranged, the ports and the module rules | [ARCHITECTURE.md](ARCHITECTURE.md) |
| The design system, the screens and the writing voice | [DESIGN.md](DESIGN.md) |
| The states, the hold and the labels | [CONTRIBUTING.md](CONTRIBUTING.md) |
| What the product does, as requirements | [`openspec/specs/`](openspec/specs/) |
| Decisions, immutable once accepted | [docs/adr/](docs/adr/); the `write-adr` skill writes one |
| This repository's loop and its scripts: a file of `.aio/` that starts with `Managed by aio` is the package's, the rest the repository's | [`.aio/loop.yaml`](.aio/loop.yaml), [`.aio/gate`](.aio/gate), [`.aio/steps/`](.aio/steps/) |
| The agent commands, one per agent state: the package's, the same in every repository | [`.ai/commands/aio/`](.ai/commands/aio/), symlinked for every runtime |
| What this repository adds to those commands: the plan as an OpenSpec change, its ports, its files | [`.aio/context.md`](.aio/context.md), read by each command when it exists |
| The harness package the app installs in a repository by pull request, and its version | [`src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Harness/Package/`](src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Harness/Package/): `package.props` names every file, `version` is the number |

**GitHub owns the state of the work** (issues, and a milestone per wave); **this repository owns facts
about the code.** Nothing in `docs/` tracks progress or carries a checkbox: a plan in a file is
invisible to every other worktree until it merges. The reasoning behind shipped work is in the
comment of the code it explains.

## What collides when several agents run at once

1. **Registrations.** Each port's adapters register in their own folder of `Infrastructure`. Add
   yours there; never reorder lines you did not add.
2. **Migrations.** Two branches adding one to a module both rewrite its model snapshot, and git's
   merge describes neither. One issue owns a module's migrations at a time.
3. **`queries.ts` and `en.ts`.** Append next to your own screen's block; do not tidy a neighbour's.
   `openapi.json` and `contracts.ts` are generated: on a conflict, take either side and build again.
4. **Moving code out of a file somebody else owns** is a conflict nothing predicts. Say it in the
   issue before you split a file.
5. **The branch is the claim.** `change/<n>` at origin, pushed by `.aio/steps/claim`: git
   refuses the second push.

## The gate

```bash
aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj
```

Restore and install, build both sides, `pnpm typecheck` and `pnpm lint`, then every test assembly of
`src/AiOrchestrator.slnx`; warnings are errors. The steps are declared in
[`CiPipeline.cs`](src/root/AiOrchestrator.AppHost/CISteps/CiPipeline.cs) (`--list-steps` prints them),
and the loop's `verifying` runs the same command through `.aio/gate`. It is one command
because four can be run as three: on 15 August an agent typed `dotnet test` without the solution and
reported 118 tests as a full run, missing the 132 that police the module boundaries. A smaller count
than every assembly is a subset, not a success.

It also reports, never enforces, coverage and CRAP in `src/artifacts/coverage/` and Stryker.NET
mutation in `src/artifacts/mutation/` for `.cs` files changed since `origin/master`
(`MUTATION_SINCE` overrides it); `.aio/steps/quality` publishes what they measured on the issue.
`pnpm format:check` and `dotnet format` stay out because both were red when the gate was assembled,
and a gate born red teaches people to read past it. There is no CI service: say `aspire do ci` passed.

## The inner loop

`aspire start --isolated --apphost <the .csproj>` (as in [README.md](README.md#quick-start)), then
`aspire describe` for every resource's real endpoint, `aspire wait server`, and `aspire stop`. Pass
the `.csproj`, not its directory, which from a worktree also matches the parent checkout's AppHost.
`--isolated` keeps ports and secrets apart; the database is already per checkout. `start`, not `run`,
which holds the terminal. Never `kill -9` an AppHost. Pass `--apphost` every time: from a worktree
the CLI remembers the last one in the main checkout's `aspire.config.json`.

## How the loop works this repository

Each issue rides one branch, `change/<n>`, and one pull request through the states of
[CONTRIBUTING.md](CONTRIBUTING.md). **A command does the judgement and nothing else.** It reads what
its state hands it, writes its part, pushes, and ends with `.aio/aio done <outcome>
[--reason …]`. The engine moves labels, applies holds, opens, readies and closes; a command naming
any of those fails `AgentCommandTests`. `/aio:propose` uses the `openspec-propose` skill,
`/aio:implement` uses `openspec-apply-change`, and `rebasing` runs `rebase-safely`. What needs no
judgement is a script in `.aio/steps/`, or at the root of `.aio/` when it is this
repository's own, answering one JSON line, runnable by hand.

The guardrails are hooks: [`.claude/hooks/pre-tool-use`](.claude/hooks/pre-tool-use) refuses
`--no-verify`, `core.hooksPath` and every merge but `.aio/steps/merge`; the git commands that tread
on a worktree, a stash stack or a remote shared with another session (`git add -A` or `.`,
`git commit -a`, `git reset --hard`, `git clean` without `-- <paths>`, `git checkout .`,
`git restore .`, `git stash drop` and `clear`, `git push --force` without `--force-with-lease`, a push
of the default branch); and a WebFetch of the vendor's site, which `aio issue`, `aio pr` or the
vendor's CLI reads. Each refusal says what to run instead. [`.githooks/pre-commit`](.githooks/pre-commit)
refuses a commit on `master` or `main`.

## House rules

- **Verify against reality, not a fixture.** Every bug found in committed code here was found by
  running the product. A check line says what you observed, in numbers.
- **Respect the ports and the module boundaries** of [ARCHITECTURE.md](ARCHITECTURE.md). MOD001–005
  and CQS001 fail the build; a sixth port is a finding for the issue.
- **UI work follows [DESIGN.md](DESIGN.md).**
- **Assert your worktree** (`git rev-parse --show-toplevel`) before any mutating git batch.
- **Confirm shared-state actions** (issues, labels, pull requests, merges) before executing them,
  unless the engine is doing them.
- **Append-only history.** Never rewrite a retro entry or an accepted ADR; supersede it.
- **Comments keep the reason.** The `code-humanizer` skill prunes the rest without touching code.

Prose follows [DESIGN.md](DESIGN.md#writing). Issues, commits and pull requests are in Spanish; code,
comments and these documents in English.
