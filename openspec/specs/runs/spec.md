# runs Specification

## Purpose
A run is one execution of a step on a PBI: who did it, where, when, what it cost and how it ended. The
Runs module starts it in an environment, watches it, reads its end and keeps the row, so the sequence
of a PBI's runs is the loop's diary of it and survives the run's terminal. Reverse-engineered from the
shipped code (`src/modules/Runs/AiOrchestrator.Modules.Runs/`), not negotiated ahead of it.

## Requirements
### Requirement: A run is kept in the control plane, not on the machine that looks
Each run SHALL be one row of `pbi_runs` with its repository and PBI, the state it ran in, the folder's
path, the branch, the provider (none for a shell) and model, the command, whether it was manual, when
it started and ended, the environment and working directory it got, its outcome or its failure with the
reason and problems, the raw end record, its input and output tokens and its cost, and the paths of its
end record and recording under the runs directory. A run SHALL end once: a second end SHALL change
nothing. The folder SHALL be referenced by path, not by a foreign key into Workspace.

#### Scenario: The Journey reads a PBI's runs oldest first
- **WHEN** `GET /api/runs?repository=owner/repo&pbi=495` is called
- **THEN** it answers every run of that PBI, oldest first, each with its end record, tokens, cost and whether its recording is on disk

### Requirement: A run is started in a prepared place, and answers at once
`IRunRequests.StartAsync` SHALL register the run as live, insert its row, and return its id while the
run proceeds in the background. The run SHALL ask the environment its folder chose for its state, or
else the configured one (`Runs:Environment`, `worktree` by default), to prepare a place on the PBI's
branch; ask the provider for the launch of its CLI there; ask the environment to turn that launch into
the one that starts inside the place; start that in a session, pump its output, and, when the session
ends, read the end and the cost. The `worktree` environment SHALL return the launch unchanged. The
session, its recording and the end record's path SHALL NOT depend on the environment. The agent's
permission mode SHALL be the environment's own when it declares one, and the instance's otherwise; the
`worktree` declares none. The place request SHALL carry the run's directory, where `AIO_DONE_FILE`
points. A choice naming an environment with no adapter SHALL fail the run with
`EnvironmentFailed` and the environments there are. The place SHALL be retired afterwards unless another
live run uses the same working directory. Nothing in this path SHALL retry.

#### Scenario: An environment wraps the launch
- **WHEN** a folder's `code-review` runs in an environment that wraps the launch in a process of its own
- **THEN** the session starts the wrapped launch, the recording holds the wrapper's output before the agent's, and the end record is read as in a worktree

#### Scenario: An environment that cannot prepare fails the run
- **WHEN** `.aio/steps/prepare` refuses the branch
- **THEN** the run ends with the failure `EnvironmentFailed` and the script's reason, and no agent starts

#### Scenario: A missing CLI fails the run
- **WHEN** the loop names a provider with no adapter, or whose CLI is not on the login shell's PATH
- **THEN** the run ends with `AgentNotFound` and a reason that says what to install

### Requirement: A state's scripts share one place on the PBI's branch
`IRunRequests.OpenScriptPlace` SHALL answer a place for one state's scripts on a PBI. The configured
environment (`Runs:Environment`) SHALL prepare it on the PBI's branch when its first script runs, and
every later script SHALL run in the same working directory, so what one leaves uncommitted is there
for the next. Disposing the place SHALL retire it once, unless a live run uses the same working
directory; a place no script ran in SHALL be neither prepared nor retired. A place that cannot be
prepared SHALL answer each script exit -1 with no line and `<branch> could not be prepared for
<script>: <why>`, and SHALL NOT be prepared again. Nothing here SHALL retry or keep a place between
passes.

#### Scenario: The second script finds what the first left
- **WHEN** the first script of a place writes a file git ignores, and the second looks for it
- **THEN** the second finds it, and the worktree is gone once the place is disposed

#### Scenario: A place that cannot be prepared
- **WHEN** the folder is not a git repository
- **THEN** the script answers exit -1, no line, and `change/<id> could not be prepared for <script>:`
  with the environment's words

### Requirement: The sbx environment runs an agent step in a Docker sandbox per PBI
The `sbx` environment SHALL prepare the PBI's worktree as `worktree` does, then create, or keep, one
sandbox per PBI named `aio-<checkout>-<branch>` with `sbx create --clone claude <checkout> <run
directory>`, a `--deny-network` for each denied host and, right after, `sbx policy allow network
--sandbox` for the allowed ones; it SHALL switch the clone to the PBI's branch as this machine has it.
A sandbox already there SHALL be kept when it mounts the run's directory and made again otherwise.
It SHALL turn a launch into `sbx exec -it -w <checkout>` with every variable of the launch but `PATH`
passed with `-e`, the checkout's `.aio`, then the older layouts' script folders, first on the sandbox's own PATH, and the CLI by
its name. A loopback `AIO_API` SHALL be passed as `host.docker.internal` at the same port, and the
sandbox's allowed hosts SHALL include `localhost:<that port>`: sbx's proxy sends
`host.docker.internal` to the host's `localhost`, and `balanced` answers it 403 until it is allowed
(measured with sbx 0.38.0 on 11 October 2026, #634). Retiring the place SHALL fetch the branch from the sandbox's `sandbox-<name>` remote and
fast-forward the worktree to it, remove the sandbox with `sbx rm --force` and its `github` and
`anthropic` secrets scoped to it, and retire the worktree; a branch that cannot fast-forward SHALL
keep the worktree and say where sbx left the commits. Creating the sandbox, which adds its remote to
the checkout's config, and the fetch, removal and mirror cleanup on retiring SHALL take the same
per-repository turn as `prepare` and `cleanup`. Its agents SHALL run in `yolo` unless
`Environments:Sbx:Permissions` names another mode; `Environments:Sbx:Allow` and
`Environments:Sbx:Deny` SHALL replace the default hosts (allow the .NET hosts `balanced` blocks; deny
`gist.github.com`, `raw.githubusercontent.com`, `*.amazonaws.com`, `*.blob.core.windows.net`). It
SHALL create and store no credential. It SHALL NOT be offered when `sbx` is not on the login shell's
PATH, when `sbx secret ls` fails, or when sbx holds no global `anthropic` secret; it SHALL be offered
with a caution when the only GitHub token is sbx's global one. A run SHALL wait when sbx holds neither
a `github` secret scoped to the PBI's sandbox nor a global one. Scripts SHALL NOT run in it.

#### Scenario: A step runs in a sandbox and its branch comes back
- **WHEN** a folder's `in-progress` runs in `sbx` and its agent commits in the sandbox and calls `aio done next`
- **THEN** the end record is on this machine at the run's path, the recording is the host's PTY around `sbx exec -it`, and after the run the worktree's branch has the commit and no sandbox is left

#### Scenario: sbx is not ready
- **WHEN** sbx is signed out or holds no Claude credential
- **THEN** `/api/machine` leaves `sbx` out of its environments with a note naming the command that fixes it, and a PBI whose state runs in `sbx` waits with that sentence

### Requirement: Where each agent state runs is a setting of its folder
Runs SHALL keep, per repository and per state, the environment its runs are placed in, as one row of
`runs_settings` named `environment:<repository in lower case>:<state>`, read when each run starts. A
state with no row SHALL run in `Runs:Environment`. `GET /api/runs/environments?repository=` SHALL
answer the states with a choice; `PUT /api/runs/environments {repository, state, environment}` SHALL
store one and answer the same, and SHALL refuse with 400 an environment no adapter is registered for,
naming the ones there are. Scripts of the loop SHALL always run in `Runs:Environment`: the gate needs
this machine. `/api/machine` SHALL list the environments offered now, the default always among them,
and a note for each that is not offered or carries a caution, with the command that fixes it.
`IRunRequests.PlacementAsync` SHALL answer the environment a run of a state would get, whether it is a
sandbox, and why it cannot be placed now.

#### Scenario: No choice, and a choice for one state
- **WHEN** nobody chose an environment for `acme/widgets`, and then a person chooses another for its `code-review`
- **THEN** a run of `code-review` first runs in `worktree` and then in the chosen one, and a run of `in-progress` stays in `worktree`

### Requirement: The agent is launched with the run's end record in reach
The Claude Code adapter SHALL resolve `claude` on the login shell's PATH and launch it with
`--permission-mode` of the permission mode (`acceptEdits` for `allow-list`, `auto` for `auto`,
`bypassPermissions` for `yolo`), in `auto` and `yolo` with `--settings` answering the menu Claude Code shows
before it starts in that mode (`skipAutoPermissionPrompt` or `skipDangerousModePermissionPrompt`), `--session-id <run id>`, `--model` when one is set, and the command as
its last argument, at 120×30,
with `.aio` of the worktree, then the older layouts' script folders, first on the PATH, `AIO_DONE_FILE` naming where the end
record goes, and, for `aio issue` (#634), `AIO_API` naming the control plane's first `http` address
(a wildcard host read as `127.0.0.1`) and `AIO_REPOSITORY` naming the PBI's repository. Every agent
launch, OpenCode's and Copilot's, a loop run's and a manual session's, SHALL carry the same three
when the server lists an address and the folder a repository. It SHALL strip the variables that mark a parent agent session, and SHALL NOT add or change
any entry of `~/.claude.json`: trusting a folder is the person's answer to Claude Code's own question.
Launches before this rule marked each entry they wrote with `aiOrchestratorTrust`. When a run's place is
retired, the adapter SHALL first take back the entries marked for that worktree, one the mark says was
created removed whole and one it changed given its earlier answer, and SHALL leave every other entry as
it was. A loop run SHALL be ended five seconds after its end record appears, if the agent has not exited by
then.

The adapter SHALL also say whether Claude Code trusts a folder, read from `~/.claude.json`
(`$CLAUDE_CONFIG_DIR/.claude.json` when set) without writing it: trusted when
`projects[<path>].hasTrustDialogAccepted` is true for the folder, for a directory above it up to and
including its repository's top, or for the main checkout of a linked worktree, which is where Claude
Code 2.1.288 stops looking. A directory above the repository does not count. A file missing or not JSON
trusts nothing.

#### Scenario: A run is not stopped by its first edit
- **WHEN** the coder's run in `allow-list` edits a file and then runs `dotnet test`
- **THEN** neither asks: edits are accepted by the permission mode and `dotnet` is on the allow list of
  `.claude/settings.json`; a command outside that list asks, nobody answers, and the run ends waiting for a person

#### Scenario: A repository under a trusted parent
- **WHEN** `~/projects` is trusted and `~/projects/aio-dogfood`, a repository, is not
- **THEN** the adapter answers that Claude Code does not trust `~/projects/aio-dogfood`

#### Scenario: A run writes no trust
- **WHEN** a `claude-code` run is launched in a folder Claude Code trusts
- **THEN** `~/.claude.json` is byte for byte as it was before the launch

#### Scenario: Retiring a worktree takes back the trust written for it
- **WHEN** `~/.claude.json` holds an entry marked `aiOrchestratorTrust` for a worktree and an entry
  without the mark, and that worktree is retired
- **THEN** the marked entry is taken back and the unmarked one is left as it was

#### Scenario: aio done ends the run
- **WHEN** the agent calls `aio done approved` and stays open
- **THEN** the session is killed five seconds later and the run ends with the outcome `approved`

### Requirement: OpenCode and Copilot are launched like Claude Code, in their own words
The `opencode` and `copilot` adapters SHALL resolve their CLI on the login shell's PATH and launch its
interactive interface at 120×30 with the same PATH, `AIO_DONE_FILE` and stripped variables as Claude
Code. A command `/aio:<name> <arguments>` SHALL be sent as the checkout's `.ai/commands/aio/<name>.md`
without its front matter and with `$ARGUMENTS` replaced, because OpenCode names that file `/aio/<name>`
and Copilot reads no command files; any other command SHALL be sent as it is. In `allow-list`, both SHALL
let edits through without asking and SHALL be given the `Bash(…)` entries of the checkout's
`.claude/settings.json` as the shell commands that run without asking or never run; every other shell
command SHALL ask. In every mode, the denied commands SHALL be given.

OpenCode SHALL be started as `opencode --standalone --session ses_<run id in hex> --prompt <prompt>`,
with the model and the permissions in `OPENCODE_CONFIG_CONTENT`: shell `*` asks, then each allowed
command and denied command, the later rule winning. In `auto` it SHALL add `--auto` after
`--standalone`; in `yolo` it SHALL add `--auto` and the configuration SHALL be shell `*` allowed, then
each denied command. Copilot SHALL be started as
`copilot --allow-tool=write --allow-tool=shell(…) --deny-tool=shell(…) --session-id=<run id>
--model=<model> --interactive=<prompt>`, where an allowed prefix of a command other than `git` and `gh` is
allowed by its first word, the only part Copilot matches. In `auto` the allow flags SHALL be
`--allow-all-tools`, and in `yolo` `--allow-all-tools --allow-all-paths`, each followed by the
`--deny-tool` flags. Before launching, the Copilot adapter SHALL add
the worktree to `trustedFolders` of `~/.copilot/config.json` unless it or a parent is there, keeping the
file's leading comments, and list it in `~/.copilot/aio-trusted-folders.json`; when the place is retired
it SHALL remove only a path it listed.

The models SHALL be what `opencode models` printed at the last probe, asked again after five minutes,
and a static list for Copilot.

#### Scenario: A real run of each CLI ends with aio done
- **WHEN** OpenCode 2.0.23 or Copilot CLI 1.0.95 runs `/aio:ping 549` in a worktree whose command file asks for `aio done approved --reason "ping $ARGUMENTS"`
- **THEN** the end record says `approved` with the reason `ping 549`, the session is recorded, and the agent was not stopped by a permission or trust menu

#### Scenario: Retiring a Copilot worktree takes its trust back
- **WHEN** a Copilot run's worktree is retired
- **THEN** `~/.copilot/config.json` is byte for byte as it was before the run

#### Scenario: The machine lists the three agents
- **WHEN** `GET /api/machine/tools` is called on a machine with `claude`, `opencode` and `copilot` installed
- **THEN** it answers `claude-code`, `copilot` and `opencode`, each with its path, its version and its models

### Requirement: One permission mode for the instance, read when each run starts
What an agent may do without asking SHALL be one mode for the instance, `allow-list`, `auto` or `yolo`,
stored in `runs_settings` and `auto` while nothing is stored (#706); a stored `allow-list` SHALL be kept. It SHALL be read when each run of
the loop, each manual run and each session on a folder starts, and handed to the adapter, so a mode
changed applies to the next one without a restart and a run already going keeps its own.
`GET /api/runs/settings` SHALL answer it as `permissionMode` beside `staleAfterSeconds`, and
`PUT /api/runs/settings {permissionMode}` SHALL store it, refusing any other value with 400; a field
left out of the request SHALL keep what is stored. It is not per folder or per state, and it does not
change the repository's allow list.

#### Scenario: A new database starts in auto
- **WHEN** `GET /api/runs/settings` is called on an instance where nobody stored a mode
- **THEN** it answers `permissionMode` `auto`, and the next run's agent starts in `auto`

#### Scenario: Yolo applies to the next run
- **WHEN** a person stores `yolo` with `PUT /api/runs/settings` and the engine then starts a run
- **THEN** the run's agent is launched in `yolo`, and the stale threshold is what it was

#### Scenario: A real run in yolo runs a command the allow list would have asked about
- **WHEN** claude-code 2.1.288 or OpenCode 2.0.23 runs in `yolo` in a throwaway repository whose allow list has only `aio done`, asked to run `ls /tmp` and end with `aio done`
- **THEN** the command runs without a permission prompt and the run ends with its outcome

### Requirement: The cost is read from the agent's own transcript
For a Claude Code run, the cost SHALL be read from the transcript `~/.claude/projects/**/<run id>.jsonl`
(or found by directory and time window when that file is absent), counting each assistant message once,
with input tokens including the cache reads and writes, and priced per model with the cache read at a
tenth and the cache write at 1.25 times the input price. An unknown model SHALL leave the cost empty
rather than guess. For an OpenCode run, the tokens and the dollar cost SHALL be what
`opencode session export ses_<run id in hex>` answers, input counting the cache reads and writes and
output the reasoning. For a Copilot run, the tokens SHALL be the sum of the `session.usage_record` rows of
`~/.copilot/session-state/<run id>/events.jsonl`, and the cost SHALL be empty, because Copilot bills in
premium requests and AI credits rather than dollars.

#### Scenario: A run on an unpriced model
- **WHEN** a run used a model the price table does not know
- **THEN** its tokens are recorded and its cost is null

### Requirement: A run the server lost is ended from what it left
A run row that has not ended and is not live in this process SHALL be ended the first time it is read,
from its end record on disk when there is one; with none, its reason SHALL start with "The control
plane stopped while the run was live." A lost run SHALL NOT be ended by its age.

#### Scenario: A restart in the middle of a run
- **WHEN** the server restarts while a run is live and the agent had written its end record
- **THEN** the next read of that PBI's runs shows the run ended with that record's outcome

### Requirement: A run whose agent waits for a person ends and says why
A loop run's session that has written nothing for `Runs:PersonWait` (30 seconds by default) SHALL be
read by its provider's adapter, which answers whether its CLI waits for a person: a permission or a
login. The phrases SHALL be each adapter's, never the engine's: Claude Code's adapter SHALL read
`Do you want to proceed?` as a permission, naming the command it draws between the two dashed rules above it, and
`Not logged in`, `Login expired` and `Please run /login` as a login, each only within the last
1000 characters of the screen with cursor moves read as spaces and colours as nothing, so a phrase
the agent printed and went on from is not one it waits at. An adapter that names no phrases SHALL
never end a run this way. When the adapter answers, the session SHALL be killed and the run SHALL fail
with `WaitingForPerson` or `NotLoggedIn` and the adapter's sentence, which says what was asked and
what to do: "Claude Code is not logged in. Run `claude /login` on this machine, then release." or
"The agent asked to run `<command>`. Switch permissions to auto in Settings or add it to the allow
list of .claude/settings.json, then release." Nothing SHALL answer the prompt or log in. A run with
its end record keeps its outcome, a run a person stopped ends `Stopped`, and a manual run is never
ended this way. The stale threshold is not changed.

#### Scenario: A login that expired
- **WHEN** a loop run's Claude Code prints `Login expired · Please run /login` and writes nothing more for 30 seconds
- **THEN** its session is killed and the run fails with `NotLoggedIn` and "Claude Code is not logged in. Run `claude /login` on this machine, then release."

#### Scenario: A command outside the allow list
- **WHEN** a loop run in `allow-list` asks `Do you want to proceed?` about `python3 -c "…"` and writes nothing more for 30 seconds
- **THEN** the run fails with `WaitingForPerson` and a reason that quotes the command

#### Scenario: A phrase the agent went on from
- **WHEN** a session prints `Do you want to proceed?`, goes on writing, then is silent for longer than `Runs:PersonWait` and calls `aio done approved`
- **THEN** the run ends `approved`

### Requirement: A run that stops writing is marked stale
A live run SHALL track when it last wrote output, and SHALL read as stale once it has been silent for
the stale threshold: the seconds stored in `runs_settings`, read every time a run's liveness is, or else
`Runs:StaleAfter` (10 minutes by default). `GET /api/runs/settings` SHALL answer `{staleAfterSeconds}`,
and `PUT /api/runs/settings {staleAfterSeconds}` SHALL store a threshold between 60 and 86400 seconds. Stale SHALL be shown on the Loop screen, where a stale loop
run puts its PBI under "Needs you", the tray and the Journey. Being stale SHALL NOT move the PBI or
write at the vendor, and a manual run that is stale SHALL keep running; a loop run silent that long
without its end record ends as the next requirement says.

#### Scenario: A silent manual run
- **WHEN** a live manual run has written nothing for eleven minutes
- **THEN** it is shown as stale with how long it has been silent, and it keeps running

### Requirement: A loop run silent for the stale threshold without its end record ends waiting for input
A loop run whose session has written nothing for the stale threshold, read again every 5 seconds while
it is live, and whose end record does not exist SHALL have its session killed, as a person's *Stop*
kills it, and SHALL fail with `WaitingForInput` (#764). Its reason SHALL be "The agent wrote nothing
for <threshold> and did not call aio done, so its session was stopped. Do what it asks, then release.
Its screen ended with:" followed by the last 6 lines of its screen that hold a letter or a digit, one
per line. The screen SHALL be rebuilt from what the session printed at the session's size, following
printing, carriage returns, line feeds, cursor moves and erasing, so text a CLI draws with cursor
moves reads as it was shown; with nothing on it the reason SHALL end "Its screen was empty." Whatever
the screen says, the run ends: a CLI that finished its turn and sits at its prompt is waiting as much
as one at a permission prompt. A permission or a login its adapter recognises SHALL still end it
first, after `Runs:PersonWait`, with its own sentence. A run with its end record is ended by it, a
run a person stopped ends `Stopped`, and a manual run is never ended this way. Nothing SHALL answer
the agent or retry the run, and the agents' permissions are not changed.

#### Scenario: An agent that asks a person and sits at its prompt
- **WHEN** a loop run's agent prints `Allow the commit and push, or run it yourself.`, writes nothing
  more for the stale threshold and has not called `aio done`
- **THEN** its session is killed and the run fails with `WaitingForInput` and a reason whose last line
  is `Allow the commit and push, or run it yourself.`

#### Scenario: An agent that writes all along
- **WHEN** a loop run's agent writes every 200 milliseconds for longer than the stale threshold and
  then calls `aio done next`
- **THEN** the run ends `next`
