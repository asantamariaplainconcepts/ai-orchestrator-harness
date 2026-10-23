## Context

`.harness/` was split by owner in #729: the root is the repository's, `aio/` the package's. This repository also keeps at the root what only it runs: `archive` (the `merging` state's first script), `keep-committer` (the `rebasing` state's `git rebase -x`), `pack` and `release` (the .NET tool and its publication), `trust-sweep` (this machine's Claude Code trust), and `package/` (the files of the harness package the app embeds, declared by `package.props`). Three things fix how they can move:

- `LocalScriptRunner` refuses a script name with a `/`, so `loop.yaml` cannot say `repo/archive`. `HarnessScripts.Find` searches `HarnessScripts.Folders`: `.harness`, `.harness/aio`, `.harness/scripts`.
- `package.props` locates the repository from itself (`HarnessRepository = $(MSBuildThisFileDirectory)..\..\`), and `AiOrchestrator.Modules.Workspace.csproj` imports it as `..\..\..\..\.harness\package\package.props`.
- `pack` finds the repository as `$(dirname "${BASH_SOURCE[0]}")/..`; `release` calls `pack` as `$here/pack` and finds the repository as `$here/..`.

## Goals / Non-Goals

**Goals:**
- `ls .harness` shows the loop's configuration plus `aio/` and `repo/`, eleven entries.
- The engine finds a script in `.harness/repo/` by its name, so `loop.yaml` does not change.
- The embedded package is the same, file for file and path for path, at version 36.

**Non-Goals:**
- No script changes what it does; `pack` and `release` only change how they find the repository's top.
- `.harness/repo/` does not enter the package, and the package's version is not raised.
- `.harness/README.md`, the package's page, is not rewritten: it already says that whatever a repository adds at the root is its own, and changing it would change an embedded file, which the package's rule pays for with a version.

## Decisions

**`repo/` is a folder the engine searches, between the repository's root and the package.** `Folders` becomes `[Repository, RepositoryOwn, Package, Former]` with a new constant `".harness/repo"`. Alternative: let `loop.yaml` name `repo/archive`. Rejected: the runner refuses names with a slash on purpose (a name is never a path), and lifting that would let a loop run any file in the checkout. Putting `repo/` after the root keeps a repository's root script winning, as today; putting it before `aio/` keeps a repository's own script winning over the package's. `Commands` (the agents' PATH) does not gain `repo/`: nothing there is an agent's command.

**`git mv` the six entries together.** `release` finds `pack` as `$here/pack`, which stays true. `pack` and `release` find the top as `$here/../..`. `package.props` becomes `..\..\..\`, and every `$(HarnessRepository)` include keeps working; the includes relative to `$(MSBuildThisFileDirectory)` move with the file. The `LogicalName` of every embedded resource stays as it is, so `HarnessPackage`, `GET /api/harness` and an install see no difference.

**Tests read the scripts where they are.** `LoopScriptTests` copies `archive` and `release` from `.harness/repo/` and writes them into its fixture checkout at `.harness/repo/` (the archive one runs through `LocalScriptRunner`, so it also proves the engine finds it there); the rebase test runs `.harness/repo/keep-committer`. `StateScriptsTests` adds a case for `HarnessScripts.Find(checkout, "archive")` answering `.harness/repo/archive`, and one where `.harness/<name>` and `.harness/repo/<name>` both exist and the root wins. `HarnessPackageTests` asserts the repository's own scripts in `.harness/repo/` and still none of them in the package.

## Risks / Trade-offs

- [A stale `.harness/package` path in a doc or comment] → the issue's `git grep` returns nothing outside the archive, the retro log and `.harness/repo` itself.
- [The package silently loses a file because `HarnessRepository` is off by one level] → `HarnessPackageTests` compares every embedded entry with the repository's file and checks the version and paths `GET /api/harness` answers.
- [`release.yml` on a tag still calls the old path] → it names `.harness/repo/release`, and `LoopScriptTests`' release cases run that file.
