# harness-scripts Specification

## Purpose
What a command can decide, a model does not: the claim, the gate, the archive and the merge are scripts
in `.aio/`, each answering one JSON line, so a step behaves the same in the loop and by
hand. Two of them are scores, whose answer is a model's opinion routed by code. Reverse-engineered from
the shipped code (`.aio/steps/`, `src/modules/Loop/AiOrchestrator.Modules.Loop/Engine/ScriptVerdict.cs`),
not negotiated ahead of it. #729 put the gate and the repository's own scripts at the root of
`.aio/` (then `.harness/`) and the package's in a folder of its own, and made the engine find a script by its
name; #779 renamed the folder to `.aio/` and called the package's `steps/`.
## Requirements
### Requirement: The engine finds a script by its name
A script named in `loop.yaml` (`script: gate`, `script: [archive, merge]`) SHALL be a name, never a
path, and the engine SHALL run the first of `.aio/<name>`, `.aio/steps/<name>`, `.harness/<name>`,
`.harness/repo/<name>`, `.harness/aio/<name>` and `.harness/scripts/<name>` that exists in the checkout
(`HarnessScripts.Find`), so the repository's own script, at the root of `.aio/`, wins over the package's
(#760, #779). The `.harness/` folders are the layouts before harness 37 and before harness 24: a repository that has not
reinstalled yet SHALL keep running until it does. A name with a slash or a leading dot SHALL fail
without running anything, and a name no folder has SHALL fail saying so. `prepare` and `cleanup`
SHALL be found the same way. `laya` is a tool, not a step: it SHALL be found first at
`.aio/scorers/laya/laya`, then at `.harness/aio/scorers/laya/laya`, and only where neither is, in the folders any script is
found in, so a repository that has not installed harness 36 still has its `.harness/aio/laya`
found until the install moves it (#759). An agent's PATH SHALL start with the checkout's
`.aio`, then `.harness/aio` and `.harness/scripts`, so a bare `aio` is the checkout's in any layout.
The repository's own `loop.yaml` and `version` SHALL be read from `.aio/`, or from `.harness/` where
`.aio/` has none (`HarnessScripts.Own`).

#### Scenario: The gate and the claim
- **WHEN** the engine runs `script: gate` and `script: claim` in a checkout of the new layout
- **THEN** it runs `.aio/gate` and `.aio/steps/claim`

#### Scenario: A repository with harness 36
- **WHEN** a checkout has `.harness/gate`, `.harness/aio/claim` and `.harness/repo/archive` and no `.aio/`
- **THEN** `script: gate`, `script: claim` and `script: archive` run those files

#### Scenario: A repository that has not updated since harness 24
- **WHEN** a checkout has its scripts only in `.harness/scripts/`
- **THEN** `script: gate` runs `.harness/scripts/gate`

#### Scenario: laya in its folder
- **WHEN** a checkout has `.aio/scorers/laya/laya`
- **THEN** `HarnessScripts.Find(checkout, "laya")` answers that file

#### Scenario: laya left beside the steps
- **WHEN** a checkout has `.harness/aio/laya` and no `.aio/scorers/laya/laya`
- **THEN** `HarnessScripts.Find(checkout, "laya")` answers `.harness/aio/laya`

#### Scenario: laya in both places
- **WHEN** a checkout has `.harness/aio/laya` and `.aio/scorers/laya/laya`
- **THEN** `HarnessScripts.Find(checkout, "laya")` answers `.aio/scorers/laya/laya`

#### Scenario: A script of the repository's own
- **WHEN** a checkout has `.aio/archive`
- **THEN** `HarnessScripts.Find(checkout, "archive")` answers `.aio/archive`, and `script: archive` runs it

#### Scenario: The root before steps/
- **WHEN** a checkout has both `.aio/gate` and `.aio/steps/gate`
- **THEN** `HarnessScripts.Find(checkout, "gate")` answers `.aio/gate`

### Requirement: A script answers one JSON line, and the engine reads only that
Every script the loop runs SHALL print its answer as the last line of its output, a JSON object. The
engine SHALL read that line the same way for every script, by no script's name (#593): a non-empty
string `outcome` SHALL be the state's outcome whatever the exit code; otherwise `wait: true` SHALL be a
wait; otherwise exit 0 SHALL pass and any other exit SHALL fail, holding the PBI. `why` SHALL be the
reason in each case. A last line that is not a JSON object SHALL fail the script whatever its exit code;
a terminal's colour codes and other control characters in it SHALL be left out before it is read, so a
gate that quotes a coloured line of its log in its `why` still answered its line (#710).
Any other field SHALL be for a person reading the line, and the engine SHALL NOT read it. `gate` SHALL
answer `outcome: green` or `red`; `merge` SHALL answer `outcome: merged`, or `conflict` when gh says the
pull request is not mergeable or conflicts, and no outcome on any other failure; `file-overlap` SHALL
answer `wait: true` on a wait and exit 1 on a hold; `risk-score` and `diff-risk` SHALL answer
`outcome: low` or `high`; `evidence` SHALL answer `outcome: passed`, `skipped`, `blocked` or `failed`,
exiting 0 on the first two and 1 on the others, its `why` saying what it published or why it published
nothing. The fields they wrote before (`go`, `clear`, `hold`, `band`) SHALL stay.

#### Scenario: A script that prints prose last
- **WHEN** a script exits 0 and its last line is not JSON
- **THEN** the engine holds the PBI saying the script failed

#### Scenario: A script of a loop of its own decides an outcome
- **WHEN** a script no loop of ours names answers `{"outcome": "shipped", "why": "it went out"}`
- **THEN** the engine takes `shipped` through the state's `on`, as it takes the gate's `green`

#### Scenario: A red gate exits 1 and is still an outcome
- **WHEN** `gate` answers `{"outcome": "red", "go": false, "why": "..."}` and exits 1
- **THEN** the outcome is `red`, and the PBI holds with the reason the `red` transition carries

#### Scenario: A red gate quotes a coloured line of its log
- **WHEN** `gate` exits 1 answering `{"outcome": "red", …}` whose `why` carries the escape codes of
  `aspire do ci`'s colours
- **THEN** the outcome is `red` with that `why` without the codes, and no "without its JSON line"

### Requirement: Evidence shows the screens the change touched
`evidence <issue>` SHALL read what to capture from `evidence` in the repository's own `.aio/config.json`, a JSON
object whose whole lines starting with `//` are comments (#709, #779). It SHALL answer `{outcome, published, comment, why}`
(#598, #733), where `outcome` is `passed` when it published; `skipped` when nothing asked for captures:
no config file, no `evidence` key, no `paths`, or no changed file inside them; `blocked` when captures were asked for and could not
be taken: the config is not JSON or its `evidence` names neither `start` nor `capture`, the app or a shot did not start,
the capture did not start (a spawn error, a signal, or the shell's 126 or 127), it left no PNG, or gh
did not attach; and `failed` when the repository's capture ran and exited non-zero, which is its tests
failing. Blocked never counts as skipped. It SHALL exit 0 on `passed` and `skipped` and 1 on `blocked`
and `failed`. Without the file or the key, or with no `paths`, it SHALL capture nothing and say so. `paths` SHALL be the globs (`*`, `**`, `?`) whose change against the pull request's
base (`origin/HEAD` without one) asks for captures; with no changed file inside them it SHALL capture
nothing. Then it SHALL either:

- with `start` and `routes`, start the app, `{"aspire": "<apphost.csproj>", "resource": "<name>"}` with
  `aspire start --isolated`, `aspire wait` and the resource's http URL from `aspire describe`, or
  `{"command": "...", "url": "..."}` in its own process group until the URL answers, at most two
  minutes; run `npx playwright install chromium`, shoot each route, with the word `tomorrow` replaced by
  tomorrow's date, with `npx playwright screenshot` in light and dark at 1440×900 and 390×844, and stop
  the app before publishing; each image's alt text SHALL be `<route> <width>x<height> <theme>`;
- or, with `capture`, `{"command": "...", "screens": "<folder>"}`, empty the folder, run the command,
  and read each PNG beside its JSON sidecar `{screen, module, viewport, theme}`, publishing the screens
  whose module lives in a `features/` folder the change touched, and every screen when the change is
  outside those folders or in one that holds no screen; each alt text SHALL be
  `<screen> <width>x<height> <theme>` from its sidecar.

It SHALL publish the captures as one `gh issue comment` with one `--attach` each, at most 50, opening
with `<!-- aio:evidence <head sha> -->`. Its `why` SHALL say what it published, or that the file is
missing or names no paths, that nothing in the paths changed, that the start, a shot or the capture
failed and with what, or that gh could not attach and what gh said.

This repository's own `.aio/config.json` SHALL name, under `evidence`, `src/frontend/**` and a `capture` that runs
`pnpm install --frozen-lockfile` when `src/frontend/node_modules` is missing, then the
`ScreenCaptureTests` end-to-end class, which reads the screens from the route table (`app/screens.tsx`
and the PBI's tabs) rather than from a list of its own and shoots each one in both themes at 1440×900
and 390×844 into the ignored `src/artifacts/design/screens/`, each PNG with its sidecar.

#### Scenario: No configuration
- **WHEN** a repository has no `.aio/config.json`, or has the package's example with every `evidence` line commented
- **THEN** `evidence` exits 0 with `outcome: skipped`, `published: false` and a why that says the file is missing or names no paths, and runs nothing

#### Scenario: A change outside the paths
- **WHEN** `paths` is `["frontend/**"]` and the diff touches only `Server/Program.cs`
- **THEN** nothing is started, the outcome is `skipped` with exit 0, and the why says nothing in `frontend/**` changed

#### Scenario: Another repository's app under Aspire
- **WHEN** `start` names an AppHost and its `webfrontend`, `routes` are `/` and `/?day=tomorrow`, and the diff touches `frontend/`
- **THEN** the AppHost starts isolated, the eight captures are shot against the resource's http URL with tomorrow's date in the second route, the AppHost is stopped, one comment carries the eight, and the outcome is `passed`

#### Scenario: An app that does not start
- **WHEN** the diff touches `frontend/` and `aspire start` fails
- **THEN** `evidence` exits 1 with `outcome: blocked`, nothing is shot or published, and the why starts `Nothing published: aspire start failed:` followed by aspire's own words

#### Scenario: A capture whose tests fail
- **WHEN** the change touched a screen and the repository's capture command exits 1
- **THEN** `evidence` exits 1 with `outcome: failed`, and the why says which test failed

#### Scenario: gh cannot attach
- **WHEN** the change touched a screen, the captures passed, and `gh issue comment` fails
- **THEN** `evidence` exits 1 with `outcome: blocked` and `published: false`, and its why, which the Journey shows as the script's `Ran` line, starts `Nothing published: gh could not attach:` followed by gh's own words

#### Scenario: A change to the Review tab
- **WHEN** this repository's diff touches only `src/frontend/features/pbi/tabs/ReviewTab.tsx`
- **THEN** the comment carries the PBI's tabs in both themes and both widths, and not PBIs, Loop or Settings

#### Scenario: A new route
- **WHEN** a screen is added to `app/screens.tsx`
- **THEN** the next capture shoots it without the test changing, and a route with a parameter the test has no value for fails the test by name

### Requirement: The branch is the claim
`claim <issue> [base]` SHALL push `change/<issue>` to origin as an empty commit of its own on top of the
base, so a second claimant's push is rejected as a non-fast-forward, and SHALL answer `{claimed, why}`.
A rerun from the same clone SHALL find its own commit and carry on. `file-overlap <issue>` SHALL answer
whether the files the issue declares overlap an open issue that holds a `change/<n>` branch at origin; a
`change/<n>` whose number is a pull request SHALL not count as in flight.

#### Scenario: Two machines claim one issue
- **WHEN** two clones run `claim 42` at the same time
- **THEN** one answers `claimed: true` and the other `claimed: false`, with git's refusal as its why

### Requirement: file-overlap reads where the issue lands, and holds an issue it cannot compare
`file-overlap` SHALL read an issue's paths from its Where it lands section, whose title is a heading,
a bold line or a bold lead-in and may read `Where it lands`, `Dónde cae`, `Port o zona` or the older
`Seam`. Every backtick span in the section that looks like a path SHALL count wherever it sits in its
line, and so SHALL a line that is nothing but a path. `/aio:refine` SHALL write that section with one
path per line. An issue whose own paths cannot be read SHALL answer `clear: false` with `hold: true`
and exit 1, because only a person or a refine can fix it. Another issue in flight on the same files, or
one in flight that declares nothing, SHALL answer `wait: true` with `hold: false` and exit 0: a wait
that passes on its own. Its why SHALL name the section as the repository's installed
`.aio/templates/issue.md` titles it, the first heading there that names where an issue lands, and
`Dónde cae / Where it lands` when the repository has no such template or heading.

#### Scenario: A path inside a sentence under a bold lead-in
- **WHEN** the body reads "**Dónde cae.** En el propio comando `.ai/commands/aio/refine.md` (...)" and
  no other issue is in flight
- **THEN** `file-overlap` answers `clear: true`

#### Scenario: An issue that names no path
- **WHEN** the issue has no Where it lands section
- **THEN** `file-overlap` answers `clear: false` and `hold: true` and exits 1, and the engine holds the PBI with that why

#### Scenario: The section named as the template names it
- **WHEN** the repository's `.aio/templates/issue.md` has the heading `## Dónde cae` and issue 7 has no such section
- **THEN** the why reads "#7 has no Dónde cae section, so its files cannot be compared. /aio:refine adds one."

#### Scenario: Another issue in flight on the same file
- **WHEN** an open issue with a `change/<n>` branch at origin names the same file
- **THEN** `file-overlap` answers `wait: true` and `hold: false`, and the engine waits

### Requirement: The archive refuses a change with open tasks
`archive [--base <ref>] [<change>]` SHALL archive the OpenSpec change the branch added, refusing one
with an open task and naming every open checkbox of its `tasks.md`, refusing a branch with two changes,
and answering `ok` for a branch with none. It SHALL commit and push the archive and read the push back.

#### Scenario: A change with one task left
- **WHEN** `tasks.md` has an unchecked box on line 14
- **THEN** `archive` answers `ok: false` with `tasks.md:14 …` in `open`, and the PBI is held in `merging`

### Requirement: The one merge reads itself back
`merge <pull request> [expected head]` SHALL squash-merge the pull request at its expected head, refusing
when the head moved, delete the branch at the remote, read the pull request and the branch back, and
answer `merged` and `observed` true only when the read-back says merged at that head. Nothing SHALL
retry. It is the only merge: the agent's hook refuses any other. It SHALL NOT release: its line SHALL
carry no release field, and its `why` on a merge SHALL be one sentence about the pull request and its
branch (#652). `release` stays this repository's script, run by a person.

#### Scenario: A head that moved after approval
- **WHEN** the pull request's head is not the SHA the merge was given
- **THEN** nothing is merged and the answer says the head moved

#### Scenario: A merge releases nothing
- **WHEN** `merge` squash-merges a pull request and reads it back merged
- **THEN** its line has no `released`, `releaseTag`, `release` or `releaseObserved`, its `why` is one sentence, and `release` is not run

### Requirement: release publishes to the feed and tags once the push is accepted
`release [--source <url-or-folder>] [--no-tag] [commitish]` SHALL read the version from
`src/Directory.Build.props` at the commit named and run, in order and stopping at the first that fails:
`scripts/pack`; `dotnet nuget push artifacts/aio.<version>.nupkg --source <feed>`, with
`--api-key` from `NUGET_API_KEY` when it is set; and the tag `v<version>` with a GitHub release of notes
and no files, unless `--no-tag`. The feed SHALL default to `https://api.nuget.org/v3/index.json`. The
feed SHALL be asked whether it lists the version (the flat container named by the feed's service index,
or the package in the folder) once, before packing, and never after the push: nothing SHALL wait for
nuget.org to list a version it accepted. After a push a remote feed accepted, `why` SHALL say so:
"nuget.org accepted aio <version>; it lists it once it validates the package, usually 15 to 30
minutes" (#724). A refused push SHALL stop the step with nothing tagged. A version the feed
already lists SHALL not be packed or pushed; unless `--no-tag`, its tag and release SHALL still be made
when origin has no `v<version>`. A `v<version>` origin already has on the commit with its release SHALL be
said to be done, and one on another commit SHALL stop the step without moving it (#711). The `release`
workflow SHALL write the script's line to its run summary. The key SHALL appear in no output of the script: its line, and whatever it passes on
from `pack` and `dotnet`. It answers `{published, version, feed, tag, release, observed, why}` (#681).

#### Scenario: A release in order
- **WHEN** a person runs `release` with `NUGET_API_KEY` set and nuget.org does not list the version
- **THEN** `pack` runs, then the push, then the release, with no read of the feed after the push; the line says `published` and `observed`, its `why` says nuget.org lists the version once it validates it, and the key is in no output

#### Scenario: A refused push
- **WHEN** `dotnet nuget push` fails
- **THEN** the step stops with exit 1, `published: false` and no tag, and no release is created

#### Scenario: A version published but not tagged
- **WHEN** the feed already lists the version and origin has no `v<version>`
- **THEN** nothing is packed or pushed, the tag and its release are made on the commit, and the line says `published: false` with the tag and `observed`

#### Scenario: A version published and tagged
- **WHEN** the feed lists the version and `v<version>` is on the commit with its release
- **THEN** nothing is packed, pushed or tagged, and the line says it is done with `observed`

#### Scenario: A tag on another commit
- **WHEN** the feed lists the version and `v<version>` is on another commit
- **THEN** the step stops with exit 1, the line names both commits, and the tag is not moved

### Requirement: A score is a model's opinion in two bands, unsure is high, and no score is unknown
`risk-score` and `diff-risk` SHALL answer `{outcome, band, why}`, where `outcome` is `low`, `high` or
`unknown`: a score is a script whose outcome is one of the three, and the engine reads it as any other
outcome. `risk-score` SHALL answer `high` for its `medium` band, because a score that is not sure asks a
person. `diff-risk` SHALL read `protected` in `.aio/config.json` from the merge base, never from the change,
and SHALL answer `high` without asking the model when the diff touches a path the list names (one
array of patterns, `*` crossing directories), its why `protected: <path>, <path>` (#732).
Otherwise it SHALL answer `low` without asking the model when the diff changes at most 20 lines and
holds no binary file, its why saying how many lines and files (#551). For any other diff it SHALL ask
the model only what a list does not decide (`new_dependency`, `deletes_or_rewrites`, `untested`,
`beyond_issue` and the band), and answer `low` only when the model chose low with enough confidence
and no fact fired. A base from before harness 37 has the list in `.harness/protected-paths`, one pattern per line, and SHALL be read from there. When the base has neither, no path SHALL be protected and the
why SHALL say so once. When there is no score to give — the model server or the issue is missing, or the
answer is outside its contract — both SHALL answer `unknown` with a why that starts
`<script>: no se pudo puntuar:` and names the cause. Neither SHALL install or start the model server:
the engine runs `.aio/scorers/laya/laya ensure` before a score (loop-engine).

#### Scenario: No model server
- **WHEN** the laya server is not running and `proposal-review` runs `risk-score`
- **THEN** the score answers `outcome: unknown`, and its why says `no se pudo puntuar` and that the server was not reached, never that the risk came out high

#### Scenario: A one-line change
- **WHEN** `merge-review` runs `diff-risk` on a branch that changes one line of a comment, and no laya server is running
- **THEN** the score answers `outcome: low` with a why starting `small diff: 1 changed line in 1 file`

#### Scenario: A one-line change to a protected path
- **WHEN** the base's `protected` names `src/*/Migrations/*` and the branch changes one line of a migration
- **THEN** `diff-risk` answers `outcome: high` with the why `protected: <the migration's path>`, and the model is not asked

#### Scenario: A change to the list
- **WHEN** the branch adds the path it touches to `protected`, or removes it
- **THEN** `diff-risk` judges the diff by the list of the base: the path it added is not protected yet, and the one it removed still is

#### Scenario: A base with the old list
- **WHEN** the base has `.harness/protected-paths` naming `secrets/*` and no `.aio/config.json`, and the branch changes one line of `secrets/key.txt`
- **THEN** `diff-risk` answers `outcome: high` with the why `protected: secrets/key.txt`

#### Scenario: A repository without the list
- **WHEN** the base has no `.aio/config.json` and no `.harness/protected-paths` and the branch changes one line
- **THEN** `diff-risk` answers `outcome: low`, and its why ends saying no path is protected

### Requirement: The worktree is prepared and removed by scripts
`prepare <issue | branch> [base]` SHALL make or resume the worktree of the branch under
`<repository>/.worktrees/`, from origin's branch when it was pushed, and answer `{path, why}`. `cleanup
<…> --worktree-only` SHALL remove it without force, so uncommitted work refuses the removal. The local
environment SHALL use both, from the repository's copy, or from the copy the app carries when the
repository has none. Because every worktree shares the checkout's `.git/config` and refs, the app
SHALL run `prepare` and `cleanup` of one repository one at a time, keyed by its git common directory,
while runs in different worktrees go on in parallel; and when git answers `could not lock config file`
it SHALL run the script once more after a pause of half a second before failing the place.

#### Scenario: Several worktrees of one repository at once
- **WHEN** a loop with `wip: 4` prepares four worktrees of one repository at the same moment
- **THEN** each prepare waits for the one before it and all four worktrees are made, none failing on `.git/config`

#### Scenario: A worktree with uncommitted work
- **WHEN** a run ends with uncommitted changes in its worktree
- **THEN** `cleanup` refuses to remove it and the worktree stays on disk

### Requirement: trust-sweep lists the trust left for folders that are gone
`scripts/trust-sweep` SHALL read Claude Code's configuration, `$CLAUDE_CONFIG_DIR/.claude.json`
or `~/.claude.json`, and answer one JSON line with the file, how many folders it trusts, how many of
those are no longer on disk, how many it removed, their paths and a why. A trust entry SHALL be
`projects[<folder>]` with `hasTrustDialogAccepted` true. Without `--remove` it SHALL write nothing; with
it, it SHALL remove exactly the entries it listed and leave every other key of the file as it was,
writing through a rename that keeps the file's mode. It SHALL stay out of the harness package, because
it is about the machine and not the repository. A test that launches a real `claude` SHALL give it a
`CLAUDE_CONFIG_DIR` of its own, so no test writes the person's file.

#### Scenario: Listing without removing
- **WHEN** the file trusts three folders, two of them gone, and names a fourth gone folder it does not trust
- **THEN** `trust-sweep` answers `trusted: 3`, `gone: 2`, `removed: 0` with the two paths, and the file is byte for byte the same

#### Scenario: Removing
- **WHEN** the same file is swept with `--remove`
- **THEN** the two entries are gone, the trusted folder on disk, the untrusted entry and every other key stay, and the mode is unchanged

### Requirement: laya keeps one environment per machine and pin
`.aio/scorers/laya/laya` SHALL build its Python environment at
`~/.aiorchestrator/laya/<pin>/venv`, where `<pin>` is the first 12 hex characters of the SHA-256 of
the content of `.aio/scorers/laya/laya-requirements.txt`, never of its path, and SHALL keep the server's pidfile and log in the same
`<pin>` directory. Every worktree and repository on the machine with the same pin SHALL use that one
environment. An environment whose `bin/laya-serve` exists SHALL NOT be rebuilt. Two `ensure`s at once
SHALL build it once: the second waits for the first and uses its result. A changed pin SHALL build a
new environment and SHALL NOT touch the old one. `laya` SHALL write nothing under `.aio/`.

#### Scenario: A second worktree ensures
- **WHEN** `laya ensure` has built the environment from one worktree, the server is stopped, and `laya ensure` runs from another worktree with the same pin
- **THEN** the second starts the server from `~/.aiorchestrator/laya/<pin>/venv` without running `pip install`, and no `venv` exists under either worktree's `.aio/`

#### Scenario: The pin changes
- **WHEN** a line of `laya-requirements.txt` changes and `laya ensure` runs
- **THEN** a new `~/.aiorchestrator/laya/<new pin>/venv` is built, and the directory of the old pin is still there, unchanged

#### Scenario: stop from another worktree
- **WHEN** the server was started from one worktree and `laya stop` runs from another
- **THEN** the pidfile beside the environment names the server, and it is stopped

