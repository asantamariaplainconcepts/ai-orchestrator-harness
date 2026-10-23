# secret-resolution Specification

## Purpose
Secrets are resolved by name, per read, from a store the host registers — so a credential added while the app
is running is visible immediately, a rotated value needs no restart, and no module ever links a secret-store
SDK. Reverse-engineered from the shipped code, not negotiated ahead of it.

## Requirements
### Requirement: A secret is resolved per read, by name, never snapshotted
`ISecretResolver` SHALL resolve a secret value from its name **per read**. Anything that snapshotted the set of
secrets at startup could not see a credential added afterwards, and the failure would read as "credential
missing" rather than "process is stale"; per-read resolution also means a rotated value is picked up with no
restart and no cache to invalidate.

#### Scenario: A credential added while running is used immediately
- **WHEN** a connector is pointed at a credential name created after startup
- **THEN** the next sync resolves it without restarting the app

#### Scenario: A rotated value needs no restart
- **WHEN** the stored value behind a name changes
- **THEN** the next read uses the new value

### Requirement: The host registers the store and a module never links one
An implementation of the resolver and its store SHALL be registered by the host, never by a module: modules
receive only `IServiceCollection` and `IConfiguration` and SHALL stay free of any secret-store SDK.

#### Scenario: A module cannot reach a secret store directly
- **WHEN** a module needs a credential
- **THEN** it takes `ISecretResolver` and the host decides which store answers

### Requirement: The self-host store is one Data-Protection file per secret
`ProtectedFileSecretStore` SHALL keep one file per secret, its contents produced by ASP.NET Core Data
Protection, in a directory the deployment mounts — the framework's implementation or nothing, because every
decision a bespoke design would make here (cipher, key derivation, IV handling, envelope format, rotation) has
a well-known wrong answer that looks correct in a passing test. Files rather than a table SHALL be the
arrangement, so a leaked database dump contains no credential at all.

#### Scenario: A database dump carries no credential
- **WHEN** the database is dumped
- **THEN** no secret value is in it, because none is stored there

#### Scenario: The store is the framework's, not a bespoke envelope
- **WHEN** a secret is written
- **THEN** its contents are produced by Data Protection rather than by hand-rolled encryption
