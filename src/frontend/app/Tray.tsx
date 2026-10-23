import { CircleStop, SquareTerminal } from "lucide-react";
import { useEffect, useState } from "react";
import type { LiveSession } from "@/shared/http/api";
import { explain } from "@/shared/http/client";
import { useEndSession, useSessions } from "@/shared/http/queries";
import { t } from "@/shared/i18n";
import { ago } from "@/shared/ui/ago";
import { Button, Dialog } from "@/shared/ui/kit";
import { useNarrowContainer } from "@/shared/ui/useNarrowContainer";

/** The name a session goes by in the tray and over its terminal. */
export function sessionName(session: LiveSession): string {
  const agent = session.provider ?? t("tray.terminal");
  // A session opened on a folder (#516) has no PBI; it goes by its folder's last segment.
  return (
    session.pbi === null
      ? t("tray.chipFolder", {
          agent,
          folder: session.folderPath.split(/[\\/]/).filter(Boolean).pop() ?? session.folderPath,
          command: session.command,
        })
      : t("tray.chip", { agent, pbi: session.pbi, command: session.command })
  ).trim();
}

/**
 * The session tray (DESIGN.md#the-screens): the live sessions, each once, and nothing else.
 * One press on a chip opens its terminal over the screen and another closes it; neither stops the
 * run. The stop button beside it ends the session, after {@link EndSession} asks (#577). A session that
 * ended leaves the list, because what it wrote is a recording in the Journey.
 *
 * It starts nothing: a run starts from the loop or from a PBI, and a manual session from the rail's
 * `…` menu or the PBI's header.
 */
export function Tray({
  openId,
  onToggle,
  onEnd,
}: {
  openId: string | null;
  onToggle: (session: LiveSession) => void;
  onEnd: (session: LiveSession) => void;
}) {
  const sessions = useSessions();
  const live = sessions.data ?? [];
  // Below this the note goes: at 375px it ran off the right edge (#515), and the chips need the room.
  const [measure, narrow] = useNarrowContainer<HTMLElement>(720);

  // A poll that brings back the same list renders nothing, so the ages would stop. A tick keeps
  // "12m" true while a session sits there.
  const [, tick] = useState(0);
  useEffect(() => {
    if (live.length === 0) return;
    const timer = setInterval(() => tick((n) => n + 1), 5_000);
    return () => clearInterval(timer);
  }, [live.length]);

  return (
    <section
      ref={measure}
      aria-label={t("tray.label")}
      style={{
        flex: "none",
        display: "flex",
        alignItems: "center",
        gap: 12,
        minHeight: 44,
        padding: "6px 16px",
        borderTop: "1px solid var(--border)",
        background: "var(--sidebar)",
        color: "var(--muted-foreground)",
        fontSize: "var(--text-xs)",
        whiteSpace: "nowrap",
        minWidth: 0,
      }}
    >
      <span style={{ display: "inline-flex", alignItems: "center", gap: 6, flex: "none" }}>
        <SquareTerminal size={14} aria-hidden />
        {t("tray.count", { count: String(live.length) })}
      </span>

      {/* The chips scroll on their own, so the tray is never wider than the screen. */}
      <div
        style={{
          flex: 1,
          minWidth: 0,
          display: "flex",
          alignItems: "center",
          gap: 12,
          overflowX: "auto",
        }}
      >
        {sessions.error ? (
          <span role="alert" style={clipped}>
            {t("tray.failed", { reason: sessions.error.message })}
          </span>
        ) : live.length === 0 ? (
          <span style={clipped}>{t("tray.empty")}</span>
        ) : (
          live.map((session) => {
            const name = sessionName(session);
            const shown = openId === session.runId;
            return (
              <span
                key={session.runId}
                style={{ flex: "none", display: "inline-flex", alignItems: "center", gap: 2 }}
              >
                <Button
                  variant={shown ? "secondary" : "outline"}
                  size="sm"
                  aria-pressed={shown}
                  aria-label={shown ? t("tray.close", { name }) : t("tray.open", { name })}
                  title={shown ? t("tray.close", { name }) : t("tray.open", { name })}
                  onClick={() => onToggle(session)}
                  style={{ flex: "none", fontSize: "var(--text-xs)" }}
                >
                  <span style={{ fontWeight: 500 }}>{name}</span>
                  <span
                    style={{ fontFamily: "var(--font-mono)", color: "var(--muted-foreground)" }}
                  >
                    {session.stale
                      ? t("tray.silent", { ago: ago(session.lastOutputAt) })
                      : ago(session.startedAt)}
                  </span>
                </Button>
                <Button
                  variant="ghost"
                  size="icon-sm"
                  aria-label={t("tray.endOne", { name })}
                  title={t("tray.endOne", { name })}
                  onClick={() => onEnd(session)}
                  style={{ color: "var(--muted-foreground)" }}
                >
                  <CircleStop size={14} aria-hidden />
                </Button>
              </span>
            );
          })
        )}
      </div>

      {narrow ? null : <span style={{ flex: "none" }}>{t("tray.note")}</span>}
    </section>
  );
}

/**
 * The one line before a session is ended, from the tray or over its terminal (#577). What it says
 * depends on what dies: a loop run fails and the loop holds its PBI on the next pass, as with
 * *Stop run*; a manual run on a PBI keeps its recording in the Journey; a folder session leaves
 * nothing. Cancel, the scrim and Escape end nothing.
 */
export function EndSession({ session, onClose }: { session: LiveSession; onClose: () => void }) {
  const end = useEndSession();
  const name = sessionName(session);

  useEffect(() => {
    const escape = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    document.addEventListener("keydown", escape);
    return () => document.removeEventListener("keydown", escape);
  }, [onClose]);

  const line = !session.manual
    ? t("tray.end.loop", { command: session.command, pbi: session.pbi ?? "" })
    : session.pbi === null
      ? t("tray.end.folder", { name })
      : t("tray.end.manual", { name, pbi: session.pbi });

  return (
    <Dialog
      title={t("tray.end")}
      onClose={onClose}
      width={460}
      footer={
        <>
          {end.error ? (
            <span role="alert" style={{ fontSize: "var(--text-xs)", color: "var(--destructive)" }}>
              {t("calls.pbi.refused", { reason: explain(end.error) })}
            </span>
          ) : null}
          <div style={{ flex: 1 }} />
          {/* Focus starts on Cancel: Enter on a dialog that just opened should not kill a process. */}
          <Button variant="outline" size="sm" autoFocus onClick={onClose}>
            {t("calls.pbi.cancel")}
          </Button>
          <Button
            variant="destructive"
            size="sm"
            disabled={end.isPending}
            onClick={() => end.mutate(session, { onSuccess: onClose })}
          >
            {t("tray.end")}
          </Button>
        </>
      }
    >
      <p style={{ margin: 0, color: "var(--muted-foreground)" }}>{line}</p>
    </Dialog>
  );
}

const clipped: React.CSSProperties = { minWidth: 0, overflow: "hidden", textOverflow: "ellipsis" };
