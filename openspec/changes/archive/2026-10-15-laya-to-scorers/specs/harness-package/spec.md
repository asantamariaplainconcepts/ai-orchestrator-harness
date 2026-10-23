## MODIFIED Requirements

### Requirement: The root of .harness/ is the repository's and .harness/aio/ is the package's
An installed repository SHALL find at the root of `.harness/` only what it makes its own and the
package never overwrites, `loop.yaml`, `gate`, `evidence.json`, `quality.json`, `protected-paths`,
`templates/` and `version`, which the package writes, `README.md`, which the package overwrites on every update, and
anything the repository adds; and in `.harness/aio/` what
every update overwrites whole: the scripts `aio`, `claim`, `prepare`, `cleanup`, `file-overlap`,
`evidence`, `quality`, `risk-score`, `diff-risk`, `merge` and `feedback`, flat, so opening `aio/` shows
the loop's steps, and in folders what is not a step: `checks/`, `lib/`, `schemas/`, and
`scorers/laya/` with `laya` and `laya-requirements.txt` (#759). No tool SHALL sit flat beside the
steps. The agents' command SHALL be `.harness/aio/aio`, still called `aio` in the
commands and their prose. This repository SHALL keep what only it runs at its root, `package/`,
`archive`, `pack`, `release` and `trust-sweep`, and SHALL have no `.harness/scripts/`. The checks `quality` runs, `crap.sh`, `tests-lost-or-skipped.sh`,
`tests-touched.sh`, `old-tests-new-code.sh` and `out-of-seam.sh` with the `_lib.sh` they source, SHALL
be the package's in `.harness/aio/checks/` (#746, #758).
`.harness/context.md` SHALL say the rule in one line, and this repository's README and CONTRIBUTING
SHALL point to `.harness/README.md` instead of repeating it.

#### Scenario: This repository's .harness/
- **WHEN** a person lists `git ls-files .harness`
- **THEN** the root holds `README.md`, `loop.yaml`, `gate`, `evidence.json`, `quality.json`, `protected-paths`, `context.md`, `templates/`, `package/`, `archive`, `pack`, `release`, `trust-sweep` and `version`, `aio/` holds the rest, and there is no `scripts/`

#### Scenario: laya is not beside the steps
- **WHEN** a person lists `.harness/aio/`
- **THEN** there is no `laya` and no `laya-requirements.txt` in it, and both are in `.harness/aio/scorers/laya/`

### Requirement: The app carries one harness package with its version
The Server SHALL carry the harness package embedded in `Modules.Workspace`, declared by
`.harness/package/package.props`, and `GET /api/harness` SHALL answer its version, the first line of
`.harness/package/version`, and every entry it would write, each with its path from the repository's top, whether
it is executable, and the target of a symbolic link. The package SHALL hold `.harness/README.md`, `.harness/loop.yaml`,
`.harness/gate`, `.harness/evidence.json`, `.harness/quality.json`, `.harness/protected-paths`, `.harness/version`,
`.harness/aio/schemas/done.v1.json`, every script of `.harness/aio/`, with `.harness/aio/lib/` and
`.harness/aio/checks/` that they read, `.harness/aio/scorers/laya/laya` and the
`.harness/aio/scorers/laya/laya-requirements.txt` it reads, the six commands of `.ai/commands/aio/`, the issue
and epic templates `.harness/templates/issue.md` and `.harness/templates/epic.md`,
`.claude/hooks/pre-tool-use`, `.claude/settings.json` with its hook and allow list, and the links
`.claude/commands` and `.opencode/command` to `../.ai/commands`. The gate, the scripts straight in
`.harness/aio/`, `.harness/aio/scorers/laya/laya` and the hook SHALL be executable and nothing else SHALL be.

#### Scenario: Reading the package
- **WHEN** a client asks `GET /api/harness`
- **THEN** it answers version `36` and the paths above, with `.opencode/command` pointing at `../.ai/commands`, `.harness/aio/scorers/laya/laya` executable, no `.harness/aio/laya`, no `.harness/archive`, no `.harness/release`, no `.harness/trust-sweep`, no `.harness/pack`, no `.harness/keep-committer` and no `.harness/context.md`

### Requirement: The package's README says what is whose and what each step of the loop does
The package SHALL carry `.harness/README.md`, this repository's own file embedded and not copied, in
English and on one page. Its first line SHALL say that the file is the package's and every update
overwrites it, and it SHALL NOT be in `HarnessPackage.Owned`, so an install over a repository that has
one writes the package's. It SHALL say, under these headings: what is whose (each file a repository
makes its own, `context.md`, `version`, `aio/` with each of its folders, and the README); the loop, as a table with one row per
state of the package's `loop.yaml` naming what runs (the agent and its command, or the scripts) and
what it hands on, then the `{outcome, why}` line of the scripts with the outcomes of
`gate`, `evidence`, `merge` and the scores, the `aio done <outcome>` of the agents checked against
`aio/schemas/done.v1.json`, and one sentence saying that what is deterministic is the path taken
with a result, never the result, so a tool that answers the same format replaces another; how to make
it yours (`gate`, `evidence.json`, `protected-paths` with a `merge-review` state running `diff-risk`
that the package's loop accepts, `loop.yaml`, `context.md`, `templates/`); where
the loop leaves its work (the `status:*` labels, `change/<n>`, the comments on the issue and the
`aio/evidence` branch); and how it is updated. `HarnessPackageTests` SHALL fail when a state of the
package's loop has no row, a row has no state, or a row does not name the state's agent, command or
every script, and when the row for `aio/` does not name a folder of `.harness/aio/` the package carries.

#### Scenario: A state removed from the package's loop
- **WHEN** a state is taken out of the package's `loop.yaml` and the README is left as it is
- **THEN** `HarnessPackageTests` fails, naming the row the loop no longer declares

#### Scenario: A folder of aio/ the README does not name
- **WHEN** the row for `aio/` in the README's table of what is whose leaves out `scorers/laya/`
- **THEN** `HarnessPackageTests` fails, naming `scorers/laya/`

#### Scenario: Installing over a README of the repository's own
- **WHEN** a person installs on a repository whose default branch has its own `.harness/README.md`
- **THEN** the pull request carries the package's README in its place, and the answer's `kept` does not list it

### Requirement: Installing proposes the package by pull request
`POST /api/folders/{id}/harness/install` SHALL commit every entry of the package on the branch
`harness/install` of the folder's `origin`, started from its default branch in a worktree of its own that
is removed afterwards, push it with force and read the push back; then open a pull request from it
through `IPbiProvider` and create every state the package's loop names, declared or targeted, with the
hold. It SHALL never write to the default branch, and SHALL NOT touch the folder's checkout. The
repository's own `.harness/loop.yaml`, `.harness/gate`, `.harness/evidence.json`, `.harness/quality.json`,
`.harness/protected-paths` and `.harness/templates/*.md` (`HarnessPackage.Owned`) SHALL be kept where
the default branch has them. Over a repository installed before harness 24, the update SHALL move,
not copy: each file of `.harness/aio/` SHALL be removed from where the old layout had it
(`.harness/scripts/<name>`, `.harness/lib/`, `.harness/schemas/`), except harness 23's
`.harness/laya-requirements.txt`, which SHALL stay where it is (#759),
and a gate at `.harness/scripts/gate` SHALL become `.harness/gate` as the repository wrote it, unless
the default branch already has a `.harness/gate`. Whatever else `.harness/scripts/` holds SHALL be
removed with it, a script an older package carried and this one does not, such as `release`, or one
a person added, which the pull request's diff shows as removed. Over a repository installed before
harness 36, `.harness/aio/laya` and `.harness/aio/laya-requirements.txt` SHALL move to
`.harness/aio/scorers/laya/` in the same pull request (#759). A pull request already open from the branch SHALL be the answer, with nothing written. A folder
with no origin on github.com, a default branch that already has every file, and a push or pull request
that fails SHALL be 409 with the reason; a write that does not read back SHALL be a problem in the
answer. `GET /api/folders/{id}/harness/install` SHALL answer the open pull request from the branch, or
none, or the vendor's problem.

#### Scenario: Installing in a repository with no harness
- **WHEN** a person installs on a folder whose repository has no `.harness/`
- **THEN** a pull request from `harness/install` holds every file of the package on top of the default branch, the default branch is where it was, and the loop's nine states and the hold exist at the vendor

#### Scenario: Reinstalling over version 1
- **WHEN** the default branch has `.harness/version` `1` and a gate of its own, and the install pull request of version 1 is merged
- **THEN** a new pull request carries the package's current version and the repository's gate unchanged, and the default branch is not written

#### Scenario: Reinstalling over the old layout
- **WHEN** the default branch has harness 23, with its scripts and its own gate in `.harness/scripts/`, `lib/`, `schemas/` and `laya-requirements.txt` at the root, and its own `evidence.json`
- **THEN** the pull request holds the package in `.harness/aio/`, the repository's gate at `.harness/gate` as git's rename of the old one, its `evidence.json` unchanged, and nothing under `.harness/scripts/`, `.harness/lib/` or `.harness/schemas/`; its `.harness/laya-requirements.txt` stays, and nothing reads it

#### Scenario: Reinstalling over laya beside the steps
- **WHEN** the default branch has harness 34, with `.harness/aio/laya` and `.harness/aio/laya-requirements.txt`
- **THEN** the pull request's diff shows both renamed to `.harness/aio/scorers/laya/`, `laya` with mode `100755`, and the tree has no `.harness/aio/laya` and no `.harness/aio/laya-requirements.txt`
