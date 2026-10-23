## RENAMED Requirements

- FROM: `### Requirement: The attachments are read from the body the task read already holds`
- TO: `### Requirement: The attachments are read from the issue body and its comments`
- FROM: `### Requirement: A cut body is said, and never counted as none`
- TO: `### Requirement: An incomplete evidence read is said, and never counted as none`

## MODIFIED Requirements

### Requirement: The attachments are read from the issue body and its comments

The Evidence step SHALL read an issue's attachments through a Tasks-owned read of its body and comments. Comments SHALL be read on demand from the selected vendor, not mirrored in the inbox snapshot. Body and comment attachments SHALL use the same vendor-asset parser and retain their origin.

An attachment is a markdown image or link whose address is a supported vendor asset, including `github.com/user-attachments/assets/...` and the older `github.com/<owner>/<repo>/assets/...`. Its name is its alt text. Empty alt text SHALL not remove an attachment. Pagination, capped results and comment-read failures SHALL report incompleteness rather than a complete empty list.

#### Scenario: An attachment in the body is listed
- **WHEN** the task body carries a vendor image named `evidence-294-login.png`
- **THEN** it is listed with body provenance

#### Scenario: The loop publishes to a comment
- **WHEN** an issue comment carries a vendor image named `evidence-294-login.png`
- **THEN** it is listed with that comment's identity and address

#### Scenario: A picture that is not a vendor asset is not an attachment
- **WHEN** a body or comment embeds an image outside the supported vendor asset addresses
- **THEN** it is not listed as an attachment that the credentialed reader can fetch

#### Scenario: An unnamed attachment is still listed
- **WHEN** a supported asset has empty alt text
- **THEN** it is listed unnamed and does not match the filename convention

#### Scenario: Comments exceed the read budget
- **WHEN** another comment page exists after the bounded reader stops
- **THEN** attachments already read remain available and the result says the comment read is incomplete

### Requirement: Every row says where it came from, and the two provenances are told apart

Each row SHALL distinguish issue-body provenance from comment provenance in words and not by colour alone. A body row SHALL name the issue and its read freshness, without inventing an uploader or upload time. A comment row SHALL identify its comment and use its author/time only when returned by the provider.

A valid versioned publication record SHALL add the actual captured screen, viewport, theme and observed revision to that comment's assets. It SHALL name a Run/step only when supplied by that record. A filename match alone SHALL NOT establish execution provenance or freshness.

Ordinary comment attachments without a publication record SHALL remain readable as unversioned evidence. A malformed publication record SHALL be diagnosed without discarding unrelated attachments or assigning invented metadata.

#### Scenario: A body attachment says which issue it is attached to
- **WHEN** an attachment comes from issue #294's body
- **THEN** its row identifies the issue and read freshness, not an invented uploader

#### Scenario: A publication comment is read
- **WHEN** a comment carries a valid capture record and its embedded assets
- **THEN** those rows identify the comment and the revision and capture metadata recorded with it

#### Scenario: Neither provenance is carried by colour alone
- **WHEN** body and comment attachments are drawn together
- **THEN** each identifies its origin in words

#### Scenario: Capture metadata is malformed
- **WHEN** a comment contains images and an invalid publication record
- **THEN** the images remain ordinary comment attachments and the record's failure is explicit

#### Scenario: The publication degrades
- **WHEN** upload support, credentials or the provider refuse publication
- **THEN** local captures remain and the issue check reports them as unpublished rather than implying vendor evidence exists

#### Scenario: A run-step capture says which step captured it
- **WHEN** a capture record names the Run step that took the capture
- **THEN** the row names that step; a record that names none leaves the step unstated rather than guessed

#### Scenario: The loop publishes what a change moved
- **WHEN** `/aio:implement` publishes the captures for issue `#n` through `.harness/scripts/evidence`
- **THEN** one comment carries the images and their `aio:evidence` record with the full observed SHA, and the step lists them with that comment's provenance and pairs them by the `evidence-<n>-*` name

### Requirement: An incomplete evidence read is said, and never counted as none

The evidence read SHALL distinguish attachments found, a complete read with none and an incomplete or failed read. A truncated body, failed comment read, missing page or exhausted pagination budget SHALL prevent the screen from declaring the issue has no attachments.

Attachments found before an incomplete read SHALL still be drawn with the reason completeness could not be established. A complete body with no images SHALL NOT establish absence until the comment read is also complete.

#### Scenario: A cut body with nothing found is unknown, not empty
- **WHEN** the body is truncated and no attachments were found
- **THEN** the step says its evidence read is incomplete, not that the issue has none

#### Scenario: A whole body with nothing found is empty
- **WHEN** the body and comment reads are complete and neither carries an attachment
- **THEN** the step says no attachments were found and states the matching convention

#### Scenario: What arrived is still read
- **WHEN** a body or comment read becomes incomplete after two attachments were found
- **THEN** those two are drawn beside an explicit incompleteness notice

#### Scenario: The comment read fails
- **WHEN** the body is complete and empty but the vendor refuses the comment read
- **THEN** the step states the read failure and does not report an empty issue

### Requirement: The picture is fetched through the seam, with the vendor's credential

Attachment bytes SHALL be fetched by the server through the vendor seam and never by a direct credentialed browser request. Requests SHALL identify the selected provider/repository/issue and their body or comment origin.

The server SHALL resolve that origin through the caller's selected issue, and SHALL fetch bytes only if its current authoritative text carries the exact requested supported asset URL. A missing origin or unmatched URL SHALL be a not-found before any asset fetch. Origin validation SHALL not be waived by a client-provided metadata record.

Existing supported host/path restrictions, redirect restrictions, size/time limits and credential handling SHALL remain enforced. Unsupported attachment vendors SHALL return an explicit inability to read rather than a successful empty picture. Existing body-origin callers SHALL remain compatible.

#### Scenario: The picture is drawn from bytes this process fetched
- **WHEN** a matched body or comment attachment is drawn
- **THEN** the server fetched it through the seam and the browser made no credentialed vendor request

#### Scenario: An address the issue does not carry is refused
- **WHEN** the requested origin does not carry the asset address
- **THEN** the route returns not-found and performs no asset fetch

#### Scenario: An attachment belongs to another issue
- **WHEN** a request names a comment or asset carried only by another issue or repository
- **THEN** it is not cleared for the selected issue

#### Scenario: A comment was edited after listing
- **WHEN** an attachment URL was removed from the selected comment before bytes were requested
- **THEN** the byte read returns not-found instead of relying on the earlier list

#### Scenario: The issue body was edited after the inbox snapshot
- **WHEN** an attachment URL remains in the inbox snapshot but has been removed from the issue's current body
- **THEN** the byte read refreshes the selected origin, returns not-found and performs no asset fetch

#### Scenario: Current origin cannot be read
- **WHEN** the selected body or comment cannot be refreshed from the provider
- **THEN** the request reports the inability to clear the origin and does not authorize an asset fetch from stale text

#### Scenario: An existing body caller asks for bytes
- **WHEN** the caller uses the existing body-origin request and its issue carries the URL
- **THEN** the read retains the existing byte response and per-row failure distinctions
