# pbi-proposal Specification

## Purpose
The Proposal tab answers what the change says it will do: the OpenSpec change on the PBI's pull
request, rendered, or, in a repository that does not plan in OpenSpec, the issue's latest `## Plan`
comment, with comments anchored to its lines as review comments of the pull request, so the
coder reads them on its next run. It is the place where a person decides on `proposal-review`.
Reverse-engineered from the shipped code (`src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Vendor/ReadProposal.cs`,
`ReadProposalFile.cs`, `CommentOnProposal.cs`, `ProposalChange.cs`,
`src/frontend/features/pbi/tabs/ProposalTab.tsx`), not negotiated ahead of it.

## Requirements
### Requirement: The change is the one the pull request carries
`GET /api/pbi/proposal?repository&id` SHALL read the PBI's pull request and its files, and take as the
change the first `openspec/changes/<name>/` it adds or modifies outside `archive/`, else an archived
one. With no pull request, or no change on it, the plan SHALL be read from the issue as the next
requirement says, and with no plan there either the answer SHALL say so in `missing` rather than fail.
The files SHALL be ordered proposal, design, the spec deltas, tasks, then the rest, each read at the
pull request's head; each spec delta SHALL come with the spec it changes as the default branch has it,
or none when it is new, and the count of requirements it adds, modifies, removes or renames; tasks SHALL
come with how many of its checkboxes are done out of the total.

On GitHub the change SHALL be read in one `gh` call, the pull request with its files without their
patches, and every file of the change with the default branch's spec of each delta in one more: one
GraphQL query that names each blob by `<ref>:<path>`, where it was one call per file (#621). A blob
GraphQL calls binary or cut short SHALL be read raw on its own.

#### Scenario: A change of six files
- **WHEN** the tab opens on a pull request whose change has six files, two of them spec deltas
- **THEN** `gh` is called twice for the change and its eight files, and the files read as they did one call at a time

#### Scenario: A PBI whose proposal is not written yet
- **WHEN** the tab opens on a PBI with no pull request and no `## Plan` comment
- **THEN** it says the pull request does not exist yet and that the engine opens it when `spec-ready` ends

### Requirement: Without an OpenSpec change the plan is the issue's ## Plan comment
`/aio:propose` writes the plan as an issue comment headed `## Plan`; only a repository whose
`.aio/context.md` asks for it writes an OpenSpec change instead (#726). When the pull request
carries no change, or there is no pull request, `GET /api/pbi/proposal` SHALL answer the issue's latest
comment whose first line is `## Plan` in `plan`, with its id, author, date, body as written, address
and attachments, and the earlier such comments, oldest first, in `earlierPlans`. A change on the pull
request SHALL win over any comment, and `plan` SHALL then be null. With neither, `missing` SHALL say
that there is no OpenSpec change and no `## Plan` comment yet.

The tab SHALL render the plan's markdown, name its author and date linked to the comment, count the
earlier plans with a link to each, and offer Approve and Request changes as it does for a change;
Request changes SHALL post its review on the pull request as it does for a change. A plan comment has
no lines to anchor a comment to.

#### Scenario: A repository that plans in the issue
- **WHEN** the tab opens on a PBI whose pull request carries no `openspec/changes/` and whose issue has a `## Plan` comment
- **THEN** it shows that comment rendered, with its author and date

#### Scenario: A second pass
- **WHEN** the issue has two `## Plan` comments because a person asked for another
- **THEN** the later one is shown and the earlier one is counted and linked

#### Scenario: No plan anywhere
- **WHEN** the pull request carries no change and the issue has no `## Plan` comment
- **THEN** `missing` says there is no OpenSpec change and no `## Plan` comment yet

### Requirement: A line takes a comment that lives on the pull request
`POST /api/pbi/proposal/comment {repository, id, path, line, body}` SHALL publish a review comment on
that file and line of the pull request at its head, and read it back by path, line and body. A path the
change does not hold SHALL be 404, and a PBI with no pull request 409 `Pbi.NoPullRequest`. The tab SHALL
show the pull request's review comments under the change, oldest first, on their lines.

#### Scenario: A comment on a requirement
- **WHEN** a person comments on line 12 of a spec delta
- **THEN** the comment is a review comment of the pull request on that file and line, and the tab shows it there

### Requirement: A file of the change is served as it is, and only that
`GET /api/pbi/proposal/file?repository&id&path` SHALL serve the bytes of a file the change contains, at
the pull request's head, with the content type of its extension and a content security policy that
runs nothing; any other path SHALL be 404 `Pbi.NoSuchChangeFile`, and a path with an empty, `.` or `..`
segment SHALL be refused.

#### Scenario: An image inside the design
- **WHEN** `design.md` shows an image the change adds
- **THEN** the image is fetched from the change at the head and shown inside the text

### Requirement: Approve and Request changes decide proposal-review
The tab SHALL be marked, and offer its decisions, while the PBI's state declares `tab: proposal` in the
loop; in this repository that is `proposal-review`. *Approve* SHALL release the PBI's hold and SHALL be
enabled only while it is held. *Request changes* SHALL appear only when the state declares `back`,
SHALL require a body, and SHALL send the PBI to that `back` (`spec-ready` here) as the person-decisions
capability says.

#### Scenario: Approving a high-risk proposal
- **WHEN** a PBI is held in `proposal-review` because the score came out high, and a person presses Approve
- **THEN** the hold comes off and the PBI moves to `in-progress`
