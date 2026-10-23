import { create, type Player } from "asciinema-player";
import "asciinema-player/dist/bundle/asciinema-player.css";
import { useEffect, useRef } from "react";

/**
 * A run's recording, replayed by `asciinema-player` from the recording endpoint
 * (ARCHITECTURE.md#what-we-did-not-invent; #511). Loaded only when a person presses
 * Play, so the player is not the Journey's to pay for until then.
 *
 * The player draws on the terminal surface in both themes, like the live terminal (DESIGN.md, the
 * one deliberate exception); `app.css` points its two surface colours at the tokens.
 */
export default function Recording({ url, label }: { url: string; label: string }) {
  const host = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const element = host.current;
    if (!element) return;

    const font = getComputedStyle(element).getPropertyValue("--font-mono").trim();
    let player: Player | null = create(url, element, {
      autoPlay: true,
      fit: "width",
      idleTimeLimit: 2,
      terminalFontFamily: font || undefined,
    });

    return () => {
      player?.dispose();
      player = null;
    };
  }, [url]);

  return (
    <div
      ref={host}
      className="aio-recording"
      role="region"
      aria-label={label}
      style={{ width: "100%", minWidth: 0, borderRadius: "var(--radius-lg)", overflow: "hidden" }}
    />
  );
}
