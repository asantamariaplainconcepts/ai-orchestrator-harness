## Context

Issue **#294**, step 4 of epic **#288**. It sits on #289 (which replaced seven tabs with the
five-step Change tab and deleted `IssueTab` as a tab) and #290 (which made a step's tick expire
when the branch moves). It remakes **#275**, whose PR **#276** is closed — what is rescued from it
is the idea of provenance and one measurement, not its structure: #275's panel hung under
`IssueTab`, and that surface no longer exists.

Two facts from outside this ticket set every decision below.

**#275 measured the vendor.** A `user-attachments` asset is served **200 `image/png`, 99,670 bytes**
to a request carrying the vendor's token, and **404** to this app's browser. An `<img>` pointed at
github.com is therefore a broken image every time — which is why #275 ended up drawing alt text
linked to the comment instead, and why its own commit message names proxying the bytes as a
separate issue. This is that issue.

**#283 made truncation travel.** The mirror stored 4,000 characters of a body and two parsers read
the cut version believing it whole; #283 raised the limit to 20,000 and gave every parser of a
mirrored body a third answer, `ReadFromBody<T>` / `isUnreadable()` in `features/folders/seam.ts`,
carried on the wire as `TaskItem.bodyTruncated` / `TaskDetail.bodyTruncated`. A panel that reads
attachments out of that same body and says *"no attachments"* over a body it could not read to the
end would reintroduce exactly the bug #283 was opened to kill, one ticket later, in a new surface.

## Goals / Non-Goals

**Goals:**
- The Evidence step draws the issue's attachments without leaving the product.
- Matched by name against the issue's number, with the rule written on screen and the count said.
- Each row carries an honest provenance, and the two provenances are told apart in words.
- The picture is fetched by the server with the vendor's credential, never by the browser.
- Every way it can fail is a sentence, and no failure is a broken image or an empty panel.

**Non-Goals:**
- **Capturing anything.** #278 produces the screenshots and is not in this epic.
- **Publishing anything.** Attaching is the loop's job, with `gh --attach`; this only reads.
- **Comparing a capture with a design.** Whoever looks decides what it proves.
- **A cache, a column or a migration.** #290 holds the epic's only migration slot.
- **Reviving #275's dossier** — the check-line-to-evidence binding, and the panel under `IssueTab`.

## Decisions

### D1 — The list is parsed from the mirrored body, on the frontend, beside `seam.ts`

The ticket's Seam names two options and says the cheap one must be ruled out before a seam is asked
for. It is not ruled out: the vendor writes attachments into the issue body as markdown, the mirror
already stores that body, and `TaskDetail` already carries it to this screen — `ChangeTab` calls
`useTask` twelve lines above the panel, so the second read is a cache hit and the list costs one
request of zero.

The parser goes in `features/folders/evidence.ts`, beside `seam.ts` (#149/#283) and `check.ts`
(#185), which are the two existing readers of a mirrored body and are both frontend modules over
`TaskDetail.body`. A third reader of the same text in a different tier would be a third place to
disagree about what that text says.

*Alternative rejected:* a `GET /api/tasks/{key}/attachments` that parses server-side. It buys
nothing — the body is already on the client — and it puts the third reader of a mirrored body on
the other side of the wire from the two that exist.

### D2 — The name is the alt text, and an attachment with none is still listed

Nothing else survives the upload. Measured against `gh` 2.99.0: `--attach '<file>#<alt text>'` is
the only door for media (there is no REST or GraphQL upload), and **the alt text you pass becomes
the markdown alt** — the address itself is an opaque UUID. GitHub's own editor does the same with a
dragged file's basename.

#275's commit says *"nothing about the file's name survives into the comment … a convention on
filenames was the obvious idea and it does not exist"*. That is true of the address and false of the
alt, and the difference is the whole of this ticket's matching rule. An attachment whose alt is
empty is listed under *other attachments* with no name — the honest place for something that cannot
be matched, and the reason the rule is printed on screen.

### D3 — Three answers, in the shape `seam.ts` already spells

`parseAttachments(body, truncated)` returns `ReadFromBody<Attachment[]>` — the list, `null` for "no
attachments", `unreadable` for "the body we were handed is not the whole body". Imported from
`seam.ts`, not re-declared: #283's own words are *"one shape, not a flag per parser … every parser
added later joins this one"*, and this is the parser added later.

Truncation makes the read unreadable when **nothing** was found, and does not otherwise: attachments
that arrived are still drawn, and the panel says the body was cut beside them. That mirrors
`readSection`'s rule — truncation changes nothing about what a parser does with the text it does
have.

*Alternative rejected:* a fourth vocabulary (`"partial"`, a `cut: boolean` beside a list). #290 hit
the same fork and echoed an existing spelling for the same reason; a third name for one idea on one
screen is two of them being read as the other.

### D4 — Provenance says what the mirror knows, and nothing it does not

The ticket's example line is `attached to issue #235 by andoniplain · 2h ago`. The mirror cannot say
`by andoniplain`: **the vendor exposes no uploader and no upload time for an asset embedded in a
body** — the markdown is a URL, and `MirroredTask` has no author column that no-migration would let
us add anyway. Fabricating it from `Meta`'s first segment (`SourcedText.Meta(login, "updated …",
"N comments")`, joined with ` · `, and the login is optional so the first segment is not reliably a
login) would be a guess wearing the shape of a fact — the same failure as the one above.

So a body row reads **`attached to issue #294 · mirrored 2h ago`**, and the freshness it gives is
the mirror's own, said as the mirror's own. `mirroredAt` is added to the `TaskDetail` response as an
**optional** field over a column that already exists (`MirroredTask.MirroredAt`); absent means the
row says only which issue it is attached to.

The second provenance — `captured by run step verify-ui · 41m ago` — is modelled and rendered, and
**nothing produces it today**: #278 is what fills it and has not landed. The union is written now
because check 2 asks for the two to be distinguishable and because a shape added later would change
the row rather than extend it. What is deliberately *not* built is a heuristic that labels a body
attachment as run-captured from its filename; that would be inventing the provenance this whole
requirement exists to make trustworthy.

### D5 — `ITaskSource` gains the read; `World.cs` gains nothing

The ticket's Seam offers "`ITaskSource` **or** a sibling seam". `ITaskSource` wins on the
credential: a vendor asset is served only to a request carrying the vendor's token, `GitHubTaskSource`
already holds `ISecretResolver` and its secret's name, and `TaskSourceCatalog` is the one place that
knows which connector is configured. A sibling seam would have to re-derive all of that — a second
place to configure a credential, and wrong the first time two connectors exist.

It is still `BuildingBlocks/World/`, which is what "goes through World, not the browser" means. It
needs no `AddWorld()` line because `TaskSourceCatalog` constructs the readers, so the one `World.cs`
line this epic allocated to #294 is **not spent**.

The shape, beside the six reads already there:

```csharp
Task<AttachmentRead> ReadAttachment(string address, CancellationToken ct = default);

public sealed record AttachmentRead(AttachmentAnswer Answer, AttachmentContent? Content);
public sealed record AttachmentContent(string ContentType, string Bytes, long Length);
public enum AttachmentAnswer { Read = 1, Gone = 2, Unreachable = 3, TooLarge = 4 }
```

Four answers rather than a nullable: *gone* and *unreachable* are the difference between "somebody
deleted your screenshot" and "your network is down", and collapsing them tells a reader the first
when the second is true. `TooLarge` is the bound on an untrusted input, in the spirit of
`CommandBounds` — 8 MB, against a measured 99,670-byte screenshot and the vendor's own 10 MB
per-image ceiling. The three readers that do not serve attachment bytes answer `Unreachable`
explicitly, one line each, rather than inheriting a default that would read as an accident.

*Enum note:* `AttachmentAnswer` is a **new** enum, so its ordinals 1–4 collide with nothing. No
existing enum is extended and no allocated ordinal is spent.

### D6 — The route serves one attachment's bytes, and the body is what clears the address

`GET /api/tasks/{key}/attachments/bytes?url=…&repository=…&source=…`, one use case,
`GetTaskAttachmentBytes.cs`, permission `[Requires(Access.FiltersToCaller)]` — what every read in
the Tasks module declares. It is a sub-path of the `/api/tasks/{key}/attachments` route this ticket
was allocated, and it echoes the house pair `GetFile` → `/file` and `GetFileBytes` → `/file/bytes`.

**The address is the untrusted input, and the mirrored body is what clears it** — the same shape as
`GetFileBytes`, where the path is untrusted and git clears it, and as `WorkingRoot`, where an
unmatched path answers **NotFound rather than Forbidden so probing teaches nothing**. The handler
parses the row's own body for asset addresses and refuses anything not in that set. Without it this
route is a credentialed fetcher for any URL a caller likes.

Bytes travel base64 in a JSON response, as `GetFileBytes` already does, and the failures travel as a
field on a 200 rather than as a problem document — the shape `GetWorktreePullRequest.Response(row,
Unreachable, Detail)` established for "the vendor did not answer". They are facts about the vendor,
not refusals by this product. So **no new error code is minted**, and the allocated
`Folder.AttachmentUnreachable` is not spent — it was allocated in `FolderErrors`, which belongs to
Workspace, and this use case is in Tasks, where reaching for `FolderErrors` would be the cross-module
reference `MOD001` fails the build over.

One request per attachment, keyed per address, so a failure is confined to its own row — which is
what check 5 asks for and what a single batched read could not give.

### D7 — One line of `ChangeTab.tsx`

`{shown.id === "evidence" ? <EvidencePanel … /> : null}` replaces the `<Todo step="evidence" />`
line. The five slots stay five separate lines: #289 wrote them that way so four concurrent tickets
edit four different lines, and collapsing them into a table would turn four zero-conflict merges
into four conflicts on one hunk.

### D8 — Colour is never the carrier

`--pc-*` functional tokens are defined once in `:root` and never redefined under `.dark` — #289
measured `--pc-info-100` rendering near-white on near-white, and #290 measured `--pc-success-700` at
**3.34:1** in dark. Every state in this panel is carried by a **sentence and a glyph**; where a tone
is used at all it is `--muted-foreground`, `--pc-warning-700` (5.03:1 dark) for "could not be read",
and the themed `--accent`/`--accent-foreground` pair for anything tinted. The panel does not invent a
fix for the unthemed ramp — that is one decision for all the surfaces, and it is not this ticket's.

## Risks / Trade-offs

- **The run-step provenance has no producer until #278 lands.** → It is modelled and rendered from a
  real code path, and this document says plainly that nothing feeds it. The alternative — inferring
  it from a filename — is the lie this requirement exists to prevent.
- **A body row cannot name a person.** → It names the issue instead and says how fresh the mirror's
  reading is. If the mirror ever grows an author, the row grows a clause; today it says less rather
  than saying something untrue.
- **Base64 through JSON is roughly 4/3 the bytes.** → Bounded at 8 MB per attachment, against a
  measured ~100 KB screenshot, and cached per address by the query client so a redraw costs nothing.
  It is the shape `GetFileBytes` already uses for exactly this job.
- **`gh --attach` may write an alt this rule cannot match.** → Then the attachment lands under
  *other attachments*, named or unnamed, and the rule is printed on screen so the reader can see
  why. Nothing is dropped; the panel degrades to a list.
- **A vendor other than GitHub has attachments this cannot read.** → Its reader answers
  *unreachable* in words. Azure DevOps, Jira and Linear each address attachments differently, and
  guessing at one of them from here would ship an untested vendor integration inside a UI ticket.

## What was observed, running

The AppHost was started (`--isolated`) and the following were measured rather than reasoned about.

- **The vendor claim reproduces, first-hand.** On this repository's issue #289, asset
  `41bea21e-…`: anonymous → **404 `text/plain`, 9 bytes**; the same request carrying the vendor's
  token → **200 `image/png`, 147,206 bytes**. #275's measurement holds, and the seam is the only
  way the picture reaches a screen.
- **The route is mapped and its refusal is the designed one.** `GET
  /api/tasks/%23294/attachments/bytes?url=…` answers `404 Task.NotFound` with the RFC 7807 body,
  and `/openapi/v1.json` lists the path.
- **A missing required query parameter answers 500, not 400 — and it is not this ticket's.**
  `GetFile` and `GetFileBytes` answer 500 to the same omission (`BadHttpRequestException` escaping
  to `GlobalExceptionHandler`). Pre-existing house behaviour on every use case with a required
  query parameter; reported rather than fixed.
- **The parser was run over this repository's real issue bodies** — the expressions from
  `evidence.ts`, over the live bodies of #275 and #288. It finds **6 of 6** and **2 of 2**, with
  their alt text intact. **None of the eight matches `evidence-<n>-*`**: this repository's issues
  use a descriptive Spanish sentence as the alt, not a filename. The panel therefore reads *0
  matched* today and lists all of them under *other attachments*, with the rule on screen — which
  is the designed degradation, and it is why the rule is printed rather than implied.

**Not observed:** the panel itself. An isolated AppHost has an empty mirror and no configured
connector, so the Change tab's Evidence step renders its "no issue resolved" sentence and nothing
past it. Getting further needs a real credential written into the secret store, which is a side
effect outside this ticket. The states above the API were verified by test and by the parser run,
not by looking at them.

## Open Questions

None blocking. Two are recorded for the orchestrator's ledger rather than resolved here: whether
`AttachmentAnswer` should grow a *forbidden* state once a vendor other than GitHub implements the
read, and whether the mirror should carry an issue author — a column, which this epic has no slot
for.
