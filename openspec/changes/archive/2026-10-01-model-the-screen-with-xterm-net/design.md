# Design: model-the-screen-with-xterm-net

## Context

`RunFrames` tracks one screen per live run so `GET /api/runs/{id}` can hand a late attacher the
whole screen instead of a mid-stream of deltas. The VT interpretation lives in `ScreenFrame.cs` —
836 lines of hand-written parser (CUP/cursor, CR/LF, ED/EL, raw SGR, alt-screen 1049, DECAWM,
origin mode, wide glyphs with a vendored `EastAsianWidth`) whose header states the part it elected
not to model: scroll regions, IL/DL, ECH, tab stops. Agentic TUIs repaint with exactly those. The
live render is xterm.js in the client and never touches this code; only the reattach frame does.

Issue: #397. Seam: `src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/` — one module,
nothing else.

## Goals / Non-Goals

**Goals**

- A late attacher sees the screen the agent drew, whatever sequences drew it.
- The VT parser stops being our code: XTerm.NET becomes the single interpreter.
- 836 lines of parser plus vendored width tables leave the tree.

**Non-Goals**

- Changing the live render or streaming: xterm.js, backpressure-by-acks and the Chunk protocol
  stay as they are.
- The final screen of a finished run — derivable from persisted chunks, a follow-up if it hurts.
- Server-side search over clean text and OSC 133 agent state — paths this opens, not this change.
- Mode 2004 (bracketed paste) in the serialization — dropped closing #193.
- Touching `FolderTerminal`/`TerminalHub`: folder terminals replay raw bytes and do not have this
  problem.

## Decisions

### D1: Adopt XTerm.NET rather than extend `ScreenFrame`

XTerm.NET (MIT, net10.0 — same target as the repo) is a headless VT emulator with the full command
set: DECSTBM, IL/DL/ECH, tab stops, OSC 133, wide characters with wcwidth tables that ship with
parity tests against python's implementation. Every sequence our 836 lines hand-model, plus the
ones the header admits are missing, is already modeled by a library whose only reason to exist is
to model them.

Alternative — keep extending `ScreenFrame` — is the status quo trajectory: the surface grows
every time a TUI uses something new, and each addition is ours to test. Rejected.

Adoption risk accepted knowingly: 32 stars, one main author. Mitigations: MIT license; isolated
behind `RunFrames`, one file; if the library dies, the full parser behind a dying seam lives at a
single point of replacement — strictly better than owning 836 lines.

### D2: `RunFrames` keeps its shape, swaps its engine

`RunFrames` is not rewritten: its role (one tracker per run, `Feed`/`Resize`/`TrySerialize`,
lock per run, forget in `Finish`) is unchanged. Inside, the `Entry`'s `ScreenFrame` becomes an
XTerm.NET `Terminal` created with `new Terminal(new TerminalOptions { Cols = DefaultCols, Rows =
DefaultRows })`, and `Feed` calls `terminal.Write(chunk.Text)` per stdout chunk — still only
stdout, as today, because stderr is raw bytes a serialized screen cannot insert without
reparsing, and nobody has asked.

- Defaults: `RunFrames.DefaultCols/Rows` (120/32, already the launcher's) flow into
  `TerminalOptions`.
- Resize: `terminal.Resize(cols, rows)` under the same per-run lock.
- Scrollback: 0. The tracker is a screen, not history — history is `RunLogLine`.

### D3: A serializer walks the buffer

XTerm.NET is headless and does not serialize to ANSI, so the existing `Serialize()` output shape
is produced by walking `terminal.Buffer.Lines` and emitting, per line, SGR when the active
attribute changes plus each cell's text, then the final cursor position as an absolute CUP. This
is the mechanical half of the work (~150 lines) — reading cells, not interpreting sequences. The
string contract — what `GetRun.cs:262` receives from `frames.TrySerialize` — does not change, so
the SignalR protocol and client do not either.

### D4: Tests are replaced, not patched

`ScreenFrameTests` asserts on the modeled subset — its 11 cases are provenance for the parser
being deleted. It is deleted, and `RunFramesTests` is written in the same place with the same
shape: plain text, SGR, resize, wide glyphs, plus new cases for scroll regions, IL, DL, ECH and
DECAWM. The suite tests `RunFrames` directly, as `ScreenFrameTests` tested `ScreenFrame`, no
host needed.

### D5: Package handling

`XTerm.NET` is pinned in `src/Directory.Packages.props` (central version management) and
referenced only by `AiOrchestrator.Modules.Runs` — the module that owns the seam. Vendoring is
deliberately not chosen: the library is on NuGet, MIT, and the seam already provides the
isolation vendoring would buy.

## Risks / Trade-offs

- [XTerm.NET is small (32 stars, one main author)] → accepted knowingly; MIT, isolated behind
  `RunFrames` (one file), single point of replacement; strictly better than 836 home-grown lines
  that already diverge.
- [A buffer-walking serializer can drift from what a terminal would print] → covered by
  `RunFramesTests` asserting cell-level expectations after feeding real sequences, including the
  previously-missing ones.
- [Mode 2004 (bracketed paste) is not serialized; a viewer's paste bracket is absent] → known and
  accepted closing #193; out of scope unless it hurts again.
- [Behavior change is invisible until a specific sequence is used] → the check verifies against a
  real Claude Code TUI run and a scroll-region TUI, not only the unit suite.

## Migration Plan

One branch, one commit series, no data or schema movement: frames are in-memory and forgotten on
finish; persisted state is untouched. Rollback is reverting the branch — nothing to migrate back.

## Open Questions

None blocking. The serializer's exact emission order (attribute-change SGR before or within a run
line) is an implementation detail settled by matching the existing `Serialize()` output format
the client already renders.
