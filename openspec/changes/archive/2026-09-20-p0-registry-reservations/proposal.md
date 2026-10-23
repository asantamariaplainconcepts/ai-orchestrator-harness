## Why

Controlled Workflow execution needs a shared reservation primitive before it can coordinate
writers. A local Task claim cannot reserve repository WIP capacity atomically across checkouts.

## What Changes

- Add an inactive World registry seam backed by one repository-scoped Git control ref.
- Reserve a Task claim and repository capacity in the same exact compare-and-swap commit.
- Refuse stale ownership, ambiguous reads, malformed state and unconfirmed quiescence.
- Verify the primitive using independent clones of a local bare remote.

## Capabilities

### New Capabilities

- `shared-control-registry`: Bounded registry snapshots, fenced claim and capacity reservations,
  explicit observation states and conservative release.

### Modified Capabilities

None.

## Impact

BuildingBlocks/World, Infrastructure/Git, the World registration and InfrastructureTests.
No active caller, Coordination module, dispatcher, UI, external effect or provider mutation.

## Non-goals

This does not complete a multiwriter control plane. A Git ref CAS cannot fence OS processes
or provider effects made with shared credentials. Shared dispatch stays disabled.
