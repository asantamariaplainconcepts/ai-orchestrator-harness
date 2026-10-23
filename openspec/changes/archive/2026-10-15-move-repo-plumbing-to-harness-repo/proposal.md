## Why

The root of `.harness/` promises to be the repository's own loop configuration, yet beside `loop.yaml`, `gate`, `evidence.json`, `quality.json`, `protected-paths`, `context.md` and `templates/` it holds six entries that only this repository has and no other will: `archive`, `pack`, `release`, `trust-sweep`, `keep-committer` and `package/`. Whoever arrives cannot tell the loop's configuration from the plumbing that builds and publishes the app. Issue #760, part of epic #756.

## What Changes

- The six entries move, unchanged, to `.harness/repo/`, with a short `README.md` there saying what each does and who calls it.
- The engine learns `repo/`: `HarnessScripts.Folders` searches `.harness`, then `.harness/repo`, then `.harness/aio`, then `.harness/scripts`. `loop.yaml` still names `archive` by its name, so `merging` keeps its shape, and any repository can keep its own scripts there.
- What finds itself by its own path climbs one level: `HarnessRepository` in `package.props` becomes `..\..\..\`, the `Import` of `AiOrchestrator.Modules.Workspace.csproj` points at `.harness\repo\package\package.props`, and `pack` finds the root at `../..`.
- Everything that names these paths follows: `release.yml`, `HarnessPackage`, the `rebase-safely` skill, `AGENTS.md`, `ARCHITECTURE.md`, `CONTRIBUTING.md`, the comment in `AiOrchestrator.Server.csproj`, the specs and the tests that copy or run the scripts.
- The root of `.harness/` is left with eleven entries: `README.md`, `version`, `loop.yaml`, `gate`, `evidence.json`, `quality.json`, `protected-paths`, `context.md`, `templates/`, `aio/` and `repo/`.
- The package an installation receives does not change, nor does its version (36): every embedded file keeps its content and its install path, and `.harness/repo/` is not embedded.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `harness-scripts`: the engine also finds a script in `.harness/repo/`, after `.harness/` and before `.harness/aio/`; `release` runs `.harness/repo/pack`; `trust-sweep` is `.harness/repo/trust-sweep`.
- `harness-package`: this repository keeps what only it runs in `.harness/repo/`, and the package's own files are `.harness/repo/package/`.
- `commit-marks`: the rebase runs `.harness/repo/keep-committer`.
- `installed-app`: the tool is packed by `.harness/repo/pack`.

## Impact

- `.harness/`, `.harness/repo/` (new), `.github/workflows/release.yml`, `.ai/skills/rebase-safely/SKILL.md`, `AGENTS.md`, `ARCHITECTURE.md`, `CONTRIBUTING.md`.
- `src/shared/AiOrchestrator.BuildingBlocks/Ports/IScriptRunner.cs`, `src/modules/Loop/AiOrchestrator.Modules.Loop/Definition/LoopDefinition.cs` (doc comment), `src/modules/Workspace/AiOrchestrator.Modules.Workspace/AiOrchestrator.Modules.Workspace.csproj`, `src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Harness/HarnessPackage.cs`, `src/root/AiOrchestrator.Server/AiOrchestrator.Server.csproj`.
- Tests: `StateScriptsTests`, `LoopScriptTests`, `HarnessPackageTests`.
- No API, no screen, no package version.
