# remember-preferences Specification

## Purpose
The two choices this product remembers per machine — which editor to open a folder in, and whether a
run defaults to its own worktree — read by the settings screen and the folder screen. The fallbacks are
the behaviour that already existed before the table did, so a machine that has never opened the screen
behaves exactly as it did. Reverse-engineered from the shipped code, not negotiated ahead of it.

## Requirements
### Requirement: Preferences are typed fields with the previous behaviour as their fallback
The system SHALL expose `GET /api/preferences` (`Features/Preferences/UseCases/GetPreferences.cs`,
`Access.AnyCaller`) returning typed fields rather than a dictionary, so a preference that does not
exist is a compile error at the screen that wanted it rather than an empty string at runtime. Absent
values SHALL fall back to what the code already did: the editor to what `DesktopIntegration` has always
read, and the worktree default to the browser's previous `useState(true)`.

#### Scenario: An untouched machine is told what the app would actually do
- **WHEN** no preference has ever been saved and `GET /api/preferences` is read
- **THEN** the response carries the fallbacks that describe the app's existing behaviour

### Requirement: Every field of a save is optional and absent means leave it alone
The system SHALL expose `PUT /api/preferences` (`SavePreferences.cs`, `Access.AnyCaller`) where every
field is optional and absent means "leave it alone", because two screens write here — the settings
toggle writes the worktree default and the folder screen's "Open in" menu writes the editor — and a
whole-object PUT from either would silently reset the other's value to whatever that screen last read.

#### Scenario: Saving one preference leaves the other alone
- **WHEN** the folder screen saves only the editor
- **THEN** the worktree default keeps the value it had

#### Scenario: A chosen editor survives the next read
- **WHEN** an editor is saved and the preferences are read again
- **THEN** the saved editor is returned

### Requirement: An editor name is held to a shape that cannot reach a shell
An editor value SHALL be held to the same shape wherever it arrives from — the settings screen or the
"Open in" click — and a value that could reach a shell SHALL be refused.

#### Scenario: An editor that could reach a shell is refused
- **WHEN** the editor value contains shell syntax
- **THEN** the save is refused, whichever screen sent it
