# ADR-0002: A finished run is read from a record, not from its terminal bytes

- **Status:** Proposed
- **Date:** 2026-08-16
- **Deciders:** repository owner; agent implementing #78
- **Tags:** frontend, backend, architecture

## Context

A finished run cannot be read back. Opening one shows the tail of a terminal with no history: you
cannot scroll up to the beginning of what the agent wrote, and a normal terminal tab can. #78
diagnosed why from the code — an agent CLI is a full-screen TUI, `RunSink.Append` stores what the
pseudo-terminal produced verbatim, and replaying those bytes re-enters the alternate screen, which
has no scrollback by definition.

The proposal on #78 named the fix that follows from that diagnosis — consume the alternate-screen
switches on replay, so the transcript is painted on the normal buffer and gets scrollback — and
named the one measurement that decides whether it works: **how much of a real transcript is
alternate-screen repaint**. If most of a run is line-by-line output, stacking the repaints is
acceptable. If most of it is repaint, the proposal said, stacking produces N superimposed screens.

Two real Claude Code runs were launched, driven to completion and measured. The transcripts came
back from `GET /api/runs/{id}` — the same bytes the terminal replays — and were replayed through
`@xterm/headless`, the emulator the app itself uses.

| | run A, 81,191 bytes | run B, 81,850 bytes |
| --- | --- | --- |
| Normal buffer | 8,082 bytes — **10.0%** | 647 bytes — **0.8%** |
| Alternate screen | 73,109 bytes — **90.0%** | 81,203 bytes — **99.2%** |
| Entries into the alternate screen | 1 | 1 |

The normal-buffer bytes are entirely the preamble: the shell echoing the command line, the CLI's
banner, and the workspace-trust dialog. **Everything the agent did happens inside one alternate
span**, entered once and left once, at the end.

The anatomy of that span is what settles it. Across 81,203 bytes, run B emitted **856 absolute
cursor moves**, **279 erase-in-line sequences**, **2 full-screen erases** — and **zero line feeds
and zero scroll operations**. The TUI never advances the screen. It addresses cells inside a
40-row viewport and overwrites them.

So replaying it on the normal buffer does not stack N screens. It repaints one screen 856 times.
Measured on the same emulator the app uses, at 120×40:

| | as stored, today | with the switches consumed |
| --- | --- | --- |
| `buffer.active.type` | `normal` (`alternate` if the run was stopped mid-flight) | `normal` |
| Scrollback rows | 0 | **2** |
| Non-empty rows reachable | 6 | 33 |
| Reaches the start of the conversation | no | **no** |

Two rows of scrollback, for a run of either length. The reader is left with the agent's last frame
and nothing above it: in run B, `Write(a.txt)` through `Write(d.txt)` and the prompt itself are
unreachable. The transform **passes check 2 of the issue and fails check 1** — the buffer is no
longer alternate, and the run still cannot be read back.

The content is not merely hidden, it was never in the stream as history. What scrolled out of the
agent's viewport existed only as cells the next frame overwrote; the bytes are present as
fragments, the layout that made them a conversation is not. This is also why the follow-on the
proposal anticipated — splitting the two buffers at capture, as orca's producer does — does not
reach it either. orca serialises a normal-buffer scrollback plus an alternate frame, and a TUI's
scrolled-off lines are absent from its `scrollbackAnsi` for exactly the same reason.

## Decision

We will treat a finished run as a **record the server keeps**, and stop trying to recover one from
the run's terminal bytes.

The terminal bytes stay what they are and are still the right thing for a live run: `RunSink` keeps
writing them verbatim, and a running agent is still an emulator with a keyboard attached. What
changes is the source a finished run is read from. The bytes are a faithful recording of a screen,
and a screen is not a record — no transform of a stream of cell writes can reconstruct the history
that stream deliberately discarded.

Where that record comes from is the follow-on issue's decision, not this one's. The strongest
candidate is already in the codebase: `ClaudeUsageReader` locates `~/.claude/projects/…/{runId}.jsonl`
by the run's own id — because the run pins `--session-id` — and the file for run B holds 63 rows,
16 of them assistant messages: the whole conversation, structured, searchable and copyable. The
product already reads that file for cost.

## Consequences

- **Positive:** the objective of #78 — read a finished run whole, search it, copy from it — becomes
  reachable. A record can be paginated, searched and selected as text, which a 10,000-row emulator
  buffer cannot. It also stops the transcript table being the thing a person reads through, which
  is the cost F2's tail exists to bound.
- **Negative:** the faithful render of the TUI is lost for a finished run. Colour, box drawing and
  the agent's own layout are a screen, and a record is not a screenshot of it. The reader who wants
  to see exactly what the agent's interface looked like at the end no longer gets it for free.
- **Negative:** a record read from a vendor's own transcript is per-vendor. `AgentCatalog` is
  already the one place that knows a vendor's name, and this adds a second thing each vendor must
  be able to answer.
- **Neutral:** #78 closes on this finding rather than on a fix, and the work moves to the follow-on
  issue with the measurement attached. The partial improvement the reverted shape did deliver —
  showing the agent's last frame instead of the shell's leftovers — is a separate, smaller claim
  and can be its own item.

## Alternatives considered

- **Consume the alternate-screen switches on replay** (the shape proposed on #78, built and
  reverted on this branch) — rejected because it was measured: 2 rows of scrollback, and the start
  of the conversation still unreachable. It passes the proxy check and misses the goal.
- **Split the two buffers at capture, in `RunSink`, as orca's producer does** — rejected as the
  answer on its own, because the lines that scrolled out of the alternate viewport are not in the
  captured stream at all. It would make the transition ours to own, which is real, and still leave
  nothing above the last frame.
- **Run a headless emulator on the server and record the rows the alternate viewport evicts** —
  not rejected, but not chosen here: the TUI overwrites rows in place rather than scrolling, so
  "evicted" has to be inferred by diffing frames, which is a heuristic rather than a reading.
- **Leave it, and tell people to read the agent's own log** — rejected because the product's
  central screen is the transcript, and a run whose history is only outside the product is a run
  the product did not record.

## References

- Related: ADR-0001 — the agent terminal runs in a browser, not a desktop shell
- Issue #78 — a finished run cannot be read back, because its replay re-enters the alternate screen
- `renderer/src/components/terminal-pane/terminal-snapshot-replay-paint.ts` in `~/projects/study/orca`
- `src/shared/AiOrchestrator.Infrastructure/Agents/ClaudeUsageReader.cs` — already reads the
  agent's own transcript, keyed by the run's id
