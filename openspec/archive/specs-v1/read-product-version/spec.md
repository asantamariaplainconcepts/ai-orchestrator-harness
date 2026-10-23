# read-product-version Specification

## Purpose
TBD - created by archiving change single-version-source. Update Purpose after archive.
## Requirements
### Requirement: One MSBuild property is the single source of the version

`src/Directory.Build.props` SHALL declare `<Version>` in its existing `PropertyGroup`, so every project under `src/` inherits the same value into `AssemblyInformationalVersion` without a `.csproj` edit. The property SHALL be a semver string of three parts, starting at `0.1.0`.

The version SHALL move when work lands: a pull request merged to master SHALL carry, in its close-out edit, the patch's next revision — or the minor's, where the pull request's title is feature-shaped and the person chose to say so — so that the tag the merge creates names a master whose source already carries the number the tag says. A master whose `<Version>` did not move across a merge is a state the release step names rather than fixes: no script SHALL edit `Directory.Build.props` on master after the merge.

#### Scenario: Every assembly carries the version

- **WHEN** the solution is built
- **THEN** the Server, the Shell and every module assembly carries `AssemblyInformationalVersion` set to the value of `<Version>`

#### Scenario: Changing the version in one site changes it everywhere

- **WHEN** `<Version>` is changed in `src/Directory.Build.props` and the solution is rebuilt
- **THEN** every assembly's `InformationalVersion`, the bundle's `CFBundleShortVersionString` and the value shown in the settings screen all reflect the new value

#### Scenario: A merge carries its own bump

- **WHEN** a pull request is squash-merged to master through the loop's close-out
- **THEN** the squash commit's `Directory.Build.props` carries the next patch revision (or the next minor, where the person said so), and the tag the merge creates names that number

#### Scenario: A forgotten bump is named, not invented

- **WHEN** a merge lands whose close-out did not move `<Version>`
- **THEN** the release step says the version did not move rather than editing master after the merge, and no tag is created over a number the source does not carry

### Requirement: The macOS bundle reads the version from the build rather than carrying a literal
`src/root/AiOrchestrator.Shell/mac/bundle.sh` SHALL evaluate the version through `dotnet msbuild -getProperty:Version` against the Shell project and write the result into `CFBundleShortVersionString`, rather than carrying a hardcoded number. The literal `0.1` in the plist heredoc SHALL be removed.

#### Scenario: The bundle's version matches the build
- **WHEN** the bundle is built
- **THEN** `plutil -extract CFBundleShortVersionString raw <app>/Contents/Info.plist` returns the value of `<Version>` from `Directory.Build.props`

#### Scenario: No hardcoded version remains in the bundle script
- **WHEN** `grep` searches `bundle.sh` for `CFBundleShortVersionString`
- **THEN** it finds the property written from a variable, not a literal number

### Requirement: A version endpoint returns the running product's version
The host SHALL expose `GET /api/version` mapped in `Program.cs`, returning `{ "version": "<string>" }` where the value is the `AssemblyInformationalVersion` of the entry assembly. The endpoint SHALL not declare a permission — the version is public information, the same as the health endpoint.

#### Scenario: The endpoint returns the current version
- **WHEN** `GET /api/version` is called
- **THEN** the response is `200 OK` with `{ "version": "0.1.0" }`

#### Scenario: The version reflects a change in the source
- **WHEN** `<Version>` is changed to `0.2.0` and the server is rebuilt and started
- **THEN** `GET /api/version` returns `{ "version": "0.2.0" }`

### Requirement: The settings screen shows the version a person is running
`SettingsScreen.tsx` SHALL show a read-only line at the bottom of the screen displaying the version string fetched from `GET /api/version`, using the shared `row` style the screen already defines. The string SHALL come from `shared/i18n/en.ts`, not hardcoded in the component.

#### Scenario: A person reads the version without leaving the app
- **WHEN** the settings screen is opened
- **THEN** a line at the bottom shows the version string the endpoint returned

#### Scenario: The version line is read-only
- **WHEN** the settings screen is rendered
- **THEN** the version line has no editable control, no toggle and no button — it displays the value

