---
description: Implement an issue from its plan on its branch, push the code, and end.
argument-hint: <issue number or key>
---

Implement issue **$ARGUMENTS** on the branch and pull request its plan led to. The run starts in the
issue's worktree. The job ends when the code is written and pushed: the gate runs in `verifying`,
after this.

## Read

- The issue and its comments: `.aio/aio issue view $ARGUMENTS --markdown`. The latest
  comment headed `## Plan` is what to build. An issue with no plan is a `blocked`: `spec-ready` did not finish.
- What came back, when this is not the first pass. `code-review` and the person both send a PBI back
  here: the reviewer's findings are a comment on the pull request, and a person's are comments on
  the issue. `.aio/aio pr view $ARGUMENTS --markdown` shows both kinds on the pull
  request, each comment on a line under its `file:line`; answer each one in the code.
- `.aio/context.md`, when the repository has one: what this repository adds to this command.
  Where it speaks, it replaces what this file says; where it is silent, this file stands.

## Write

1. Work the plan, committing as you go. Do not squash — the branch is the record until the merge.
2. Stay where the plan says the change lands. A file the plan does not name is a finding: it goes
   in `--problems`, not quietly into the diff.
3. Where the code proved the plan wrong, change the code the way the code needs, and say what moved
   and why in the commit message.
4. Run what the plan's **How it is checked** names, and the tests you touched. Do not run the gate,
   `.aio/gate`: it is `verifying`'s and runs right after this run ends, and inside this
   session its tests would inherit this run's variables. A red gate in `verifying` holds the PBI and
   is not retried, so build and test what you changed before you end.
5. Push: `git push origin HEAD`.

## End

```bash
.aio/aio done next --problems "<a problem found and not fixed>"   # one --problems each, or none
```

`next` is only for a branch that does every line of the plan. A line left undone, because it cannot be
done on this branch or not without a person, ends the run blocked and names it, and the PBI holds with
that reason:

```bash
.aio/aio done blocked --reason "plan step not done: <the step> — <what stops it>"
```
