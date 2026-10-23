## Context

`DevDatabase.ForAppHostAt` (`AppHost.cs:104`) walks up from the AppHost to the checkout root and returns `<root>/.harness/data/aiorchestrator.db`. `RunsOptions` and `ScriptOutputs` put `runs/` and the scripts' outputs beside whatever database the connection string names, so moving the database moves all three. `EvidenceBranchTests` builds its own scratch under a temporary `data/runs/.evidence` and does not depend on the checkout's path.

`.harness/aio/laya` sets `data="$root/data/laya"` with `root` the `.harness/` folder, and keeps `venv/`, `laya-serve.pid` and `laya-serve.log` there. The server binds the fixed port 8317 on loopback, so there is already one server per machine; only the venv, pid and log are per checkout.

## Goals / Non-Goals

**Goals:** nothing under `.harness/` is runtime data. One laya environment per machine and pin. `git grep -E "\.harness/(data|evidence)/"`, with the two OpenSpec archives and `retro-log.md` excluded, returns nothing.

**Non-Goals:** the `aio/evidence` branch and `aio:evidence` comments. Where the installed app keeps its data. Moving or migrating an existing `.harness/data/aiorchestrator.db`. Where Hugging Face caches the weights.

## Decisions

**The development data directory is `<root>/src/artifacts/data/`.** `src/artifacts/` is where the gate already writes coverage, test results and mutation reports, and `artifacts/` is already in `.gitignore`, so nothing new is ignored. It stays inside the checkout, so deleting a worktree still deletes its database and two checkouts never share one (ADR-0003 holds). Only the path segment in `ForAppHostAt` and its comment change; `RepositoryRootAbove` stays.

**laya's home is `${HOME}/.aiorchestrator/laya/<pin>/`.** `~/.aiorchestrator` is already the app's directory on the machine. `<pin>` is the first 12 hex characters of `shasum -a 256 laya-requirements.txt`: short enough to read, and a pin edit is a new directory, so the old environment keeps working for a checkout still on the old pin. `venv/`, `laya-serve.pid` and `laya-serve.log` live in that directory. `status` and the error messages name the full path.

**One build at a time, under a lock directory.** Two worktrees can run `ensure` at the same moment. The script takes `mkdir <pin>/.lock` before building and starting and removes it on exit through a trap; an `ensure` that cannot take it waits for it to go, up to 3600 s (a build and the first health wait), and then checks for a healthy server and `venv/bin/laya-serve` again instead of building. The lock covers the start too, a change from the plan found while building it: with one pidfile per machine, two starts at once race for the port and the loser writes a dead pid over the winner's. A lock whose holder pid is gone is taken over. `mkdir` is atomic and needs no `flock`, which macOS lacks. The venv is built in place, not built aside and moved, because a moved venv's `bin/` shebangs point at the old path. A failed `pip install` removes the half-built `venv/`, so nothing that looks provisioned is left behind.

**The pid and log are the machine's too.** With one port there is one server; a pidfile per checkout let `stop` in one worktree miss a server another started. Beside the venv, every checkout's `stop` and `status` see the same server.

**The weights stay in Hugging Face's cache.** They are already per machine; the header now says both the environment and the weights are the machine's, and why the environment is keyed by the pin.

**`.harness/data/` leaves `.gitignore` in this change.** After it, nothing in the repository writes there. A machine that still has the folder sees it as untracked once; deleting it is the person's call, and the commit message says so.

**The branch's `git grep` also excludes this change's own folder.** Its artifacts must name the paths they remove. `merging` archives the change into `openspec/changes/archive/`, which the issue's command already excludes, so after the merge the issue's command returns nothing as written.

## Risks / Trade-offs

- [The development database starts empty at the new path] → The first `aspire start` migrates and seeds it as for a new checkout; the old file stays in `.harness/data/` until someone deletes it.
- [Old environments pile up in `~/.aiorchestrator/laya/`] → One per pin ever used, each ~915 MB; pins change rarely. The header says how to remove an old one.
- [A server started from the old per-checkout pidfile is still running] → `ensure` sees it healthy on 8317 and changes nothing; `stop` from the new path does not know its pid, and says nothing is recorded. Stopping it once by hand is the price of the move.
