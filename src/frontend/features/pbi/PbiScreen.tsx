import { GitPullRequest, Pause, RefreshCw, Sparkles, SquareTerminal } from "lucide-react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Suspense, useEffect } from "react";
import { Navigate, NavLink, useParams } from "react-router";
import { type SessionAsk, askSession } from "@/features/sessions/ask";
import { explain } from "@/shared/http/client";
import type { LoopFolder, PbiDetail, RunRow } from "@/shared/http/api";
import {
  useFolders,
  useIssueChildren,
  useLoopDefinition,
  usePbi,
  usePbiFollowsJournal,
  usePbiJournal,
  usePbiRuns,
  prefetchPbiTab,
  refreshPbi,
  revalidatePbi,
  usePbiTabPrefetch,
} from "@/shared/http/queries";
import { t } from "@/shared/i18n";
import { Badge, Button, ErrorState, Loading } from "@/shared/ui/kit";
import { useNarrowContainer } from "@/shared/ui/useNarrowContainer";
import { HeaderAction } from "./HeaderAction";
import { holdOf, liveLoopRun, loopState } from "./journey";
import { pbiTabs } from "./tabs";

/**
 * The PBI (DESIGN.md#the-pbi, #511): where it is and what is missing. A header with
 * its state, the hold with its reason and the pull request; one button whose words depend on the
 * state, with Terminal and Explore with agent beside it; and the five tabs below, each a lazy file
 * of its own under `tabs/`.
 *
 * `/pbis/:folderId/:pbiId/:tab`. The folder names the repository, and the PBI is read from the
 * vendor through it.
 */
export function PbiScreen() {
  const { folderId = "", pbiId = "", tab = "" } = useParams();
  const folders = useFolders();
  const folder = folders.data?.find((entry) => entry.id === folderId);
  const repository = folder?.repository ?? undefined;
  const detail = usePbi(repository, pbiId);
  const [measure, narrow] = useNarrowContainer<HTMLDivElement>(720);

  // #627. Entering the PBI, from another screen or another PBI, shows what is kept of its vendor
  // reads at once and reads them again behind it. A change of tab is not an entry.
  const client = useQueryClient();
  useEffect(() => {
    if (repository) revalidatePbi(client, repository, pbiId);
  }, [client, repository, pbiId]);

  // #617. Everything the screen and its open tab read starts with the repository, beside the PBI:
  // the header's runs, journal and loop, the tab's reads at the vendor, and the tab's own chunk.
  usePbiRuns(repository, pbiId);
  // #627. The journal, which is this host's and polled, is also what tells the header the loop moved.
  usePbiFollowsJournal(repository, pbiId);
  useLoopDefinition(repository);
  usePbiTabPrefetch(repository, pbiId, tab);
  const current = pbiTabs.find((entry) => entry.path === tab);
  useEffect(() => {
    void current?.load();
  }, [current]);
  if (!current) return <Navigate to="../journey" relative="path" replace />;

  let body;
  if (folders.error) body = <ErrorState detail={explain(folders.error)} onRetry={() => void folders.refetch()} />;
  else if (!folders.data) body = <Loading />;
  else if (!folder) body = <ErrorState detail={t("calls.pbi.missingFolder")} />;
  else if (!folder.repository)
    body = <ErrorState detail={t("calls.pbi.noRepository", { folder: folder.name })} />;
  else if (detail.error)
    body = (
      <ErrorState
        detail={t("calls.pbi.failed", { id: pbiId, reason: explain(detail.error) })}
        onRetry={() => void detail.refetch()}
      />
    );
  else if (!detail.data) body = <Loading />;
  else
    body = (
      <Screen
        folder={folder}
        repository={folder.repository}
        id={pbiId}
        detail={detail.data}
        tab={current}
        narrow={narrow}
      />
    );

  return (
    <div
      ref={measure}
      style={{
        padding: narrow ? "16px" : "24px 40px 40px",
        display: "flex",
        flexDirection: "column",
        gap: 20,
        minWidth: 0,
      }}
    >
      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: 8,
          fontSize: "var(--text-xs)",
          color: "var(--muted-foreground)",
        }}
      >
        <span>{folder?.name ?? t("calls.pbi.unknownFolder")}</span>
        <span aria-hidden>/</span>
        <span style={{ fontFamily: "var(--font-mono)", color: "var(--foreground)" }}>#{pbiId}</span>
      </div>
      {body}
    </div>
  );
}

function Screen({
  folder,
  repository,
  id,
  detail,
  tab,
  narrow,
}: {
  folder: LoopFolder;
  repository: string;
  id: string;
  detail: PbiDetail;
  tab: (typeof pbiTabs)[number];
  narrow: boolean;
}) {
  const runs = usePbiRuns(repository, id);
  const journal = usePbiJournal(repository, id);
  const loop = useLoopDefinition(repository);
  // #698. A PBI with sub-issues is an epic: the loop does not move it, so the header counts them.
  const children = useIssueChildren(repository, id);
  useWarmTabs(repository, id);
  const client = useQueryClient();
  const refresh = useMutation({ mutationFn: () => refreshPbi(client, repository, id) });

  const { pbi, pullRequest } = detail;
  const refreshWords = {
    id,
    vendor:
      pbi.vendor === "github"
        ? t("vendor.github")
        : pbi.vendor === "azure-devops"
          ? t("vendor.azure-devops")
          : pbi.vendor,
  };
  const hold = pbi.held ? holdOf(journal.data ?? [], pbi.state) : null;
  const live: RunRow | null = liveLoopRun(runs.data ?? []);
  const Tab = tab.Component;

  const declared = loopState(loop.data, pbi.state);
  // Terminal and Explore with agent (#516) open on this PBI's branch, as a manual run of its state.
  const command = declared?.command ?? null;
  const session = (kind: SessionAsk["kind"]) =>
    askSession({
      kind,
      folder,
      pbi: { repository, id, suggestion: command ? `${command} ${id}` : null },
    });

  return (
    <>
      <div
        style={{
          display: "flex",
          alignItems: "flex-start",
          justifyContent: "space-between",
          gap: 16,
          flexWrap: "wrap",
        }}
      >
        <div style={{ display: "flex", flexDirection: "column", gap: 10, minWidth: 0 }}>
          <h1 style={{ margin: 0, fontSize: "var(--text-h1-size)", fontWeight: 600, lineHeight: 1.3 }}>
            {pbi.title}
          </h1>
          <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
            <Badge variant="outline" style={{ fontFamily: "var(--font-mono)" }}>
              {epic(pbi.closed, children.data) ?? pbi.state ?? t("calls.pbi.noState")}
            </Badge>
            {pbi.held ? (
              <Badge variant="warning" style={{ whiteSpace: "normal", textAlign: "left", maxWidth: "100%" }}>
                <Pause size={11} aria-hidden />
                {hold
                  ? t("calls.pbi.hold", { reason: hold.why })
                  : t("calls.pbi.holdByPerson")}
              </Badge>
            ) : null}
            {pullRequest ? (
              <a
                href={pullRequest.url}
                target="_blank"
                rel="noreferrer"
                className="pc-badge pc-badge--outline"
                style={{ textDecoration: "none", display: "inline-flex", gap: 6 }}
              >
                <GitPullRequest size={11} aria-hidden />
                <span style={{ fontFamily: "var(--font-mono)" }}>
                  {t("calls.pbi.pr", { id: pullRequest.id })}
                </span>
                <span style={{ color: "var(--muted-foreground)" }}>
                  {pullRequest.isMerged
                    ? t("calls.pbi.pr.merged")
                    : pullRequest.isApproved
                      ? t("calls.pbi.pr.approved")
                      : pullRequest.isDraft
                        ? t("calls.pbi.pr.draft")
                        : t("calls.pbi.pr.ready")}
                </span>
              </a>
            ) : null}
          </div>
        </div>

        <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
          <Button
            variant="ghost"
            size={narrow ? "icon-sm" : "icon"}
            aria-label={t(refresh.isPending ? "calls.pbi.refreshing" : "calls.pbi.refresh", refreshWords)}
            title={t("calls.pbi.refresh", refreshWords)}
            aria-busy={refresh.isPending}
            onClick={() => {
              if (!refresh.isPending) refresh.mutate();
            }}
          >
            <RefreshCw size={15} aria-hidden className={refresh.isPending ? "pc-spinner" : undefined} />
          </Button>
          <Button
            variant="outline"
            size={narrow ? "sm" : "md"}
            aria-label={t("calls.pbi.terminalLabel")}
            title={t("calls.pbi.terminalLabel")}
            onClick={() => session("terminal")}
          >
            <SquareTerminal size={15} aria-hidden />
            {t("calls.pbi.terminal")}
          </Button>
          <Button variant="outline" size={narrow ? "sm" : "md"} onClick={() => session("explore")}>
            <Sparkles size={15} aria-hidden />
            {t("calls.pbi.explore")}
          </Button>
          <HeaderAction
            repository={repository}
            id={id}
            pbi={pbi}
            epic={Array.isArray(children.data) && children.data.length > 0}
            hold={hold}
            live={live}
            loop={loop.data}
            ready={Boolean(runs.data && journal.data && loop.data) && !children.isPending}
            size={narrow ? "sm" : "md"}
          />
        </div>
      </div>

      <nav
        aria-label={t("calls.pbi.tabs")}
        className="pc-tabs__list"
        style={{ maxWidth: "100%", overflowX: "auto", justifyContent: "flex-start" }}
      >
        {pbiTabs.map((entry) => {
          const now = declared?.tab === entry.path;
          return (
            <NavLink
              key={entry.path}
              to={`/pbis/${folder.id}/${id}/${entry.path}`}
              className="pc-tabs__trigger"
              data-state={entry === tab ? "active" : "inactive"}
              style={{ textDecoration: "none", flex: "none", padding: "0 12px" }}
            >
              {t(entry.label)}
              {now ? (
                <Badge variant={pbi.held ? "warning" : "info"}>
                  {pbi.held ? t("calls.pbi.tabNeedsYou") : t("calls.pbi.tabNow")}
                </Badge>
              ) : null}
            </NavLink>
          );
        })}
      </nav>

      <Suspense fallback={<Loading />}>
        <Tab folder={folder} repository={repository} id={id} detail={detail} />
      </Suspense>

    </>
  );
}

/** #698. The header's words for an epic, or null for a PBI without sub-issues. */
function epic(closed: boolean, children: readonly { closed: boolean }[] | undefined): string | null {
  if (!Array.isArray(children) || children.length === 0) return null;
  if (closed) return t("calls.pbi.epicClosed");
  const open = children.filter((child) => !child.closed).length;
  return t("calls.pbi.epicOpen", { open: String(open), total: String(children.length) });
}

/**
 * #627. Once the screen is up, and when the browser next has nothing to do, asks for every warm tab's
 * chunk and vendor read, so a click on it paints from the cache. The open tab's own are already in
 * flight or done, and asking again is a no-op while the cache holds them.
 */
function useWarmTabs(repository: string, id: string) {
  const client = useQueryClient();
  useEffect(
    () =>
      whenIdle(() => {
        for (const entry of pbiTabs.filter((each) => each.warm)) {
          void entry.load().catch(() => undefined);
          prefetchPbiTab(client, repository, id, entry.path);
        }
      }),
    [client, repository, id],
  );
}

/** Runs `work` when the browser is idle, or after a second at most; the returned function cancels it. */
function whenIdle(work: () => void): () => void {
  if (typeof window.requestIdleCallback === "function") {
    const handle = window.requestIdleCallback(work, { timeout: 1_000 });
    return () => window.cancelIdleCallback(handle);
  }
  const handle = window.setTimeout(work, 200);
  return () => window.clearTimeout(handle);
}
