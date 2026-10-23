# update-the-installed-app Specification

## Purpose
TBD - created by archiving change update-from-the-shells-settings. Update Purpose after archive.
## Requirements
### Requirement: The page asks its shell through one channel, shaped once

The webview and the shell SHALL communicate over Photino's string pipe with one message shape —
an id, a method name, and an optional body — parsed in the shell (`WebMessages`), answered with a
reply that carries the same id and exactly one of a result or an error. An unknown method SHALL
be answered with an error reply rather than silence, so a page newer than its shell reads a
sentence instead of a promise that never resolves. On the page, a `shared/shell` module SHALL
send a request and return a promise that resolves when the reply with its id arrives. Where there
is no shell — a plain browser tab over a dev server — the module SHALL say so once, and the
sections that need a shell SHALL draw that answer as itself.

#### Scenario: A method the shell knows

- **WHEN** the page sends a request with method `status`
- **THEN** the shell answers on the channel with the same id and a result the page can parse

#### Scenario: A method the shell does not know

- **WHEN** the page sends a request with a method this shell has no route for
- **THEN** the reply is an error naming the method, and the page's promise rejects with that sentence

#### Scenario: No shell at all

- **WHEN** the SPA runs in a plain browser with no Photino host
- **THEN** the update section says it is not an install rather than showing controls that cannot work

### Requirement: Updating is offered, never done behind somebody's back

The installed app SHALL offer an update — a question in the platform's own dialog, on the UI
thread, stating what the restart costs — and SHALL NOT install anything without the person's
Yes. The check SHALL read the repository's releases feed with the person's own credential
(`GITHUB_TOKEN`, or `gh auth token`), SHALL download the newer release quietly so that Install
means seconds, and SHALL NOT ask on a schedule: the launch checks once, and settings checks when
pressed. A feed that cannot be read is an answer with its sentence, not a silent nothing — the
repository is private, and an anonymous read is a 404 that would look like "no releases" for ever.

#### Scenario: A newer release is checked from settings

- **WHEN** the person presses Check and the feed carries a newer version
- **THEN** the section says the version is available, and the download has already finished quietly

#### Scenario: The question, and what a No means

- **WHEN** the person presses Install and answers the dialog
- **THEN** Yes applies the update and restarts the app; No keeps the downloaded package on disk, offered again at next launch

#### Scenario: No credential on the machine

- **WHEN** Check is pressed with no GITHUB_TOKEN and no signed-in gh
- **THEN** the section shows the feed's own error sentence, and nothing else happens

### Requirement: Applying stops the app's own server before its files move

Applying an update SHALL stop this install's own server — matched on where it runs from, not on
its name — before the files are replaced, because a half-replaced install whose window talks to
its old API is a version mismatch nobody can act on. A stopped download is not a stopped check:
the package already on disk stays, and the next launch or press finds it without fetching a byte.

#### Scenario: The server goes first

- **WHEN** the person answers Yes to the update question
- **THEN** the install's own server is stopped and waited for before the files are replaced, and the app restarts into the new version

#### Scenario: A failure while applying is shown

- **WHEN** the swap fails
- **THEN** the failure is shown in a dialog with its own words, the window closes, and reopening the app starts a healthy pair of the version already installed

### Requirement: The installed app recovers the account's PATH whoever opened it

The installed app SHALL ask the account's login shell — as the user database records it for the
effective user — for the PATH its server runs with, and SHALL fall back to `$SHELL` only when no
record names an existing shell. A launch by the installer under `sudo`, which hands down
`SHELL=/bin/sh`, SHALL produce the same PATH as a launch from the Dock.

#### Scenario: Opened by the installer

- **WHEN** the installer opens the app with `SHELL=/bin/sh` and `USER=root` in its environment
- **THEN** the server's PATH is the one the account's recorded shell answers, with the directories its login configuration adds

### Requirement: An orphaned server is replaced, not adopted

When a window of the installed app finds this install's server answering on its port and no other
window of the install running, it SHALL stop that server and start its own, because a server no
window owns keeps whatever environment it was started with. A server owned by another running window
SHALL still be adopted. A server whose executable is not inside this install SHALL never be stopped.

#### Scenario: A quit that left the server behind

- **WHEN** the app is opened while its previous server still answers and no other window of the install runs
- **THEN** the previous server is stopped and a new one is started with this launch's environment

#### Scenario: A second window

- **WHEN** a window of the install is running and another is opened
- **THEN** the second window adopts the running server

