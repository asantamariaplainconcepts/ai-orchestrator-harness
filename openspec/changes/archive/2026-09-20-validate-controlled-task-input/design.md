## Context

The Tasks mirror receives bodies through `ITaskSource`; it does not own their content or freshness. The existing DoR is prose. ACP-00 defines identity as provider, canonical API authority, repository, and provider key. Existing read and authorization patterns remain unchanged.

## Goals / Non-Goals

**Goals:** One bounded deterministic parser, actionable deficits, stable acceptance IDs, explicit dependencies, and opt-in input assessment for GitHub.

**Non-Goals:** Lifecycle enforcement, provider resolution, approval, dispatch, claim creation, historical migration, and PR semantic validation. No changes to #317. #262/#263 are optional authoring aids, not dependencies.

## Decisions

- A GitHub form contains one required `Task input` textarea rendered as JSON. Its `### Task input` section contains exactly one JSON fence. API-created and manually edited bodies use that identical representation. Separate free-text form fields would require a second parsing grammar.
- Version 1 carries `schemaVersion`, `taskKind` (`implementation` or `epic`), `observation`, `goal`, `acceptance` (`id`, `text`, `check`), `seam` (`module`, `paths`, `excludedPaths`, `worldInterfaces`), `exclusions`, `dependencies` (`type`, `target`), `decisions`, `changeId`, and `evidence`. Empty arrays explicitly declare no dependencies, decisions, interfaces, or evidence. A change ID identifies OpenSpec rather than copying its specification.
- Seam paths are literal repository-relative files or directory prefixes, not globs. Included and excluded paths cannot overlap in either direction. External interfaces are explicit names; no filesystem or network discovery is invented.
- A standalone authorized `POST /api/tasks/input/validate` receives `identity`, `body`, and `bodyTruncated`. It returns an assessment DTO with stable code/field/remedy deficits and a SHA-256 body digest. It has no effects and does not establish provider provenance. Callers must fetch current provider content and later bind approvals independently.
- Only provider `github` with host `api.github.com` is supported initially. Canonical full identities use decimal positive keys without `#`; GHES and other providers report unsupported rather than falling back.
- `depends-on` references remain unresolved without an authoritative resolver; authored statuses are forbidden. `related` references do not block. Self-dependencies are cycles. Transitive cycle discovery requires resolved external bodies and is deferred; all such unresolved dependencies already block.
- `legacy`, `unsupported`, `invalid`, `blocked`, and `valid-input` describe input, never lifecycle readiness. Missing opt-in marker is legacy. A truncated body is unknown/invalid even if its missing marker might have been cut. Epics and open decisions block.
- The PR template links the Task, change ID, acceptance IDs and evidence only; it does not duplicate the shape.

## Risks / Trade-offs

- Supplied bodies and identities are untrusted snapshots -> label assessment non-authoritative and keep consumers out of this slice.
- JSON editing is less convenient than prose -> provide a runnable form example and concrete field remedies.
- Existing prose Seam sections can disagree with the structured block -> reject a second Seam section rather than choosing one silently.
- GitHub forms do not enforce JSON structure after edits -> the backend always validates again.

## Migration Plan

Additive only, without persistence changes. Removing the endpoint and templates restores the previous surface. Legacy launch behavior is unchanged.

## Open Questions

Provider freshness/resolution and lifecycle integration belong to ACP-03/07; this slice cannot answer whether a dependency exists or has completed.
