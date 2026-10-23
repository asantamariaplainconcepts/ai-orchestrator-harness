## ADDED Requirements

### Requirement: A per-task call names the repository, and a mismatch is a 404 rather than the other row
Every per-task read and write — `GET /api/tasks/{key}/comments`, `GET /api/tasks/{key}/labels`,
`GET /api/tasks/{key}/assignees`, `POST /api/tasks/{key}/labels` and
`POST /api/tasks/{key}/assignees` — SHALL let the caller **name the repository** the key belongs to,
matched case-insensitively, and SHALL resolve the mirror row by source, repository and key together. A key
that is absent from the named repository SHALL be a 404 rather than the row another repository holds under
the same number.

Resolving by key alone SHALL NOT be how these calls find their row. Reading the wrong row answers with
another repository's thread, and writing to it puts a label on somebody else's issue; the first is
recoverable and the second is not.

A caller that names no repository SHALL still be answered — that is how every caller asked before two
repositories could be mirrored — and the answer SHALL be the same one every time rather than whichever row
the query plan reached first. Where the caller also names a **source**, that SHALL narrow the row too: two
vendors may use the same key, and an issue and the pull request that closes it share a number.

The rule SHALL be written once and read by every call that resolves a row from a key, `GET
/api/tasks/{key}` included, so the eight cannot drift into eight rules.

The browser SHALL carry the repository **in the query cache key as well as in the request**, so a cached
answer for one repository's `#217` is never served to a screen showing another's.

#### Scenario: A named repository decides which row answers
- **WHEN** two repositories hold `#217` and a comment thread is read naming one of them
- **THEN** the thread of that repository's issue is returned

#### Scenario: A key absent from the named repository is a 404
- **WHEN** the read names a repository that has no such key
- **THEN** the response is a 404 rather than the other repository's row

#### Scenario: A write names the row it means
- **WHEN** a label is written on `#217` naming one of two repositories holding it
- **THEN** the label lands on that repository's issue and the other is untouched

#### Scenario: The cache does not cross repositories
- **WHEN** one repository's `#217` has been read and another's is opened
- **THEN** the second read is not served the first's cached answer

## MODIFIED Requirements

### Requirement: Labels and assignees are written at the vendor first, and the UI reverts on refusal
The system SHALL expose `POST /api/tasks/{key}/labels` (`SetTaskLabels.cs`, `Access.AnyCaller`) and
`POST /api/tasks/{key}/assignees` (`SetTaskAssignees.cs`, `Access.AnyCaller`), each adding and removing at
the vendor. The vendor SHALL be written first and the mirror SHALL follow through the ordinary poll, so a
change made here and one made in a browser are indistinguishable downstream. The optimistic update in the UI
SHALL revert on refusal, and a write SHALL be addressed with the row's own URL.

Addressing the write with the row's URL SHALL NOT be the whole of it: the row itself SHALL be found by
repository as well as by key, because the right URL read off the wrong row is still the wrong issue.

#### Scenario: A refused write reverts the screen
- **WHEN** the vendor refuses a label write
- **THEN** the optimistic change is reverted and the vendor's words are shown

#### Scenario: The write goes to the row's own repository
- **WHEN** two repositories hold the same key
- **THEN** the row is resolved by repository, and the write is addressed with the URL that row carries
