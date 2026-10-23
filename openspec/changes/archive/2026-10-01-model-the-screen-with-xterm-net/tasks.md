# Tasks: model-the-screen-with-xterm-net

## 1. Package adoption

- [x] 1.1 Add `XTerm.NET` to `src/Directory.Packages.props` with a comment naming what it is (the
  headless VT emulator behind the reattach frame, #397) and reference it from
  `AiOrchestrator.Modules.Runs.csproj` only.
      req: read-a-run/1
      files: src/Directory.Packages.props, src/modules/Runs/AiOrchestrator.Modules.Runs/AiOrchestrator.Modules.Runs.csproj
- [x] 1.2 Confirm the package restores against net10.0 with warnings-as-errors
  (`dotnet restore` on the solution) before writing any code against it.
      req: read-a-run/1
      files: src/Directory.Packages.props, src/modules/Runs/AiOrchestrator.Modules.Runs/AiOrchestrator.Modules.Runs.csproj

## 2. Engine swap behind `RunFrames`

- [x] 2.1 Rewrite `RunFrames.Entry` to hold an XTerm.NET `Terminal` created at
  `RunFrames.DefaultCols/DefaultRows` with scrollback 0; `Feed` writes each stdout chunk's text
  via `terminal.Write`, `Resize` calls the terminal's resize — both under the existing per-run
  lock; stdout-only feeding, `Forget`, and the create-on-first-bytes behavior stay as they are.
      req: read-a-run/1
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/RunFrames.cs
- [x] 2.2 Write the buffer-walking serializer (design D3): walk `terminal.Buffer.Lines`, emit SGR
  per attribute change plus cell text per line, absolute final cursor position, output shape
  matching what `ScreenFrame.Serialize()` produced so `GetRun.cs:262` and the client are
  untouched.
      req: read-a-run/1
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/RunFrames.cs
- [x] 2.3 Delete `ScreenFrame.cs` and `ScreenFrameTests.cs`.
      req: read-a-run/1
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/ScreenFrame.cs, src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/ScreenFrameTests.cs

## 3. Tests

- [x] 3.1 Write `RunFramesTests` in `src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/`
  testing `RunFrames` directly, same shape `ScreenFrameTests` had: plain text, cursor movement,
  ED/EL, SGR, resize, wide glyphs.
      req: read-a-run/1
      files: src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/RunFramesTests.cs
- [x] 3.2 Add cases for the sequences the old parser skipped: DECSTBM scroll region, IL, DL, ECH
  and DECAWM wrapping.
      req: read-a-run/1
      files: src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/RunFramesTests.cs
- [x] 3.3 Add an assertion that a stderr chunk does not touch the screen and that `Finish`/
  `Forget` empties the tracker.
      req: read-a-run/1
      files: src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/RunFramesTests.cs

## 4. Gate and check

- [x] 4.1 Run `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`
  and record the full test count in the issue check.
      req: read-a-run/1
      files: openspec/changes/model-the-screen-with-xterm-net/tasks.md
- [x] 4.2 Verify against reality per the issue's check: attach to a live Claude Code TUI run from
  a second tab mid-run (full screen drawn, compared against the agent's own TUI), then a
  scroll-region TUI (opencode with a long list) with no shifted or duplicated lines.
      req: read-a-run/1
      files: openspec/changes/model-the-screen-with-xterm-net/tasks.md
- [x] 4.3 Record in the check that `ScreenFrame.cs` no longer exists in the tree.
      req: read-a-run/1
      files: openspec/changes/model-the-screen-with-xterm-net/tasks.md
