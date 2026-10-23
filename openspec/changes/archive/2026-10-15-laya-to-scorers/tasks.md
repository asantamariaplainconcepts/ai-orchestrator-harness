## 1. Move laya

- [x] 1.1 `git mv .harness/aio/laya .harness/aio/laya-requirements.txt .harness/aio/scorers/laya/`, keeping `laya`'s execute bit; the requirements' content is unchanged, so the pin is too
- [x] 1.2 Point the comments of `.harness/aio/risk-score`, `.harness/aio/diff-risk` and `laya`'s own header (its `building venv from …` line among them) at `.harness/aio/scorers/laya/`
- [x] 1.3 `HarnessScripts` in `IScriptRunner.cs`: `Scorers` and the tool map; `Find` looks for `laya` in `.harness/aio/scorers/laya/` first, then where any script is (D1), with its doc comment
- [x] 1.4 `StateScripts.EnsureModel`'s comment names `.harness/aio/scorers/laya/laya ensure`

## 2. Package and install

- [x] 2.1 `package.props` embeds `aio\scorers\laya\*` (D3), and its comment says what `aio/` holds
- [x] 2.2 `HarnessPackage.Executable` marks an extension-less file straight in `aio/scorers/<tool>/` (D2)
- [x] 2.3 `HarnessPackage.Former` maps `aio/scorers/<tool>/<file>` to `.harness/aio/<file>` (D4)
- [x] 2.4 `.harness/package/version` from 34 to 36, past #765, which took 35

## 3. Docs

- [x] 3.1 `.harness/README.md`: the `aio/` row of *What is whose* names `checks/`, `lib/`, `schemas/` and `scorers/laya/`, and says the steps are flat in `aio/`
- [x] 3.2 `README.md` (the `score` row) and `ARCHITECTURE.md` (laya's pin and link) name `.harness/aio/scorers/laya/`

## 4. Tests

- [x] 4.1 `HarnessPackageTests`: the route lists `.harness/aio/scorers/laya/laya`, executable, and `.harness/aio/scorers/laya/laya-requirements.txt`, plain, and no `.harness/aio/laya`; version `36`; `Former` of both new paths is `.harness/aio/<file>`
- [x] 4.2 `HarnessPackageTests`: the README test takes the folders of `.harness/aio/` from `HarnessPackage.Files` and fails naming one the `aio/` row leaves out (D5)
- [x] 4.3 `InstallHarnessTests`: over a default branch with `.harness/aio/laya` and `.harness/aio/laya-requirements.txt` (harness 34) the pull request holds only `scorers/laya/`, git sees both as renames, and `scorers/laya/laya` is `100755`; the harness-23 test leaves the package and harness 23's `.harness/laya-requirements.txt`, nothing else (D4)
- [x] 4.4 `StateScriptsTests` and `ScoreUnknownTests` write laya at `.harness/aio/scorers/laya/laya`; `StateScriptsTests` also shows that `Find` answers `.harness/aio/laya` only where `scorers/laya/laya` is not
- [x] 4.5 `LayaScriptTests` copies both files from `.harness/aio/scorers/laya/` and expects `aio/` to hold only `scorers/`; `DiffRiskTests` passes
- [x] 4.6 `AppTests` expects `the app ships 36`, and its comment names #759
- [x] 4.7 `dotnet test` on the Workspace, Loop and Infrastructure test projects, green
