## ADDED Requirements

### Requirement: The voyage is the destination the rail, the inbox and Loop open
The PBI's voyage — the detail/journey the `pbi-journey-by-stages` change centres — SHALL be the single
destination a rail row, an inbox row and the `Loop` entry open when they reach a PBI, and SHALL be
resolved by the PBI's full identity (source, host, repository, key). A row SHALL NOT open a second
reading beside the voyage: no rail-owned detail, no forked journey, no destination inferred from a
reused branch or the first run with a matching key. Where the voyage's read is not yet served (before
the `pbi-journey-by-stages` change lands), a row SHALL open the `PbiDetailPane`/`PbiWalk` reading of
today, and SHALL NOT invent a destination the voyage does not offer — the row's destination is a
contract the voyage satisfies, not a parallel screen.

#### Scenario: A rail row opens the same voyage as the inbox
- **WHEN** a PBI is opened from a rail row and from the inbox
- **THEN** both open the same voyage, reached by full identity, with no second reading

#### Scenario: A closed PBI's voyage is reachable
- **WHEN** a PBI is closed at the vendor and a rail row or History journey is opened
- **THEN** the voyage opens, because closing does not remove the voyage, the way it does not collapse
the detail (`pbi-details`'s lifecycle rule)

#### Scenario: The destination is the identity's, not the first match
- **WHEN** two repositories hold a PBI numbered 12 and a rail row for one is clicked
- **THEN** only that repository's voyage opens, the rule `pbi-details` enforces for its entries

### Requirement: Old worktree links resolve to the PBI's voyage or explain
A `?worktree=` link (a published folder-route query the bookmarks carry) SHALL resolve to the PBI whose
voyage owns that worktree when the relation is unambiguous, and SHALL open the voyage by full identity.
When the relation is not unambiguous — a worktree whose branch name a removed PBI left behind, or a
path two folders' PBIs could own — the link SHALL show an explanation naming the reason it cannot
resolve, and SHALL NOT select another PBI that shares a key or path. This is the same rule
`pbi-details` enforces for review and execution entries: a destination belongs to one identity, never
to the first match.

#### Scenario: A worktree link opens the PBI's voyage
- **WHEN** a folder route carries `?worktree={path}` identifying one PBI
- **THEN** that PBI's voyage opens, reached by full identity

#### Scenario: A removed worktree's link explains
- **WHEN** a folder route carries `?worktree={path}` whose worktree was removed and no PBI owns it
unambiguously
- **THEN** the screen shows an explanation naming the removal, and no other PBI is selected

#### Scenario: A shared key does not reselect
- **WHEN** a folder route carries `?worktree={path}` and two PBIs could own it by shared key or path
- **THEN** the screen shows an explanation naming the ambiguity, and neither is selected in silence
