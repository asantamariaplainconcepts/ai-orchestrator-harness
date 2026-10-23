import { useEffect, useState } from "react";
import { t } from "@/shared/i18n";
import { Button } from "@/shared/ui/kit";

/**
 * The keys a phone cannot type into a terminal: Esc, Tab, Ctrl, the arrows and Enter.
 *
 * A touch keyboard has no Esc and no arrows — the two things an agent's TUI is *made of*: its menus
 * move with ↑↓, their answer is Enter, and their way out is Esc. On a phone that pane would be a
 * screen somebody can watch and cannot steer, so the terminal grows a row of the keys the glass
 * left out, under the pane, on a **coarse pointer only**.
 *
 * **The row is about what hands have available, not how much room the layout has.** The test is
 * the primary pointer's type — `(pointer: coarse)` — and not the window's width: a laptop with a
 * touchscreen is a fine-pointer machine that also happens to be touchable, and a person there has
 * the real keys. This is why the phone's 390px pass (#218) and this row are separate facts: a
 * narrow window with a keyboard needs nothing here, and a wide tablet without one needs all of it.
 *
 * **Bytes, not intents.** Every key sends exactly what a physical press of it produces — the
 * caller hands this the same `send` its keyboard path uses, a binary frame on the session's
 * WebSocket, so a byte from a thumb and a byte from a key are the same wire. Never "submit this
 * line": at a TUI menu a line followed by CR confirms whatever is highlighted, the one keypress the
 * app must never make on somebody's behalf.
 *
 * Back with #548: the first tree had it (#395), and #509 rebuilt the terminal without it.
 *
 * **Ctrl latches, and the next key anywhere consumes it.** Pressing Ctrl arms it and the row says
 * so by staying highlighted; the next key — on this row, or a letter typed on the soft keyboard —
 * consumes it, and the latch clears. Arrows carry it as the CSI modifier parameter (`ESC[A`
 * becomes `ESC[1;5A`); a typed letter becomes its control byte (`\x03` for c — the interrupt an
 * agent's TUI listens for), which is why the letter half of the mixing is
 * {@linkcode controlByteFor}, exported for the terminals to apply where `onData` flows. Hardware
 * Ctrl never passes through here armed: the OS delivers its byte already made. It is not a toggle
 * that re-arms, because a forgotten armed Ctrl eating the next genuine keystroke is a surprise in
 * the one place surprises cost the most.
 *
 * Composed of `Button` from the kit — the terminal's dark surface is the pane's own exception in
 * DESIGN.md, and does not extend to the chrome around it. The size is `sm`, not the `xs` the
 * terminal's header uses: a thumb is the pointer here, and `xs` is a pointer-finger size.
 */
export function TerminalKeys({
  send,
  armed,
  onArm,
}: {
  /** Receives exactly what a physical press of the same key would have produced. */
  send: (bytes: string) => void;
  /** The Ctrl latch, held by the caller so typing on the soft keyboard can consume it too. */
  armed: boolean;
  onArm: (armed: boolean) => void;
}) {
  const coarse = useCoarsePointer();
  if (!coarse) return null;

  /** The row's keys, in order: Esc first because it is the way out, Enter last because it commits. */
  const row: readonly { id: string; label: string; bytes: string }[] = [
    { id: "esc", label: t("terminal.keys.esc"), bytes: "\x1b" },
    { id: "tab", label: t("terminal.keys.tab"), bytes: "\t" },
    { id: "ctrl", label: t("terminal.keys.ctrl"), bytes: "" },
    { id: "up", label: t("terminal.keys.up"), bytes: "\x1b[A" },
    { id: "down", label: t("terminal.keys.down"), bytes: "\x1b[B" },
    { id: "left", label: t("terminal.keys.left"), bytes: "\x1b[D" },
    { id: "right", label: t("terminal.keys.right"), bytes: "\x1b[C" },
    { id: "enter", label: t("terminal.keys.enter"), bytes: "\r" },
  ];

  const take = (key: (typeof row)[number]) => {
    if (key.id === "ctrl") {
      onArm(!armed);
      return;
    }
    // Ctrl on an arrow is that arrow's modifier parameter, not a different key. Everything else
    // sends its plain bytes and sets the latch down: Ctrl+Enter and Ctrl+Tab are not distinct
    // bytes in a VT, and pretending they are would invent a key no program listens for.
    const bytes =
      armed && key.bytes.startsWith("\x1b[")
        ? `${key.bytes.slice(0, 2)}1;5${key.bytes.slice(2)}`
        : key.bytes;
    onArm(false);
    send(bytes);
  };

  return (
    <div
      role="toolbar"
      aria-label={t("terminal.keys.label")}
      style={{
        display: "flex",
        gap: 6,
        flex: "none",
        padding: "6px 8px",
        borderTop: "1px solid var(--border)",
        background: "var(--muted)",
        overflowX: "auto",
      }}
    >
      {row.map((key) => (
        <Button
          key={key.id}
          variant={key.id === "ctrl" && armed ? "default" : "outline"}
          size="sm"
          aria-pressed={key.id === "ctrl" ? armed : undefined}
          onClick={() => take(key)}
          style={{ flex: "none", fontFamily: "var(--font-mono)" }}
        >
          {key.label}
        </Button>
      ))}
    </div>
  );
}

/**
 * The byte a Ctrl-held letter sends, or null when the input is not a single letter.
 *
 * Lives here and not in the terminals because both panes need the same mixing for the row's
 * latch — and it is the row's latch, so the row's file is where a reader looks for what it does.
 */
export function controlByteFor(data: string): string | null {
  if (data.length !== 1 || !/[a-z]/i.test(data)) return null;
  return String.fromCharCode(data.toUpperCase().charCodeAt(0) & 0x1f);
}

/**
 * Whether the primary pointer is coarse. Observed rather than inferred from width, for the reason
 * the component's own comment gives. False where `matchMedia` does not exist, which is nowhere
 * this app runs, but keeps the hook honest in a test.
 */
function useCoarsePointer(): boolean {
  const [coarse, setCoarse] = useState(
    () => typeof window !== "undefined" && window.matchMedia("(pointer: coarse)").matches,
  );

  useEffect(() => {
    const query = window.matchMedia("(pointer: coarse)");
    const read = () => setCoarse(query.matches);
    read();
    query.addEventListener("change", read);
    return () => query.removeEventListener("change", read);
  }, []);

  return coarse;
}
