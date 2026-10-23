# pbi-issue Specification

## Purpose
The Issue tab answers what was asked, and what has been said since: the issue as the vendor has it,
with its images inside, and a composer whose comment is published at the vendor as the person, where
the next agent reads it. It shows what is elsewhere and copies nothing. Reverse-engineered from the
shipped code (`src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Vendor/ReadIssue.cs`,
`CommentOnIssue.cs`, `Review/ReadAttachment.cs`, `src/frontend/features/pbi/tabs/IssueTab.tsx`), not
negotiated ahead of it.

## Requirements
### Requirement: The issue is read from the vendor, whole
`GET /api/pbi/issue?repository&id` SHALL answer the issue now: its URL, author, creation time, body as
markdown, every label, assignees, parent and the issues blocking it (each with repository, id, title,
URL and whether it is closed), its comments ordered by creation, and the images of the body and the
comments. A comment the harness wrote, opened by `<!-- aio:<kind>`, SHALL carry that kind, so the tab
marks a run's record or the evidence as what it is. An issue that does not exist SHALL be 404
`Pbi.NotFound`. The body SHALL NOT be editable here; *Open in GitHub* goes to the vendor.

#### Scenario: The evidence comment is marked
- **WHEN** an issue has a comment that starts with `<!-- aio:evidence`
- **THEN** that comment is shown marked as evidence, with its images, and not as plain text

### Requirement: Images are proxied only when the thread carries them
The tab SHALL show the GitHub asset images of the body and comments inside the text, each enlarged with
a click. `GET /api/pbi/attachment?repository&id&url` SHALL read the thread again and serve the bytes
only of an asset URL that the body or a comment carries now, through `gh`, with the content type sniffed
from the bytes; any other URL SHALL be 404 `Attachment.NotCarried`, and one that cannot be read 404
`Attachment.Unread`.

#### Scenario: An address the issue does not carry
- **WHEN** the attachment route is asked for a GitHub asset that is not in the issue's body or comments
- **THEN** it answers 404 `Attachment.NotCarried` without fetching anything

### Requirement: A comment is published as the person, with its images
`POST /api/pbi/issue/comment {repository, id, body, attachments?}` SHALL publish the comment through
`gh` with the person's credential, uploading up to 10 images of up to 10 MB each with `--attach`, and
SHALL read the comment back: it counts when the observed body starts with the text sent and carries at
least as many uploaded assets as images were sent. A comment with neither text nor images SHALL be
refused as "Write a comment or add an image."

#### Scenario: A comment with a dropped screenshot
- **WHEN** a person writes a comment and drops one PNG on the composer
- **THEN** the comment appears on the GitHub issue as theirs, with the image uploaded, and in the tab

### Requirement: Rendered markdown sits under the screen's title
The markdown the Issue and Proposal tabs draw (an issue body, a comment, a file of the change, a
review comment) SHALL have its headings demoted one level: `#` SHALL be an `h2`, `##` an `h3`, and so
on, with `#####` and `######` both an `h6`. The sizes of `#` to `####` SHALL follow the source level, so
the markdown looks as it did. The PBI page SHALL have exactly one `h1`, its title, on every tab.

#### Scenario: A proposal that opens with a title
- **WHEN** the Proposal tab shows a `proposal.md` that starts with `# Loop as states`
- **THEN** "Loop as states" is an `h2` and the page has one `h1`, the PBI's title

### Requirement: A Mermaid block is drawn as its diagram
A fenced block of language `mermaid` in the markdown the Issue and Proposal tabs draw SHALL be drawn as
an SVG by the `mermaid` package, pinned to an exact version and fetched with a dynamic `import()` the
first time a block is on screen, so a page without one does not load it. The diagram SHALL use
Mermaid's `base` theme fed the computed values of DESIGN.md's tokens, and SHALL be drawn again when
those values change, so it follows the theme in both directions. A block that does not parse SHALL be
shown as its code with Mermaid's error under it, and the rest of the tab SHALL render as before.

#### Scenario: A typo in a diagram
- **WHEN** an issue body has a `mermaid` block that Mermaid cannot parse
- **THEN** the block shows its code and "This diagram does not parse, so it is shown as code. Mermaid said: …", and the comments below it still show

### Requirement: An epic's graph carries the live state of its sub-issues
`GET /api/pbi/children?repository&id` SHALL answer the issue's sub-issues as the vendor has them, in its
order and closed ones among them, each with repository, id, title, loop state, hold and whether it is
closed; on GitHub it reads the REST sub-issues endpoint through `gh`. A vendor that cannot answer SHALL
be 409 `Pbi.VendorUnread`, and a repository no folder checks out 404 `Pbi.NoFolder`.

Before drawing a flowchart in the issue body, the Issue tab SHALL append to its text, for each node
whose id is `i<number>` of a sub-issue, a `class` line naming its status: `done` when it is closed or at
`done`, else `running` when the engine lists a live run or script for it, else `held` when it carries
the hold, else `waiting` when its newest journal line is `waited`, else its loop state as
`state_<name>`; a sub-issue with no state gets no class. Each class used SHALL have a `classDef` whose
colours are the computed values of DESIGN.md's tokens (success, primary, warning, accent, muted). A
`waited` line whose reason starts `#<n> is in flight`, as `file-overlap` writes it, SHALL add a dotted
edge `i<waiting> -.->|waits for| i<n>` when both are nodes of the graph. Any other diagram SHALL be
drawn as written. The diagram SHALL be drawn again only when the text this produces changes.

When an issue has sub-issues, the tab SHALL show under the body a card headed "Sub-issues · N" with
how many are in each status the graph paints: "N done", "N running", "N waiting" and "N held" always,
and "N other" for those at a plain loop state or none when there are any (#636). When its body has no
`mermaid` block, the card SHALL also draw a graph of its own: the issue with an arrow to each
sub-issue, painted the same way.

#### Scenario: Counts beside the graph
- **WHEN** an epic's five sub-issues are one closed, one with a live run, one held, one waiting on files and one at `ready`
- **THEN** the card reads "1 done", "1 running", "1 waiting", "1 held" and "1 other"

#### Scenario: A sub-issue waits for another on files
- **WHEN** an epic's graph names `i602` and `i604`, #602 has a live run, and #604's newest journal line is `waited` with "#602 is in flight and declares src/x too."
- **THEN** #602's node is painted as running, #604's as waiting, and a dotted edge labelled "waits for" runs from #604 to #602
