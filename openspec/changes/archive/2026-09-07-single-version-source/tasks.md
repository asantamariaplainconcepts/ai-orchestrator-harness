## 1. The single source

- [x] 1.1 Add `<Version>0.1.0</Version>` to the existing `PropertyGroup` in `src/Directory.Build.props`, appended at the end
      req: read-product-version/1
      files: src/Directory.Build.props
- [x] 1.2 Verify the value flows into `AssemblyInformationalVersion` by building the Server and reading the attribute
      req: read-product-version/1
      files: src/Directory.Build.props

## 2. The macOS bundle

- [x] 2.1 Replace the `CFBundleShortVersionString` literal `0.1` in `src/root/AiOrchestrator.Shell/mac/bundle.sh` with a variable read from `dotnet msbuild -getProperty:Version`
      req: read-product-version/2
      files: src/root/AiOrchestrator.Shell/mac/bundle.sh
- [x] 2.2 Verify no hardcoded version remains: `grep "CFBundleShortVersionString" bundle.sh` shows the variable, not a number
      req: read-product-version/2
      files: src/root/AiOrchestrator.Shell/mac/bundle.sh

## 3. The version endpoint

- [x] 3.1 Add `GET /api/version` to `src/root/AiOrchestrator.Server/Program.cs`, returning `{ "version": "<InformationalVersion>" }` from the entry assembly, mapped beside `MapDefaultEndpoints`
      req: read-product-version/3
      files: src/root/AiOrchestrator.Server/Program.cs, src/shared/AiOrchestrator.ServiceDefaults/ServiceDefaults.cs
- [x] 3.2 Verify the endpoint returns the current version: `curl http://127.0.0.1:5080/api/version` returns `{"version":"0.1.0"}`
      req: read-product-version/3
      files: src/shared/AiOrchestrator.ServiceDefaults/ServiceDefaults.cs

## 4. The frontend

- [x] 4.1 Add a `Version` interface to `src/frontend/shared/http/contracts.ts`
      req: read-product-version/4
      files: src/frontend/shared/http/contracts.ts
- [x] 4.2 Add a `useVersion` query to `src/frontend/shared/http/queries.ts`, keyed `["version"] as const`, fetching `GET /api/version`
      req: read-product-version/4
      files: src/frontend/shared/http/queries.ts
- [x] 4.3 Add two strings to the Settings block in `src/frontend/shared/i18n/en.ts`: the label and the value format
      req: read-product-version/4
      files: src/frontend/shared/i18n/en.ts
- [x] 4.4 Add a read-only version line at the bottom of `src/frontend/features/settings/SettingsScreen.tsx` using the shared `row` style
      req: read-product-version/4
      files: src/frontend/features/settings/SettingsScreen.tsx

## 5. The gate

- [x] 5.1 Run `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj` and confirm every test assembly passes with zero failures
- [x] 5.2 Run `openspec validate "single-version-source" --strict` and confirm it passes
