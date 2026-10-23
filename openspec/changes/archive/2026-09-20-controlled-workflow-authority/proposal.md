## Why

The shipped local controlled Workflow does not authorize shared Task transitions. ADR-0005,
the held #317/#318 proposal and multi-process dispatch need one explicit authority contract
before a watcher can safely compose them.

## What Changes

- Record Proposed ADR-0006 without editing accepted ADR-0005. Specify the exact clauses a
  future acceptance would supersede.
- Define full Task identity, published policy pinning, typed observations, human grants,
  owner generations, repository capacity and durable effect identities.
- Select a single remote git control ref with explicit compare-and-swap for atomic claim and
  capacity reservation. Distinguish registry exclusion from fencing external effects.
- Require CAS-admitted durable effect permits for cooperative authorized harness participants
  before shared dispatch is enabled; never steal a claim based on heartbeat age.
- Specify observed, absent and unknown inputs, revocation, failures, cancellation and
  closure without code. Working-copy policy changes are simulation only.
- **BREAKING for future shared controlled dispatch only:** a branch, removed Hold, legacy
  launch, restart or local gate result alone cannot authorize dispatch. Existing manual and
  local-only controlled Runs keep their behavior and acquire no shared authority.

## Capabilities

### New Capabilities

- `controlled-workflow-authority`: the shared authority, reservation, grant and effect contract.

### Modified Capabilities

- `issue-lifecycle`: separate GitHub phase/Hold authority from shared execution ownership,
  retaining the existing manual loop outside the opt-in path.
- `task-status-and-holds`: distinguish display precedence and Hold release from strict
  controlled authorization.

The shipped `controlled-workflow-execution` capability currently exists only in its unarchived
change, not `openspec/specs/`. This change references it rather than inventing a baseline or
archiving another change.

## Impact

ACP-00 is documentation and specification only. Deliverables are this OpenSpec change,
`docs/adr/0006-shared-control-authority-is-explicit-and-fenced.md` and the linked normative
`docs/control-authority-contract.md`. No runtime, labels, issues, PRs or provider state change.

ACP-01 consumes the disabled-by-default, identity and failure contracts; ACP-02 consumes
input/approval bindings; ACP-04 implements the remote registry seam; ACP-05 implements the
Coordination-owned journal and the Runs dispatch identity handshake. These are separate
implementation slices, not tasks silently included here. ACP-03/#317 and dependent lifecycle
integration remain held. Proposed artifacts are not human acceptance or activation permission.
