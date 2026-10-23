## Context

`AppFixture` and the local gate already gather media under `src/artifacts/` and ignored `evidence/`. However, `NarrowSetTests.Current` falls back to `docs/design/current/` outside the gate, `capture.sh current` writes the tracked record, and `/aio:implement` instructs the Agent to commit those captures before copying them into `.harness/evidence/`. `/aio:sync` then embeds a repository blob URL pinned to the squash SHA.

There is a second mismatch: publication uses an issue comment, but `EvidencePanel` parses only `TaskDetail.body`; `GetTaskAttachmentBytes` clears URLs only against that body. The reader cannot safely display the evidence the publisher creates. The installed `gh` supports `issue comment --attach`; availability on other machines must still be checked, not assumed.

The detailed review also inspected remote `master` at `3411d91ee86de2d1323f1b1d001b1c3be63ab5be`. Its new `.harness/scripts/evidence` collects changed tracked captures, then uploads every matching image left in `.harness/evidence/`, reporting a short HEAD SHA. Modify that canonical script on the future issue branch; do not copy an old command's inline publisher into a new mechanism. In particular, a fresh manifest must not accidentally republish a previous batch left in that directory.

## Goals / Non-Goals

**Goals:**

- Make issue attachments the durable, provider-owned record of routine captures.
- Preserve the observed revision and honest publication/read outcomes.
- Read body and comment evidence through one Tasks-owned interpretation.
- Keep authenticated asset fetching confined to authoritative, caller-visible issue origins.
- Stop routine captures changing tracked documentation.

**Non-Goals:**

- Purging historical images or replacing accepted ADRs.
- A new artifact hosting service, video streaming, arbitrary external URLs or automatic upload retries.
- Adding a second image store, a database cache or a new task snapshot containing comment threads.
- Implementing remote PR review in this change.

## Decisions

### Capture into ignored artifacts and preserve intentional design references

Routine wide and narrow captures use an explicit output directory under ignored artifacts. Narrow tests default there even outside `ci`; the existing override remains available for other ignored destinations. The capture script takes an output argument rather than always writing its current set to documentation. Routine commands reject destinations inside tracked design directories.

`docs/design/` retains descriptions, contracts, target references and existing historical images. No implicit baseline-refresh mode runs during implement or sync. An intentional update to a versioned visual reference is a separately owned change, not a side effect of taking evidence.

The alternative, committing and then uploading every capture, retains repository growth and two conflicting sources of evidence.

### Publish one structured batch per observation

Stage names retain `evidence-<issue>-<screen>.png`. A local manifest records a schema version, batch identity, full provider/repository/issue identity, full observed head SHA, optional observed base/comparison SHAs, screen, measured viewport/theme, capture time and the name of each file. A Run or step is included only when supplied by its actual execution record.

The publisher uses the manifest to compose one issue comment containing the images and a machine-readable `aio:evidence` record. Metadata identifies what was observed; it is not a declaration that a gate passed. The PR links the resulting issue comment rather than duplicating the assets. Repeated observations at different revisions remain separate records.

Preflight the CLI attachment support, credentials, manifest and named files before uploading. Confirmed publication requires the vendor's returned comment identity/address. Refusal leaves staged files and a reason; a lost response after upload is `unknown`, not a retry or a successful-looking fallback. Operators inspect the batch identity at the provider before a manual re-entry. Sync never replaces the observed SHA with the squash SHA.

### Use one authoritative evidence interpretation in Tasks

A Tasks use case reads the selected issue body plus comments on demand through `ITaskSource`. Return attachment identity, origin kind, comment identity/address where applicable, actual comment author/time, read completeness and optional capture metadata. Follow comment pagination to its bounded limit; an exhausted limit or a failed page is incomplete, not an empty list. Comments are not added to the inbox snapshot.

Parse and validate the publication record together with its own comment; do not attach one comment's SHA to another comment's images. Malformed or missing capture metadata leaves an ordinary, unversioned comment attachment, with an explicit metadata diagnostic where a publication record was malformed. Body attachments retain issue-read freshness and no invented uploader.

The browser consumes this response instead of implementing a second comment parser. Existing filename matching remains presentation grouping, never authorization or proof of a current revision.

### Clear attachment bytes against their exact origin

Keep body-backed byte requests compatible. Add comment-origin addressing to the existing attachment request, and resolve that comment from the same selected provider/repository/issue. Compare the requested asset URL with the selected origin's parsed allowlist before any asset request.

A missing comment or a URL carried only by a different issue is a not-found. Preserve vendor-asset address restrictions, byte/time limits, redirect restrictions, credential handling and per-row failure distinctions. Listing an origin is not enough: revalidate it when bytes are requested, because a comment can be edited or deleted.

The alternative, trusting a browser-provided URL or merging comment text into the task body, either creates a credentialed proxy or erases provenance and truncation information.

### Proposed contracts and exact ownership

Names below are proposed additions, not claims that endpoints already exist.

| Owner | Surface |
| --- | --- |
| Tasks | `src/modules/Tasks/AiOrchestrator.Modules.Tasks/Features/Inbox/UseCases/GetTaskEvidence.cs` beside `GetTaskComments.cs`; reuse `TaskSnapshot.Resolve`/`ReaderFor` and `[Requires(Access.FiltersToCaller)]` |
| Tasks | Existing `GetTaskAttachmentBytes.cs`; append optional `commentId` while preserving its current body request and response |
| World/Infrastructure | `src/shared/AiOrchestrator.BuildingBlocks/World/ITaskSource.cs`, `TaskSources/GitHubTaskSource.cs` and explicit unsupported/read outcomes in the other adapters |
| Browser | `src/frontend/features/folders/EvidencePanel.tsx`, its EvidenceStep integration, and owned additions in `shared/http/contracts.ts`, `queries.ts`, `shared/i18n/en.ts` |
| Capture/publication | `.harness/scripts/evidence`, `docs/design/capture.sh`, `NarrowSetTests.cs`, canonical implement/sync commands and their live documentation |

Proposed read route: `GET /api/tasks/{key}/evidence?repository=owner/repo&source=GitHub`. Preserve existing caller filtering; an ambiguous key is not permission to choose the first repository. Proposed response:

```text
EvidenceRead {
  readAt,
  completeness: complete | incomplete,
  reasons: [{source: body | comments, kind, message}],
  attachments: [{
    url, name,
    origin: {kind: body | comment, commentId?, commentUrl?, author?, at?},
    observation?: {batchId, headSha, baseSha?, mergeBaseSha?, baseRepository?, headRepository?, sourceState,
                   executionRevision, screen, viewport, theme, capturedAt},
    metadataDiagnostic?
  }]
}
```

Origin/read refusals that prevent selecting an authorized Task remain RFC 7807. Vendor incompleteness after successful Task selection remains a typed read outcome; per-asset `gone`, `unreachable`, `tooLarge` keep the existing byte shape.

`GetComments` currently returns only an `IReadOnlyList<TaskComment>`; GitHub reads the first 100 comments and converts non-success to an empty list. That cannot establish evidence completeness. Add bounded typed comment-page and single-comment reads at the existing seam, sharing adapter internals rather than copying authorization/HTTP logic. Keep the existing comments route compatible; do not implement the new reader on its ambiguous empty-list result.

For the new GitHub evidence read, propose five pages of 100 comments, a 2 MiB decoded-text aggregate ceiling and a 15-second overall comment-read budget. These are application limits, not claims about provider limits. A remaining next page, cut comment body or exhausted deadline means incomplete. Retain already parsed rows and state the cause. A vendor without that capability reports unsupported; do not infer completeness from its legacy comment method.

For comment byte clearance, use a direct comment lookup and verify its provider-returned issue identity against the selected Task. A globally addressable comment ID alone is insufficient. For body byte clearance, re-read the selected issue body rather than clearing against a stale inbox snapshot. Failed refresh is an explicit inability to clear, not an asset fetch using stale text. Do not paginate the whole thread for every image.

### Batch shape, limits and revision honesty

The local manifest and comment marker use one schema version. The following is a shape, not fabricated execution evidence:

```text
<!-- aio:evidence
{
  schemaVersion: 1,
  batchId: <unique observation identity>,
  issue: {provider: "GitHub", host: "github.com", repository: <owner/repo>, number: <number>},
  observation: {
    headSha: <full checkout commit>, baseSha?: <full commit>, mergeBaseSha?: <full commit>,
    baseRepository?: <actual comparison repository>, headRepository?: <actual comparison repository>,
    sourceState: clean | dirty | unknown,
    executionRevision: verified | unknown
  },
  captures: [{
    name: <unique evidence-issue-screen-theme-viewport filename>,
    sha256: <staged file digest>, screen: <screen>,
    viewport: {width: <measured>, height: <measured>},
    theme: light | dark, capturedAt: <UTC timestamp>
  }]
}
-->
```

Serialize actual JSON, not this illustrative notation. Parse only a bounded marker; reject unsupported versions, duplicate names, identity mismatches, malformed SHAs, invalid dimensions or ambiguous multiple records. A malformed record never removes its ordinary attachments or becomes credential authority.

Create a batch-specific directory and upload **only** manifest members after checking file containment, names, digests and the target issue. Do not scan old `evidence-<issue>-*.png` files. Proposed maximum: 50 images, matching the inspected CLI's per-comment attachment capability; refuse a larger batch before upload rather than silently split one observation. Check the current CLI and provider's size/type limits before publication; lower actual limits win.

HEAD alone identifies a checkout, not the bytes executed. Record clean/dirty status at capture boundaries and verify the selected application was built from that revision before asserting `executionRevision: verified`. If the tree changes during capture, retain the files but refuse a single clean-revision batch. A dirty or unverified observation remains readable and must never be classified as proof of the exact PR revision. When an actual PR comparison is known at capture time, record both repository identities, base tip and merge base together; the linked issue's repository need not be the code repository. No comparison metadata is synthesized from the latest remote PR after capture.

Persist a receipt beside the manifest: `batchId`, target identity, count, full observed SHA, outcome, and comment identity/address only when confirmed. The script uses an explicit repository target, not the user's gh default. An existing confirmed receipt returns that receipt without another upload. An uncertain receipt requires manual provider inspection by batch ID; a preflight check cannot guarantee the provider never accepted a timed-out write.

The manifest is an uploader's statement, not signed attestation. SHA-256 checks staging integrity; matching a comment's claimed revision does not prove test success or trustworthy authorship.

### Acceptance-to-test mapping

| ID | Acceptance | Planned proof |
| --- | --- | --- |
| E1 | Routine captures do not change docs | Wide/direct-narrow/gate runs leave tracked design bytes and git status unchanged |
| E2 | Only this observation is published | Two local batches with stale files; fake gh sees only current manifest members and explicit repository |
| E3 | Receipt describes reality | Successful URL, refusal, unsupported CLI, missing file, oversized batch and lost response produce distinct outcomes; zero automatic resends |
| E4 | Comments are readable with honest completeness | Body-only, comment-only, second-page attachment, 501 comments, failed page, truncated marker and unsupported adapter cases |
| E5 | Credentials never follow an unproven origin | Edited/deleted comment, wrong issue/repository, stale body, hostile address and redirect cases make zero unauthorized asset requests |
| E6 | Evidence never overclaims revision | Dirty tree, changed capture head, unknown execution revision and squash merge retain original metadata without becoming current proof |
| E7 | Publication reaches the actual reader | Authorized real batch appears via server byte reads in Evidence at measured 390px and in both themes; PR links its comment |

Do not count fake-gh success as the live publication check. Real publication requires permission for the exact issue. Run #411 isolation before any full live-host gate.

## Risks / Trade-offs

- More vendor reads than the body-only panel: read comments only when Evidence is requested and expose bounded incompleteness; do not pretend a capped page is complete.
- Uploaded images can outlive a PR or have different provider retention: preserve vendor links and report gone assets; this change does not promise permanent storage.
- A timeout can follow a successful upload: record an uncertain outcome, stop and do not resend automatically.
- Some old captures have no full revision: keep them readable as unversioned evidence, never retroactively assign a SHA.
- Shared Tasks contracts and frontend catalogs collide with other changes: declare the issue Seam before implementation and edit only the owned blocks.

## Migration Plan

1. Add authoritative body/comment reading and byte clearance with compatibility tests.
2. Switch the Evidence panel to that reader.
3. Change capture defaults and implement/sync publication instructions.
4. Update live documentation and the affected specs without rewriting history.
5. Verify a real issue attachment publication only after explicit permission to mutate that issue. Failure does not remove a Hold.

Rollback preserves already published issue comments. Restore the previous command behavior only through an explicit change; do not delete vendor evidence or run a repository-history cleanup.

## Open Questions

- An issue-owned branch and draft PR require confirmation; no issue has been created for this local proposal.
- The loaded readiness skills reference deleted `docs/product/` and `docs/process/definition-of-ready.md`. Do not claim their rubric passed. Use the live controlled-Task schema and repository loop when the issue is prepared, and repair that documentation mismatch separately.
- Large report/video hosting is deliberately deferred. Initial publication covers screenshots; existing local review reports remain locally available.
