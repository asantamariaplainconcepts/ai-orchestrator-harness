---
description: Interrogate an issue to the Definition of Ready, and end with ready or gaps.
argument-hint: <issue number or key>
---

Take issue **$ARGUMENTS** to the Definition of Ready. Write no code — refining is the judgement this
state asks for, and the code is `in-progress`'s.

## Read

- The issue body and every comment on it: `.aio/aio issue view $ARGUMENTS --markdown`.
- Closed issues in the same area, which catch an issue asking for something already shipped:
  `.aio/aio issue list --state closed --search "<the area>"`.
- The code the issue would touch, and the repository's own `README.md`, `AGENTS.md` or
  `CONTRIBUTING.md` when it has them, for where the change would land.
- `.aio/context.md`, when the repository has one: what this repository adds to this command.
  Where it speaks, it replaces what this file says; where it is silent, this file stands.

## The Definition of Ready

The Definition of Ready is the repository's issue template: read `.aio/templates/issue.md`. An
issue is ready when each section of the template answers what its help line asks, from the issue
itself. The help lines are the HTML comments under each heading.

## Close what you can

A gap that the issue and the code already answer is closed in the issue body, and an issue that does
not follow the template is put in its sections, with its own text kept and the help lines left out.
Edit the live body — the body as `.aio/aio issue view $ARGUMENTS --markdown` prints it,
between the lines about the issue and the first comment's `---`, written to `.aio/body.md`, then
`.aio/aio issue edit $ARGUMENTS --body-file .aio/body.md` — so its image URLs survive.

A gap only the person who opened it can close stays open for them. Never a bare "needs more detail" —
the point of this state is that the next person does not have to guess.

## Say what you found

Every run that reaches a verdict, `ready` or `gaps`, leaves exactly one comment on the issue before it
ends, so whoever opens it next can see how it got where it is. Write it to `.aio/comment.md` and
post it with `.aio/aio issue comment $ARGUMENTS --body-file .aio/comment.md`. It
says what you closed in the body, if anything; what is left for the person who opened the issue,
addressed to them, naming each gap and what would close it; or, when you changed nothing, that every
section already answered its help line. This is the run's only comment: what you added and what you
left go in it together, never in two.

## End

The run ends with its outcome, and the loop moves the issue from there:

```bash
.aio/aio done ready                                  # every section answers its help line
.aio/aio done gaps --reason "<the gaps, in one line>"   # a person has to answer first
```

Something that stops you before a verdict — the issue cannot be read, the vendor refuses — is
`.aio/aio done blocked --reason "<what stopped you>"`.
