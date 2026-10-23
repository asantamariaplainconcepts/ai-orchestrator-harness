## Why

Routine screen captures still enter `docs/design/current/` and git history, even though the gate already writes review artifacts outside tracked documentation. The implement command also publishes attachments in issue comments that the Evidence step and its credentialed byte reader do not read.

## What Changes

- Generate implementation captures in ignored artifact directories, never overwrite tracked design captures during routine implementation or sync.
- Publish relevant captures as GitHub issue attachments with a structured, versioned record naming the full observed commit, viewport, screen and publication batch.
- Read evidence from the issue body and its comments, preserving provenance, incomplete reads and per-attachment failures.
- Clear credentialed attachment requests against their actual issue body or comment before fetching any asset bytes.
- Link the issue's published evidence from the PR; do not duplicate uploads or rewrite their observed SHA after squash merge.
- Keep deliberate design references and historical screenshots; do not purge git history.

## Capabilities

### New Capabilities

- `publish-issue-evidence`: capture staging, revision metadata and explicit publication outcomes for the implement/sync commands.

### Modified Capabilities

- `evidence-from-the-issue`: include issue comments as authoritative attachment origins and preserve revision provenance.
- `the-narrow-design-set`: retain viewport and layout assertions, but write routine captures into ignored evidence rather than committing them.

## Impact

- Canonical `.ai/commands/aio/implement.md` and `sync.md`; their runtime links are not separate copies.
- The canonical `.harness/scripts/evidence` introduced on current `master` by PR #416 is the publisher to change, not a second script to add. This checkout predates that script; implementation must start from fresh trunk.
- `docs/design/capture.sh`, `docs/design/README.md`, `DESIGN.md`, `AGENTS.md` and `ARCHITECTURE.md` where their live capture contracts change.
- Tasks attachment read use cases, the existing `ITaskSource` World seam and its vendor adapters; frontend Evidence readers and typed HTTP/i18n contracts.
- Narrow capture tests, attachment functional tests and real browser coverage.
- No schema migration, new hosting service or direct credentialed browser request. GitHub publication uses the installed `gh --attach` capability, checked at runtime.
- This is an independent proposal, not another change to merged PR #417. Issue creation, an issue-owned branch and a draft proposal PR remain subject to confirmation; no implementation or shared GitHub mutation is part of preparing these artifacts.
