# adopt-the-harness Specification

## Purpose
TBD - created by archiving change adopt-harness-trunk-guard. Update Purpose after archive.
## Requirements
### Requirement: The harness is adopted from one manifest, copying files that never overwrite
The system SHALL expose `POST /api/folders/{id:guid}/harness/adopt` (`AdoptHarness.cs`) that copies the
harness to the folder's checkout from a single manifest that travels with the server's build. The manifest
SHALL be an explicit list — `.harness/` (the loop with its `gate` node emptied, the tripwires,
`protected-paths`), `.ai/commands/aio/` (the nine commands), the skills those commands name, the five
runtime links pointing at `.ai/` (`.claude/commands`, `.claude/skills`, `.agents/skills`,
`.opencode/command`, `.opencode/skills`), and `.githooks/pre-commit`. Nothing SHALL be deduced by walking
folders: a derived list is a list that disagrees with what shipped. A file that already exists SHALL be
skipped, and the response SHALL name every skipped file, because never overwriting is the adoption's whole
safety. Everything SHALL be left uncommitted in the checkout — never in a worktree — for a person to review
with `git diff` and commit like any other change. The `gate` node's command SHALL arrive empty: it runs
this repository's gate, and the adopted loop's editor already refuses to save an empty step, so the
receiving repository must fill in its own. The command SHALL NOT copy `commands.json` or `config.json`,
because declared commands are each repository's own.

#### Scenario: A folder without a harness adopts one
- **WHEN** the adopt call runs against a checkout with none of the manifest's files
- **THEN** every manifest file lands uncommitted in the checkout, and `git status` names exactly them

#### Scenario: An existing file is skipped and named
- **WHEN** the checkout already holds a file the manifest would copy
- **THEN** that file is untouched, and the response names it in the skipped list

#### Scenario: Re-adopting changes nothing
- **WHEN** the adopt call runs a second time against the same checkout
- **THEN** every manifest file is skipped, and nothing on disk changes

#### Scenario: The gate arrives empty for its new owner to fill
- **WHEN** the copied loop declares its gate node
- **THEN** the node's command is empty, and the loop editor marks the step and refuses to save until that repository's own gate fills it

### Requirement: Adopting installs the trunk guard as part of the manifest
The manifest's `.githooks/pre-commit` SHALL be the same file `IRepositoryGuard` writes, and adoption
SHALL run `EnsureInstalled` after the copy — the adopted folder is born with the rule. The hook SHALL
refuse a commit whose branch resolves to `master` or `main`, with the remedy in its message; a detached
HEAD SHALL pass, because rebase and cherry-pick in a worktree are legitimate work; an environment
carrying `AIORCH_UNGUARDED=1` SHALL pass, as the deliberate, verbose escape. A `core.hooksPath` that
already points elsewhere SHALL NOT be overwritten — the response names the conflict and the way out
instead.

#### Scenario: Committing on the trunk is refused with its remedy
- **WHEN** a commit is made while `master` is checked out in the adopted checkout
- **THEN** the hook exits non-zero, and its message names a worktree command as the remedy

#### Scenario: Committing on a branch passes
- **WHEN** a commit is made anywhere other than `master` or `main`, including a detached HEAD
- **THEN** the hook exits zero

#### Scenario: The escape is an environment variable, deliberately verbose
- **WHEN** the environment carries `AIORCH_UNGUARDED=1`
- **THEN** the hook exits zero, and its refusal message names the escape

#### Scenario: A hooks delegation that is not ours is left alone
- **WHEN** the checkout's `core.hooksPath` already points at another tool's hooks
- **THEN** adoption does not overwrite it, and the response says what was found and what to do

#### Scenario: A worktree inherits the rule
- **WHEN** a worktree is added beside an adopted checkout
- **THEN** commits in it hit the same hook, because git resolves the shared `core.hooksPath` from each worktree's root

### Requirement: Missing OpenSpec is named by the adoption, not discovered later
The adopt response SHALL say whether the checkout holds `openspec/`, because `/aio:propose` needs it.
The copy SHALL NOT create it — an adoption that invented a spec tree would be a spec tree nobody wrote.

#### Scenario: No openspec is reported, not fabricated
- **WHEN** the adoption runs against a checkout with no `openspec/` directory
- **THEN** the response says so, and no `openspec/` directory is created

### Requirement: The harness's state is verified, not repaired
The system SHALL expose `GET /api/folders/{id}/harness/verify` (`VerifyHarness.cs`) that walks the manifest
against the checkout and the trunk guard, and answers a list of checks — every manifest file present,
every runtime link resolving, `.githooks/pre-commit` installed, `core.hooksPath` pointing at it,
`openspec/` present, the loop's `gate` not empty. Every check SHALL say ok or missing with a remedy.
The verify SHALL NOT repair, write or copy anything: repairs belong to adoption and to the startup walk,
and a button that touches nothing needs no undo. The verify SHALL work on any folder, adopted or not —
a folder that never adopted answers a list whose remedy is adoption.

#### Scenario: A complete adoption verifies clean
- **WHEN** verify runs against a checkout holding every manifest file and an installed guard
- **THEN** every check answers ok

#### Scenario: A deleted file is named with its remedy
- **WHEN** a manifest file was deleted after adoption and verify runs
- **THEN** the check for that file answers missing, and its remedy names adoption

#### Scenario: The uninstalled guard is named with its remedy
- **WHEN** `.githooks/pre-commit` was removed or `core.hooksPath` no longer points at it
- **THEN** the guard's check answers missing, and its remedy names the guard

#### Scenario: The empty gate is named
- **WHEN** the adopted loop's `gate` node still carries an empty command
- **THEN** the gate check answers missing, and its remedy names the loop editor

#### Scenario: Verify never writes
- **WHEN** verify runs against any checkout
- **THEN** no file is written, changed or deleted by the verify itself

