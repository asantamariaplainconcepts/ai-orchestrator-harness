---
description: Write a short plan for an issue as a comment on it, and end.
argument-hint: <issue number or key>
---

Turn issue **$ARGUMENTS** into a **plan**: what changes, where, and how it is checked. The run starts
in the issue's worktree on the branch `claim` already pushed; there is nothing to claim, branch or
open here. Write no implementation — that is `in-progress`'s.

## Read

- The issue and its comments: `.aio/aio issue view $ARGUMENTS --markdown`.
- The code the issue would touch, enough to name the files. The repository's own `README.md`,
  `AGENTS.md` or `CONTRIBUTING.md`, when it has them, say how it is built and tested.
- When the issue already has a comment headed `## Plan`, this is a second pass: somebody asked for a
  different plan. Read what was said after it and answer it in the new plan.
- `.aio/context.md`, when the repository has one: what this repository adds to this command.
  Where it speaks, it replaces what this file says; where it is silent, this file stands.

## Write

The plan is short, and every line of it is something `in-progress` can act on and the review can
check:

```markdown
## Plan

**What changes.** One paragraph: the behaviour before and after, in the issue's words.

**Where.** The files or folders that change, one per line, each with what changes in it.

**How it is checked.** The command, test or screen that shows it works, and what it should show.

**Not in this change.** The nearest thing the issue does not ask for.
```

Every line of the plan is something `in-progress` does on this branch, before the merge, and the
review rejects a branch that leaves one undone. So **How it is checked** names no step to run the
gate, which runs in `verifying` after the implement, and no check that needs the change merged: those
stay in the issue, where the person runs them afterwards.

Write it to `.aio/comment.md` and post it as one comment on the issue, where `in-progress` and
the review read it:

```bash
.aio/aio issue comment $ARGUMENTS --body-file .aio/comment.md
```

The engine opens the pull request when this run ends, and its body closes the issue, so the plan is
one click from the diff.

## End

```bash
.aio/aio done next
```

What stops you before there is a plan — the issue cannot be read, it asks for two unrelated things —
is `.aio/aio done blocked --reason "<what stopped you>"`, and the reason is what the person
reads.
