## ADDED Requirements

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
