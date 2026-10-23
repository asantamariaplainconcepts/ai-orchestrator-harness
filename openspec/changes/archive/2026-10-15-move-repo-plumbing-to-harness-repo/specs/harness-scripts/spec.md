## MODIFIED Requirements

### Requirement: The engine finds a script by its name
A script named in `loop.yaml` (`script: gate`, `script: [archive, merge]`) SHALL be a name, never a
path, and the engine SHALL run the first of `.harness/<name>`, `.harness/repo/<name>`,
`.harness/aio/<name>` and `.harness/scripts/<name>` that exists in the checkout (`HarnessScripts.Find`),
so the repository's own script, at its root or in `repo/`, wins over the package's (#760). The last is the layout before harness 24: a repository that has not
installed 24 yet SHALL keep running until it does. A name with a slash or a leading dot SHALL fail
without running anything, and a name no folder has SHALL fail saying so. `prepare` and `cleanup`
SHALL be found the same way. `laya` is a tool, not a step: it SHALL be found first at
`.harness/aio/scorers/laya/laya`, and only where that file is not, in the folders any script is
found in, so a repository that has not installed harness 36 still has its `.harness/aio/laya`
found until the install moves it (#759). An agent's PATH SHALL start with the checkout's
`.harness/aio`, then `.harness/scripts`, so a bare `aio` is the checkout's in either layout.

#### Scenario: The gate and the claim
- **WHEN** the engine runs `script: gate` and `script: claim` in a checkout of the new layout
- **THEN** it runs `.harness/gate` and `.harness/aio/claim`

#### Scenario: A repository that has not updated
- **WHEN** a checkout has its scripts only in `.harness/scripts/`
- **THEN** `script: gate` runs `.harness/scripts/gate`, as before harness 24

#### Scenario: laya in its folder
- **WHEN** a checkout has `.harness/aio/scorers/laya/laya`
- **THEN** `HarnessScripts.Find(checkout, "laya")` answers that file

#### Scenario: laya left beside the steps
- **WHEN** a checkout has `.harness/aio/laya` and no `.harness/aio/scorers/laya/laya`
- **THEN** `HarnessScripts.Find(checkout, "laya")` answers `.harness/aio/laya`

#### Scenario: laya in both places
- **WHEN** a checkout has `.harness/aio/laya` and `.harness/aio/scorers/laya/laya`
- **THEN** `HarnessScripts.Find(checkout, "laya")` answers `.harness/aio/scorers/laya/laya`

#### Scenario: A script in repo/
- **WHEN** a checkout has `.harness/repo/archive` and no `archive` at the root of `.harness/`
- **THEN** `HarnessScripts.Find(checkout, "archive")` answers `.harness/repo/archive`, and `script: archive` runs it

#### Scenario: The root before repo/
- **WHEN** a checkout has both `.harness/gate` and `.harness/repo/gate`
- **THEN** `HarnessScripts.Find(checkout, "gate")` answers `.harness/gate`

### Requirement: release publishes to the feed and tags once the push is accepted
`release [--source <url-or-folder>] [--no-tag] [commitish]` SHALL read the version from
`src/Directory.Build.props` at the commit named and run, in order and stopping at the first that fails:
`.harness/repo/pack`; `dotnet nuget push artifacts/aio.<version>.nupkg --source <feed>`, with
`--api-key` from `NUGET_API_KEY` when it is set; and the tag `v<version>` with a GitHub release of notes
and no files, unless `--no-tag`. The feed SHALL default to `https://api.nuget.org/v3/index.json`. The
feed SHALL be asked whether it lists the version (the flat container named by the feed's service index,
or the package in the folder) once, before packing, and never after the push: nothing SHALL wait for
nuget.org to list a version it accepted. After a push a remote feed accepted, `why` SHALL say so:
"nuget.org accepted aio <version>; it lists it once it validates the package, usually 15 to 30
minutes" (#724). A refused push SHALL stop the step with nothing tagged. A version the feed
already lists SHALL not be packed or pushed; unless `--no-tag`, its tag and release SHALL still be made
when origin has no `v<version>`. A `v<version>` origin already has on the commit with its release SHALL be
said to be done, and one on another commit SHALL stop the step without moving it (#711). The `release`
workflow SHALL write the script's line to its run summary. The key SHALL appear in no output of the script: its line, and whatever it passes on
from `pack` and `dotnet`. It answers `{published, version, feed, tag, release, observed, why}` (#681).

#### Scenario: A release in order
- **WHEN** a person runs `release` with `NUGET_API_KEY` set and nuget.org does not list the version
- **THEN** `pack` runs, then the push, then the release, with no read of the feed after the push; the line says `published` and `observed`, its `why` says nuget.org lists the version once it validates it, and the key is in no output

#### Scenario: A refused push
- **WHEN** `dotnet nuget push` fails
- **THEN** the step stops with exit 1, `published: false` and no tag, and no release is created

#### Scenario: A version published but not tagged
- **WHEN** the feed already lists the version and origin has no `v<version>`
- **THEN** nothing is packed or pushed, the tag and its release are made on the commit, and the line says `published: false` with the tag and `observed`

#### Scenario: A version published and tagged
- **WHEN** the feed lists the version and `v<version>` is on the commit with its release
- **THEN** nothing is packed, pushed or tagged, and the line says it is done with `observed`

#### Scenario: A tag on another commit
- **WHEN** the feed lists the version and `v<version>` is on another commit
- **THEN** the step stops with exit 1, the line names both commits, and the tag is not moved

### Requirement: trust-sweep lists the trust left for folders that are gone
`.harness/repo/trust-sweep` SHALL read Claude Code's configuration, `$CLAUDE_CONFIG_DIR/.claude.json`
or `~/.claude.json`, and answer one JSON line with the file, how many folders it trusts, how many of
those are no longer on disk, how many it removed, their paths and a why. A trust entry SHALL be
`projects[<folder>]` with `hasTrustDialogAccepted` true. Without `--remove` it SHALL write nothing; with
it, it SHALL remove exactly the entries it listed and leave every other key of the file as it was,
writing through a rename that keeps the file's mode. It SHALL stay out of the harness package, because
it is about the machine and not the repository. A test that launches a real `claude` SHALL give it a
`CLAUDE_CONFIG_DIR` of its own, so no test writes the person's file.

#### Scenario: Listing without removing
- **WHEN** the file trusts three folders, two of them gone, and names a fourth gone folder it does not trust
- **THEN** `trust-sweep` answers `trusted: 3`, `gone: 2`, `removed: 0` with the two paths, and the file is byte for byte the same

#### Scenario: Removing
- **WHEN** the same file is swept with `--remove`
- **THEN** the two entries are gone, the trusted folder on disk, the untrusted entry and every other key stay, and the mode is unchanged
