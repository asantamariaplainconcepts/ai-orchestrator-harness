## ADDED Requirements

### Requirement: The vendor position is part of the seam
The seam for reading and moving a PBI's place in the loop SHALL be members of `IPbiSource` in
`src/shared/AiOrchestrator.BuildingBlocks/World/` — the position read and the position write — with
their answer types beside the interface, and every implementation SHALL live in
`AiOrchestrator.Infrastructure/PbiSources/` registered through `World.cs`'s `AddWorld()`. No module
SHALL know a vendor's loop vocabulary: `status:kebab` strings, `System.State` values and each
vendor's field names SHALL live only inside their adapter. A new vendor is a new adapter, not a module
change, and the seam's rule that nothing above it knows which implementation it got SHALL hold for
the position exactly as it holds for every other read and write.

#### Scenario: The module asks for a position, not for labels
- **WHEN** a use case needs where a PBI stands
- **THEN** it calls the position read on `IPbiSource`, and there is no code above the seam that maps a label or a field to a position

#### Scenario: A new vendor's positions are one adapter
- **WHEN** a fifth vendor is added
- **THEN** it implements the position read and write like the rest of the interface, and no module, use case or screen changes to teach it the loop
