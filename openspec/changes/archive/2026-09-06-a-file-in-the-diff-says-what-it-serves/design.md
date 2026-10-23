## Context

Epic #288 turns approving a worktree from seven tabs into one Change tab with five ordered steps.
#289 built the tab, the rail and the footer; #290 made a tick expire when the branch moves. The five
panels arrive one ticket at a time, and this is step 2, **Code**.

The reader's question at step 2 is not "how much changed" — `DiffTab` has answered that for months —
but "which of these files are the change and which are the scaffolding". Nothing in this repository
answers it. `tasks.md` is the nearest thing and it describes work rather than attribution.

Two constraints shape everything below.

**The claim has an author.** Which requirement a file serves is not derivable here. A path does not
say what it is for, and pairing by capability would produce a badge coarser than the one the design
canvas draws. So the answer is an agent's claim, written by the agent that wrote the code, and the
screen has to carry it as a claim.

**#293 consumes the same annotation.** The Tests step maps a test to a requirement *from this
annotation*, not by parsing a runner's output. So the format below is a contract, not a private
detail, and it is stated in the spec delta rather than only in a prompt.

## Goals / Non-Goals

**Goals:**

- One annotation format, written once, that `/aio:implement` produces and two steps consume.
- A file row that says what it serves, and says nothing where nobody said anything.
- An annotation that has gone stale is visible rather than silently ignored.
- A directory with no annotations at all — every change on this branch today — renders exactly as it
  did before.

**Non-Goals:**

- Verifying the claim. If an agent says a file serves requirement 2 and it does not, this repeats it.
- Mapping tests to requirements. That is #293, over this same annotation.
- Changing the diff or how it is computed.
- Inferring the mapping from paths. Considered and declined for the reason in the ticket.

## Decisions

### The annotation format

```
- [x] 1.2 Read the pull request in one call
      req: issue-tab/1, issue-tab/2
      files: src/worktree/pull-request.ts
```

- Only a **closed** task (`- [x]`) annotates. An open task has moved nothing; what it names is a plan.
- `req:` and `files:` sit on their own continuation lines, indented past the task's `-`.
- Values are comma-separated, trimmed, backticks stripped, empties dropped.
- Either key may repeat under one task; the lists concatenate in order.
- The annotation ends at the next task line, the next `#` heading, or the end of the file.
- A requirement is named `<capability>/<ordinal>` — the spec folder under the change's `specs/`, and
  the requirement's position in it — **as the agent wrote it**. Nothing normalises it.
- Every file under one task takes every requirement under that task.

**Why this shape and not a table or a fenced block.** It survives being read by a person, it survives
`prettier` and every markdown renderer, and it leaves `tasks.md` a task list. A table would put the
attribution somewhere other than next to the task that made the claim, which is exactly where the
claim stops being checkable by the person reviewing it.

**What it gives #293 for free.** A test file is a `files:` entry like any other, and its requirements
are its task's `req:`. #293 needs no second parser: it reads the same answer and filters the paths it
considers tests. That is the whole reason the format is one shape rather than one per step.

### The parse runs on the server; the label is drawn on the client

The ticket's Seam names `parseSeam` / `parseCheck` as the precedent and puts the reader in
`src/frontend/features/folders/`. Those two read a **mirrored issue body the frontend already holds**.
`tasks.md` is on disk in a working directory the browser cannot reach, so a server call exists either
way — and once it does, the question is only whether the text or the answer crosses the wire.

The answer crosses, for one measured reason: **this repository has no frontend test runner.**
`src/frontend/package.json` declares `dev`, `build`, `preview`, `lint`, `typecheck`, `format:check`
and nothing else; there is no vitest, and no `*.test.ts` exists under `src/frontend`. #283 changed
both `parseSeam` and `parseCheck` and added tests only on the C# side (`SourcedTextTrimTests`). A
parser in the frontend here is a parser nothing can prove, and step 2's whole content is what that
parser says.

So `GetAnnotations` parses, matches against the directory's own diff, and answers file rows. The
frontend keeps `annotations.ts` beside `seam.ts` for what is genuinely a screen decision — turning
requirement ids into the short label a row can carry — and for the map the two surfaces share.

**Recorded as an assumption**, because it departs from the ticket's Seam in where the reader sits.
It is reversible in the cheap direction: the wire carries the requirement ids verbatim, so moving the
parse to the client later needs the endpoint to answer text instead of rows and nothing else to move.

### Matching an annotated path to a changed file

`seam.ts` already solved this and measured why: the same file is written at different roots across
issues, and a diff answers one root. Equality would call almost every declaration untouched.

So the rule is the one `suffixMatch` uses — equal, or the changed path ends with the annotated path
on a `/` boundary — restated in C# rather than shared, because the two live on opposite sides of the
wire. The imprecision is in the same direction and costs the same: a short annotated path could match
a file the repository does not have.

### An annotated path the diff does not contain is said, not dropped

The ticket left this to the proposal. **It is said.**

A path that was renamed, reverted, or never written leaves a claim that reads as satisfied because
nothing contradicts it. That is an annotation ageing badly, and it ages in the direction that costs
most: a reader who has learned to trust the labels has no way to notice the ones that quietly stopped
applying.

It is also the reversible direction. Quietening a warning later is a small change; withdrawing a
silence people have come to trust is not.

### "Cannot answer" is spelled the way this screen already spells it

`IWorkspaceProbe.Contents` caps a read at 4,000 lines and answers `Truncated`. A `tasks.md` past that
cap gives annotations that are a prefix of the claim — #283's exact sentence, *the text we were handed
is not all the text*.

So the wire carries `truncated: boolean` (#283's own spelling on `TaskItem.bodyTruncated`) and the
screen says the list may be incomplete. **No third vocabulary.** The other existing spelling —
`since: number | null` from #290 — is about a fact ageing, which is not the question here.

### Which task lists are read

Every `openspec/changes/<name>/tasks.md` in the working directory where `<name>` is not `archive`,
capped at ten. Found through `IWorkspaceProbe.Files`, the existing seam the Files tab already pays
for; **no new `World` seam**, which the ticket's Seam requires.

**Assumption: the archive is not read.** A change is normally archived by `/aio:sync` immediately
before the squash-merge, so during review it is still active. A worktree that archived early loses
its labels and draws an unlabelled diff, which is the same behaviour as a change written before this
existed — degraded, never wrong. Widening to the archive later is additive.

### The Code panel is one line of `ChangeTab.tsx`

`ChangeTab.tsx` lines 452-456 are five one-line panel slots, written that way by #289 **so that four
concurrent tickets edit four different lines**. Only the `"code"` line changes here, and the five are
not folded into a table.

Four rows open, the rest behind `Show N more files` — so the ticket's 25-file case reads
`Show 21 more files`. Four because the first is open with its patch and three more fit above the fold
at the widths this panel is used at.

## Risks / Trade-offs

- **The label repeats a claim that may be false.** → It is drawn as a claim: it appears only where
  somebody wrote one, an unannotated file carries nothing, and the step says how many of the diff's
  files ended up labelled so a reader can see how much of the diff is unaccounted for.
- **Nothing annotates anything until `/aio:implement` has been annotating for weeks.** → Which is why
  the prompt half lands first and depends on nothing. Until then every directory is the
  no-annotations case, and that case is a requirement with its own scenario.
- **`Files` lists the whole tracked tree to find at most ten paths.** → It is the existing read the
  Files tab already makes on this screen, and this repository's own tree is 749 entries against a
  2,000 cap. A cheaper lookup needs a new `World` seam, which this ticket is not allowed and does not
  need.
- **The suffix match can attach a label to the wrong file** where a short annotated path suffixes two
  different files. → `seam.ts` measured this trade-off and took the same side; the alternative calls
  almost every honest annotation stale.
- **`DiffTab` is reachable from a plain checkout** (#289 rescoped `diff` to `checkout` rather than
  deleting it). → The label is additive on the row and the query is enabled for a checkout too, so
  the checkout path renders as it did, with labels where the checkout happens to have them.

## Migration Plan

None. No migration, no new permission, no new error code, no enum value. Every piece is additive: an
absent annotation is the behaviour that exists today, and the endpoint answering nothing renders the
diff whole.

## Open Questions

None outstanding. The one the ticket left open — a path that is not in the diff — is decided above and
carried into the spec delta as a requirement.
