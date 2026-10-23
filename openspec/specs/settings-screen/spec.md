# settings-screen Specification

## Purpose
Settings answers what the console works with: what is on this machine, the editor *Open in editor*
uses, the engine's interval and stale threshold, the agents' permission mode, the updates, *Start at login* and *Stop console*,
and links to the requirements screen (#680). A folder's setup is on
its own page since #565. There is no sources screen; the credential is the one `gh` holds.
Reverse-engineered from the shipped code (`src/frontend/features/settings/SettingsScreen.tsx`,
`src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Machine/ReadTools.cs`,
`Features/Preferences/`), not negotiated ahead of it.

## Requirements
### Requirement: Settings lists no folder
Settings SHALL hold the instance, the machine and the person, and SHALL NOT list folders, their
defaults or their loops: each folder's setup is on its page (folder-page), opened from its name in the
rail.

#### Scenario: Opening Settings with two folders
- **WHEN** a person opens Settings with two folders added
- **THEN** neither folder's path nor its `.aio/loop.yaml` is on the screen

### Requirement: The machine is read from the PATH, every time
`GET /api/machine/tools` SHALL probe, on every request, the CLI each registered adapter needs on the
login shell's PATH: for the vendor, `gh`, with its version and the account it is signed in as or the
problem; for each agent, its CLI with its version and its models. Settings SHALL show each as found,
missing or signed out. Nothing on this section SHALL be configurable.

#### Scenario: An agent CLI is missing
- **WHEN** `claude` is not on the PATH
- **THEN** its row says "Not on PATH. A state that names it will not start."

### Requirement: The requirements screen says what the machine lacks and how to install it
`GET /api/machine/tools` SHALL also answer `requirements`, from the same probes and on every request:
`git`, each vendor's CLI signed in, `node` and each agent CLI as required, one agent CLI being enough;
the sandbox's CLI with its credential, as its environment's readiness says, and a container runtime
(Podman, else Docker, answering `info`) as optional. Each row SHALL be `present`, `missing` or
`signed-out`, and one that is not present SHALL carry the command that puts it right on this machine
(`brew` on macOS, `winget` on Windows, the distribution's manager on Linux when `/etc/os-release` tells
it, `npm` for an agent CLI elsewhere) or the page with the CLI's install instructions. A runtime that
is installed and does not answer SHALL be `missing` with the command that starts it. `ready` SHALL be
true when every required row is present. The screen at `/requirements` SHALL list the required rows
above the optional ones, each command with a button that copies it, and SHALL install nothing. It SHALL
open when the console is opened at its root while `GET /api/preferences` answers
`requirementsSeen: false`, a link to another screen being followed as it is, SHALL store
`requirementsSeen: true` with `PUT /api/preferences` once `ready` is true, and SHALL then offer to add
the first folder when there is none. Settings SHALL link to it always.

#### Scenario: The first start on a machine without node
- **WHEN** the console starts for the first time on a Mac with `git`, `gh` signed in and `claude`, and no `node`
- **THEN** it opens on Requirements, `node` is missing with `brew install node` and a copy button, and the screen opens again on the next start

#### Scenario: The first start with everything required
- **WHEN** the console starts for the first time and every required row is present, with no folder added
- **THEN** `requirementsSeen` is stored, the dialog to add a folder opens, and the next start opens on PBIs

### Requirement: The editor is a preference
`GET /api/preferences` and `PUT /api/preferences {editor}` SHALL read and store the editor *Open in
editor* runs, a bare command name of up to 60 characters; with none stored it SHALL be `Desktop:Editor`
from configuration, else `code`. An editor that does not start SHALL be reported as "<editor> did not
start. Choose another editor in Settings."

#### Scenario: Choosing Cursor
- **WHEN** a person sets the editor to `cursor`
- **THEN** Open in editor launches Cursor from then on

### Requirement: The engine's interval and stale threshold are changed from Settings
Settings SHALL show an Engine section with the seconds between passes and the seconds after which a
silent run reads as stale, read from `GET /api/loop/settings` and `GET /api/runs/settings`, and save a
changed value with `PUT /api/loop/settings` or `PUT /api/runs/settings`. A value the server refuses
SHALL be shown with its reason. Neither SHALL need a restart.

#### Scenario: Passing every ten minutes
- **WHEN** a person sets the seconds between passes to 600 and saves
- **THEN** the Loop strip says it looks every 600 s, and the next pass runs ten minutes after the last

### Requirement: The agents' permission mode is picked from Settings
Settings SHALL show an Agent permissions section listing `allow-list`, `auto` and `yolo` as a radio
group, each with one line that says what it lets an agent do, the stored mode chosen as
`GET /api/runs/settings` answers it. Picking another SHALL store it at once with
`PUT /api/runs/settings {permissionMode}`, without a restart; a refusal SHALL be shown with its reason.

#### Scenario: Picking yolo
- **WHEN** a person picks `yolo`
- **THEN** it is stored, stays chosen after a reload, and the next run's agent starts in `yolo`

### Requirement: Settings starts the installed tool at login and updates it
Settings SHALL show a *Start at login* switch read from `GET /api/app/service` and changed with
`PUT /api/app/service`. When the app is not the installed tool, or runs on Windows, the switch SHALL be
blocked with the sentence that says why: started with `dnx`, from a checkout, or Windows not supported
yet. When an installed tool has a newer version on nuget.org, the updates SHALL offer *Update and
restart*, which calls `POST /api/updates/install`, shows the command's output, and on success says the
console restarts and reloads the page once `/api/version` answers another version; on failure it SHALL
say nothing restarts. The update command SHALL stay below it. Up to date, or started by `dnx`, there
SHALL be no *Update and restart*.

#### Scenario: A checkout opens Settings
- **WHEN** Settings opens on a server started from a checkout
- **THEN** *Start at login* is blocked and says only the installed aio tool starts at login

#### Scenario: A newer version for the installed tool
- **WHEN** the check answers `available` for an installed tool and the person presses *Update and restart*
- **THEN** the command's output is shown, and the page reloads when the new version answers

### Requirement: The console stops from Settings, saying what ends with it
Settings SHALL offer *Stop console* beside the updates. Before it stops anything, a dialog SHALL say how
many sessions are live and end with it, that a loop run without its end record is recorded as failed on
the next start and its PBI waits for the person, and that nothing starts the console again. Confirmed,
it SHALL call `POST /api/console/stop` and say the console stopped.

#### Scenario: Stopping with two live sessions
- **WHEN** two sessions are in the tray and a person presses *Stop console*
- **THEN** the dialog says "Stopping it ends 2 live sessions." before the person confirms
