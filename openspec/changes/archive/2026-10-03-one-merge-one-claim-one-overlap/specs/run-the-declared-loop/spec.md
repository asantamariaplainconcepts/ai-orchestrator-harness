## ADDED Requirements

### Requirement: The overlap node answers from the claimed branches and the declared seams
The declared loop's `overlap` node SHALL run `.harness/scripts/file-overlap`, which SHALL treat as in
flight every `change/<n>` branch at origin other than the issue's own, SHALL read each issue's
file-set from its Seam section's bullet paths and its Task input's `seam.paths`, and SHALL answer
`clear: false` naming the issue and the paths when two file-sets can name the same file — equal, one
ending with the other on a `/` boundary, or one a directory the other sits under, compared without
case. An issue that declares no Seam, here or in flight, or that cannot be read, SHALL NOT be clear.

#### Scenario: Nothing else is in flight
- **WHEN** no other `change/<n>` exists at origin
- **THEN** the node answers clear and `claim` runs

#### Scenario: The same file written from another root
- **WHEN** this issue declares `src/frontend/shared/http/queries.ts` and one in flight declares
  `shared/http/queries.ts`
- **THEN** the node answers not clear, naming the other issue and the path

#### Scenario: A merged change stops counting
- **WHEN** a change lands and the merge script deletes its branch
- **THEN** it is no longer in flight for the next tick's overlap
