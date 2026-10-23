## Context

The refiner's command is prose an agent follows; the loop runs it unattended and moves the issue on
`aio done ready` or `gaps`. Today the command asks for a comment in two places, each tied to an action
(editing the body, leaving a gap), so a run that does neither is silent on the issue. Since #582 the
instance's permission mode defaults to `allow-list`, where `claude` reads this repository's
`.claude/settings.json` and anything not on its allow list asks, which an unattended run cannot answer.
`gh issue comment` is not on it.

## Goals / Non-Goals

**Goals:**
- Every refine run that reaches a verdict leaves exactly one comment saying what it found.
- The comment goes out in `allow-list` without a prompt.
- Both refine commands, this repository's and the package's, say the same thing about it.

**Non-Goals:**
- The engine posting the comment. A command does the judgement; the comment's content is that
  judgement, and the engine cannot know what the run closed.
- `gh issue edit` on the allow list. It can `--add-label`, which is the engine's; a body edit in
  `allow-list` keeps asking as it does today.
- Any change to `blocked`, to the DoR, or to the other commands.

## Decisions

**One closing comment replaces the two per-action comments.** The command gains a section before End
that says: post one comment, whatever the outcome, with what the run closed in the body, what is left
for the person who opened it (addressed to them, naming each gap and what would close it), or that the
five were already answerable. Folding the two existing instructions into it is what keeps a `gaps` run
at one comment. Alternative considered: keep the two and add a third for the silent case only;
rejected because a run that edits the body and leaves a gap would still post two.

**`gh issue comment … --body-file <file>`**, as `harness/commands/propose.md` and `review.md` already
do, so a multi-line comment survives the shell.

**Allow `Bash(gh issue comment:*)`, and nothing broader.** It writes one comment and can move no
label, open no pull request and merge nothing, so it does not cross what the engine owns. The package
embeds `.claude/settings.json` from this repository's path, so the line reaches installed repositories
with no second edit.

**The test reads the files, as `AgentCommandTests` already does.** It asserts that `refine.md` names
`gh issue comment` in a section before `## End`, and that the allow list in `.claude/settings.json`
names `Bash(gh issue comment:*)`. That the prose yields one comment, not two, is checked by running the
refiner, which is the issue's check.

## Risks / Trade-offs

- [An agent posts the closing comment and still posts a per-gap comment] → the closing section is the
  only place in the file that asks for a comment.
- [A run dies after commenting and before `aio done`] → the comment says what was found; the engine
  sees no outcome and treats the run as any run that ends without one.
- [Wider write access on the allow list] → limited to `gh issue comment`; `gh` as a whole and `gh api`
  stay off it.
