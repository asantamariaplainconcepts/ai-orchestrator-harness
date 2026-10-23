---
description: Review a PR's diff against its plan and its issue, and end with approved or rejected.
argument-hint: <issue number or key>
---

Review the pull request of issue **$ARGUMENTS** against what was asked and what was planned. The gate
is already green: what is left is judgement — whether the diff does what the plan says, the plan
does what the issue asks, and nothing else is in it.

## Read

- The issue and its comments: `.aio/aio issue view $ARGUMENTS --markdown`. What it
  asks, and the latest comment headed `## Plan`.
- The diff, `.aio/aio pr diff $ARGUMENTS`, and
  `.aio/aio pr view $ARGUMENTS --markdown` for what earlier rounds already said.
- The newest comment on the issue that opens with `<!-- aio:quality <sha> -->`, when there is one:
  what `quality` measured of the change against its line, each check `ok`, `over` or
  `not measured`. It reports and decides nothing. Cite a row in a finding when it shows the diff
  doing what the issue or the plan says it must not, such as a test lost; a row `over` alone is
  not a finding, and a measure of another commit than the head says so.
- `.aio/context.md`, when the repository has one: what this repository adds to this command.
  Where it speaks, it replaces what this file says; where it is silent, this file stands.

## Judge

A finding is something the author would have to change, with the file and line:

- Something the issue asks or the plan promises that the diff does not do. Each line of the plan the
  branch leaves undone is its own finding and the change is rejected, whatever the reason.
- A file changed that the plan does not name, without a commit that says why.
- A test that does not test what its name claims, or behaviour the plan's check names with no test.
- Code that breaks what the repository already does, read from the diff and the files around it.

Taste is not a finding. Neither is something the plan deliberately leaves out and says so.

## Write

Write the review to `.aio/review.md` and post it as one comment on the pull request — the
coder reads it on the next pass:

```bash
.aio/aio pr review $ARGUMENTS --verdict comment --body-file .aio/review.md
```

One finding per item, each with its file and line and what would fix it. A comment, never an
approval: approving the pull request is a person's act.

## End

```bash
.aio/aio done approved                                    # no findings
.aio/aio done rejected --problems "<file:line — finding>"   # one --problems per finding
```

A plan step left undone is rejected like any finding, with `--problems "plan step not done: <the step>"`,
and the loop sends the PBI back to `in-progress`.

Something that stops the review itself — no pull request, a diff that cannot be read — is
`.aio/aio done blocked --reason "<what stopped you>"`.
