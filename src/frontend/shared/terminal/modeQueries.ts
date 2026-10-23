import type { IDisposable, Terminal } from "@xterm/xterm";

/**
 * Answers DECRQM — "is this mode set?" — ourselves, because xterm's own handler throws.
 *
 * <b>What it costs to leave alone.</b> `@xterm/xterm@6.0.0`'s `requestMode` opens with a leftover
 * `enum` declaration whose object is built and immediately discarded — `let r; (P => …)(r ||= {})`
 * — and nothing reads `r` afterwards. Minifying the app drops the declaration as unused and keeps
 * the assignment, so the shipped bundle runs `(void 0 || (r = {}))`: a write to an undeclared name,
 * which in a module is a `ReferenceError`. It is thrown out of the parser, and a parser that throws
 * takes xterm's write buffer with it — the offset it was reading at is never advanced, so *every*
 * byte after the query, in that write and in every write after it, is never drawn. The pane freezes
 * on whatever was already on screen.
 *
 * Which looks exactly like a terminal that does not work, for some agents and not others: Claude
 * Code never asks, and its pane was fine. Copilot CLI and OpenCode both ask on startup — before
 * they draw anything — so both panes showed the shell's first two lines and then nothing, for as
 * long as the tab was open.
 *
 * <b>Why here rather than a patched dependency.</b> The most recently registered handler for a
 * sequence is tried first and `true` ends the chain, so registering ours is the supported way to
 * take a sequence off xterm's hands. It needs no patch file to carry, and it keeps working when the
 * dependency moves.
 *
 * <b>What it answers.</b> `0` — "mode not recognized" — for everything. The honest report needs
 * xterm's internal mode state, which is not on the public terminal, and 0 is what a terminal that
 * does not know a mode is required to say. Both TUIs draw their interface on that answer; it is
 * also the answer they act on when a terminal says nothing at all, which is what they got from this
 * pane before, several seconds later, once their own detection timed out.
 *
 * The report goes out through {@link Terminal.input} rather than straight to the socket, so it
 * leaves by the same route as every other answer xterm generates and is subject to the same rule
 * about not typing a replay's answers back at a live process.
 */
export function answerModeQueries(terminal: Terminal): IDisposable {
  const report = (params: (number | number[])[], priv: boolean) => {
    const first = params[0];
    // A parameter can arrive with subparameters — `1;2:3` — and only the parameter itself is the
    // mode being asked about.
    const mode = Array.isArray(first) ? (first[0] ?? 0) : (first ?? 0);

    terminal.input(`\x1b[${priv ? "?" : ""}${mode};0$y`, false);
    return true;
  };

  const handlers = [
    // DECRQM, private: `CSI ? Ps $ p`. The form every TUI uses, and the one that was crashing.
    terminal.parser.registerCsiHandler({ prefix: "?", intermediates: "$", final: "p" }, (params) =>
      report(params, true)
    ),
    // DECRQM, ANSI: `CSI Ps $ p`. Same handler, same fault in it.
    terminal.parser.registerCsiHandler({ intermediates: "$", final: "p" }, (params) =>
      report(params, false)
    ),
  ];

  return { dispose: () => handlers.forEach((handler) => handler.dispose()) };
}
