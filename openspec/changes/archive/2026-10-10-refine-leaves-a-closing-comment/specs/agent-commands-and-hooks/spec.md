## ADDED Requirements

### Requirement: The refiner leaves one closing comment on every verdict
`/aio:refine`, in this repository's `.ai/commands/aio/refine.md` and in the package's
`harness/commands/refine.md`, SHALL post exactly one comment on the issue, with `gh issue comment`,
before it ends with `aio done ready` or `aio done gaps`. The comment SHALL say what the run closed by
editing the body, which gaps are left for the person who opened the issue and what would close each, or
that all five answers of the Definition of Ready were already in the issue. No other instruction in the
command SHALL ask for a comment, so a run that edits the body and leaves a gap still posts one. A run
that ends `blocked` SHALL NOT be required to comment.

#### Scenario: A ready issue whose body needs no edit
- **WHEN** the refiner runs on an issue that already meets the Definition of Ready and ends with `aio done ready`
- **THEN** `gh issue view <n> --comments` shows one closing comment from the run, besides the journal lines

#### Scenario: A run that ends in gaps
- **WHEN** the refiner closes one gap in the body and leaves another for the person who opened the issue
- **THEN** the issue gains one comment, addressed to that person, naming both, and not two

#### Scenario: The command is checked
- **WHEN** `AgentCommandTests` reads `refine.md`
- **THEN** it finds `gh issue comment` in a section before `## End`

## MODIFIED Requirements

### Requirement: The allow list is versioned with the hook
`.claude/settings.json` SHALL allow, for the Bash tool, `aio done` and `.harness/scripts/aio done`,
`git`, `dotnet`, `aspire`, `pnpm`, `node`, `gh` commands that only read (`issue view`, `issue list`,
`pr view`, `pr list`, `pr diff`, `pr checks`, `run list`, `run view`, `repo view`), and `gh issue
comment`, which writes one comment and moves nothing the engine owns. It SHALL deny
`git commit --no-verify`, `git push --no-verify` and `gh pr merge`, and SHALL NOT allow `gh` as a whole
or `gh api`. Whatever the list does not name asks, which an unattended run cannot answer.

#### Scenario: A run ends with aio done
- **WHEN** the coder's run calls `aio done approved`
- **THEN** it runs without a prompt, while `gh pr merge 42` is denied by the list and refused by the hook

#### Scenario: The refiner comments in allow-list
- **WHEN** a refine run in the `allow-list` permission mode runs `gh issue comment 575 --body-file <file>`
- **THEN** it runs without a prompt
