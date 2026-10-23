## ADDED Requirements

### Requirement: laya keeps one environment per machine and pin
`.harness/aio/laya` SHALL build its Python environment at
`~/.aiorchestrator/laya/<pin>/venv`, where `<pin>` is the first 12 hex characters of the SHA-256 of
`.harness/aio/laya-requirements.txt`, and SHALL keep the server's pidfile and log in the same
`<pin>` directory. Every worktree and repository on the machine with the same pin SHALL use that one
environment. An environment whose `bin/laya-serve` exists SHALL NOT be rebuilt. Two `ensure`s at once
SHALL build it once: the second waits for the first and uses its result. A changed pin SHALL build a
new environment and SHALL NOT touch the old one. `laya` SHALL write nothing under `.harness/`.

#### Scenario: A second worktree ensures
- **WHEN** `laya ensure` has built the environment from one worktree, the server is stopped, and `laya ensure` runs from another worktree with the same pin
- **THEN** the second starts the server from `~/.aiorchestrator/laya/<pin>/venv` without running `pip install`, and no `venv` exists under either worktree's `.harness/`

#### Scenario: The pin changes
- **WHEN** a line of `laya-requirements.txt` changes and `laya ensure` runs
- **THEN** a new `~/.aiorchestrator/laya/<new pin>/venv` is built, and the directory of the old pin is still there, unchanged

#### Scenario: stop from another worktree
- **WHEN** the server was started from one worktree and `laya stop` runs from another
- **THEN** the pidfile beside the environment names the server, and it is stopped
