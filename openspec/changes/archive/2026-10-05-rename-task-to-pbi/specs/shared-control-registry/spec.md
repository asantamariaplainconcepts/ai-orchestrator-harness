# shared-control-registry Delta

## MODIFIED Requirements

### Requirement: Atomic claim and repository capacity

The system SHALL use a validated PBI identity containing provider, canonical API host including
port, repository and provider key. A claim and its single repository WIP slot MUST change in one
registry commit. Active and draining claims MUST occupy capacity. A released claim MUST retain its
generation, and subsequent acquisition MUST increase it.

The rename of the identity type in code, from `ControlTaskIdentity` to `ControlPbiIdentity`, SHALL
NOT change the tuple it serialises: the stored registry, the intents already journalled and the
effects already derived keep their shape, and a reservation made before the rename still matches a
claim made after it.

#### Scenario: Last slot race
- **WHEN** two independent clones claim different PBIs with one slot remaining
- **THEN** at most one claim and its reservation become confirmed

#### Scenario: Stale owner
- **WHEN** release names a previous owner, generation or reservation
- **THEN** it is refused without changing the registry

#### Scenario: The renamed identity still matches old reservations
- **WHEN** a claim is made after the rename against a reservation recorded before it
- **THEN** both serialise the same `{Provider, Host, Repository, Key}` tuple and the claim resolves
