## MODIFIED Requirements

### Requirement: The app is the .NET tool aio
`dotnet pack` of the Server SHALL write `artifacts/aio.<Version>.nupkg`, a .NET tool with the package id
and command `aio`, for `net10.0` and framework-dependent, carrying the built frontend in `wwwroot`, with
`<Version>` from `src/Directory.Build.props`. `.harness/repo/pack` SHALL build the frontend and then
pack it. Run from that package, by `dnx aio` or an installed `aio`, the Server SHALL be the installed app.
Given `--version` it SHALL print the version and exit 0, given `--help` a usage
line and exit 0, given `service install|uninstall|status` it SHALL do what the login service
requirement says, and given any other argument it SHALL start nothing, say that the harness command is
`.harness/aio/aio` and exit 2.

#### Scenario: The harness command typed at the tool
- **WHEN** an agent with the tool on its PATH runs `aio issue view 1`
- **THEN** no app starts, it reads that the command is `.harness/aio/aio`, and the exit code is 2

#### Scenario: Running it once
- **WHEN** a person runs `dnx aio --add-source ./artifacts`
- **THEN** the app starts on `http://127.0.0.1:48117`, opens the browser there and `/api/version` answers the package's version
