<!-- Managed by aio. Install harness overwrites it. -->
# .aio

The harness is the loop AI Orchestrator runs on this repository's issues. An issue carries a `status:*` label; the label is its state. In each state one agent or one script does one step. A person is asked only where the loop holds, and the app lists those issues under *Needs you*, with the reason and what to do.

## What is whose

A file that starts with the line `Managed by aio. Install harness overwrites it.`, in the comment syntax of its language, is the package's: every update replaces it. A file without that line is yours, and an update writes it only when it is missing. `version` is the package's too; a number has no comment, so it carries no line.

```
.aio/
  README.md        this file
  version          the package's version
  loop.yaml        yours: the states, and what runs in each
  gate             yours: one command that runs all your checks
  config.json      yours: evidence, quality and the protected paths
  context.md       yours: what this repository adds to the six commands; the package has none
  templates/       yours: the issue and epic templates
  steps/           one file per step loop.yaml names
  checks/          one check per file, which quality runs
  lib/             code shared by steps, with its test beside it when it has one
  scorers/laya/    the risk model the engine starts before a score
  schemas/         done.v1.json, the shape of an agent's end record
  aio              the agents' CLI: `.aio/aio done next`
```

The engine looks for a script named in `loop.yaml` in `.aio/` and then in `.aio/steps/`, so a script of your own at the root, such as `archive`, wins over the package's step of the same name.

The package also writes `.ai/commands/aio/` (the six agent commands), `.claude/hooks/pre-tool-use` and `.claude/settings.json`, and overwrites them on every update.

## The loop

This is the package's `loop.yaml`. A state runs its scripts first, then its agent.

| State | What runs | What it hands on |
| --- | --- | --- |
| `ready-for-proposal` | `refiner`, `/aio:refine` | `ready` → `spec-ready`; `gaps` → `needs-refinement`, where the loop stops |
| `spec-ready` | `file-overlap`, `claim`, then `coder`, `/aio:propose` | The branch `change/<n>`, a `## Plan` comment, a draft pull request |
| `in-progress` | `coder`, `/aio:implement` | Code pushed to `change/<n>` |
| `verifying` | `gate`, `evidence`, `quality` | `green` → `code-review` and the pull request is marked ready; `red`, `blocked` or `failed` hold; `quality` reports and holds nothing |
| `code-review` | `reviewer`, `/aio:review-diff` | A review on the pull request; `approved` → `retro`, `rejected` → `in-progress`, twice at most |
| `retro` | `coder`, `/aio:review` | A `## Retro` comment when the change taught something |
| `merging` | `merge` | `merged` squash-merges, deletes the branch and closes the issue; `conflict` holds |

Every agent reads the issue, its comments and `context.md` when there is one.

### Every step answers in a format the loop can read

A script ends with one JSON line, `{"outcome": "...", "why": "..."}`. The state's `on` maps the outcome to the next state or to a hold. `"wait": true` means not yet. A script that prints no line passes on exit 0 and holds on anything else.

An agent ends with `.aio/aio done <outcome>`. That writes `.aio/done.json`, which is checked against `schemas/done.v1.json`. A record that is missing, malformed or names an outcome the state does not have fails the run.

| Step | Outcomes |
| --- | --- |
| `gate` | `green`, `red` |
| `evidence` | `passed`, `skipped`, `blocked`, `failed` |
| `quality` | `reported`, always with exit 0 |
| `merge` | `merged`, `conflict` |
| `file-overlap` | Waits while another issue in flight shares a file; holds when the issue names no path |
| `claim` | Exit 0 when this clone holds `change/<n>`, 1 when it does not |
| `risk-score`, `diff-risk` | `low`, `high`, `unknown` |
| `/aio:refine` | `ready`, `gaps` |
| `/aio:review-diff` | `approved`, `rejected` |
| `/aio:propose`, `/aio:implement`, `/aio:review` | `next` |

What is deterministic is the path the loop takes with a result, never the result. A model can answer differently twice; the same answer always moves the issue the same way. So a tool that answers in the same format can replace another: other tests behind `gate`, another model behind a score, another CLI behind an agent.

## Make it yours

`gate` is one command that runs all your checks at the top of the issue's worktree, on its branch. No halves: a skipped check is not a pass. Its last line is `{"outcome": "green", "why": "412 passed"}` with exit 0, or `red` with exit 1. The stub holds every issue with "define your gate here" until you uncomment the line for your stack or write your own. Its `run` keeps the output in a log and names it in the why.

`config.json` holds what three steps read, one key each. `evidence` says what `evidence` captures after the gate: `paths`, the globs whose change asks for captures; `start`, an Aspire AppHost and its resource or a command and its URL; `routes`, the pages to shoot in light and dark at 1440x900 and 390x844. Until `paths` names something, nothing is captured. A repository whose own tests take the screenshots names `capture` instead.

`quality` says what `quality` measures of a change after the evidence, against the pull request's base: the coverage of its changed lines from a Cobertura report, the CRAP of the methods it touched from a `crap.tsv`, mutation from a `summary.json`, new duplicated lines with jscpd, tests lost or skipped by name, the tests that stood at the base and the change touched, the files outside the issue's *Dónde cae*, and the old tests that fail in their base form against the new code. Each names its report and its line; without the key the lines are coverage 70%, CRAP 30, no new duplicated line, and no test lost, touched or failing in its base form and no file outside. A check whose report is missing or of another commit says "not measured" and why. It publishes a table on the issue and blocks nothing.

`protected` lists the paths a person always reviews, as an array of patterns. Name paths that show in a diff, such as `src/auth/*`, not words like `*auth*`, and leave out what the gate already fails on. `diff-risk` reads the list from the base commit, so a change that edits the list is judged by the list it is editing. The package loop does not run `diff-risk`; to hold on a protected path, add a state before `merging`:

```yaml
  merge-review:
    score: diff-risk
    on:
      low: merging
      high:
        hold: A protected path changed
        next: merging
      unknown: merging
```

`loop.yaml` is the loop. Edit a state's `command` to change what it asks, or its `script` to change a step; a script name is looked for in `.aio/` first, then in `.aio/steps/`. Put `model` under a role in `agents`, or on one state, to choose a model; `provider` is the CLI: `claude-code`, `copilot`, `opencode` or `codex`. A state that goes back to an earlier one needs `max_loops`. Every key is described in [AI Orchestrator's README](https://github.com/asantamariaplainconcepts/ai-orchestrator-harness#the-loop).

`context.md` is what this repository adds to the six commands: how it is built, where its plan goes, what its retro writes. Where it speaks, it replaces what a command says; where it is silent, the command stands.

`templates/issue.md` is the Definition of Ready `/aio:refine` checks an issue against, and `file-overlap` reads its *Dónde cae*. `epic.md` is the shape of an epic.

## Where the loop leaves its work

- `status:<state>` on the issue says where it is; `status:holding` says a person decides next.
- The branch `change/<n>` is the reservation. While it exists at the remote and the issue is open, no other clone takes the issue.
- On the issue: `## Plan`, `## Retro`, `<!-- aio:journey -->` (the engine's table of every move, hold and run), `<!-- aio:evidence <sha> -->` (the captures) and `<!-- aio:quality <sha> -->` (what `quality` measured). The code review is on the pull request.
- On the branch `aio/evidence`: the recording of every run, which the Journey plays.

## Updating

*Install harness* on the folder's page proposes the package as a pull request from `harness/install`. Nothing reaches your default branch until you merge it. `version` says which package this repository has; the folder's page compares it with the app's. The pull request replaces every file that carries the managed line, and touches a file of yours only when it was missing. Over a repository that still has `.harness/`, it moves everything to `.aio/` and folds `evidence.json`, `quality.json` and `protected-paths` into `config.json`.
