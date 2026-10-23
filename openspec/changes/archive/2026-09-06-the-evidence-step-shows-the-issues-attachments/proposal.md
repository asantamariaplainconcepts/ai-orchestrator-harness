## Why

The loop captures screenshots, the survivors end up attached to the issue at the vendor, and the
console does not show them: seeing the evidence for a change means leaving the product, opening a
browser and scrolling down somebody else's issue. The moment a person decides whether a screenshot
proves what it claims to prove is exactly the moment they are reading the rest of the change — so
step 4 of the Change tab is where they belong. Issue #294, epic #288; it remakes #275, whose PR
#276 is closed.

## What Changes

- The **Evidence** step of the Change tab draws the attachments of the issue this worktree is
  about, read out of the body the mirror already holds — no new mirror column, no cache, no
  migration.
- Attachments are matched **by name against the issue's own number** — `evidence-294-*.png` — and
  the panel says how many matched. Everything else hanging off the issue is listed apart, under
  *other attachments*, because matching by proximity would be the product guessing.
- The matching rule is **written on the screen**, in one sentence, so a filter nobody can see does
  not read as a fault when nothing matches.
- Each row carries its **provenance**: *attached to issue #294* is a different fact from *captured
  by run step verify-ui*, and the person approving needs to know which one they are looking at.
  The two are told apart by wording and by glyph, never by colour alone.
- The image bytes are fetched **through the seam, with the vendor's credential** — never by the
  browser. #275 measured it: `user-attachments` assets are served **200 image/png, 99,670 bytes**
  to a session that holds the vendor's token and **404** to this app's browser, so an `<img>`
  pointed straight at github.com is a broken image every time.
- A cut mirror body is said, never counted as zero. #283 gave every parser of a mirrored body a
  third answer; this parser joins it, so *"no attachments — the convention is `evidence-<n>-*.png`"*
  can never be shown over an issue whose attachments simply landed past the cut.
- An asset the vendor no longer serves, one nothing could be reached to ask about, and one too
  large to carry are **three sentences**, and none of them is a broken image.

## Capabilities

### New Capabilities
- `evidence-from-the-issue`: what the Evidence step reads, how an attachment is matched to the
  issue's number, what a row says about where it came from, and what is said when the picture
  cannot be drawn.

### Modified Capabilities
<!-- none: the delta is one new capability, and the vendor seam's existing requirements are
     satisfied rather than changed — a reader that cannot answer answers "cannot", which is what
     the-world-seam already requires of every seam. -->

## Impact

- **`BuildingBlocks/World/ITaskSource.cs`** — one read added, `ReadAttachment`, beside the six that
  are there. Its own answer type, with four states.
- **`Infrastructure/TaskSources/`** — `GitHubTaskSource` implements it against the vendor with the
  resolved credential; the other three readers answer that they do not serve attachment bytes.
- **`Modules/Tasks/Features/Inbox/UseCases/GetTaskAttachmentBytes.cs`** — one new use case,
  `GET /api/tasks/{key}/attachments/bytes`, permission `Access.FiltersToCaller` as every read in
  the module declares.
- **Frontend** — `features/folders/evidence.ts` (the parser, beside `seam.ts` and `check.ts`),
  `features/folders/EvidencePanel.tsx` (the panel), one line of `ChangeTab.tsx`, one query in
  `queries.ts`, one response shape in `contracts.ts`, one `evidence.` block at the end of `en.ts`.
- **No migration, no new World.cs registration, no new permission, no new frontend dependency.**
