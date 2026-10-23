import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { explain } from "@/shared/http/client";
import { consoleProcess } from "@/shared/http/api";
import { useSessions } from "@/shared/http/queries";
import { t, tCount } from "@/shared/i18n";
import { Button, Dialog } from "@/shared/ui/kit";

/**
 * Stopping the console (#548). Closing the browser leaves the Server running, and the installed app
 * has no terminal to stop it from, so this is where it stops.
 *
 * **The dialog says what ends with it before it asks.** The live sessions are the tray's own list,
 * counted; a loop run among them that has not written its end record is recorded as failed the next
 * time the console starts, and the loop holds its PBI. Nothing restarts the console afterwards.
 */
export function StopSection() {
  const sessions = useSessions();
  const stop = useMutation({ mutationFn: consoleProcess.stop });
  const [asking, setAsking] = useState(false);

  const live = sessions.data?.length ?? 0;
  const stopped = stop.data?.stopping === true;

  return (
    <section style={{ display: "flex", flexDirection: "column", gap: 8 }}>
      <div style={{ fontSize: "var(--text-sm)", fontWeight: 500 }}>{t("settings.stop.title")}</div>
      <div role="status" style={note}>
        {stopped ? t("settings.stop.stopped") : t("settings.stop.hint")}
      </div>
      {stopped ? null : (
        <div>
          <Button variant="outline" size="sm" onClick={() => setAsking(true)}>
            {t("settings.stop.button")}
          </Button>
        </div>
      )}

      {asking && !stopped ? (
        <Dialog
          title={t("settings.stop.button")}
          onClose={() => setAsking(false)}
          width={440}
          footer={
            <>
              <Button variant="outline" size="sm" onClick={() => setAsking(false)} style={{ marginLeft: "auto" }}>
                {t("settings.cancel")}
              </Button>
              <Button
                variant="destructive"
                size="sm"
                autoFocus
                disabled={stop.isPending}
                onClick={() => stop.mutate(undefined, { onSuccess: () => setAsking(false) })}
              >
                {stop.isPending ? t("settings.stop.stopping") : t("settings.stop.confirm")}
              </Button>
            </>
          }
        >
          <p style={{ margin: 0, color: "var(--muted-foreground)" }}>
            {live === 0
              ? t("settings.stop.none")
              : t("settings.stop.live", {
                  sessions: tCount(live, t("settings.stop.session"), t("settings.stop.sessions")),
                })}
          </p>
          <p style={{ margin: "8px 0 0", color: "var(--muted-foreground)" }}>{t("settings.stop.after")}</p>
          {stop.error ? (
            <p role="alert" style={{ margin: "8px 0 0", fontSize: "var(--text-xs)", color: "var(--destructive)" }}>
              {explain(stop.error)}
            </p>
          ) : null}
        </Dialog>
      ) : null}
    </section>
  );
}

const note: React.CSSProperties = {
  margin: 0,
  fontSize: "var(--text-xs)",
  color: "var(--muted-foreground)",
};
