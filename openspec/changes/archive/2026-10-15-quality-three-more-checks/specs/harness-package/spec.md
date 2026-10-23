## MODIFIED Requirements

### Requirement: The root of .harness/ is the repository's and .harness/aio/ is the package's
An installed repository SHALL find at the root of `.harness/` only what it makes its own and the
package never overwrites, `loop.yaml`, `gate`, `evidence.json`, `quality.json`, `protected-paths`,
`templates/` and `version`, which the package writes, `README.md`, which the package overwrites on every update, and
anything the repository adds; and in `.harness/aio/` what
every update overwrites whole: the scripts `aio`, `claim`, `prepare`, `cleanup`, `file-overlap`,
`evidence`, `quality`, `risk-score`, `diff-risk`, `laya`, `merge` and `feedback`, `checks/`, `lib/`,
`schemas/` and `laya-requirements.txt`. The agents' command SHALL be `.harness/aio/aio`, still called `aio` in the
commands and their prose. This repository SHALL keep what only it runs at its root, `package/`,
`archive`, `pack`, `release` and `trust-sweep`, and SHALL have no `.harness/scripts/`. The checks `quality` runs, `crap.sh`, `tests-lost-or-skipped.sh`,
`tests-touched.sh`, `old-tests-new-code.sh` and `out-of-seam.sh` with the `_lib.sh` they source, SHALL
be the package's in `.harness/aio/checks/` (#746, #758).
`.harness/context.md` SHALL say the rule in one line, and this repository's README and CONTRIBUTING
SHALL point to `.harness/README.md` instead of repeating it.

#### Scenario: This repository's .harness/
- **WHEN** a person lists `git ls-files .harness`
- **THEN** the root holds `README.md`, `loop.yaml`, `gate`, `evidence.json`, `quality.json`, `protected-paths`, `context.md`, `templates/`, `package/`, `archive`, `pack`, `release`, `trust-sweep` and `version`, `aio/` holds the rest, and there is no `scripts/`
