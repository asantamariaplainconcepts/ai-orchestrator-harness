# publish-issue-evidence Specification

## Purpose
TBD - created by archiving change publish-evidence-on-the-issue. Update Purpose after archive.
## Requirements
### Requirement: Routine captures are artifacts rather than documentation edits

The implement command and direct routine capture entry points SHALL write screen captures into ignored artifact directories. They SHALL NOT modify, stage or commit `docs/design/current/` as a side effect of evidence generation. Existing design references and historical captures SHALL remain unchanged.

#### Scenario: A screen changed during implementation
- **WHEN** a routine retake captures a changed screen
- **THEN** the image exists in ignored artifacts and tracked design images remain byte-for-byte unchanged

#### Scenario: A tracked destination is requested by a routine command
- **WHEN** routine evidence generation is configured to write inside a tracked design directory
- **THEN** it refuses with the destination and remedy before overwriting a tracked capture

### Requirement: Published evidence identifies the actual observation

A publication batch SHALL carry a versioned `aio:evidence` record with its batch identity, full provider/repository/issue identity, full observed checkout head commit, capture time and each capture's name, digest, screen, measured viewport and theme. It SHALL distinguish clean, dirty and unknown source state and verified or unknown execution revision. A base or comparison commit, Run or step SHALL be recorded only when actually known.

When a PR comparison is actually known at capture time, the record SHALL include its base/head repository identities, base-tip SHA and merge-base SHA independently of the linked issue's repository. Missing comparison facts SHALL remain absent rather than being reconstructed after capture.

The issue attachment names SHALL retain the `evidence-<issue>-<name>` convention. Revision metadata SHALL apply only to assets in the comment that carries it. Metadata SHALL NOT itself claim that tests passed.

#### Scenario: Two revisions were observed
- **WHEN** captures are published for two different head commits of one issue
- **THEN** each batch retains its own revision and the earlier batch is not relabelled with the newer head

#### Scenario: A manual attachment has no capture record
- **WHEN** an issue carries an attachment without structured observation metadata
- **THEN** it remains readable as unversioned evidence and no commit or execution provenance is invented

#### Scenario: The checkout has uncommitted source changes
- **WHEN** captures are taken from a dirty checkout
- **THEN** their record identifies the dirty observation and does not claim the checkout's HEAD exactly describes what was executed

#### Scenario: The source changes during a capture batch
- **WHEN** the capture-boundary revision or source-state checks disagree
- **THEN** the artifacts remain available but publication as one clean verified revision is refused

### Requirement: Publication has explicit confirmed failed and uncertain outcomes

Before uploading, the publisher SHALL check attachment support, credentials, metadata and staged files. Confirmed publication SHALL require the provider's returned comment identity/address. Failure SHALL keep local artifacts and report `UNPUBLISHED` with the reason. A response lost after a potentially accepted upload SHALL report an uncertain outcome and SHALL NOT retry automatically.

The publisher SHALL upload a nonempty relevant batch in one issue comment and SHALL NOT create an empty evidence comment for a change with no captures.

#### Scenario: The provider confirms the upload
- **WHEN** the provider returns the publication comment identity/address
- **THEN** the issue check records the capture count, full observed SHA and comment link

#### Scenario: The CLI cannot attach files
- **WHEN** the installed CLI lacks attachment support
- **THEN** no upload starts, local files remain and the outcome names the unsupported capability

#### Scenario: The response is lost after sending
- **WHEN** an upload might have reached the provider but its response was lost
- **THEN** the result is uncertain, names the batch to inspect and neither claims success nor resends it

#### Scenario: No screen needed a capture
- **WHEN** a change has no relevant captures
- **THEN** no publication comment is created

### Requirement: The canonical publisher uses only the selected batch

The existing canonical evidence script SHALL publish only files named in the selected manifest, verify their containment and digests before uploading, and explicitly identify the repository and issue in the provider call. It SHALL refuse batches exceeding its supported attachment limit before uploading. A confirmed receipt SHALL be reusable without another publication; an uncertain receipt SHALL require manual reconciliation before any new send.

#### Scenario: Earlier captures remain staged
- **WHEN** the staging directory contains images from a previous observation
- **THEN** only the selected manifest's members are attached and none of the earlier images is relabelled with the new revision

#### Scenario: The batch was already confirmed
- **WHEN** the operator selects a manifest with a confirmed receipt
- **THEN** the script returns that original receipt without creating another comment

#### Scenario: A staged file changed
- **WHEN** a manifest member is missing, escapes its directory or no longer matches its recorded digest
- **THEN** preflight refuses the batch before upload and reports the failing member

### Requirement: The PR links evidence without duplicating or rewriting it

The implement/sync flow SHALL link the issue publication from the PR rather than uploading the same batch again or embedding a committed screenshot blob. Squash merge SHALL NOT rewrite the observed head SHA as if the resulting merge commit had been tested.

#### Scenario: A PR is squash merged
- **WHEN** a PR whose evidence names its observed head is squash merged
- **THEN** the issue comment still names that observed head and no screenshot is committed or relabelled to the squash SHA

