import { CircleStop, PanelLeft, X } from "lucide-react";
import { lazy, Suspense, useEffect, useState } from "react";
import { Outlet, useLocation } from "react-router";
import type { LiveSession } from "@/shared/http/api";
import { askSession, clearSessionAsk, useSessionAsk } from "@/features/sessions/ask";
import { useSessions } from "@/shared/http/queries";
import { t } from "@/shared/i18n";
import { Button, Loading } from "@/shared/ui/kit";
import { useTheme } from "@/shared/ui/useTheme";
import { Rail, type RailAsk } from "./Rail";
import { EndSession, sessionName, Tray } from "./Tray";
import { useFirstStart } from "./useFirstStart";

// xterm is not the first screen's to pay for (vite.config.ts, the entry budget): it loads the
// first time somebody opens a session.
// The rail's `+` (#515) is opened now and then, so its dialog loads when it is.
const AddFolderDialog = lazy(() =>
  import("@/features/settings/AddFolderDialog").then((module) => ({
    default: module.AddFolderDialog,
  })),
);

// Terminal and Explore with agent (#516) load the first time somebody opens one.
const SessionsHost = lazy(() =>
  import("@/features/sessions/SessionsHost").then((module) => ({
    default: module.SessionsHost,
  })),
);

const SessionTerminal = lazy(() =>
  import("@/shared/terminal/SessionTerminal").then((module) => ({
    default: module.SessionTerminal,
  })),
);

/** Below this the rail stops sharing the row and opens over the content instead. */
const railFreeAbove = 900;

/** How long a session just opened is waited for in the tray before it is no longer opened over the screen. */
const openWait = 20_000;

/**
 * The shell (#509): the rail, the content area and the session tray, at the root since #518. It
 * owns the theme class, the one open session and the
 * dialogs the rail asks for. A session a person opens by hand (#516), from the rail or a PBI's
 * header, opens over the screen as soon as the tray lists it.
 *
 * The open session's terminal covers the content area and leaves the rail and the tray in view, so
 * the chip that opened it is the one that closes it. Closing detaches: the socket goes, the run
 * does not. Ending, from the chip or over the terminal, kills it after one line of confirmation.
 */
export function Layout() {
  const [theme, toggleTheme] = useTheme();
  const { pathname } = useLocation();
  const narrow = useBelow(railFreeAbove);
  const [railOpen, setRailOpen] = useState(false);
  const [ask, setAsk] = useState<RailAsk | null>(null);
  const [openId, setOpenId] = useState<string | null>(null);
  const [awaitedId, setAwaitedId] = useState<string | null>(null);
  const [ending, setEnding] = useState<LiveSession | null>(null);
  const sessionAsk = useSessionAsk();
  const sessions = useSessions();
  useFirstStart();

  // A session just opened is in the tray once its terminal has started; then it opens over the
  // screen. One that never gets there, a CLI that exits at once, stops being waited for.
  useEffect(() => {
    if (awaitedId === null) return;
    if ((sessions.data ?? []).some((session) => session.runId === awaitedId)) {
      setOpenId(awaitedId);
      setAwaitedId(null);
      return;
    }
    const timer = setTimeout(() => setAwaitedId(null), openWait);
    return () => clearTimeout(timer);
  }, [awaitedId, sessions.data]);

  // A session that ended leaves the list, and its terminal goes with it rather than staying open
  // on a socket the server closed.
  const open = (sessions.data ?? []).find((session) => session.runId === openId) ?? null;
  useEffect(() => {
    if (openId !== null && sessions.data && !open) setOpenId(null);
  }, [openId, open, sessions.data]);

  // Moving to another screen closes the rail drawn over it at a narrow width.
  useEffect(() => setRailOpen(false), [pathname, narrow]);

  useEffect(() => {
    if (!railOpen) return;
    const escape = (event: KeyboardEvent) => {
      if (event.key === "Escape") setRailOpen(false);
    };
    document.addEventListener("keydown", escape);
    return () => document.removeEventListener("keydown", escape);
  }, [railOpen]);

  const toggle = (session: LiveSession) =>
    setOpenId((current) => (current === session.runId ? null : session.runId));

  const rail = (
    <Rail
      theme={theme}
      onToggleTheme={toggleTheme}
      onAsk={(next) => {
        setRailOpen(false);
        if (next.kind === "addFolder") setAsk(next);
        else askSession({ kind: next.kind, folder: next.folder });
      }}
    />
  );

  return (
    <div
      className={theme === "dark" ? "dark" : undefined}
      style={{
        position: "relative",
        display: "flex",
        height: "100vh",
        overflow: "hidden",
        background: "var(--background)",
        color: "var(--foreground)",
        fontFamily: "var(--font-sans)",
        fontSize: "var(--text-sm)",
      }}
    >
      {narrow ? (
        railOpen ? (
          <div className="pc-overlay" style={{ zIndex: 30 }} onClick={() => setRailOpen(false)}>
            <div
              style={{ height: "100%", width: "fit-content" }}
              onClick={(e) => e.stopPropagation()}
            >
              {rail}
            </div>
          </div>
        ) : null
      ) : (
        rail
      )}

      <div style={{ flex: 1, minWidth: 0, display: "flex", flexDirection: "column" }}>
        {narrow ? (
          <div
            style={{
              flex: "none",
              display: "flex",
              alignItems: "center",
              gap: 8,
              padding: "6px 12px",
              borderBottom: "1px solid var(--border)",
            }}
          >
            <Button
              variant="ghost"
              size="icon-sm"
              aria-label={t("rail.open")}
              title={t("rail.open")}
              aria-expanded={railOpen}
              onClick={() => setRailOpen(true)}
            >
              <PanelLeft size={16} />
            </Button>
            <span style={{ fontWeight: 600 }}>{t("app.name")}</span>
          </div>
        ) : null}

        <main style={{ flex: 1, minHeight: 0, position: "relative" }}>
          <div style={{ position: "absolute", inset: 0, overflow: "auto" }}>
            <Outlet />
          </div>

          {open ? (
            <div
              style={{
                position: "absolute",
                inset: 0,
                zIndex: 10,
                display: "flex",
                flexDirection: "column",
                gap: 8,
                padding: 12,
                background: "var(--background)",
              }}
            >
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <span style={{ fontFamily: "var(--font-mono)", fontSize: "var(--text-xs)" }}>
                  {sessionName(open)}
                </span>
                <div style={{ flex: 1 }} />
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => setEnding(open)}
                  aria-label={t("tray.endOne", { name: sessionName(open) })}
                >
                  <CircleStop size={14} aria-hidden />
                  {t("tray.end")}
                </Button>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => setOpenId(null)}
                  aria-label={t("tray.close", { name: sessionName(open) })}
                >
                  <X size={14} />
                  {t("dialog.close")}
                </Button>
              </div>
              <Suspense fallback={<Loading />}>
                <SessionTerminal key={open.runId} runId={open.runId} />
              </Suspense>
            </div>
          ) : null}
        </main>

        <Tray openId={open?.runId ?? null} onToggle={toggle} onEnd={setEnding} />
      </div>

      {ending ? <EndSession session={ending} onClose={() => setEnding(null)} /> : null}

      {ask?.kind === "addFolder" ? (
        <Suspense fallback={null}>
          <AddFolderDialog onClose={() => setAsk(null)} />
        </Suspense>
      ) : null}
      {sessionAsk ? (
        <Suspense fallback={null}>
          <SessionsHost
            key={`${sessionAsk.kind}:${sessionAsk.folder.id}:${sessionAsk.pbi?.id ?? ""}`}
            ask={sessionAsk}
            onOpened={setAwaitedId}
            onClose={clearSessionAsk}
          />
        </Suspense>
      ) : null}
    </div>
  );
}

function useBelow(width: number): boolean {
  const query = `(max-width: ${width - 1}px)`;
  const [below, setBelow] = useState(() => window.matchMedia(query).matches);

  useEffect(() => {
    const media = window.matchMedia(query);
    const read = () => setBelow(media.matches);
    read();
    media.addEventListener("change", read);
    return () => media.removeEventListener("change", read);
  }, [query]);

  return below;
}
