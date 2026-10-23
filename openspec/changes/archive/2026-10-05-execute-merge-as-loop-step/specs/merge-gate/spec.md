## ADDED Requirements

### Requirement: The admission refuses a merge when the PBI it resolves to is held

The grant admission SHALL read the PBI's position through `IPbiSource.GetPosition` for the identity
it resolved (provider, host, repository, key) before the grant lookup, and SHALL refuse typed when
the read answers `Held: true`. A hold is orthogonal to the grant and to the head: a valid grant on
the approved head with an active hold is a merge that SHALL NOT happen, and reading the hold first
short-circuits the grant and vendor round-trips a hold forbids. The refusal SHALL name the remedy —
release the hold on the PBI before merging — in the handler's sentence, the way every other refusal
does, and SHALL carry a typed reason the closed attention vocabulary projects. A position read that
fails SHALL be refused as the vendor's own words rather than read as not-held: merging past an
unknown position is what the admission exists to stop, on the same terms an unknown grant is.

The hold SHALL be read from the vendor through the seam — never from the snapshot, which a write
empties — so a hold applied after the PR's approval and before the merge is caught at the admission,
not inferred from a stale cache. The grant admission's existing `Missing` and `CandidateChanged`
refusals SHALL stand unchanged beside the new `HeldByPbi` refusal; the admission answers one
question with five typed answers rather than four, and the handler's switch stays the single place
sentences are written.

#### Scenario: A held PBI is refused before the grant is read

- **WHEN** the PBI the merge resolves to carries `status:holding` at the vendor
- **THEN** the admission refuses typed `HeldByPbi`, the grant is not read, nothing is merged, and
  the refusal names releasing the hold as the remedy

#### Scenario: A hold that appeared between approval and merge is caught

- **WHEN** the pull request was approved and the grant is live, and the PBI was held between the
  approval and the merge's admission
- **THEN** the admission refuses `HeldByPbi`, the live grant does not override the hold, and
  nothing is merged

#### Scenario: A position read that fails is a refusal, not a not-held

- **WHEN** the position read cannot be put or the vendor refuses
- **THEN** the admission refuses with the vendor's own sentence, and is not read as `Held: false`

#### Scenario: An unheld PBI proceeds to the grant

- **WHEN** the position read answers `Held: false`
- **THEN** the admission proceeds to the grant lookup unchanged, and the hold is not read again

#### Scenario: A folder that never adopted the registry still reads the hold

- **WHEN** the folder's tunables declare no policy pin, so the grant admission returns the pre-
  adoption no-op answer
- **THEN** the hold is still read for the PBI the merge resolves to, and a held PBI is refused
  before the no-op grant, because the hold is orthogonal to the registry's adoption

### Requirement: The merge is launched by the PBI's identity from the journey's done step

The merge use case SHALL accept the PBI's identity — source, repository and key together — as the
entry the journey's `done` step launches, resolving the worktree from that identity and the folder as
the launcher's detail, and cleaning it up after the merge is confirmed. A PBI-keyed entry SHALL sit
beside the folder-scoped, `WorktreeId`-keyed route the worktree screen keeps; the journey's step
SHALL NOT launch the folder-scoped route with a worktree it had to invent. The permission the merge
requires SHALL be unchanged — `WorkspacePermissions.ManageWorktrees` — and an undeclared entry is
refused, the way an undeclared request is anywhere else.

The worktree SHALL be resolved by the PBI's identity and the folder, not by a branch-naming
convention: the admission already resolves the identity from the folder's remote and the worktree's
branch, and the launcher resolves the worktree from the same identity the admission will use, so a
PBI with one worktree in flight is launched against that one and a PBI with none is refused with the
remedy rather than invented. The effect identity (`MergeEffectId.Derive`) SHALL derive from the same
canonical identity the admission resolved, so a merge launched by identity and a merge launched by
worktree journal as one effect when they are one merge.

#### Scenario: The journey's done step launches the merge by identity

- **WHEN** the journey's `done` step offers the merge and it is taken
- **THEN** the request carries the PBI's identity (source, repository, key) and the folder, the
  worktree is resolved as the launcher's detail, and the operation is the same squash the loop's
  merge node and `/aio:sync` run

#### Scenario: A PBI with no worktree in flight is refused with a remedy

- **WHEN** the journey's `done` step offers the merge for a PBI whose folder holds no worktree for
  its identity
- **THEN** the launch is refused naming that no worktree is in flight for this PBI, rather than the
  launcher inventing one

#### Scenario: The folder-scoped route is unchanged

- **WHEN** the worktree's `ChangeTab` launches the merge with a `WorktreeId`
- **THEN** the folder-scoped route behaves as it did, and the worktree screen's contract is
  untouched

#### Scenario: A merge launched by identity and one by worktree journal as one effect

- **WHEN** a merge is launched by the PBI's identity and the worktree it resolves is the same a
  worktree-scoped launch would have named
- **THEN** the derived effect id is the same, and a retry from either entry resumes rather than
  repeats

## MODIFIED Requirements

### Requirement: The close is a named, resumable effect

The merge's close — stop environment, remove worktree, drop branch — SHALL retain its resumable
semantics, and the whole merge effect SHALL be journaled: recorded before the merge request,
settled after the response or observation, with a derived effectId (task identity, folder and
operation hashed) so re-entry resumes rather than repeats. The effectId SHALL derive from the same
canonical PBI identity the admission resolved, whether the merge was launched by the PBI's identity
from the journey's `done` step or by `WorktreeId` from the folder screen, so the two entries journal
one effect for one merge. "Merge confirmed, cleanup pending" SHALL be a query the journal answers,
not a memory the person or the prompt holds.

#### Scenario: A retry resumes where it stopped

- **WHEN** the merge landed and the cleanup failed, and the person retries
- **THEN** the re-entry reads the pull request as merged, resumes the cleanup, and merges nothing
  a second time

#### Scenario: The journal names what finished

- **WHEN** the cleanup is partially done
- **THEN** the journal's record settles to the state that names what happened, and a pending
  cleanup is visible as pending

#### Scenario: A merge launched by identity and one by worktree share one effect

- **WHEN** a merge is launched by the PBI's identity and the worktree it resolves is the one a
  worktree-scoped launch would have named
- **THEN** both derive the same effectId, the journal recognises the second as the first's resume,
  and the vendor merge is not requested a second time
