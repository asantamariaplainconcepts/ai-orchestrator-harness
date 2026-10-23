## 1. Confirm and claim the independent change

- [x] 1.1 Confirm the issue title/body and prepare its controlled Task input with acceptance IDs, exact Seam paths, exclusions and this change ID; do not claim the deleted readiness rubric passed.
- [x] 1.2 Create the confirmed issue and its separate proposal branch from fresh origin/master using the repository's claim procedure; do not add this feature to merged PR #417.
- [x] 1.3 Publish the validated proposal as its own draft PR, apply the proposal Hold and stop until a person approves; declare all shared-file ownership before implementation.

## 2. Read authoritative body and comment evidence

- [x] 2.1 Define the additive attachment-origin, capture-metadata and read-completeness response in a Tasks-owned one-file use case with explicit authorization.
      req: evidence-from-the-issue/1, evidence-from-the-issue/2, evidence-from-the-issue/3
      files: src/modules/Tasks/AiOrchestrator.Modules.Tasks/Features/Inbox/UseCases/GetTaskEvidence.cs
- [x] 2.2 Implement one body/comment evidence interpretation using the existing ITaskSource seam; preserve vendor/repository/issue identity and do not mirror comments into TaskSnapshot.
      req: evidence-from-the-issue/1, evidence-from-the-issue/2
      files: src/modules/Tasks/AiOrchestrator.Modules.Tasks/Features/Inbox/UseCases/GetTaskEvidence.cs, src/modules/Tasks/AiOrchestrator.Modules.Tasks/Features/Inbox/IssueAttachments.cs, src/modules/Tasks/AiOrchestrator.Modules.Tasks/Features/Inbox/TaskSnapshot.cs, src/shared/AiOrchestrator.BuildingBlocks/World/ITaskSource.cs
- [x] 2.3 Add bounded comment pagination/completeness where the vendor read needs it, exposing failures and exhausted budgets rather than treating them as empty.
      req: evidence-from-the-issue/3
      files: src/shared/AiOrchestrator.BuildingBlocks/World/ITaskSource.cs, src/shared/AiOrchestrator.Infrastructure/TaskSources/GitHubTaskSource.cs, src/shared/AiOrchestrator.Infrastructure/TaskSources/AzureDevOpsTaskSource.cs, src/shared/AiOrchestrator.Infrastructure/TaskSources/JiraTaskSource.cs, src/shared/AiOrchestrator.Infrastructure/TaskSources/LinearTaskSource.cs
- [x] 2.4 Cover body-only, comment-only, mixed, unnamed, duplicate-origin, malformed-metadata, partial-page and refused-comment reads in functional/adapter tests.
      req: evidence-from-the-issue/1, evidence-from-the-issue/2, evidence-from-the-issue/3
      files: src/tests/AiOrchestrator.Modules.Tasks.FunctionalTests/EvidenceReadTests.cs, src/tests/AiOrchestrator.Modules.Tasks.FunctionalTests/TasksFixture.cs, src/tests/AiOrchestrator.InfrastructureTests/GitHubCommentReadTests.cs

## 3. Preserve safe attachment byte clearance

- [x] 3.1 Add compatible comment-origin addressing to GetTaskAttachmentBytes and resolve the named comment only through the selected issue.
      req: evidence-from-the-issue/4
      files: src/modules/Tasks/AiOrchestrator.Modules.Tasks/Features/Inbox/UseCases/GetTaskAttachmentBytes.cs
- [x] 3.2 Clear the exact asset URL from the current authoritative origin before the byte fetch; preserve supported addresses, redirect/size/time restrictions and per-row outcomes.
      req: evidence-from-the-issue/4
      files: src/modules/Tasks/AiOrchestrator.Modules.Tasks/Features/Inbox/UseCases/GetTaskAttachmentBytes.cs, src/shared/AiOrchestrator.Infrastructure/TaskSources/GitHubTaskSource.cs, src/shared/AiOrchestrator.BuildingBlocks/World/ITaskSource.cs
- [x] 3.3 Test cross-repository keys, issue-versus-PR identity, nonexistent/deleted/edited comments, wrong URLs, unsupported vendors and zero asset fetches on uncleared requests.
      req: evidence-from-the-issue/4
      files: src/tests/AiOrchestrator.Modules.Tasks.FunctionalTests/AttachmentOriginTests.cs

## 4. Use the same evidence reader in the screen

- [x] 4.1 Wire typed HTTP/query contracts and EvidencePanel to the authoritative evidence read instead of a separate browser-only body parser.
      req: evidence-from-the-issue/1, evidence-from-the-issue/2
      files: src/frontend/shared/http/contracts.ts, src/frontend/shared/http/queries.ts, src/frontend/features/folders/EvidencePanel.tsx, src/frontend/features/folders/evidence.ts
- [x] 4.2 Render body/comment provenance, recorded observation metadata, unversioned records and incomplete reads using the shared kit and typed i18n.
      req: evidence-from-the-issue/2, evidence-from-the-issue/3
      files: src/frontend/features/folders/EvidencePanel.tsx, src/frontend/shared/i18n/en.ts
- [ ] 4.3 Verify one failed attachment leaves other rows readable and legacy body attachments remain visible; add browser coverage for comment-published captures.

## 5. Stage and publish captures without documentation churn

- [x] 5.1 Change direct narrow capture defaults and wide capture output selection to ignored artifacts; refuse routine destinations inside tracked design directories.
      req: publish-issue-evidence/1, the-narrow-design-set/2
      files: docs/design/capture.sh, src/tests/AiOrchestrator.EndToEndTests/NarrowSetTests.cs
- [x] 5.2 Modify the canonical .harness/scripts/evidence from fresh trunk to consume a versioned batch manifest and persist confirmed/unpublished/uncertain receipts; do not add a parallel publisher or automatically resend.
      req: publish-issue-evidence/2, publish-issue-evidence/3, publish-issue-evidence/4
      files: .harness/scripts/evidence
- [x] 5.3 Replace implement's commit/copy workflow with artifact staging and issue publication; keep viewport/theme measurements and no-op behavior for unchanged screens.
      req: publish-issue-evidence/1, the-narrow-design-set/2
      files: .ai/commands/aio/implement.md
- [x] 5.4 Replace sync's committed screenshot links with links to the original issue publication, retaining the observed SHA after squash merge.
      req: publish-issue-evidence/5
      files: .ai/commands/aio/sync.md
- [x] 5.5 Test capture destination defaults, CLI capability refusal, credential/file preflight, empty batches, successful publication records and lost-response handling without uploading during automated tests.
      req: publish-issue-evidence/1, publish-issue-evidence/3
      files: src/tests/AiOrchestrator.EndToEndTests/NarrowSetDestinationTests.cs
- [x] 5.6 Cover stale staged files, duplicate names, batch bounds, explicit repository selection, existing receipts and changed file digests; upload only the current manifest.
      req: publish-issue-evidence/4
      files: .harness/scripts/evidence
- [x] 5.7 Record source cleanliness and verified/unknown execution revision at capture time; refuse a clean-revision claim when the tree changes or the selected app cannot be tied to that revision.
      req: publish-issue-evidence/2
      files: .harness/scripts/evidence

## 6. Validate and document the changed contract

- [x] 6.1 Update live capture documentation, Tasks attachment clearance documentation and affected command references; do not rewrite accepted ADRs or historical screenshots.
      req: publish-issue-evidence/1
      files: docs/design/README.md
- [x] 6.2 Run affected tests, then aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj --non-interactive and strict OpenSpec validation.
- [x] 6.3 Observe a routine wide/narrow retake with docs/design/current unchanged; inspect comment evidence in both themes and at a page-proven 390px.
      req: publish-issue-evidence/1, the-narrow-design-set/2
- [x] 6.4 With explicit target-issue permission, publish a real batch, record its full SHA/comment address and verify the same attachments through the Evidence screen.
      req: publish-issue-evidence/2, publish-issue-evidence/3, evidence-from-the-issue/1, evidence-from-the-issue/4
- [x] 6.5 Reconcile E1-E7 against observed results, including fresh body/comment byte clearance and bounded partial reads; require #411 isolation before the full live-host gate.
