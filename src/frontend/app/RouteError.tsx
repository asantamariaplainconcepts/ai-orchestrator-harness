import { useRouteError } from "react-router";
import { t } from "@/shared/i18n";
import { Button } from "@/shared/ui/kit";

/**
 * The route's error boundary, where the router's default "Hey developer" used to land.
 *
 * The one failure this names: a window still running the pre-update bundle asked for a chunk the
 * install renamed, and the import died. The sidebar's refresh cannot heal that — it re-asks the
 * server for the data the window already holds, and a broken module stays broken in memory. A
 * real reload is the only remedy, and it costs nothing the runs did not already survive: they are
 * server-side, and the update that broke the chunks did not touch them.
 *
 * Any other error the boundary sees says the same one sentence, with the same one way out — a
 * reload re-fetches the screen's code and its data alike, which covers the rest.
 */
export function RouteErrorElement() {
  const error = useRouteError() as Error | undefined;
  const stale =
    /failed to fetch dynamically imported module|importing a module script failed/i.test(
      error?.message ?? "",
    );

  return (
    <div
      style={{
        height: "100%",
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        gap: 8,
        padding: 24,
        textAlign: "center",
      }}
    >
      <span className="pc-empty__title">
        {stale ? t("routeError.staleTitle") : t("state.error")}
      </span>
      <span className="pc-empty__desc">
        {stale ? t("routeError.staleDesc") : t("routeError.desc")}
      </span>
      <Button onClick={() => window.location.reload()}>
        {t("routeError.reload")}
      </Button>
    </div>
  );
}
