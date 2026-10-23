# agent-commands-and-hooks Specification

## Purpose
An agent state's command does the judgement and nothing else: it reads what the state hands it, writes
its part, pushes, and says its outcome. What touches the vendor is the engine's, and the rules an agent
could break by typing are refused by hooks rather than written in its prompt. Reverse-engineered from
the shipped code (`.ai/commands/aio/`, `.claude/hooks/pre-tool-use`, `.githooks/pre-commit`,
`src/tests/AiOrchestrator.Modules.Loop.FunctionalTests/AgentCommandTests.cs`), not negotiated ahead of
it.
## Requirements
### Requirement: One command per agent state, in one place for every runtime
The commands SHALL live in `.ai/commands/aio/` — `refine`, `propose`, `implement`, `review-diff`,
`review` and `explore` — symlinked into `.claude/commands/`, `.opencode/command/` and `.github/prompts/`
so every runtime reads the same text. Each state of this repository's loop that runs a `/aio:*` command
SHALL have its file: `/aio:refine`, `/aio:propose`, `/aio:implement`, `/aio:review-diff` and
`/aio:review`. `/aio:explore` SHALL be no state's: it turns an idea into an issue, or an epic of
issues, with a person in the session.

#### Scenario: A state names a command with no file
- **WHEN** the loop names `/aio:x` and `.ai/commands/aio/x.md` does not exist
- **THEN** `AgentCommandTests` fails the gate

### Requirement: A command ends by saying an outcome its state knows
Each state's command SHALL end with a section `## End` that names, with `.aio/aio done`,
every outcome its state's `on` knows (or `next`), and no `aio done` anywhere in the file SHALL name an
outcome the state does not know, `blocked` aside, which any run may say.

#### Scenario: The review command's outcomes
- **WHEN** `review-diff.md` is checked against `code-review`
- **THEN** its End names `aio done approved` and `aio done rejected`

### Requirement: An open task stops the change before the merge
`/aio:implement` SHALL end with `next` only for a branch that does every line of the plan; a step left
undone SHALL end the run with `aio done blocked` and a reason that names it, so the PBI holds with
that reason. `/aio:review-diff` SHALL take every step left undone as a finding and end with
`aio done rejected`, one `--problems` per step, which sends the PBI back to `in-progress` within
`code-review`'s `max_loops`. `/aio:propose` SHALL put in the plan no step to run the gate, which is
`verifying`'s, and no check that needs the change merged, which stays in the issue's check section
(#588). In this repository the plan is an OpenSpec change, and `.aio/context.md` SHALL say the
same of `tasks.md`: the implement ticks each box as it does the task and ends `next` only when every
box is ticked, naming each open one in its `blocked` reason; the review takes each open box as its
own finding, `tasks.md:<line> — open task <number>`; and the archive in `merging` SHALL stay the last
barrier and still refuse a change with open tasks (#618).

#### Scenario: The implement cannot do a task on its branch
- **WHEN** `tasks.md` still has `- [ ] 3.3 Run aspire do ci` at the end of an implement run
- **THEN** the run ends `blocked` with a reason naming task 3.3, not `next`

#### Scenario: The review finds an open box
- **WHEN** `code-review` reads a change whose `tasks.md` has two open boxes
- **THEN** it ends `rejected` with two problems, each naming `tasks.md`, its line and the task

### Requirement: The implement does not run the gate
`/aio:implement` SHALL tell the agent not to run the gate, `.aio/gate`, and no command
block in it SHALL run it; `.aio/context.md` SHALL say it of `aspire do ci` by name. The gate is
`verifying`'s and runs right after the implement; run inside the agent's session, its tests inherit
the run's variables, and one wrote `merged` into the run's own end record (#590). The implement builds
and tests what it touched.

#### Scenario: An implement command that runs the gate
- **WHEN** `implement.md` gains a command block with `aspire do ci`
- **THEN** `AgentCommandTests` fails, naming the file

### Requirement: A command never does what the engine does
No command file SHALL name `.aio/steps/claim`, `.aio/steps/feedback`, `gh pr ready`, `gh pr
merge`, `gh pr create`, `--approve`, `--verdict approve`, `--verdict request-changes`, `--add-label`,
`--remove-label` or `push-session-report`: moving a
label, opening, readying, approving or merging a pull request, claiming and pushing telemetry are the
engine's at the transition, and a command doing them as well would race it.

#### Scenario: A command that opens its own pull request
- **WHEN** `propose.md` gains a line with `gh pr create`
- **THEN** `AgentCommandTests` fails, naming the file, the line and the word

### Requirement: The retro asks for the commit it makes
When `.aio/context.md` puts the retro in a file on the branch, `/aio:review` SHALL itself ask for
that commit: a command block that adds the one file with `git add`, commits it with `git commit` and
pushes it with `git push origin HEAD`, the push `/aio:implement` names. It SHALL commit nothing else.
Auto mode's classifier judges a commit against the command the person ran, not against a file the
agent read: the retros of #757 and #758 were denied as an unrequested commit while `review.md` said to
commit nothing and only `.aio/context.md` asked for one. No allow rule is added for it, and
`.claude/settings.json` and the hook stay as they are.

#### Scenario: A retro in auto mode
- **WHEN** a PBI of a repository whose `.aio/context.md` puts the retro in a file reaches `retro`, and its run is in the `auto` permission mode
- **THEN** the entry is committed and pushed to `change/<n>` with no denial, and the run ends with `next`

#### Scenario: The command is checked
- **WHEN** `AgentCommandTests` runs
- **THEN** `review.md` has one command block with `git commit`, which adds one file and pushes with `git push origin HEAD`, and no line of it says to commit nothing at all

### Requirement: The agent's hook refuses what would skip the gate or merge
`.claude/hooks/pre-tool-use`, registered for the Bash tool in `.claude/settings.json`, SHALL read the
command line the agent typed, split as a shell splits it and read again inside `bash -c`, `sh -c`,
`zsh -c` and `eval`, and SHALL refuse, with exit 2 and a sentence saying what to do instead:
`git commit --no-verify` or `-n`, `git push --no-verify`, `git -c core.hooksPath=…` with `commit` or
`push`, `gh pr merge`, and a `gh api` call that merges a pull request. Text inside quotes SHALL be an
argument, so a commit message that mentions `gh pr merge` passes, and the body of a heredoc (`<<EOF`,
`<<'EOF'`, `<<-EOF`) SHALL be text, so a file written with `cat <<'EOF'` may mention any command (#731).
What the hook cannot read SHALL pass: it is a guard rail, not a sandbox. `.aio/steps/merge` SHALL
still merge, because a script's own processes are not the agent's tool calls.

#### Scenario: An agent tries to merge
- **WHEN** an agent runs `gh pr merge 42 --squash`
- **THEN** nothing runs, and the agent reads that merges go through `.aio/steps/merge`

#### Scenario: A heredoc mentions a refused command
- **WHEN** an agent runs `cat > notes.md <<'EOF'` with a body that mentions `git commit --no-verify`
- **THEN** the hook exits 0 and the file is written

### Requirement: The agent's hook refuses what treads on another session's work
A worktree, its stash stack and its remote can be shared with another session: on 26 September a
`git add -A` in a shared checkout took another session's work. The hook SHALL refuse, with exit 2, a
sentence that says what the command breaks and what to run instead, and `Nothing was run.`:
`git add -A`, `--all` or `.` (stage named paths); `git commit -a`, `--all` or a cluster with `a` such
as `-am` (stage named paths first); `git reset --hard` (revert the touched paths with
`git checkout -- <path>`); `git clean` without `-- <paths>`, a dry run aside (scope it with
`git clean -f -- <path>`); `git checkout .` and `git restore .`; `git stash drop` and `git stash clear`;
`git push --force` or `-f` without `--force-with-lease`; and `git push` of the remote's default branch,
read as `refs/remotes/<remote>/HEAD` in the call's `cwd`, named in a refspec or pushed as the branch
the worktree is on (push the work branch). A remote with no `HEAD` says nothing and the push passes.
The harness's scripts call git themselves and never reach the hook, so `merge`, `claim` and `cleanup`
work as before (#731).

#### Scenario: An agent stages everything
- **WHEN** an agent runs `git add -A`
- **THEN** nothing runs, and the agent reads `git add <path> <path>`

#### Scenario: The work stays possible
- **WHEN** an agent runs `git add src/a.cs src/b.cs`, `git push --force-with-lease`, `git clean -f -- tmp/` or `git push origin change/12`
- **THEN** the hook exits 0

#### Scenario: A push from the default branch
- **WHEN** an agent on `master`, whose `origin/HEAD` is `origin/master`, runs `git push`
- **THEN** nothing runs, and the agent reads to push its work branch

### Requirement: The agent's hook refuses a WebFetch of the vendor
`.claude/settings.json` SHALL register the hook for the WebFetch tool too, and the hook SHALL refuse a
WebFetch whose host is, or is under, `github.com`, `dev.azure.com`, `visualstudio.com`,
`atlassian.net` or `gitlab.com`, saying that issues and pull requests go through
`.aio/aio issue` and `.aio/aio pr`, and anything else through the vendor's CLI. Any
other host SHALL pass (#731).

#### Scenario: A fetch of an issue page
- **WHEN** an agent fetches `https://github.com/octo/harness/issues/731`
- **THEN** nothing is fetched, and the agent reads that issues go through `.aio/aio issue`

#### Scenario: A fetch of documentation
- **WHEN** an agent fetches `https://learn.microsoft.com/en-us/dotnet/aspire/`
- **THEN** the hook exits 0

### Requirement: The allow list is versioned with the hook
`.claude/settings.json` SHALL allow, for the Bash tool, `.aio/aio done`,
`.aio/aio issue` (#634), `.aio/aio pr` (#651), and no rule for a bare `aio`,
which is also the app's .NET tool (#678), `git`, `dotnet`, `aspire`, `pnpm`, `node`, and `gh` commands that only read (`pr list`, `run list`,
`run view`, `repo view`). It SHALL NOT allow any `gh issue` command, nor `gh pr view`, `gh pr diff` or
`gh pr checks`: the commands reach their issue through `aio issue` and its pull request through
`aio pr`. It SHALL deny `git commit --no-verify`, `git push --no-verify` and `gh pr merge`, and SHALL
NOT allow `gh` as a whole or `gh api`. Whatever the list does not name asks, which an unattended run
cannot answer.

#### Scenario: A run ends with aio done
- **WHEN** the coder's run calls `.aio/aio done approved`
- **THEN** it runs without a prompt, while `gh pr merge 42` is denied by the list and refused by the hook

#### Scenario: The reviewer posts its review in allow-list
- **WHEN** a review-diff run in the `allow-list` permission mode runs `.aio/aio pr review 7 --verdict comment --body-file .aio/review.md`
- **THEN** it runs without a prompt

#### Scenario: The refiner comments in allow-list
- **WHEN** a refine run in the `allow-list` permission mode runs `.aio/aio issue comment 575 --body-file .aio/comment.md`
- **THEN** it runs without a prompt

### Requirement: Nobody commits on the trunk
`.githooks/pre-commit`, the hooks path of this repository, SHALL refuse a commit on `master` or `main`,
saying to commit on a branch in a worktree, unless `AIORCH_UNGUARDED=1` is set on purpose.

#### Scenario: A commit on master
- **WHEN** somebody commits on `master` in this repository
- **THEN** the commit is refused with the command that creates a worktree on a branch

### Requirement: The refiner leaves one closing comment on every verdict
`/aio:refine`, in `.ai/commands/aio/refine.md`, which is the package's since #618, SHALL post exactly
one comment on the issue, with `aio issue comment`,
before it ends with `aio done ready` or `aio done gaps`. The comment SHALL say what the run closed by
editing the body, which gaps are left for the person who opened the issue and what would close each, or
that every section of `.aio/templates/issue.md` already answered its help line. No other instruction in the
command SHALL ask for a comment, so a run that edits the body and leaves a gap still posts one. A run
that ends `blocked` SHALL NOT be required to comment.

#### Scenario: A ready issue whose body needs no edit
- **WHEN** the refiner runs on an issue that already meets the Definition of Ready and ends with `aio done ready`
- **THEN** `aio issue view <n> --markdown` shows one closing comment from the run, besides the journal lines

#### Scenario: A run that ends in gaps
- **WHEN** the refiner closes one gap in the body and leaves another for the person who opened the issue
- **THEN** the issue gains one comment, addressed to that person, naming both, and not two

#### Scenario: The command is checked
- **WHEN** `AgentCommandTests` reads `refine.md`
- **THEN** it finds `aio issue comment` in a section before `## End`

### Requirement: The commands reach their issue through aio issue and its pull request through aio pr
None of the six commands in `.ai/commands/aio/`, nor this repository's `.aio/context.md`, SHALL
name `gh issue`, `gh pr` or `gh api`: each reads and writes its issue with `.aio/aio issue`
and reads and reviews its pull request with `.aio/aio pr` (#651), which go through the
control plane and the folder's vendor adapter, so the commands read the same on any vendor.
`refine`, `propose`, `implement`, `review-diff` and `review` SHALL read their issue with `aio issue view
<n> --markdown`, `refine` taking the live body it edits from that text, and none SHALL need `python3` or
`jq` to read it (#710). `implement`, `review-diff` and `review` SHALL read the pull request with `aio pr view <issue>
--markdown`, `review-diff` and `review` the diff with `aio pr diff <issue>`, and `review-diff` SHALL
post its findings with `aio pr review <issue> --verdict comment --body-file .aio/review.md`. Every
body an agent posts SHALL be a literal path under `.aio/`, never a shell variable, which would stop
an unattended run at a permission prompt (#644).

#### Scenario: The commands are checked
- **WHEN** `AgentCommandTests` reads the six commands and `.aio/context.md`
- **THEN** no line names `gh issue`, `gh pr` or `gh api`, each command names `.aio/aio issue`, implement, review-diff and review name `.aio/aio pr`, and every `--body-file` is a literal path under `.aio/`, and every `aio issue view` in refine, propose, implement, review-diff and review carries `--markdown`, with no `python3` and no `jq`

### Requirement: Explore cuts an idea too big for one change into an epic
`/aio:explore` SHALL end in one of two ways: one issue written with `.aio/templates/issue.md`, or,
when the idea does not fit one change, an epic written with `.aio/templates/epic.md` whose
sub-issues are each written with `issue.md` and name the epic in their parent section. The command
SHALL say the idea is an epic and propose the cut before writing it. The cut SHALL aim at parallelism:
each sub-issue declares where it lands, and the cut leaves as many sub-issues as possible sharing no
file, by the rule `file-overlap` applies (one path ends with the other on a `/` boundary, or one is a
folder the other sits under). Two that share a file SHALL be put in order, and the epic's graph SHALL
draw an arrow from the first to the one that waits, with nodes `i<number>` and a line saying that an
arrow means the second waits for the first. An arrow SHALL come from a shared file only: the command
decides no priority and estimates nothing (#635).

Nothing SHALL be created until the person confirms the whole plan, every title and body. Then the
command SHALL create the epic with `aio issue create`, each sub-issue with `aio issue create --parent
<epic>`, and rewrite the epic's body with `aio issue edit <epic>` so the graph and the table carry the
real numbers. Moving the sub-issues into the loop SHALL be a question of its own, asked after: those
the person names go to `ready-for-proposal` with `aio issue move <n> ready-for-proposal`, and the rest,
the epic and a single issue stay without a status label.

#### Scenario: The command is checked
- **WHEN** `AgentCommandTests` reads `explore.md`
- **THEN** it names both templates, `file-overlap`, the nodes `i<number>` and what an arrow means, and
  says to create the epic, then each sub-issue with `--parent <epic>`, then edit the epic, then ask to
  move them, in that order

#### Scenario: Two sub-issues share a file
- **WHEN** an idea is cut into an API change and a screen that both touch the same contracts file
- **THEN** the plan says which file, the graph has an arrow from the one that goes first to the other,
  and every sub-issue that shares no file has no arrow into it from another sub-issue
