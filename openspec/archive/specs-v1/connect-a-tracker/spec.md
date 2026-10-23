# connect-a-tracker Specification

## Purpose
How this machine reaches one vendor: what scope to read, and the **name** of the credential to read it with.
Both come from configuration — `TaskSources:<Kind>:Scope` and `TaskSources:<Kind>:SecretName` — which is
also what builds the vendor's reader, so there is one place that says what a connector reads. The name,
never the value — a token in configuration or in a response would be a token in every backup, log and
screenshot. Reverse-engineered from the shipped code, not negotiated ahead of it.

## Requirements
### Requirement: A connector is read from configuration and carries a credential name, never a value
The system SHALL expose `GET /api/connectors` (`Features/Inbox/UseCases/ListConnectors.cs`,
`Access.FiltersToCaller`) returning one connector per vendor that is its own connector — GitHub, Azure
DevOps, Jira and Linear; a pull request arrives through GitHub's — each with its scope and its credential's
**name** as configuration states them, `no scope set` and `not set` where they are absent. It SHALL never
return a credential value. There SHALL be no read path for a credential value anywhere in this product,
which is what makes "the value stays in your keychain" a property of the code rather than a claim in a
document. The secret itself SHALL be fetched through `ISecretResolver` at the moment of use.

#### Scenario: The list names every vendor and a credential never its value
- **WHEN** the settings screen lists connectors
- **THEN** each vendor is listed with its configured scope and a credential name such as
  `keychain: github-pat`, and no value is returned

### Requirement: A connector's status is how this process last read it
Each connector SHALL carry a status and the time it was last read, both held in memory by the task snapshot
rather than stored. A vendor with no reader — no scope configured — SHALL read as `not configured`, which is
unfinished rather than broken. A vendor whose last read in this process failed SHALL read as
`last sync failed`, and SHALL read as `connected` again once the vendor answers. Otherwise it SHALL read as
`connected`. `lastSyncedAt` SHALL be when this process last read the vendor, and null when it has not read
it since it started.

#### Scenario: A vendor with no reader reads as not configured
- **WHEN** a vendor has no scope configured
- **THEN** it reads as `not configured`, not as a failure

#### Scenario: A failed read shows on its connector until the vendor answers
- **WHEN** a vendor's read fails and a later read succeeds
- **THEN** the connector reads `last sync failed` in between and `connected` afterwards

### Requirement: A connector is not edited through this product
There SHALL be no endpoint that writes a connector: pointing a vendor at a scope or naming its credential is
done in configuration and takes effect when the process reads it. A connector screen SHALL therefore offer no
field a token could be typed into and no field a scope could be typed into, so nothing here can disagree with
the reader configuration builds.

#### Scenario: The settings screen only reads
- **WHEN** a person opens the connectors on the settings screen
- **THEN** each one is shown as configuration states it, with no control that writes it
