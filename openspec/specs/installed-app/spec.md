# installed-app Specification

## Purpose
The installed app is one process: the server with the frontend inside it, packed as the .NET tool `aio`,
with the browser as its window and the PWA as its icon. Whatever runs on the developer's
machine is what would run on a shared server, and it listens on loopback because its one person is
everyone it trusts. Reverse-engineered from the shipped code
(`src/root/AiOrchestrator.Server/Program.cs`, `Installed/`, `src/frontend/public/`, ADR-0008, ADR-0009, ADR-0010), not
negotiated ahead of it.
## Requirements
### Requirement: One process serves the API and the frontend, same-origin
The Server SHALL serve the API under `/api` and the built frontend as static files from its own
directory, falling back to `index.html`, with no API base URL and no CORS. Files under `/assets/` that
exist SHALL be cached as immutable, and every other response outside `/api` SHALL be revalidated. In
development the Server SHALL proxy the same paths to the Vite dev server instead. A path under `/api`
that no route maps SHALL answer 404 in every habitat: it SHALL NOT fall back to `index.html` nor be
handed to the Vite dev server, whose proxy sends `/api` back to the Server.

#### Scenario: A window held open across an update
- **WHEN** an update replaces the bundle while a browser window is open on the old one
- **THEN** the next navigation revalidates `index.html` and loads the new chunks

#### Scenario: An unknown API route in development
- **WHEN** a browser on the Vite dev server asks for `/api/no-existe`
- **THEN** Vite proxies it to the Server, and the Server answers 404 without proxying it back

### Requirement: The app is the .NET tool aio
`dotnet pack` of the Server SHALL write `artifacts/aio.<Version>.nupkg`, a .NET tool with the package id
and command `aio`, for `net10.0` and framework-dependent, carrying the built frontend in `wwwroot`, with
`<Version>` from `src/Directory.Build.props`. `scripts/pack` SHALL build the frontend and then
pack it. Run from that package, by `dnx aio` or an installed `aio`, the Server SHALL be the installed app.
Given `--version` it SHALL print the version and exit 0, given `--help` a usage
line and exit 0, given `service install|uninstall|status` it SHALL do what the login service
requirement says, and given any other argument it SHALL start nothing, say that the harness command is
`.aio/aio` and exit 2.

#### Scenario: The harness command typed at the tool
- **WHEN** an agent with the tool on its PATH runs `aio issue view 1`
- **THEN** no app starts, it reads that the command is `.aio/aio`, and the exit code is 2

#### Scenario: Running it once
- **WHEN** a person runs `dnx aio --add-source ./artifacts`
- **THEN** the app starts on `http://127.0.0.1:48117`, opens the browser there and `/api/version` answers the package's version

### Requirement: Installed, it migrates, takes its port and opens the browser
Installed as the .NET tool, the Server SHALL write its log to `~/.aiorchestrator/server.log`, keep its
database in `~/.aiorchestrator/`, take the login shell's PATH, migrate its own database before
listening with the same call the MigrationService makes, which also drops the tables no module
migrates any more, and listen on `http://127.0.0.1:48117` unless that port answers as something else, then open
the default browser there, unless `AIO_BROWSER` is `none`. A second launch that finds the same version
already answering SHALL only open the browser and exit. A launch that finds another version answering
SHALL say both versions, ask it to stop with `POST /api/app/stop` (or `/api/console/stop` when that
version has no such route), wait up to 30 seconds for the port and listen there; when the port stays
taken it SHALL say so and exit 1. `POST /api/app/stop` SHALL exist only in the installed tool and SHALL
answer 403 to a request that is not from loopback or that carries an `Origin` header.

#### Scenario: Launching it twice
- **WHEN** the app is running and a person launches the same version again
- **THEN** the browser opens on the running app and no second server starts

#### Scenario: A newer version finds an older one running
- **WHEN** 0.3.1 answers on 48117 and a person launches 0.3.2
- **THEN** 0.3.2 prints `aio 0.3.1 is running at http://127.0.0.1:48117; this is 0.3.2.`, 0.3.1 stops, and 0.3.2 listens on 48117

#### Scenario: A checkout has no stop for a newer launch
- **WHEN** something posts to `/api/app/stop` on a server started from a checkout
- **THEN** the answer is 404

### Requirement: Settings stops the console
`POST /api/console/stop` SHALL answer `{stopping: true}` and then stop the process gracefully, half a
second later. Closing the browser SHALL NOT stop it. Every live session SHALL end with the process,
and nothing SHALL start it again.

#### Scenario: Stopping the installed app
- **WHEN** a person stops the console from Settings
- **THEN** the page says the console stopped, and the Server process exits

### Requirement: Identity is one person, on loopback only
With `Identity:Reach` at its default, `this-machine`, the host SHALL refuse to start when it would
listen on anything but loopback, and the one person SHALL hold every permission. A request type
that declares nothing SHALL be refused.

#### Scenario: Listening on a public address
- **WHEN** `ASPNETCORE_URLS` names `http://192.168.1.10:5080`
- **THEN** the host does not start, and says it is not a machine one person owns

### Requirement: Updates are checked against nuget.org and installed from Settings
`GET /api/updates` SHALL answer the last check without asking anything new, and `POST /api/updates/check`
SHALL read `https://api.nuget.org/v3-flatcontainer/aio/index.json` without a credential. Both SHALL say
the running version, the newest stable version on nuget.org, how the app was started (`tool`, `dnx` or
`checkout`) and a state: `unchecked`, `up-to-date`, `available`, `unpublished` when nuget.org answers 404,
or `error` with its reason. Run as the tool, the app SHALL check once when it starts. Settings SHALL
give `dotnet tool update -g aio` with a copy button to an installed tool, and say that the next
`dnx aio` runs the new version to one started by `dnx`.
`POST /api/updates/install` SHALL, in the installed tool only, run `dotnet tool update aio` with `-g`
when the tool is in `~/.dotnet/tools` and with `--tool-path` for any other folder, and answer whether it
succeeded with the command and its output. On success it SHALL restart half a second later: started by
the login service, by exiting with 75 so the service starts the new version; started by hand, by
launching the tool's command detached, with no browser, and stopping. Anywhere else it SHALL answer 409
and run nothing, and to a request whose `Origin` names another host it SHALL answer 403.

#### Scenario: A newer version on nuget.org
- **WHEN** the installed tool runs 0.3.0 and nuget.org lists 0.3.0 and 0.3.1
- **THEN** the check answers `available` with 0.3.1, and Settings shows `dotnet tool update -g aio`

#### Scenario: Nothing published yet
- **WHEN** nuget.org answers 404 for the index of `aio`
- **THEN** the check answers `unpublished`, and Settings says no version is published yet

#### Scenario: A checkout opens Settings
- **WHEN** Settings reads the updates on a server started from a checkout
- **THEN** the answer is `unchecked` with `how` `checkout`, and nuget.org was not asked

#### Scenario: A checkout is asked to install an update
- **WHEN** something posts to `/api/updates/install` on a server started from a checkout
- **THEN** the answer is 409 and no command runs

### Requirement: The installed tool starts at login as a user service
`aio service install` SHALL register the installed tool's command to start at login with no terminal
and start it now: on macOS a LaunchAgent `~/Library/LaunchAgents/<label>.plist` with `Label`,
`ProgramArguments` naming the tool's command, `RunAtLoad`, `KeepAlive` `{SuccessfulExit: false}`, the
environment it needs (`HOME`, `DOTNET_ROOT`, `AIO_SERVICE`, `AIO_BROWSER=none`, and `ASPNETCORE_URLS` when
set) and its output to `~/.aiorchestrator/server.log`, loaded with `launchctl bootstrap gui/<uid>` unless
it is already loaded; on Linux a `systemd --user` unit with `Restart=on-failure`, enabled with
`--now`. The label SHALL be `aio` unless `AIO_SERVICE_LABEL` names another.
`aio service uninstall` SHALL unload it, which stops its process, and delete the file. `aio service
status` SHALL say whether the file is there and which version answers on the app's address. On Windows,
and from a process that is not the installed tool, each verb SHALL say why it cannot and exit 2.
`GET /api/app/service` SHALL answer `how`, whether the platform is supported and whether the service is
registered; `PUT /api/app/service {registered}` SHALL install or uninstall it in the installed tool and
answer 409 anywhere else. Uninstalled from Settings by the service's own process, only the file SHALL go.

#### Scenario: Registering the service on macOS
- **WHEN** a person runs `aio service install` with the tool installed in `~/.dotnet/tools`
- **THEN** `~/Library/LaunchAgents/aio.plist` runs `~/.dotnet/tools/aio`, launchd loads it, and `launchctl print gui/<uid>/aio` shows it running

#### Scenario: The service's process is killed
- **WHEN** the service's process dies with `kill -9`
- **THEN** launchd starts it again, and a clean exit such as Stop console is not restarted

#### Scenario: Windows
- **WHEN** a person runs `aio service install` on Windows
- **THEN** it says starting at login is not supported on Windows yet and exits 2

### Requirement: The browser installs it as a PWA
The frontend SHALL carry `manifest.webmanifest` (standalone, with its icons) and a service worker that
caches nothing, registered outside development, so Chrome and Edge offer to install the console as an
app with its own window.

#### Scenario: Installing from Chrome
- **WHEN** a person opens the installed app's address in Chrome
- **THEN** Chrome offers to install it, and the installed window opens the same server

### Requirement: In a checkout, the development instance keeps its data under src/artifacts/data
Run from a checkout through the AppHost, the instance SHALL keep its SQLite database at
`<checkout root>/src/artifacts/data/aiorchestrator.db`, and its runs and its scripts' outputs beside it,
where the AppHost already leaves what it generates. It SHALL write nothing under `.aio/`. The
checkout root SHALL be the nearest directory above the AppHost holding `.git`, resolved through
symlinks, so deleting a worktree deletes its data and two checkouts never share a database.
Publishing a recording to the `aio/evidence` branch SHALL read it from this data directory as from
any other.

#### Scenario: A fresh worktree starts the development instance
- **WHEN** `aspire start` runs in a worktree that has never run it
- **THEN** `src/artifacts/data/aiorchestrator.db` exists in that worktree, migrated, and `.aio/data/` does not

#### Scenario: Two worktrees run at once
- **WHEN** two worktrees of the same repository each run `aspire start`
- **THEN** each has its own `src/artifacts/data/aiorchestrator.db`, and a folder added in one does not appear in the other

