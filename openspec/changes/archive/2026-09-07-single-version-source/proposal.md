## Why

The product has no version anywhere. The only number in the codebase that answers "what version is this?" is `0.1`, hardcoded as `CFBundleShortVersionString` in `bundle.sh` — a literal with no source behind it, put there because there was nowhere to derive it from. The consequence is already visible: a person who has only the app (the direction ADR-0004 fixes) cannot tell you what they are running, and a diff between two binaries that look identical from the outside is indistinguishable. A single source fixes all three at once: what a person says when something fails, what an updater compares, and what distinguishes two builds.

## What Changes

- Add `<Version>0.1.0</Version>` to the existing `PropertyGroup` in `src/Directory.Build.props`. This is a root file every project under `src/` inherits, so every assembly — Server, Shell, modules — carries the same `AssemblyInformationalVersion` without a `.csproj` edit. Appended to the end of the group, not reordered, matching rule 1 of *What collides*.
- `src/root/AiOrchestrator.Shell/mac/bundle.sh` reads the version from MSBuild evaluation (`dotnet msbuild -getProperty:Version`) rather than from a literal, and writes the result into `CFBundleShortVersionString`.
- A `GET /api/version` endpoint on the Server that returns the assembly's `InformationalVersion` — the value `<Version>` populates — as `{ "version": "0.1.0" }`. Mapped in `Program.cs` alongside the health endpoints, since version is a host concern, not a module's.
- `SettingsScreen.tsx` shows the version in a read-only line at the bottom of the screen, fetched from the endpoint.
- The versioning policy is semver, starting at `0.1.0` — three parts because semver requires three, and `0.1` carries no patch. A person writes the literal; deriving from git does not work when there is no repository, which is precisely the case that motivates this change.

## Capabilities

### New Capabilities
- `read-product-version`: The product's version is a single value in one MSBuild property, readable through an endpoint and shown in the settings screen. One source, three consumers: the assembly, the bundle, the person.

### Modified Capabilities
None. The settings screen gains a read-only line, but `remember-preferences` covers editable fields — version is not one.

## Impact

- `src/Directory.Build.props` — one line appended to the existing `PropertyGroup`. Touched by every branch; added at the end, nothing reordered.
- `src/root/AiOrchestrator.Shell/mac/bundle.sh` — the `CFBundleShortVersionString` literal `0.1` is replaced by `$(dotnet msbuild -getProperty:Version …)`, and the plist heredoc changes from a fixed string to a variable.
- `src/root/AiOrchestrator.Server/Program.cs` — one `MapGet` for `/api/version`, reading `AssemblyInformationalVersionAttribute` from the entry assembly. Three lines, beside `MapDefaultEndpoints`.
- `src/frontend/shared/http/contracts.ts` — a `Version` interface, one line.
- `src/frontend/shared/http/queries.ts` — a `useVersion` query, appended to the file.
- `src/frontend/shared/i18n/en.ts` — two strings under the Settings block: the section label and the value format.
- `src/frontend/features/settings/SettingsScreen.tsx` — a read-only line at the bottom showing the version string.
