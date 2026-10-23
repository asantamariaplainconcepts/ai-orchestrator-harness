## Why

Attaching to a live run mid-flight shows half a screen for any agent TUI that repaints with more
than CUP/ED/EL: `ScreenFrame` models a hand-picked subset of VT and says so in its own header
(`ScreenFrame.cs:493`), while Claude Code and opencode repaint with scroll regions, `IL`/`DL`, `ECH`
and tab stops — exactly the part not modeled. The screen serialized for a late attacher is missing
whatever those sequences moved. The fix is not to model more sequences ourselves: XTerm.NET (MIT,
net10.0) is a headless VT emulator whose only job is to model all of them, with wide glyphs carried
by wcwidth tables that include parity tests.

## What Changes

- `ScreenFrame.cs` (836 lines: hand-written VT subset + vendored `EastAsianWidth`) is deleted.
- `ScreenFrameTests.cs` is deleted with it — its cases assert on the subset being replaced — and
  `RunFramesTests.cs` is written in its place, with the same shape and new cases for scroll
  regions, `IL`, `DL`, `ECH` and `DECAWM`.
- `RunFrames` keeps its role (one tracker per live run, feed/resize/serialize, lock per run,
  forget on `Finish`) but each `Entry` holds an XTerm.NET `Terminal` fed with
  `terminal.Write(chunk.Text)` at `DefaultCols`/`DefaultRows`, scrollback 0.
- The frame serialization is re-written against XTerm.NET's buffer: walk `terminal.Buffer.Lines`,
  emit SGR per attribute change plus cell text, absolute final cursor position. The
  `TrySerialize`-returning-a-string contract (`GetRun.cs:262`) is unchanged.
- New package dependency: `XTerm.NET`, pinned in `Directory.Packages.props`, referenced by the
  Runs module only.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `read-a-run`: the requirement *A live run carries its screen as a frame* gains that the screen is
  modeled by a full VT emulator, so a TUI that repaints with scroll regions, insert/delete-line,
  erase-chars or autowrap produces the screen the agent drew rather than the subset a hand-written
  parser happened to model.

## Impact

- **Code**: `src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/` — `ScreenFrame.cs`
  deleted, `RunFrames.cs` rewritten behind its unchanged surface, `RunSink`/`RunLauncher`/`GetRun`
  untouched. No `World.cs` registration changes (there is none for `RunFrames`: it is constructed
  by the Runs module directly).
- **Dependencies**: `XTerm.NET` added to `Directory.Packages.props`; a small library (32 stars,
  one main author) accepted knowingly — MIT, isolated behind `RunFrames`, a single point of
  replacement.
- **Protocol/client**: none — SignalR streaming, the Chunk protocol and the client's xterm.js
  render are untouched; only the server-side reattach frame changes.
- **Out of scope**: final-screen-of-a-finished-run, server-side search over clean text, OSC 133
  agent state, mode 2004 (bracketed paste, dropped closing #193).
