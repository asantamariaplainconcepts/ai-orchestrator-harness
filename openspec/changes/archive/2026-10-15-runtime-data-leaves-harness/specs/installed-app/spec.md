## ADDED Requirements

### Requirement: In a checkout, the development instance keeps its data under src/artifacts/data
Run from a checkout through the AppHost, the instance SHALL keep its SQLite database at
`<checkout root>/src/artifacts/data/aiorchestrator.db`, and its runs and its scripts' outputs beside it,
where the AppHost already leaves what it generates. It SHALL write nothing under `.harness/`. The
checkout root SHALL be the nearest directory above the AppHost holding `.git`, resolved through
symlinks, so deleting a worktree deletes its data and two checkouts never share a database.
Publishing a recording to the `aio/evidence` branch SHALL read it from this data directory as from
any other.

#### Scenario: A fresh worktree starts the development instance
- **WHEN** `aspire start` runs in a worktree that has never run it
- **THEN** `src/artifacts/data/aiorchestrator.db` exists in that worktree, migrated, and `.harness/data/` does not

#### Scenario: Two worktrees run at once
- **WHEN** two worktrees of the same repository each run `aspire start`
- **THEN** each has its own `src/artifacts/data/aiorchestrator.db`, and a folder added in one does not appear in the other
