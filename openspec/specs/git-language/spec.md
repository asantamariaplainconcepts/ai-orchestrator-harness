# git-language Specification

## Purpose
Git's sentences in English whatever the machine's language, and tests that start from the product's
defaults whatever process runs them. Code here reads what git says ("couldn't find remote ref",
"could not lock config file", "contains modified or untracked files"), and git translates it: on a
Spanish machine the installed service's gate went red with 7 tests that pass in a terminal, and the
first push to a repository's `aio/evidence` read as a refusal. Written with the code of #762
(`src/shared/AiOrchestrator.Infrastructure/Files/GitCli.cs`, `.aio/steps/`,
`src/tests/InheritedEnvironment.cs`).

## Requirements
### Requirement: git runs in the C locale wherever its output is read
`GitCli`, the one place in the product that starts git, SHALL run every git process with `LC_ALL=C`,
whatever the app's own environment says. Each script of the harness package in `.aio/steps/` that
runs git SHALL set `LC_ALL=C` once, at its start, for everything it runs: `export LC_ALL=C` in a bash
script and in `checks/_lib.sh`, which the checks source, and `process.env.LC_ALL = 'C'` in a node
script. What git says SHALL be read in English; no message is translated.

#### Scenario: The first recording of a repository on a Spanish machine
- **WHEN** the app runs with `LANG=es_ES.UTF-8` and `LC_ALL=es_ES.UTF-8` and pushes the first
  recording of a repository whose origin has no `aio/evidence`
- **THEN** git's "couldn't find remote ref" is read as a branch that does not exist yet, and the push
  creates `aio/evidence` with the recording, with no refusal

#### Scenario: cleanup on a Spanish machine
- **WHEN** `.aio/steps/cleanup` is run with `LC_ALL=es_ES.UTF-8` on a worktree with uncommitted work
- **THEN** its `why` is git's English sentence, which contains "contains modified or untracked files"

### Requirement: tests do not inherit the product's variables
Every test project of `src/AiOrchestrator.slnx` SHALL remove, when its assembly loads, every
environment variable whose name starts with `AIO_` from its own process, so the server it hosts and
every process it starts read none of them. The variables a person sets to opt into a test against
something real, those starting with `AIO_REAL_AGENTS` or `AIO_PBI_TEST_`, SHALL stay.

#### Scenario: The gate run by the installed service
- **WHEN** the gate runs with `AIO_SERVICE=aio` exported, as the installed service runs it
- **THEN** a run started after the permission mode is saved as `auto` starts in `auto`, as it does in
  a terminal
