import { FitAddon } from "@xterm/addon-fit";
import { Terminal } from "@xterm/xterm";
import "@xterm/xterm/css/xterm.css";
import { useEffect, useRef, useState } from "react";
import { attachUrl } from "@/shared/http/api";
import { t } from "@/shared/i18n";
import { answerModeQueries } from "./modeQueries";
import { controlByteFor, TerminalKeys } from "./TerminalKeys";
import { openWebLinks } from "./webLinks";

/**
 * A live session's terminal over its WebSocket (#508, #509).
 *
 * The first tree's `AgentTerminal` spoke SignalR with acknowledgements and a server-side frame; this
 * wire is plainer, so this is the same emulator with less around it. Everything the server sends
 * is a binary frame of the bytes the session printed, the replay of its tail first. A binary frame
 * sent is keystrokes and a text frame `{"columns","rows"}` resizes the PTY. Unmounting closes the
 * socket, which detaches this viewer and leaves the run running.
 *
 * Beside it: the link handler and the answer to the mode queries an agent asks on its first line,
 * both of which are about xterm rather than about the wire.
 */
export function SessionTerminal({ runId }: { runId: string }) {
  const host = useRef<HTMLDivElement>(null);

  // The effect's `send`, reached by the touch-keys row without the effect re-running on each arm.
  const sendRef = useRef<((data: string) => void) | null>(null);

  // The row's Ctrl latch. Held here rather than in the row so a letter from the soft keyboard can
  // consume it, and mirrored in a ref so `onData` reads it without subscribing again.
  const [ctrlArmed, setCtrlArmed] = useState(false);
  const ctrlArmedRef = useRef(false);
  const setCtrl = (armed: boolean) => {
    ctrlArmedRef.current = armed;
    setCtrlArmed(armed);
  };

  useEffect(() => {
    const element = host.current;
    if (!element) return;

    // A canvas cannot resolve a custom property, so the font and the colours are read here and
    // handed to xterm as values. The neutral ramp is the same in both themes: the terminal keeps
    // its dark surface whichever the app shows (DESIGN.md, the one deliberate exception).
    const style = getComputedStyle(element);
    const token = (name: string) => style.getPropertyValue(name).trim() || undefined;

    const terminal = new Terminal({
      cursorBlink: true,
      scrollback: 10_000,
      fontFamily: token("--font-mono") ?? "monospace",
      fontSize: 12,
      theme: {
        background: token("--pc-neutral-900"),
        foreground: token("--pc-neutral-50"),
        cursor: token("--pc-blue-400"),
      },
    });

    const fit = new FitAddon();
    terminal.loadAddon(fit);
    const links = openWebLinks(terminal);
    terminal.open(element);
    const modeQueries = answerModeQueries(terminal);

    const socket = new WebSocket(attachUrl({ runId }));
    socket.binaryType = "arraybuffer";

    // xterm answers the queries it parses through `onData`, which is the agent's keyboard. The
    // first frame is the replay of output the agent printed before this viewer came, so an answer
    // produced while it is parsed is an answer to an old question and is dropped.
    let first = true;
    let replaying = 0;

    const send = (data: string) => {
      if (replaying > 0 || socket.readyState !== WebSocket.OPEN) return;
      socket.send(new TextEncoder().encode(data));
    };

    const sendSize = () => {
      if (socket.readyState === WebSocket.OPEN)
        socket.send(JSON.stringify({ columns: terminal.cols, rows: terminal.rows }));
    };

    // A pane narrower than this is a layout mid-flight, and a size measured from it would have the
    // agent redraw at a width nobody can read. The next real measurement replaces it.
    const measure = () => {
      if (element.clientWidth < 240 || element.clientHeight < 40) return false;
      try {
        fit.fit();
        return true;
      } catch {
        return false;
      }
    };

    socket.onopen = () => {
      measure();
      sendSize();
    };

    socket.onmessage = (event: MessageEvent) => {
      if (!(event.data instanceof ArrayBuffer)) return;
      const bytes = new Uint8Array(event.data);
      if (first) {
        first = false;
        replaying += 1;
        terminal.write(bytes, () => {
          replaying -= 1;
        });
      } else {
        terminal.write(bytes);
      }
    };

    socket.onclose = (event: CloseEvent) => {
      const line = event.reason
        ? t("terminal.ended", { reason: event.reason })
        : t("terminal.lost");
      terminal.write(`\r\n\x1b[90m› ${line}\x1b[0m\r\n`);
    };

    // Both keyboards share `send`, the physical one through here and the touch row through the ref.
    // A letter typed while the row's Ctrl is latched goes as its control byte: Ctrl+C is the
    // interrupt every agent listens for, and a phone has no other way to send it.
    sendRef.current = send;
    const typed = terminal.onData((data) => {
      if (ctrlArmedRef.current) {
        const held = controlByteFor(data);
        if (held !== null) {
          ctrlArmedRef.current = false;
          setCtrlArmed(false);
          send(held);
          return;
        }
      }
      send(data);
    });

    const observer = new ResizeObserver(() => {
      if (measure()) sendSize();
    });
    observer.observe(element);

    terminal.focus();

    return () => {
      sendRef.current = null;
      observer.disconnect();
      typed.dispose();
      socket.onclose = null;
      socket.close(1000, "Detached.");
      modeQueries.dispose();
      links.dispose();
      terminal.dispose();
    };
  }, [runId]);

  return (
    <div style={{ flex: 1, minHeight: 0, display: "flex", flexDirection: "column" }}>
      <div
        ref={host}
        style={{
          flex: 1,
          minHeight: 0,
          padding: "8px 10px",
          borderRadius: "var(--radius-lg)",
          background: "var(--pc-neutral-900)",
        }}
      />
      <TerminalKeys armed={ctrlArmed} onArm={setCtrl} send={(bytes) => sendRef.current?.(bytes)} />
    </div>
  );
}
