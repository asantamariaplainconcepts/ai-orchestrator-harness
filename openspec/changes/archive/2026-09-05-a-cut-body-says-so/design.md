## Context

`SourcedText.Trim` is the one place four vendor readers pass an issue body through, and it cuts at
4,000 characters. The doc comment gives the reason it was written with, and the reason was true then:
the detail panel showed a paragraph and the agent read the real thing from the vendor.

Two features have been built on the body since. `parseSeam` (#149) reads `## Seam` for the merge
gate's Seam tile; `parseCheck` (#185) reads `## Check` / `## El check` for the Issue tab's check
list. Both return `null` when they find no section, and both document `null` as a fact about the
issue. Over a truncated body, `null` is a fact about the mirror instead, and the two look identical.

The column is not the constraint: `TasksDbContext` already declares
`task.Property(entity => entity.Body).HasMaxLength(20000)`. Nobody was using it.

Constraints this design works inside:

- **The Tasks module's migration slot.** One issue owns a module's migrations at a time, because two
  branches adding one both rewrite `TasksDbContextModelSnapshot.cs` and git merges the text into
  something that describes neither. This change claims that slot for epic #288; verified free
  against `change/262`, `change/264` and `change/275`, none of which touches Tasks migrations.
- **Three hot shared frontend files.** `contracts.ts` and `en.ts` are appended to beside the block
  the change owns, never tidied. `MergeGate.tsx` and `IssueTab.tsx` are rewritten by #289
  immediately after this change, so the additions here are small and contiguous.
- **No frontend test runner exists.** `src/frontend/package.json` has `build`, `typecheck`, `lint`
  and `format:check` and no test script, so the frontend half is proved by types, by lint, and by
  running the product. The parsers' new state is exercised from the .NET side only where the .NET
  side owns it — the trim.

## Goals / Non-Goals

**Goals:**

- The whole body fits in the ordinary case: 20,000 characters, and 60 of 60 of this repository's most
  recent issues fit.
- The cut declares itself, and the fact reaches both parsers.
- One named shape for "could not be read", reusable by the parsers that come after these two.
- Nothing already on a screen is withdrawn: the field is optional, its absence is today's behaviour.

**Non-Goals:**

- **Not storing without a limit.** The limit stays and keeps its reason; the number changes and the
  cut becomes visible.
- **Not moving the body to a live read.** Comments, labels and assignees are read from the vendor and
  not mirrored, and `task-details` explains why with its own measurements. The body is what the inbox
  lists, and listing 39 issues with 39 vendor calls is a different decision.
- **Not changing what the parsers do with the text they have.** `parseSeam` and `parseCheck` read
  exactly what they read today. What they gain is a third answer.
- **Not judging a check row.** #185 left "what counts as evidence" to its own issue and that stands.

## Decisions

### The trim answers a value, not a string

`Trim` returns `TrimmedText(string Text, bool Truncated)` — a readonly record struct beside the
method that produces it.

*Alternatives considered.* An `out bool truncated` parameter: four call sites, and an `out` is four
chances to drop the answer while the code still compiles. A second method `WasTruncated(body)` that
re-derives it: two readings of one fact, which is the defect `SourcedText` exists to prevent — its
own doc says a copy in each of four readers is a copy that gets fixed in one of them. A sentinel in
the text (checking for the trailing ellipsis): an issue body may end in an ellipsis on purpose.

The four readers each destructure it into the `SourcedTask` they were already building. One extra
argument each, at the end, defaulted — so a fifth reader added later that says nothing reports a
whole body, which is the same direction of safety as the frontend field.

### The limit is 20,000 and it is a constant, not a setting

`HasMaxLength(20000)` admits it and the longest body measured here is 18,348. A configurable limit
would be a knob nobody turns and a second place for the two numbers to disagree; the column and the
constant are already two, and keeping them equal by reading the column at runtime buys a coupling
from Infrastructure to a module's persistence that the module boundaries forbid outright. So: one
constant, and the spec names the column it must match.

### The truncation fact is one nullable column, overwritten by `Absorb`

`MirroredTask.BodyTruncated` is a `bool`, set in `Absorb` beside `Body`. It is a fact about the body,
so it is overwritten wholesale on every sync exactly as the body is — which is also what makes a
shortened issue stop reporting truncation without anything special.

One migration, `MirroredBodySaysWhenItWasCut`, generated against the rebased model. The `Progress`
column that `MirroredTask` documents as "on its way out … goes with the next change that owns this
module's migrations" is **left alone**: dropping it is a separate decision with its own blast radius,
and folding it into a bug fix about body length would put two unrelated schema changes in one
migration. Reported as a debt rather than taken silently.

### On the wire: optional, absent means whole

`TaskItem.bodyTruncated?: boolean` and `TaskDetail.bodyTruncated?: boolean`. The server always sends
it; the type says optional so that every existing reader — and every existing test factory — is
unchanged and correct. This is the reversible direction: a later slice can make it required, and
withdrawing a field a screen already draws is the move that cannot be undone.

The server-side records (`ListTasks.Response`'s row and `GetTask.Response`) take it as a new trailing
member. Both are positional records with no default, because the server has the value in hand and a
default there would let a handler forget it.

### The third state: one shape, in `seam.ts`, beside `sectionLines`

```ts
export const unreadable = "unreadable";
export type Unreadable = typeof unreadable;
export type ReadFromBody<T> = T | null | Unreadable;
export function isUnreadable<T>(read: ReadFromBody<T>): read is Unreadable;
```

`seam.ts` is already the shared home: `sectionLines` lives there and `check.ts` imports it, with a
comment saying why one reading of where a section ends is better than two. The third state is the
same kind of fact and goes to the same place.

*Alternatives considered.* A boolean second return per parser (`{ value, unreadable }`): two shapes
for one distinction, and the ticket says later tickets extend this one. A thrown error: a screen
would have to catch it to draw a tile, and an exception for an expected answer is a control flow no
tile wants. A fourth state distinguishing "cut and the section is missing" from "cut and the section
was clipped": the screen draws both the same way and says the same sentence, so the distinction would
exist only in the type.

`parseSeam(body, truncated = false)` and `parseCheck(body, truncated = false)`. The default is what
makes this additive: a caller that says nothing gets today's two answers.

### A section running to the end of a cut body is also unreadable

The section scan already computes where the section ends. If it ended because the text ran out — not
because a later heading started — and the body was truncated, then only part of the section arrived.
A `## El check` beginning at character 19,900 would otherwise render one line of six as the whole
list, which is the same lie in a smaller font.

`sectionLines` keeps its exact signature and behaviour; a new internal scan returns both the lines and
whether it ran to the end, and a new exported `readSection(lines, truncated, matches)` applies both
rules. So the shared helper two parsers already agree on is not re-signed, and there is one new seam
rather than a changed one.

### The screens

The Seam tile already has an *Unknown* for "nothing was compared", and the ticket asks for that exact
state with a different reason. `seam(rows: ReadFromBody<SeamRow[]>)` gains one branch above the
existing `rows === null` one, same icon, same `tone: "none"`, its own detail line. The check list
gains one branch beside its existing `rows === null` and `rows.length === 0`.

Both additions are a handful of contiguous lines, because #289 rewrites both files next.

## Risks / Trade-offs

- **A body between 18,348 and 20,000 is still cut, and the class of failure survives.** → That is
  the point of the second half: after this change the cut says so and the two screens report it
  honestly, so the next issue that outgrows the number is a visible *Unknown* rather than a confident
  false statement.
- **Existing rows are all stored at 4,000 and none of them says so.** A `BodyTruncated` defaulting to
  false makes every already-cut row claim to be whole until it re-syncs. → The sync overwrites the
  body and the flag together on the ordinary poll, so the window is one poll interval and it closes
  by itself. Backfilling in the migration is not possible: the migration cannot tell a body cut at
  4,000 from a body that happened to be 4,000 long, and guessing from the trailing ellipsis would
  mark an issue that ends in one. The honest repair is the re-sync that was going to happen anyway.
- **5× more text per row in the mirror.** → 60 issues × up to 20,000 characters is under 1.2 MB
  against a SQLite file in the checkout, and the column was already declared for it.
- **A truncated body whose seam section survived still compares against a possibly incomplete
  declaration.** The section ended at a later heading, so its bullets are all there — but a second
  Seam-titled section further down would be missed. → `sectionLines` reads the first matching section
  by design and this repository writes one; not a new risk, and unchanged by this change.
- **#289 rewrites both screens right after this.** → The additions are contiguous, minimal, and
  reported by location so that ticket can carry them.

## Migration Plan

1. One EF migration on the Tasks context, generated (never hand-written) against the model after the
   rebase in step 5, with its embedded designer snapshot regenerated with it.
2. Roll-forward only in practice: the column is nullable-by-default `bool` with `false`, so an older
   binary against a newer database ignores a column it does not read. `Down` drops the column, which
   loses only a fact the next sync recomputes.
3. No backfill — see the risk above.

## Open Questions

None left open. The two decisions the orchestrator assigned — the exact limit, and how the fact
travels — are settled above with their measurements, and both are recorded as assumptions in
`proposal.md` for a person to overturn. The `Progress` column's removal is deliberately **not**
resolved here and is reported as a debt.
