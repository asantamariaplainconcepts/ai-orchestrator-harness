## Why

`.harness/aio/` shows sixteen entries side by side: the loop's steps (`claim`, `merge`, `evidence`, `quality`…), the agents' CLI (`aio`), a Python model server with its requirements (`laya`, `laya-requirements.txt`), and `checks/`, `lib/` and `schemas/`. Nothing tells a step from a tool a step uses underneath. `laya` is the clearest case: it is not a step, it is optional (a loop with no `score:` never needs it), and the engine starts it, not `risk-score` or `diff-risk`. Issue #759, part of epic #756.

## What Changes

- `laya` and `laya-requirements.txt` move to `.harness/aio/scorers/laya/`. The loop's steps stay flat in `.harness/aio/`, because `loop.yaml` names them and the engine looks for them there; `checks/`, `lib/` and `schemas/` stay.
- `HarnessScripts.Find` looks for `laya` in `.harness/aio/scorers/laya/` first and, when it is not there, where any script is, `.harness/aio/` among them, so a repository that has not installed 36 still scores; `StateScripts.EnsureModel` keeps asking `Find` for it, so the engine starts it from the new folder wherever the package put it there.
- The package embeds `aio/scorers/laya/*`, gives `scorers/laya/laya` the execute bit, and an install over a repository with the old `aio/laya` and `aio/laya-requirements.txt` moves them in the same pull request. Harness 23's `.harness/laya-requirements.txt` is left where it is: no repository is on 23 any more, and a file nothing reads does not stand in the way (the person's review on #759).
- The pin in `~/.aiorchestrator/laya/<pin>/` does not change: it is the hash of the requirements file's content, and the content does not move.
- `risk-score`, `diff-risk`, `laya`'s own header, `.harness/README.md`, `README.md` and `ARCHITECTURE.md` name the new place. The README's table of what is whose names every folder of `aio/`, and `HarnessPackageTests` fails when one is missing.
- `.harness/package/version` goes from 34 to 36 (#765 took 35).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `harness-package`: `aio/` holds the steps flat and `scorers/laya/`; the package carries and marks executable `scorers/laya/laya`; an install moves the old `aio/laya` files; the README names every folder of `aio/`.
- `harness-scripts`: `Find` looks for `laya` in `.harness/aio/scorers/laya/` first, then where any script is; laya's environment is pinned by `.harness/aio/scorers/laya/laya-requirements.txt`, with the same pin.
- `loop-engine`: the model server ensured before a score is `.harness/aio/scorers/laya/laya`.

## Impact

- Package: `.harness/aio/` (the move, and the comments of `risk-score`, `diff-risk` and `laya`), `.harness/README.md`, `.harness/package/package.props`, `.harness/package/version`.
- Code: `src/shared/AiOrchestrator.BuildingBlocks/Ports/IScriptRunner.cs` (`HarnessScripts`), `src/modules/Loop/AiOrchestrator.Modules.Loop/Engine/StateScripts.cs` (its comment), `src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Harness/` (`HarnessPackage.Executable`, `HarnessPackage.Former`).
- Docs: `README.md`, `ARCHITECTURE.md`, `openspec/specs/{harness-package,harness-scripts,loop-engine}` through this change's deltas.
- Tests in `src/tests/`: `HarnessPackageTests`, `InstallHarnessTests`, `StateScriptsTests`, `ScoreUnknownTests`, `LayaScriptTests`, `DiffRiskTests`, and the shipped version in `AppTests`.
- A repository that has not installed 36 keeps its `aio/laya`, which the engine still finds and starts until the install moves it (design, D1).
