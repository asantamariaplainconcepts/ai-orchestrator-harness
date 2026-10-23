# harness-package Specification

## Purpose
The harness is a package with a version that the app carries, so a repository can receive it instead of
copying it by hand, and this repository is its first installation. Written with the code that ships it
(`src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Harness/Package/`, `src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Harness/`). #566
made the package runnable in any repository, with its own commands and no OpenSpec, and installs it by
pull request (`InstallHarness.cs`, `ReadHarnessInstall.cs`, `IBranchPublisher`). #572 gave it its own
`refine`, `explore` and `review`, which name nothing of this repository, and left `archive` out. #618
made the package's six commands this repository's own, and moved what is this repository's into
`.aio/context.md`, which the commands read when it exists. #633 put the issue and epic templates
in the package, and made the issue template the Definition of Ready that `refine` and `explore` read.
#635 made `explore` cut an idea too big for one change into an epic of `epic.md` with sub-issues of
`issue.md`. #729 split the folder by owner and made an update over the old layout move the files instead of copying
them. #742 made the README the package's, and the page a repository that installed the harness reads to
know what is whose and what each step of the loop does. #779 renamed the folder from `.harness/` to
`.aio/`, said who owns a file by a line at its top instead of by where it sits, put the steps in `steps/`,
gathered the three files of configuration into `config.json` and took what only this repository runs out
of the folder.
## Requirements
### Requirement: A file of the package starts with its managed line and the rest of .aio/ is the repository's
An installed repository SHALL find in `.aio/`, from the root: `README.md` and `version`, the package's;
`loop.yaml`, `gate`, `config.json`, `context.md` and `templates/`, the repository's, which the package
never overwrites; `steps/`, one file per step `loop.yaml` names (`claim`, `prepare`, `cleanup`,
`file-overlap`, `evidence`, `quality`, `risk-score`, `diff-risk`, `merge` and `feedback`); `checks/`, one
check per file, `crap.sh`, `tests-lost-or-skipped.sh`, `tests-touched.sh`, `old-tests-new-code.sh` and
`out-of-seam.sh` with the `_lib.sh` they source (#746, #758); `lib/`; `schemas/`; `scorers/laya/` with
`laya` and `laya-requirements.txt` (#759); and `aio`, the agents' command, still called `aio` in the commands
and their prose. No tool SHALL sit flat beside the steps. Every file of the package under `.aio/` and the
hook `.claude/hooks/pre-tool-use` SHALL start, after its shebang when it has one, with a line of one sentence in
the comment syntax of its language: `Managed by aio. Install harness overwrites it.`, with no version, so a bump of the package rewrites no managed file; a file of the repository's SHALL NOT carry it. The files whose bytes mean
something to a program SHALL NOT carry it: `version`, which is a number the engine reads,
`laya-requirements.txt`, whose hash is the pin of laya's Python environment, `.claude/settings.json`, and the
six commands, which open with front matter. This repository SHALL keep `archive`, its own step, at the root of
`.aio/` where the engine finds it before the package's steps, and what only a person or a workflow runs in
`scripts/` at its top (`pack`, `release`, `trust-sweep`, `keep-committer`, with a `README.md` saying what each
does and who calls it), and SHALL have no `.harness/` (#760, #779). `.aio/context.md` SHALL say the rule in one
line, and this repository's README and CONTRIBUTING SHALL point to `.aio/README.md` instead of repeating it.

#### Scenario: This repository's .aio/
- **WHEN** a person lists `git ls-files .aio`
- **THEN** the root holds `README.md`, `version`, `loop.yaml`, `gate`, `config.json`, `context.md`, `archive`, `aio`, `templates/`, `steps/`, `checks/`, `lib/`, `schemas/` and `scorers/laya/`, and there is no `.harness/`, no `.aio/repo/` and no `.aio/scripts/`

#### Scenario: Every file of the package says whose it is
- **WHEN** `HarnessPackageTests` reads each file of the package
- **THEN** each one but the four exceptions starts with the managed line, and no file of the repository's own (`loop.yaml`, `gate`, `config.json`, the templates, `context.md`, `archive`, `scripts/`) does

#### Scenario: laya is not beside the steps
- **WHEN** a person lists `.aio/steps/`
- **THEN** there is no `laya` and no `laya-requirements.txt` in it, and both are in `.aio/scorers/laya/`

### Requirement: The app carries one harness package with its version
The Server SHALL carry the harness package embedded in `Modules.Workspace`, declared by
`src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Harness/Package/package.props`, and `GET /api/harness` SHALL answer its version, the first line of
`src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Harness/Package/version`, and every entry it would write, each with its path from the repository's top, whether
it is executable, and the target of a symbolic link. The package SHALL hold `.aio/README.md`, `.aio/loop.yaml`,
`.aio/gate`, `.aio/config.json`, `.aio/version`, `.aio/aio`,
`.aio/schemas/done.v1.json`, every script of `.aio/steps/`, with `.aio/lib/` and
`.aio/checks/` that they read, `.aio/scorers/laya/laya` and the
`.aio/scorers/laya/laya-requirements.txt` it reads, the six commands of `.ai/commands/aio/`, the issue
and epic templates `.aio/templates/issue.md` and `.aio/templates/epic.md`,
`.claude/hooks/pre-tool-use`, `.claude/settings.json` with its hook and allow list, and the links
`.claude/commands` and `.opencode/command` to `../.ai/commands`. The gate, `.aio/aio`, the scripts straight in
`.aio/steps/`, `.aio/scorers/laya/laya` and the hook SHALL be executable and nothing else SHALL be.

#### Scenario: Reading the package
- **WHEN** a client asks `GET /api/harness`
- **THEN** it answers version `37` and the paths above, with `.opencode/command` pointing at `../.ai/commands`, `.aio/scorers/laya/laya` executable, no `.aio/steps/laya`, no `archive`, `release`, `trust-sweep`, `pack` or `keep-committer` anywhere, nothing under `.harness/` and no `.aio/context.md`

### Requirement: The package's README says what is whose and what each step of the loop does
The package SHALL carry `.aio/README.md`, this repository's own file embedded and not copied, in
English and on one page. Its first line SHALL be the managed line, as an HTML comment, and the page SHALL say, in its
first section, what that line means, and SHALL NOT be in `HarnessPackage.Owned`, so an install over a repository that has
one writes the package's. It SHALL say, under these headings: what is whose (the rule, and the layout of `.aio/`
with a line for each file a repository makes its own and each folder the package carries); the loop, as a table with one row per
state of the package's `loop.yaml` naming what runs (the agent and its command, or the scripts) and
what it hands on, then the `{outcome, why}` line of the scripts with the outcomes of
`gate`, `evidence`, `merge` and the scores, the `aio done <outcome>` of the agents checked against
`schemas/done.v1.json`, and one sentence saying that what is deterministic is the path taken
with a result, never the result, so a tool that answers the same format replaces another; how to make
it yours (`gate`, `config.json` with its keys `evidence`, `quality` and `protected` and a `merge-review` state running `diff-risk`
that the package's loop accepts, `loop.yaml`, `context.md`, `templates/`); where
the loop leaves its work (the `status:*` labels, `change/<n>`, the comments on the issue and the
`aio/evidence` branch); and how it is updated. `HarnessPackageTests` SHALL fail when a state of the
package's loop has no row, a row has no state, or a row does not name the state's agent, command or
every script, and when the layout does not name a folder of `.aio/` the package carries.

#### Scenario: A state removed from the package's loop
- **WHEN** a state is taken out of the package's `loop.yaml` and the README is left as it is
- **THEN** `HarnessPackageTests` fails, naming the row the loop no longer declares

#### Scenario: A folder the README does not name
- **WHEN** the README's layout leaves out `scorers/laya/`
- **THEN** `HarnessPackageTests` fails, naming `scorers/laya/`

#### Scenario: Installing over a README of the repository's own
- **WHEN** a person installs on a repository whose default branch has its own `.aio/README.md`
- **THEN** the pull request carries the package's README in its place, and the answer's `kept` does not list it

### Requirement: The package's loop is the simple one
The package's `.aio/loop.yaml` SHALL be a loop `ValidateLoop` accepts, with seven states
(`ready-for-proposal`, `spec-ready`, `in-progress`, `verifying`, `code-review`, `retro`, `merging`), no
score, and `claude-code` as the provider of every role. `verifying` SHALL run `gate`, then `evidence`
(#709), then `quality`, whose `reported` no `on` names and which holds nothing (#746). A red gate and a conflict SHALL hold the PBI, and so SHALL evidence's `blocked` and `failed`,
which `verifying`'s `on` names with a hold (#733); its `passed` and `skipped` leave the gate's green to
decide.
It SHALL have no `rebasing` state and SHALL run no `archive` in `merging`: nothing in it needs OpenSpec.

#### Scenario: The template validates
- **WHEN** the package's loop is posted to `/api/loop/validate`
- **THEN** it is valid, with those seven states in that order and no problems

### Requirement: The package's config.json is an example that captures nothing and protects nothing
The package's `.aio/config.json` SHALL be an example with three keys, `evidence`, `quality` and `protected`, whose
every line is a `//` comment around an empty object or array: what `paths`, `start` (an Aspire AppHost and
resource, or a command and URL), `routes` and `capture` mean for `evidence`, what each line of `quality` says, and
what a pattern of `protected` means, that `diff-risk` reads the list from the base, the rule for writing it
(concrete paths, that show in the diff, and nothing the gate already fails), with one line of each to
uncomment (#732, #779). Installed as it ships, `evidence` SHALL answer that it names no paths and `diff-risk`
SHALL protect no path. Like the gate, it SHALL be the repository's own once installed.

#### Scenario: Verifying in a repository that kept the example
- **WHEN** the package's `evidence` runs after the gate with the example as it ships
- **THEN** it publishes nothing, exits 0 with `outcome: skipped`, and its why is `Nothing published: .aio/config.json (evidence) names no paths.`

#### Scenario: A repository that kept the example
- **WHEN** the package's `config.json` is installed as it ships
- **THEN** its `evidence` and `quality` are empty objects and its `protected` is an empty array, so no path is protected

### Requirement: The package's gate is a stub that refuses
The package's `.aio/gate` SHALL answer `{"outcome": "red", "go": false, "why": "define your gate here"}` as its
last line and exit 1, and SHALL carry one commented line per stack (`aspire do ci`, `dotnet test`,
`pnpm test`) for a person to choose from, each through its `run` function. It SHALL NOT detect the stack.
`run <command...>` SHALL send the command's whole output to `${TMPDIR:-/tmp}/aio-gate-<short sha>-<pid>.log`
and answer green or red by the command's exit code, with a `why` that names the command, the last
`N/N steps succeeded` the log holds when `aspire do ci` printed one, the first line marked failed
(`✗`, `Failed!`, `FAIL` or `Error:`) when it did not pass, and `Log: <path>` (#638), so a red gate
can be read from the Journey without running it again.

#### Scenario: Verifying in a repository that kept the stub
- **WHEN** the installed gate runs
- **THEN** the gate is red with the why "define your gate here", and the PBI is held

#### Scenario: A red aspire do ci through the stub
- **WHEN** the `aspire do ci` line is uncommented and `aspire` exits 1 after printing `✗ ci-test failed` and `9/10 steps succeeded`
- **THEN** the gate answers `outcome: red` with a why that reads `aspire do ci failed (exit 1): ✗ ci-test failed (9/10 steps succeeded). Log: <path>`, and the file at that path holds everything `aspire` printed

### Requirement: The package runs without OpenSpec or skills
The package SHALL carry six commands, `propose`, `implement`, `review-diff`, `refine`, `explore` and
`review`: this repository's `.ai/commands/aio/`, embedded from there and installed at the same path, so
there is one set of them. `/aio:propose` SHALL post a short plan on the issue, headed `## Plan`, with
what changes, where, how it is checked and what it leaves out; `/aio:implement` SHALL work from the
latest plan; `/aio:review-diff` SHALL review the diff against the plan and the issue. None of the six
SHALL name OpenSpec or a skill, and none SHALL do what the engine does. The package SHALL carry no
skills, no `.aio/context.md`, and no `archive` script: its loop archives nothing.

#### Scenario: A repository with no OpenSpec
- **WHEN** a PBI of a repository that installed the package reaches `spec-ready`
- **THEN** `/aio:propose` comments a plan on its issue and ends with `next`, and no command it runs needs `openspec`

### Requirement: The package's commands name nothing of this repository
None of the six commands SHALL name this repository's ports, modules, documents or folders
(`IPbiProvider` and the other four ports, `ARCHITECTURE.md`, `DESIGN.md`, `docs/`, `src/`, the retro
log, OpenSpec's `proposal.md` and `tasks.md`); a command that needs to know how a repository is built
SHALL point at that repository's own `README.md`, `AGENTS.md` or `CONTRIBUTING.md` when it has them.
Each command's Read section SHALL name `.aio/context.md`, read when the repository has one, and
say that where the file speaks it replaces the command and where it is silent the command stands. This
repository's `.aio/context.md` SHALL carry what its commands used to: that the plan is an OpenSpec
change written with `openspec-propose`, worked with `openspec-apply-change` and reviewed against its
`tasks.md`, that the retro is an entry in `docs/process/retro-log.md` on the branch, and its five ports.
A repository without the file SHALL run the commands as they are.
`/aio:refine` SHALL take an issue to the Definition of Ready and end with `ready` or `gaps`;
`/aio:explore` SHALL turn an idea into one issue, or into an epic with sub-issues when it does not fit one change, with the person, created only when they confirm; `/aio:review` SHALL post what the change taught as one comment on the issue, headed `## Retro`,
or none when it taught nothing, commit nothing unless `.aio/context.md` puts the retro in a file
on the branch, and end with `next`.

#### Scenario: A retro in a repository with no retro log
- **WHEN** a PBI of a repository that installed the package reaches `retro`
- **THEN** `/aio:review` comments on the issue or says why it has nothing to add, the branch is not written, and the run ends with `next`

#### Scenario: The commands are checked for this repository's names
- **WHEN** `HarnessPackageTests` runs
- **THEN** no command of `.ai/commands/aio/` contains a name of this repository's list, each names `.aio/context.md` in its Read section, and `.aio/context.md` contains the ports, the retro log and OpenSpec's files

#### Scenario: This repository's propose writes an OpenSpec change
- **WHEN** a PBI of this repository reaches `spec-ready` and `/aio:propose` reads `.aio/context.md`
- **THEN** the branch gains a change under `openspec/changes/` that validates, and the issue gains no `## Plan` comment

### Requirement: The Definition of Ready is the package's issue template
The package SHALL carry two templates, embedded from `src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Harness/Package/templates/` and installed at
`.aio/templates/`, written in markdown with a help line under each section as an HTML comment, so
it does not show in the issue. `issue.md` SHALL have the sections *Parent* (optional, with `#<n>`),
*Qué pasa hoy*, *Qué tiene que pasar*, *Dónde cae*, *Comprobación* and *Lo que no es*; its help line
for *Dónde cae* SHALL ask for one path per line in backticks, say that *Where it lands* is the title
in an English issue, and name no path itself. `epic.md` SHALL have *Objetivo*, *El grafo* with a
`mermaid` block of `flowchart LR`, nodes `i<number>` and a line saying what an arrow means,
*Sub-issues* as a table of Issue, Qué and Dónde cae, *Criterios de aceptación* and *Lo que no es*.
`/aio:refine` and `/aio:explore` SHALL carry no list of their own: each SHALL say to read
`.aio/templates/issue.md`, and that an issue is ready when each section of the template answers
what its help line asks. `/aio:refine` SHALL put an issue that does not follow the template in its
sections, keeping its text. Both templates SHALL be a repository's own once installed, like its loop
and its gate: a reinstall writes them only where the repository has none, so a repository that changes
its template changes its Definition of Ready without touching the commands. This repository SHALL have
both installed, byte for byte the package's. The package does not use the vendor's templates
(`.github/ISSUE_TEMPLATE` and the like): the vendor receives text.

#### Scenario: An issue written with the template
- **WHEN** an issue's body is `issue.md` with two paths in backticks under *Dónde cae*, and nothing else is in flight
- **THEN** the package's `file-overlap` answers `clear: true`, and on the template as it ships, help lines and all, it answers `hold: true` because the section names no path

#### Scenario: The commands read the template
- **WHEN** `AgentCommandTests` runs
- **THEN** `refine.md` and `explore.md` name `.aio/templates/issue.md`, and neither carries the five-point list

### Requirement: This repository shares the package's other files
The package's own files SHALL be `src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Harness/Package/`: `loop.yaml`, `gate`, `config.json`,
`version`, `links`, `package.props` and the two templates of `templates/`. Every other entry of the package (`.aio/README.md`, the scripts,
`lib/`, the schema, the hook, the settings and the six commands) SHALL be this repository's own file, embedded from its path and not
copied, so the two cannot drift; a link SHALL be the repository's link with the same target. This
repository's `loop.yaml`, `gate` and `config.json` SHALL be its own and differ from the package's, its
`.aio/context.md` and `.aio/archive` SHALL stay its own and out of the package, and
its `.aio/version` SHALL say what it has installed, `1`, while the package is at its own version. A change to any
entry SHALL raise `src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Harness/Package/version`.

#### Scenario: A script fixed in this repository
- **WHEN** `.aio/steps/merge` changes and the Server is built
- **THEN** the package carries the changed script, and `HarnessPackageTests` finds every script of
  `.aio/steps/` byte for byte the same in both

### Requirement: Installing proposes the package by pull request
`POST /api/folders/{id}/harness/install` SHALL commit every entry of the package on the branch
`harness/install` of the folder's `origin`, started from its default branch in a worktree of its own that
is removed afterwards, push it with force and read the push back; then open a pull request from it
through `IPbiProvider` and create every state the package's loop names, declared or targeted, with the
hold. It SHALL never write to the default branch, and SHALL NOT touch the folder's checkout. The
repository's own `.aio/loop.yaml`, `.aio/gate`, `.aio/config.json` and `.aio/templates/*.md` (`HarnessPackage.Owned`) SHALL be kept where
the default branch has them. Over a repository installed before harness 37 the update SHALL move,
not copy: each file of the package SHALL be removed from where the old layout had it, the nearest layout
first (`HarnessPackage.Former`): `.harness/aio/<step>` to `.aio/steps/`, `.harness/aio/aio` to `.aio/aio`,
`.harness/aio/{checks,lib,schemas,scorers}` to `.aio/`, and the root of `.harness/` to the root of `.aio/`;
`.harness/context.md` to `.aio/context.md`; and the repository's own files, `loop.yaml`, `gate` and the
templates, with git's rename, byte for byte, when `.aio/` does not have them. `.aio/config.json` SHALL be
written, when the default branch has none, from the default branch's `.harness/evidence.json`,
`.harness/quality.json`, `.harness/protected-paths` and `.harness/config.json`: `evidence`, `quality` and `protected`
SHALL hold what the first three had, comments included, the patterns of `protected-paths` as an array, and
every other key of `.harness/config.json`, such as `feedback`, SHALL stay as it was; it SHALL be the example when
none of them exists. The four files, `.harness/aio/` and `.harness/scripts/` SHALL then be removed, whatever else
they hold, a script an older package carried and this one does not, such as `release`, or one a person
added, which the pull request's diff shows as removed. Over a repository installed before harness 24 a gate at
`.harness/scripts/gate` SHALL become `.aio/gate` as the repository wrote it, unless the default branch already
has one, and harness 23's `.harness/laya-requirements.txt` SHALL stay where it is (#759). A pull request already open from the
branch SHALL be the answer, with nothing written. A folder with no origin on github.com, a default branch that
already has every file, and a push or pull request that fails SHALL be 409 with the reason; a write that does not
read back SHALL be a problem in the answer. `GET /api/folders/{id}/harness/install` SHALL answer the open pull
request from the branch, or none, or the vendor's problem.

#### Scenario: Installing in a repository with no harness
- **WHEN** a person installs on a folder whose repository has no `.aio/`
- **THEN** a pull request from `harness/install` holds every file of the package on top of the default branch, the default branch is where it was, and the loop's nine states and the hold exist at the vendor

#### Scenario: Reinstalling over version 1
- **WHEN** the default branch has `.aio/version` `1` and a gate of its own, and the install pull request of version 1 is merged
- **THEN** a new pull request carries the package's current version and the repository's gate unchanged, and the default branch is not written

#### Scenario: Reinstalling over harness 36
- **WHEN** the default branch has harness 36: its package in `.harness/aio/`, its own `loop.yaml`, `gate`, `context.md` and templates at the root of `.harness/`, and its `evidence.json`, `quality.json`, `protected-paths` and a `config.json` with `feedback`
- **THEN** the pull request holds everything in `.aio/`, git shows the repository's own files as renames of the same bytes and the package's files as renames into `steps/`, `checks/`, `lib/`, `schemas/` and `scorers/laya/`, `.aio/config.json` has `evidence`, `quality`, `protected` and `feedback` with what each had, and nothing is left of `.harness/`

#### Scenario: A config.json the repository already has
- **WHEN** the default branch already has `.aio/config.json`
- **THEN** the pull request leaves it as it is, and still removes the old files from `.harness/`

#### Scenario: Reinstalling over the layout before harness 24
- **WHEN** the default branch has harness 23, with its scripts and its own gate in `.harness/scripts/`, `lib/`, `schemas/` and `laya-requirements.txt` at the root, and its own `evidence.json`
- **THEN** the pull request holds the package in `.aio/`, the repository's gate at `.aio/gate` as git's rename of the old one, its evidence in `.aio/config.json`, and nothing under `.harness/scripts/`, `.harness/lib/` or `.harness/schemas/`; its `.harness/laya-requirements.txt` stays, and nothing reads it

#### Scenario: Reinstalling over laya beside the steps
- **WHEN** the default branch has harness 34, with `.harness/aio/laya` and `.harness/aio/laya-requirements.txt`
- **THEN** the pull request's diff shows both renamed to `.aio/scorers/laya/`, `laya` with mode `100755`, and the tree has no `.harness/aio/laya` and no `.harness/aio/laya-requirements.txt`
