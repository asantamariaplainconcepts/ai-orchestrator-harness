# What this repository adds to the loop's commands

The six `/aio:*` commands in `.ai/commands/aio/` are the harness package's, the same files every
repository receives, and they name nothing of this repository. Each reads this file when it exists
(#618). Where this file speaks, it replaces what the command says; where it is silent, the command
stands. A repository without this file runs the commands as they are: the plan is a comment, the
retro is a comment, and nothing needs OpenSpec.

What is at the root of `.aio/` and does not carry the managed line this repository edits and the
package never overwrites, this file among them; every file that carries it is the package's, and
every update overwrites it.

## How this repository is built

- The code is arranged by `ARCHITECTURE.md`: five ports (`IPbiProvider`, `IAgentProvider`,
  `IEnvironment`, `ISession`, `IScriptRunner`) and modules with boundaries the build enforces. An
  idea or an issue lands in one port or inside one module, and says which. A change that crosses a
  port or a module boundary is a finding for `--problems`, never a quiet edit.
- Screens follow `DESIGN.md`, and their copy is read from `en.ts`, never hardcoded.
- An observation here looks like "the terminal pane is 80px at 560×700"; "the layout is broken" is
  not one. Issues here are written in Spanish, in the sections of `.aio/templates/issue.md`, and
  one without its `## Dónde cae` list holds in *Needs you* until a refine adds it.
- The gate is `aspire do ci`, and `verifying` runs `evidence` after it, which captures the screens a
  change touched and posts them on the issue, and then `quality`, which posts what the gate measured
  of the change and blocks nothing. The implement does not run `aspire do ci` either; it
  builds and runs the tests of the projects it touched and, for a screen, looks at it.

## The plan is an OpenSpec change on the branch, not a comment

`/aio:propose` writes an **OpenSpec change** on the branch instead of posting a `## Plan` comment,
and the Proposal tab renders it from the pull request the engine opens:

- Read the capabilities the issue touches: `openspec list --specs`, then
  `openspec show <id> --type spec`. The delta goes against an existing capability wherever one covers
  the ground; a new id is for behaviour with no home.
- Invoke the **`openspec-propose`** skill for the artifacts — proposal, design, spec deltas and
  tasks — seeded with the issue's number, intent, where it lands and its check.
- `openspec validate "<name>" --strict`. A change that does not validate is not finished.
- Commit the artifacts and push: `git push origin HEAD`. The commit message names the issue. Post no
  comment on the issue.
- When the branch already holds a change, this is a second pass: a person asked for changes in
  `proposal-review`. Their threads are review comments on the PR, anchored to a file and a line —
  `.aio/aio pr view <issue> --markdown` prints each under its `file:line`. Answer each one in
  the change, not in a reply.
- `tasks.md` may carry checkboxes where `AGENTS.md` forbids them, because it is one issue's own plan
  on its own branch. Every box in it is one `in-progress` ticks on this branch, before the merge, and
  `code-review` rejects a change with a box left open. So it holds no task to run the gate, which is
  `verifying`'s script and runs after the implement, and no check that needs the change merged, such
  as running the new command on a real issue: those stay in the issue's check section, where they
  already are.
- What stops you before the change validates is `blocked`, with the reason.

`/aio:implement` works the change's tasks, and the change is the plan:

- Read the change: `openspec status --change "<name>" --json`. Everything the schema requires before
  implementation is `done`, or the proposal is unfinished and that is a `blocked`. A person's
  findings are comments on the issue as well as on the PR:
  `.aio/aio issue view <issue> --markdown`.
- Invoke the **`openspec-apply-change`** skill to work the tasks, committing as you go. Tick each box
  in `tasks.md` as you do its task, in the same commit.
- Keep the spec delta true to what you built, because the archive folds it into `openspec/specs/`.
  Where the code proved a requirement wrong, edit the delta and say so in the commit.
- `next` is only for a `tasks.md` with every box ticked. A task left open, because it cannot be done
  on this branch or not without a person, ends the run blocked and names it, one for every open task:
  `.aio/aio done blocked --reason "open task <number>: <what stops it>"`.

`/aio:review-diff` reviews the diff against the change, not against a plan comment:

- Read the change on the branch, `openspec/changes/<name>/` — proposal, design, spec deltas and
  tasks — and the evidence comment on the issue when there is one,
  `.aio/aio issue view <issue> --markdown`.
  Look at the images: a screen that moved without the change saying so is a finding.
- A finding is also: a requirement of the spec delta the diff does not meet, or a task ticked that the
  diff does not do; a box left open in `tasks.md`, whatever the task, each one its own finding, with
  `--problems "tasks.md:<line> — open task <number>"`, because `merging` archives the change and the
  archive refuses one with open tasks; code outside where the issue says the change lands, or a port
  or module boundary crossed; a screen the evidence shows broken, or copy hardcoded instead of read
  from `en.ts`.
- Approving the PR on the vendor is the person's act, and it releases `merge-review`'s hold.

## The retro is a file on the branch, not a comment

`/aio:review` appends its entry to `docs/process/retro-log.md` on the branch, so the entry travels in
the same PR and the same squash as the code it is about, and posts no comment on the issue. Read the
change's `proposal.md` and `tasks.md` as well as the diff. The entry has the shape of the entries
above it — `## <date> — #<issue> <what it taught>`, then the paragraph — headed *Agent-written, not
confirmed by a person.* The log is append-only: never edit an earlier entry. Commit and push:
`git push origin HEAD`. When the change taught nothing, commit nothing and end with
`.aio/aio done next --reason "Nothing to add: <why>"`, as the command says.
