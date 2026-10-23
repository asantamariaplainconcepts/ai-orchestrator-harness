# Design

> **Corrected during implementation (P0, H3).** This design described the watcher as a dispatcher.
> The agent-control-plane hardening of #313 deliberately withheld that authority: what shipped
> observes a tick and writes nothing, on a mode key that has no value which enables dispatch.
> The durability this design planned — the journal, the WIP gate, the exact Folder resolution —
> ships as the `Coordination` module's dispatch-intent journal, ready for a trusted authority (H5)
> to act on. Two launch-era Decisions below ("The launch failure hold", the `GET /api/runs?live=true`
> skip) describe code that no longer exists; `tasks.md` 3.5 and 3.6 record what replaced them.

## Context

See `proposal.md` — Why. Three facts from the current codebase shape everything below, none of them
mentioned in issue #313 because they only surface by reading the code:

1. **No module may reference another's implementation assembly** (`module-boundaries` spec, MOD001–005
   / CQS001). Today no module publishes a `.Contracts` assembly either — cross-module composition does
   not exist yet anywhere in this codebase. The only thing every module exposes to the outside is its
   own HTTP endpoints (`IModule.MapEndpoints`), the same surface the frontend calls.
2. **A task's Folder is not resolved server-side today.** `MirroredTask.FolderPath` is empty on every
   mirrored row (`SetTaskStatus.cs`'s own comment says so); the Tasks screen and a project's Overview
   resolve it in the browser, via `folderFor`/`sameRepo` in `src/frontend/shared/repo.ts`, by comparing
   a task's vendor URL against each Folder's `RemoteUrl`. Nothing headless can call into a browser.
3. **The WIP limit is enforced by a person following prose**, not by code. `issue-lifecycle`'s existing
   requirement says `/aio:implement`'s gate gets it done — that command tells a person or agent to read
   `.harness/config.json` and count by hand.

## Goals / Non-Goals

**Goals:**
- Start the matching Run automatically from a task's status/hold, on the same order of minutes
  `TaskPoller` already polls at.
- Enforce the same WIP number in code for the automatic path, without touching how `/aio:implement`'s
  prompt-driven gate works today.
- Resolve a task's Folder server-side without asking the frontend for anything, since nobody may be
  watching a browser.

**Non-Goals:**
- Changing `folderFor` or any frontend code. The browser keeps its own copy of the same rule.
- A new cross-module contracts mechanism. This change deliberately stays inside "call the existing
  public HTTP endpoints," even though that costs a loopback call — introducing `.Contracts` assemblies
  for the first time is a bigger, separate architectural change this issue does not ask for.
- Any change to `status:holding`'s meaning, or to who removes one. A failure applies a hold; nothing
  here ever clears one (see the added requirement on failure).
- A recommendation UI, a "why" (that is #250), or a new Connector/vendor call. Detection reads the
  mirror `TaskPoller` already fills; it does not poll GitHub a second time.

## Decisions

### The watcher lives in the host, calling its own HTTP API
**Decision:** a new `BackgroundService`, registered in `AiOrchestrator.Server` alongside the app's own
startup composition, holds an `HttpClient` bound to the server's own base address and calls
`GET /api/tasks`, `GET /api/folders` and `POST /api/runs` exactly as the frontend does.

**Why:** it is the only path that touches both Tasks and Runs without inventing a cross-module
mechanism that does not exist yet (see Context #1). The host is the one place already allowed to know
about every module, because it is what wires `MapEndpoints` for all of them — it simply has never
before been the one place also *calling* them.

**Alternatives considered:**
- *A `.Contracts` assembly per module, referenced directly.* Removes the loopback HTTP round trip, but
  is the first use of a mechanism the codebase has never built, on every module this change touches, for
  one background loop. Bigger and riskier than this issue's scope; a candidate for its own change if a
  second consumer ever needs it.
- *Split the watcher's two halves into Tasks and Runs, each running its own poll.* Neither module can
  see the other's data (Context #1) without exactly the cross-module reference the analyzers forbid, so
  this does not actually avoid the boundary — it just hides where it is crossed.

### Folder resolution ports the browser's rule, in C#, once
**Decision:** a small internal helper, owned by the watcher, reimplements `repoIdentity`/`sameRepo`
from `src/frontend/shared/repo.ts` against the `Url` field `GET /api/tasks` already returns and the
`RemoteUrl` field `GET /api/folders` already returns.

**Why:** both fields are already public API responses; nothing new needs to be exposed. Duplicating a
well-isolated ~30-line string rule in a second language is cheaper and safer than inventing a way for
TypeScript and C# to share it, and the two are already independent implementations of the same
documented rule rather than one canonical source — same risk profile as any two client languages
agreeing on a wire format.

**Alternatives considered:**
- *Finish `MirroredTask.Attribute` in production and write `FolderPath` at sync time.* Fixes the
  underlying gap for every consumer (including the UI panel, which still asks a person to pick a
  Folder today), but is a bigger change than this issue's Seam claims, and changes `sync-the-mirror`'s
  and `task-inbox`'s specs, not just this one's. Left as a natural follow-up, not folded in here.

### `.harness/config.json` is read through the existing declared-file seam

> **Corrected during implementation.** The premise below is wrong about the prior art, and the
> implementation follows the real one. `IDeclaredCommandFile` does **not** read
> `.harness/commands.json` through `IWorkspaceProbe.Contents`: `HarnessCommandFile` opens the file
> directly with a size bound, behind its own `IDeclared*File` interface in BuildingBlocks. The
> tunables reader mirrors that — `IDeclaredConfigFile`/`HarnessConfigFile`, same bound, same
> comment-tolerant parser, same reading of a malformed file as null. The *reasoning* below still
> holds, including the branch caveat; only the named seam was wrong.

**Decision:** the watcher reads a Folder's `.harness/config.json` through `IWorkspaceProbe.Contents`
— the same read-only, git-gated file access `IDeclaredCommandFile` already uses for
`.harness/commands.json` — rather than a raw filesystem read.

**Why:** it is exactly the prior art `/aio:propose` asks this document to name. `IWorkspaceProbe`
already answers "one file as text, for reading rather than diffing," already refuses a path git does
not list in that working directory, and already exists precisely to keep a module from touching the
filesystem directly. No new World interface.

**Consequence accepted:** like every declared file, `.harness/config.json` is read from whatever the
Folder's working copy has checked out right now, not a specific ref. In this product's normal shape the
main checkout stays on the trunk branch while `/aio:propose`'s own worktrees carry the feature branches,
so this reads the same file a person would see. A Folder deliberately left on a feature branch reads
that branch's config instead — the same caveat `IDeclaredCommandFile`'s own doc comment already states
for commands, extended here to tunables.

### Launch failure holds the task; it does not retry

> **Corrected during implementation: this names the wrong write path, and the hold is written.**
> `SetTaskHold` does decline `held: true` by design — but its reason is about a *control*: one that
> did both *"would let a misclick silence the loop's only handoff"*. Nothing in it forbids the system
> from applying a hold, and every `/aio:propose` and `/aio:implement` applies one with
> `gh issue edit`. The path that exists is the generic label write, `POST /api/tasks/{key}/labels`
> (`SetTaskLabels`), which filters nothing and is the same door the `reviewed:*` marks already use —
> `review-steps-per-worktree` calls it *"the existing task-label write path"*. The release-only
> control stays exactly as it is.
>
> **What still cannot be built is the message, not the hold.** This decision asks for the failure
> *"in the tool's own words"* on the task, and no seam can put it there: `ITaskSource` reads comments
> and cannot write one, so the words reach the host's log and nowhere a person looks. Closing that
> needs a new capability on the seam, outside this change's Seam. The requirement is left standing as
> written rather than weakened to match — see task 3.6.

**Decision:** a failed `POST /api/runs` call for an automatically-eligible task writes `status:holding`
back to the vendor (reusing `SetTaskHold`'s write path) with the failure's own message, and the watcher
does not attempt that task again until the hold is cleared.

**Why:** matches `AGENTS.md`'s house rule verbatim — "a failure halts, applies the hold, and comments
the reason in the words the tool used" — for the automatic path exactly as `/aio:implement` already
does for the manual one. Retrying blind would mean an agent that cannot start (say, a signed-out CLI)
burns a request every tick forever.

## Risks / Trade-offs

- **[Risk] A loopback HTTP call adds latency and a new failure mode (the server calling itself) that a
  direct in-process call would not have.** → Mitigation: the same call already happens from every
  browser tab today; the watcher is one more caller of an endpoint that already has to be reliable.
- **[Risk] Porting `sameRepo` twice means the two copies can drift.** → Mitigation: both are small,
  stable, already-documented string comparisons with no external dependency; the backend port cites the
  frontend file it mirrors in a comment, as this design does.
- **[Risk] Reading `.harness/config.json` from whatever branch a Folder happens to have checked out is
  a known inaccuracy, not a full fix.** → Mitigation: named explicitly above rather than hidden; the
  main checkout's ordinary state already matches this assumption, and a wrong read is a wrong `wipLimit`
  number, not a wrong launch of the wrong workflow.
- **[Risk] Two Folders whose `RemoteUrl` momentarily matches the same task (a fork, a mirror) could
  double-launch.** → Mitigation: `folderFor`'s own doc comment already accepts "first match wins" for
  this same ambiguity in the browser; the port keeps the same rule rather than inventing a stricter one
  this change was not asked to design.

## Open Questions

- Whether the watcher becomes its own tiny project under `root/` instead of living inside
  `AiOrchestrator.Server` — a naming/placement choice with no effect on the requirements above, safe to
  settle in code review.
