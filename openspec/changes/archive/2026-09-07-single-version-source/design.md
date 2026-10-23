## Context

The product ships as a Server (the BFF host), a Shell (the Photino desktop wrapper) and a frontend SPA, across four modules. None of them carries a version: `<Version>`, `<VersionPrefix>`, `AssemblyVersion` and `InformationalVersion` are absent from `src/Directory.Build.props` and every `.csproj`, and `src/frontend/package.json` has no `version` field. The one place a number appears is `CFBundleShortVersionString 0.1`, a literal in `src/root/AiOrchestrator.Shell/mac/bundle.sh` with no source behind it — put there because there was nothing to derive it from.

ADR-0004 fixes the direction: the console is given to somebody who does not have the repository. At that point the version stops being cosmetic — it becomes what that person says when something fails, what a future updater compares, and what distinguishes two binaries that look the same from the outside.

## Goals / Non-Goals

**Goals:**
- One MSBuild property is the single source of the version, inherited by every project under `src/` without a `.csproj` edit.
- The macOS bundle reads that property rather than carrying a literal.
- A person who has only the app — no repository, no terminal — can read the version, inside the settings screen.
- Changing the version in one site and rebuilding changes it everywhere.

**Non-Goals:**
- Release automation. No tags, no changelog, no artifact publishing.
- The updater. Comparing versions is distribution work (#267); this is the prerequisite.
- Versioning policy beyond choosing semver and a starting number. A person writes the literal; deriving from git does not work without a repository, which is the case that motivates this change.
- Schema versioning. `__EFMigrationsHistory_*` already exists and is a different question.
- Windows or Linux bundle versioning. `bundle.sh` is macOS-only; a Windows `.iss` or a Linux `.desktop` would be a separate change when they exist.

## Decisions

### The source is `<Version>` in `src/Directory.Build.props`

`<Version>` populates `AssemblyInformationalVersion` and `PackageVersion` for every project that inherits the file — the Server, the Shell and the four modules, without touching any `.csproj`. It sits in the existing `PropertyGroup` beside `TargetFramework`, `Nullable` and `TreatWarningsAsErrors`, appended to the end. One property, one file, and every assembly carries it.

Alternative considered: a separate `Directory.Build.props` under `src/root/` for the host projects. Rejected — it would mean two files to keep in step, and the version applies to the modules too, not only the shell.

Alternative considered: `Directory.Packages.props` central version management. Rejected — that is for package versions this product consumes, not for the product's own version.

### The bundle reads it via `dotnet msbuild -getProperty:Version`

`bundle.sh` already runs `dotnet publish`. Before that, it evaluates the version by invoking `dotnet msbuild src/root/AiOrchestrator.Shell/AiOrchestrator.Shell.csproj -getProperty:Version -nologo` and trimming the output. The result is written into the plist heredoc as a variable rather than a literal. This is fast enough (under 2 seconds on the machine this runs on) and avoids parsing a `.csproj` with `sed` or `grep`.

Alternative considered: reading `AssemblyInformationalVersionAttribute` from the published assembly via reflection. Rejected — the bundle writes the plist before publishing completes, and reading the attribute after publish would mean reordering the script for no gain. MSBuild evaluation is the direct read of the property.

### The endpoint is `GET /api/version` on the Server, mapped in `Program.cs`

Version is a host concern, not a module's — it describes the product, not a capability. Mapping it in `Program.cs` beside `MapDefaultEndpoints` keeps it out of the module discovery, which is the property the arrangement exists to protect: adding a module changes nothing here, and the version is not a feature of any module.

The handler reads `AssemblyInformationalVersionAttribute` from the entry assembly (`typeof(Program).Assembly`), which carries the `<Version>` value. The response is `{ "version": "0.1.0" }`. No `IUseCase`, no CQS pipeline, no permission — the version is public information; the health endpoint does not require a permission either.

Alternative considered: a use case in the Workspace module, beside `GetPreferences`. Rejected — preferences are per-machine state the user can change; the version is a build-time fact nobody changes from the app, and putting it in a module suggests it belongs to that module's vocabulary.

### The frontend reads it through a TanStack Query and shows it in the settings screen

A `useVersion` query, keyed `["version"] as const`, fetches `GET /api/version`. `SettingsScreen.tsx` shows a read-only line at the bottom — a `Label` and a monospace value — using the same `row` style the screen already defines. The copy comes from `en.ts`, two strings: the label and the value format.

Alternative considered: showing it in the shell header beside the app name. Rejected — the header is shared by every screen, and the version is something a person looks up, not something that belongs in their peripheral vision on every route.

### The versioning policy is semver, starting at `0.1.0`

Three parts because semver requires three. `0.1.0` rather than `0.1` because `0.1` carries no patch, and a future updater comparing `0.1.0` to `0.1.1` needs the third part to exist from the first release. A person writes the literal in `Directory.Build.props`; this change sets it to `0.1.0` and future bumps are one-line edits.

Alternative considered: deriving the version from git (`git describe --tags`). Rejected — the case that motivates this change is the one where there is no repository, which is precisely where git derivation produces nothing.

## Risks / Trade-offs

- **`src/Directory.Build.props` is touched by every branch.** Mitigation: append one line to the end of the existing `PropertyGroup`, reorder nothing — rule 1 of *What collides*.
- **`dotnet msbuild -getProperty:Version` adds a step to `bundle.sh`.** Mitigation: it runs before `dotnet publish`, the step the script already takes. If it fails, `set -euo pipefail` halts the script — the same guard that already protects the rest.
- **The version shown in the app is the Server's version, not the Shell's.** They are the same `Directory.Build.props`, so the values are identical by construction. If they ever diverge, the Server's is what a person reads, which is what they care about — the Server is the product.
