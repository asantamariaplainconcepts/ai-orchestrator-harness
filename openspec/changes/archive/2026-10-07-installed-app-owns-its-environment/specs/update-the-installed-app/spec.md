## ADDED Requirements

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
