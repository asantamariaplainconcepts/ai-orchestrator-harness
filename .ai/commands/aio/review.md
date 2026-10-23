---
description: Comment on the issue what a change taught, before the merge, and end.
argument-hint: <issue number or key>
---

Issue **$ARGUMENTS** is reviewed and about to merge. Write down what it taught as a comment on the
issue, which every repository has and which stays one click from the pull request that closes it.

## Read

- The diff, `.aio/aio pr diff $ARGUMENTS`, and the review conversation,
  `.aio/aio pr view $ARGUMENTS --markdown`.
- The issue and its comments, particularly the `## Plan` and where what was observed differs from what
  the plan expected: `.aio/aio issue view $ARGUMENTS --markdown`.
- `.aio/context.md`, when the repository has one: what this repository adds to this command.
  Where it speaks, it replaces what this file says; where it is silent, this file stands.

## Write

An entry is worth writing when it is one of these, and not otherwise:

- **A bug the tests did not catch**, with the reason they did not. The reason is the entry; the bug
  is the example.
- **A case the plan did not have**, found by running the thing.
- **A place where the code already in the repository was right and this was not**, or the reverse.

Not a summary of the change — the diff is the summary. Not a resolution ("be more careful"). Not an
entry at all if the change taught nothing: an honest empty retro is worth more than filler.

When there is one, write it to `.aio/comment.md`, headed `## Retro`, then the paragraph, then
*Agent-written, not confirmed by a person.*, and post it as one comment on the issue:

```bash
.aio/aio issue comment $ARGUMENTS --body-file .aio/comment.md
```

When `.aio/context.md` puts the retro in a file on the branch instead, write the entry there with
your editor, then commit that one file and push it, as `/aio:implement` does:

```bash
git add <the file>
git commit -m "<what the change taught, in one line>"
git push origin HEAD
```

Commit nothing else: the rest of the branch is the one the review read.

## End

```bash
.aio/aio done next                                         # with a comment
.aio/aio done next --reason "Nothing to add: <why>"        # without one
```
