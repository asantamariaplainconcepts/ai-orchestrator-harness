## 1. The development instance's data

- [x] 1.1 In `src/root/AiOrchestrator.AppHost/AppHost.cs`, `DevDatabase.ForAppHostAt` returns `<root>/src/artifacts/data/aiorchestrator.db`, and its comment says why: inside the checkout so a worktree's data dies with it, beside what the AppHost already generates, and already ignored by `artifacts/`
- [x] 1.2 Run `aspire start` in this worktree and confirm `src/artifacts/data/aiorchestrator.db` exists and `.harness/data/` was not created; stop the AppHost
- [x] 1.3 Build `AiOrchestrator.InfrastructureTests` and run `EvidenceBranchTests`; it passes unchanged

## 2. laya's environment on the machine

- [x] 2.1 In `.harness/aio/laya`, compute `pin` as the first 12 hex characters of `shasum -a 256 "$here/laya-requirements.txt"` and set `data="$HOME/.aiorchestrator/laya/$pin"`, with `venv`, `pidfile` and `logfile` inside it; the "once per checkout" message says "once per machine and pin" and names the directory
- [x] 2.2 Guard the build with `mkdir "$data/.lock"`, removed by a trap on exit; an `ensure` that cannot take the lock waits for it to go (bounded like the health wait) and re-checks `venv/bin/laya-serve` before building. A failed `python -m venv` or `pip install` removes the half-built `venv/`
- [x] 2.3 Rewrite the header's "The venv is the checkout's, the weights are the machine's" paragraph: environment, pid and log live in `~/.aiorchestrator/laya/<pin>/`, why keyed by the pin, why shared (915 MB and minutes per worktree before, one server on one port), and how to remove an old pin's directory
- [x] 2.4 Add `LayaScriptTests` in `src/tests/AiOrchestrator.InfrastructureTests/` with a temporary `HOME` and a fake `python3`, `curl` and `laya-serve` on PATH (the fake `laya-serve` writes a marker the fake `curl` reads as healthy; the fake `pip` records each install): `ensure` from two copies of `.harness/aio/` builds one environment under `$HOME/.aiorchestrator/laya/<pin>/venv` and installs once; a changed `laya-requirements.txt` builds a second `<pin>` and leaves the first; `stop` from the second copy stops the server the first started; nothing is written under either copy's `.harness/`
- [x] 2.5 `.harness/package/version` goes from 33 to 34

## 3. What leaves `.harness/`

- [x] 3.1 Remove the `.harness/evidence/` block and the `.harness/data/` block from `.gitignore`
- [x] 3.2 Delete `.harness/evidence/` from this worktree if it exists (it is untracked)
- [x] 3.3 In `ARCHITECTURE.md`, say where runtime data lives and why: the development instance in `src/artifacts/data/` of its checkout, the installed app in `~/.aiorchestrator/`, laya's environment in `~/.aiorchestrator/laya/<pin>/`, and that `.harness/` holds only what a person reads and edits
- [x] 3.4 `git grep -E "\.harness/(data|evidence)/" -- ':!openspec/archive' ':!openspec/changes/archive' ':!docs/process/retro-log.md' ':!openspec/changes/runtime-data-leaves-harness'` returns nothing
- [x] 3.5 `openspec validate --specs --strict` and `openspec validate runtime-data-leaves-harness --strict` pass
