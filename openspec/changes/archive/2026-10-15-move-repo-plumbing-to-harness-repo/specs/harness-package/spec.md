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
commands and their prose. This repository SHALL keep what only it runs in `.harness/repo/`, `package/`,
`archive`, `pack`, `release`, `trust-sweep` and `keep-committer`, with a `README.md` saying what each does
and who calls it, and SHALL have no `.harness/scripts/` (#760). The checks `quality` runs, `crap.sh`, `tests-lost-or-skipped.sh`,
`tests-touched.sh`, `old-tests-new-code.sh` and `out-of-seam.sh` with the `_lib.sh` they source, SHALL
be the package's in `.harness/aio/checks/` (#746, #758).
`.harness/context.md` SHALL say the rule in one line, and this repository's README and CONTRIBUTING
SHALL point to `.harness/README.md` instead of repeating it.

#### Scenario: This repository's .harness/
- **WHEN** a person lists `git ls-files .harness`
- **THEN** the root holds `README.md`, `version`, `loop.yaml`, `gate`, `evidence.json`, `quality.json`, `protected-paths`, `context.md`, `templates/`, `aio/` and `repo/`, `repo/` holds `README.md`, `package/`, `archive`, `pack`, `release`, `trust-sweep` and `keep-committer`, and there is no `scripts/`

#### Scenario: laya is not beside the steps
- **WHEN** a person lists `.harness/aio/`
- **THEN** there is no `laya` and no `laya-requirements.txt` in it, and both are in `.harness/aio/scorers/laya/`

### Requirement: The app carries one harness package with its version
The Server SHALL carry the harness package embedded in `Modules.Workspace`, declared by
`.harness/repo/package/package.props`, and `GET /api/harness` SHALL answer its version, the first line of
`.harness/repo/package/version`, and every entry it would write, each with its path from the repository's top, whether
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
- **THEN** it answers version `36` and the paths above, with `.opencode/command` pointing at `../.ai/commands`, `.harness/aio/scorers/laya/laya` executable, no `.harness/aio/laya`, no `archive`, `release`, `trust-sweep`, `pack` or `keep-committer` anywhere in `.harness/`, nothing under `.harness/repo/` and no `.harness/context.md`

### Requirement: The package runs without OpenSpec or skills
The package SHALL carry six commands, `propose`, `implement`, `review-diff`, `refine`, `explore` and
`review`: this repository's `.ai/commands/aio/`, embedded from there and installed at the same path, so
there is one set of them. `/aio:propose` SHALL post a short plan on the issue, headed `## Plan`, with
what changes, where, how it is checked and what it leaves out; `/aio:implement` SHALL work from the
latest plan; `/aio:review-diff` SHALL review the diff against the plan and the issue. None of the six
SHALL name OpenSpec or a skill, and none SHALL do what the engine does. The package SHALL carry no
skills, no `.harness/context.md`, and no `archive` script, at the root of `.harness/` or under `.harness/repo/`: its loop archives nothing.

#### Scenario: A repository with no OpenSpec
- **WHEN** a PBI of a repository that installed the package reaches `spec-ready`
- **THEN** `/aio:propose` comments a plan on its issue and ends with `next`, and no command it runs needs `openspec`

### Requirement: The Definition of Ready is the package's issue template
The package SHALL carry two templates, embedded from `.harness/repo/package/templates/` and installed at
`.harness/templates/`, written in markdown with a help line under each section as an HTML comment, so
it does not show in the issue. `issue.md` SHALL have the sections *Parent* (optional, with `#<n>`),
*Qué pasa hoy*, *Qué tiene que pasar*, *Dónde cae*, *Comprobación* and *Lo que no es*; its help line
for *Dónde cae* SHALL ask for one path per line in backticks, say that *Where it lands* is the title
in an English issue, and name no path itself. `epic.md` SHALL have *Objetivo*, *El grafo* with a
`mermaid` block of `flowchart LR`, nodes `i<number>` and a line saying what an arrow means,
*Sub-issues* as a table of Issue, Qué and Dónde cae, *Criterios de aceptación* and *Lo que no es*.
`/aio:refine` and `/aio:explore` SHALL carry no list of their own: each SHALL say to read
`.harness/templates/issue.md`, and that an issue is ready when each section of the template answers
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
- **THEN** `refine.md` and `explore.md` name `.harness/templates/issue.md`, and neither carries the five-point list

### Requirement: This repository shares the package's other files
The package's own files SHALL be `.harness/repo/package/`: `loop.yaml`, `gate`, `evidence.json`,
`quality.json`, `protected-paths`, `version`, `links`, `package.props` and the two templates of `templates/`. Every other entry of the package (`.harness/README.md`, the scripts,
`lib/`, the schema, the hook, the settings and the six commands) SHALL be this repository's own file, embedded from its path and not
copied, so the two cannot drift; a link SHALL be the repository's link with the same target. This
repository's `loop.yaml`, `gate`, `evidence.json`, `quality.json` and `protected-paths` SHALL be its own and differ from the package's, its
`.harness/context.md` and everything in `.harness/repo/` SHALL stay its own and out of the package, and
its `.harness/version` SHALL say what it has installed, `1`, while the package is at its own version. A change to any
entry SHALL raise `.harness/repo/package/version`.

#### Scenario: A script fixed in this repository
- **WHEN** `.harness/aio/merge` changes and the Server is built
- **THEN** the package carries the changed script, and `HarnessPackageTests` finds every script of
  `.harness/aio/` byte for byte the same in both
