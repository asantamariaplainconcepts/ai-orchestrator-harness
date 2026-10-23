---
name: code-humanizer
description: Prune AI-written prose out of code comments without changing code — cut the clause that restates the name, keep the reason nothing else records. Use when reviewing the comments on a diff or a file, when a comment block has outgrown what it explains, or when a loop or command asks for a comment pass.
license: MIT
metadata:
  author: ai-orchestrator
---

# code-humanizer: prune the prose, keep the reason

**Only comment lines change. Code is never touched** — not a rename, not a reorder, not a
formatting nudge. The name says code because comments live in it; the diff must not.
[`scripts/scan-comments.py verify`](scripts/scan-comments.py) is what proves it, and running it
is the last step, not a claim you make.

The second fence is the harder one. **This repository deleted the alternatives to the comment.**
`docs/backlog.md`, `roadmap.md` and `parallel.md` are gone, and
[`AGENTS.md`](../../../AGENTS.md) put the reasoning behind shipped work "in the comment of the code
it explains". A comment here is often the only surviving record of why something is the way it is.
So the default is **keep**, the burden is on the cut, and a lost fact is a worse outcome than a
kept sentence.

## What to do

1. **Scan.** `python3 .ai/skills/code-humanizer/scripts/scan-comments.py scan [paths]` — no path
   means the files changed against the merge base. It reports candidates, never verdicts.
2. **Judge each candidate** against [What not to touch](#what-not-to-touch) before the catalogue.
   Most hits in this repository are keeps. That is the expected result, not a failed run.
3. **Rewrite.** Cut whole clauses and whole paragraphs. Never patch a flagged phrase in place and
   leave the sentence around it limping.
4. **Never invent a reason.** Do not add a fact, number, date, issue, ADR or mechanism that is not
   already in the comment, the code, or a file you read. If condensing needs a connective you
   cannot source, keep the longer text. Inventing rationale is the worst failure available here,
   because the invention will read exactly like the record.
5. **Verify**, then report.

## Scope comes from the caller

Same process in every mode; only the return changes.

- **Diff** (default, and what a loop gets) — files changed against the merge base. Only comments
  the diff introduced or moved are in scope. A block the diff never touched is out of scope even
  when the scanner flags it: say it exists, change it in its own pass.
- **Files** — the paths named. Every comment in them is in scope.
- **Embedded** — invoked by a command or another agent. Return the summary only.

## The catalogue

Six patterns, derived by scanning this repository's own 1,080 comment blocks rather than from a
general style guide. Counts are what the scanner found at the time of writing, and they are small
on purpose: the corpus is already clean lexically, so the judgement lives in
[What not to touch](#what-not-to-touch), not here.

### P1. Internals of a codebase this repo does not control

**Tells:** a backticked symbol or path attributed to another product — ``orca's
`installWindowVisibilityInterval` ``, `` `src/main/agent-trust-presets.ts` ``, `` `web-preload-api.ts:518` ``.
**Problem:** it cannot be checked from here and rots with no signal when the other repo moves it.
The observation it supports is usually sound; the coordinate is what fails.
**Cut the coordinate, keep the observation.** Where the borrowed mechanism is genuinely
non-obvious, name the product without the symbol.

> Before: The visibility gate is orca's `installWindowVisibilityInterval` — a background tab …
> After: The visibility gate came from orca — a background tab …

### P2. Credit that carries no consequence

**Tells:** another product named as agreement — "X reaches the same conclusion", "X is explicit
about it too", "X came to the same answer".
**Problem:** it argues that the decision is respectable. It does not say what the code does or why
it had to. Humanizer calls this name-dropping to prove importance, and it reads the same in a
comment as in prose.
**Keep provenance, cut agreement.** "orca supplied the second gate" explains why a gate exists
that nothing local motivates — that stays. A paragraph establishing that somebody else agrees goes.

### P3. Cross-reference that restates what it points to

**Tells:** "the same rule/reason as X" followed by a quoted or paraphrased run of that rule.
**Problem:** two homes for one meaning, so changing it is two edits and one of them gets missed.
The comment usually admits the duplication in the same breath.
**Keep the pointer, cut the restatement.** A `<see cref="…">` or a file reference is the whole of
the job.
**The tell over-reports.** It cannot tell a restated rule from quoted UI copy or a quoted error
message, and most of its hits are the second. Only a quoted *rule* is a finding.

### P4. Old state with no consequence

**Tells:** "used to", "no longer", "until now", "had been", "previously" with nothing following
that says what the old state cost.
**Problem:** it narrates a history the reader cannot use.
**This one is mostly a false positive and the guard matters more than the tell.** `DESIGN.md`
says reasons travel with rules, so the old behaviour is very often exactly the *because* — and in
this repository's voice the because arrives after an em-dash. Cut only where no consequence
follows at all.

> Keep: It used to write on every change, defended as cheap — and a single `setItem` is. What is
> not cheap is the render standing in front of it …

### P5. Status note whose subject is an issue

**Tells:** "Since #98 …", "until #167 nothing said …", "which is the ground #6 has to stand on".
**Problem:** it pins the comment to a moment in a workflow, and the workflow moves. When the issue
closes the sentence is wrong and nothing tells anyone.
**Distinguish a measurement, which always stays.** "24 rows with no `FinishedAt` against one live
agent, going back to #124" is evidence and irreplaceable. "Since #98 the seed file is its only
author" is a snapshot — state the constraint, drop the timestamp.

### P6. A clause restating the declaration

**Tells:** the comment's content words are the identifier's words. `// Saves the worktree` over
`SaveWorktree()`.
**Problem:** the reader has already read the name.
**Zero in this repository today.** It is in the catalogue as a floor for new code, and the scanner
catches it structurally rather than by taste.

## What not to touch

Most of the judgement is here. None of the following is a tell, and several are house rules that
a general prose pass would break:

- **Bold rule, em-dash, then the because.** `DESIGN.md` mandates it — "reasons travel with rules"
  — and the corpus carries 2,276 em-dashes and 622 `<b>` in comments. It is the voice, not residue.
  A general humanizer flags this; here it is correct.
- **Saying what a thing is *not*.** `DESIGN.md`: "say what a thing is not when the boundary
  matters." The "not X but Y" shape is required here, not a tell.
- **Measured facts, in any form.** "1,490 bytes in seven chunks inside 570 ms", "24 rows",
  "36 minutes", "412 passed", "Claude Code 2.1.226". Unrounded numbers are a house rule and the
  comment is their only home.
- **Incidents with a date.** "on 15 August a Postgres container outlived its AppHost by 36
  minutes." Nothing else in the repository records it.
- **Terminal captures and `<code>` blocks.** Evidence, and unreproducible from the code.
- **ADR and issue citations** as pointers. It is the *restatement* beside them that P3 cuts, never
  the citation.
- **Length.** A long block is not a tell. Some decisions took that long to explain, and
  `IDeclaredCommandFile.cs` is the worked example of a block that earns 75 lines.
- **`<para>` count, formality, density.** No specific tell means no finding.
- **Quoted copy, error text and product strings.** A comment quoting what a screen or a refusal
  says — `would read as "there are none"`, `refuse every pull request with "nothing is
  configured"` — is quoting, not restating. P3 flags these and they are all keeps.
- **A comment you cannot verify.** If it claims something the code, an ADR or the issue does not
  confirm, leave it and say so in the report. Do not paraphrase it into confidence.

## Two questions before you finish

Ask both, in these words, and treat either answer as an error to fix rather than a note to file:

1. **What comment still says only what the code says?**
2. **Did the rewrite lose a fact, number, date, incident, issue, ADR or capture — or add a claim
   nothing sourced?**

## Done means

- `python3 .ai/skills/code-humanizer/scripts/scan-comments.py verify` exits 0. Zero code lines
  changed.
- **Every candidate the scan reported is accounted for** — rewritten, or kept with a one-line
  reason naming which [What not to touch](#what-not-to-touch) entry protects it. Not "reviewed the
  comments": a list as long as the scan's.
- No fact, number, date, issue, ADR or capture present before is absent after.
- The report says how many candidates were cut and how many were kept, with the counts separate.
  A pass that kept everything is a valid outcome and is reported as one.
