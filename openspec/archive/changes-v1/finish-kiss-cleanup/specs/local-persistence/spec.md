## REMOVED Requirements

### Requirement: The move off Postgres carries the existing data across, once

**Reason**: The owner retired the one-off Postgres import tool after adopting SQLite.

**Migration**: Existing SQLite databases and module migrations remain unchanged. The application
no longer ships a Postgres-to-SQLite import command.
