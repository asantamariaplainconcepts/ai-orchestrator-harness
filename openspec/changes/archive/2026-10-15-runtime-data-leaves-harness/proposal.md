## Why

`.harness/` mixes what a person reads with what a run writes. The development instance keeps its SQLite database, its runs and its scripts' outputs in `.harness/data/`, while the installed app keeps them in `~/.aiorchestrator`. `.harness/aio/laya` builds its Python environment in `.harness/data/laya/venv` of each checkout: 915 MB and several minutes for every worktree that scores, because `data/` is gitignored and every worktree starts empty, although the weights already live in the machine's cache. `.harness/evidence/` holds PNG captures from 26 September, from the mechanism before the `aio/evidence` branch, and nothing reads it. Issue #757, part of epic #756.

## What Changes

- The development instance keeps its database, runs and scripts in `src/artifacts/data/`, where the AppHost already leaves what it generates (`DevDatabase.ForAppHostAt` in `AppHost.cs`). Runs and scripts follow because `RunsOptions` and `ScriptOutputs` already place them beside the database. Publishing recordings to `aio/evidence` reads the data directory, whichever it is, and does not change.
- `laya` builds its environment in `~/.aiorchestrator/laya/<hash of laya-requirements.txt>/venv`, once per machine and pin, shared by every worktree and repository. The server's log and pid go beside it. A changed pin is another hash, so it builds a new environment and leaves the old one intact.
- `.harness/evidence/` and its `.gitignore` entry go. `.harness/data/` leaves `.gitignore` once nothing writes it.
- `laya`'s header comment and `ARCHITECTURE.md` say where each thing lives and why.
- `.harness/package/version` goes from 33 to 34, because `.harness/aio/laya` is the package's.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `installed-app`: a requirement for where a checkout's development instance keeps its data — `src/artifacts/data/`, never `.harness/`.
- `harness-scripts`: a requirement for where `laya` keeps its environment, pid and log — the machine's cache, keyed by the pin.

## Impact

- AppHost: `src/root/AiOrchestrator.AppHost/AppHost.cs` (`DevDatabase.ForAppHostAt` and its comment).
- Scripts: `.harness/aio/laya`, `.harness/package/version`.
- Repository: `.gitignore`, `ARCHITECTURE.md`; the untracked `.harness/evidence/` folder is deleted where it exists.
- Tests: `src/tests/AiOrchestrator.InfrastructureTests/EvidenceBranchTests.cs` stays green; a new script test for `laya`'s environment path in `src/tests/`.
- Machines: an existing `.harness/data/` in a checkout is left alone and simply stops being written; the development database starts empty at the new path.
