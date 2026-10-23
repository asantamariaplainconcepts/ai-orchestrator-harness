## MODIFIED Requirements

### Requirement: The engine finds a script by its name
A script named in `loop.yaml` (`script: gate`, `script: [archive, merge]`) SHALL be a name, never a
path, and the engine SHALL run the first of `.harness/<name>`, `.harness/aio/<name>` and
`.harness/scripts/<name>` that exists in the checkout (`HarnessScripts.Find`), so the repository's own
script wins over the package's. The last is the layout before harness 24: a repository that has not
installed 24 yet SHALL keep running until it does. A name with a slash or a leading dot SHALL fail
without running anything, and a name no folder has SHALL fail saying so. `prepare` and `cleanup`
SHALL be found the same way. `laya` is a tool, not a step: it SHALL be found first at
`.harness/aio/scorers/laya/laya`, and only where that file is not, in the folders any script is
found in, so a repository that has not installed harness 36 still has its `.harness/aio/laya`
found until the install moves it (#759). An agent's PATH SHALL start with the checkout's
`.harness/aio`, then `.harness/scripts`, so a bare `aio` is the checkout's in either layout.

#### Scenario: The gate and the claim
- **WHEN** the engine runs `script: gate` and `script: claim` in a checkout of the new layout
- **THEN** it runs `.harness/gate` and `.harness/aio/claim`

#### Scenario: A repository that has not updated
- **WHEN** a checkout has its scripts only in `.harness/scripts/`
- **THEN** `script: gate` runs `.harness/scripts/gate`, as before harness 24

#### Scenario: laya in its folder
- **WHEN** a checkout has `.harness/aio/scorers/laya/laya`
- **THEN** `HarnessScripts.Find(checkout, "laya")` answers that file

#### Scenario: laya left beside the steps
- **WHEN** a checkout has `.harness/aio/laya` and no `.harness/aio/scorers/laya/laya`
- **THEN** `HarnessScripts.Find(checkout, "laya")` answers `.harness/aio/laya`

#### Scenario: laya in both places
- **WHEN** a checkout has `.harness/aio/laya` and `.harness/aio/scorers/laya/laya`
- **THEN** `HarnessScripts.Find(checkout, "laya")` answers `.harness/aio/scorers/laya/laya`

### Requirement: A score is a model's opinion in two bands, unsure is high, and no score is unknown
`risk-score` and `diff-risk` SHALL answer `{outcome, band, why}`, where `outcome` is `low`, `high` or
`unknown`: a score is a script whose outcome is one of the three, and the engine reads it as any other
outcome. `risk-score` SHALL answer `high` for its `medium` band, because a score that is not sure asks a
person. `diff-risk` SHALL read `.harness/protected-paths` from the merge base, never from the change,
and SHALL answer `high` without asking the model when the diff touches a path the list names (one
pattern per line, `#` comments, `*` crossing directories), its why `protected: <path>, <path>` (#732).
Otherwise it SHALL answer `low` without asking the model when the diff changes at most 20 lines and
holds no binary file, its why saying how many lines and files (#551). For any other diff it SHALL ask
the model only what a list does not decide (`new_dependency`, `deletes_or_rewrites`, `untested`,
`beyond_issue` and the band), and answer `low` only when the model chose low with enough confidence
and no fact fired. When the base has no `.harness/protected-paths`, no path SHALL be protected and the
why SHALL say so once. When there is no score to give — the model server or the issue is missing, or the
answer is outside its contract — both SHALL answer `unknown` with a why that starts
`<script>: no se pudo puntuar:` and names the cause. Neither SHALL install or start the model server:
the engine runs `.harness/aio/scorers/laya/laya ensure` before a score (loop-engine).

#### Scenario: No model server
- **WHEN** the laya server is not running and `proposal-review` runs `risk-score`
- **THEN** the score answers `outcome: unknown`, and its why says `no se pudo puntuar` and that the server was not reached, never that the risk came out high

#### Scenario: A one-line change
- **WHEN** `merge-review` runs `diff-risk` on a branch that changes one line of a comment, and no laya server is running
- **THEN** the score answers `outcome: low` with a why starting `small diff: 1 changed line in 1 file`

#### Scenario: A one-line change to a protected path
- **WHEN** the base's `.harness/protected-paths` names `src/*/Migrations/*` and the branch changes one line of a migration
- **THEN** `diff-risk` answers `outcome: high` with the why `protected: <the migration's path>`, and the model is not asked

#### Scenario: A change to the list
- **WHEN** the branch adds the path it touches to `.harness/protected-paths`, or removes it
- **THEN** `diff-risk` judges the diff by the list of the base: the path it added is not protected yet, and the one it removed still is

#### Scenario: A repository without the list
- **WHEN** the base has no `.harness/protected-paths` and the branch changes one line
- **THEN** `diff-risk` answers `outcome: low`, and its why ends saying no path is protected

### Requirement: laya keeps one environment per machine and pin
`.harness/aio/scorers/laya/laya` SHALL build its Python environment at
`~/.aiorchestrator/laya/<pin>/venv`, where `<pin>` is the first 12 hex characters of the SHA-256 of
the content of `.harness/aio/scorers/laya/laya-requirements.txt`, never of its path, and SHALL keep the server's pidfile and log in the same
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
