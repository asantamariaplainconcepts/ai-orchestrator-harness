## 1. The engine learns repo/

- [x] 1.1 In `src/shared/AiOrchestrator.BuildingBlocks/Ports/IScriptRunner.cs`, add the constant `".harness/repo"` to `HarnessScripts` and make `Folders` search `.harness`, `.harness/repo`, `.harness/aio`, `.harness/scripts` in that order; `Commands` stays as it is. Update the class's doc comment, and the `Scripts` parameter comment in `src/modules/Loop/AiOrchestrator.Modules.Loop/Definition/LoopDefinition.cs` that says where a script is found.
- [x] 1.2 In `StateScriptsTests`, a checkout with `.harness/repo/archive` finds `archive` there, and one with both `.harness/gate` and `.harness/repo/gate` finds the root's.

## 2. Move the plumbing

- [x] 2.1 `git mv` `.harness/archive`, `pack`, `release`, `trust-sweep`, `keep-committer` and `package/` into `.harness/repo/`, keeping each script's executable bit.
- [x] 2.2 `pack` and `release` find the repository's top at `$here/../..` (`pack` from `dirname "${BASH_SOURCE[0]}"`); every usage line in the six headers names `.harness/repo/<name>`. Nothing else in the scripts changes.
- [x] 2.3 `package.props`: `HarnessRepository` becomes `$(MSBuildThisFileDirectory)..\..\..\`, and its header comment says the repository's own entries live in `.harness/repo/`. The `LogicalName` of every embedded resource stays as it is.
- [x] 2.4 The `Import` in `src/modules/Workspace/AiOrchestrator.Modules.Workspace/AiOrchestrator.Modules.Workspace.csproj` points at `..\..\..\..\.harness\repo\package\package.props`.
- [x] 2.5 Write `.harness/repo/README.md`: a few lines, one per entry, saying what it does and who calls it (`archive` by `merging` in `loop.yaml`, `keep-committer` by the `rebasing` run's `git rebase -x`, `release` by a person or `release.yml`, `pack` by `release` or a person, `trust-sweep` by a person, `package/` imported by `Modules.Workspace`), and that the engine finds a script here by its name.

## 3. Whoever names them follows

- [x] 3.1 `.github/workflows/release.yml` runs `.harness/repo/release`, comment included.
- [x] 3.2 `.ai/skills/rebase-safely/SKILL.md` says `git rebase -x .harness/repo/keep-committer <upstream>`.
- [x] 3.3 `AGENTS.md`, `ARCHITECTURE.md` and `CONTRIBUTING.md` name `.harness/repo/package/`, `.harness/repo/keep-committer`, `.harness/repo/pack` and `.harness/repo/release`, links included.
- [x] 3.4 The doc comment of `HarnessPackage.cs` names `.harness/repo/package/package.props`, and the comment in `src/root/AiOrchestrator.Server/AiOrchestrator.Server.csproj` names `.harness/repo/pack`.
- [x] 3.5 The *Purpose* paragraphs of `openspec/specs/harness-package/spec.md` and `openspec/specs/commit-marks/spec.md` name `.harness/repo/package/` and `.harness/repo/keep-committer`; the requirements change through this change's deltas.

## 4. The tests read the scripts where they are

- [x] 4.1 `LoopScriptTests`: the archive test copies `.harness/repo/archive` into its fixture's `.harness/repo/` and still runs it through `LocalScriptRunner` by the name `archive`; the release tests copy `.harness/repo/release` and write their fake `pack` beside it in `.harness/repo/`; the rebase test runs `.harness/repo/keep-committer`; the gate stub and `evidence.json` are read from `.harness/repo/package/`.
- [x] 4.2 `HarnessPackageTests` reads the version and the package's own files from `.harness/repo/package/`, asserts the repository keeps `archive`, `release`, `trust-sweep`, `pack` and `keep-committer` in `.harness/repo/`, and still finds none of them, nor anything under `.harness/repo/`, in the package; the version stays `36`.
- [x] 4.3 `dotnet test` of `AiOrchestrator.Modules.Loop.FunctionalTests`, `AiOrchestrator.InfrastructureTests` and `AiOrchestrator.Modules.Workspace.FunctionalTests` passes.

## 5. It shows

- [x] 5.1 `ls .harness` shows exactly `README.md`, `version`, `loop.yaml`, `gate`, `evidence.json`, `quality.json`, `protected-paths`, `context.md`, `templates`, `aio` and `repo`.
- [x] 5.2 `git grep -n "harness/archive\|harness/pack\b\|harness/release\|harness/trust-sweep\|harness/keep-committer\|harness/package" -- ':!openspec/archive' ':!openspec/changes/archive' ':!docs/process/retro-log.md' ':!.harness/repo' ':!openspec/changes/move-repo-plumbing-to-harness-repo'` returns nothing.
- [x] 5.3 `.harness/repo/pack`, run from the repository's top, builds `artifacts/aio.<version>.nupkg` and prints its path.
- [x] 5.4 `openspec validate move-repo-plumbing-to-harness-repo --strict` and `openspec validate --specs --strict` pass.
