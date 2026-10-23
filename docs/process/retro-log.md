# Retro log

One entry per change that landed, appended when it is synced. **What the change taught**, not what
it did — the diff is in the history and the reasoning is in the item it came from.

Append only. Nothing here is edited once written, including the entries that turned out to be wrong;
being able to read what somebody believed at the time is the whole value of a log.

---

## 2026-08-15 · I7 — a run opens as a tab, not a route (#16, PR #17)

**A resolver belongs at the route, not at every call site.** Four screens navigated to `/runs/<id>`,
and the issue predicted six files would change to stop them landing on a dead end. Making the route
component itself resolve the id to a place left all four callers untouched and the change at two
files: the ones that own the behaviour. The instinct to rewrite every caller is the expensive way to
say the same thing once.

## 2026-08-15 · The plan moved to GitHub, and four documents went with it

**A file in the repository is on a branch, which is the one thing a coordination record cannot
afford.** `parallel.md` had already written the argument — *"a worktree is on its own branch, so an
edit here is invisible to every other agent until it merges"* — and applied it only to the claim,
while keeping an ownership table that fails the same way. A record is worst exactly when it is most
needed: two agents about to touch one file are, by definition, on two branches.

**What was kept was not what was expected.** Of 40 backlog items, 33 were done and the instinct was
that deleting them would destroy the reasoning. Checking three at random — A6's "keep the maximum of
each count", I5's "at 560×700 the terminal was left with 60", D7's whole-set reconciliation — found
every one of them already written in the comment of the code it explains, where it cannot drift from
what it describes. The document was the copy. The seven unfinished items were the only thing in
2,163 lines that could not be found elsewhere, and they are issues now.

Two things that were neither state nor code-specific — *read orca first*, and the five collision
rules earlier loops paid for — moved to `AGENTS.md`, because a rule an agent has to be told is a
rule that belongs where agents are told things.

**What this cost, and it is the honest part:** twice today `backlog.md` caught an error that reading
the code had not. I7 said a run already opens as a tab, after an issue had been written proposing
it; D7 said the label write goes to the vendor first. Both were `[x]` items — the archive working.
That value now has to come from closed issues, which are searchable but nobody browses. If the next
loop re-proposes something already shipped, this entry is where to look for why.

## 2026-08-15 · One status per task, derived from the labels (#23, PR #42)

**The hard case was not the six values, it was that two of them sit on one issue at the same time.**
The obvious derivation — walk the labels, return on the first `status:` match — passes every test
anybody would think to write, and is wrong against live data: #2 in this repository carries
`status:in-progress` and `status:holding` together, and GitHub returns a label array in whatever
order it likes. The rule that survives is a precedence, not a search, and the reason it was reached
before shipping is that orca's `getWorktreeStatus` is an early-return chain over exactly the same
problem. Reading it cost ten minutes; finding this by watching a list reorder itself would not have.

**Deriving instead of storing is not a style preference, and orca has the receipt.** It stores its
equivalent, so `src/shared/workspace-status-default-migration.ts` exists — a file whose entire job
is repairing status ids written to disk before the default set changed, carrying two named
bad-payload cases. Here the same value is a dictionary lookup on every read, and renaming a state is
a one-line diff with no snapshot to merge. That mattered twice over in a wave with two branches in
flight: a migration is the one edit two worktrees on one module cannot both make.

**Rendering a state is how you find out you disagree about what the states are.** The issue named
six. `CONTRIBUTING.md` and the `set-issue-status` skill name nine, and all nine labels exist in the
repository. Nobody had noticed, because until this change no surface drew the state at all — the
labels were grey chips and a chip is equally happy to say anything. Three of the nine now fall
through to `backlog` and keep their label as an ordinary chip. Nothing carries them today, so
nothing is wrong on screen; the disagreement is real and it is a decision about the loop, not the
code.

**Smaller, and it cost half an hour:** `TaskStatus` cannot be the name of an enum in a module called
Tasks. `System.Threading.Tasks.TaskStatus` arrives through the implicit usings of every file, and
the ambiguity is a build error rather than a matter of taste. `LoopStatus` in C#, `status` on the
wire, `TaskStatus` in TypeScript where nothing collides.

## 2026-08-15 · The folder list gets a name, and the + gets a menu (#29, PR #43)

**A seam that lists files answers "where does this land", not "what does it cost to finish".** The
issue said one file, `app/Sidebar.tsx`, and six were touched. Not one of them was a surprise once
the question changed: copy is in a typed catalogue, so `en.ts`; a popover's border and surface may
not be inlined in a screen, so the kit; and *Create worktree* offered from a header that does not
know which checkout has to ask somewhere, so the dialog and the three lines that own it. The issue
had already made this argument and had not applied it to itself — it dropped *clone repo* precisely
because an item opening a dialog that cannot finish is worse than a menu of two. The same test
applied to the two that stayed is what set the real file list. **Write the seam as what must be true
for the change to finish, and the file list falls out of it.**

**When a check names a mechanism, the mechanism is a hypothesis.** This one asked for
`event.stopPropagation()` so the row's two icons would not collapse the group, and that is what
orca does — three layers of it: a selector matching the actions *container* so the gaps between the
icons are dead too, `stopPropagation` on click, pointer-down and key-down, and a fourth on the menu
content, because a Radix portal leaves the DOM tree and stays in the React one. It has no choice;
that row is also a drag handle. This rail is not, so caret, icon and name became one `<button>` and
the icons became its siblings — and the bug class disappeared rather than being handled. Reading
orca was what made the difference visible: the value was not its code, it was seeing how much
machinery its constraint costs, and then noticing the constraint is absent here.

**A flake and a failure are indistinguishable until you run the test alone.** The first full suite
gave 228 of 229, failing on an end-to-end assertion that "Staged loop" is visible. Nothing about it
looked flaky, and "a red check is a stop" nearly stopped the change. It waits for the screen's
subtitle and then asserts on a workflow row that arrives with a second query and no wait of its
own — the folders test directly above it waits for the element it asserts on, which is the
difference. On the untouched tree, run by itself, it passes in three seconds. **Stash, run the one
test, read the assertion** is thirty seconds and it is the only cheap way to tell a stop from a
coin toss.

**Two things about running the product from a wave worktree, both of which the next agent will
hit.** The app seeds the checkout it is started from, so a screenshot taken here records a folder
named after the worktree — the harness, photographed as if it were the product. And
`docs/design/current/` is seven binaries that every rail change moves at once, which two branches in
one wave will both rewrite: `tasks.png` was left at #23's capture rather than conflicting over it,
and is now one rail out of date on purpose. **Smaller, and it cost twenty minutes:** a dev server
started under a background timeout is killed mid-verification while Aspire's proxy keeps holding the
port, so the app does not fail — it hangs, and looks exactly like a product bug.

## 2026-08-15 · One folder, one scope, and a row of tabs (#24, PR #45)

**A control that opens a popover cannot live inside a scroll container.** The tab strip is
`overflow-x: auto`, because a strip of sessions has to scroll. The `+` at the end of it drops the
kit's shared menu, which is absolutely positioned — and a scroll container clips what is absolutely
positioned inside it, so the menu came out as a four-pixel sliver against the right-hand edge of the
window. Nothing about the code says so: the component is the right one, the rule about not building
a popover inside a screen was followed, and the markup reads correctly. The fix is where the trigger
lives, not what it is — the tabs scroll in their own box and the `+` sits outside it, which also
means it stays reachable once there are more tabs than fit. **Found by clicking it.** The suite has
no opinion about a menu that renders four pixels wide.

**Half of what looks like architecture in orca is a missing browser.** It funnels roughly forty
entry points through one `activateAndRevealWorktree`, and keeps a hand-rolled navigation stack —
a linear array capped at fifty, a re-entrancy flag, page sentinels mixed in with worktree ids —
because it has no router at all. Reading that as a pattern to copy would have produced a store slice
holding the selected worktree beside a URL that already holds it, and the two would disagree the
first time either was used. The parameter *is* the funnel here: the chips write it, the rail writes
it, and the browser owns the history. **The useful question when reading orca is not "what did it
build" but "what is it compensating for"** — and the answer is sometimes a thing this app gets for
nothing. What did transfer was the part that has nothing to do with Electron: it validates a
remembered tab twice, once on load and again at render, and the render-time half is what stops a
panel going blank.

**A screenshot moves for reasons that are not the diff.** `capture.sh current` retakes seven
screens; all seven differed, and five of them because this machine has gained folders and worktrees
since the set was last taken, not because of anything in the change. "Commit only the files that
actually differ" reads, in that moment, as "commit all seven" — and committing four while leaving
`tasks.png` alone for #40 would have made the set less consistent than leaving all five. The rule
that works is narrower than the one written down: **commit the screens your change moved, and say
on the issue which ones the machine moved underneath you.**

**A tab can ship before its contents, and the empty state is the seam between two issues.** Three of
the six fixed tabs belong to other items, and the row has to exist before anything can live in it.
What made that honest rather than a stub was the copy: each says what is true now *and where the
thing is today* — the file tree is still the rail, this project's tasks are on the Tasks screen. An
empty state that only reports emptiness would have been three dead tabs; one that names where to go
is a smaller version of the feature, and it is the design system's own rule about empty states doing
the work.

## 2026-08-15 · The inbox learns to filter by kind (#40, PR #44)

**"Server-side or client-side?" is answered by asking who holds the set the count is over.** The
issue's Seam said `ListTasks` *if* the counts should be computed there — the one open question, left
as taste. It is not taste. The screen's own rule is that a count is over everything and only the
list narrows, so a chip never reads as "there are none"; and the browser is only ever sent the list
*after* the filters ran. It cannot count what it was not given. Once the question was "which side
has the whole set", the answer stopped being arguable and the code got shorter. **Ask it of any
filter that shows a number beside it, before anything else.**

**Three gates passed over a NUL byte in a source file.** Joining the sorted kinds on `U+0000` looked
like a careful choice of an unambiguous separator. `pnpm typecheck`, `pnpm lint` and `pnpm build`
all passed — the string is legal JavaScript — and what noticed, an hour later during a rebase, was
`git diff --stat` printing `queries.ts | Bin 17002 -> 17797 bytes`. **The gates check the language;
they do not check that the file is still a file.** A source file that diffs as binary is one no
reviewer reads and no `grep` finds.

**Reaching for a separator was the smell, twice, and both times the answer was to have none.** The
wire could have joined the kinds on a comma and the cache key on a NUL; a vendor label is free text
somebody typed, so every separator is eventually inside one. Repeated `?kind=` on the wire and the
sorted array whole in the key have no such case. Orca had already concluded this in its own words —
`quoteGitHubSearchValue` exists solely because `label:"needs review"` breaks the round trip.

**What orca was worth here was a hazard, not code.** Its `cmd-j/palette-filter-options.ts` pins the
*selected* options so they survive falling out of the vocabulary, and calls an option that can only
ever yield an empty list a trap. The matching case here is a sync removing the last task carrying a
selected kind: the chip disappears, the selection does not, and the screen is empty with nothing to
press. That is a bug nobody would have found before shipping — the whole set has to change under a
live filter — and it cost one line to not have.

**`capture.sh current` cannot work under `aspire run`, and its comment says it can.** It shoots
`localhost:5173`, which is the port a hand-started `pnpm dev` takes; Aspire allocates the Vite server
a random one (54054 here) and proxies it through the Server, so the script screenshots a connection
error at full size and reports success. The Server's own origin — `localhost:5080/tasks` — is what
works, and is also the arrangement the product actually ships. Two smaller ones beside it:
`aspire run --apphost <relative path>` finds every AppHost under the parent directory and needs an
interactive prompt it cannot get from a non-tty, so pass the absolute `.csproj`; and a second wave
worktree cannot bring the app up at all while the first one has it, because `5080` and `5173` are
fixed and the Postgres data volume is shared by name.

## 2026-08-15 · Files becomes a tab, and the tree says when it was read (#26, PR #46)

**The suite is 235 tests, and a fresh worktree reports 124 of them as a pass.** `dotnet test
src/AiOrchestrator.slnx` in a newly created worktree failed three `EndToEndTests` because
`src/frontend/node_modules` did not exist yet; `pnpm install` fixed all three. The number is the
thing to check, not the word at the end: #24's implementation comment reports *118 passed* as a full
run, which is the five functional assemblies and omits `InfrastructureTests` (93) and
`ArchitectureTests` (18) — the two that enforce MOD001–005 and CQS001, so the run that skipped them
is exactly the run that proves nothing about the boundaries. **Know what the total should be before
you read the result**, because a subset and a suite both end in "Passed!".

**`prettier --write <file>` formats the file, and your ownership is a block inside it.** `en.ts` is
one of the 18 files already failing `format:check` on `master`, so running prettier over it to tidy
this change's own lines also rewrapped `tasks.worktreeOff` — a string in somebody else's screen.
Typecheck, lint and build all passed; nothing but reading the diff would have caught it. AGENTS.md
rule 3 already says do not tidy a neighbour's, and the way that rule gets broken is not by deciding
to break it, it is by running a formatter with a filename argument. **Read the diff of a shared file
before committing it, and expect the tool to have edited more than you did.**

**orca not having the feature was the argument for having it.** The tree needed a "when was this
read" stamp, and orca shows none anywhere — its `DirEntry` carries no time at all, and staleness is
structural rather than temporal, a per-directory revision token plus a watcher-overflow marker. That
reads as a verdict against the stamp until you find *why*: `@parcel/watcher` pushes filesystem events
in ~150ms, so a clock would be noise. The decisive file was the one that shares this product's
shape — `mobile/src/files/MobileFileExplorerPanel.tsx`, a thin client over RPC, which has no watcher,
no polling and no stamp either, and therefore cannot tell its reader a tree read a second ago from
one read ten minutes ago. **The standing rule says write down what orca knows that you do not; the
case worth naming is when what it knows is that its constraint is not yours.** Read the reason under
the absence, not the absence.

## 2026-08-15 · The rail learns what is running, from runs (#22, PR #47)

**The item that deleted four always-empty fields specified a fifth.** #22's whole argument is that
`Worktree.State` was assigned in one place — its own initialiser — so four controls read a fact
nobody wrote. Its own design then asked the live row for `/aio:implement · step 3 of 5`, fed by
`CurrentStep` and `TotalSteps`. `AddStep` has exactly one caller in this repository and it is
`RunsFixture.cs`, so every real run is at step 0 of 0 and that line would have shipped an
always-zero counter — the same bug, drawn in the same rail, by the change removing it. The check
that catches this is one grep and it is not the one you reach for: **before writing a line that
reads a field, find the code that writes it**, and count test fixtures as nothing. A proposal is not
evidence that the data exists; it is the place the assumption gets laundered into a spec.

**Four gates passed on a chip that read `Live 2` for one agent.** Build, typecheck, lint and 235
tests were all green while the header counted matching *rows* instead of distinct runs — and a
worktree added as a folder in its own right is listed twice, once under its parent and once as
itself, so one run lit two rows and the count believed both. Nothing in the diff looks wrong; the
join is correct and each lit row is true. **It was caught by opening the screenshot and reading the
number**, which is also how the first capture was caught showing `Live 0` (the run under it had
failed 16 seconds in, so the picture was honest and the setup was not). Two bugs this change found
in itself, both by looking at the rendered product, neither by any gate — which is the standing
claim in `implement.md`, now with a count.

**The formatter's collateral was five hunks in four files, and #26's retro only named one.** The
lesson recorded last time was `en.ts`; the reflows were also in `Sidebar.tsx`, `FoldersScreen.tsx`
and `queries.ts` — style objects and `onSuccess` bodies rewrapped in code this change never touched,
every one of them passing all three frontend gates. The tell is a hunk that changes only whitespace,
and the fix is mechanical: **read the diff of every shared file hunk by hunk and revert the ones you
did not mean**, until `en.ts` is a pure addition. The rule is not "be careful with prettier on
`en.ts`", it is that a formatter's blast radius is the file, on every file you ran it over.

## 2026-08-15 · The vendor's session id, recorded per run and terminal (#2, PR #15)

**The implementation diverged from the accepted proposal, and nothing re-checked the new
assumption.** The item argued for reading OpenCode's SQLite at
`~/.local/share/opencode/opencode.db`, the way orca does. What shipped asks
`opencode session list --format json -n 25` instead, and the reasoning is sound — orca lists every
session on the machine to fill a browser and cannot pay for a process per refresh, while this asks
once at the end of one run. But the swap moved the change's entire correctness onto flags nobody had
run. **A read-back seam fails silently by construction**: if `--format json` had moved, `List`
returns null, `SessionId` stays null forever, and every test still passes, because the tests
exercise the pure `Select` against fixed JSON and never spawn the vendor. Four gates and 250 tests
cannot see it. Asking the real CLI takes one command and settled it — `-n/--max-count` and
`--format json` exist in 1.18.18, `created` is epoch milliseconds, and a real spawn in a fresh
directory was attributed to exactly one session. **When implementation replaces the mechanism the
proposal was argued from, the prior art is no longer evidence for what shipped; re-verify the new
mechanism against the thing itself, or the divergence is an untested assumption wearing a citation.**

**The list of files two branches both touch is computed, not remembered.** This rebase was expected
to collide in seven files across `GetRun.cs`, `StartRun.cs`, `IAgentSessionReader.cs`,
`IWorkspaceProbe.cs`, `IWorktreeManager.cs`, `GitWorkspaceProbe.cs` and `GitWorktreeManager.cs`. Two
actually collided. Master had touched five of those, but this change never did — and two of the
seven were named at paths that do not exist, since `GitWorkspaceProbe` and `GitWorktreeManager` live
under `Infrastructure/Git/`. The real answer is one command against the merge-base, intersecting
`git diff --name-only <base> HEAD` with `git diff --name-only <base> origin/master`, and it costs
seconds. **A remembered collision list decays into a list of files somebody was once worried
about**, which budgets fear for five files that merge clean and says nothing about the one that does
not. The same command settles the migration hazard AGENTS.md warns about: master's two migrations
were Workspace's, `Runs/Persistence/` was untouched, so the snapshot never forked and
`20260815001432_RunSession` needed no regeneration.

**516 lines of finished work sat in a working tree for a day with no commit under it.** It built, it
passed, and it existed in exactly one place that no `git` command can restore — a stray `checkout`
or `worktree remove` would have taken all of it, and the branch's only commit was the proposal. The
fix is not discipline, it is ordering: **commit what exists before improving it**, because a commit
is the cheapest thing in git and the only one that makes the next step reversible. Rebasing first
would have put an irreplaceable working tree into conflict resolution. Committing first cost one
command and made a backup ref possible, which is what `git range-diff` then compared against to
prove the rebase dropped only the intended hunk. One tail worth recording: `gh pr merge
--delete-branch` fails with `fatal: 'master' is already used by worktree` when another worktree holds
the branch — **the merge lands and only the cleanup fails**, so read which half of that command
failed before re-running anything.

## 2026-08-15 · The project screen opens on what is waiting (#25, PR #49)

**Four green gates and 250 tests described a screen that was empty in the product.** Overview was
built to read `GET /api/tasks?folder=<path>`, exactly as the item's Seam specified, and the Seam was
wrong in a way no gate can see: `ListTasks` filters on a task's own `FolderPath` column and
*nothing in the product ever writes it*. Every mirrored task carries `""`, so the scoped request was
not narrower than the unscoped one, it was empty — in every project, permanently. Typecheck, lint,
build and the suite all passed on a default tab that could only ever render its own empty state. The
header's inbox had the same fault wearing different clothes: it matched holds to folders on the same
dead column, so the row named the vendor where the project belongs ("GitHub · #12") and the click
fell through to the global task list instead of opening the project the hold was in. **A filter that
returns nothing and a filter that is wired to nothing are the same colour from inside a test**; only
starting the app told them apart, and the fix was already in the codebase — the Tasks screen had met
this and written the fallback, with a comment saying why.

**The prior art for a seam is often one directory away, not in orca.** AGENTS.md sends you to orca
for the cases it knows that you do not, and that is right for mechanism. This one was cheaper than
that: `folderFor` already existed in `TasksScreen.tsx`, carrying a comment that says nothing
attributes a task today. Reading the screen that already solves your problem is the first search,
and its comments are the design note. What the change added was not the rule but one copy of it —
the count on the tab, the three lists under it and the rows on Tasks now derive from one function,
because three answers to "which project is this task in" is a screen that argues with itself.

**A verification the tooling claims to do is worth re-deriving once.** `docs/design/capture.sh`
reports success while writing a picture of nothing: its guard curls the API on `5080` and then
shoots at `5173`, which Aspire reassigns to a random port. The script has no way to notice, because
the thing it checks and the thing it photographs are different servers. **A guard that probes a
different address than the operation it guards is not a guard**, and its green is the most expensive
kind of false — it is trusted precisely when nobody is looking at the output.

## 2026-08-15 · A worktree gets its own dev database (#50, PR #52)

**Ask the platform before you build the thing, and ask it with an experiment rather than a
reading.** The Aspire 13.4.6 CLI ships `--isolated` — "randomized ports and isolated user secrets,
allowing multiple instances to run simultaneously" — which is this problem's exact wording, and if
it had isolated the data volume too, this issue would have been documentation and the code would
have been a hand-rolled copy of something already in the box. It does not: with the flag, the
dashboard moved to a random port and the secrets were the run's own, and the container still mounted
**the exact string handed to `WithDataVolume`**. The flag randomises what the CLI owns; a literal in
the AppHost is not something it owns. Ports and user secrets left the issue's scope on that
measurement, and the volume stayed — a smaller change than the one that was written up, arrived at
by measuring instead of by reasoning about a help string.

**An experiment that would damage the thing under repair can usually be re-cut to answer the same
question.** Measuring `--isolated` the obvious way means starting the app from a worktree with the
real literal in place, which is precisely the second postmaster over the user's `PGDATA` that cost
the run history that morning. Changing the literal to a decoy first asks a *better* question — does
the CLI rewrite an explicitly named volume, yes or no — because it isolates the variable instead of
confounding it with the checkout's identity, and `aio-postgres-data` is never mounted at all. The
same re-cut answered the untouched-main-checkout check: a `.git` **directory** placed where the walk
would find it, plus a manifest publish, runs the real derivation and starts no container.

**Reading orca bought a bug that all four gates would have shipped.** Its `repo.ts` realpaths both
sides before comparing checkouts, because on macOS a path reached through a symlink does not compare
equal to the same path reached directly. Here there is no second path to compare against — the
comparison is against yesterday's run — so the identical slip surfaces as a different digest, a
different volume, and **a database that is silently empty for no reason anybody can see**. Nothing
fails; the data is simply not there. Orca also settles main-versus-linked by reading `.git` as file
or directory exactly where spawning git is too expensive, and keys its per-worktree state by path
with the branch deliberately never part of an id — which is the same answer this reached, now with a
second machine's experience behind it rather than only an argument.

**A precedent is a mechanism plus a reason, and copying only the mechanism is how it misleads.**
`ApiServiceFixtureBase` has run Postgres on a throwaway Testcontainers instance since the beginning,
and it was tempting to read that as "the dev path should be ephemeral too". A test wants a
guaranteed-empty database because it asserts what it seeds; development wants continuity because the
person is the one seeding, by hand, through the UI. The same mechanism serves opposite reasons, and
the issue had already priced the difference: verifying #25 needed a folder registered by hand.

**`Persistent` is a promise, and it keeps it after everything else has been isolated.** A container
declared persistent was still up after a clean `aspire stop`, not a kill — which is why the orphan
half of this was never going to be fixed by any amount of isolation, and why opting *out* is a line
you delete rather than a line you add: Aspire's default lifetime is already `Session`.

---

## 2026-08-15 · The gate becomes one command (#51, PR #54)

**A gate you have to assemble is a gate that gets assembled wrong, and the wrong assembly reports
success.** The four commands in `AGENTS.md` were correct, unambiguous and copy-pasteable, and an
agent still ran `dotnet test` without the solution — which does not fail, it runs a subset. 118 tests
were reported as a full run of 250, the two suites policing the module boundaries were among the 132
missing, and #24 merged behind that number. The fix was not a clearer list. `aspire do ci` has no
halves to pick from, and `WithTestStep` takes the solution as a required argument with no overload
that omits it: the thing that was forgotten is now the thing the compiler will not let you forget.

**The dependency graph found a bug the four commands could not have.** `EndToEndTests` starts the
AppHost, which starts the Vite dev server, so it needs `src/frontend/node_modules` — and this
worktree had none, so `dotnet test src/AiOrchestrator.slnx` scored 247 passing and 3 failing on the
same line. Expressed as commands that is a footnote about running `pnpm install` first, which is a
prerequisite somebody eventually forgets. Expressed as a graph it is one edge: the .NET test step
depends on the whole `ci-install` stage, so a JavaScript install happens before a .NET test because
each half declared what it needs and neither had to know about the other.

**A gate that is red before you write it is worse than no gate.** Both formatters were candidates
and both were already failing on `master` — `pnpm format:check` on 17 files, `dotnet format
--verify-no-changes` on RCS1139 across four projects, which also means the formatter and the build
have quietly disagreed for some time. Adding either would have shipped a step that is always red,
and a step that is always red trains people to skim the output, which is how the one that matters
gets missed. They are #56, and adding them later is one call each.

**Aspire's pipeline namespace is flat and shared, and it fails at two different times.** A stage
called `build` throws at composition — *a step with the name 'build' has already been added* — and
renaming it to `build-server` throws at execution instead, because Aspire generates a build step per
resource. Two collisions, two different moments, one cause. Prefixing everything `ci-` removes the
class, including the collisions the next Aspire version would introduce.

**`aspire do` runs in publish mode, which is not where the app is declared.** The Vite resource sat
behind `IsRunMode`, so the pipeline saw an application with no frontend and `ci` silently lost half
of itself — silently, because a stage with nothing under it passes. Declaring the resource in both
modes and excluding it from the manifest outside run mode keeps publish output identical, and the
general shape is worth remembering: **a step that has nothing to do is indistinguishable from a step
that passed.**

**Reading orca was worth it for one line and one refusal.** The line is `Kill(entireProcessTree:
true)` — orca walks `ps` for descendants *before* signalling the root, because once the root dies its
children reparent to pid 1 and can no longer be found, and `pnpm` is a launcher for node exactly as
its agents are launchers for node. The refusal is its `$SHELL -ilc` probe for a login shell's `PATH`,
which is right for a GUI-launched Electron app and wrong here: `aspire do ci` is typed into a
terminal, and probing for a better `PATH` would let the gate run a different pnpm than the developer
beside it — which is precisely the disagreement the gate exists to catch.

**stderr is not an error, and saying it is costs you the ones that are.** The first green run printed
four red lines: Vite's advice about chunks over 500 kB, which every well-behaved build tool writes to
stderr. A passing step full of red teaches people that red means nothing here. The exit code says
what failed; stderr is a warning.

**From a worktree, `aspire` writes its settings into the main checkout.** It walks up for the
repository root and a linked worktree's `.git` is a file, not a directory, so the walk goes straight
past it — the same file-versus-directory distinction #50 used deliberately, met here as a surprise.
The file remembers the last `--apphost`, so a worktree that runs `aspire` and is then deleted leaves
the main checkout pointing at an AppHost that is gone.

**And the honest half: this creates no CI.** There is still no `.github/workflows` and `gh pr checks`
still reports zero on every pull request, so the documents now say `aspire do ci` passed rather than
"CI is green" — a sentence that was true of every change in this repository, including the ones that
were not.

## 2026-08-15 · Each worktree gets its own terminal tabs (#20, PR #59)

**When a worktree lives inside its checkout, containment is the wrong path test and it fails
looking right.** The instinct for "is this session in this scope" is `isPathInsideOrEqual`, and this
repository's worktrees are at `.worktrees/<name>` under the checkout — so that test would have given
the checkout all three shells and left every worktree's strip empty. The checkout screen is the one
anybody verifies first, and on it the answer would have looked correct. Orca knows this case and
sorts its candidates by longest normalised path; here the right answer was narrower still, because
`workingPath` names a scope root rather than an arbitrary directory below one, so equality is not a
shortcut but the correct comparison.

**The path to compare against is the one the server resolved, never the one in the URL.** `?worktree=`
is a caller's string; `GetFolder` answers with the checkout when it matches none of the folder's own
worktrees, so a stale link renders the checkout's tree, runs and branch. Filtering the strip on the
raw parameter would have emptied it beside them — "nothing is running here" instead of "that link is
stale", which is the silent-empty shape #50 met with an unrealpathed digest. Both sides being one
database column read twice is also what makes ordinal equality right and any normalising wrong: a
client that lowercases or trims would invent a difference that cannot exist and mask a real one.

**Two thirds of the item had already been built, by deletion rather than by the fix it described.**
#24 removed the Commands tab's private directory state and #26 removed the file rail, so "the chip
follows the screen" and "running elsewhere moves the screen there" named controls that no longer
exist — and every line number in the issue was wrong within a day. The item was written against the
one file three issues are queued behind, which is exactly where an issue goes stale fastest. Reading
the file before the issue is the cheap half of the fix; the expensive half is that a bottleneck file
should be split, and nobody can propose that from inside a change that also has to land.

**`aspire start --isolated` runs the projects as Production, and the documented inner loop yields a
404.** `ASPNETCORE_ENVIRONMENT` is unset on the `server` resource, so the development branch that
proxies the Vite dev server never runs and `MapFallbackToFile("index.html")` answers from a `wwwroot`
that does not exist. The AppHost's own `launchSettings.json` sets it; the Server's does not, and
`--isolated` does not propagate the AppHost's. The gate cannot see this — 250 tests pass against a
server nobody can open — which makes it the second time this week that the thing all four gates
missed was found by opening the app.

**The two-worktree claim is no longer untested.** Two AppHosts ran simultaneously from two
checkouts, each with its own Postgres container on its own port; neither disturbed the other, and
`aspire stop` took one container and left the other. What #50 designed for is what happens.

## 2026-08-15 · The rail is told when a folder's worktrees change (#19, PR #58)

**Two of the five things the issue asked for had shipped 36 minutes after it was filed, and the
issue never noticed.** `e2cf443` gave `useFolders` a twenty-second poll and made `useStartRun`
invalidate `["folders"]` — both named in the Seam section as work to do. Nothing is wrong with an
observation going stale; what is wrong is finding out while implementing it. Reading the two files
an issue cites before writing a line of it costs minutes, and it changed the argument here rather
than weakening it: the delay was never "the rail shows what it showed", it was up to eighty seconds
made of two independent waits, and a poll that is wrong 79 times out of 80.

**"Did this change" belongs to whatever moves the rows, not beside it.** `RecordProbe` already
reconciles the worktree set whole, so it returns whether the set moved and the poller reads that.
The alternative — comparing two lists of paths in the caller — is a second predicate over the same
data, and a second predicate is a second chance to disagree with the one that decides which row is
actually added. Orca has exactly that scar: two `areWorktreePathsEqual` implementations, one of
which folds macOS `/private/tmp` and one of which does not.

**And orca's `realpath` is not where `AGENTS.md` says it is.** The standing rule reads "its path
comparison `realpath`s both sides first". Read closely, that is true of its **watch roots** —
`canonicalizeExistingPath` — and not of worktree path equality, which is lexical
`normalize(resolve(…))` plus a hardcoded `/private/tmp` → `/tmp` fold, because `realpath` in a hot
comparison costs a syscall and fails on a directory that has just been deleted. The rule is still
right about the class of bug; it is wrong about the line of code, and a rule that cites the wrong
line teaches the wrong fix. Here neither normalisation was correct: both sides are strings this app
stored from git's own output, so normalising one side would invent changes that did not happen.

**The announcement has to come after the save, and that is the only ordering this design can get
wrong.** Told to refetch while the sweep's transaction is open, a client reads the rows the sweep is
about to replace, caches them, and stays wrong until the next real change — with the message that
would have corrected it already spent. Orca solved the same trap one layer up, invalidating its scan
caches synchronously before sending. Pinning it needed a test that reads the database from inside
the announcement, because there is no other moment to observe.

**A hidden browser tab is not a quiet one, it is a dead one, and it produced a false negative that
looked exactly like a broken feature.** The first end-to-end check failed: the server reconciled, the
rail did not move. Chrome throttles timers in a background tab, which stops SignalR's keep-alive, so
the server had timed the connection out — and stops the twenty-second poll too, so nothing was left
to paper over it. An independent SignalR client from Node, which is not throttled, showed the server
had sent exactly one message at the sweep. Two instruments were what separated "the server did not
send" from "the browser did not hear", and with one instrument the wrong half would have been
rewritten. That throttling is also the argument for keeping the poll: it is the floor under a socket
that has quietly gone.

**The stale bundle is a worse failure than the 404 beside it.** `aspire start` runs the Server as
Production, so the Vite proxy branch never runs — #20 met that as `GET /` returning 404, which at
least says something is wrong. Here the CI step's own `pnpm build` had left a bundle in `wwwroot`, so
the same misconfiguration served an app that opened, rendered and behaved normally while running
frontend code from before the change. `pnpm build` and `aspire resource server restart` is the way
through, and the gate is green either way.

---

## 2026-08-15 · An isolated instance becomes an app you can open (#61, PR #62)

**The suite had already found this bug and written the finding in the wrong place.** `AppFixture`
carried `WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")` under a comment describing the
exact 404 this change was filed for, weeks before it was filed. Patching the composition inside the
harness that tests it does not fix the composition — it blinds the gate to the defect, which is how
`aspire do ci` counted 259 green against a server nobody could open. A workaround in a fixture is a
bug report nobody filed. The rule that falls out: when a test has to configure the product to work,
that configuration belongs in the product, and the comment explaining it belongs beside the change
it forces.

**The environment could only ever have gone in one place, and it was measurable rather than
arguable.** Aspire starts a project resource with `--no-launch-profile`, printed in the resource's
own log, so `Server/Properties/launchSettings.json` is never opened in this loop. The whole "on the
resource or in the launch profile?" question dissolved on reading one line of output. Two hours of
argument were available and not needed.

**Deleting the stale bundle is not the remedy — it is a second failure.** With `wwwroot` removed
underneath a built binary the Server does not start at all: `DirectoryNotFoundException` from
`StaticWebAssetsLoader`, inside `WebApplication.CreateBuilder`, before line one of `Program.cs`,
because `dotnet run --no-build` reuses a manifest that still names the directory — and an
incremental `dotnet build` does not regenerate it. So the answer to a stale artefact was not to
validate it or delete it but to stop reaching it, and to have the gate assert *which branch served
the document* rather than that something answered. `/@vite/client` is in the dev server's output and
in no built bundle; it is the only thing a rendering screen cannot fake. That is #63.

**A flake found at the gate is a finding, and re-running is how it stops being one.** The first `ci`
run failed at 252 ms on a pre-existing assertion that read a data row the instant the screen's static
copy appeared. Two more runs were green, and green twice is exactly what makes it tempting to call it
unrelated and move on. It mattered here because this change makes those browser tests the regression
net for the environment the AppHost declares, and a net that fails at random is one people learn to
shake twice.

**Orca's answer was the shape of the fix, not the fix.** It refuses to trust a grandchild to inherit
anything — its hook scripts read coordinates from an endpoint file rather than the environment, and
one of them walks `/proc/$pid/environ` up the parent chain to recover what the middle process
stripped — and it writes no port literals anywhere, discovering them or passing them explicitly. Both
map straight onto a Server that is a grandchild of the shell and a Vite proxy that named 5080. Where
it had nothing to offer was the stale artefact: its own bundled web client is an `existsSync` with no
build stamp, served `immutable`. That silence was worth as much as the answers — it is why this
change stopped trying to validate the bundle.

## 2026-08-15 · A folder can be removed, and nothing on disk moves (#21, PR #65)

**The one-line comment above a permission constant is the specification, and reusing the constant
rewrites it.** `ManageWorktrees` says it is for "the operations that change the repository", and
removing a folder changes nothing in the repository. Reaching for the nearest existing permission
would have cost nothing at the call site and made that sentence false — which is how a permission
stops describing anything and becomes a name that happens to be held. The cheaper-looking option was
the one that destroys information.

**Where the guard cannot go is more informative than where it went.** The refusal while runs are
live sits in the dialog because the handler may not read the Runs schema. Orca, asked the same
question, turns out to have **no guard at all** at this altitude — its Remove Project dialog inspects
nothing and its main-process handler validates nothing — while the one place it *does* gate, deleting
a worktree, puts the gate in the backend with an explicit force waiver, because a gate with no waiver
wedges the resource. Neither shape was copyable, and that is the finding: an advisory guard is not a
weaker version of an enforced one, it is what is left when the boundary you chose forbids enforcement.
Writing that down is the difference between a compromise and an oversight.

**An ordering that looks like a rule was a fact about the handle.** Terminals are closed before the
row is deleted, and the reason reads like a principle until orca is read doing the exact opposite —
row first, PTYs after — for a project removal that also touches nothing on disk. It can, because its
renderer holds the pty ids directly; here the folder id is the only route back to a session, so
afterwards there is nothing left to close with. The general rule is not "close first", it is "know
which of the two things you are deleting is the address of the other".

**The item's own observation had already been fixed, and the code said so by name.** Point 4 argued
from `liveCount` always being `0`; #47 had deleted the column and left `Worktree.cs` saying
*"`liveCount` came within one grep of becoming a live-run guard in #21 on the strength of looking like
a fact"*. An issue is a photograph of a codebase on the day it was written, and reading the files it
cites before believing it is not scepticism, it is the point of the grill.

**A screenshot set with no colour scheme pinned compares the machine, not the design.**
`capture.sh` fixes the window at 1440×900 and explains why, and then lets headless Chrome inherit
`prefers-color-scheme` from the OS — where `useTheme` follows it, because a headless capture has no
profile and therefore no stored choice. The first retake came out dark against six light siblings.
The same reasoning that produced the width comment reaches one line further than it was taken.

## 2026-08-15 · Tasks gets a board that writes back (#28, PR #64)

**A seam with no caller is not a finished seam, and four green gates said otherwise.**
`ITaskSource.ApplyLabel` had an interface, XML documentation explaining why it is the single write,
and four working implementations — GitHub, Azure DevOps, Jira, Linear — from the day the connectors
landed. `rg "ApplyLabel" src/modules src/frontend` returned nothing. Every test passed, every
analyzer was quiet, and the product could not change a label. The backlog had marked the capability
done because the seam existed; what was missing was the one file that drives it. The check that
would have caught it is not a test — it is grepping the seam for its callers, which is now the
thing this change recommends doing to the other `World` interfaces.

**Orca made the opposite choice on the same gesture, and the reason is which side owns the record.**
Its kanban drops a card into a lane and syncs the state to Linear best-effort, *without reverting*:
"board moves are local-first; slow provider reads must not let an older board move overwrite a newer
local status in Linear". That is right for a desktop app whose board is its own. It is wrong here
for a reason already written down in this repository — `MirroredTask` is documented as *a mirror,
not a record* — so a card left where the vendor disagrees would be this product lying about somebody
else's tracker. The useful lesson was not the code; it was that "optimistic" is not one decision. It
is two, and the second one — what happens on failure — is decided by whose copy is authoritative.

**The vendor's read path can lie about the write you just made.** Orca pays for this with a whole
pending-mutation registry, generation counters and a bounded re-poll, because GitHub's search index
lags and `gh` caches URLs for 60s: a refetch after a *successful* write returns the old value and
clobbers it (STA-3343). The analogous liar here is this product's own mirror, which by design does
not hold the new label until the next poll. That is the entire reason `useSetTaskStatus` does not
invalidate on success — the obvious, tidy `onSettled: invalidate` would have sprung every card back
to where it came from, for two minutes, after a write that worked. It would have looked exactly like
the failure path and been reported as one.

**Adding before removing is not fussiness; it decides what a half-finished write leaves behind.**
Two labels on one issue resolve to the furthest along, and none resolves to Backlog. So the order
that fails safely is add-then-remove: a vendor that dies between the calls leaves a task carrying
two states rather than sitting in Backlog on somebody else's board, having been put there by a
gesture nobody made. The test asserts the order, not just the set.

**The screenshot found two defects the suite structurally could not.** A real connector's scope —
`issues, pull requests · owner/name` — is long enough to wrap the header, and `Sync now` wrapped
away from the layout group it belongs with: two halves of one corner, in two places. And a project
reading issues *and* pull requests drew two source badges, against the very invariant the scoped tab
exists to restore, because the fold from kind to connector was written on the server twice and in
the browser not at all. Neither is expressible as an assertion. Both were obvious in a picture.

**A screen you cannot link to is a screen you cannot photograph.** The board and the project's Tasks
tab were local state, so the only way to reach either was to press something — which headless Chrome
cannot do, and which is why `capture.sh` could never have taken these two shots whatever ports it
used. Making them addressable was three lines, following `?task=` and `?run=` exactly. Worth
remembering the next time a screen's state feels like nobody else's business: the review needs to
point at it too.

**The recovery for a broken filter existed and the screen that needed it most was not using it.**
`ListTasks` filters `?folder=` on `MirroredTask.FolderPath`, which nothing writes, so the request
comes back empty in every project — #25 found that and built `folderFor` for Overview. The Tasks
screen went on sending `?folder=`, so "Start run" on a project's Overview navigated into a guaranteed
empty list. A shared fix is only shared once every caller uses it, and nothing tells you which ones
do not.

---

## 2026-08-15 · The file reader moves out of the screen (#68, PR #69)

**"It takes everything through props" is a claim about props, and a file split needs a claim about
module scope.** The issue measured the block carefully — two ranges, 522 lines, both entry points
receiving everything through props and closing over nothing — and it was right about props. It
missed `clock`, a nine-line helper at the top of the screen whose only call site is the
`read HH:MM:SS` stamp inside `FilesTab`. Nothing about the props signature could have shown it,
because it is not a prop; only grepping the moved text for identifiers it does not define does.
The cost of missing it is not a compile error you can shrug at either — leaving it behind trips
`noUnusedLocals`, and the obvious repair, exporting it from the screen, makes a cycle out of a file
the screen already imports from. The measurement that decides whether a split is cheap has to be
"what does this text reference that it does not define", not "what does it take as arguments".

**A move is the one change where reading the whole diff is the check.** There is no assertion for
"this is the same code somewhere else": the suite counted 284 before and 284 after, which is exactly
what it would have counted had a behaviour been quietly altered along the way. What established the
move was mechanical — reconstructing the moved text from `HEAD` and comparing it byte for byte, then
confirming the origin's side of the diff was 549 deletions and exactly one added line. That second
number is the one that catches the failure this repository actually has: a formatter run over a
whole file rewrites lines belonging to neighbours, and it arrives looking like part of the move.

## 2026-08-15 · The camera checks what it took (#55, PR #71)

**A silent failure is not fixed until the silence has been reproduced.** Both of this script's
defects were known and written down before anybody ran them, and both descriptions were right — but
the numbers only arrived by staging them: seven PNGs **byte for byte identical** at 28.4 KB and
**exit 0** for the dead port, and a perfectly rendered screen at mean luminance 17.9 against a
committed sibling at 250.0 for the theme. Those numbers are what the thresholds were then set from.
Fixing a described failure without staging it means guessing where the line goes.

**Two checks, because either one alone passes the other's failure.** The obvious fix for the theme
is to force light and stop there; the obvious fix for the blank shot is to look at how much of the
picture is one colour. Under forced light, Chrome's error page reads **254** mean luminance — the
theme check waves it straight through — while the picture is 99% one colour against 58–81% for a
real screen. The two failures looked like one shape and are not: one is about *what* rendered and
one about *how*, and a check for either is a check for neither.

**The disagreement between two addresses was fixed by there being one.** The guard curled `5080`
while every shot went to `5173`, and the repair that suggests itself is to make the guard curl both.
Asking `aspire describe` instead turned up that the Server proxies Vite same-origin, so there is one
origin to ask about — which is also the arrangement the product ships. Half a bug can be deleted
rather than handled, and the way to find out is to ask the platform what is actually there.

**The drift this issue was written about had already happened again.** `#21` caught one screenshot
in dark by eye and fixed that run; measuring all seven found `project-screen.png` and `tasks.png`
sitting in `current/` in dark, from a commit **after** that fix. A defect that can only be caught by
noticing is a defect that gets through the next time nobody is looking, and "we know about this one"
is not a control.

## 2026-08-16 · The preview header gives from the front (#72, PR #74)

**The box was innocent, and every box query said so.** `getBoundingClientRect()` on the name span,
the path span and the badge reports **no overlap** — before the fix and after it. The column carried
`minWidth: 0` and its box shrank exactly as asked, to 159.7px; what ran under the badge was painted
*text*, 97.7px past a box that had already given way. Only `Range.getClientRects()` sees it. A test
asserting that two elements' rects do not intersect would have called this screen clean at every
width, which is worth knowing before writing one: for overflow, the element is the wrong thing to
measure and the ink is the right one.

**Two treatments of one problem in one file were not drift.** The issue found `break-all` on the path
row and ellipsis on the tree's rows and asked which the header should copy, as though one of them had
to be the mistake. Neither was. `break-all` sits in a `flexWrap` strip with nothing under it competing
for height; ellipsis sits in a fixed 26px row above a list that scrolls. The header is `flex: none`
over a scrolling pane, so it belongs to the second kind — and wrapping had cost it 76px of a 700px
window, all of it taken from the file somebody opened the tab to read. **Look under a component
before matching it to a neighbour**; consistency of appearance and consistency of constraint are
different questions and only the second one decides.

**The issue named its prior art precisely, and that bounded the search too tightly.** It said "both
siblings of the same file", identified the real tension — an ellipsis keeps the head, and the head of
an absolute path here is `/Users/andoniplain/proje`, which is the same sentence for every file on this
machine — and called resolving it "the actual work". It was already resolved, one directory away, in
`features/runs/RunScreen.tsx`: a header of the same shape over a scrolling pane, truncating from the
left. The decision the issue posed as a choice between two options was a lookup of a third that
already existed. A well-scoped issue points at its own prior art; it can also fence it in.

**Half a copied idiom is a different idiom, and it breaks the case nobody was looking at.**
`direction: rtl` is the half everybody quotes about left-truncation. `textAlign: left` is the half
that gets dropped — and without it a path *short enough to fit* is pushed flush to the right of its
column, ink at 452.5 instead of 345.6, sitting against the badge it was supposed to clear. The
narrow case would have looked fixed and every wide one would have moved. Measured both ways on the
running app rather than reasoned about, because the reasoning ran the wrong way first.

**A screenshot register has a width, and a bug can live underneath it.** `capture.sh` shoots at
`1440,900`; this defect does not exist above roughly 700px. So the correct output of a frontend fix
was **no image change at all**, proven rather than assumed: at 1440x900 the header is byte-identical
with and without the diff — 48px tall, name ink 381 to 683.4, path ink 381 to 869.4. Step 5 of
`/aio:sync` still wants evidence, and the honest evidence is the existing image plus the reason it
did not move. "Nothing to recapture" is a finding when it comes with the number that makes it one.

## 2026-08-15 · The working diff moved to the screen that owns the directory (#18, PR #76)

**Before deciding a block of copy belongs to one screen, check who else says it.** The item asked,
correctly, whether ten diff strings should be renamed `run.*` → `folders.*` now that the diff is not
about a run. Nine of them were the diff's alone. The tenth, `run.binaryFile`, is also what the Files
preview says about a binary file — so the block was never one screen's to take, and renaming the
nine would have split one screen's copy across two prefixes to fix a prefix that had merely stopped
being literal. The decision was to leave them and write the reason beside the keys.

That turned out to be load-bearing rather than tidy. **#72 merged into the middle of this change and
moved that exact call site from `FilesTab.tsx:116` to `:150`** — a file this issue was told not to
touch. A rename would have compiled here, passed the gate here, and broken on somebody else's merge.
The parallel-wave rule about not tidying a neighbour's block is usually argued as conflict
avoidance; this is the sharper version, where the shared thing is a *key* and the conflict is not
textual at all.

**A check written from an assumption cannot catch the assumption.** The item's *What it is not* said
the endpoint answers "the working directory against `HEAD`". It does not — `GitWorkspaceProbe.Diff`
diffs against the **merge base**, which is the orca lesson `AGENTS.md` already cites, applied here
and then forgotten one document later. The item's own check specified "commit nothing", so it would
have passed either way. Measured after committing this change: `git diff HEAD --stat` empty, and the
tab still listing all four files. Had the sentence been true, the new tab would have been blank for
exactly the runs that did the most work — the original bug, re-shipped on a new screen.

**Orca pays for one list with a fact it can no longer state, and so do we.** It runs two
enumerations and renders them as two labelled sections — Changes, Staged, Untracked, then *Committed
on Branch* — letting a file that is both committed and dirty appear twice on purpose. The only place
it collapses them is the `+N −M` chip, whose docblock says why: `git diff <mergeBase>` with no second
rev is the "correctly-deduplicated result", where summing per-area rows "would double-count any line
touched in two areas". That single-diff form is precisely what this repository's probe already runs
for the list itself. So our one list is orca's deduplicated shape, and the price is the thing orca
buys with its split: **this tab cannot say whether a change is committed or still only in the working
tree.** For an agent that commits as it goes, that is the difference between "the agent did this" and
"this is sitting in my tree". Not a bug, and not fixed here — but now written down rather than
discovered later by someone wondering why the count looks wrong.

## #78 — a finished run cannot be read from its terminal bytes

**A measurement can be the deliverable, and this one killed the change it was meant to guide.** The
proposal was written, the shape compiled, and the first step of implementing it was the number
nobody had: how much of a real transcript is alternate-screen repaint. Two runs answered 90.0% and
99.2% — but the reason to stop was sharper than the proportion. Inside that span: 856 absolute
cursor moves, 279 erase-in-line, and **zero line feeds or scroll operations**. The TUI never advances
the screen; it overwrites forty rows in place. So consuming the buffer switches does not stack N
screens as everyone had assumed, including the issue and the proposal — it repaints one screen 856
times, and yields two rows of scrollback.

**The follow-on nobody had tested died with it.** "Split the buffers at capture, like orca" was the
anticipated fallback if the frontend shape failed. It does not reach the conversation either: the
scrolled-off lines were never in the stream as history, only as cells the next frame covered, and
orca's `scrollbackAnsi` has exactly the same limit. Two options were closed by one measurement,
because both rested on the same unexamined belief — that what a TUI painted is somewhere.

**Four ideas arrive in this shape, and they arrive in every conversation about it**: consume the
switches, split at capture, reconstruct by diffing frames, serialize in the browser and upload. All
four are reasonable, all four are the same wall from a different angle, and none survives the
measurement. They are recorded on #80 with their numbers rather than left to be reproposed — the
cost of not writing them down is half an hour of the same reasoning, repeated by someone who cannot
know it was already done.

**And the honest finding lands where it will be read.** Not only in the issue, which closes and
drifts, but in the doc comment of the component that does the replaying — where the next person to
think "this should scroll" meets the numbers before writing a line.

## #80 — a finished run is read from the record its agent kept

**The measurement moved the scope twice, in opposite directions.** The proposal expected three
vendors to need readers and made the first implementation step a measurement of Copilot CLI, which
is launched with `-p` and might have no problem. It does not: a real run produced 385 bytes, 100% of
them on the normal buffer, no alternate screen at all. But the same measurement said OpenCode is
flat too — 334 bytes, 100% normal — which nobody had predicted. **Only Claude Code loses its own
conversation**, at 99.4% alternate screen. So one reader fewer than planned for the reason expected,
and one more than the problem strictly requires, kept for reasons written into it rather than
assumed: search, no dependence on the stored tail, and marking which turns a resume inherited.

**A number can be wrong and plausible, and only the product says which.** `ContinuedFrom` — how many
leading turns a resumed run inherited rather than produced — borrowed the usage reader's two-minute
scan margin as the fork boundary. On a real resume the parent had ended ninety seconds earlier, so
sixteen of its turns were credited to the new run: `continuedFrom` read 6 where the truth was 22.
Every test passed. The number existed, was in range, and was wrong, and what caught it was opening
the run and seeing the line drawn in the wrong place. It is now the run's own `StartedAt`, pinned by
a test — but the lesson is that a plausible number is the hardest kind of bug to write a test for
before you have seen it be wrong.

**The design record does not cover the screen this changed.** `capture.sh` photographs seven screens
and none of them is a run. The change adds a new one and there is nowhere in `docs/design/current/`
for it to go, which is why this merged with no screenshot and an explanation instead. Worth its own
item: the reference set has a hole exactly where the newest surface is.

## #86 — the command a person presses was the one the sweep missed

**A fix that lands in four documents and not in the data file is a fix that landed where it is read,
not where it runs.** #51 corrected the AppHost form in `AGENTS.md`, `README.md`, `DESIGN.md` and
`ship.md`, and left `.harness/commands.json` on `aspire run --apphost src/root/AiOrchestrator.AppHost`
for three weeks. All four corrected files are prose an agent reads before deciding what to type. The
one left behind is the only place the string is not advice but the literal command the product hands
a person on a button. A sweep driven by reading documents finds documents; the configuration that
the feature actually executes is data, and it has to be looked for on purpose.

**The symptom outlived the version that produced it, and the check still asserted it.** The issue
led with "today the CLI asks which AppHost and waits" — inherited from #51, where it was true and
expensive. On Aspire 13.4.6 it does not reproduce: probed from a worktree with the pinned
`aspire.config.json` moved aside, the directory form resolved silently to the worktree's own
`AiOrchestrator.AppHost.csproj`, because an explicit relative `--apphost` is resolved against the
working directory and exactly one project file sits inside it. The four candidates that make the
glob ambiguous are all really there — `master` and three worktrees, and a linked worktree's `.git`
is a file so the root walk does go past it — but passing the directory explicitly never reaches that
search. **The reason to fix it was the second one on the list.** `--isolated` was buying something
today: two instances came up side by side, on 53571 and 57589, with separate Postgres containers.

**So a check that predicts the failure has a shelf life, and a check that reports an observation does
not.** "Pressing this hangs on a prompt" was written once, was true once, and was carried forward
into an issue by people who had every reason to believe it — including into this one's *Hoy*. What it
cost was nothing, because the fix was right anyway; what it could have cost is an agent concluding
the change was unnecessary because the symptom would not reproduce. Both halves belong in the report:
the form is what #51 established everywhere else and a command a person presses should not depend on
a CLI version's resolution order staying friendly, **and** the prompt did not happen here.

---

## #88 — a start line that detaches cannot be launched from a terminal that closes

**Three bugs, none of which any test would have caught, and all three found in the first ten
minutes of running the product.** The suite was green through every one of them.

**A login shell rebuilds the PATH the product computed.** `ProcessCommandRunner` ran every folder
command through `/bin/sh -lc`, and `LoginShellEnvironment` exists to hand that shell a PATH worked
out from the user's own startup files. The `-l` undid it: macOS `path_helper` runs from
`/etc/profile` and does not append to PATH, it **rebuilds** it from `/etc/paths` and `/etc/paths.d`
and puts whatever it inherited after. Measured: `aspire` resolved to a **13.0.0** global tool in
`~/.dotnet/tools`, which ships in `/etc/paths.d`, rather than the **13.4.6** on the developer's own
PATH — and 13.0.0 has no `start`, `stop` or `describe`, so the CLI printed its help and exited 1.
That had been true of every command this product ran since commands existed. Nobody noticed because
the two things anyone had run through it, `dotnet` and `pnpm`, are installed exactly once.

**Then the same line, with the right binary, still did nothing.** The terminal said `AppHost started
successfully`, gave a PID, and printed `exit 0`. No container ever appeared. `aspire start` detaches
and returns, so the launching shell exits, so the pseudo-terminal closes — and the kernel SIGHUPs
everything in that session, including the application that was thirty seconds from being ready.
Reproduced outside the product with nothing but `script -q /dev/null sh -c 'aspire start …'`, which
rules out anything this codebase does; the same line under `trap '' HUP` was answering on its port
twelve seconds after the pty closed. **The pseudo-terminal this product runs everything in is not
neutral.** It was chosen because build tools print differently without a TTY, and for every other
kind of command it is free. For the one kind meant to outlive its launcher it is fatal, and the
symptom points nowhere near it: a success message, a PID, and a clean exit.

**What made all three findable was the same thing.** A start that could not report its address was
still a terminal somebody could open, and the reason was in it. The failure state was designed to
stay stoppable and to name where to look, on the argument that a preview the product has forgotten
is worse than one that failed — and the first use of that terminal was not a user's, it was the
author's, three times in a row.

**And the third was a sentence.** The tab read `Preview — exited` beside a live address, because the
process it named really had exited. True of a shell, true of a build, and a lie about an application
— the same label reused for a session whose process is a launcher rather than the thing.

## #94 — a concept was three fields paying for a case two ordinary entries already covered

**The `preview` object was not wrong, it was early.** #88 asked what previewing an application needs
and answered honestly: a line to start it, a line to ask where it went, a line to stop it — plus a
strip of its own, a terminal kind of its own, and a rule that closing its tab is not how it is
stopped. Every one of those is defensible on its own. What made it a concept rather than a feature
was that each was reasoned about *for previewing*, and none of them turned out to be about
previewing at all: a dev server is not stopped by closing its tab either, and a watcher is not
either. The generalisation was available at the time and nothing pointed at it, because the case in
front of the author was singular and the code was written to fit it exactly.

**The field that went is the one that was hardest to argue against.** `address` was the cleverest
part of #88 — an address cannot be declared, only asked for, so the checkout answers with a script,
and the reasoning for that is three paragraphs long and correct. It is also unnecessary: the process
already prints its address, and #88 itself had just made a terminal's URLs clickable. Two mechanisms
for one fact, shipped in the same change, and the weaker one had the documentation. The tell was
that the strong one needed none.

**The mirror was the bug that had not fired yet.** Commands were reconciled from the checkout into a
table, and #88 wrote a comment beside that reconciliation explaining why the *preview* half must not
be mirrored — a worktree is a different branch and the file is on it. The same sentence was true of
the commands three lines above it, and the comment sat next to them for a wave. Verified after this
change: the `master` checkout offers 3 commands and `change/94` offers 5, from one query, because
each reads the file on its own branch.

**Two verbs, because a tab is where output lives.** Stopping a command and closing its tab were one
operation, which meant ending a dev server threw away what it said on the way down — the half
somebody usually wanted. Orca is the counter-example and it is emphatic: it auto-closes the tab on
exit and discards the exit code entirely, receiving it as `_code` and never rendering it anywhere.
Right for a desktop app where the tab and the process are the same window; here the honest fix for
#88's `Preview — exited` beside a live address was to say `exit 0` and take the address from the
process, rather than to hide the word.

**A guard against a string has to be a test, not a paragraph.** `--isolated` is the word the whole
arrangement rests on and it lives in free text inside a JSON file. The safeguards written down for
it were that the file is in the repository and goes through review — both true, and neither of them
fails. `IsolatedIsDeclaredTests` reads *this repository's own* `.harness/commands.json`, not a
fixture, which is the only version of that test that cannot pass while the file it is about says
something else.

**And the address still came out wrong the first time it was run.** `python3 -m http.server` prints
`(http://[::]:8099/) ...`, the URL regex took the bracket, and the header offered a link that
resolves to nothing. Every gate was green. It took forty seconds in front of the product, which is
the fourth change in a row where that was where the bug was.

## 2026-08-16 · A command can be edited and removed (#96, PR #97)

**Replacing the entry is the same mistake as re-serialising the document, one scope smaller.** The
item said the writer must splice rather than round-trip, and the obvious way to honour that is to
build the new entry's line and write it over the old one's bytes — every comment in the file
survives, the check passes, and the *inside* of the entry is gone. This repository's own
`start-the-app` carries a four-line comment about the shutdown reaper and a `stop` field the dialog
never asks about, both inside its braces. Replacing the object takes both, and what is lost is the
one field whose absence means a container outlives the server. So an edit rewrites the five values
where they sit and adds only the fields the entry lacks. The rule that generalises: **the unit you
preserve has to be one level smaller than the unit you are changing.**

**A test written against a fixture proves the fixture's comments survive.** Nine of the new tests
write the JSON they then read, and all nine passed against a writer that took the wrong comma —
because none of their fixtures had a comment between two entries, which this repository's file has
four of. The one that caught it asserts the *whole file* rather than substrings, and the one that
matters reads a copy of `.harness/commands.json` itself. `IsolatedIsDeclaredTests` had already made
this argument for `--isolated` a change earlier; it is the same argument and it had to be made twice.

**The comma belongs to the element after it, not before it.** Removing an entry looks symmetric and
is not: a comment between two entries sits *after* the first one's comma, so taking the comma in
front of an entry leaves the comment holding the next entry's separator — a file that reads
`// What starts the application.,`. Found by an exact-text assertion in about four seconds; a
`ShouldContain` on the comment would have passed, because the comment was still there.

**And the bug that all four gates shipped was one nobody was looking for.** Editing
`cd src/frontend && pnpm typecheck` wrote `cd src/frontend && pnpm typecheck`:
`System.Text.Json` escapes by default anything a browser could misread. Valid, round-trips exactly,
unreadable — the same damage as losing the comments, reached from the other side, in a file whose
entire point is that a team reads it in a pull request. It had been latent since #94 and only
surfaced now, because adding a command writes a line nobody typed and editing one rewrites a line
somebody did. Seen in `git diff` forty seconds after the feature first worked, which is the fifth
change in a row where the bug was in front of the product rather than in the suite.

**Orca is the counter-example here, and it is a working product.** Its `writeHooksJson` serialises
the whole document with `JSON.stringify`, and its Devin path reads that config with `jsonc-parser`
*because it may contain comments* and then writes it back flattened. The comment-preserving half of
the API it already depends on — `modify()`, `applyEdits()` — is not called anywhere in its source.
It knows how: `ensureOrcaDirIgnored` appends to the user's `.gitignore` byte-preservingly, with an
idempotent guard. The lesson is not that orca was careless; it is that the round-trip is what you
write when you are not thinking about it, and the splice is what you write when you are.

## #100 — the preflight came across without its preflight (#100, PR #101)

**A generated file's provenance header tells you what came with it, not what it needed.**
`components.css` says it was transcribed from shadcn/ui "with Tailwind utilities resolved to their px
values", and that sentence is the whole bug: Tailwind's *preflight* is where `box-sizing: border-box`
lives, and the preflight is not a utility, so it was not resolved and did not travel.
`grep -rn box-sizing src/frontend/` returned nothing, and every `.pc-input` in the app was therefore
`width: 100%` plus 24px of padding and 2px of border — 26px past its container, in a stylesheet that
is correct upstream. DESIGN.md's rule about not editing the vendored CSS held and pointed the right
way: the fix belongs in the hand-written file, because it is a missing import rather than a changed
value. **The question to ask a vendored artefact is not "is this file right" but "what did this file
assume was already in the page".**

**The dialog was the only place it was visible, and that is why it survived.** One rule was wrong for
every input on seven screens; it showed up in the one container narrow enough and clipped enough to
turn 26px into a scrollbar. A bug proportional to nothing and visible in one place gets filed as a
layout complaint about that place — #39 filed it twice, once as a scrollbar and once as wrapping, and
only the scrollbar half was this. **Symptoms that share a screen are not evidence they share a cause;
symptoms with a constant offset are.**

**Measuring the fix meant measuring the fix against itself.** The first before/after run showed the
input already fitting and no overflow, which read as "the bug is not real" — the flex row added
alongside the reset was doing the work. Reverting *both* frontend changes gave the real number:
`content-box`, input 546px, dialog `scrollWidth` 566 against `clientWidth` 560, the input's right edge
25px past the panel's content box. Then a control run of the same code against itself put the
run-to-run noise at 0.000%, which is what made 2.96%–14.29% per screen mean something. **A before
picture taken with half the change still applied is the most convincing wrong answer available.**

**And two screenshots that lied, both caught by reading them.** One shot caught Settings mid-load and
reported a 3.058% difference that was a spinner; polling for the absence of "Loading" instead of
sleeping a guessed interval took it to 0.000%. The other was the theme: `Emulation.setEmulatedMedia`
looked like it had worked and the page came out light both times, because `useTheme` reads
`localStorage` first and only falls back to the media query — the exact trap `capture.sh`'s own
comment describes, arrived at from a different direction. `capture.sh` verifies every shot it writes
for this reason; anything else that photographs this app needs the same habit.

**Cancel is not a failure, and the platform will not tell you which it is.** `osascript` exits 1 both
when a person presses Cancel and when the script breaks; `-128` on stderr is the only thing that
separates them. Matching the *word* would have failed: AppleScript writes "User cancelled." with two
Ls, where the Electron API orca uses hands back a `canceled` boolean spelled with one. Orca gets the
distinction as a field and never has to make it — **the value of reading it was not its code but
knowing the distinction existed to be made**, which is the argument AGENTS.md makes for the trip. Its
other two lines paid off the same way: `openDirectory` without `createDirectory`, because native
creation leaves typed prefix directories behind, and the caller supplying where the chooser starts.

**A picker turns a latent key mismatch into a two-click one.** `AddFolder` deduped on
`folder.Path == path` in SQL, so `~/code/portal` typed and `/Users/me/code/portal` chosen were two
folders over one checkout — true before this change and unreachable, because nothing produced the
second spelling. AppleScript resolves symlinks before it answers (`POSIX path of` on `/tmp` gives
`/private/tmp/`), which is the case AGENTS.md records as having cost this project a silently empty
database. One `ResolveLinkTarget` on the whole path does not fix it: it resolves the last component,
and `/tmp/x` is a real directory whose *parent* is the link. **A comparison that has only ever seen
one spelling of its key is untested, not correct.**

**The suite is 348 tests and three files say 250.** The figure exists because an agent once reported
118 as a full run, and a number a reader is invited to check against only works while it is right.
Ten of the difference was this change; 88 predate it. Filed as #103 rather than fixed here, and worth
the sentence: **a literal count in prose is a gate that decays every time somebody adds a test.**

**Stubbing a seam because the real one works.** `IDesktopIntegration` had to be replaced in
`WorkspaceFixture` — not to reach an untestable case, which is why every other stub there exists, but
because the real `ChooseFolder` waits five minutes for a person and would have hung `dotnet test`
behind a Finder dialog on whoever ran it. **A seam that blocks on a human is the first one a suite
must not be allowed to reach.**

## #98 — a table, a file, and a table again, and only one of them was a mirror (#98, PR #99)

**The same shape twice is not the same decision, and which way the facts flow is what tells them
apart.** `folder_commands` existed before #94 dropped it, and #98 created it again — the third answer
to one question, with the first and third looking identical in the schema. They are not: the first
table was a *mirror* of `.harness/commands.json`, so it held a second record of a fact the file
already stated and answered every worktree with the checkout's list. This one is the record, and the
file is a seed read once. #94 was right about the copy and bought a new problem doing it — the file is
on a branch, so the set changed underneath you on every `git switch`. **Neither reversal was a mistake
being undone; each fixed the cost that had come to dominate, and a schema diff cannot see the
difference.**

**orca had already fixed half of it without a database, and only the trip found that out.** It reads
`orca.yaml` from `repo.path` and never from a worktree (`orca-runtime.ts:20901`), so its setup script
does not change under you when you switch branches — the same win #98 wanted, bought by choosing which
directory to read. It keeps a two-layer model beside it: the tracked file as the project default and a
gitignored `.orca/issue-command` as the per-user override, where an empty override is *deleted* so the
shared value shows through again, and `readIssueCommand` returns both plus which one won. This
deliberately did something else — a folder here need not be a repository, so there is no `.gitignore`
to maintain and no natural home for a local file — but **the small alternative was only visible
because somebody went and looked, and an item that skips the trip cannot record that it declined it.**

**Keeping the file is what kept its guard, and deleting it would have deleted the only thing
protecting `--isolated`.** `IsolatedIsDeclaredTests` reads this repository's own
`.harness/commands.json`, so the cheapest answer to "what replaces it" was "nothing, because the file
stays as the seed". A row is one person's, on one machine, reviewed by nobody; a guard reading rows
would guard whatever was last typed. **A guard is worth exactly as much as the thing it reads is
reviewed**, which is also why a save-time validation was rejected rather than added: it would have put
knowledge of one product inside a feature whose whole seam comment insists it understands nothing
about a command beyond whether it ends.

**A local `pnpm typecheck` reported "No errors found" with `node_modules` missing.** The real gate
caught `Callout tone="danger"` against a union of `"neutral" | "info"` seconds later. A tool that
cannot run and says nothing is worse than one that fails, because the passing sentence is
indistinguishable from a real one. **`aspire do ci` is the gate for the reason that it installs before
it checks.**

**`mergeStateStatus: CLEAN` is a claim about text, not about a tree anybody has built.** `master` moved
three commits between implement and sync — #100 among them, touching five of the same files — and the
gate had only ever run on the branch alone. Merging upstream in and re-running it was the difference
between a tested merge and a hopeful one: 337 tests on the combined tree, none of which the branch's
own green run had covered. **The gate belongs on what is about to be merged, and that is a different
tree from the one on the branch whenever anybody else has landed anything.**

**A Definition of Ready can carry a check that something upstream of the whole change forbids.** Check
3 — a folder that is not a repository may have commands — is refused at `AddFolder.cs:94` with
`Folder.NotARepository`, so you never get the folder and therefore never get its commands. The command
half needs no git at all, which the functional tests show by running on bare temp directories.
**Grilling proved the check was worth wanting and could not prove it was reachable**; that only came
out against the running product, which is the house rule doing its job.

**Verifying a run's UI needed a run, and starting one in "your checkout" rewrote the main checkout's branch.** The run launcher, given `worktreeName: null`, branched `102-run-issue-tab` and switched the *main* checkout onto it — found later by `git worktree list` showing `master`'s path on a branch nobody made. Restoring it was one `checkout master` and a branch delete, but a second agent's mutating batch against that checkout would have run on the wrong branch. **A run pointed at a checkout rather than a worktree is a mutation of that checkout, and the verification plan has to treat it as one.**

**The vendor and the mirror can both be right and disagree.** Releasing the hold through the new button wrote the label off at GitHub; `GET /api/tasks` still said `held: true` one second later, because the mirror follows by the poll and the nudge had not landed yet. The first read of the result looked like a failure and was the design working. **A check that reads the mirror has to wait for a sync before it reads anything.**

## #106 — "what is in this file" was a text question only because nobody had shipped the bytes (#106, PR #107)

**The binary guard was already the right door; what was missing was a second thing to carry through it.** `GetFile` answered `isBinary: true` and stopped, and the issue read as a rendering problem. It was a transport problem: no code path shipped bytes at all. The sibling endpoint is the whole shape — same `ls-files` guard, same permission, base64 because JSON has no byte string — and the renderer is three branches over a contract. When a feature request and its nearest neighbour differ only in payload, the seam usually already knows which side each belongs on.

**`capture.sh current` records the environment, not the change.** All seven screenshots moved on a retake, and the diff was a seeded-folder count, not the Files tab — the screen this change actually moved is not in the set at all. Committing them would have written somebody else's machine over the design record; reverting them and saying so in the item is what the "commit only what differs" rule is for. A screenshot set that changes when the seed changes is a fixture pretending to be a photograph.

## #109 — the check found the bug the diff did not (#109, PR #110)

**A one-line argv change had a two-bug tail in the same seam.** Switching OpenCode from `run` to
`--prompt` was the issue; the check run then read back no session id, for two reasons the diff had
nothing to do with: `opencode session list` is scoped by the caller's working directory (asked from
the server's checkout, it answered with that checkout's sessions), and OpenCode records the
**realpath** of its working directory while the run carries the path it was handed — `/var/…` vs
`/private/var/…` on macOS, exactly the mismatch the orca `realpathSync.native` lesson predicts. Both
were pre-existing; `opencode run` hit them identically. **The check line that says "run one and
look" is the only reason they are fixed rather than still shipping.**

**A launch-shape change is not done when the TUI draws.** The first run showed the banner, the frame,
the steered answer — everything the issue asked for — and `sessionId` was still null, with the
transcript unreadable. The observable symptom of the wrong thing is often downstream of the
observable symptom of the right thing; the checklist has to reach the second one or the first one
lies by omission.

## #113 — two dead buttons were one missing resolver, and the third was a missing seam (#113, PR #115)

**Three symptoms, two different root causes, one issue too small to see it.** The badge and the
VS Code / Finder buttons looked like one bug — "the header is dead" — and they were not. The badge
was a display bug: `activityLabel` already computed the right answer and the badge just did not call
it. The buttons were a wiring bug: the hook and the endpoint existed, only the `onClick` was absent.
But the third button, Open PR, was a *missing seam*: discovering the URL of a branch's PR is a reach
outside the process (`gh pr view` or REST), and no interface in `BuildingBlocks/World` offered it.
Bundling the three into one change would have meant smuggling a new World registration in beside two
one-line fixes, and the seam is the thing review exists to see. **Splitting an issue is not failure;
it is the seam check working.** The split is #114.

**The folder id was a client-side lookup away all along.** The proposal sketched a `folderId` field
added to `GetRun.Response` — a two-table join across a schema boundary MOD002 refuses. Reading the
existing `RunScreen` redirect first found it already resolving folder-by-path against `useFolders()`
to route `/runs/<id>` to `/folders/<id>`. The header could do the same lookup with the same cached
query, which made the backend change zero lines. **The fix for "the frontend does not know X" is not
always "the backend should send X"; sometimes it is "the frontend already has X, one lookup away".**

## #114 — a missing seam was the whole issue, and the simple path was the right one (#114, PR #118)

**The third dead button was a different species, and #113 was right to split it.** The badge was a
display bug, the desktop buttons were a wiring bug, and Open PR was an absent reach outside the
process. What the split bought: the seam — `IPullRequestLookup` registered in `World.cs` — is the
entire diff a reviewer had to see, and it arrived without two one-line fixes beside it obscuring it.

**Orca's answer to the same question was the elaborate one, and the reason it is elaborate is the
reason it does not apply.** `getPRForBranch` caches PR state per worktree and refreshes it
continuously, so it pays for a state machine, a REST-first fallback chain and quota accounting. A
button asked once per click needs none of that: `gh pr view <branch> --json url --jq .url`, and a
non-zero exit *is* the answer. Reading orca's version first is what made "no cache, no state" a
decision rather than an omission — and the exit-code contract was verified against this repository
(`no pull requests found for branch "change/114"`, exit 1) before a line of the lookup was written.

**A disabled button with a tooltip was the wrong shape for "we cannot know until you ask".** The
first version of the screen guessed unavailability from data it did not hold, so the tooltip pointed
at the issue instead of the answer. What shipped lets the click ask, and surfaces the refusal —
"no pull request found for branch X. Open one first" — as a sentence beside the button. **A refusal
a person can act on beats a disabled control with an excuse.**

## 2026-08-17 · A run keeps reading its issue after the vendor closes it (#116, PR #119)

**The filter was the bug, and the fix was to move it.** Every vendor query filtered to open, which
made "closed" indistinguishable from "deleted" — the sync's `seen` set lost the row and pruned it.
Moving the filter one layer up, out of the four sources and into the inbox read, is what let the
mirror keep the row and the run go on reading it. **A filter applied at the source is a fact
destroyed; applied at the read it is a choice.**

**"Not in the inbox" was the wrong sentence for "closed".** The run screen read its issue from the
inbox list, so the message meant two things and helped with neither. A read by key, separate from
the list, is what made the inbox's open-only rule safe to state — it no longer has to serve two
readers with opposite needs.

## 2026-08-17 · A workflow's typo is fixed where it was made (#117, PR #120)

**A rename that does not move anything is the easy half; writing down what it does not touch is the
half that keeps it easy.** The issue said no key, no file, no cascade into the override pair, no
backfill of `Run.workflowLabel` — four sentences that read as restrictions and acted as a design.
The diff that satisfies them is one domain method, one use case, one mutation, one dialog; a diff
that forgot any one of them is the one that would have had to be reverted.

**`aspire start --isolated` from a worktree does not mean the AppHost you found is yours.** The
machine already had the main checkout's AppHost up, and `aspire ps` from the worktree lists it
without saying whose it is. Verifying against it would have tested last week's code. `lsof -p` on
the AppHost PID is the check that answers the question the CLI will not.

## 2026-08-17 · Replay one clean frame to late attachers (#8, PR #121)

**The suite blessed a parser that the first real byte stream hung.** Every fixture transcript in
the test project is plain lines; the one thing none contained was a two-byte escape at a chunk's
end, which is precisely where the skip path returned a relative offset as an absolute index and
restarted the feed from byte 2 — forever, under the tracker's lock, with `GetRun` starving behind
it. The regression test is now a 64,760-char `opencode` capture fed in 4 KB pieces. The lesson is
the standing rule paying for itself again: a terminal parser's test corpus is real captures, not
invented sequences, because the bug lives in the bytes nobody thought to write down.

**A TUI's opcode set is measured, not assumed.** The whole change was sized L on the assumption of
a .NET `@xterm/headless`; what the stream actually carried was CUP, SGR, two erases, an alt-screen
pair and nothing else — a framebuffer, not an emulator. Counting the opcodes before choosing the
design turned a dependency decision into a few hundred lines of C#. The honest bound is written
into the tracker: what it does not model, it consumes by shape, and a TUI's next repaint heals the
glyph.

## 2026-08-17 · Universal Agent Skills: one Run drives every workflow step (PR #123)

**The workflow was a picture of itself.** The UI sent the first step's title as the prompt and the
rail of steps never moved, because nothing ever wrote to it. Making the run drive the steps needed
a seam that did not exist: Runs cannot read the Workflows schema, so the plan crosses as a DTO and
is snapshotted onto the run — which is the same rule as the task title, and for the same reason. A
workflow edited mid-run must not rewrite what that run is recorded as doing.

**A step's end cannot be inferred from the terminal going quiet.** The PTY title flips to idle for
a question, a gate, a long pause mid-task — none of them is "done". The result is a file the step
writes, `.aio/run-step.json`, and the transcript only carries the marker that points at it. A
launcher that advanced on the title would send the next step over a question the agent was still
waiting to have answered.

**Four copies of the same instruction is the kind of duplication that survives review.** The loop's
actions existed as `.ai/commands` symlinked into three runtimes' own formats. The fix is not a
fourth copy that is somehow more canonical: it is one `SKILL.md` per action in the format all three
read, and the run stores the neutral key rather than anybody's spelling of it.

**Un modelo puede describir un mecanismo que nadie ejecuta, y la suite no lo nota.** `RunStep`,
`CurrentStep`, `TotalSteps` y el rail de pasos existían, se proyectaban al cliente y se dibujaban —
con `Run.AddStep` llamado solo desde un fixture de test, así que todo run real corría con cero pasos.
Las 368 pruebas pasaban porque el fixture sí los creaba: **la suite verificaba el único caso que no
ocurre en producción.** Lo que lo encontró fue preguntarle al código quién llamaba a un método, no
correr las pruebas. Antes de construir sobre una tabla, `grep` de quién escribe en ella.

**Una suite que solo prueba el caso que ella misma construye pasa un botón que no funciona.** #130
comparaba fichero y filas por conteo, y sus pruebas siempre guardaban exactamente el conjunto declarado
— así que una carpeta con un plan sin exportar quedaba "desfasada" para siempre, y el aviso ofrecía
importar, que no puede borrar un plan. Un botón que no puede limpiar el estado por el que se ofrece.
Lo encontró levantar la app en treinta segundos. Cuando un test construye el mundo que después
comprueba, lo que valida es su propio setup: hay que probar el estado que llega de fuera, no el que
escribimos nosotros.

**Un fichero de configuración compartido es estado, y una escritura idempotente que nunca se probó
dos veces no lo es.** El instalador de hooks se reconocía a sí mismo por un marcador que no estaba
en el comando que escribía, así que cada arranque *añadía* en vez de reemplazar: tres arranques, 7
entradas por evento, 28 curls a puertos de servidores ya muertos — dentro del `settings.json` del
usuario. La suite no podía verlo porque nada en ella arranca el host dos veces, y una prueba de
"escribe el hook" pasa igual de bien la primera vez que la séptima. Lo encontró abrir el fichero
después de arrancar. **Lo idempotente hay que ejecutarlo dos veces para saber que lo es**, y cuando
lo que se escribe es de otro, el marcador tiene que viajar en lo escrito, no en lo que se pretendía
escribir.

**Un canal con varios lectores reparte, no difunde.** El seam de hooks entregaba a todos el mismo
`Channel`, así que un run y un terminal escuchando a la vez se robaban los eventos: cada uno llegaba
a quien leyera primero, y el otro se quedaba mudo sin error, sin log y sin fallo de compilación. Con
un solo suscriptor —que es como se escribe y como se prueba— el defecto es invisible. La forma
correcta es una cola por suscriptor; la lección es que "publicar" y "encolar" se escriben casi igual
y significan lo contrario.

**Una etiqueta de accesibilidad es una aserción que se lee gratis contra la app en marcha.** Cada
pestaña de agente dibujaba el check de "terminado" mientras el agente trabajaba, porque `done={!live}`
y `live` significaba *comando de larga duración*, falso para todo agente por definición. En una
captura de pantalla el glifo verde a 12px parece plausible; lo que no engaña es leer los
`aria-label` del DOM: dos agentes vivos anunciando "Done". Los nombres accesibles son el texto que
la UI dice de sí misma — y por eso son la forma más barata de comprobar que dice la verdad.

**Un comentario que explica por qué algo no se puede hacer es el diseño de la siguiente pieza.**
`RemoveFolderDialog` llevaba escrito, desde que se escribió, que su rechazo por runs vivos vivía en
el cliente porque Workspace no puede leer el esquema de Runs, y que hacerlo exigible significaba
darle a un seam del World una consulta de "hay algo vivo aquí" — decisión propia, no de un diálogo.
Ese párrafo fue el plano entero de este cambio: qué seam hacía falta, por qué, y qué se ganaba.
Buscar dónde alguien ya escribió el problema sale más barato que volver a pensarlo, y por eso el
comentario que dice *no lo hago y esta es la razón* vale más que el que dice qué hace el código.

**El registro de un seam va donde vive su DbContext, no donde viven los seams.** El item pedía
registrarlo al final de `AddWorld()`, que es el fichero que `AGENTS.md` nombra primero entre los que
colisionan. Pero `AddWorld()` es todo singletons que salen del proceso, y esta implementación lee el
esquema de su módulo: es scoped, y el espejo que copia — `IWorkflowPlanReader` — ya se registraba en
el `Add` de su módulo. Seguir el precedente en vez de la instrucción dejó el cambio fuera del
fichero más disputado del repositorio, gratis.

**Que dos suites no compartan esquema no es un límite de las pruebas, es dónde va cada mitad.** El
rechazo se prueba con el probe fingido donde vive el endpoint, y la consulta se prueba contra filas
reales donde viven las filas. Ninguna de las dos podía probar las dos cosas, y forzarlo habría
significado resetear un esquema que la suite no posee — que es exactamente la frontera que el seam
existe para respetar.

**Una puerta roja que se informa en un mensaje de commit envejece, y nada la vuelve a mirar.** El
segundo commit de #41 informaba honestamente de lo que había visto: cuatro tests end-to-end que pasan
sueltos en 27,6 s contra un timeout de 30 s, y ese margen de 2,4 s perdido bajo la concurrencia de la
propia puerta. Eso viajó al cuerpo del PR y se quedó ahí, en pasado perfecto, mientras seis commits
entraban en `master` y el rebase los traía. Al volver a medir sobre el árbol rebasado, `aspire do ci`
pasa 15/15 y `EndToEndTests` hace 4/4 en 3 s y en 8 s: no se reproduce. Sin volver a correrla, un
revisor habría leído "la puerta no pasa" sobre un cambio cuya puerta pasa — y el fallo informado, que
era la parte honesta, se habría convertido en la desinformación. **Un fallo se informa contra un
árbol, y el árbol cambia**: el informe caduca con el rebase, no con el arreglo.

**El editor de una etiqueta no puede vivir detrás de la rama que esa etiqueta decide.** El único
conflicto del rebase fue textual —#135 había metido los editores de assignees y labels justo donde
este cambio envuelve la sección *Run it* en un ternario— pero resolverlo obligaba a decidir algo que
no era textual. Dejar los editores dentro del `else` era lo que el diff pedía; habría escondido el
control que quita la marca `needs-a-person` exactamente cuando la marca está puesta. Un estado que el
producto enseña y del que no ofrece salida.

## 2026-08-18 · Owner actions on the workflow editor (no issue, merged straight to master)

**El prototipo también se queda atrás, y nadie lo declara.** `docs/design/README.md` ya avisaba de
que leer una pantalla no es lo mismo que mirarla; esto pasó en la dirección contraria. Renombrar
(#120) y borrar (#140) se enviaron sin que `Harness.dc.html` dibujara ninguno de los dos, así que la
pantalla improvisó — papelera roja sin etiqueta pegada a *Add a step*, badge de scope con la ruta
absoluta entera — y el prototipo dejó de ser el objetivo sin que nada lo dijera. Lo que lo delató fue
la captura de `current/`, que existe exactamente para eso. Un prototipo que no recibe lo que se envía
es un archivo, no una referencia, y la dirección diseño → código no es la única que hay que mantener.

**Un token que no existe no falla: no pinta.** El aviso del rechazo 409 pedía `--pc-error-50`, que no
está en la rampa — las familias funcionales son 100, 300, 500 y 700 — y CSS inválido se descarta en
silencio. Pasó lint, typecheck, tests y revisión, y el estado sólo aparece con un run vivo sobre ese
workflow, así que el recuadro llevaba desde #140 sin tinte alguno sin que nadie tuviera ocasión de
verlo. La regla 2 de DESIGN.md dice que todo valor es un token; lo que añade esto es que un valor
inventado *con forma de token* no deja rastro en ninguna puerta. Componer `pc-alert--destructive` no
es sólo más limpio: es lo único que hace imposible ese fallo.

**Un diseño que sólo vive en el canvas se puede leer, pero no editar.** `Harness.dc.html` son 290 KB
y el techo de lectura de la herramienta son 256 KiB, así que la mejora aterrizó en un fichero
adyacente del proyecto de diseño en vez de en la pantalla que especifica; y para leer el markup del
editor hubo que descomprimir a mano el payload gzip que `design-import/Harness.html` lleva inline. El
detalle que lo desbloquea la próxima vez: **el límite es de lectura, no de escritura** — con el
fuente `.dc.html` commiteado en el repositorio se edita en local y se sube sin leerlo nunca por la
API.

## 2026-08-18 · Copy the open file's absolute path from the Files tab (#142, PR #143)

**A clipboard check that reads the clipboard proves nothing on its own.** The control was clicked, the
path came back from `navigator.clipboard.readText()`, and it matched — three times, in three different
degraded states, and every one of those readings was worthless: the same path from the same file was
already on the clipboard from the successful case two steps earlier, so a control that had written
nothing would have passed identically. Writing a sentinel through the real API first, and asserting it
was *replaced*, is what turned a green reading into evidence. The same trap is why orca's fallback
tracks a `served` flag instead of trusting `execCommand`, which Chromium answers `true` when no handler
supplied any data — a browser and a checking agent can both report a copy that never happened, and the
fix for both is to compare against what was there before.

**The screen a change touches can be the one the design record cannot see.** `docs/design/target/`
has `files.png`, from the prototype's `tab:Files` variant, and `current/` has no counterpart, because
the `current` half of `capture.sh` shoots seven URLs and the two project-screen ones land on Overview.
So the after of this change is unphotographable by the script that exists to photograph it, and the
before was never taken. `git status` after a capture is how the docs say to find a screen that moved;
it cannot find one the script never visits.

**One flaky gate run costs more than one failure.** `Workspace.FunctionalTests` failed 65 of 65 on a
Postgres socket, `Operation timed out` in 77ms, on a branch whose diff was two frontend files — and
passed alone, and passed whole on `master`, and passed on the next run of the same branch. What the
suite is short of is a fixture that waits for its container's socket instead of assuming it. Until
then the gate has a way of failing that has nothing to do with the change under it, and the habit that
answers such a failure — run it again — is the habit that ships a real one.

## 2026-08-19 · A worktree's position against the trunk (#146, PR #153)

**Null and zero are different answers, and the type is the only place that can keep them apart.**
`ahead: 0` means the branch has caught up; a repository with no trunk has nothing to have caught up
with, and rendering both as `0` tells a reviewer a change is up to date when nothing was compared.
`MergeBase` already had the case — it falls back to `HEAD` — and the fallback was invisible above it
until the record made every count nullable. The same rule ran the whole change: a git that cannot
run `merge-tree --write-tree` does not report a clean merge, it reports that it could not look.

**A capability that fails should be remembered for a while and then forgotten.** Orca probes
`--write-tree` optimistically and remembers a refusal for thirty minutes
(`git-capability-cache.ts`), and both halves earn their place here: without the memory a folder with
six worktrees pays six dead processes on every open, and with a permanent no, a git upgraded while
the server is up is never noticed. A boolean would have been cheaper and would have needed a
restart to become true.

**Rehearse the command before parsing its output.** git 2.48.1 prints a conflicted `merge-tree` as
the tree oid, the conflicted paths, a **blank line**, then messages written for a person. Built on
`GitResult.Lines()`, which drops empty entries, the parse would have reported `Auto-merging f.txt`
as a filename — and it would have looked right in every test written from the same assumption. Five
minutes in a temp repository is what a fixture cannot replace.

## 2026-08-19 · A check remembers its commit (#147, PR #156)

**A fact's lifetime decides where it lives, and a session is the wrong place for anything a person
approves on.** `TerminalSession` had said so about itself for months — *"it does not survive the
server… unlike a run's, which is a row"* — and the Commands tab was still deriving "the tests
passed" from it. The fix was not a column on the session; it was noticing that a session and a
result have different lifetimes, and giving the result its own row.

**Read the thing you are stamping at the moment it is true.** The commit a check ran against is
knowable only when the command starts: a four-minute run can finish against a repository that moved
under it, and stamping at the end would record the wrong commit while looking correct in every test
that starts and ends in the same second.

**Three states beat two wherever a comparison can fail.** `since = 0` is on this commit, `since > 0`
is stale, and `since = null` is a commit git no longer has — a rebase. Two of those are a tick and
one is a shrug, and a boolean would have printed the shrug as a tick. This is the same shape #146
landed for `ahead`/`behind` a day earlier; it is becoming the house rule.

**A screen this repository cannot photograph is now two screens.** The Commands tab is not in
`capture.sh`'s seven URLs, so a change to it has no before and no after — exactly #145's finding
about Files. The script's list, not the change, is what has to move.

## 2026-08-19 · A run executes its plan (#148, PR #154)

**The contract answered the design question the issue could not.** "Which side owns the sequence —
the product dispatching each step, or the agent running the whole workflow from one prompt" looked
like a decision somebody had to make, and it had already been made in writing:
`ListWorkflows.Item.Command` is documented as *"the **first** command this workflow runs"*, computed
as the first non-skipped step by ordinal. Reading a doc comment settled a fork that had held a change
for a day. The lesson is not "read the docs" — it is that a field's own words are evidence, and
searching for the sentence that would be false under one of the two answers is faster than arguing
about which is better.

**Nothing advanced the steps because nothing executed them.** The issue was written as a missing
writer on a column. It was a plan the product never ran: the rail, `CurrentStep`, `TotalSteps` and
the hold flag were all downstream of a loop that did not exist. A "the column has no writer" issue is
worth one look further down before it is believed.

**Put the decision where a test can reach it.** The dispatch is a pseudo-terminal, a hub and a
database; the *decision* is a five-state machine. Split out as `RunPlan`, the failures that matter
became arithmetic: advancing twice on two idles, walking past a hold — which would turn a two-hold
workflow into an unattended one — and finishing a plan that still has a step. None of those is
reachable by a test that has to spawn an agent.

**Stop loudly at what is not built.** A step the product would run itself now halts the plan and says
so in the run's own output. No workflow declares one, so this is dead weight today — and a plan that
half-executes in silence is indistinguishable from one that finished, which is the bug that would
have been found six months later by somebody adding `tests` to a workflow.

## 2026-08-19 · The fixture waits for the mapped port (#144, PR #161)

**"Started" and "reachable" are different facts, and a readiness probe can only answer the one it
can see.** `PostgreSqlBuilder` runs `pg_isready` *inside* the container; the host-side port mapping
is outside it. The distinction was already in the stack trace and readable by anyone who looked at
which exception it was: `Operation timed out`, not `Connection refused` — a refusal is a container
not listening, a timeout is a mapping not up. One word in an error message was the whole diagnosis.

**A flaky gate is a habit problem before it is a test problem.** 65 red tests in 1ms each, on a
branch of two `.tsx` files, teaches people that the gate is noise and that the answer is to run it
again — which is the habit that eventually ships a real failure. That is why the check for this one
was five consecutive green runs and not one.

**Report the number you measured, not the one you were asked for.** The issue asked the assembly to
stay within a second of 11.2s. It runs 13.0s — for 67 tests where it ran 65, one of them spending
600ms failing to reach a dead port on purpose. The honest line is the measurement plus what changed
under it; rounding it to "unchanged" would have been true of nothing.

## 2026-08-19 · A hold can be released wherever it is drawn (#157, PR #159)

**A query-key filter is a prefix match, and that made a shipped feature dead on arrival.**
`useSetTaskHold` collected `getQueriesData({ queryKey: ["tasks"] })` and read `.tasks.map` on
everything it got back — which includes `["tasks", "by-key", key]` holding a `TaskDetail` that has no
`.tasks` array. `onMutate` threw, the mutation failed *before* `mutationFn` ran, and no request was
ever sent. The run's Issue tab was the worst possible home for it: `IssueTab` calls `useTask` two
lines above the mutation, so the entry that breaks the loop is always cached exactly where the only
button is. #102 shipped that button and it had never once cleared a hold.

**The failure said the wrong thing, which is why nobody caught it.** A `TypeError` is not an
`ApiError`, so `explain` fell through to "Could not reach the server." — a sentence about the network,
for a request that never left the browser. An error message that blames the wrong layer is worse than
none: it sends the reader to check the server, which was fine.

**Four gates and 435 tests cannot see this, and that is not a gap to close with a test.** `build`,
`typecheck`, `lint` and every assembly pass on code whose only client is a browser nobody opens.
AGENTS.md already says every bug found in committed code was found by running the product; this is
the sharpest example yet, and the fix was to drive the real app in a real browser over CDP — the same
headless Chrome `docs/design/capture.sh` already depends on, so no automation dependency was added to
press a button.

**An optimistic update destroys the component that would have reported its own failure.** Releasing a
hold clears `held`, and the header inbox and a project's Overview both *filter* on it — so the row is
gone before the answer arrives, and the last hold takes the whole chip and its menu with it. State
kept in the row means a refusal reverts the cache, the row returns as a fresh component with an idle
mutation, and the vendor's words are lost. Orca's `useImmediateMutation` keeps `pendingKeys` outside
the row and guards the post-await update for exactly this reason. Where this went its own way: one
release at a time rather than orca's `Set`, because `useSetTaskHold` snapshots the whole cache, so a
refused first release would restore a snapshot that silently undoes a second.

**Two branches that each commit a picture of one screen leave a merge with no correct side.** #151
landed mid-review and its `project-worktree.png` shows the merge gate without this control; ours
showed the control without the gate. Neither is the merged product. `--ours` and `--theirs` are both
wrong on a screenshot: the resolution is to start the app and retake it.

**The count in the instructions drifts, and only the run knows.** `/aio:implement` still says `ci`
"counts 250"; #103 corrected three files to 348; it was 428 before rebasing onto #147 and 435 after.
The rule that a smaller count is a subset only works if the number is read out of the run rather than
recited from a document.

## 2026-08-19 · A worktree says whether it can be merged (#151, PR #165)

**A screenshot found what no test would have.** The panel drew **Ready to merge**, in green, on a
worktree whose own Checks tile said *not run here*: a condition with no answer counted as no
objection. Every unit of it was correct — the tile said the truth, the verdict aggregated the tones
it was given — and the composition was a lie. It was visible in the first picture and would have
been invisible in any assertion written by the person who wrote the bug.

**Null is not zero, three times in one wave.** #146 kept it for a count with no trunk to compare
against, #147 for the exit code of a command that was killed, and this one for a verdict with
nothing behind it. The shape is always the same: *nothing is wrong* and *nothing was looked at* are
different sentences, and every rounding of the second into the first flatters the change under it.
Worth watching for as a house rule rather than rediscovering per issue.

**Rename what a person reads; leave what a URL names.** Diff became Review and Commands became
Checks, and their tab ids did not move: `?tab=diff` is in links and in `capture.sh`, and changing it
buys nothing anybody can see. A rename that reaches an identifier is a migration wearing a label's
clothes.

**The design record cannot yet tell a design change from a data change.** `capture.sh current`
retakes all seven shots against whatever folders happen to be registered, so adding this repository
in order to photograph one screen changed the other six — different projects in the rail, different
tasks on the board. "Commit only the files that actually differ" is unsatisfiable under those terms:
everything differs, and one of them differs for the reason you care about. The record needs a fixed
dataset before its diffs mean anything.

## 2026-08-19 · The Issue tab shows the whole issue (#162, PR #166)

**Count the thing before deciding it is an edge case.** "The tab shows the body and not the
comments" sounds like a nicety until the repository is measured: 351,931 characters of body against
514,934 of comments, 59 of 92 issues with at least one, and the three worst offenders are the issues
somebody actually worked through. The number is what turns a polish item into a missing half, and it
took one script to get.

**Vendors differ in type, not only in markup.** Jira Cloud's v3 answers a comment body as an
Atlassian Document Format *object*; Server's v2 answers a *string*. A reader that switches on the
configured API version would be right until somebody points the same connector at the other
deployment; one that switches on the JSON kind that actually arrived is right either way, and is
shorter.

**A second copy of a component is a second place to fix it.** The markdown overrides — links open a
new tab, images cap at their container — existed once in `FilesTab`, and this needed exactly them.
Extracting `Prose` was smaller than copying, and the next screen that renders vendor text now has
nowhere to get it wrong.

**Empty is the common case here, so it is a sentence.** A third of these issues have no comments. An
empty state drawn as a shrug would be what most readers see most of the time.

## 2026-08-19 · Choose what a run may do without asking (#160, PR #168)

**The default has to be the old line, byte for byte, and a test is the only way to know it.** Adding
an argument to a spawn is the kind of change that quietly alters every run that did not ask for it.
The assertion that matters is not "the flag appears when chosen" — it is
`["--session-id", id, prompt]` with nothing else in it when nobody chose anything.

**Read the vendor's vocabulary off the vendor.** The six modes came from `claude --permission-mode`
on the installed binary, and `Invoke` drops one the definition never named. A picker that invented a
mode would produce a run that dies at spawn with usage text — a failure that looks like the product
breaking rather than like a stale screen.

**A typed message catalogue is defeated by a template literal, and the local check did not notice.**
`t(\`tasks.permission.${mode}\`)` type-checked under `npx tsc --noEmit` and failed under
`pnpm run typecheck` in the gate, which is the same compiler with the project's own settings. The
lesson is not about i18n: **run the project's script, not the tool the script wraps.** A local check
that is a paraphrase of the gate is a check that can be greener than the gate.

**Carry a choice through the resume or it becomes a surprise.** A run allowed to work, resumed into
one that asks again, is worse than one that always asked: the person is not watching by then.

## 2026-08-19 · Runs tab shows workflows, not processes (#152, PR #170)

**A screen that could not be built is sometimes a screen waiting on a fact.** This tab was blocked
twice, and both blockers were data rather than design: steps that never advanced (#148) and a strip
with nowhere to mount (#151). Written before either, it would have drawn a ladder of five queued
steps for every finished run — which is precisely what the old Runs tab was avoiding by not drawing
one at all.

**The interesting fact was the join, not the list.** After a hold, the first half of a workflow
belongs to one run and the second to another. Putting the run's id on each *step* is what makes that
readable, and it is one line of markup; the two years of confusion it answers were about a
relationship no screen was drawing rather than a control nobody had built.

**Replace, do not leave beside.** `RunsTab` is gone in the same commit that mounts its successor. A
dead component that still compiles is a component somebody will mount again by accident, and the
diff that removes it later has to argue for itself.

**A sighting is worth recording even without a diagnosis.** The whole suite failed once with 67
errors between two green runs of the same commit — the shape of #144's flake, after #144. There was
no captured stack, so it went onto that issue as *"seen once, here is what was true at the time"*
rather than into a retro conclusion it cannot support. The rule this repository already has —
refuse "the failing one is unrelated" — needs its other half: an unexplained green is not evidence
either.

## 2026-08-19 · A command says whether it is a check (#167, PR #169)

**A set nobody declared is a set the code will infer, and it will infer it twice, differently.**
"Is this a check" had no field, so two screens each answered it from whatever was nearest: the
Re-run checks button read `kind === "one-shot"`, the Checks tile read "has this ever finished". Both
readings are defensible and neither is the question, so this repository's own `Start the app` and
`Stop the app` were members of its merge verdict — one press started the application and stopped it,
and `aspire stop` exiting 0 counted as evidence that a branch worked. The fix is not a better
heuristic; it is a field, and a single predicate both surfaces call.

**Orca had the same rule written down, from the other direction.** `provider-check-summary.ts` says
"every check surface must route through it", because otherwise the same PR reads green on desktop
and grey on mobile — and its rollup answers `none` for an empty set and `neutral` when nothing
actually passed, never success. Two of those were already the plan; the third changed it. The trip
was worth making for a change that looked like a boolean and a filter, which is not where the
standing rule says to expect it.

**The backfill is the decision, not the migration.** Every existing row could have been given the
flag by the same `kind` reading the change was removing. Deriving it once, at the moment of adding
the field, would have written the bug into the data on the way to fixing it in the code — so the
column lands false everywhere and folders say "No checks declared" until somebody marks something.
An honest empty state cost nothing and is the only version that is true.

**Verifying with a cheap command found a bug the real ones hide.** The declared checks here run for
minutes, and every one of them beats the race in `ProcessCommandRunner`, which reads the pty's exit
code at EOF rather than after the child is reaped. A one-line `echo` does not: `sleep 3; exit 7`
recorded 7, 7, then 0 on three consecutive runs of the same line. A check can therefore record a
pass it did not earn (#171). The fixture-sized command was the instrument — the house rule says
verify against reality, and reality at a different timescale is still reality.

**Two findings, two issues, neither in the diff.** The exit-code race and `AddCommand` silently
dropping the time limit it validates (#172) were both found here and both left alone. The seam the
issue names is the boundary of the change, and "while I was in there" is how a reviewed diff
acquires work nobody agreed to.

## 2026-08-19 · Labels stick without a reload (#158, PR #173)

**A cache key is a claim about what a screen reads, and it has to be checked against the screen
rather than assumed from the sibling that works.** `useSetTaskAssignees` and `useSetTaskLabels` had
the same shape — patch a detail cache, revert it on error — and only one of them was wired to
anything a screen actually rendered. `TaskAssignees` reads `useTaskAssignees`'s own cache; `TaskLabels`
reads `task.labels` off the **list** query. Copy-and-adjust from a working neighbour carried over the
pattern without checking that this consumer's read path matched.

**The failure mode of a stale cache patch is silence, not an error.** The vendor write succeeded,
the server was never at fault, and the screen simply looked like the press had done nothing — which
is a worse failure than a visible one, because there is nothing to notice until somebody reloads and
finds the change was real all along.

**A generic refusal message is a second bug hiding behind the first.** `setLabels.isError` already
rendered something before this change; it said "the vendor refused" for every failure, where
`explain()` — already used three other places in this same screen — would have named a 403 from a
404. The first fix made the second one obvious.

## 2026-08-19 · files.png has a file open (#145, PR #175)

**A screenshot is a fixture, and a fixture has to be chosen, not assumed.** "The first non-directory
entry in the listing" sounded like a safe, generic rule and broke on the first real checkout it ran
against: `.agents/skills`, a symlink to a directory, comes back `isDirectory: false` from the probe.
The screen answered correctly — git refused to list it, and it said so — which produced a perfectly
honest, perfectly useless screenshot. A deterministic rule is only as good as the case it was never
tested against; `README.md` is a worse-sounding rule (a hardcoded name!) that is actually more
robust, because it names a thing rather than a position.

**A bug found while building the fixture is not the fixture's bug.** The symlink misclassification
is real and worth fixing, in `GitWorkspaceProbe`, on its own — it went to #174 rather than into this
diff, because folding it in would have made one PR answer two questions and neither answer would be
checkable on its own.

**A PR body with backticks in an inline shell string fails silently.** `gh pr create --body "…\`…\`…"`
executed the backticked span as command substitution rather than passing it through, and the first
attempt produced no error and no PR — `gh pr list --head <branch>` came back empty, which is what
caught it. Markdown code spans in a PR body belong in a heredoc or a `--body-file`, never inline.

## 2026-08-19 · Compare the seam an issue declared with the files its worktree touched (#149, PR #176)

**A seam's own text tells you how to parse it, if you read real ones before writing the parser.**
Every backtick in the section looked like the obvious extraction rule and would have double-counted
a file this repository's own issues mention twice — once as its declaration, once in a sentence
about it two lines later. Reading three real Seam sections before writing the TypeScript — the same
dry-run-in-plain-JS discipline this session used for the merge-tree parser in #146 — caught it before
it shipped rather than after.

**Two facts written the same way are not the same fact until you check the root they're relative
to.** This repository's own issues declare a seam path at whatever root felt natural in the moment —
`shared/http/queries.ts`, `src/frontend/shared/http/queries.ts`, a bare `queries.ts` in prose — and a
worktree's diff always answers the repository's own root. Exact-equality matching would have made
almost every real declaration in this repository's own history read as "untouched" on the exact file
it named, which is a comparison that is wrong in the direction that erodes trust in the feature
fastest: a false negative on the common case.

**A design decision the issue left open got made and written down, not guessed at.** "A Seam that
names a directory rather than a file" had no answer in the issue; this PR picked suffix-prefix
matching for directories and said so in `seam.ts`'s own doc comment, including the one case it
knowingly gets wrong (a namespace reference like `` `BuildingBlocks/World` `` reads as a directory
declaration nothing ever matches) and why that cost is the acceptable one.

## 2026-08-19 · Choose the model when starting a run (#122, PR #181)

**A fact known at Start and a fact known from Usage are different shapes even when they share a
name.** `RequestedModel` almost went inside `GetRun`'s `UsageRow`, right next to the already-shipped
`PermissionMode`, because that record was already the place `Model` lived and copy-and-adjust reached
for the sibling that looked right. Both `RequestedModel` and `PermissionMode` are known the instant a
run starts; `UsageRow` is null until `totalTokens != 0`, which for a long-running step is minutes
after that instant. Building this feature is what made the existing placement's cost visible enough
to fix — the same class of thing had shipped once already, in #160, without anyone hitting the case
that exposed it, because nothing before this needed the field to show up before usage did.

**Verifying a CLI flag against `--help` is not the same as verifying the spawned command.** All three
`ModelArguments` entries were checked against real `--help` output before being written into
`AgentCatalog`. That confirms the flag exists; it does not confirm this product assembles the
argument list correctly, in the right order, next to session and prompt arguments that were already
there. Only starting real runs against `opencode` and `claude-code` on a live isolated instance and
reading the PTY's own recorded command line — `opencode --model opencode/big-pickle --prompt …`,
`claude --session-id … --model sonnet …`, and the resumed line carrying the same flag — closed that
gap. `--help` verifies the vendor; running it verifies this codebase.

**A dependency that was never installed in this worktree made an early typecheck lie.** `rtk tsc
--noEmit` returned "No errors found" before `pnpm install` had ever been run in this worktree — a
success message with nothing behind it, the same shape as the `rtk git push` failure this session
already learned to distrust. `node_modules/.bin/eslint` failing with `MODULE_NOT_FOUND` is what
surfaced it. A green result from a tool this session already knows can misreport is not evidence
until something independent — here, a second tool refusing to run at all — corroborates it.

**A CLI's own text and its own behavior can disagree, and the behavior wins.** The issue's Check
section describes `opencode`'s flag as `-m <model>`; the binary's `--help` lists `-m, --model` as the
same flag, two spellings of one option. Long-form `--model` was kept, matching the other two vendors,
because the issue's phrasing was shorthand for "the flag opencode accepts," not a spelling mandate —
confirmed by reading `opencode --help` directly rather than treating the issue's wording as the
source of truth for a CLI it does not control.

## 2026-08-19 · Retire the read-orca standing rule (#177, PR #179)

**The tempting argument for deleting a rule was the wrong one, and measuring said so.** The obvious
case was decay — the trips stopped paying, so retire the instruction. The data refused it: 27 of 52
retro entries cite orca, 4 of the last 18 still do, and the most recent (#167) says outright "The
trip was worth making." The honest case had to be rebuilt on a different fact — that every finding
the rule bought is already in the comment of the code it explains, six of them carrying an explicit
`Prior art: orca's <file>` pointer and one pinned by a named test. **A rule is spent when its findings
have been re-homed, not when it stops firing**, and the second is checkable by grep while the first is
a feeling.

**A rule that contradicts its own restatement is invisible, because nobody reads both in one sitting.**
`AGENTS.md:57–61` exempted copy changes from the orca trip; `propose.md:19` instructed it
unconditionally. Grilling this issue meant executing `/aio:propose` on a change to `/aio:propose`, and
the disagreement only surfaced because one agent had both open at once. The four-copies-of-one-rule
shape is what `AGENTS.md` already warns about in its own second paragraph — "a second copy of a rule
is a copy that goes stale" — and it had gone stale twice: once in the `realpath` citation
(`retro-log.md:490`), once in when the trip is required.

**The gate's own documentation had drifted, and the gate is where that matters most.** `aspire do ci`
counted **452** tests; `implement.md` says it counts 250 and `AGENTS.md:533` implies it (118 + 132).
The reasoning around both is intact — `ci` has no halves, and `dotnet test` without the solution
really did run a subset — but an agent checking its own run against a documented 250 reads 452 as "I
ran the wrong thing". Left out of the diff on purpose, so one PR answered one question, and filed
against #91, which is already open on stale claims in those same two files.

**A permission denial is not a gate failure, and the difference is worth keeping straight.** The merge
was blocked by the harness classifier after `ci` had passed on the exact commit. `/aio:ship` says a
failure halts, holds and comments the reason in the tool's own words — which was right here even
though nothing was wrong with the change. The temptation was to push the branch to `master` and reach
the same end state; that would have bypassed exactly what the denial exists to stop, and the halt is
the cheaper outcome.

## 2026-08-19 · A stale origin/HEAD measures against the wrong branch (#178, PR #180)

**The fix the item proposed did not fix the bug, and one command said so before any code was written.**
Grilling had settled on distrusting `origin/HEAD` when it is an ancestor of `origin/main` — a clean,
cheap test that reads as obviously right. Run against the folder that reported the fault, it says
trust it: `origin/feature/scaffold` carries 8 commits `origin/main` does not, so it is nobody's
ancestor. The item's own evidence block contained the number that refutes its own recommendation, two
sections apart, and nobody had put them side by side. **A "to decide when proposing" note is a
hypothesis, not a decision** — running it against the reported case costs one command, and here it was
the difference between shipping the fix and shipping a no-op with tests that pass.

**Ancestry answers "did this branch merge"; divergence answers "which branch is this repository
working on".** They look interchangeable and the second is what a trunk probe needs. Ancestry cannot
separate a default branch that moved (believed 8, real 186) from a `develop` trunk beside a `main`
nobody has touched — both are diverged, and only which side carries the commits tells them apart.

**A pointer sorted into a list of names is how a cache became an answer.** `origin/HEAD` sat first in
`Trunks` among four branch names, which made it look like the most reliable entry rather than a
clone-time cache no `fetch` refreshes at any depth. The type mismatch was the bug: everything else in
the list is checked for existence, and the one entry that needed its *content* checked was the one
returned unexamined.

**A baseline is the only thing that turns "unrelated failure" into a finding.** `aspire do ci` failed
with `EndToEndTests` 4/4 red on a Playwright navigation timeout — the temptation being to call it
flaky, which it was. Establishing that cost a detached worktree at `origin/master`, a `pnpm install`
in it, and a run that passed 4/4. Worth it: the failing test opens the folders screen, which is the
screen this change alters, so "obviously unrelated" was a guess about the one place it could have
been true. The suite is now **457** tests, not the 250 `AGENTS.md` and `implement.md` still claim.

**Verifying against the product found nothing wrong and was still the point.** The five new tests all
pass, but they assert what this file believes git does. Adding the real folder to a running instance
is what showed `origin/feature/scaffold` become `origin/main` and 1414 changed files become 0 — the
two numbers the item was written around, neither of which any fixture could have produced.

## 2026-08-19 · A held run keeps its terminal (#183, PR #184)

**A predicate that agrees with the domain in four of five states is not a rounding error.**
`state === "running"` was correct for `running`, `merged`, `done` and `failed`, and wrong for exactly
one — `waiting`, the state the domain documents as "holds for a person **without ending**". The one it
got wrong is the one where somebody has to reach the keyboard, so the failure landed precisely where
the cost was highest. Four of five is what let it ship: #80 was specified as *a finished run* and
implemented as *not running*, and the two words differ only in the state nobody had yet produced.

**The server already knew, in prose, in a file the frontend never reads.**
`RunningWorkflowProbe` says "Unfinished means Running or Waiting … Merged, Done and Failed are
history", and `Sidebar` was the single surface that agreed with it. The other four asked the question
themselves. A rule stated once as a comment and re-derived at every call site is a rule that holds
until the fifth caller, which is why this became a module rather than four edits — and why the table
is a `Record<RunState, boolean>`, so the sixth state cannot be added silently.

**Fixing what a screen shows made a poll that never fired start mattering.** `useRun` stopped polling
the moment a run held, which was invisible while a held run was drawn as history: nothing was waiting
for it to change. Give the same run a terminal and the dead poll becomes a dead terminal, because the
swap to its record is what the next poll is for. **A latent bug is one whose symptom is being masked
by a second bug**, and the mask here was the thing being removed — which is the argument for widening
a seam rather than the argument against it.

**`tsc` in a fresh worktree reports "No errors found" having compiled nothing.** `pnpm typecheck`
before `ci-install-frontend` runs npx's "this is not the tsc command you are looking for" and exits 0,
which a filter reads as a pass. The gate is one command for exactly this reason: the halves are not
merely slower to type, they are individually capable of passing while doing nothing.

**A capture set is evidence only for the screens in it.** This change alters the run screen's Agent
tab, and `capture.sh` shoots eight screens, none of them a run — `run-terminal.png` sits in `current/`
as a legacy file the script never retakes. Saying so beat both alternatives: a screenshot of an
unrelated screen, or silence that reads as "no screen moved".

## 2026-08-19 · A pasted screenshot reaches the agent as a file (#182, PR #191)

**The measurement the item demanded first said the bug was not there, and that was the most useful
thing it could have said.** #182 required one thing before any code: what Ctrl+V does today. The
answer was that both keys already attach the image, for both agents asked — because xterm's empty
bracketed paste is not a dropped paste but a *trigger*, which Claude Code and OpenCode answer by
reading the operating system's clipboard themselves. The item's mechanism was right in every detail
and its conclusion — *"the agent receives a paste that says nothing was pasted"* — was wrong. An item
can be correct about the code and wrong about the outcome, and only running it separates the two.

**A pre-code measurement is not a formality when its answer can invert the design.** Had the change
been written to the item as specified, it would have typed a path *in addition* to what already
happens, and delivered two attachments from one keypress — failing the item's own check, in a way no
test would have caught because both halves work. What the measurement bought was `preventDefault`
instead of a second channel: the fix is not "add our route" but "take the event away from xterm".

**Working by accident is still worth replacing, and the reason has to be written down or it reads as
make-work.** The old behaviour depended on the browser and the agent's process sharing a host, and on
a vendor behaviour this product neither asks for nor checks. Both are true today and neither is
ours. The argument for the change survived the measurement only because it stopped being "it is
broken" and became "it is not ours, and #34 removes the coincidence it rests on".

**Prior art's reasons matter more than its code, and two of orca's five lessons did not survive being
checked.** It reads `navigator.clipboard.read()` because *"the paste event is not enough"* — true of
Electron's clipboard bridge, false in Chrome, where a probe showed `types = ["Files"]` and
`getAsFile()` returning the bytes. And it chunks at 512 KB with a slot ceiling and a TTL because its
transport has a message limit; over HTTP that is machinery for a constraint that does not exist. Only
the cap and *text wins* carried over. **Copying an answer whose question you have not re-asked is how
a 32 KB limit becomes a chunk table in a product that has neither.**

**Two panes doing the same thing is a differential, and it found a bug older than the change.** The
folder terminal collapsed the paste to `[Image #1]`; the run terminal typed a visible path. The cause
was that `ScreenFrame` rebuilds a live run's screen and tracks mode 1049 only, so xterm never learns
the agent enabled bracketed paste. Which means **plain multi-line text pasted into a live run has been
submitted one line at a time all along** — measured: `line one` went on its own and the agent replied
"waiting for the rest". Nobody had reported it. Adding a second consumer of an existing path is a
cheap way to audit it, and #193 exists because there were two panes to compare rather than one.

**A guard that validates after it writes is a guard against being believed, not against being wrong.**
`capture.sh` reads every shot back and says why — *"a picture nobody looks at is trusted exactly when
nobody is looking at it"* — and still had already overwritten the committed screenshot by the time it
looked. It exited non-zero with a blank `folders.png` in the working tree, which is safe only for
somebody reading the output. #194. **Where the check sits in the sequence is part of the check.**

## 2026-08-19 · A worktree screen shows the issue it exists to resolve (#185, PR #190)

**Reading the corpus beat reading the convention, and the margin was 42 issues.** The tab had to draw
the issue's check section as a list, and both the Definition of Ready in `/aio:grill` and the four
newest issues agree that a check is written as `- ` bullets. Measured over the 60 most recent issues
here: 10 use bullets, 41 use `1. `, 4 write prose, 4 have no check section at all. A parser built to
the documented convention would have found a list in 10 of 60 and drawn *"this issue writes its check
as prose"* — confidently, in the product — over 42 issues that have a perfectly good list. The
convention a repository documents is what it decided; the corpus is what it does.

**Naming one thing to share is not naming the boundary.** The Seam section said to reuse
`IssueComments` "exported, not copied, so the two tabs cannot drift on how a comment thread is
fetched or rendered" — and the proposal did exactly that, while copying the seventy lines of header,
body, labels and held block that surround it. The one file the issue thought to name was the one file
that did not get duplicated. What needed sharing was the whole panel, and nothing in the item could
have said so, because the duplication had not been written yet when the item was.

**"No backend seam" was true, and still incomplete.** The item was right that `useTask` and
`useTaskComments` already return everything the tab reads; no endpoint changed. Three frontend files
outside the declared Seam changed anyway — a section-slicer lifted out of `seam.ts` so two readers of
one issue body cannot disagree about where a section ends, and a `flush` variant of `Prose` because a
paragraph's own margin inside a list row pushes the text off the marker beside it. **A Seam section
enumerates the files you expect to add. It cannot enumerate the ones your addition makes wrong.**

**The mechanism that isolates a worktree also isolates a feature from its own evidence.** This tab
resolves its issue *from a run*, and since #52 a worktree's database is derived from its checkout
path — so the worktree implementing the feature is the one place on the machine with no runs and
therefore nothing to show. Verifying it meant seeding run rows into that throwaway database, pointing
at real mirrored bodies, and removing them before the screenshots were retaken. Worth knowing before
the next feature that reads run history: the fresh database is a property of the tool, and it lands on
whoever is building against it.

**Two observations taken at different times looked exactly like two code paths disagreeing.** The
list endpoint said a task was held and the by-key endpoint said it was not, and the obvious reading —
`GetTask` forgets to derive `held` — was wrong: both call the same helper, and a background sync had
overwritten the local label between the two reads. It cost a detour into code that was correct all
along. **A disagreement between two readings is not a disagreement between two code paths until both
readings are taken at once**, which for anything a poll can overwrite means in one command.

**Six screenshots moved that this change never touched.** `capture.sh` retakes the whole set against
whatever machine runs it, so a retake carries that machine's folder list and its inbox: `files`,
`folders`, `history`, `settings`, `tasks` and `workflows` all differed, and none of them because a
screen changed. `/aio:implement`'s instruction to commit only what actually differs is the only thing
standing between the design record and a record of whose laptop last ran the script.

## 2026-08-19 · A worktree totals what its runs cost, read, and waited for (#187, PR #192)

**A number already on the wire is not necessarily a number you can add.** The issue scoped this as
frontend-only — every figure was "already on the wire", and it was. But `GetRun` formats cost at the
edge, `"$0.42"` or the literal `"unknown"`, and two runs costing 0.1145 and 0.1144 both display as
`$0.11`: adding what the screen shows gives $0.22 where the total is $0.23. The runs that resist
parsing are exactly the unpriced ones a total has to name rather than drop, so the parse fails hardest
where it matters most. **Formatting at the edge is a decision about who is allowed to do arithmetic**,
and it is invisible until the second consumer wants to.

**A requirement can name the wrong source and still be right about the goal.** The issue asked for
hold-waiting time "summed from the steps every run already carries". It is not there: a hold is its
own step, but nothing ever begins it — `Advance` calls `Began` only when it dispatches, and on a hold
calls `Held`, which re-completes the step that *just ran* as `holding`. `RunStep.Begin` is the only
writer of `StartedAt`, so a hold-flagged step never has one, and "sum the steps where `hold` is true"
is the constant zero drawn as a measurement. The figure survived by changing where it came from — the
stamp on the run continuing it — not by being dropped. #196. **A check bullet that cannot be satisfied
from the data it names is a finding about the data, and the deliverable usually still stands.**

**"The first entry of that kind" is a guess wearing the clothes of a fact.** The issue described the
spawned command as the run's first `"runner"` log line, and on the happy path it is. That kind also
carries an unknown agent, a spawn failure, the exit code and a person's steer — so the one run that
most needs reading, the one whose binary was missing, would have printed "Could not start claude"
under a column headed with the command. Matching the `$ ` prefix the writer actually emits costs one
line. **Reading the writer beats trusting the description of the reader**, and both were in this
repository.

**An undefined design token fails silently, and copying a neighbour's line copies its bug.** The big
figure here started as `var(--text-xl)`, lifted from `HistoryScreen`'s spend band. The scale is xs,
sm, base, lg, 2xl — there is no `xl`, so that band has been rendering at inherited size for as long as
it has existed, and nothing failed. A missing colour would have been obvious; a missing size is just
slightly wrong. #197. **The tokens stop drift only where something checks that the token exists.**

**A screenshot set captured against machine-local data cannot be retaken one screen at a time.** All
eight shots moved on a retake, because `capture.sh` photographs whatever folders, tasks and runs the
capturing machine happens to have — so retaking the two screens a change actually moved means
reverting six by hand, and the honest set and the convenient one differ. #195.

**A screen that falls back to home makes a wrong URL indistinguishable from a right one.** Once
Telemetry became a worktree's tab, `capture.sh` still asked for it on the checkout — and the screen
answered with its home, so `telemetry.png` would have become a second photograph of Overview, past
both of the script's guards, because a rendered wrong screen is a rendered screen. The same fallback
that spares a person a blank panel from a stale bookmark is what hides a broken URL from anything
automated. **A guard that asks "did something render" cannot answer "did the right thing render";
only naming the expected screen can.** #200.

**A design target drawn before the features it lacks measures growth, not what to cut.**
`docs/design/target/overview.png` shows six fixed tabs against the nine that shipped, but it predates
Issue, Review and Telemetry — so it is evidence that the strip grew by half without anything leaving,
and no evidence at all about which six should stay. Deciding that needed the question the tab answers
— folder or scope? — not the older picture. #200.

## 2026-08-20 · A folder need not be a git checkout (#104, PR #203)

**A null that deliberately fuses two causes is correct for one caller and unusable by another, and
the fix is not to split it.** `IWorkspaceProbe.Read` answers null both for "not a repository" and
for "git could not be asked", on purpose: `FolderProbePoller` reconciles whole worktree sets, and
the only safe response to an incomplete answer is no answer. This issue needed exactly that
distinction — and splitting the seam to get it would have traded the poller's safety for the
dialog's convenience. It was drawn at the one caller that needs it instead, from the presence of
`.git`, in a handler already making filesystem calls. **A seam that is vague on purpose is not a
seam with a gap in it; the question belongs to whoever has a person standing there to answer.**

**A generated migration decides your backfill, and it decides it wrong for any flag whose true
answer is true.** `dotnet ef migrations add` wrote `defaultValue: false` for the new
`IsRepository`, which would have told every folder already on every machine that it was not a
checkout — the sidebar dropping every branch row at once. Correcting it to `true` then walks into
the opposite trap: `false` is the CLR default for a bool, so EF **omits the column from an INSERT**
whose value is false and the store default fills in true — corrupting precisely the rows the change
exists to create. Both halves were needed: backfill true, then `DROP DEFAULT`. **A column default
that agrees with the CLR default is invisible; one that disagrees with it is a data bug with no
compiler and no test that naturally reaches it.**

**When a collection becomes legitimately empty for the first time, every count derived from it is a
suspect.** `ListFolders` had always synthesized a primary branch row, so `branches` was never empty
and `branches.length - 1` was a safe way to count worktrees. One folder with no branches later, the
Folders card read **"-1 worktrees"**, beside a branch glyph with no branch and a "clean" describing
a working tree that does not exist. Every gate was green — 485 tests, typecheck, lint, build — and
it took one look at the screen. **Widening a type's domain does not fail where the domain is
defined; it fails wherever somebody assumed the old one**, and arithmetic on a length is the
cheapest place to hide that.

**"The gate passed" has a base, and a change long enough to be worth gating can outlive it.** `ci`
ran green here on `df07fae`, and #201 merged eleven minutes later — so the green referred to a tree
that was no longer what would be merged, while nothing about the branch, the PR or the report
looked any different. `sync` catches it by asking for the run *on what is about to be merged*, and
the only thing that surfaces it is re-reading `origin/master` immediately before merging rather
than trusting the check that already said yes. **A gate result is a statement about a commit, not
about a branch.**

**A scope decision made to dodge a collision should be revisited when the collision merges.** Files
and Review were deferred out of this change because #201 owned that block of `FolderDetailScreen`
and was open for review; rule 5 says the branch is the claim. #201 then merged mid-implementation,
and the deferral — a follow-up issue, its own PR, its own gate run — would have survived its own
reason by pure momentum, for four lines on a table that was by then already on `master`. **The
reason for a workaround is worth re-reading at the moment you would otherwise ship it.**

## 2026-08-20 · Ask GitHub for open and closed issues separately (#206, PR #208)

**A ceiling written for one set stops being a ceiling when a second set moves in under it, and
nothing about the number looks different.** `MaxIssues = 100` was sized for an inbox, with a comment
arguing about a repository with four thousand open issues. #116 changed the query from `state=open`
to `state=all` so a run could keep reading its issue after somebody closed it — correct, and it did
not touch the number. From that moment the archive shared the budget, and the archive is the half
that only ever grows: 86 pull requests and 74 closed issues took 77 of the 100 slots, and the inbox
got what was left. The reviewable diff was three characters. **When a change widens what a request
returns, the limit on that request has become a limit on something else.**

**A row that was never fetched is indistinguishable from a row that does not exist, so the mirror
deleted the evidence.** `SyncTasks` prunes what the vendor no longer returns, which is the right
rule for a deleted issue and the wrong one for an issue that fell off a page. Nineteen open issues
were absent from `/api/tasks` and from every count on the screen; `roadmap`, `backlog`, `platform`
and `mobile` were not chips reading zero, they did not exist. Nothing was red anywhere. **A cache
that prunes on absence turns a truncated read into a confident, permanent lie.**

**The bug was shaped like the work, which is why nobody caught it for ninety issues.** `sort=updated`
means closing an issue or merging a pull request lifts it to the front and pushes an open one out of
the window, so the inbox narrowed every time the loop finished something, and what survived was
exactly what this loop had touched recently. The labels left on screen looked like ours because they
*were* ours. **A list that quietly narrows toward what you did last reads as focus.**

**Splitting one request into two changed what an absent row means, and the fix was only half a fix
until pruning learned that.** Asked separately the two sets are not the same shape: `state=open`
returns all 42 here and would fit under any ceiling a to-do list deserves, while `state=closed`
returns a truncated 100 of 160 and always will. So open-and-absent is deleted, and closed-and-absent
is merely the 101st by `updated`. Shipping the split alone would have deleted about sixty archived
rows per sync and re-fetched most of them — churn with a green suite, invisible on screen, and
destroying precisely what #116 was bought to protect. **When a single answer becomes two answers,
every rule that read "the vendor did not return it" has to be re-derived per answer.**

**A backend change with no frontend line in it can still move a whole screen, and the screenshot
rule does not know that.** `/aio:sync` decides whether to post visual evidence by asking whether the
diff touched `src/frontend/app/` or `src/frontend/features/`. This diff touched neither and took the
Tasks screen from 20 rows and seven Kind chips to 44 and eleven. The shot was worth committing and
worth pinning to the squash SHA anyway. **A path test answers "did the code that draws this change",
which is not the question "did this screen change".**

## 2026-08-20 · A folder without git is a row that goes somewhere, not an empty group (#205, PR #207)

**A predicate that runs per child never runs for a childless parent, and the folder's own name was
inside it.** The rail matched what you typed against
`` `${folder.name} ${branch.name} ${searchable}` `` — the folder's name is right there in the string
— but the comparison lives in the per-branch loop, and a folder that is not a checkout has no
branches. Over an empty list the loop body is dead code, so the one field that would have kept the
row was written, shipped and never once evaluated for it. Typing `dir` left `tree` empty and the rail
answered "Nothing matches. Clear the filter to see every folder." with `director-assement` in the
workspace. **A field appearing in a predicate is not the same as the predicate being reachable for
the row that needs it.**

**The failure only appeared when somebody went looking.** The filter's fallback was
`branches.length > 0 || (!query && !liveOnly)`: with nothing typed the folder was in the rail, and it
fell out on the first keystroke. The default state — the one anybody demonstrating or screenshotting
sees — was the working one, and reaching the broken state took the exact action the control exists
for. **A guard whose fallback is "show it when nobody is filtering" hides its own bug from every
casual look.**

**Giving a dead area a job only fixes it where the job has an effect.** #203's comment on this row
says the name "is the widest thing in the row and it used to be the one part of it that did
nothing", and made caret, icon and name one button that collapses. For a checkout that is right. For
a folder with nothing under it the widest target went from doing nothing to doing nothing visible,
and now it advertised: the tooltip read "Collapse or expand" over a group that draws no rows, and
the only way in was a 13px icon at the end of the row. **The second version of a dead control is
worse than the first when it makes a promise.**

**A screenshot retaken on a machine without the case records the absence of the case, and the guard
cannot tell.** #203 retook `docs/design/current/folders.png` precisely because it changed how a
folder without git draws — and the workspace it was taken in held two checkouts and no plain
directory, so the committed picture shows the case nowhere. `capture.sh` reads every shot back
before it counts, but what it measures is share-of-one-colour and mean luminance: whether a screen
rendered, and in which theme. Both pass on a true picture of the wrong workspace. **A guard that asks
"did a screen render" cannot ask "is the thing I changed in it", which is the question a design
record exists to answer.**

**The gate had passed on a base nobody was about to merge.** `aspire do ci` was green on this branch
at 485 tests while it sat cut from `2145ca5`; by merge time #208 and a retro entry had landed and
`origin/master` was two commits ahead. Rebasing and re-running took it to 491 — the six #208 brought
— and it passed, so nothing was hiding. That it passed is not the point: the run that would have
been cited measured a tree that would not exist after the merge. **#189 is open about this, and an
unattended loop is where it stops being theoretical: nothing about a green gate says how old its base
is.**

## 2026-08-20 · The Tasks panel renders its issue body and comment thread (#204, PR #209)

**A token-saving filter turned a command that never ran into a passing one.** `rtk pnpm typecheck`
printed `TypeScript: No errors found` twice in this change, and both times the truth underneath was
`sh: tsc: command not found` — this worktree had no `node_modules`, because nothing in it had ever
installed any. The filter summarises a compiler's output; asked to summarise a shell's failure it
found no error lines and said so, and exit code went with the summary. The first commit was pushed
claiming the shape compiled on the strength of it. `aspire do ci` caught it minutes later only
because the gate installs dependencies before it builds. **A wrapper that reports "no errors found"
is reporting on the output it parsed, not on whether the thing it was wrapping happened — and the
absence of errors is exactly what a missing binary produces.**

**A doc comment stating a design premise does not fail when the premise dies.**
`SourcedText.Trim` caps a mirrored body at 4,000 characters and explains itself: "The detail panel
shows a paragraph, and the agent reads the real thing from the vendor." #162 retired the first half
for the run's Issue tab, #185 gave the folder tab the same component, and this change did it for the
panel that the sentence is literally about. Three screens now render a body in full that arrives four
fifths of itself, and 8 of the 41 open issues are cut. Nothing broke, nothing warned, and the comment
still reads as current because it describes an intention rather than a fact. **A premise written in
prose beside the code that depends on it is the one kind of coupling no compiler, test or analyser
checks.**

**The same component was correct twice at 720px and wrong the first time it was 388.** `Prose` had
shipped to two callers, both in a wide region. Dropped into a 388px column it overflowed twice over:
a table widened its container, and a path in inline code — which this repository's issues are written
almost entirely out of — is one unbreakable token. Neither is a new bug in `Prose`; both were latent
in it, and the panel's own `overflow: auto` would have turned either into *Start run* scrolling
sideways out of the column. **A shared component's second caller tests its behaviour; a narrower
caller tests its assumptions, and width is an input nobody thinks to vary.**

**The risk the issue was made to announce turned out not to exist, and checking cost one grep.**
Rule 4 asks you to say in the issue before splitting a file somebody else owns, and this one said it
might have to lift `IssueComments` out of `RunScreen.tsx`. It did not: `IssueTab.tsx` already imports
`IssueView` across that exact seam, so the diff to the neighbour's file was the word `export`. The
declaration was still worth making — but the move it warned about was priced before anyone looked for
the precedent that made it unnecessary. **Announcing a conflict is cheap and correct; paying for it
before checking whether the codebase already solved it is neither.**

**The capture path cannot click, so half of a disclosure is unverifiable by the tool that records
it.** `capture.sh` is headless Chrome with a virtual clock, which is why it reads a screen back
before trusting it — but a clamp with `Show all` and a thread behind `Read comments` are two states
each, and screenshots only ever see the closed one. Both open states were checked by temporarily
defaulting them open and reverting, which exercises the real render path with real vendor data and
leaves the two `onClick` handlers untouched by any click. **A screen with state has more states than
the design record can hold, and saying which one was photographed is part of the check.**

## 2026-08-20 · ADR-0003 — local persistence runs on SQLite, decided by a throwaway spike (#210, PR #211)

**The feared coupling was free and the unfeared one broke 32 tests.** The issue's check named the
`text[]` as the risky point, and EF Core 10 mapped the `PrimitiveCollection` to JSON `TEXT` with
zero code. What nobody listed — `DateTimeOffset` not ordering on SQLite, and a vulnerable
`SQLitePCLRaw` transitive failing the warnings-as-errors build — is exactly what a paper-only ADR
would have shipped wrong twice. The risk you can name up front is rarely the one that bites; a
spike's failures are its product, and both went into the ADR with their remedies.

**The decisive question was product, not technical.** Two of the three grill answers — "always
local, one person" and "Docker as a start dependency is the pain" — settled the database choice
before any code was read; the measured 7.9s-versus-11.7s was too modest to decide anything. When
an ADR seems to hinge on benchmarks, check first whether a product decision already made it.

**A number a document asserts is a number a run contradicts.** AGENTS.md still says `ci` counts
250 tests; the gate counted 491 across the same seven assemblies. The rule held — every assembly,
zero failures — because the rule is a shape, not a number; the stale count is now the only wrong
sentence in the gate's description and worth its own one-line fix.

## 2026-08-20 · The packaged launcher stopped starting Docker, and the check it needed did not exist (#214, PR #215)

**The verification attached to the developer's live database and reported 200.** The whole point of
the change is that an installed app decides where its database is when nobody tells it. The first
launch came up, served the SPA, listed folders — and `lsof` showed it holding the main checkout's
`.harness/data/aiorchestrator.db`, with the dev server writing to the same file. `open` hands an app
the environment of whoever called it, and this machine's shell exports
`ConnectionStrings__aiorchestratordb`. The code was right: explicit wins, which is what it is built
to do. The *test* was wrong, in the direction that looks like success. **When a change's claim is
"the app decides X when nobody says", the check has to prove nobody said it — and on a developer's
machine somebody usually has.** The launcher now logs which database it opened, because the only
reason this took an `lsof` to find is that a process with no terminal had no way to say.

**Two of the four dead package pins cited issues belonging to another repository.** `Docker.DotNet`
explained itself with "#257" and a feature-management pin with "#331"; this repository's highest
issue is #212. They came from the sibling project with the skeleton. A comment that cites an issue
number is checkable in one command, and a citation that crosses a repository boundary makes a plan
look permanently pending instead of absent.

**The check written at grill time forbade the word, not the behaviour.** It asserted
`grep -ci docker launch.sh` → 0. The answer is 3, and all three are inside the comment that records
what was removed and why — the assertion would have deleted the explanation to pass. The useful form
is that no docker line *executes*: `grep -v '^\s*#' | grep -ci docker` → 0. **A check phrased against
a file's text rather than its behaviour can only be satisfied by making the file worse.**

**And the gate's own number was contradicted the day after it was dated.** `d16c57b` recorded 491
tests on 2026-08-20 as an observation rather than a contract; this change counted 489 across the same
seven assemblies with zero test files differing from `master` and no filesystem-driven theories
anywhere. The shape held, which is why the shape is the rule.

## 2026-08-21 · A phone gets a 390px layout, and tests measure it (#217, PR #218)

**A check phrased against the wrong property is satisfied by something three levels away.** The issue
asked for `document.scrollingElement.scrollWidth <= window.innerWidth` at 390×844, and that assertion
was green on `master` with every defect in place — the shell's root element carries `overflow: hidden`,
so content wider than the window is clipped rather than scrolled and the document measures clean at
*every* width. Its own fourth check knew the risk and named it ("la aserción tiene que depender del
viewport"), which is why the answer was to measure before committing to the assertion rather than to
implement the sentence. The property that changed with the viewport was the screen's own heading: 84px
of `change-217` in a 43px box, red on four routes at 390 and green on all five at 1280. **The
neighbouring lesson from #214 was that a check phrased against a file's text can only be satisfied by
making the file worse; this one is that a check phrased against the wrong element can only be satisfied
by accident.**

**The measuring instrument was lying, and it took a page that printed its own width to catch it.**
Four screenshots were taken with `--window-size=390,844` and read as evidence — a header cut off at
the edge, a merge gate filling the viewport. Chrome headless on macOS clamps the window to **500** and
scales the result into a 390px PNG: measured at `innerWidth` 500 × 757 under both `--headless` and
`--headless=old`. So the "header cut off by the edge" never existed at 390; the header *fitted*, and
what was broken was the title inside it. `capture.sh`'s comment that "this repo has no browser
automation dependency" is false since `EndToEndTests` existed, and it is the reason the only tool in
the repository that can render this width was the one nobody reached for.

**Two layouts are two branches, not one branch with twelve conditional properties.** The first attempt
at the merge gate wrote the narrow/wide difference as ternaries on `whiteSpace`, `overflow`,
`textOverflow`, `flex` and `flexDirection`. One of them — a wrapper with `flex: "none"` — sized a box
to its longest line at 1440 and took the ellipsis off the Seam detail, moving a layout nobody had
complained about. Caught by comparing ink profiles of the two PNGs, not by reading the diff. **When a
change is "this looks different below N pixels", the cheapest proof that the other side did not move is
for the other side to be the same code.**

**And the issue's own list of green gates was one item optimistic.** It recorded stage 2 of
`validate-design-system.sh` as red on `master` and stages 1 and 3 as green. Stage 1 is red too, in nine
files this change never touches, at `origin/master`. A check written at grill time is an observation
about the afternoon it was written on.

## 2026-08-21 · Six blocks leave FolderDetailScreen, and the threshold that measured them had expired (#198, PR #216)

**A line-count threshold is an observation about the afternoon it was written on, and this one was
self-invalidating.** The issue's check said "under 1950", derived as 2235 − 334. By the time the work
started the file was 2334, because it had grown 99 lines while the issue waited — which is precisely
the defect the issue exists to fix. So the number could only be met by doing more than the issue
described, and the honest move was to say the arithmetic had expired and report the measured figure
instead: 1133. **A check whose denominator is the thing being fixed cannot be a contract; it can only
be a reading.** The scope widened from one block to six, and that was written into the issue before
the file was touched — correcting the proposal's own *Qué no hace* section rather than contradicting
it in silence, which is the whole point of rule 4.

**"Self-contained" is a grep, not a feeling.** Five of the six blocks read zero module-level symbols
from the file they left — not `fixedTabs`, not `tabsFor`, not `runOf`, not the `Tab`/`FixedTab` types
— because everything already arrived through props. The sixth, `CommandsTab`, reached backward for
`lastRun` and `CheckLine`, so those two plus `checkLine` and `ago` went with it once a search proved
nothing else in the repository called them. **What a block reaches backward for is what separates a
move from a redesign, and it is one search per block, before the first line is cut.** The same search
is what kept `FolderDetailScreen()` itself out: it owns the tab strip's types and table, so those
stay, and splitting its 902 lines is an architecture change wearing a refactor's clothes.

**And `mergeable: CLEAN` is a claim about text, which is the least informative thing about this
merge.** `master` had moved three commits, one of them editing `FolderDetailScreen.tsx` — the exact
file this change empties by 1231 lines. Git reported no conflict and would have been right about the
characters; a deletion of 1231 lines and an edit inside them do not overlap textually until they do.
Running the gate on the branch alone would have measured a tree that never existed on `master`. So
`origin/master` went into the branch first and `aspire do ci` ran on the result — 15/15 steps, 7
assemblies, 526 tests, 0 failures — and both of master's edits were confirmed present by name
afterwards, because "the merge succeeded" and "the other side's change survived" are two different
findings. **When one side of a merge deletes most of a file, the green tick belongs to the merge
commit, never to the branch.**

## 2026-08-21 · Una frase se escribe al lanzar el run, y el vacío es lo que hay que revisar (#219, PR #220)

**Un valor que puede estar vacío no solo se pinta en blanco: hace que cosas sin relación se parezcan
entre sí.** La issue nombraba `RunScreen.tsx` como el único sitio que lee `workflowLabel`. Eran cinco,
y el quinto no pinta nada: `WorktreeRuns.tsx:241` deriva qué run continúa a qué comparando `taskKey`,
`workflowLabel` y `startedAt`. Con la etiqueta vacía, dos runs de texto libre sobre la misma tarea
salen iguales en las tres columnas y el segundo queda encadenado como resume del primero — una
relación que no ocurrió, en el mismo bloque cuyo comentario ya decía que prefería un grupo de uno
antes que *«a guessed join»*. El lector peligroso de un campo que se vuelve opcional no es el que lo
muestra, es el que lo **compara**: `render` de vacío se ve mal y se arregla, `===` de dos vacíos se ve
bien y miente. Cuatro celdas fueron un `??`; la quinta fue el hallazgo.

**Y el `tail` con el que se leyó el gate destruyó la prueba sin tocar el exit code.** AGENTS.md dice
que *«a smaller count is a subset, not a success»*, y esa regla da por hecho que la cuenta se puede
ver. `aspire do ci` pipeado a `tail -100` dejó un log que empezaba a mitad de la tanda: cinco
ensamblados, 279 tests, **exit 0** — indistinguible de una subtanda real, que es exactamente el fallo
del 15 de agosto que hizo que este gate sea un comando y no cuatro. La segunda pasada, capturando
entero, contó siete y 532. La regla que faltaba escribir es que **el informe del gate es la prueba, así
que cualquier cosa que lo recorte deja el visto bueno intacto y la evidencia fuera.**

**La colisión 4 de AGENTS.md muerde con un rebase limpio, no con uno conflictivo.** #198 aterrizó
mientras esto esperaba y partió `FolderDetailScreen` en seis ficheros nuevos. El conjunto de colisión
era **vacío** y los tres commits se replicaron sin un conflicto, que es justo cuando no hay nada que
obligue a mirar. Lo que había que preguntar no era textual: si la partición hubiera dejado un sexto
lector de `workflowLabel`, la corrección del seam se habría quedado corta por un fichero y nada lo
habría dicho. Se comprobó con `git grep` sobre `origin/master` —los seis nuevos no lo leen— y el
`range-diff` confirmó los tres commits idénticos. **En una partición ajena, el trabajo es el grep
después del rebase, no la resolución durante.**

**De propina, el propio run demostró la premisa.** El paso despachable del workflow `Ship unattended`
se titula `/aio:ship`, sin `<n>`, así que el agente que escribió este cambio recibió `/aio:ship` sin
número de issue y tuvo que sacarlo del nombre de la rama. Es dato de workflow y no código, pero es la
frase de la issue comprobada en vivo: lo derivado no lleva nada de *esta* tarea, ni siquiera cuál es.

## 2026-08-21 · Los botones de History pedían clases que no existen (sin issue, PR #225)

**Una clase de CSS mal escrita no falla: se ignora, y lo que queda es el estilo por defecto del
navegador, que es plausible.** `HistoryScreen` pedía `pc-button pc-button--sm pc-button--secondary`
y el sistema define `.pc-btn`. Las tres clases eran inexistentes, así que los tres botones de ventana
salían con el chrome del navegador y el seleccionado se veía igual que los otros dos. Nada lo
detectó, y no por falta de puertas: `tsc` no mira un `className`, `eslint` tampoco, y la regla que sí
lo habría cogido —componer `kit.tsx` en lugar de escribir clases a mano, DESIGN.md regla 1— no es
ejecutable. **El fallo silencioso de CSS es el que llega a producción, porque un botón sin estilo
sigue siendo un botón que se pulsa.** El grep que lo cierra es de una línea: `pc-button` aparecía dos
veces en todo el frontend, las dos en la misma expresión.

**Y el validador que existe para esto no puede correr aquí.** `validate-design-system.sh` falla en
`master` desde antes de este cambio: su etapa 2 lee `docs/design-system/tokens`, un directorio que
este repositorio no tiene. Cuatro FAIL idénticos con la rama guardada y sin ella. Es la misma forma
que AGENTS.md ya describe para `pnpm format:check` y `dotnet format` —una puerta roja enseña a leer
por encima de ella— salvo que estas dos están fuera de `ci` a sabiendas y esta no está en ninguna
parte. **Una comprobación que no puede pasar nunca no es una red, es un ruido con nombre de red.**

**El sync se hizo sin issue, y eso deja un agujero concreto.** El cambio se arregló al detectarlo
mientras se investigaba lo que acabó siendo #224, así que no pasó por grill ni por propose. Lo que se
perdió no fue ceremonia: fue el paso de `/aio:implement` que retoma la captura. `docs/design/current/`
queda contando una pantalla que ya no existe, y la nota de que queda así está en la PR y no en una
issue que alguien vaya a leer.

## 2026-08-21 · El panel del seam se va, y el registro de diseño nunca lo había visto (#221, PR #226)

**Cubrir una pantalla no es cubrir un estado.** El paso de `/aio:implement` que retoma capturas dio
por hecho que si un cambio mueve una pantalla, hay una captura de esa pantalla que retomar. Aquí la
había —`project-worktree.png` y su variante de 390— y ninguna contenía el panel: se dibujaba solo con
una task resuelta, y el entorno de captura no resuelve ninguna. Las dos decían `Seam · Unknown` desde
el día en que se tomaron. La captura de 7 filas con la que se argumentó la issue venía de un scope
vivo, no del registro. El registro tiene una foto de cada pantalla y ninguna de la mitad de los
estados en los que esas pantallas entran, y un cambio que sale de uno de esos estados no puede
enseñar un antes.

**La entrada anterior se preocupaba de que `current/` quedase desactualizada; esto es peor y más
silencioso.** Una captura vieja al menos discute con algo. Una que nunca contuvo el elemento hace que
el paso 4 de `/aio:sync` —"tocó frontend y no vino captura"— salte por la razón correcta y con la
lectura equivocada: parece un olvido, y no lo es. La red no distingue "nadie la retomó" de "no existe
ninguna que retomar", y solo la primera es un defecto.

**Un retake movió las nueve capturas de 1440 y ninguna por el cambio.** `git status` después de
capturar es la lista de lo que difiere, no la lista de lo que hay que commitear: el diff era de datos
—otra carpeta escaneada, otras ramas— más dos ficheros que llevaban en oscuro desde un retake que ya
estaba documentado en el propio `capture.sh`. Colar eso dentro de una PR de tres ficheros habría
hecho pasar deriva por cambio. La instrucción dice "commitea solo lo que difiere"; lo que hace falta
es "solo lo que difiere *por esto*".

**Los números de línea de una issue caducan entre grill e implement, y el contenido no.** El seam de
#221 daba `en.ts:614-618` para las claves del panel y `608-613` para las del tile; estaban en 633-637
y 627-632. El reparto —cinco y seis— era exacto. Se verifica por contenido y el número de línea se
usa para encontrarlo, nunca para confirmarlo.

## 2026-08-21 · Un run que acaba porque se puede demostrar, no porque se suponga (#224, PR #227)

**«Huérfano por definición» era una categoría, y la comprobación que la acompañaba no se podía
pasar.** La issue proponía cerrar al arrancar todo run `Running` o `Waiting`, y pedía que el endpoint
devolviese después «el que tiene agente vivo». Las dos frases no caben juntas: la regla solo dispara
al arrancar, así que para verla hay que reiniciar, y ese reinicio cierra también el run que la
comprobación espera encontrar. Una regla sobre un estado no se verifica con una comprobación sobre una
fila. Lo que lo arregló no fue afinar la regla, fue darle a la fila algo propio que mirar — una
columna con el pid, y la suposición pasa a ser una pregunta.

**La señal determinista no faltaba: estaba pagada y se tiraba.** `IPtyConnection.Pid` llevaba en la
mano del launcher todo este tiempo, en la línea que hace `new Session(connection, …)`, y nadie lo
escribía. Antes de diseñar un seam nuevo conviene mirar qué tiene ya el código en la mano y suelta.
Lo que dio el vocabulario fue mirar orca: separa `wait --for exit` (el proceso se fue, determinista)
de `wait --for tui-idle` (heurística, y escribe que «heartbeats and visible terminal activity mean the
worker is alive, not done») de `worker_done` (lo dice el agente, con el preámbulo inyectado por el
runtime y no por el prompt). Con esos tres nombres encima de la mesa, saltaba a la vista cuál nos
faltaba y cuál —el hook `Stop`, que ya escuchamos— no servía aquí.

**El tercer camino lo encontró `ps`, y ninguna suite lo habría encontrado.** El servidor en pie tenía
siete hijos `claude` vivos y cinco muertos sin cosechar, y `kill -0` contesta OK sobre un hijo no
cosechado. Eso es un run con el proceso muerto, el worktree en su sitio y el servidor que lo lanzó
todavía arriba: ninguna de las dos reglas de la issue lo toca. También es la razón por la que la
pregunta al sistema devuelve tres valores y no dos — «no puedo saberlo» no puede colapsar en «se
acabó», porque pasarse de estricto termina un run que sigue escribiendo en el repositorio de alguien.

**«El PR está aprobado» no es comprobable en un repositorio de una sola persona.** GitHub no deja
aprobar tu propio PR, así que los cinco últimos mergeados tienen `reviews=0` y `reviewDecision`
vacío. La aprobación de este bucle es que una persona quite `status:holding`, y la puerta de
`/aio:sync` se lee mejor diciéndolo que dejando «approved» a que cada agente lo interprete — igual
que la propia puerta ya explica por qué «los checks están verdes» no significa nada aquí.

## 2026-08-21 · Una tab de Chat, y las tres cosas que solo dijo el binario (#229, PR #230)

**La issue sabía qué protocolo hablaba el CLI, y aun así se equivocó sobre qué le pregunta.** Daba por
hecho que la app vería «cada tool call» y podría aprobarla; lo que llega es lo que el CLI le habría
preguntado a una persona, que es más estrecho — `Bash(echo …)` se aprobó solo con `--permission-mode
manual`, sin allowlist y con el entorno limpio. Un `--help` verificado en la máquina prueba que el
modo existe, no qué eventos emite. La diferencia entre «el protocolo existe» y «el protocolo dice
esto» solo la paga ejecutarlo, y la copia del producto tuvo que moverse al sitio donde se decide.

**«No hace falta tabla» era verdad, y su mecanismo no.** El razonamiento —el agente ya escribe su
`.jsonl`, el producto ya sabe leerlo— aguantó entero; lo que no aguantó fue el id: no se puede
reutilizar («Session ID … is already in use»), así que «derivado del directorio» tuvo que ser una
cadena de eslabones y no un valor. Abrir continúa el más nuevo hacia el siguiente, y continuar hace
fork, que es lo que arrastra la conversación al fichero nuevo. Una decisión de diseño puede sobrevivir
mientras la implementación que se imaginó para ella no sobrevive ninguna línea.

**Un segundo consumidor de un lector compartido es una prueba del lector.** Las filas de
`<local-command-stdout>` se pintaban como turno de la persona, y no era un fallo nuevo: cualquier run
cuyo agente usara un slash command tenía `<command-name>` como algo dicho por alguien, desde siempre,
porque esas filas no llevan `isMeta`. La tab nueva no lo introdujo, lo hizo visible — y por eso el
arreglo fue en el sitio compartido y arregló las dos superficies. Añadir la segunda pantalla que lee
un parser es más barato como test que cualquier fixture que se le pudiera escribir.

**Una guarda que pasa veinte veces puede estar midiendo otra cosa.** `capture.sh` rechazó una captura
correcta por «luminancia 128» contra un umbral de 140: medía el contenido creyendo medir el tema, y
funcionaba solo porque ninguna pantalla anterior era casi toda panel oscuro (`run-terminal.png` ya
raspaba por 150). La primera que lo era la delató. Ahora mide la barra lateral, donde las veinte
capturas del repositorio caen entre 238 y 240 — un margen así dice que ahora sí mide el tema. Un
umbral que no ha fallado nunca no es prueba de que mire donde crees.

**El seam entregó al módulo un proceso que el módulo no podía matar.** Una conversación seguía viva
después de que `aspire stop` dijera que sí: nada la poseía al terminar. `AgentChatReaper` es la mitad
del ciclo de vida que un `IAsyncEnumerable` no lleva escrita. Cuando un seam nuevo abre algo que dura
más que una petición, la pregunta que hay que hacerse no es cómo se abre, es quién lo cierra cuando
nadie lo pide.

## 2026-08-21 · Medir borró más trabajo del que añadió (#231, PR #232)

**La puerta que mide el fichero equivocado la desactiva la herramienta que la issue proponía usar.** El
techo de tamaño mide el cierre estático del entry y no el fichero de entrada, y esa distinción no es
un detalle de implementación: medido sobre el fichero, sacar React con `manualChunks` —lo que la propia
issue nombraba en el seam— baja el número a unos 56 kB sin que el navegador descargue un byte menos.
Un presupuesto sobre una cifra que se puede repartir entre más ficheros no es un presupuesto. Y se vio
fallar antes de verse pasar: 221,97 kB con `FilesTab` todavía importando react-markdown, nombrando los
dos chunks culpables.

**Instrumentar borró de la lista más trabajo del que añadió.** La issue daba por hecha una cascada de
renders desde `AppShell` y proponía una frontera de `memo` en `AppShell` → `<Outlet/>`. Los 40 renders
del arrastre del rail producen **1** commit por debajo del `Outlet`: el router devuelve el mismo objeto
elemento que tiene en contexto, así que React se sale del subárbol solo. La frontera ya estaba. Medir
antes de optimizar no es prudencia, es lo que impide escribir el `memo` que no compra nada — y el
mismo instrumento dijo qué no puede ver, porque el hover vive en un hermano del `Outlet` y no en un
hijo.

**Los bytes eran un proxy, y el proxy no se nota donde se mide.** 132 kB gzip menos en la primera
carga, y sin CPU estrangulada esta máquina empeora 20 ms de FCP: dos peticiones cuestan un viaje más
donde los bytes desde localhost son gratis. El ahorro aparece a 4x —72 ms de FCP y tres cuartas partes
del tiempo bloqueado—, que es donde vive quien no tiene esta máquina. Una mejora que solo se ve
estrangulando no es sospechosa; lo sospechoso habría sido no comprobar los dos.

**Una medida de rendimiento caduca mientras espera revisión.** Los 174,88 kB gzip que midió la rama
eran ciertos contra `6fc8220` y falsos contra el `master` de dos horas después: la tab de Chat de #229
importa signalR de forma estática y la primera pantalla quedó en 177,03. Nadie tocó el mismo fichero
—cero conflictos, cero solape— así que ningún rebase iba a avisar. La cifra de una issue de
rendimiento hay que volver a leerla en el momento de fusionar, porque el número describe el árbol
fusionado y no la rama, y el margen que lo absorbió es la única razón de que aquí no doliera.

## 2026-08-21 · El Chat elige con qué agente hablar, y deja de ser una terminal (#233, PR #234)

**La razón que la issue daba para mover un dato era falsa, y la regla que lo impedía era la buena.**
`Chat.Query.Response` iba a llevar la lista de agentes «para que el picker no tenga que pedirla
aparte». No se puede: los agentes son la tabla del módulo Runs, Workspace nunca los enumera —toma un
id como string y le pregunta a `IAgentRunner` por ese—, y enumerarlos exigía un método nuevo en el
seam que la propia issue decía que no hacía falta. Y la premisa tampoco se sostenía: la pantalla ya
dibuja su menú de agente con `useAgents()`, así que la lista estaba en la caché antes de abrir la tab.
Cuando una frontera de módulo choca con una optimización, conviene medir la optimización antes de
mover la frontera: aquí no había nada que ahorrar.

**El comentario que ya estaba escrito era el diseño, terminado un paso más allá.** `AgentChatChain`
explicaba que el folder id va en el hash *«porque un directorio puede entregarse a una carpeta
distinta, y dos carpetas compartiendo cadena pondrían una conversación en dos pantallas»*. Dos agentes
en un directorio es la misma frase con otro sustantivo, y el argumento ya estaba en el fichero sin
haber llegado a su conclusión. Lo que se descubrió al aplicarlo es que no era preparación:
`Recorded(folder, path, "copilot")` devolvía el id de la sesión viva de Claude Code, porque comprobaba
el agente para una cosa y lo ignoraba para la otra. Un parámetro que se recibe y se usa a medias es un
defecto esperando a que exista el segundo caso.

**Compartir un componente es una apuesta a que dos superficies seguirán siendo la misma superficie.**
`TranscriptRow` estaba exportado para esta tab y su comentario decía por qué: un solo mapeo en el
servidor produce las dos listas, así que dos renderizadores serían dos sitios decidiendo cómo se ve
una llamada plegada. El argumento era correcto y dejó de serlo el día que una de las dos salió del
negro. La reutilización no se rompió por un cambio de requisitos: se rompió porque el motivo escrito
—«los datos son los mismos»— nunca fue el que sostenía el componente, que era «el sitio es el mismo».

**Los dos defectos que quedaban sólo se veían corriendo la app, y ninguno estaba en el diff.** El menú
del kit siempre caía hacia abajo, lo cual es correcto para todos sus llamadores anteriores y deja dos
de tres filas fuera de la ventana cuando el trigger está en el composer. Y una burbuja no conserva los
saltos de línea, porque el CSS de arriba está escrito para hijos que ya son bloques: el mensaje de
cuatro líneas —lo que esta tab existe para hacer posible— llegaba al servidor como uno y se dibujaba
en una sola línea. Vendorizar CSS es heredar los supuestos de quien lo escribió, y los supuestos no
viajan en el fichero.

**Retomar la captura reescribe las diez pantallas y sólo una es tuya.** `capture.sh current` no tiene
media: las nueve restantes salieron modificadas por el estado de este worktree —otra lista de
carpetas, otro recuento de sin-commitear—, que es dato y no deriva de diseño. Se revirtieron. Una
herramienta que sólo funciona entera obliga a decidir, cada vez, qué parte de su salida es la
respuesta a la pregunta que se hizo.

## 2026-08-22 · Los filtros de Tasks y la puerta de merge se pliegan en móvil (sin issue, PR #237)

**#217 arregló lo que se rompía a 390 y dejó lo que lo ocupa, y son dos medidas distintas.** Apilar
las cinco celdas de la puerta para que sus valores se lean, y plegarla para que la pantalla se pueda
navegar, no son el mismo arreglo: el primero se mide en la tarjeta y el segundo sólo se ve en la tira
que tiene debajo. El número que lo dice es la `y` de la primera pestaña — 600 con las razones
abiertas, 230 plegada—, y no aparece en ninguna medición hecha sobre la tarjeta. La propia issue
anotó la frase «aquí no puedo navegar» y la respondió para la tira sola. Un arreglo de layout medido
sobre lo que se arregla puede no ver lo que desplaza.

**Los ternarios de la rama ancha murieron en cuanto la estrecha dejó de ser una variante suya.**
`MergeGate` duplicó a propósito sus celdas apiladas y las anchas, y escribió por qué: escribir la
diferencia en una docena de propiedades condicionales movió el detalle de Seam a 1440. Ese argumento
valía para las celdas y caducó para la cabecera — con la estrecha convertida en un desplegable,
`stacked ? "column" : "row"` es una rama a la que no se puede llegar, y dejarla puesta afirma que el
código atiende una anchura donde nunca se ejecuta. Código muerto que miente sobre lo que cubre es
peor que un literal.

**Duplicar sirve cuando lo que cambia es el contenido, no cuando lo único que cambia es la caja.** El
primer intento de la barra de filtros copió el markup ancho dentro de la rama estrecha, siguiendo el
precedente de `MergeGate` al pie de la letra. Pero allí cada celda se dibuja distinta en cada rama, y
aquí los chips, sus contadores y sus manejadores son idénticos: lo único distinto es si envuelven o
hacen scroll. Dos copias de un chip son dos contadores que tienen que seguir de acuerdo. Los
elementos se definen una vez y las dos cabeceras los componen; el precedente se leyó mal por citarlo
sin comprobar de qué hablaba.

**Chrome headless miente a 390 en macOS, y el registro ya lo decía de las mediciones — también vale
para mirar la foto.** Las cuatro primeras capturas, tomadas con `--window-size=390,844`, mostraban la
cabecera cortada por el borde derecho y el botón del header a medias; empecé a razonar sobre un
desbordamiento que no existe. `docs/design/README.md` ya tiene escrito que ese flag maqueta a 500×757
y escala, y por eso el conjunto estrecho se toma con Playwright. Lo que no estaba escrito es que el
aviso no es sólo sobre los píxeles que se miden, sino sobre los que se miran.

**Una constante de breakpoint también es un instrumento.** Para poner un número real a lo que costaba
la barra vieja a 390 hubo que hacer que la rama pudiera dibujarla: `filtersAbove = 0` durante una
recarga, 335 px medidos, y vuelta a 560. La alternativa —volver a `master` con un stash— competía por
el mismo árbol de trabajo que la puerta que estaba corriendo en ese momento. El «antes» de una medida
de layout no siempre está en otra rama; a veces está a un literal de distancia.

## 2026-08-22 · La tab Issue del worktree lee la pull request (#235, PR #236)

**La premisa medida de la issue estaba invertida, y medirla antes de diseñar cambió el diseño.** El
cuerpo de #235 apoyaba «y sus comentarios» en el argumento de `task-details` — 351.931 caracteres de
body contra 514.934 de comentarios en las issues — dando por hecho que la pull request se parecería.
`gh pr list --state all --limit 300` sobre las 101 del repositorio dice lo contrario: 183.344 de body
contra 3.524 de comentarios en 5 de ellas, y cero reviews. 52 a 1 al otro lado. El loop lo explica —
los `/aio:*` comentan en la issue, la PR nace draft, la review es una persona leyendo el diff — y la
consecuencia no fue recortar el alcance sino reordenarlo: el body se dibuja entero sin un «ver más»
delante, y el hilo vacío es una frase porque es la respuesta ordinaria 96 veces de 101. Una analogía
medida en un sitio no es una medida en el otro.

**El discriminador estructural existía y el que estaba puesto era una cadena.** El seam tenía que
separar «no hay pull request» de «no se pudo preguntar», y `gh pr view` gasta exit 1 en las dos, así
que el camino evidente era leer su stderr. `gh pr list --head` sale con **0 y `[]`** cuando no hay
pull request: la ausencia deja de ser un fallo que clasificar y pasa a ser un dato, y todo exit
distinto de 0 es «no se pudo preguntar» por construcción. El login además tiene su propio exit 4. Se
comprobó contra el `gh` de la máquina antes de escribir el diseño, no después. Antes de parsear el
mensaje de una herramienta conviene buscar el subcomando que responde con una estructura.

**La dirección de un default es una decisión de diseño, no un detalle.** Lo que no se reconoce cae en
`Failed` con las palabras del vendor, nunca en «no hay». De las dos, sólo una es un hecho que este
proceso puede afirmar: un `gh` desconectado dibujado como «esta rama no tiene pull request» es la
pantalla inventando. El comentario que el seam tenía escrito para justificar el colapso —«the
screen's response to all of them is the same disabled button»— era correcto mientras el consumidor
era un botón, y caducó con él. Un razonamiento anotado dice cuándo deja de valer si dice de qué
depende.

**Renombrar un scenario le parece a OpenSpec perderlo, y tiene razón.** `openspec archive` abortó dos
veces: un `MODIFIED` cuyo scenario cambió de nombre cuenta como un scenario caído, y el requisito
cuyos dos títulos dejaron de ser verdad no era un `MODIFIED` en absoluto. La forma correcta era
`RENAMED` para los títulos y `REMOVED` + `ADDED` para el requisito cuya premisa murió — un 404 sobre
un run que este endpoint ya no toma. El guardarraíl obligó a decir cuál de las tres cosas estaba
pasando en cada caso, que es exactamente lo que un delta escrito a mano se salta.

**El aviso sobre Chrome headless a 390 estaba escrito dos veces y abrí una issue antes de leerlo.**
Tomé capturas con `--window-size=390,844`, vi la cabecera y el tab strip cortados, y abrí #238 con
tres síntomas medidos. `docs/design/README.md:24-31` dice que ese flag maqueta a **500×757** y escala
el resultado a un PNG de 390, así que toda medida de esa foto es de un layout de 500. La entrada de
#237, dos secciones más arriba en este mismo fichero, ya lo había pagado y añadido que el aviso «no
es sólo sobre los píxeles que se miden, sino sobre los que se miran». #238 quedó cerrada. El registro
sirvió; lo que falló fue leerlo antes y no después, y el coste fue una issue de más y un diagnóstico
equivocado que además metió `minWidth: 0` en un sitio donde no arreglaba nada.

**El tema oscuro no se comprobó, y decirlo es parte del trabajo.**
`--blink-settings=preferredColorScheme=1` renderiza byte-idéntico al claro en este entorno — MD5
iguales — y `capture.sh` sólo tira en claro y rechaza un tiro oscuro. Lo verificable sin el
instrumento es el contrato de `design-system-compliance`: diez tokens `var(--…)`, ningún hex, ningún
literal de radio ni de tipografía, y ninguna lectura del tema. El conjunto estrecho y oscuro se toma
con Playwright por `AppFixture.Phone`, y esta vez no se retomó. Una casilla que no se cumplió se
marca como no cumplida; ticarla habría hecho el resto de la lista igual de poco fiable.

## 2026-09-03 · El repositorio entra en la identidad de una tarea espejada (#268, PR #269)

**La decisión ya estaba escrita en tres sitios, y el código que la necesitaba no podía ejecutarse.**
`VendorRepository` dice en su propio resumen que existe porque «dos sitios necesitan ese repositorio
y tienen que estar de acuerdo». `GetTask` estaba escrito para varias filas por clave —«cada fila con
esta clave, no la primera»— y aceptaba `?repository=`. Y el resumen de `RepositoryScopeTests`
nombraba el agujero que quedaba: «las dos filas pueden coexistir solo porque `Source` difiere». Pero
el índice único era `{Source, Key}`, así que dos issues con el mismo número no podían coexistir
nunca: la rama de `GetTask` que elige entre candidatos era inalcanzable, y una lectura escrita para
una forma que la escritura prohíbe se lee como código que funciona. Leer los tests antes que el
código encontró la propuesta entera; el diseño estaba decidido a medias y lo que faltaba era la
mitad que hace existir el estado.

**«Latente» era una afirmación sobre una base de datos poblada, y nadie lo dijo.** El segundo modo de
fallo —`existing` cargado una vez, `Add` sin ampliarlo, dos inserts y una violación del índice en el
único `SaveChangesAsync`— se archivó como «hoy no está pasando; hace falta que los dos repositorios
estrenen número a la vez». Sobre un espejo vacío *toda* tarea estrena: la primera sincronización de
este worktree contra GitHub real habría escrito cero filas, y no solo de GitHub, de los cuatro
proveedores. La condición que hace latente un fallo merece decir respecto a qué estado lo es.

**Determinista no era lo mismo que correcto.** El desempate de `GetTask` estaba ordenado para que la
respuesta no se moviera, y con las dos filas existiendo por fin respondía el `#266` **cerrado** del
otro repositorio, porque `Plain-DS3D` ordena antes que `asantamariaplainconcepts`. Arreglar el fallo
es lo que hizo visible el segundo: mientras las dos filas no podían coexistir, la cerrada del vecino
simplemente *era* la fila, y el desempate no tenía nada entre lo que elegir. Un criterio que se
eligió por estabilidad conviene revisarlo el día que empieza a elegir de verdad.

**Romper el arreglo a propósito es lo que dice si el test lo fija.** Con `RepositoryOf` devolviendo
vacío —la identidad de antes— fallaron 5 de los 11 tests de la clase y los 6 que ya existían
siguieron pasando, que es la señal de que el cambio no reinterpreta lo que aquellos fijaban. Y el
test del segundo modo falla como un **500 en `/api/tasks/sync`**, no como una fila duplicada: si se
hubiera escrito esperando el duplicado habría pasado en verde con el fallo intacto. La forma en que
un test falla es parte de lo que pin­cha.

## 2026-09-04 · La consola gana una ventana propia, y la evaluación se come su propia pregunta (#266, PR #270)

**Medir el primer candidato disolvió una de las dos preguntas en vez de contestarla.** La evaluación
se montó sobre dos capacidades que solo una carcasa daría: el atajo global y quién arranca el
servidor. La segunda se cayó al medirla, y por un detalle del banco: corría con `--no-window`, así
que lo que sobrevivió a la salida del padre —reparentado a `ppid` 1, respondiendo 200— fue el hijo de
**un proceso de consola corriente**. La propiedad era de `Process.Start` sin esperar, la forma que
`DesktopIntegration` ya usaba. La carcasa no era lo que lo hacía funcionar, así que tampoco era lo
que hacía falta para tenerlo. Una comparación puede disolverse en vez de ganarse, y conviene notarlo
antes de puntuar al segundo candidato contra una pregunta que ya no existe.

**Compilar, pasar la puerta y arrancar son tres estados distintos, y solo el tercero es la app.** La
carcasa entró en el slnx con 608 tests en verde y se cayó al abrirla:
`NSInternalInconsistencyException: API misuse: setting the main menu on a non-main thread`. Un solo
`await` en las sentencias de nivel superior vuelve `async` el punto de entrada y la continuación
resume en un hilo del pool; macOS exige el hilo principal. La puerta no abre ventanas y no puede ver
esto. Es el mismo agujero que #61 nombró para el servidor —259 tests verdes contra una app que nadie
podía abrir— reaparecido en la única superficie nueva que se ha añadido desde entonces.

**El mismo `SetSize(1400, 900)` dio 2560×960 y 178×136 según cómo se lanzara.** Con `dotnet run`, ancho
de pantalla; desde el `.app`, una ventana inservible. Dos respuestas incorrectas y distintas de una
llamada. Medir una API de plataforma **una vez** mide también la forma de arrancarla, y la diferencia
entre las dos formas no era una variable que nadie hubiera declarado. Lo que quedó no es el tamaño
pedido —sigue sin respetarse— sino `SetMinSize`, que no impide que salga grande pero sí que salga
inútil: cuando una API no cumple, el suelo vale más que la petición.

**Cobrarle a una dependencia un coste que el repositorio ya paga es un error simétrico.** Escribí que
Electron costaba «un runtime de Node», y no: `README.md:23` ya exige Node 22 con pnpm y la puerta
corre cuatro pasos de pnpm. Es el mismo error que cobrarle a Photino el SDK de .NET. El coste real
estaba en el otro extremo del ciclo —el Chromium que empaqueta, y una major cada 8 semanas
manteniendo solo las tres últimas— y no se parece en nada al que había escrito. Un coste mal nombrado
sobrevive en el ADR mucho después de que nadie recuerde de dónde salió.

**Aceptar un ADR es un acto del dueño, y adelantarlo encarece rebatirlo.** Puse ADR-0004 en `Accepted`
al concluir la medición; concluir la evaluación y tomar la decisión no eran lo mismo. Aquí importa
más que en otros sitios porque `issue-lifecycle` exige que un ADR aceptado se **supersede** en vez de
reescribirse: en `Proposed` cuesta un comentario discutirlo, en `Accepted` cuesta un ADR entero. La
recomendación es del que mide; el veredicto llega cuando una persona mergea.

**Y el issue no cabía en el molde del bucle, cosa que la Definition of Ready no comprueba.**
`/aio:refine` lo marcó listo con los cinco campos respondidos, y `/aio:propose` no pudo expresarlo:
`openspec validate --strict` exige al menos un delta y este cambio no cambiaba comportamiento — su
entregable era una decisión. Se resolvió sin andamiaje OpenSpec, con la rama llevando solo el ADR.
La DoR pregunta si el issue se puede contestar, no si el bucle puede transportarlo, y son dos
preguntas distintas.

## 2026-09-04 · Las siete llamadas que sobrevivieron al arreglo, y un checkout dos commits viejo (#273, PR #274)

**La evidencia en vivo tenía dos commits de retraso, y no lo dijo.** El diagnóstico salió de
interrogar la consola corriendo y de leer los ficheros del checkout principal: 20 de 22 issues
abiertos perdidos, `#272` respondiendo con la issue del otro repositorio. Todo cierto, y todo de
código que ya no existía — `decb2e3` había metido el repositorio en la identidad la noche anterior,
el checkout estaba `behind 2` y el binario que respondía era del día 1. Se escribió un issue entero y
se arrancó un `/aio:ship` sobre él antes de notarlo. Dos comprobaciones lo habrían visto antes de la
primera frase: `git log HEAD..origin/master`, y la fecha del binario contra la del commit sospechoso.
Una app local no se versiona a sí misma en pantalla —eso es el #272— así que **preguntarle a la app
es preguntarle a un build, y el build no dice cuál es**.

**Un arreglo que llega a seis sitios de trece deja el residuo justo en la mitad que escribe.** #268
arregló la identidad, el sync y `GetTask`; las siete llamadas por-tarea siguieron resolviendo la fila
por el número, y cuatro de las siete escriben —labels, asignados, el estado del loop y el hold. Y la
garantía estaba **escrita** en el sitio del fallo: `SetTaskLabels` pasaba `row.Url` con el comentario
«la URL de la fila, así que esto no puede escribir en la issue de otro repositorio con el mismo
número». Cierto de esa línea, falso de la función: la fila la había elegido un `FirstOrDefaultAsync`
sobre la clave dos líneas antes. **Un comentario puede ser verdad sobre su línea y mentira sobre su
alcance**, y esa es la clase de mentira que sobrevive a una revisión, porque leerla confirma lo que
dice.

**Los tests de esas siete llamadas no podían fallar antes de que existiera el fallo.** `(Source, Key)`
no dejaba coexistir dos *issues* con el mismo número, así que un test que espejaba un repositorio no
probaba nada sobre una búsqueda por clave: había una sola fila que encontrar y siempre era la
correcta. Verde durante meses sobre una ambigüedad que el esquema no permitía expresar. El cambio que
introdujo el riesgo es también el que hizo escribibles los tests — y el orden importó: primero la
identidad, después los llamantes, y **entre los dos hubo un día en el que las dos filas existían y
nadie las distinguía al escribir**.

**El gate cuenta 624 tests y ninguno es del navegador, donde vive la mitad de este cambio.**
`src/frontend/package.json` declara `dev`, `build`, `preview`, `lint`, `typecheck` y `format:check`:
no hay runner, y `aspire do ci` no ejecuta ninguno. Las claves de caché, los prefijos de
invalidación y `HeldTask` descansan en `tsc` y en la comprobación en vivo. Se dejó **nombrado en el
tasks.md en vez de marcado**, porque un checkbox verde sobre algo que nadie ejecutó vale menos que
una línea que dice qué no se probó.

**Y la comprobación en vivo de una escritura se diseña por lo que pasa si falla.** Probar que la
label aterriza donde se dice pedía escribir en GitHub real; si la resolución estuviera mal, aterriza
en el repositorio del vecino. Se hizo sobre la issue propia —la #273 de este repositorio— leyendo las
labels de la #273 del vecino antes y después, y quitando la label acto seguido. **La prueba de que
una escritura fue al sitio correcto es leer el sitio equivocado.**

## 2026-09-05 · La rama resuelve la tarea, y el espejo entrega media issue (#277, PR #282)

**La pantalla estaba ciega justo donde está el trabajo, y llevaba así desde que el loop dejó de
lanzarse desde la app.** `taskFor` sacaba la tarea del run más reciente del worktree, que era la
respuesta buena cuando un worktree nacía del botón «Run a task here». `/aio:propose` lo crea desde
una terminal y no registra ningún run: los tres worktrees vivos de esta máquina tienen **cero**, y 26
`change/*` históricos sí los tienen. Así que la tab Issue decía «No run has worked here yet» y la
tarjeta Seam leía *Unknown* sobre worktrees cuya rama lleva el número del issue en el nombre. Nadie
lo había notado porque la evidencia de que funcionaba estaba en los worktrees viejos.

**Derivar de la rama parecía obvio y los datos dijeron que no del todo.** 3 de esos 26 pares
discrepan — `change/174` corrió `#104` dos veces, `change/6` corrió `#104`, `change/182` corrió
`#174`. Un worktree se reutiliza. La regla que sobrevive a eso es *el run gana siempre y la rama sólo
propone*, y sólo se sabe consultando la base de datos antes de escribir la función, no después.

**La confirmación ya estaba construida.** Los dos consumidores llamaban a
`useTask(key, repository)`, que resuelve la fila por repositorio y clave y devuelve 404 para una que
no tiene. Eso convierte un candidato en respuesta sin codificar `change/<n>` en ninguna parte, que
era el otro riesgo: un producto que usan varios loops no puede imponer el nombrado de uno. **La
comprobación que hacía falta ya existía; lo que faltaba era usarla como comprobación.**

**Y correrlo destapó un fallo más grande que el arreglo.** `SourcedText.Trim` corta el cuerpo de una
issue a **4.000 caracteres**, con una razón escrita que era cierta cuando el cuerpo sólo se enseñaba
como un párrafo. Desde #149 y #185 el cuerpo se *parsea*: `parseSeam` busca `## Seam`, `parseCheck`
busca `## El check`, y en un issue largo las dos secciones caen pasado el corte. **32 de los 60
issues más recientes de este repositorio pasan de 4.000**; #275 son 13.121 y el espejo guarda 4.001.
La columna admite 20.000. Lleva meses en verde diciendo «este issue no declara seam» sobre issues que
sí lo declaran. **Una constante deja de ser correcta el día que alguien le da un segundo lector, y
nada avisa** — el doc que la justifica seguía describiendo con precisión el uso que ya no era el
único.

**Lo que este cambio no pudo comprobar, y por qué es un hallazgo y no una excusa.** El ancho estrecho
de la tab Issue no lo puede medir la red de seguridad: un 390 de Chrome headless aquí es un artefacto
—maqueta a 500 y escala—, y `AppFixture.Phone` no puede abrir esa tab porque es de worktree y
`WorkspaceSeed` no crea worktrees, *«a seeder cannot ask git»*. La superficie que este cambio toca es
exactamente la que la red no alcanza. Con esto dentro sí se alcanza, porque una rama ya basta.

**Corrección a una nota propia:** el tema oscuro **sí** se comprueba con Chrome headless. La nota de
este repositorio decía que no, sobre la base de que `preferredColorScheme=1` sale idéntico a claro.
Es cierto y la conclusión no: `1` no es el extremo oscuro de ese enum, `0` sí.

## 2026-09-06 · El épico de la tab Change: el ancho es un requisito, y no se comprueba al final (#288, PR #298)

**Diez cambios seguidos sobre una rama viven mucho mejor de lo que predice la regla del conflicto.** #283, #289–#297
aterrizaron como diez cambios OpenSpec sobre `epic/288`, cada uno con su propuesta, sus tasks y su
cierre, y ninguno esperó un rebase: las colisiones que la issue del épico predeclaró (#289 borra tres
tabs que #264 estaba tocando; #290, #294 y #295 comparten el esquema de Workspace) se resolvieron
*por orden de aterrizaje dentro de la propia rama*, no por rebase. La regla que sobrevive es que el
grafo de dependencias de un épico se cumple igual en una rama que entre issues —y que una rama épica
es diez decisiones de orden tomadas de una vez, delante.

**El requisito-fichero y el requisito-test son una afirmación del agente, y el formato `req:`/`files:`
por tarea es lo que la hace auditable.** La anotación no es un hecho derivado del diff — no se puede
derivar — y por eso vive en `tasks.md` y la pantalla la lee tal cual. El hallazgo de distribución:
anotarla costó añadir dos líneas al prompt de `/aio:implement`, y leerla ya existía — la tab sólo
reordena lo que el tasks.md ya llevaba desde que OpenSpec lo impone. **Un estándar que ya se escribe
es gratis de convertir en pantalla.**

**La red de 390 enseñó que los hallazgos del ancho llegan tarde si el set estrecho no existe.** El
retake del set encontró cuatro defectos reales que ningún review a 1280 vio: el paso dibujando 2px de
los 154 que pide, el botón de #296 comprimiendo la columna de hechos de 180px a 37px, un `h1` cortado
en todo scope por worktree y un badge contradiciendo su propia frase al lado. **Ninguno arreglado en
el épico** — el set los midió con números y los dejó reportados, porque un décimo cambio que
arreglara cuatro hallazgos ajenos a su seam no habría tenido proposal honesta. La red de seguridad
mide; no rescata.

**Y el gate contó en todas partes, con su exposición a mano.** Las tandas del épico pasaron `aspire
do ci` con 657–727 tests y 7 assemblies según el punto de la rama, y los dos fallos que aparecieron
son los suyos-no-suyos documentados: `PhoneTests` falla en worktrees cuyo *nombre de directorio* no
cabe en el `h1` a 390 — `agent-a43c…` necesita 199px y tiene 172px — y el finding 3 del retake dice
que eso no es un artefacto del nombre largo sino un defecto real del heading que HEREDA CADA
WORKTREE. **Un test que falla por el nombre de tu worktree y pasa en `master` es un test que miente
verde en todas partes y a la vez** — el defecto es del producto, la aserción solamente elige qué
nombre lo dispara.

**Lo que el épico no pudo comprobar donde más importaba.** `aspire start` se desacopla y no queda
sesión que leer: #295 resolvió el paso «The app» declarando el comando y su estado, y la afirmación
«Up on …» sigue sin poder sostenerse contra el proceso vivo sin el hallazgo de #286, que este épico
releerá. La verificación en vivo del toggle corrió contra la app real en el primer commit del paso 5
y contra el seed de #297 en el último; entre los dos queda un trecho que sólo se cierra el día que
`aspire ps` sea una respuesta del seam.

## 2026-09-07 · #272 — El producto no tiene versión, y el bundle de macOS se inventó una (#272, PR #299)

**Una propiedad de MSBuild heredada es mejor que un literal copiado a tres sitios.** `Directory.Build.props`
ya vivía en la raíz de `src/` y ya era heredado por cada proyecto — `TargetFramework`, `Nullable`,
`TreatWarningsAsErrors` — y `<Version>` es lo mismo: una línea al final del `PropertyGroup` y cada
ensamblado la lleva en `AssemblyInformationalVersion` sin tocar un `.csproj`. El bundle la lee con
`dotnet msbuild -getProperty:Version` antes de publicar, y `GET /api/version` la lee del atributo del
ensamblado en runtime. Tres consumidores, cero duplicaciones, y el literal `0.1` que #266 puso por
no tener de dónde derivarlo ya no existe.

**Lo que enseñó este cambio: el endpoint de versión es un concern del host, no de un módulo.** Mapearlo
en `Program.cs` junto a `MapDefaultEndpoints` — y no como un caso de uso en un módulo — mantiene la
versión fuera del descubrimiento de módulos. El Server describe el producto; la versión es un hecho
del build, no una preferencia que una persona cambie, y ponerlo en un módulo sugeriría que pertenece
al vocabulario de ese módulo. El health endpoint no declara permiso; el de versión tampoco.

## 2026-09-07 · #300 — Cinco labels en la issue, no cinco filas en SQLite (PR #301)

**#290 tuvo razón y #300 la contradice, y ambas son decisiones, no bugs.** El agregado `ReviewStep`
era honesto: una marca que sobrevive a un `git commit` es una afirmación falsa con la forma de una
verdadera, peor que no tener marcas. Lo que #300 acepta es ese coste a cambio de que el estado viva
en la issue — en cualquier navegador, para cualquiera, en cualquier máquina — y no sólo en este
producto. El trade-off se dice en la issue, se dice en la propuesta, y se dice en el spec: un label
sobrevive un rebase y la pantalla lo dibuja igual. No es un defecto; es una decisión registrada.

**La escritura ya existía.** `SetTaskLabels` escribe vendor-first y el espejo sigue por el poll. El
cambio lo usa por segunda vez — la primera es el inbox — sin tocar un seam, sin un endpoint nuevo,
sin un permiso nuevo. `workspace:review` se va, porque la escritura de labels vive en el módulo Tasks
con `Access.AnyCaller`, y un permiso que nadie más usa es un permiso que se borra.

**`openspec archive` no deja caer escenarios en silencio.** Falló tres veces antes de archivar:
un escenario de la spec vieja que no estaba en el bloque MODIFIED, luego dos más. La herramienta no
adivina si un escenario se cayó a propósito o por despiste — pregunta, y si la respuesta es «a
propósito», el bloque MODIFIED tiene que incluirlo con su nuevo wording, o hay que REMOVER el
requirement entero. Un escenario sin requirement padre no se puede REMOVEr. Menos sorpresas que un
diff que pierde escenarios sin decirlo.

## 2026-09-07 · #302 — Un worktree mergeado ofrece cerrarse, no mergearse otra vez (PR #303)

**El camino `alreadyMerged` ya existía en el servidor (#296) y faltaba el botón que lo invoca.**
`MergeChange.cs` lee el estado del PR y salta el merge cuando es `MERGED` — resumabilidad sin
contador almacenado. Lo que faltaba era la mitad del UI: `ChangeTab` no leía el estado del PR
que `VerdictRow` debajo suyo ya tenía. Un cache hit de React Query, no una segunda petición.

**El hallazgo fue que el botón y el diálogo viven copys distintas para el mismo endpoint.** El
servidor no cambia: `POST /api/folders/{id}/merge` ya hace las dos cosas. El botón dice "Close
worktree" con `FolderMinus` cuando el PR es `MERGED`, y "Merge change" con `GitMerge` cuando no.
El diálogo adapta título, cuerpo y botón de confirmación. Sin nuevo seam, sin nueva ruta, sin
nuevo contrato — el cambio vive en tres archivos del frontend.

## 2026-09-07 · #304 — El cambio entre master y change es lento (PR #305)

**`useTransition` era lo que faltaba, no más endpoints.** El problema no era que el backend tardara
— era que el frontend bloqueaba el primer render de ChangeTab en llamadas que ya tenía respuestas
en caché, y luego reemplazaba cada step panel con un spinner. `useTransition` en `setTab` deja la
tab anterior visible mientras la nueva monta; los skeletons (`ProposalSkeleton`, `CodeSkeleton`,
`TestsSkeleton`) danstruyen la estructura del panel, no un círculo girando. El header ya pintaba
con caché — no tocó nada.

**El hallazgo fue que tres panels tenían `if (isPending) return <Loading />` y el cuarto no.**
`EvidencePanel` ya manejaba el pending por fila, no por panel. La asimetría no era un diseño — era
que cada step se escribió en su propio ticket y ninguno heredó la regla del otro. Unificar a tres
en skeletons fue un cambio de tres imports y tres funciones. Sin nuevo contrato, sin nuevo endpoint,
sin nuevo seam — el cambio vive en cuatro archivos del frontend.

## 2026-09-07 · #306 — Code step: sidebar y vista raw (PR #307)

**Adoptar un patrón existente es más barato que inventar uno.** CodeStep listaba los ficheros del
diff en columna vertical con el patch debajo de cada uno — 25 ficheros era un scroll sin overview.
DiffTab ya tenía el sidebar lateral con breakpoint `useNarrowContainer(660)`. Este cambio copió la
estructura (no el componente — tienen metadatos distintos) en lugar de inventar una. Tres archivos
cambiaron: CodeStep adoptó el layout, PatchView ganó RawView, y en.ts ganó tres claves.

**La vista raw no necesitó un endpoint nuevo.** La tentación era añadir `GET /api/folders/{id}/file-with-changes`
que devolviera el fichero entero con estado por línea. Pero `useFileContents` ya existe (la tab Files
lo usa) y `usePatch` ya devuelve los `newLine`/`oldLine` que dicen qué línea es añadida, eliminada o
contexto. La raw view cruza los dos en el cliente: un `Map<number, DiffLineKind>` del patch, y cada
línea del fichero lee su estado. Sin nuevo seam, sin nueva migración, sin nuevo contrato.

## 2026-09-08 · #309 — A worktree shows the commits ahead of the trunk (PR #310)

**El separador de campos de `git log --format` fue el hallazgo del cambio.** `%x1f` (unit
separator) parecía el elegido — hasta que un subject que lo contiene partió un commit en dos,
porque cualquiera puede escribir un `\x1f` en un mensaje y git no lo sanea. NUL sí: git elimina
los bytes NUL de los commit messages al almacenarlos, así que es el único byte que no puede
aparecer en un field. La lección: el separador de un parser de git se elige por lo que git
*garantiza* que no está en el dato, no por lo que *normalmente* no está.

**Uno más allá del límite pregunta, un truncado adivina.** Pedir `-{MaxHistoryCommits + 1}`
convierte el boundary en una cuenta: exactamente 101 respuestas significa truncado, 100 o menos
significa completo. Un parser que pidiera solo 100 no podría distinguir "branch agotada" de
"cap alcanzado", y dibujaría una lista que termina en silencio sobre trabajo que existe.

**`history: null` sobre el hilo, otra vez.** La discusión null-vs-vacío ya se había tenido para
`ahead`/`behind` (#146) y `lastCheck.since`. Este cambio la tercera ocurrencia: sin trunk no es
"0 commits ahead", es "nada fue comparado". La spec nueva (`read-the-commits-ahead`) lo codifica
como requirement propio para que la cuarta pantalla que lo olvide falle su validación.

## 2026-09-08 · #311 — El header pierde el chrome global (PR #312)

**Mover, no copiar, fue la decisión que más archivos ahorró.** `WorkflowsScreen.tsx` era 869
líneas y la tab era 73. La tentación era copiar la pantalla en la tab y dejar la ruta como
redirect. Pero mover el código entero — mismo cuerpo, mismo estado, mismos diálogos — en un solo
edición fue un cambio de dos archivos, no de tres. El redirect es una línea en `routes.tsx`, y la
tab pasó de catálogo a editor sin un archivo nuevo.

**El backdrop a `z-index: 30` no es可见mente un botón cierre.** Playwright informó que el `span`
del heading interceptaba el click del botón "Close projects" — el backdrop a `inset: 0` tenía su
centro detrás del sidebar overlay a `z-index: 31`, y cualquier elemento del sidebar en esa posición
interceptaba. La lección: un botón de backdrop que cubre toda la pantalla no se cierra desde su
centro si hay algo encima; se cierra desde una posición que el overlay no cubre. El test usa
`Position` explícito en el lado derecho.

**`/workflows` redirige, no 404.** La ruta era un link publicado en `WorkflowsTab.tsx`. Un
bookmark no hace nada malo; merece la landing page, no un 404. Un redirect es una línea en el
router.

## 2026-09-17 · #314 — La telemetría que `collect-usage` daba por escrita no existía (PR #315)

**La primera cifra real del log, y hay que leerla con su asterisco: ~$35,27 estimados, 87.823
tokens de entrada y salida, 51 minutos de sesión.** Cubre solo la sesión que arregló y cerró el
cambio. Las fases `propose` e `implement` corrieron en sesiones de Claude remote, y sus registros
no llegaron: `.telemetry/executions.jsonl` está en `.gitignore`, así que muere con la máquina que
lo escribió. El mecanismo cumple lo que promete y aun así el 88.º retro sigue midiendo menos de lo
que costó — no por falta de instrumento, sino porque el instrumento escribe en un sitio que la rama
no se lleva. Dónde vive ese fichero es la pregunta que este cambio deja abierta.

**El bug salió de ejecutar el script, no de leerlo, y la gate no lo habría visto jamás.** Cada push
suma la transcripción desde su primer mensaje, así que dos pushes de una misma sesión se solapan:
el segundo contiene al primero. `collect-usage` decía sumar todas las líneas. Y `implement.md`
empuja dos veces por sí solo — paso 7 al pasar la gate, paso 9 al marcar la PR lista — de modo que
toda medición suya habría salido al doble: $46,3504 contra $23,1752 medidos sobre dos pushes
reales. `aspire do ci` no toca `.ai/`, ni un test cubre estos scripts; 727 tests en verde eran
compatibles con la cifra equivocada. Para el código que mide, el único test es medir algo conocido.

**El arreglo tenía que tocar el spec, no solo el script.** El requirement decía literalmente
*"summing that change's lines"*. Corregir el `.mjs` y dejar esa frase en pie habría dejado la regla
mala como la autoritativa, y el siguiente que implementara contra la capability habría reintroducido
el error creyendo que seguía la spec. Un bug que vive en una frase del spec no se arregla en el
código.

**La tarea bloqueada fue el mecanismo funcionando, no fallando.** La sesión remota no tenía `dotnet`
ni `aspire`, y en vez de saltarse la gate en silencio dejó la 4.1 marcada `BLOCKED` y empujó un
registro `--status failed`. Esa nota es exactamente lo que este cambio existe para producir, y es
lo que hizo que la gate se corriera de verdad dos días después en una máquina con toolchain. Un
agente que no puede verificar algo tiene una salida mejor que adivinar: decir qué le faltó, con el
`which` que lo confirma.

## 2026-09-20 · El suelo P0 del control plane, cinco changes en una PR (PR #322)

**Comprobar el solapamiento de ficheros antes de rebasar deja el rebase sin sorpresas.** La rama
llevaba trece commits sobre una base con un commit por delante en `master` — el archivo de
`controlled-workflow-execution` (PR #323). Antes de rebasar, `comm -12` entre los ficheros que
cada lado había tocado desde el ancestro común dio un conjunto vacío: nada compartido. El rebase
salió limpio, sin un solo conflicto, y el `range-diff` contra la rama de respaldo confirmó los
trece commits idénticos. La comprobación cuesta un comando; adivinar que "no debería haber
conflicto" y descubrirlo a mitad de un rebase de trece commits cuesta mucho más.

**`push-session-report` no tiene forma de repartir el coste de una sesión entre varios changes
archivados juntos.** Esta PR agrupaba cinco changes de OpenSpec (`controlled-workflow-authority`,
`label-triggered-launch`, `validate-controlled-task-input`, `p0-registry-reservations`,
`durable-controlled-dispatch`) para una sola integración. Empujar un registro de sync por change,
como pide `/aio:sync` para un change suelto, produjo cinco líneas con las mismas cifras exactas de
la misma sesión — sumarlas por change multiplicaría ×5 un coste real de 1. El requirement asume
una PR, un change; una PR de integración con varios rompe esa asunción y hoy no hay una salida
mejor que leerlo sabiendo que hay que agrupar por `session_id`, no por `change`, antes de sumar.

**Archivar con tareas abiertas es correcto cuando la propia PR ya nombró por qué.**
`label-triggered-launch` (renombrado `dispatch-observation` en el código) tenía 16 de 19 tareas
hechas; las tres abiertas —H5, H6 y la comprobación viva 4.3— son exactamente las que el cuerpo de
la PR ya listaba como "deliberately left open (epic #321)", cada una nombrando el change separado
que la resuelve. Confirmar eso con una persona antes de archivar, en vez de tratar "16/19" como una
señal de alarma o como un three-zeroes silencioso, es lo que distingue un diferimiento documentado
de trabajo olvidado.

## 2026-09-20 · #317 — La máquina del bucle se declara en un fichero (PR #318, retro post-merge)

**Un merge por la UI de GitHub se salta el único paso que dobla el change.** #318 se mergeó
directo, y `/aio:sync` — que archiva el change y pliega sus deltas a `openspec/specs/` antes del
squash — nunca corrió. El resultado está en `master`: `openspec/changes/declared-loop-machine/`
sigue vivo al lado de `code-step-sidebar-raw-view`, la capability `declared-loop-machine` no existe
en `openspec/specs/`, y `issue-lifecycle` no lleva su delta de `.harness/lifecycle.json`. El código
llegó; la regla que el siguiente change escribe contra esa capability, no. Un botón de merge
entrega el diff, pero solo el comando entrega la spec — y este backfill corrige lo que el retro
encontró.

**La comprobación viva ya es un issue, no una promesa de issue.** La tarea 4.3 — el watcher decide
igual con la máquina declarada que con `CommandFor` para el inbox real — se registró como
comparación contra el inbox a fecha 2026-09-20 en el cuerpo del commit, y su verificación
continua vive en #319 (`status:ready-for-proposal`), no como nota al pie. El patrón "la
comprobación viva se propone como issue antes del merge" ya es la forma por defecto de este repo.

**Coste, con su asterisco:** una sola sesión (`04dc3063…`) cubrió implement y ready — 1.012 input,
369.629 output, 1,44M cache-create, 128M cache-read, 2h15m de span. Sin `cost_usd_estimated` (el
script no reconoce el modelo). El registro de review salió de otro runtime sin session_id:
`usage.unavailable`, contado, no silenciado.

## 2026-09-20 · Flake de `PhoneTests` bajo la gate completa, otra vez

**Dos fallos de gate seguidos, distinta prueba cada vez, pasa 5/5 en aislado.** Las dos corridas
fallaron en una `PhoneTests` distinta por contención de recursos ejecutando toda la solución junta;
el cambio no toca frontend. Corridas 2 y 3 verdes completas (la tercera: 15/15 pasos, 5m15s, 1035
tests, 0 fallos). Es la cuarta entrada de flake en este log (véanse 2026-08-15, entradas 551 y
1362): el patrón es estable — timeout de Playwright esperando un `header` bajo carga paralela — y
sigue sin tiempo propio para una corrección. La regla que funcionó: reutilizar la gate completa, no
declarar verde aislado como verde completo.

## 2026-09-20 · Backfill del archive tras dos merges por la UI (PR #324)

**El segundo change sin archivar no era un hueco: sus deltas ya estaban plegados.**
`code-step-sidebar-raw-view` (PR #307, 07-09) llevaba trece días vivo en `openspec/changes/` con
4/4 artefactos y 19/19 tareas, igual que `declared-loop-machine` — pero al abrirlo, sus dos
deltas (`read-the-working-diff`, `review-a-change-in-one-tab`) ya existían en `openspec/specs/`
verbo a verbo, con una línea en blanco de diferencia. Alguien plegó la spec a mano y no movió el
directorio. El diagnóstico correcto antes de arreglar: comparar el delta con la spec, no asumir
que "sin archivar" equivale a "sin plegar" — son las dos mitades de `/aio:sync`, y solo una de
ellas estaba hecha en cada caso. #318 no tenía nada plegado; #307 lo tenía todo. Mismo síntoma,
cirugías distintas.

**Un worktree principal con ficheros sucios no es resto del change que se está cerrando.** El
checkout principal llevaba cuatro ficheros modificados (`Sidebar.tsx`, `CodeStep.tsx`, `en.ts`,
`IsolatedIsDeclaredTests.cs`) que la sesión de la víspera atribuyó a este change — y no lo son:
quitan el show-more de CodeStep y endurecen un `ShouldContain` del AppHost csproj, trabajo de
otro frente sin issue. Cerrar un change no da permiso para absorber WIP ajeno que comparte
carpeta; verificar de qué es cada línea antes de tocarlo.

## 2026-09-20 · #328 — El watcher reclama en el registry que la persona arranca (PR #330)

**El flake de `PhoneTests` llevaba cinco días siendo un AppHost olvidado.** Tres gate runs del
día fallaron en timeouts de PhoneTests — siempre frontend, siempre 30s, en un cambio que no
tocaba un solo `.tsx` — y el diagnóstico cómodo era "el flake conocido". La causa real: un
AppHost en el checkout principal llevaba corriendo desde el martes, invisible ya para quien
trabajaba en worktrees, compitiendo por CPU con los servidores e2e que cada gate levanta.
`aspire ps` lo mostró en la primera mirada; pararlo hizo la siguiente gate verde completa sin
un solo cambio de código. La lección operatoria: antes de re-run de una gate roja por flake de
carga, `aspire ps` y mirar qué más corre en la máquina — "el flake conocido" era otro nombre
para "hay algo en la máquina que nadie miró".

**El bootstrap necesita ser una transición del ledger, no un fichero suelto.** La operación
obvia era pushear un documento vacío — pero `ValidDocument` exige `Receipts >= 1`, así que un
registry sin receipts sería ilegible para el primer `Read` que lo mirara. El bootstrap es un
`RegistryOperationKind` con receipt y claim Released que ninguna cuenta de capacidad verá, y el
replay del ledger lo salta sin dirigirlo a la tabla de claims: el primer acquire real encuentra
la task libre y todas las plazas vacías. La identidad marker usa la key "1" porque las reglas
exigen decimal positivo — "0" fallaba la propia validación de identidad, encontrado al correr
el CLI de verdad contra un bare remoto en vez de parase en los tests.

**Coste:** sesión sin figura de uso por runtime (usage.unavailable en cada push), contado, no
silenciado.

## 2026-09-20 · #329 — El dispatch con cadena durable y reconciliación (PR #331)

**El effectId derivado es lo que hace idempotente a un proceso sin memoria.** La decisión al
implementar: el effectId de (task, operación) es sha256 del prefijo de dominio sobre la identidad
completa — no un Guid.NewGuid() — de modo que un reinicio, un tick concurrente o un watcher
reinstalado que recomputen el journey llegan a la misma fila del journal en vez de acuñar un
segundo dispatch del mismo intent. La idempotencia por construcción le gana a la idempotencia por
estado compartido, y el journal que ya existía (replay idempotente sobre payload igual, 409 sobre
payload distinto) es exactamente el árbitro que esa derivación necesita.

**La respuesta de StartRun tiene que llevar la observación dentro.** Primer intento del test: el
wire devolvía `{"id", "state", "dispatch":null}` y el watcher registraba Unknown sobre un POST
201 con RunId — porque `StartRun` lee el envelope y solo el campo dispatch trae el runId que la
decisión necesita. Encontrado por el RecordingLog ampliada (la excepción del 8405 grabada
entera, no solo el formatter), que también descubrió en dos minutos un JSON interpolado mal
cerrado que habría sido una hora de adivinanza. La lección de test: un log de test que no guarda
la excepción es un log que obliga a re-correr con debugger.

## 2026-09-20 · #332 — Grants con binding exacto y razones tipadas (PR #334)

**El replay del bootstrap enseñó que era posicional, y un doc con grants lo rompió.** El
ValidDocument existente comprobaba el receipt bootstrap contra el documento final completo
(`Grants.Length > 0` en mi primer intento): un doc con grants fallaba porque el bootstrap veía
grants "detrás". El orden es lo que el replay verifica, no el recuento final — el fix fue
re-afirmar que bootstrap solo exige que ninguna task esté claimada en su punto del hilo. Un
invariante escrito como chequeo-estado en vez de chequeo-historia es un bug esperando al primer
hecho nuevo que venga después.

**La igualdad de un grant es su binding, no su registro.** El replay del grant idéntico fallaba
como conflicto: GrantedAt difería (UtcNow por llamada), y la comparación por igualdad de record
veía dos grants distintos. Lo que una persona aprobó es actor+task+candidato+árbol+base+pin+epoch
+expiry; GrantedAt es cuándo el ledger lo aceptó, no parte de lo aprobado. La misma distinción
que separa un hecho de su recepción en el evento.

## 2026-09-20 · #333 — La transición nombra su Workflow y el run dice si es transición (PR #335)

**Un scoped reader no puede cruzar la frontera del BackgroundService — la pregunta viaja HTTP.**
El primer intento inyectó `IWorkflowPlanReader` en el ctor del vigilante: compila, y habría
explotado en el primer arranque — el reader es scoped (lleva DbContext del módulo) y un
BackgroundService es singleton. La solución no fue un scope a mano ni captive dependency
tolerada: un endpoint de tres campos (`/api/workflows/plan`: exists, controlled, refusal) al lado
del resto de las preguntas que el tick ya hace por HTTP. La lección de estructura: cuando el
Watcher necesita un dato de módulo, la respuesta es preguntar como pregunta todo lo demás, no
forzar la dependencia — la frontera existía por una razón.

**La captura del refusal enseñó dos bugs de test seguidos.** El rejection test leía `Refusal`
inmediatamente después de construir la Task — antes de que la tarea asíncrona hubiera corrido su
`catch` — y luego leyó el formatter cuando el LoggerMessage structured lleva el valor en el
state. El orden de las dos lecciones es el clásico: primero se pregunta "por qué null" al logger
en vez de a la asincronía; luego se descubre que un LoggerMessage no es un string formateado sino
pares clave-valor, y la aserción debe leer el campo, no la frase.

## 2026-09-20 · #336 — El manifiesto de verificación y la baseline del trunk (PR #338)

**El summary lo escribe el paso que corrió los tests, y el pipe drenado ya existía.** La pieza
central no fue un transportista nuevo (gate y pipeline son procesos sin canal común): fue notar
que `StepCommand` drena línea a línea para loguear — y que un segundo consumidor del mismo dren
es gratis mientras un segundo lector del pipe no lo es. La captura, el regex copiado de una
línea real de `Passed!`, y el fichero junto a la solución: una persona en el worktree lee lo
mismo que leyó la gate, que es la propiedad que un transportista privado no tiene.

**Exit 0 dejó de ser suficiente en el mismo instante en que el summary pudo corroborarlo.** Las
reglas del veredicto se escriben en el orden en que fallan: sin exit, exit≠0, sin summary,
assemblies declarados pero no corridos, cero tests — y cada inconclusive nombra qué falta. La
frase de la epic ("un comando arrancado o cero tests no es gate superada") era una spec
esperando su fixture: el suelo de cobertura se cuenta del slnx del árbol ahí presente, no de un
número en fichero que mañana rompe en silencio.

**Gate completa viva: 1062 tests, y el summary recién escrito corrobora 8/8 assemblies con 1062
listados** — la primera corrida del pipeline produce su propia evidencia sin divergencia entre
lo corrido y lo declarado.

## 2026-09-21 · #337 — El merge verificado: la cabeza aprobada y el cierre con nombre (PR #339)

**El grant ya existía; lo que faltaba era la puerta por donde se le consulta.** La pieza de #332
registraba exactamente el SHA aprobado y nada lo consultaba — el merge era ciego. La admission no
vive en el seam ni en un gate del prompt: vive en `MergeChange`, el único camino que comparten el
botón y `/aio:sync` — una puerta, del lado del código, porque el botón es lo que una persona
cansada aprieta a la 1am.

**Respuesta perdida = observación, nunca re-petición — y el stub lo aprendió a malas.** Dos tests
fallaron porque el stub respondía el contrato que el handler esperaba, no el que el seam defiende:
el grant expirado debe filtrarse en lectura (el registro real filtra en `Grants`, no en el
recorrido del handler), y el head movido se niega en el seam — leído antes de pedir — no en la
admission. El stub es un contratocopies del seam real, y cuando diverge, quien está equivocado es
el stub.

**El effectId derivado es la mitad del cierre idempotente.** `Guid.NewGuid` por intento habría
grabado tres efectos para un merge — el id se deriva de la identidad canónica de la tarea, y por
eso el reintento re-entra en el mismo efecto y la cola reanuda sin volver a preguntar al vendor.
Reanudar empieza por ser reconocido.

## 2026-09-21 · #278 — El comentario de evidencia lo escribe quien la produjo (PR #340)

**La convención era el contrato — el issue pidió infraestructura que ya existía.** La premisa del
issue era "bloqueado por #275"; el dossier murió con #288, pero su convención de emparejamiento
(`evidence-<n>-*.png`, leída del cuerpo de la issue con `--attach` del vendor) sobrevivió en el
paso 4. El comando no necesitaba producto: renombrar a la convención y adjuntar es todo lo que hay
entre el PNG que Chrome escribió y el panel que ya lo lee.

**Corregir la premisa es parte del diseño.** El issue decía "con la app viva publica, sin ella
degrada" — pero la publicación nunca pasó por la app: pasó por `gh`. La app es un requisito de la
*captura* (capture.sh), no del publish; el degradado del paso 8 ya lo escribe capture.sh, y el
paso 9 degrada por credencial o versión de gh. Dicho en el design.md para que nadie lo relea mal
después.

**Verificar con publish real, no con la forma del comando.** Publicar un comentario de verdad y
releerlo — el vendor reescribió la imagen embebida a `user-attachments/assets/…`, el nombre quedó
`evidence-278-*` — es la prueba de que el paso 4 lo emparejará; ningún dry-run de la sintaxis del
compose lo prueba.

## 2026-09-21 · #271 — La clave que nadie consultaba, y el VACUUM que no puede vivir en la migración (PR #341)

**El precedente doméstico cerró la decisión sin tocar BuildingBlocks.** El issue ofrecía dos
caminos — dejar de heredar, o extender `BaseEntity` para claves alternativas — y la respuesta ya
vivía en el módulo: `RunDispatch` declara `EffectId` sin herencia desde #329. Extender
BuildingBlocks para resolver lo que un vecino ya resolvía habría tocado a todos los herederos por
 Resolver un caso.

**EF scaffolds lo que SQLite rehúsa.** El scaffold de la migración propuso `DropPrimaryKey` +
`AddPrimaryKey` in-place; SQLite contesta "cannot define a primary key constraint" — la operación
no es un ALTER. La receta oficial es reconstrucción tabla-nueva-copia-drop-rename, escrita a mano
sobre el scaffold. Y el guard de duplicados aprendió lo mismo por su lado: `RAISE(ABORT)` sólo
existe dentro de un trigger; el INSERT en una temp table con UNIQUE hace el mismo abort con el
mensaje del propio SQLite.

**La gate encontró el VACUUM antes que producción.** Primera corrida: 216+ tests rojos con
`cannot VACUUM from within a transaction`. La lección de la casa se repite — la transacción de EF
envuelve la migración y SQLite prohíbe el VACUUM ahí dentro — y el remedio no fue luchar: el
vacuum salió, las páginas quedan libres y reutilizables, y el doc-comment de la migración dice
cómo reclamarlas al SO a mano. Lo que el cambio promete — un índice menos sobre las mismas filas —
lo prueba la corrida; el tamaño del fichero lo termina quien haga el vacuum.

## 2026-09-21 · #344 — El snapshot de la decisión y su replay cero-I/O (PR #349)

**La decisión ya cruzaba la frontera — lo que faltaba era almacenarla.** El dispatch llevaba
`PlanJson` desde #329; el watcher ya leía la máquina y componía los facts en `LabelLaunchPlan.For`.
El snapshot tomó el camino del payload: nada de re-leer al leer, nada de derivar después. Una
forma (`LoopSnapshot`), un lugar, el mismo `JsonSerializerOptions` del sink.

**Explain junto a Decide — una evaluación, dos preguntas.** El riesgo de una explicación es que
discrepe con la decisión que explica; escribirlas en la misma clase contra el mismo recorrido del
interpreter lo hace estructuralmente imposible. La lección es la de siempre en este repo: dos
códigos que responden una pregunta distinta pueden compartir el suyo.

**La verificación viva enseñó dos cosas que ningún fixture veía.** Primera: los GUIDs se almacenan
en mayúsculas y SQLite compara TEXT case-sensible — los INSERT a mano con `uuidgen | tr a-f` no
unen las FK, y el debug fue un silencio (`commands: []`), no un error. Segunda: el wire de enums
es números y el archivo declarativo son strings — dos formas legítimas del mismo vocabulario, y
solo verlas juntas enseña dónde vive cada una. La corrida viva (run con snapshot, edición de
`lifecycle.json` que no movió la decisión del run, simulate con diff exacto) es la prueba que la
suite de fixture no puede dar: que los tres pedazos acuerdan *en el producto que corre*.

## 2026-09-21 · #345 — La cola de atención compuesta en el host (PR #350)

**El argumento del watcher, gastado la segunda vez.** "El host es el único sitio que conoce
todos los módulos" estaba escrito en Program.cs para justificar el watcher; reusarlo para la
cola lo convirtió en patrón — y es la señal de que la composición multi-módulo ya tiene su casa,
sin abrir la puerta a referencias in-process.

**El enum no crece sin su writer.** La tentación era añadir `PublicationPending` y `GrantMissing`
al `AttentionReason`; la disciplina del propio #246 ("no crear un enum rival") aplicada al revés:
un enum con miembros sin productor es tan vacío como dos enums rivales. La cola deriva causas
del estado persistido; el vocabulario crecerá en el mismo change que lo escriba.

**Dos lecciones de interacción con la pipeline CQS:** una query con `Requires` que no declara
`Access.FiltersToCaller`/`IScopedToProject` falla en runtime con un mensaje que nombra el wiring
(ya lo dice el propio Decorators.cs); y devolver `ErrorOr<Response>` desde una query serializa
el sobre entero — el endpoint-http espera la forma plana que las demás queries devuelven.

## 2026-09-21 · #346 — La wave declarada en la propia issue, computada entera (PR #351)

**El DAG no pedía tabla: pedía sección.** Las issues ya declaran su Seam en una sección con
bullets; Depends es su hermana exacta. Nada de migraciones, nada de relaciones persistidas —
el mirror ya guarda el body, y el contrato ya vive donde el equipo lo escribe. La consistencia
entre declarar y publicar la mantiene el mismo sitio que seam/check: la issue es el documento.

**Computada entera, jamás incremental — la lección L7 es un invariante, no un consejo.** El
PoC pagó por creer que "lo que vi el tick anterior" es estado; aquí la elegibilidad es función
pura del mirror imagen, y el test de "cerrar B libera a C al tick siguiente" la clava.

**Verificar con las issues vivas de la propia wave que implementa el cambio.** Declare
depends en #347 y #348 (verdad del diseño: 348 depende de 346/347), corrió el código real
contra sus bodies — leídas exactas, elegibilidad correcta. El producto verificándose con su
propia backlog: el dogfooding más corto posible.

**Nota de alcance**: la congelación por doble invalidación (tarea 2.2) quedó cubierta por el
vértice de atención de #345 + el manifiesto de #336 — la invalidación doble es visible en la
cola; aplicar el comment automático de congelación queda para cuando una wave real la
produzca dos veces, no antes de que exista el caso. Dicho en el PR.

## 2026-09-21 · #347 — El corpus generado, la comparación prestada (PR #352)

**El corpus vive de lo que ya existía.** No hizo falta esquema, ni tabla, ni migración: los
dispatches de #344 ya guardan el snapshot, la telemetría de #314 ya empuja coste, y el corpus
es el join reproducible de los dos. La lección repetida del control plane entero: cada pieza
anterior resultó ser el almacén de la siguiente, si se diseñó pensando en quién la leería
después.

**El comparador es el simulador corrido en lote — cero código de motor nuevo.** #344 dejó
`LoopSimulator.Replay` como función pura sin seams; el lote es un foreach y una forma de
respuesta. Lo que sí se exigió: los cinco identificadores en la respuesta (evaluador, modelo,
rúbrica, dataset, muestras), porque un número sin paternidad es un rumor con decimales.

**El hash cubre las entries solas.** La primera versión hasheaba el body entero — con
`generatedAt` dentro — y dos regeneraciones daban hashes distintos: el sello medía el reloj.
El arreglo es la regla: `generatedAt` nombra el momento, `hash` nombra el contenido, y sólo
el segundo debe ser estable.

## 2026-09-21 · #348 — La escalera de activación y el peldaño que faltaba (PR #353)

**El watcher ya tenía la llave; faltaba el peldaño.** Los modos existían desde #329
(disabled/observe-claims/dispatch-claims) pero saltaban de mirar a adquirir. `observation` es
el corte correcto: TODO el cálculo, NINGUNA ejecución — ni siquiera el claim, que escribe. Que
la llave ya existiera hizo del cambio unas líneas; que el corte esté después del cálculo
completo hace que el log dé la respuesta de "¿qué habría pasado?" sin simulación aparte.

**PilotTask recorta, no filtra.** La tentación de un allowlist; la solución de UNA task nombrada:
una issue recorre el ciclo entero con el resto mirando, y quitar la clave es toda la ceremonia.
El piloto es una condición, no un subsistema.

**El 404 que no era mío.** Primera corrida de la gate: 1 test de Tasks falló con 404 y pasó solo
en re-run y en su ensamblado — flaky de entorno bajo el load del pipeline, no del cambio. Se
documentó, se reintento la gate completa, verde. (Cuando un flaky es de otro ensamblado, se
dice y se reintenta; enterrarlo es cómo se pudren las suites.)

## 2026-09-25 · #368 — Restaurar los terminales abiertos tras un reinicio (PR #370)

**Lo que la issue pedía era más estrecho de lo que decía.** El grill found que los chats ya
sobreviven (`AgentChatChain` deriva los ids) y los runs ya sobreviven (tabla +
reconciliación); el hueco real era exactamente `TerminalSessions._sessions`. Decir "sesiones
agenticas" y verificar qué sobrevive ya de por sí ahorró dos tercios del cambio.

**El precedente era la respuesta.** `ICheckSink` — Infrastructure reporta, Workspace persiste —
copiado como `ITerminalRestore` con cero decisiones nuevas de arquitectura. Cuando un grill
anterior (#6) dejó escrito el precedente exacto, la propuesta más rápida es leerlo.

**El restore no atraviesa la pipeline CQS.** `IdentityComposition` es explícito: un servicio en
background no tiene caller y no debe recibir un default. El restore habla directo al seam
`ITerminals` reutilizando la resolución de directorio de `TerminalTarget` — el proceso
reabriendo lo que el proceso tenía, no una persona pidiendo un terminal. La lección: la
pipeline de identidad es para callers; los hosted services van al seam.

**El flaky que no era mío, otra vez.** `JournalApiTests.Outcome_is_idempotent` falló en la gate;
verificado en `origin/master` limpio antes de tocar nada — preexistente (timeout de HttpClient),
documentado en la evidencia. Patrón repetido: verificar la línea base ANTES de culpar al diff.

**El clone de claude restaurado muere sin lector.** `exec claude --resume <id>` al boot con
nadie adosado al PTY: el proceso sale enseguida sin escribir transcript. La línea y el id son
correctos (re-abrir al lado vive); el restore deja tab y fila intactas. Pendiente honesto: el
arranque diferido hasta que alguien abra la pestaña — no era de esta issue, y decirlo es mejor
que fingir que resume-en-boot es suficiente.

## 2026-09-26 · #357 — La cola que confundía historia con presente (PR #369)

**`runs` es un transcript; la cola es una lista de tareas.** La cola de atención llevaba
semanas creciendo monótonamente porque ningún estado la drenaba: 19 (luego 34) filas de ruido
para la única que importaba. La regla que faltaba no fue un umbral de edad ni un estado nuevo
fue recordar que el mirror ya es la autoridad de "qué está abierto", y que el join del host
—el único sitio que ve ambos módulos— es donde ese hecho se consulta. El drain entero es un
`HashSet.Contains` en el sitio correcto.

**Drenar sobre un desconocido escondería el bloqueo real.** La mitad del mirror puede fallar
de lectura, y el día que falle es exactamente el día en que no quieres una pantalla tranquila.
El drain se frenó ahí: mirror ilegible = unknown declarado, ninguna fila descartada. El empty
mirror, en cambio, es una respuesta real (todo cerrado) y drena todo. "No sé" y "nada" son
respuestas distintas y la cola las trata distintas.

**El fallo preexistente se documenta, no se entierre.** El gate contó 929 pasadas y 1 fallo:
`JournalApiTests` colgado 100 s en un GET, reproducible en master limpio y en el log de aspire
de dos días antes de la rama. Se comentó en la issue con números y fechas y se siguió: el fallo
es ajeno al diff, y silenciarlo habría sido perder la observación que lo nombra. Queda como
finding para issue propia.

## 2026-09-26 · #374 — El worktree es la instancia del loop (PR #375)

**La inversión ya estaba hecha en el backend; solo la UI no se había enterado.** #365 dejó
escrito en `LoopWorkflowPlanReader` que "a controlled Run is one segment of the declared loop",
pero la navegación seguía tratando al run como sustantivo central: ruta propia, tabla global,
pantalla que no sabía dónde estaba el viaje. Este change no movió ni un hecho — solo dejó de
promocionar la vista que había quedado obsoleta. Lección: cuando el modelo ya dice una cosa y la
UI dice otra, el diff pequeño está en la UI.

**Proyectar venció a almacenar, dos veces.** `Worktree.State` era una columna muerta que ya se
había quitado; la posición del viaje es `journeyFor(runs, loop)` y no una columna nueva. Y la
palabra de posición de la fila del sidebar es el mismo join de liveness que ya corría la rail.
En ambos casos la decisión estaba grabada en un comentario del código (`Worktree.cs`) que este
change solo siguió. Precedente: `ICheckSink`/#368 — cuando el precedente está escrito, leerlo
es la propuesta más rápida.

**El matching run↔nodo se hace por palabra de comando, no por id.** El loop se edita entre
transiciones; los ids de nodo y el orden no son estables. `/aio:implement` — lo que el dispatch
mandó y el run grabó en su `workflowLabel` — es lo único que ambos lados siguen diciendo igual.

**`git add -A` en un checkout compartido es un bug de proceso, no un force majeure.** A mitad
del change, un `add -A` absorbió trabajo in-flight de otra sesión (la extracción de LoopTab)
que estaba modificada en el árbol de trabajo. Se detectó revisando el diff (archivos que
nadie había escrito), se preservó en backup, `reset --hard` sobre el commit contaminado y
re-commit archivo-por-archivo propio. Regla que ya existía ("assert your worktree") pero con
una segunda mitad que esta casa aprendió: el `status --porcelain` previo debe mostrar solo los
propios archivos, y cuando no lo hace, el fila-a-fila, no el `-A`.

## 2026-09-27 · #377 — laya puntúa el riesgo del change (PR #378)

**El modelo puntúa; el código pesa.** Medido sobre la propia #365 antes de escribir nada:
`noul` atómico acertó `touches_migrations` 0.92, `new_dependency` 0.93, y el `choice` de banda
dio medium 0.83 — pero el `score` de blast radius contestó 1.29/3 para un cambio que borra un
módulo entero. Esa medición fijó el diseño antes de que existiera: primitivas `noul` para los
hechos, pesos en el script, y la banda `low` del modelo se eleva a `medium` cuando la carga de
hechos lo pide (en este change: 3.0). Lección: medir el primitivo contra un caso propio antes
de construir el gate encima.

**El fix SSL de Homebrew es parte del instalador, no de un documento.** El primer `ensure` murió
con `CERTIFICATE_VERIFY_FAILED` — el Python de Homebrew no lleva el truststore del sistema y la
descarga de pesos de HuggingFace falla. Una línea (`SSL_CERT_FILE` al bundle de certifi del venv)
en el launcher; sin ella, la instalación "funciona" en Linux y falla en la plataforma donde se
desarrolla. Los fixes de plataforma viven en el script que arranca la plataforma.

**La consulta de la época previa quedó en el camino: laya-serve sirve el mismo contrato
`POST /v1/systemone` que Jev, en localhost, sin clave.** El nodo `risk` llevaba meses citando un
API hospedada que este repo nunca integró; el reemplazo no tocó ni una línea de `when:` del loop
ni un solo archivo de `src/`. Un fact-step bash + HTTP es todo lo que hizo falta — sin seam World,
sin cliente C#, sin migración. Cuando el loop declara un hueco así de estrecho, llenarlo puede no
tocar el producto.

**Telemetría: 3 fases sin figura (runtime sin session id en el transcript).** Coste no medido
por este mecanismo; el gate (`aspire do ci`) corrió dos veces — una en el checkout principal,
una en el worktree tras el rebase — ~5 min cada una.

## 2026-09-27 · El estado y la escalera del watcher se ven en Settings (#381, PR #382)

**El diagnóstico fue el issue.** Una sesión entera en averiguar por qué el watcher no cogía las
tasks, y la respuesta era un string de configuración (`LabelWatcher:Mode`, default `disabled`) que
nada en el producto nombraba — `MayObserve()` salía en silencio. El cambio que salió de eso no fue
"mostrar un modo": fue que el estado del watcher es legible (snapshot del último tick con sus
refusals), movible (un rung por comando, el rechazo server-side), y persistente donde velopack no
lo borre (`~/.aiorchestrator/`, la misma propiedad que la base de datos de ADR-0003). Cuando el
coste de un diagnóstico se convierte en la explicación de un issue, el issue vale lo que la sesión
que lo enseñó.

**Dos bugs encontrados corriendo el producto, ninguno por la suite.** Primero: el raise desde un
`disabled` persistido se rechazaba — mi primera lectura de "un rung a la vez" confundió "no saltar
rungs" con "no arrancar desde abajo"; el comando no lleva destino, así que un skip ni se puede
pedir, y el único rechazo legítimo es el techo. Segundo: el timer solo se armaba si el proceso
arrancaba en modo activo — un raise desde disabled requería restart, exactamente lo que el cambio
decía evitar. Ambos salieron del curl y del tick en vivo, no de `dotnet test`; ambos se corrigieron
con su spec delta editado. La lección de siempre de este repo, dos veces en un cambio.

**El merge se adelantó al archive.** Una persona mergeó el PR antes de que corrieran el archive y
la retro, así que los specs fold y esta entrada aterrizaron como follow-up a master (`close-381`)
en lugar de ir dentro del squash. Coste: un commit más en master y el squash quedó sin los specs.
El loop diseña el orden (archive → retro → merge) para que eso no pase; una persona con permisos
siempre puede saltárselo, y el próximo agente encontró esta entrada para saber qué pasó. Nada se
perdió — solo el orden.

**Telemetría: 4 fases sin figura (runtime sin session id en el transcript), una degraded — el
merge adelantado.** Coste no medido por este mecanismo; el gate corrió tres veces (una fallada por
un test del beat disabled, dos verdes, 18/18 steps, ~7 min la última).

## 2026-09-27 · Adopt the harness, con la regla de trunk y Verify (#364, PR #383)

**El bug del hook lo encontró el test que hacía el commit de verdad.** La adopción escribía
`.githooks/pre-commit` y el guard, al ver el contenido correcto byte a byte, salía temprano —
sin escribir `core.hooksPath`. Un hook perfecto que git nunca ejecutaba: los tests de contenido
pasaban todos, y solo el test que corría `git commit` sobre un repo real lo vio. La house rule de
este repo (verificar contra la realidad, no contra un fixture) no es una preferencia de estilo —
es la única cosa que distinguía "instalado" de "configurado", y ninguna aserción sobre ficheros
podía ver la diferencia.

**El primer approach del hook embebido se tiró entero.** Generar el recurso desde la constante
del guard con un target de MSBuild y regex sobre el fuente era frágil — los raw strings con
comillas triples dentro de un `ReadLinesFromFile` no son un lugar donde vive la verdad. La
solución es la que debió ser la primera: el guard expone `Hook` y el adopter escribe desde la
constante, un solo lugar define el texto. Media hora perdida que un diseño más simple habría
ahorrado; la señal era que el csproj estaba haciendo trabajo del código.

**Tres capas que se necesitan mutuamente.** El auto-worktree hace que ningún agente dispare
nunca el hook (nunca están en trunk); el hook hace que una persona que trabaje rápido en master
lea el remedio en la negación. Sin cualquiera de las dos, la otra es una trampa — runs que fallan
a mitad, o commits que se bloquean sin salida. Y la adopción instala las tres de una vez: un
folder adoptado nace con la regla, el manifiesto en el build del server.

**Telemetría: 4 fases sin figura (runtime sin session id en el transcript).** Coste no medido
por este mecanismo. El gate corrió verde completo — 6 ensamblados, 896 tests, 0 fallos — tras dos
cancelaciones del pipeline que resultaron ser del entorno de la sesión, no del cambio.
---

## 2026-09-27 · #384 — la pestaña restaurada difiere su spawn al primer attach (PR #385)

**La deferral rompió una suposición de dos bifurcaciones.** El `Open` de `TerminalSessions`
siempre había sido un solo acto — registrar y lanzar — y el attach del hub se escribía como
una lectura pura ("un hub no es el sitio donde se lanza un proceso"). Diferir el spawn obligó
a que el hub llamara al seam por la puerta del open: el id que nombra lo registró el restore,
la línea la fijó el restore, y nada que llega al hub llega a una línea de comando. La
restricción del hub se honró sin doblarla, pero la docstring de "lectura pura" murió aquí: el
attach es el momento del spawn, y decirlo en la doc del hub antes que en el código de
`TerminalSessions` fue lo que hizo la review legible.

**El bug que la suite no ve era de la otra bifurcación.** Corriendo el producto, el attach de
un pane a un open normal (sin `ResumeFrom`) lanzó la misma línea una segunda vez: el open
lanzó su pump sinclaimar el start, así que el `EnsureStarted` del attach lo reclamó y
volvió a lanzar — dos PIDs de `claude` sobre 1 session, ambos escribiendo en 1 buffer. El fix
es una línea (`ClaimStart()` en la rama que lanza), pero sólo un pane real la produce, porque
la suite stubba el runner y cuenta starts, no PIDs. La de siempre de este repo, medida otra
vez.

**`aspire do ci` desde un worktree resolvió la solución del checkout padre, tres veces.** Con
`--apphost` absoluto al AppHost del worktree, el pipeline restauró, compiló y testó
`ai-orchestator-new/src/AiOrchestrator.slnx` — 458 tests de Infrastructure contra 463 del
worktree — y dejó los artifacts en `src/artifacts` del padre. El "PIPELINE SUCCEEDED" no
era evidencia de la rama; el árbol se verificó a mano (`dotnet build` + `dotnet test`
sobre el slnx del worktree: 1008/1008). Es una anomalía del wrapper, no del change: un
`--apphost` absoluto debería fijar el árbol, y no lo hace. Su propio issue.

**Telemetría: 3 fases, 2 degraded (el label de propose ausente, el worktree de `aspire do
ci`), ninguna con figura de coste (runtime sin session id).** Gate final sobre el árbol
rebased: 1021/1021 tests, 0 failures, frontend typecheck/build/lint verdes.

## 2026-09-27 · #388 — la mitad del paso de tests de la gate (PR #389)

**Medir antes de tocar encontró los tres costes correctos.** 377.5s de los 399.9s de la gate eran
`ci-test-server`, y el desglose por assembly/clase separó tres cosas distintas que el instinto habría
mezclado: un rebuild incremental dentro del reloj del paso, un boot de `WebApplicationFactory` por
test (~2.4s de suelo × 100), y once clases seriales en una colección xUnit. Un `--no-build`, un host
por clase, una partición en dos colecciones: 250.6s → 95.5s en Runs, 318.5s → 163.5s la suite entera,
1015/1015. Ninguna conflagración — tres cambios pequeños que solo eran visibles con el reloj en la
mano. La cobertura era gratis y la mutación se saltó sola: el instrumento nunca fue el problema.

**`aspire do ci` no puede verificar su propia rama.** Desde un worktree enlazado, el CLI resuelve el
AppHost caminando hacia arriba hasta el `.git` directorio — el checkout principal — y la gate mide
`master`, no el cambio. Es el mismo walk-up que AGENTS.md documenta para `aspire.config.json`,
heredado por toda la gate, y `/aio:implement` (paso 7) asume que puede correr desde un worktree. El
arreglo fue la verificación equivalente a mano (`dotnet test` + cobertura desde el worktree); el
hallazgo merece un ADR — la regla del repo es worktree-por-issue, así que cada implement paga este
desvío. Notas degraded x3 en telemetría, las tres el mismo hecho.

**Dos clases del plan no llevaban el patrón que el plan decía.** `LabelWatcherTests` (16.3s) no bootea
ningún host — su coste es bootstrap de git real por test. `JournalApiTests` (27.1s) crea un fixture
completo por test con base propia y restart semántico adentro. Ninguna conversión forzada: quedaron
como están, documentado en tasks.md. La lección es que la/issues que clasifican clases por su duración
deberían clasificarlas por su forma — la duration se correlaciona mal con el remedio.

**Telemetría: 4 fases sin figura (runtime sin session id).** Coste no medido por este mecanismo.

## 2026-09-28 · #400 — leer los hechos de la claim en su worktree (PR #401)

**El guard que protege a un agente de inventarse un diseño mintió por mirar la carpeta equivocada.**
#361 enseñó a este loop a comprobar que un label `ready-for-implementation` tiene detrás un proposal
real — pero la comprobación se hacía contra el checkout primario, en `master`, donde `/aio:propose`
nunca escribió nada: el cambio vive en la rama que crea, cuya posición de trabajo es el worktree
`change/<n>`. El resultado medido con #397: un refusal eterno (`no OpenSpec change` — "run
/aio:propose first") para un propose que ya corrió, y el flujo automático que nunca arranca. La
distinción que lo arregló ya estaba escrita en el propio watcher para otra cosa: el **loop file** se
lee del checkout porque es un hecho del trunk (#365); los **hechos de la claim** son hechos de la
rama, y la rama trabaja en su worktree. La misma regla, aplicada al objeto equivocado, era el bug.

**Un test que no puede fallar es un test que no ha probado nada.** El primer corte del fallback por
ref (`ls-tree` sin `-r`) nombraba el directorio del cambio y nada dentro de él — el matcher de
`tasks.md` no podía disparar nunca — y su test pasaba en vacío porque la rama "remota" era local y el
ref nunca resolvió: el hecho lo respondía el walk de directorio. Lo encontró la verificación contra
la realidad (el `ls-tree` real contra `origin/change/397` no listaba `tasks.md`), no la suite. El
arreglo del test — `update-ref` para un remote-tracking ref real, `reset --hard` + `clean` para que
el walk de directorio no pueda responder — es la forma que este repo ya usa en GitHistoryTests, y el
caso cuarto (directorio sin `tasks.md` no es cambio) existe para que el matcher no vuelva a mentir.

**La gate compartió máquina con otro agente y falló por las dos mitades a la vez.** El primer `ci`
falló con 1 test de Server.FunctionalTests (50/50 en aislado), y las tiradas sueltas posteriores
compilaron el frontend del **checkout primario** — con su `Sidebar.tsx` roto por el trabajo en curso
de otro agente (change/402), no el del worktree — mientras la propia gate documentaba en AGENTS.md el
walk-up del `.git` que lo causa. `/aio:ship` con holds quitados y otro loop en la misma máquina es
una combinación que vuelve a pasar; la segunda tirada limpia (1059/0) es la evidencia, no la primera
ruidosa.

**Telemetría: 4 fases (propose ok, implement degraded, implement-ready ok, sync ok), ninguna con
figura de coste (runtime sin session id).** Gate final: 1059/1059 tests, 0 failures, en el árbol que
se va a mergear.

## 2026-09-28 · Open a task's loop position beside the Overview's lists (PR #402)

**Una lista de las seis etapas del loop no se copia, se lee.** El grafo del pane del Overview
necesitaba los seis estados, sus iconos, colores y etiquetas, y la forma barata de dárselos era
copiarlas de Tasks. El cambio las exportó de donde ya existían (`loopOrder`, `statusLabel`,
`statusIcon`, `statusColor`, e incluso `IssueBody`), en vez de escribir una segunda lista que el
propio comentario de la original avisa que "eventually would be five and six". Dos pantallas
leer la misma lista es la mitad de la lección de que el servidor traduce los labels una sola vez.

**Una gate roja con la app corriendo no es la gate del cambio.** El primer `aspire do ci` sobre la
rama rebaseada falló un test — `LabelWatcherTests.A_disabled_clock_records_its_disarming_at_start`
— que pasa en aislado sobre el mismo punto base (`a5293ec`), y la gate volvió a verde (1044/1044)
al parar el AppHost de dev que este propio trabajo había levantado en el checkout primario. El
mecanismo no está probado; lo que está probado es la correlación: un AppHost dev vivo mirando este
mismo repo mientras la gate corre sus functional tests, un fallo que el árbol solo no reproduce.
#400 lo escribió ya para los agentes — "la gate compartió máquina con otro agente" —; esta es la
variante silenciosa: el proceso es el del propio trabajo, así que nadie se acuerda de pararlo.

**El sync corrió por fuera del loop, y sus puertas frenaron donde debían.** Nada de lo que
`/aio:sync` espera existía: no issue, no change de OpenSpec, worktree no visto por el producto, PR
draft sin aprobar. El guard detuvo el merge y registró el `degraded` del momento; el landing siguió
por aprobación explícita en vez de por un `--admin` que habría pasado la puerta. La puerta no se
saltó, se cambió de camino, y el record quedó antes del merge, que es donde la orden lo pedía.

**Telemetría: 2 fases del sync (degraded al detectar el loop ausente, ok al volver la gate),
ninguna con figura de coste (runtime sin session id).** Gate final: 1044/1044 tests, 0 failures,
en el árbol que se va a mergear.

## 2026-09-28 · Explore idea: una línea interrogada hasta su issue (PR #404)

**La base estaba roja, y lo vio el primer paso de la gate.** El primer `aspire do ci`
falló sobre master — un ternario JSX sin cerrar que #396 dejó en `Sidebar.tsx`, con
typecheck, build y lint a la vez — y no era de nadie visible. El paso 0 del plan
("gate en la base, y si está roja, dilo y para") obligó a un fix de un commit
separado antes de tocar la feature; el landing la lleva distinta del cambio. Un
paso de gate no es formalidad: es la única lectura que detecta la base roja que
nadie más ha mirado.

**Un plan escrito antes de leer el código puede pedir duplicar lo que el código ya
posee.** El plan pedía una familia nueva de lambdas `SpeakArguments` por vendor,
pero `AgentCatalog` ya guardaba las mismas spellings como `PromptArguments` y
`ModelArguments`; `Speak` compone sobre esas piezas en vez de crecer un segundo
catálogo, que es lo que el registro existe para impedir. La otra desviación es la
misma forma: el plan listaba los 4 stubs que ensanchar `IAgentRunner` rompería, y
un quinto (`RecordingCommands` en `TerminalDeferralTests`) no estaba en la lista —
compiló a ciegas y el build lo nombró. "Qué se rompe" se encuentra con
`rg ": IAgentRunner"`, no se escribe de memoria en un plan.

**El suite prueba la forma; la realidad prueba la conducta.** La verificación
manual contra un repositorio desechable (`/tmp/explore-fixture` + remote throwaway)
es la única que probó que el agente lee el `AGENTS.md` y el `.harness/config.json`
del fixture, pregunta las tres preguntas del DoR, y filea el issue #1 en el remote
con la etiqueta que ese config declara y el cuerpo en las cinco secciones del
prompt. En la DB, 0 filas `runs` y 0 worktrees: solo la fila legítima de
`terminal_sessions` para el restore. La regla de la casa se cumplió: cada bug que
esto encuentra se encuentra corriendo el producto.

**El sync lo interrumpió un merge concurrente.** `#402` entró a master en
medio de la sincronización y tocó los dos files frontend de este cambio
(`Sidebar.tsx` — cerró también el ternario de `#396`, y rebase `--skip`ó este
commit suyo, que no aportaba nada ya; `en.ts`); el append del retro quedó
stageado en `master`, donde el trunk guard lo rechazó, porque "una rama es una
claim" y el main checkout no es la claim de una rama ajena. El rebase sobre el
nuevo master no tuvo choque semántico: la intersección de files estaba cubierta
por el `--skip`.

**Telemetría: 3 fases del sync (2 degraded con la nota de la desviación, 1 ok),
ninguna con figura de coste (runtime sin session id).** Gate final sobre la rama
rebaseada a `ee0fdcf`: 18/18 steps, 1058/1058 tests, 0 failures, en el árbol que
se va a mergear.

## 2026-10-01 · #397 — la pantalla del reattach, modelada por XTerm.NET (PR #398)

**836 líneas de parser VT propio eran un subscriptivo, no un activo.** `ScreenFrame`
modelaba a mano un subconjunto de VT (CUP, ED/EL, SGR, alt-screen, wide glyphs) y su
propio comentario confesaba lo que faltaba: scroll regions, IL/DL, ECH, tab stops —
exactamente con lo que repintan los TUIs agénticos. El reattach a un run vivo mostraba
media pantalla, y cada secuencia nueva de un agente era mantenimiento nuestro. La
salida no fue extender el parser: fue reemplazarlo tras el mismo seam por XTerm.NET
(un emulador headless con VT completo), y escribir solo el `Serialize()` que recorre
el buffer y emite SGR + texto + cursor. **Un parser VT es un commodity; comprarlo
deja la superficie de bug donde nadie la paga dos veces.** El riesgo aceptado y
escrito en el issue — librería pequeña, un autor — se mitigó aislándola en `RunFrames`,
un solo punto de reemplazo.

**La conducta se probó contra un run real de 6 horas, no contra las secuencias que
el parser viejo modelaba.** La prueba que importa no fue la suite nueva (26
`RunFramesTests`, incluyendo los casos que el parser viejo se saltaba: DECSTBM, IL,
DL, ECH, tab stops), sino un run vivo de `opencode` — el TUI de lista con scroll
regions que el issue nombra como roto — reattachado desde otra pestaña durante más
de 6 h: pantalla completa, lista avanzando (70→80→…) sin líneas desplazadas ni
duplicadas, barra inferior y pies presentes. En el mismo intento, un run de Claude
Code se quedó parado antes de pintar nada (solo bytes de setup) — la verificación
dice qué vio, no adorna el hueco.

**La gate roja dos veces enseñó dos cosas, y ninguna era de este cambio.** Primero,
el flake `LabelWatcherTests.A_disabled_clock_records_its_disarming_at_start` (pasa
4/4 aislado, falla bajo la carga paralela de `ci-test-server`): #405 ya lleva el root
cause — `LastTick` se asigna tras el primer await, así que `StopAsync` puede ganar la
carrera. Segundo, `aspire do ci` lanzado desde el worktree ligado resolvió el slnx
al **checkout primario** (los conteos 1044-vs-1047 lo prueban): la gate "pasó" dos
veces sin probar esta rama. La validación de la rama fueron los números del worktree
sobre su propio slnx. El hallazgo — el AppHost deriva el slnx de
`AppHostDirectory`, ergo cada checkout corre el suyo, pero la CLI aspire escribe
`aspire.config.json` en el checkout primario y la gate puede leer de más — merece
issue propia y no la tiene.

**El sync se reentró sobre un landing interrumpido.** El squash (#398) ya estaba en
master y la issue cerrada, pero el archivo, el retro y el pliegue de specs nunca
aterrizaron: una sesión anterior saltó los pasos 1–4. El close-out se recreó sobre
una rama `change/397` reseteada a master (con backup ref del head original,
`06ffb252`) y se dejó caer por el endpoint del producto, `POST
/api/folders/{id}/merge`, para que su cola — parar entorno, borrar worktree, tumbar
rama — dispusiera de todo como en un sync normal. La gate final corría sobre el árbol
exacto que se va a mergear.

**Telemetría: 5 fases del cambio (propose ok; implement/implement-ready degraded con
las notas del flake y del slnx cruzado; sync degraded al reentrar el landing
interrumpido), ninguna con figura de coste (runtime sin session id).** Gate final
sobre la rama recreada `1a3e9c1f`: 18/18 steps, 7/7 ensamblados del slnx, 1083/1083
tests, 0 failures, en el árbol que se va a mergear.

---

## 2026-10-04 · A 390 px the Change tab's step panel is reachable (#427, PR #430)

**El defecto lo encontró una persona en un móvil real, y dos suites no lo vieron en un día.** El set
estrecho ya fotografiaba los seis pasos a 390 y ya asercionaba dónde terminan los controles —
`past=0` — porque el defecto no estaba en el ancho, estaba en el alto: la cabecera, la tarjeta y el
rail llenan el viewport y el panel queda debajo de un pliegue que ningún ancestro desliza. La
aserción que falta no es otra captura, es que la primera fila del panel entre en el viewport por el
scroller de la propia columna. Una captura sin la aserción es la misma fotografía que había antes:
el defecto seguía verde.

**Un solo scroller en la cadena.** Dos scrollers anidados eran la carrera que medía la issue: con el
panel como `flex: 1` dentro de ancestros que recortan, un `scrollIntoView` dentro del panel
desplazaba la vista 48 px y se rendía. Bajo 560 px de su propio elemento (la convención
`useNarrowContainer`), la columna entera es el scroller y el panel toma su altura natural — sin
tocar el layout ancho, que se mide pixel a pixel con el conjunto existente.

**Telemetría: 3 fases (propose, implement-ready, sync), ninguna con figura de coste (runtime sin
session id). Gate sobre `cca48e8c`: 18/18 steps, 8 ensamblados, 1080/1080 tests, 0 fallos; 12
capturas en ambos temas publicadas contra ese SHA.**

## 2026-10-04 — #431 Separate the review and execution entries of the task detail

A run never carried the repository it was for — only `taskKey` and the folder it worked in. The
proposal had declared the join as "extend the run row with a repository", and grounding cancelled
that: `folderFor` and `repoIdentity` already turn a folder's `remoteUrl` into the identity a task's
own address names. The join stayed frontend, and the `read-a-run` delta was edited to say what the
join is rather than what the proposal assumed it would need. Two lessons ride with it: a spec delta
written before grounding declares the shape the author expected, not the shape the code has —
1.3 exists to catch that, and it did; and react-query keeps `data` undefined when a query errors,
so a loading check that comes first masks the error for ever — the panel held "looking" while the
query said "failed" beside it. The e2e fixture taught a third, already a house rule here: two
classes sharing a Vite-booting fixture kill each other in parallel, so the `review-frontend`
collection serializes them — measured at 0.55 s of silence in `ci-test-server`, not a hypothesis.

## 2026-10-04 — #432 Read the closed result only from the pull request

Two races taught the same lesson twice in one day: the frames between an answer and its arrival
are a state, and code that fires "on every scope" serves them. The parent screen had one
unconditional diff query — its own comment said "whether or not anybody opened the mode" — and it
was the last local read serving a closed result; withdrawn behind the Change tab and the pull
request's answer, which itself holds the diff off while pending, so the working copy can never
serve as the closed result for the frames in between. The network-level negative test is what
found it: every request was recorded, and the assertion "closed means zero local reads" was red
until the parent stopped firing.

The fixture race underneath had a sharper edge: Vite's config merge treats an inline `port: 0`
as unset, so the checkout's `strictPort: 5173` won over the fixture's ask and the review Vite
and the AppHost's own Vite killed each other — ten tests dead in a millisecond each, with the
reason only visible in the fixture's queued log lines. The fixture now reserves an OS port and
passes a concrete number, which the merge honours. And the entry-budget gate earned its keep a
third way: composing #419's panels from the Change tab pulled their prose renderer into the
entry chunk and pushed it to 202.95 kB against a 200 kB budget — the lazy boundary the route
already gives the review screen was the shape to copy, not a discovery.

## 2026-10-04 — #433 Answer where a task's work stands, from the task itself

The walk was one new file and no moved code, which is what 1.2 was for: `journey.ts` and
`commandWord.ts` are pure, and purity is what made them reusable without ceremony. Three edges
were found by the tools this repository already trusts. The entry-budget plugin refused a
static import — 244.75 kB against 200, the YAML parser riding in what loads before anything is
drawn — and the lazy boundary the journey header already had was the answer, not a discovery.
The e2e stub's loop file had to use the declaration's real shape (`command:` fields, not an
invented `main:`), because a stub that does not parse reads as feature failure, and the
debugging went the wrong way for an hour before the shape was questioned. And the first join
matched on `taskKey` alone — the exact bug #431 was filed to remove, rebuilt two weeks later in
new code by the same hands; the spec's identity rule earned a second reader the day it was
written, and the test that caught it (`Another_repositorys_runs_do_not_join_the_walk`) is now
the reason it cannot quietly return.

## 2026-10-04 — #434 Observe a run's remote agent session, from its own run

The epic's last change carried the epic's oldest rule to a new place: nothing crosses a seam
unearned. The ACA adapter promotes exactly two facts from the demo — resume belongs to the
connect door and only to it, and a data-plane refusal is unreachable rather than guessed — and
refuses the demo's resume-on-every-connection habit, which is the exact behaviour the two-door
split exists to prevent. The lesson worth keeping is where the promises were measured: the
"effect-free read door" claim is a count at the adapter's own CLI boundary (a test double
records every argument list; stopped reads as one `list` and zero resumes), and the
"confirmation before activation" claim is a count of posted bodies after the offer is pressed.
Promises about effects belong at the boundary where the effects would cross, not in review
prose.

The architecture tests earned their keep again: a permission-named query without a project scope
and an explicit Admin grant — the exact two mistakes a new use case invites — failed the build
before a person could read them. And the honest open item is recorded as open: the real
data-plane proof runs where the Data Owner role lives, on the person's go; a checkbox claimed
from a test double would be the invention this product refuses everywhere else.

## 2026-10-04 — #440 Bump the version on every merge to master

The version had been a fact without a witness: 0.2.2 sat in the props with no tag, five merges
underneath it, and the last release a week old. The change that fixed it is mostly one decision
about where a shared state may be created: the release rides the merge script — the one path
every caller already walks — and the bump rides the close-out edit that the squash-merge was
always going to land. A script editing master after the merge would be a second commit nobody
approved; a workflow would be a second path to shared state; both rejected in the design before
the first line of bash.

Two mechanics taught by running it: `gh release create <tag> <sha>` in the positional form does
not resolve a bare commit — `--target <sha>` does, and one call creates both the tag at the
remote and the release, so there is exactly one shared-state action to be idempotent about.
And idempotency has to be asked of the vendor: the tag lives at the remote before any local
checkout has fetched it, so the re-run reads `gh release view` and `git ls-remote` rather than
`refs/tags`, which said "no tag" while the release sat there the whole time.

## 2026-10-04 — #442 Attach the installers to every release

#440's machinery named states and shipped nothing you could run, and the one release that ever
carried an installer had it uploaded by hand — the exact forgetting the machinery existed to
prevent. The fix is one decision about whose job the body of a release is: pack lives inside
`release`, because the pkg is the version's body and `release` already owns "one version, one
tag"; `merge` stays the caller it was and inherits everything.

Two mechanics worth keeping. Velopack fixes the Setup.pkg's name on purpose — it is the URL an
installer and a future feed rely on — so the file is rewritten by every pack and can witness
nothing; the version-carrying full nupkg is the skip's witness, and a skip keyed on the wrong
file would happily upload yesterday's installer under today's tag. And the backfill completed
v0.2.3 in place rather than reproducing it: the tag existed, history does not move (ADR-0005),
and "the release is wrong so we re-cut it" was the habit this whole change exists to retire.

## 2026-10-05 — #448 Rename Task to PBI, keeping identity and history

A rename that touches the locked vocabulary is a change about scope discipline as much as
naming: the word lives in the module, the seam, fifteen routes, the wire, the SPA and 121 i18n
keys, and a rename that ships in halves leaves two words for one thing — exactly what the
vocabulary lock exists to prevent. Delivering it whole, against an inventory that classified
every occurrence first, is what kept `System.Threading.Tasks.Task`, the vendors' `status:`
labels, the stored `### Task input` marker and the deployed `TaskSources:` config out of a
mechanical sweep. The identity half is where the care paid: no migration was needed because
`PbiKey` keeps `HasColumnName("TaskKey")` on disk, and the effectId invariant is a regression
test deriving the recorded operation through the renamed `ControlPbiIdentity` and asserting the
same UUID.

Two mechanics this change taught. First, the spec fold of a superseding rename meets openspec's
scenario-preservation rule: MODIFIED blocks must carry every scenario name the current spec
holds or the archive refuses, and there is no scenario-level RENAMED — so refreshing a stale
delta against the merged master is the sync's work, scenario names verbatim, bodies reworded.
Master had moved under this branch in eight capabilities, and the abort-early, no-files-changed
refusal is what made refreshing safe rather than silent loss. Second, the environmental halt
was the right halt: a gate red for a reason the change did not cause (a Spanish-localized `gh`
stderr tripping the unreachable classifier) still stops the merge, and the fix belonged to
master (#460) with its own squash — the branch that could merge and the branch that fixed the
machine kept being different branches. Cost note: no usage figure was pushed for any phase of
this change (no session id in the runtime that ran them), so the retro carries no token count
for it — a measurement lost, said out loud rather than guessed.

## 2026-10-05 — #449 Read and move a PBI's position through the Connector

The epic's vocabulary rule was tested at its deepest layer: the position — the one field the
core still derived by parsing the vendor's labels — had to cross the seam without dragging the
label table along. What kept the move honest was refusing the silent defaults: a vendor that
cannot say where a PBI stands answers `unknown` with its reason, and that answer is a state on
the wire and its own zone in the UI — never `backlog`, never a lane, never a drag target. The
three positions the loop declares and the old enum dropped (`needs-refinement`,
`proposal-review`, `blocked`) arrived in the same edit as the contract that reads them, because
a vocabulary that ships in halves leaves the module re-deriving what the Connector was meant to
own. ADO's `System.State` was already being read and thrown away; the change's cost lived in
the promotion, not in new plumbing. Jira and Linear refuse with a remedy — the honest answer a
fake translation would have erased.

The sync re-meet: openspec's scenario-preservation rule found this delta's renamed requirements
too. `RENAMED Requirements` exists at the requirement level and carried the two headers
(`label` → `position`) in the #448 style; the scenario names stayed verbatim as the current
specs hold them — one renamed scenario in the delta had to go back to its `label` name with its
body reworded to say position — and the abort-early, no-files-changed refusal is again what
made refreshing safe rather than silent loss.

Cost note: no usage figure was pushed for any phase of this change (the runtime reported "no
session id"), so the retro carries no token count for it — a measurement lost, said out loud
rather than guessed.

## 2026-10-05 — #450 Show the PBI's journey with its history by stages

The detail's centre moved from the launch form to the journey, and the load-bearing decision was
not the redraw but the refusal to redraw twice: one spine component serves the folders' detail pane
and the PBIs screen's walk, both fed by `journeyFor`'s single resolution, and `LoopFlow` was deleted
because it was the ONLY thing on the screen still deriving a stage list from the `PbiStatus`
enumerations on the wire. The design's iron rule — nothing re-interprets the declared loop, no
parallel state machine, no lifecycle-label-to-stage mapping — is what made the deletion a
convergence rather than a removal; the `PbiStatus` list the prototype's `STAGES` would have seeded is
the seven statuses of the wire, not the loop's declaration, and the design forbade it in writing
before a line of the spine was written. #449's Connector owns the current pointer; the runs' command
words own the walked history; where a label and a dispatch disagree the spine shows the
Connector's position as the current pointer and the runs' transitions as the walked history, and
names the label as the workflow's claim, not a position.

The by-identity read was moved from the browser to the seam for a reason that pays only at the
boundary: a closed journey is the SAME read as a live one, because a run survives by `WorktreeName`
and key and NOT by the worktree's directory, so a PBI whose worktree was removed answers through the
same path as one whose worktree is live. The join is #431's identity rule, the one #448 wrote a
regression test for (`Another_repositorys_runs_do_not_join_the_walk`), HERE at a new surface — a
read served once, source/repository/key together, so two repositories sharing a key do not borrow
each other's runs. The bash-stage mechanic carries the same honesty: the loop's own `overlap`,
`claim`, `risk`, `gate`, `tripwires`, `evidence` and `merge` nodes produce NO run row, so the spine
marks them reached through the declared graph's `x-harness.status` and refuses to invent a run for
them — the first synthetic row beside a stopped run would be indistinguishable from a real failure.

Two mechanics worth keeping. First, the spec-fold scenario-preservation rule earned its THIRD reader
in five days: #448 named it for a rename's MODIFIED blocks, and this change found it for a
rewording's. The delta had renamed two scenarios for honesty ("A walk for a task…" → "A walk for a
PBI…", and "Active and historical runs" → "…under one stage"), and openspec REFUSES to archive a
MODIFIED block whose scenario names do not equal the current spec's verbatim — there is no
scenario-level RENAMED, so the refresh is the sync's work, names verbatim and bodies reworded, and
the abort-early no-files-changed refusal is what made catching the two renamed scenarios safe
rather than silent loss. Second, the open items were left open on purpose: 1.3 (the join against a
gone directory and a same-key second repository), 2.7 (PR and evidence references as links to the
published object and the issue), 6.1/6.2 (the narrow 390px capture), and 7.2 (a person's
end-to-end observation of a PBI in progress, one without a PR, and one closed whose worktree was
removed) are verifications that require runs in this checkout or a person's eyes, and the fresh
worktree had neither — marking them `[x]` would be the exact invention this product refuses
everywhere else, so they sit as deferred with their reason, and the gate run (18/18, 1320 tests
across seven assemblies, zero failures) is what was actually observed and recorded.

Cost note: no usage figure was pushed for any phase of this change (the runtime reported "no
session id"), so the retro carries no token count for it — a measurement lost, said out loud
rather than guessed.

## 2026-10-05 — #454 Review a published PR without a local checkout

The change that first read a published PR's checks taught a distinction a simpler model would
have collapsed: three honest answers a vendor gives about a head commit, and the product refusing
to flatten them. `noChecksDeclared` is an honest empty — a head with no check-runs and no combined
status — and it is NOT success, the way `Unreadable` (a refusal or transport failure carrying the
provider's remedy) is NOT absence. The appliance this product refuses everywhere — a synth row
beside a real one, a label re-derived into a position — has its analogue here: presenting no checks
as passing, or an access error as evidence-not-found, is exactly the invention the ReadChecks pattern
rejects. The four observed PRs held the line in practice (#463 with no checks declared, reported
honestly; #390 with five passing and nine skipped; #295 carrying a refusal with its remedio; #457
refreshing a moved head), across both themes and at the 390px the narrow design set proves.

Three mechanics worth keeping. First, the checks read reached the seam without a registration:
`GitHubPullRequestReader` already lived behind `IRemotePullRequestReader`, and adding `ReadChecks`
appended one member and one record — `World.cs` was not touched, no new process, the module boundary
held. The auto-discovery of the comparison's files, proposal and evidence reads the same pinned
comparison the diff does, so there is no second source of reviewed code once a PR exists: the
working diff is the reading before a PR, never a parallel one after. Second, the open tasks were left
open on purpose — 3.1 through 3.3 and 3.5 are browser-cover and tab-mount verifications a fresh
worktree without live runs cannot honestly mark, 5.3 is architecture documentation deferred — and
`openspec archive --yes` accepted them with a warning rather than a refusal, because the spec deltas
and the implementation that shipped are true. Marking them `[x]` would have been the invention this
change's own ReadChecks pattern exists to prevent.

Third, the spec-fold rule earned its FOURTH reader in six days, and found a new failure mode. The
delta's `review-a-change-in-one-tab` block renamed a requirement header — `A task's review entry is
not the whole detail` → `…is the published PR, not a local diff` — and openspec refused, because
there is no scenario-level RENAMED and a MODIFIED block must carry the current spec's header verbatim.
Restoring the verbatim header with the reworded body is the #448/#449/#450 pattern, and the
abort-early no-files-changed refusal is again what made it safe. The new wrinkle:
`review-a-remote-pull-request` was written as a MODIFIED delta against a spec that does not exist in
`openspec/specs/` — the proposal believed #419 had introduced it, but the archive that would have
folded it never ran, so the capability lived in a closed issue and not in the baseline. openspec
refuses MODIFIED on a missing target ("only ADDED requirements are allowed for new specs"), and the
fix is to convert the whole delta to ADDED: five requirements, one block, the new spec created on
archive. The lesson the three rewrites taught — a delta is a patch against the current spec, names
verbatim — now has a zeroth case: a delta is a patch, which means the spec it patches must be there.

Cost note: the `aio:state` comment on the issue records only the propose phase, with
`usage_unavailable: "no session id in this runtime"`; no implement or sync phase carried a session id
either, so the retro carries no token count for any phase of this change — a measurement lost, said
out loud rather than guessed.

## 2026-10-05 — #451 Follow the background work from the Loop entry

The load-bearing decision was the one the proposal named before a screen existed: the new surface
composes three reads that already ran — `usePbis`, `useLiveRuns`, `useAttention` — and writes none.
A "what is the loop doing" screen is the natural place to invent a scheduler, and the discipline
that kept it a projection is the design rule that nothing re-interprets the declared loop: the
position a row draws is the `PbiStatus` the Connector reads, the next step is `journeyFor`'s
existing projection, and the screen's own contribution is the band ordering, not a second state
machine. The deletion of `LoopFlow` in #450 was the half that closed the path; this change is the
half that proves closing it was enough — a global view of the loop's motion reached for no new
plumbing because the loop's declaration was already the only authority.

The join is the lesson that travels. A run attaches to a PBI by full identity — source plus
repository plus key, the run's `folderPath` resolved to the repository the PBI names — never by key
alone, because `#12` in two repositories is two different PBIs and a same-numbered other is exactly
the wrong row to light up. This is #431's identity rule, the one #448 wrote a regression test for,
applied here at a third surface — the inbox, the journey, and now the global board — and each new
surface is a new chance to forget it. The counter belongs to the same family: this screen's counts
run over the visible set, not the server's whole set, because a chip that reads "there are 30" while
the filter shows 19 is a count arguing with its screen, and `GET /api/pbis`'s whole-set counts are
untouched on the wire for the screens that want exactly that. "Which side holds the set the count is
over" is the #40 question, asked again, and the answer was the same one it was last time.

The four states carried the work the gates could not name. A failed read is not a calm empty result,
and the attention queue's `unknownSources` is a partial read with its own sentence — never dropped
as if the queue were empty. This is the shape #25 met as "a filter that returns nothing and a filter
that is wired to nothing are the same colour from inside a test", here written down before a screen
rendered, and the partial-read alert plus the `ErrorState` with four reachable states is what kept a
broken seam honest rather than invisible. The e2e flakiness after a live verification — an async
assertion that read a data row the instant the screen's static copy appeared — was the second
appearance of the #19/#61 shape in the same wave; green twice is what makes it tempting to call it
unrelated, and a flake found at the gate is a finding that earns its re-run.

Cost note: no usage figure was pushed for any phase of this change (the runtime reported "no
session id"), so the retro carries no token count for it — a measurement lost, said out loud
rather than guessed.

## 2026-10-05 — #455 Publish review comments and verdicts from the review

This change landed an empty implementation diff on purpose, and the honesty of that is the teaching.
The code that publishes a review was already on master — #419 shipped the two write use cases in the
Pbis module, the `IRemotePullRequestWriter` seam exposing only `SubmitReview` and `Reply`, the GitHub
adapter and the frontend publisher — and #454, reconciling the read side of
`review-a-remote-pull-request` with epic #447, rewrote that capability's delta in place and
deliberately excluded publication (its Non-Goals read "Publishing comments or a review verdict (that
is #455)"), so the three publication requirements the master change carried were dropped from a delta
that never reached baseline. The code ran; its spec had no home. This change is the spec for that
code, refined to where #447 placed publication — the Review tab of the PBI and the direct external-PR
entry, both composed from the shared `PullRequestReview` component, not the standalone screen the
original #419 spec described — and the archive folds three ADDED requirements into
`openspec/specs/review-a-remote-pull-request/` so the capability reaches baseline whole.

What this change actually contributed was live verification against a real PR, and that is where the
empty diff paid for itself. The implement's gate counted 18/18 steps green but the suite does not
exercise the vendor, so the behaviours a spec names but no fixture touches — that an `Uncertain`
outcome (a 5xx after a potentially accepted write) retains the draft and does NOT resend, that
`Confirmed`/`Refused`/`Uncertain`/`Outdated` render as four states, that the publisher holds its
narrow width under both light and dark themes, and that a verdict and a thread reply both travel the
seam's two methods and nothing else — were confirmed by publishing a comment, a reply and a verdict
against PR #457 (head 86099c2c) in the running product and reading what the vendor and the UI did.
Every bug this project has found in committed code was found by running the product; this change
found none, because there was none to find, and that is a finding the empty diff recorded in the spec
rather than the code. The boundary the spec states verbatim — publication SHALL NOT merge a PR,
bypass policies, remove a Hold or change issue lifecycle status — is the contract the seam already
freezes at two methods, and is the line between this change and #456.

The spec-fold scenario-preservation rule earned its fourth reader in a week — #448 named it for a
rename's MODIFIED blocks, #449 and #454 found it for rewordings and renamings, and this change found
its cleanest case: an ADDED delta carries no renamed or modified blocks, so `openspec archive`
refreshed the spec without the abort-and-restore dance the renamed-scenario changes forced. The
delta-to-baseline fold ran first try, three requirements added in place, and that is the shape the
rule was written for.

Cost note: no usage figure was pushed for any phase of this change — `push-session-report` wrote
only the propose phase with `usage_unavailable: "no session id in this runtime"` — so the retro
carries no token count for it. A measurement lost, said out loud rather than guessed.

## 2026-10-05 — #452 Navigate PBIs from folders and read their history

The rail stopped listing branches and started listing PBIs, and the load-bearing choice was that
this was a move of destination, not a new read. A folder's active PBIs are the global inbox reduced
to the folder by `folderFor` matching the PBI's `origin` against the folder's remotes — the same
reduction `OverviewTab`'s `usePbisHere` already performed, the same reduction the design's "no
second source of the same PBIs" rule demands. The server holds no folder filter (a `pbi-inbox`
requirement, not a gap), so asking for one would have duplicated the inbox's identity resolution and
its counts; the rail is a view of the read the inbox draws, in vendor order, with the same vendor
fields. A row does not depend on a worktree existing, which is the point: a PBI is the work, a
worktree is one run of it, and the rail was listing runs under the wrong name.

History's group key was the decision that would have been easy to get wrong, and #450's join is why
it wasn't. `useClosedJourneys` grouped runs by `run.key` alone — local to one folder's checkout — so
two PBIs sharing a remote key collapsed into one and a journey closed in another folder was
invisible. The global History reuses `PbiWalk`'s full-identity join (`run.key === task.key && run.folderPath === repo.path`), the contract #450 established behind `read-a-run`, so a closed
journey stays reachable after its worktree is removed and two PBIs sharing a remote key keep separate
histories. This is #431's identity rule at a fourth surface — the inbox, the journey, the loop board,
and now the closed-history list — and each new surface is a new chance to forget it; the discipline
is that the join lives in one place and every reader points at it, never re-deriving the formula.

The old `?worktree=` link resolving holds a small honesty. A folder route carrying `?worktree={path}`
opens the PBI's voyage only when that path identifies one PBI by full identity — unambiguous, never
selecting another PBI that shares a key or path. The ambiguous case explains rather than reselects,
and a worktree link with no PBI match stays on the folder, and one naming a gone worktree stays on
the checkout. The retail "explain rather than stay" the proposal carried was amended during the
work: the unambiguous case opens the voyage and the rest stay, leaving the rail and the inbox as the
way a person reaches a voyage from an ambiguous link. A same-key other is exactly the wrong PBI to
open, and the rule is the same one that keeps histories separate.

The Change tab in the folder's detail was the change that didn't survive the work. It moved the
`Change` tab to the PBI's voyage, and the move broke what the entry-budget gate exists to catch.
That tab's draw pulled the voyage's screens into the entry chunk — the very collision the gate is
for — and the gate-breaker was reverted, leaving Overview and Files in the folder's context and the
folder's `Change` reaching the voyage through the rail row and the inbox, the two paths that already
opened it. A gate that turns red on a real regression is a gate that earned its colour; a tab kept
only to satisfy it would be the kind of code a gate taught people to write past.

The entry-budget gate caught a real inter-branch collision, and that is the change's main teaching.
#451's Loop entry and this change's PBI rows and History pushed the entry chunk to 200.53 kB gzip,
53 bytes over the 200 kB budget `vite.config.ts` declares — not a future risk, a present red.
`LoopScreen` and `HistoryScreen` paint nothing until somebody navigates to them, so they are not the
first screen's bytes to spend; moving both behind `lazy()` boundaries — the same pattern
`PullRequestReview` already used — brought the entry to 198.28 kB, within budget. The collision was
between two branches landing the same wave, neither of which was wrong alone: #451 added a
legitimate `/loop` entry, and #452 added legitimate PBI rows and a History entry, and the gate named
the combined cost the way no review of either branch in isolation could have. A budget enforced in
the build is a budget two parallel tracks cannot each quietly exceed, and the lazy boundary is the
discipline that keeps a screen's first paint honest about what it actually paints.

The spec-fold scenario-preservation rule earned its fourth reader in this change. The delta had
renamed two scenarios for honesty — "History is a screen, not a redirect" replacing "A bookmark to
the old history route lands on the inbox", and "History is reachable from the rail" replacing
"Settings is reachable from the rail" — and openspec refuses to archive a MODIFIED block whose
scenario names do not equal the current spec's verbatim. There is no scenario-level RENAMED, so the
refresh is the sync's work: the history-route bookmark kept its header verbatim with its body
reworded to describe the screen rather than the redirect, and "History is reachable from the rail"
was added beside "Settings is reachable from the rail" rather than replacing it. The abort-early,
no-files-changed refusal is what made catching the two renamed scenarios safe rather than silent
loss — a fourth appearance in five days, which is what "a rule that earns its keep" looks like.

The open items were left open on purpose, the same shape as #450. The narrow capture (8.2), the
gate's own celled observation in a fresh worktree (9.1), and the two-folder same-key navigation
(9.2) are verifications that require runs in this checkout or a person's eyes, and the fresh
worktree had neither. Marking them `[x]` would be the exact invention this product refuses
everywhere else — the gate run is what was actually observed, and the rest sit as deferred with
their reason, said out loud.

Cost note: no usage figure was pushed for any phase of this change — `push-session-report` wrote
only the propose phase with `usage_unavailable: "no session id in this runtime"` — so the retro
carries no token count for it. A measurement lost, said out loud rather than guessed.

## 2026-10-05 — #453 Operate sessions independent of environment and view

The session was welded to its worktree and the worktree to the screen that opened it, and this change
severed both welds at the seam. Two interfaces landed in `BuildingBlocks/World` — `ISession`
answering what the interaction is, `IEnvironment` answering where it executes — and the choice to
make them two and not one is the teaching the diff cannot re-derive. Session abstracts the
interaction and changes when the agent's interaction shape changes; Environment abstracts the place
and changes when a Docker or Kata or ACA backing arrives. The ACA precedent
(`RemoteSessionQuery(Environment, SessionId)`) had already separated them on the remote side; this
change made the local side its mirror, and a single seam would have folded two reasons to change into
one interface and paid for it the first time a container backing landed. Two seams, two change axes.

`RunLauncher` now drives the agent through `ISession` rather than through `IAgentRunner`/`ITerminals`
directly, and `sessionId == runId` is the identity rule the seam keeps — one run is one session,
and the seam does not mint a second id space beside the run's. The local PTY (`PtyAgentRunner`,
`ProcessCommandRunner`) becomes the first implementation wrapped behind the seam, never the
contract; replacing the backing does not change the contract, which is the property the ACA precedent
already proved in the other direction. `StartRun` resolves the environment through
`IEnvironment.Resolve` and refuses an unavailable one with `RunErrors.EnvironmentRefused` rather
than simulating it as the default — an environment that is not real is refused with a sentence the
caller can show, never offered as if it worked. The seams register as singletons at the end of this
change's block in `World.cs` (lines 355–356), appending and not reordering, the AGENTS collision rule
held.

`worktree-lifecycle` dropped from a first-class, UI-backed capability to the launcher's internal
mechanics, and the removal was honest rather than silent. `POST /api/folders/{id}/worktrees`
(`CreateWorktree.cs`) and `DELETE /api/folders/{id}/worktrees/{worktreeId}`
(`RemoveWorktree.cs`) were removed from the HTTP surface, the folder dialog that called them went,
and the spec was a full REMOVED delta — all four baseline requirements gone. `IWorktreeManager`
stays as the seam the launcher calls internally to create, reuse and retire a worktree, and the
journey a worktree held survives its removal in the run's transcript, commits and pull request; the
worktree is the launcher's, not a navigation destination a screen carries, and the `?worktree=`
parameter went with the dialog. Git is still the record; the rows still follow it. The launcher's
ownership is documented under `choose-an-environment`, where it belongs, not revived as a public
verb.

The rebase onto #452 brought the collision the AGENTS shared-file rule predicts, and the resolution
is the rule's clean case. `contracts.ts` carried #452's `RailPbiRow` block and this change's
`SessionKind`/`LiveSession` block, and both blocks survived the merge by appending next to their
own screen's block rather than reformatting a neighbour's — the same rule `en.ts` (grouped by
screen, not alphabetical) and `queries.ts` hold. A two-block conflict resolved by keeping both
sides' appended blocks is the shape the rule was written for, and a branch that lands without
touching a neighbour's block is a branch that lands without a whole-file conflict.

The archive ran into the spec-fold rule twice, and the second is the structural finding this change
leaves behind. `open-a-terminal`'s MODIFIED delta had replaced the baseline scenario "The attention
chip counts the machine" with "The tray counts the machine, each session once" — a rename in
disguise — and `openspec archive` aborted with the precise sentence naming the dropped scenario;
restoring the baseline header verbatim beside the new scenario (add rather than replace, the same
shape #452 used) let the fold proceed. But `worktree-lifecycle`'s full REMOVED delta — all four
requirements gone — is the case openspec 1.6.0 does not handle: it refuses an empty spec with
"Spec must have at least one requirement," and there is no scenario-level RENAMED to repair. The
resolution was `openspec archive --skip-specs` (move the change to archive/ verbatim, no spec
mutation) and the delta applied by hand: the two ADDED capabilities created in `openspec/specs/`
(`run-a-session`, `choose-an-environment`), the six MODIFIED requirement blocks replaced in place,
and `worktree-lifecycle`'s directory removed entirely — the same result `review-evidence`'s
`review-steps-per-worktree` removal produced earlier. A full-capability REMOVED is the one delta shape
1.6.0 does not fold automatically, and the workaround is documented here so the next full removal
does not re-derive it. `openspec validate --specs --strict` passes 75 (74 prior, minus
`worktree-lifecycle`, plus two new), and that count is the fold's receipt.

The gate taught the stale-static-web-assets lesson a second time. `aspire do ci` failed red on the
first run with eleven `StaticWebAssets.Compression.targets` errors — assets with old vite content
hashes (`PbiSpine-rVbbP1DU.js`) the obj manifest referenced while `wwwroot/assets/` now held the
new hashes (`PbiSpine-BKT8Dp01.js`) the vite rebuild had just produced. All .NET assemblies had
compiled; the desync was between the vite output and the .NET static-web-assets manifest, a stale
`obj/` from a prior build. Clearing `AiOrchestrator.Server/obj` and re-running brought the gate
green, 18/18 steps, and the source tree was unchanged by the clear — only gitignored build
artifacts. A gate that fails on a stale manifest rather than on a code defect is a gate that teaches
the worktree to start clean when its intermediates outlive their inputs, and the lesson is the same
one #452's entry-budget collision taught: the build is the honest reader, and a transient red is a
finding kept rather than a verdict ignored. The mutation step recorded "not measured" for three
projects (`Stryker exited 1`), recorded as not-measured rather than failed, per the gate's own rule.

The global session tray is not built, and that is the change's honest scope line. The tray's
placement was validated with a person during the work (task 7.1) rather than guessed in the spec, and
the tray itself (tasks 6.2, 6.3, 6.4, 6.5, 6.7) and its visual review (10.3) are left open with their
reasons said out loud — the same shape #450 and #452 used for open items that require a run or a
person's eyes this checkout did not have. `openspec status --change --json` reports `isComplete:
true` against the planning artifacts, and the eight unchecked task boxes are the tray and its
review, deferred rather than invented. A future change builds the tray against the `ISession` seam
this change lands; marking those tasks done here would be the exact invention this product refuses
everywhere else.

Cost note: no usage figure was pushed for any phase of this change — `push-session-report` wrote
only the propose phase with `usage_unavailable: "no session id in this runtime"` — so the retro
carries no token count for it. A measurement lost, said out loud rather than guessed.

## 2026-10-05 — #456 Execute the merge as a loop step (PR #469)

The merge capability already existed on master — the seam (`MergeGrantAdmission`,
`MergeChange`), the journal (`MergeEffectId.Derive`) and the loop's `merge` node shipped in #419
and #448 — and this change did not rewrite any of them. It closed three gaps the loop exposed
without touching the mechanics that already worked, and that restraint is the first thing the
diff teaches: extending an admission's typed answers and adding a second entry to an existing use
case is smaller than re-plumbing the operation, and a change that keeps the seam and the journal
untouched is a change that costs less to land and less to reason about after. `MergeGrantAdmission`
answers one question with five typed refusals now rather than four — `Missing`,
`CandidateChanged`, `HeldByPbi` and the two the admission already had — and the handler's switch
stays the single place sentences are written, beside a refusal that names its remedy.

`HeldByPbi` closes the criterion this product holds that the server validates holds, not just the
loop. The loop's `hold` node already refused to start when an issue carried `status:holding`;
the merge use case did not, so a person could merge a PR whose issue was held between approval and
admission. The refusal reads the hold from the vendor through `IPbiSource.GetPosition` — never
from the snapshot, which a write empties — so a hold applied after the PR's approval is caught at
the admission, not inferred from a stale cache. A position read that fails is refused as the
vendor's own words rather than read as not-held, on the same terms an unknown grant is: merging
past an unknown position is what the admission exists to stop. The hold is orthogonal to the
grant registry's adoption, and a folder that never adopted the registry still reads the hold —
the pre-adoption no-op grant does not bypass it.

The journey's `done` step replaces the worktree's merge dialog as the place the merge is offered.
`PbiSpine`'s `done` step presents the merge as its action — the same operation the loop's `merge`
node and `/aio:sync` run — addressed by the PBI's identity (source, repository, key) and the
folder, with the worktree as the launcher's resolved detail. A PBI-keyed route
(`POST /api/folders/{id}/merge-by-pbi`) sits beside the folder-scoped `WorktreeId`-keyed route
the worktree screen keeps; the step does not launch the folder-scoped route with a worktree it
had to invent. `MergeEffectId.Derive` derives from the same canonical identity the admission
resolved, so a merge launched by identity and one by worktree journal as one effect — the identity
of the effect is the same for both entries, and a retry from either resumes rather than repeats.
The worktree's `ChangeTab` keeps its merge control; this change composes the same
confirmed-merged renderer into the step rather than moving it — one affordance per surface, both
backed by the one operation.

This is the last change of a wave of nine (#448–#456), and the wave is the finding this entry
leaves behind. Spec-Driven Development against capability specs, parallelised by issue and branch
behind a WIP gate of four, ran nine issues to master without a collision that stopped a merge.
The entry-budget gate in `ci-build-frontend` caught the one inter-branch overlap that mattered —
#451's folder-scoped PBI block against #452's, both appending to `contracts.ts` — and the
deterministic `cleanup` script removed each worktree, branch and claim record the moment its merge
landed, so no orphan survived into the next issue. The shape that recurred is the spec-fold
friction: `openspec archive`'s MODIFIED delta treats a renamed baseline scenario as a dropped
scenario and aborts, and the repair — restoring the baseline header verbatim beside the new
scenario, add rather than replace — was re-derived in five of the nine issues (#448, #449, #450,
#453, #454). A scenario-level RENAMED, or an archive flag that relaxes the dropped-scenario check
to a warning with the candidate rename offered, is the openspec friction this wave surfaces; it is
not a defect of this change, and it is the one structural improvement the wave's retro would name.
`openspec validate --specs --strict` passes 75 after this change — the count is the fold's
receipt — and this change's archive folded cleanly: three ADDED, one MODIFIED, zero renamed.

The gate ran green on the rebased branch, 18/18 steps, 1336 tests across seven assemblies (zero
failed): `ArchitectureTests` 18, `Workspace.FunctionalTests` 189, `Pbis.FunctionalTests` 297,
`Server.FunctionalTests` 53, `Runs.FunctionalTests` 123, `EndToEndTests` 45,
`InfrastructureTests` 611. A smaller count is a subset, not a success — the lesson of 15 August,
held. The mutation step recorded "not measured" for `AiOrchestrator.Modules.Workspace`
(`Stryker exited 1`, a path-config fault in its temp copy), recorded as not-measured rather than
failed, per the gate's own rule. Coverage stands at 64.5% line, 55.6% branch, 70.9% method, with
207 methods above CRAP 30 — none of them in files this change touches, the `crap` tripwire's
threshold for asking a person.

Cost note: no usage figure was pushed for any phase of this change — the runtime this subagent
runs in did not surface a session id — so the retro carries no token count for it. A measurement
lost, said out loud rather than guessed.

## 2026-10-05 — #444 Update from the shell's settings, the Orbion model (PR #445)

The worktree this issue rode arrived with its work already half done and not committed: 1143 lines
of implementation — the channel, `Updates`, the settings section, the six endpoint tests — on the
disk of a fresh `change/444` worktree, with a `Assert.Fail("DEBUG: …")` left inside one test. The
loop's `prepare` refused to start on it, as designed, and the review found the two real faults the
uncommitted state was covering for: the test's channel stub registered its listeners on `external`
but read them back off `receiveMessage` — a write and a read at two different sites, so no reply
ever arrived and the whole stub was being debugged against its own bug; and the section component
drew the `error` state out of a parsed reply but not out of a channel rejection, so a page newer
than its shell — the spec's own unknown-method scenario — would have shown a promise that never
resolves as a blank. Both were fixed before the gate, and the 6/6 suite is the record that the
spec's "the page's promise rejects with that sentence" is a sentence the screen shows, not one it
swallows. The lesson the change names for the next issue: a worktree is a surface where another
run's half-finished work can land, and the review a fresh `implement` does of that state is the
guard that state had — there was no rule for where uncommitted work should not sit, and the loop's
prepare-stop is the only one there is. The entry budget stays what the gate made it on this exact
branch: `aspire do ci` 18/18 on `origin/master` after the rebase, the same command on the same
machine that lands the merge.

The change's one finding rides out open on its own: the updates section sits under the 1440 fold
of the settings screen — the published capture is the record of it — so the 1440 set photographs
the screen as it is and the section's own coverage is the channel's six tests against a stubbed
pipe. A capture that tallies the section is its own change, with the picture this batch left
behind. The install-real half (tasks 4.1–4.3: a real release, a real restart, `/api/version`
saying the new number) was not verifiable on this machine and is named as not verified wherever
the check reads it; the gate's number is the number the gate measured.

No usage figures were pushed for any phase — this runtime surfaced no session id — said out loud
rather than guessed.

## 2026-10-07 — #471 Llevar a master las pantallas del rediseño PBI (PR #472)

The epic #447 closed with nine merged changes and none of the screens its own body pictured. Each
sub-issue fenced the visual work off as another's — #450 "no pide tabs", #454 "el contenedor lo
aporta #450", #452 left the worktree controls to #453, and #453 archived its tray as "Deferred:
the tray is not built" — and the tasks that would have caught it, the visual reviews in the
product, were the ones each change archived unticked. The prototype that carried the pictures
(PR #457) was cited everywhere as "reference, not contract" and never merged. The lesson: a split
that hands a container to one issue and its contents to another needs one of them to own the
pairing, and an archive with open verification tasks is the signal that the pairing was dropped.

What worked: comparing the issue's screenshots with the running product, side by side, found in
ten minutes what nine green gates had not. Building on what existed — `usePbi`, `PbiSpine`,
`PullRequestReview` opened on a step, the sessions store — kept the change in the frontend; the one
join the wire lacked, an issue's PR, came from the inbox by GitHub's own closing keywords. The rail
was settled on a design canvas mid-run (variant A of four), with the person choosing between
drawn options rather than reading a proposal.

What did not: the first `Create worktree` this change removed had been wired to a no-op since
#453 — a control that did nothing for two days without a test noticing. A menu inside the rail's
scrolling list was clipped until the kit gained a `floating` placement. The new end-to-end test
failed first on the review fixture's phone width, where the rail is not drawn.

Next time: a sub-issue whose scope says "no pide X (eso es #N)" gets a check that #N says it does.
`worktree-is-a-loop` still describes the worktree this change redefined; folding it is its own change.

`aspire do ci` 18/18, 1347 tests in 7 assemblies, 0 failed, on `e2dd0d95`; evidence 26 images in
issuecomment-6045431424. One session, 52 minutes, about $55.69 estimated, no friction raised.

## 2026-10-07 — #462 The release tags the merge's own commit

Two releases in one afternoon landed on the wrong commit: v0.2.14 on the master before #472, and
v0.2.15 one commit short of its own bump even after master was brought up to date by hand — the
squash does not exist locally until it is fetched, and the version came from the worktree while
the commit came from master. The lesson is the one the scripts' own headers already stated for
names and did not apply to commits: whatever a step acts on is named by the step that made it, not
looked up afterwards. `merge` now passes the squash the vendor reports; `release` reads the
version out of that commit and refuses to pack a checkout that is not it. This entry's own release
is the check — v0.2.16 should sit on this change's squash. A correction rides with it: the #462
comment said v0.2.14's assets lacked #471; they were packed in #471's worktree and carry it — only
the tag was misplaced.

## 2026-10-07 — #475 The installed app owns its environment

The first install of v0.2.16 opened on an empty inbox. The installer launched the app under `sudo`,
the app asked `$SHELL` — `/bin/sh` — for the PATH, and the server ran without Homebrew: no `gh`, no
agents. The code already knew a GUI process inherits a thin PATH and had a whole recovery for it;
what it trusted was the one variable that recovery is meant to distrust, whoever the parent is. The
user database names the account's shell regardless of who started the process, and that is what
both the server and the Shell now read. Quitting then left the server alive and the next window
adopted it, carrying the bad environment forward: a server is now owned only while a window of the
install runs, and an orphan is replaced. Both were found the only way this repository finds things —
installing the build and looking — and were invisible to every test, because the tests start the
server themselves, with the developer's own environment.

## 2026-10-08 — #493 The loop's prompts, audited

*Agent-written, not confirmed by a person.* The prompt-audit method — one reviewer per prompt, a
checklist, findings with severity, a fix pass and a second pass — run over `/aio:*` and its skills
found 13 defects, and most of them were one defect seen from many sides: `/aio:ship` removes the
people from a loop whose commands and skills still ask them things. Sync's approval, the retro's
confirmation, the minor bump, a rebase conflict, an archive that confirms first — each is a place an
unattended run either guesses or stops without saying why. The fix is one rule in ship rather than
seven patches, and it is now a requirement of `land-a-change`. The rest were rules the agent had
followed on 7 October on its own initiative because the text did not ask: rerunning the gate on the
close-out commit, evidencing a screen `capture.sh` does not shoot. The five parallel reviewers hit
the session limit before writing a line; the audit was finished serially, and the second pass is the
grep in the change's task 2.1.

## 2026-10-10 — #575 The allow list and the commands are two lists nothing compares

*Agent-written, not confirmed by a person.* The issue asked for one more comment. The proposal found
a second problem the issue did not mention: since #582 made `allow-list` the default permission
mode, `gh issue comment` was not on `.claude/settings.json`'s allow list. So the two comments
`refine.md` already asked for would have stopped an unattended run at a prompt nobody could
answer. The suite missed this because it tests the command texts and the allow list separately.
Nothing checks that each `gh` call a command tells the agent to make is one the list permits. The
new test pins a single entry, `Bash(gh issue comment:*)`, so the gap is closed for that call only.
Two more of the same are still open after this change: `refine.md` edits the body with
`gh issue edit`, and `propose.md` reads review comments with `gh api repos/…`, and neither is on the
list. The check that would have caught all three takes the `` `gh <noun> <verb>`` calls out of
`.ai/commands` and the skills and asserts that each one is either allowed or deliberately absent.

## 2026-10-15 — #758 A check narrower than the issue's, and a version bump that pins two tests

*Agent-written, written down by the orchestrator: Claude Code's auto mode denied the retro run its
commit and push, and the run waited at a prompt nobody could answer.* Two lessons. First, the plan's
check excluded more than the issue's: task 5.7's search skipped `openspec/changes`, where spec
deltas live, and archiving copies their text into `openspec/specs`. So "no `.harness/tripwires/`"
and "no `TRIPWIRE_*`" would have put the word back on merge; the narrower search passed and the
review caught it. A plan never excludes more than the issue's check does, and a delta that bans a
name must not spell it. Second, every package version bump breaks `HarnessPackageTests` and
`AppTests`, which both pin the version. The implement ran only the tests of the projects it touched
and missed both; the gate caught them two rounds later. A plan that raises the version names those
two tests next to it.

## 2026-10-15 — #757 Sharing a file across checkouts makes every write to it a race, not only the slow one

*Agent-written, written down by the orchestrator: Claude Code's auto mode denied the retro run its
commit and push (#765).* The plan moved laya's venv, pidfile and log from each checkout to
`~/.aiorchestrator/laya/<pin>/` and put a `mkdir` lock around the build, because the build was the
step that took minutes and could clearly collide. The implement found that it was not the only step
that could collide: the start could too. Before the move, every checkout had its own pidfile, so two
`ensure`s starting at once could not overwrite each other's pid. After the move there is one pidfile
per machine; two starts race for port 8317, and the loser writes a pid that has already exited over
the winner's, which leaves a healthy server that `stop` cannot find. The plan's lock left the start
outside it. Writing `LayaScriptTests`' "two ensures at once" case found the gap, and the design now
says so. When per-checkout state becomes per-machine state, every write to it has to be checked for
a second writer; the build only looked like the risky step because it was the expensive one.

## 2026-10-15 — #759 A version number in a spec delta is a pin no test reads

*Agent-written, not confirmed by a person.* The proposal raised the package from 34 to 35 and wrote
35 into the change: the `harness-package` delta's *Reading the package* scenario, the proposal, and
tasks 2.4 and 4.6. Meanwhile #765 merged to master with its own bump to 35. Resolving the conflict
moved the branch to 36 in every place a test reads — `.harness/package/version`,
`HarnessPackageTests` and `AppTests`, the two #758 named — and the gate went green over a spec delta
that still said 35. Nothing failed because no test compares the number in an OpenSpec scenario with
`.harness/package/version`; only the diff review caught it, and without that review `merging` would
have archived into `openspec/specs/harness-package/` a version the code does not ship. So a version
bump pins three places, not two, and the third is prose. A number chosen at propose time is also a
guess about what master will hold at merge time, and with several changes in flight at once that guess
goes stale. Resolving a version conflict means a `grep` for the old number across
`openspec/changes/<name>/` as well as the tests.

## 2026-10-16 — #760 A lesson in the retro log does not reach the next plan, because no command reads the log

*Agent-written, not confirmed by a person.* The #758 entry, written the day before, says two
things: a plan never excludes more from the issue's check than the issue does, and a delta that bans
a name must not spell it. #760's proposal did both again. Task 5.2 copied the issue's `git grep` and
added `':!openspec/changes/move-repo-plumbing-to-harness-repo'`, and the `harness-package` and
`harness-scripts` deltas said "no `.harness/archive`, no `.harness/release` …" to assert that the
scripts were gone. The narrower grep passed, and the archive would have copied the old paths into
`openspec/specs/`, where the issue's own grep would have found them after the merge. This time the
implement caught it (08197e3) by archiving in a scratch worktree and running the issue's grep, not
the task's. The review then caught it again the same way. Nothing in `propose.md`, `implement.md`
or `.harness/context.md` points at `docs/process/retro-log.md`, so an entry only reaches the next
change when whoever runs the next change happens to remember it. The log records lessons but does
not feed them back. While that holds, the same mistake will be repeated and logged again. Second,
the plan missed a case that a person raised at proposal review: the change moves `archive`, which
`merging` runs, into a folder that only the new engine searches. The installed 0.3.10 therefore
cannot merge the PR that teaches it the folder. A change that moves what a state runs ships in two
releases, or its own merge is done by hand.
