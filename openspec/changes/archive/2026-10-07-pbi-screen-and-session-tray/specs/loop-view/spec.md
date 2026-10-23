## ADDED Requirements

### Requirement: A row names its folder and its PR, and opens the PBI screen

A row SHALL name the folder by the folder's name rather than its path, and SHALL carry the PR joined
to the PBI by the rule the PBI screen uses, when there is one. Opening a row SHALL navigate to the
PBI's own screen, `/pbis/:key?folder=<path>`, the same destination the rail, History and the inbox
open.

#### Scenario: The folder is named

- **WHEN** a row belongs to the folder registered as `ai-orchestator-new`
- **THEN** the row reads `ai-orchestator-new`, not the folder's absolute path

#### Scenario: A row opens the PBI screen

- **WHEN** a row is opened
- **THEN** the PBI screen for that key and folder is drawn, with the Journey tab selected
