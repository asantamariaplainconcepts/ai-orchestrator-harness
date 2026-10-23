---
description: Interrogate an idea with the person until it is an issue, or an epic of issues, at the Definition of Ready.
argument-hint: <the idea, in a few words>
---

Turn the idea **$ARGUMENTS** into one issue, or into an epic with its sub-issues when it is too big
for one change, with the person in the session. This is a conversation, not a state of the loop: the
person answers, and nothing is created until they say so. Write no code.

## Read

- The code the idea would touch, and the repository's own `README.md`, `AGENTS.md` or
  `CONTRIBUTING.md` when it has them.
- Open and closed issues in the same area, so the idea is not one already shipped or already asked:
  `.aio/aio issue list --state all --search "<the area>"`.
- `.aio/context.md`, when the repository has one: what this repository adds to this command.
  Where it speaks, it replaces what this file says; where it is silent, this file stands.

## Ask

The Definition of Ready is the repository's issue template: read `.aio/templates/issue.md`. An
issue is ready when each section of the template answers what its help line asks; the help lines are
the HTML comments under each heading. Ask one cluster of questions at a time, until every section has
its answer, and write the body in the template's sections with the help lines left out.

Answer what the code already answers yourself, and say where you read it.

## One issue or an epic

An idea that fits one change is one issue. One that does not is an epic: say so, propose where you
would cut it, and ask. The epic follows `.aio/templates/epic.md` as an issue follows its
template, each section answering its help line. Each sub-issue is an issue of
`.aio/templates/issue.md`, ready on its own, whose parent section names the epic.

The cut aims at parallelism, because the loop works at the same time the issues that share no file.
Write each sub-issue's list of where it lands first, then cut so that as many sub-issues as possible
share no file. Two share a file when one path ends with the other on a `/` boundary, or one is a
folder the other sits under: the rule `file-overlap` applies when they enter the loop, said before
they exist. When two share one, say which file, put them in order, and draw an arrow in the graph from
the one that goes first to the one that waits. The graph's nodes are `i<number>` for each sub-issue
and `e<number>` for the epic, and the line under the block says that an arrow means the second waits
for the first. Until they exist, number them in the plan's order, `i1`, `i2` and so on. An arrow
comes from a shared file and from nothing else: the cut does not decide priorities or estimate.

## Create

Show the person everything that would be created, every title and every body: the issue, or the epic
and each of its sub-issues. Create only when they confirm, because an issue is shared state; what they
change is shown again before anything is created.

One issue:

```bash
.aio/aio issue create --title "<title>" --body-file .aio/issue.md
```

An epic, in this order, reading each number from the `id` of the `issue` the command answers:

- The epic: `.aio/aio issue create --title "<title>" --body-file .aio/epic.md`.
- Each sub-issue, its parent section naming the epic's number, filed under it:
  `.aio/aio issue create --title "<title>" --parent <epic> --body-file .aio/issue.md`.
  The body file is removed once posted, so write it again for each one.
- The epic's body again, with the real numbers in the graph and in the table:
  `.aio/aio issue edit <epic> --body-file .aio/epic.md`.

When a call fails, stop: say what was created and what was not, and create nothing twice.

## Into the loop

What is created carries no status label. With an epic, ask whether to move its sub-issues into the
loop, all of them or the ones the person names:

```bash
.aio/aio issue move <n> ready-for-proposal
```

The rest stay without a label, as a single issue does: putting it in the loop is the person's
decision, made in the vendor or on the PBI's screen. The epic moves to no state.

## End

Say the number and link of each issue created, the epic first, and stop.
