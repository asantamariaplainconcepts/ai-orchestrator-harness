import { useMemo, useState } from "react";
import { NavLink, useSearchParams } from "react-router";
import { explain } from "@/shared/http/client";
import type { LoopFolder, VendorPbi } from "@/shared/http/api";
import { useFolders, usePbis, useSessions } from "@/shared/http/queries";
import { useDonePbis, useLoopChecks } from "@/shared/http/settings";
import { t, type TranslationKey } from "@/shared/i18n";
import { Button, ErrorState, Loading, Select } from "@/shared/ui/kit";

/** The State filter. Done is the only way to the closed PBIs: there is no History screen. */
const stateFilters = ["open", "held", "done", "all"] as const;
type StateFilter = (typeof stateFilters)[number];

const stateLabels: Record<StateFilter, TranslationKey> = {
  open: "calls.pbis.open",
  held: "calls.pbis.inHold",
  done: "calls.pbis.done",
  all: "calls.pbis.all",
};

/** What `ListDonePbis.Limit` caps each folder's closed PBIs at. */
const doneLimit = 100;

/** Rows a group shows before it offers the rest. */
const shown = 8;

/**
 * The PBIs screen (#515, DESIGN.md#the-screens): every PBI of every folder in one list grouped
 * by its loop state, in the order the loop declares them, with filters for source, folder and
 * state and a search by text or number. There is no board: it would be this list drawn another way.
 *
 * The filters live in the address, so a PBI opened from here comes back to the same list.
 */
export function PbisScreen() {
  const [params, setParams] = useSearchParams();
  const source = params.get("source") ?? "";
  const folderPath = params.get("folder") ?? "";
  const state = asStateFilter(params.get("state"));
  const search = params.get("q") ?? "";

  const set = (key: string, value: string) =>
    setParams(
      (current) => {
        const next = new URLSearchParams(current);
        if (value === "") next.delete(key);
        else next.set(key, value);
        return next;
      },
      { replace: true },
    );

  const open = usePbis();
  const done = useDonePbis(state === "done" || state === "all");
  const folders = useFolders();
  const sessions = useSessions();
  const order = useStateOrder(folders.data ?? []);

  const live = useMemo(
    () => new Set((sessions.data ?? []).map((session) => `${session.repository}#${session.pbi}`)),
    [sessions.data],
  );

  const vendors = useMemo(
    () => [...new Set((folders.data ?? []).flatMap((folder) => (folder.vendor ? [folder.vendor] : [])))],
    [folders.data],
  );

  if (open.isPending || folders.isPending) return <Loading />;
  if (open.error) return <ErrorState detail={explain(open.error)} onRetry={() => void open.refetch()} />;
  if (folders.error)
    return <ErrorState detail={explain(folders.error)} onRetry={() => void folders.refetch()} />;

  const candidates = [
    ...(state === "done" ? [] : open.data.pbis),
    ...(state === "done" || state === "all" ? (done.data?.pbis ?? []) : []),
  ];
  const visible = candidates.filter(
    (pbi) =>
      (source === "" || pbi.vendor === source) &&
      (folderPath === "" || pbi.folderPath === folderPath) &&
      (state !== "held" || pbi.held) &&
      matches(pbi, search),
  );
  const groups = grouped(visible, order);
  const unread = [...open.data.unread, ...(done.data?.unread ?? [])];
  const reading = folders.data.length;

  return (
    <div
      style={{
        padding: "24px clamp(16px, 4vw, 40px) 40px",
        display: "flex",
        flexDirection: "column",
        gap: 20,
      }}
    >
      <div
        style={{
          display: "flex",
          alignItems: "flex-end",
          justifyContent: "space-between",
          gap: 16,
          flexWrap: "wrap",
        }}
      >
        <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
          <h1 style={{ margin: 0, fontSize: "var(--text-lg)", fontWeight: 600 }}>
            {t("screen.pbis")}
          </h1>
          <p style={{ margin: 0, fontSize: "var(--text-xs)", color: "var(--muted-foreground)" }}>
            {reading === 1
              ? t("calls.pbis.summaryOne", { open: String(open.data.pbis.length) })
              : t("calls.pbis.summary", {
                  open: String(open.data.pbis.length),
                  folders: String(reading),
                })}
          </p>
        </div>

        <div
          role="search"
          aria-label={t("calls.pbis.filters")}
          style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}
        >
          <Filter label={t("calls.pbis.source")}>
            <Select value={source} onChange={(event) => set("source", event.target.value)}>
              <option value="">{t("calls.pbis.all")}</option>
              {vendors.map((vendor) => (
                <option key={vendor} value={vendor}>
                  {vendorName(vendor)}
                </option>
              ))}
            </Select>
          </Filter>
          <Filter label={t("calls.pbis.folder")}>
            <Select value={folderPath} onChange={(event) => set("folder", event.target.value)}>
              <option value="">{t("calls.pbis.all")}</option>
              {folders.data.map((folder) => (
                <option key={folder.id} value={folder.path}>
                  {folder.name}
                </option>
              ))}
            </Select>
          </Filter>
          <Filter label={t("calls.pbis.state")}>
            <Select
              value={state}
              onChange={(event) => set("state", event.target.value === "open" ? "" : event.target.value)}
            >
              {stateFilters.map((filter) => (
                <option key={filter} value={filter}>
                  {t(stateLabels[filter])}
                </option>
              ))}
            </Select>
          </Filter>
          <input
            type="search"
            className="pc-input"
            aria-label={t("calls.pbis.search")}
            placeholder={t("calls.pbis.searchHint")}
            value={search}
            onChange={(event) => set("q", event.target.value)}
            style={{ width: 220, maxWidth: "100%" }}
          />
        </div>
      </div>

      <div style={{ display: "flex", flexDirection: "column", gap: 20, maxWidth: 1100 }}>
        {unread.map((entry) => (
          <p key={`${entry.folderPath}:${entry.why}`} style={note}>
            {t("calls.pbis.unread", { repository: entry.repository, why: entry.why })}
          </p>
        ))}

        {done.isPending && (state === "done" || state === "all") ? <Loading /> : null}
        {done.error ? <ErrorState detail={explain(done.error)} onRetry={() => void done.refetch()} /> : null}

        {groups.length === 0 && !done.isFetching ? (
          <div className="pc-empty">
            <span className="pc-empty__desc">
              {folders.data.length === 0 ? t("calls.pbis.noFolders") : t("calls.pbis.empty")}
            </span>
          </div>
        ) : null}

        {groups.map((group) => (
          <Group
            key={group.key}
            group={group}
            folders={folders.data}
            live={live}
          />
        ))}

        {state === "done" || state === "all" ? (
          <p style={note}>{t("calls.pbis.doneLimit", { limit: String(doneLimit) })}</p>
        ) : null}
      </div>
    </div>
  );
}

interface PbiGroup {
  key: string;
  title: string;
  /** A state's name, which is written in mono; the two headings of the screen's own are not. */
  state: boolean;
  hint?: TranslationKey;
  pbis: VendorPbi[];
}

/**
 * Declared states in the loop's order, then the states some PBI carries and no loop declares
 * (such as `needs-refinement`), then the PBIs with no state, then the closed.
 */
function grouped(pbis: VendorPbi[], order: string[]): PbiGroup[] {
  const byState = new Map<string, VendorPbi[]>();
  const none: VendorPbi[] = [];
  const closed: VendorPbi[] = [];

  for (const pbi of pbis) {
    if (pbi.closed) closed.push(pbi);
    else if (pbi.state === null) none.push(pbi);
    else byState.set(pbi.state, [...(byState.get(pbi.state) ?? []), pbi]);
  }

  const declared = order.filter((state) => byState.has(state));
  const undeclared = [...byState.keys()].filter((state) => !order.includes(state)).sort();

  return [
    ...declared.map((state) => ({ key: state, title: state, state: true, pbis: byState.get(state)! })),
    // Said only once some loop is known: before that, every state would look undeclared.
    ...undeclared.map((state) => ({
      key: state,
      title: state,
      state: true,
      hint: order.length > 0 ? ("calls.pbis.undeclared" as const) : undefined,
      pbis: byState.get(state)!,
    })),
    ...(none.length > 0
      ? [{ key: ":none", title: t("calls.pbis.noState"), state: false, hint: "calls.pbis.noStateHint" as const, pbis: none }]
      : []),
    ...(closed.length > 0
      ? [{ key: ":closed", title: t("calls.pbis.closed"), state: false, pbis: closed }]
      : []),
  ];
}

function Group({
  group,
  folders,
  live,
}: {
  group: PbiGroup;
  folders: LoopFolder[];
  live: Set<string>;
}) {
  const [expanded, setExpanded] = useState(false);
  const rows = expanded ? group.pbis : group.pbis.slice(0, shown);
  const hidden = group.pbis.length - rows.length;
  const heading = `pbis-group-${group.key}`;

  return (
    <section aria-labelledby={heading} style={{ display: "flex", flexDirection: "column", gap: 6 }}>
      <h2
        id={heading}
        style={{
          margin: 0,
          display: "flex",
          alignItems: "baseline",
          gap: 10,
          flexWrap: "wrap",
          fontSize: "var(--text-sm)",
          fontWeight: 600,
        }}
      >
        <span style={group.state ? { fontFamily: "var(--font-mono)" } : undefined}>{group.title}</span>
        <span style={{ fontWeight: 400, color: "var(--muted-foreground)" }}>{group.pbis.length}</span>
        {group.hint ? (
          <span style={{ fontWeight: 400, fontSize: "var(--text-xs)", color: "var(--muted-foreground)" }}>
            {t(group.hint)}
          </span>
        ) : null}
      </h2>
      <div
        style={{
          display: "flex",
          flexDirection: "column",
          border: "1px solid var(--border)",
          borderRadius: "var(--radius-xl)",
          overflow: "hidden",
        }}
      >
        {rows.map((pbi, index) => (
          <Row
            key={`${pbi.repository}#${pbi.id}`}
            pbi={pbi}
            folder={folders.find((folder) => folder.path === pbi.folderPath)}
            live={live.has(`${pbi.repository}#${pbi.id}`)}
            last={index === rows.length - 1}
          />
        ))}
      </div>
      {hidden > 0 || expanded ? (
        <Button
          variant="ghost"
          size="xs"
          onClick={() => setExpanded((shownAll) => !shownAll)}
          style={{ alignSelf: "flex-start", color: "var(--primary)" }}
        >
          {expanded ? t("calls.pbis.fewer") : t("calls.pbis.more", { count: String(hidden) })}
        </Button>
      ) : null}
    </section>
  );
}

function Row({
  pbi,
  folder,
  live,
  last,
}: {
  pbi: VendorPbi;
  folder: LoopFolder | undefined;
  live: boolean;
  last: boolean;
}) {
  return (
    <NavLink
      to={`/pbis/${folder?.id ?? ""}/${encodeURIComponent(pbi.id)}/journey`}
      className="aio-list-row"
      title={pbi.title}
      style={{
        display: "flex",
        alignItems: "center",
        flexWrap: "wrap",
        columnGap: 16,
        rowGap: 2,
        padding: "10px 14px",
        borderBottom: last ? 0 : "1px solid var(--border)",
        color: "inherit",
        textDecoration: "none",
      }}
    >
      <span style={{ flex: "none", width: 56, fontFamily: "var(--font-mono)", color: "var(--muted-foreground)" }}>
        #{pbi.id}
      </span>
      <span
        style={{
          flex: "1 1 240px",
          minWidth: 0,
          overflow: "hidden",
          textOverflow: "ellipsis",
          whiteSpace: "nowrap",
        }}
      >
        {pbi.title}
      </span>
      <span
        style={{
          display: "flex",
          alignItems: "center",
          gap: 16,
          marginLeft: "auto",
          fontSize: "var(--text-xs)",
          color: "var(--muted-foreground)",
        }}
      >
        <span>{folder?.name ?? pbi.repository}</span>
        <span>{vendorName(pbi.vendor)}</span>
        <span style={{ width: 16, display: "flex", justifyContent: "flex-end" }}>
          {pbi.held ? (
            <span role="img" aria-label={t("rail.held")} title={t("rail.held")} style={{ display: "flex", gap: 2 }}>
              <span style={pauseBar} />
              <span style={pauseBar} />
            </span>
          ) : live ? (
            <span
              role="img"
              aria-label={t("rail.live")}
              title={t("rail.live")}
              style={{ width: 8, height: 8, borderRadius: "var(--radius-full)", background: "var(--primary)" }}
            />
          ) : null}
        </span>
      </span>
    </NavLink>
  );
}

function Filter({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label
      style={{
        display: "flex",
        alignItems: "center",
        gap: 6,
        fontSize: "var(--text-xs)",
        color: "var(--muted-foreground)",
      }}
    >
      {label}
      {children}
    </label>
  );
}

/**
 * The states in the order the folders' loops declare them, the first folder's first. A loop that
 * does not validate adds nothing: its states are not states the engine walks.
 */
function useStateOrder(folders: LoopFolder[]): string[] {
  const yamls = [...new Set(folders.flatMap((folder) => (folder.loopYaml ? [folder.loopYaml] : [])))];
  const checks = useLoopChecks(yamls);
  return [...new Set(checks.flatMap((check) => (check.data?.valid ? check.data.states.map((state) => state.name) : [])))];
}

/** A number matches the id, with or without its `#`; anything else matches the title. */
function matches(pbi: VendorPbi, search: string): boolean {
  const wanted = search.trim().toLowerCase();
  if (wanted === "") return true;
  const number = wanted.replace(/^#/, "");
  return /^\d+$/.test(number) ? pbi.id.startsWith(number) : pbi.title.toLowerCase().includes(wanted);
}

function asStateFilter(value: string | null): StateFilter {
  return stateFilters.find((filter) => filter === value) ?? "open";
}

function vendorName(vendor: string): string {
  return vendor === "github" ? t("vendor.github") : vendor === "azure-devops" ? t("vendor.azure-devops") : vendor;
}

const note: React.CSSProperties = {
  margin: 0,
  fontSize: "var(--text-xs)",
  color: "var(--muted-foreground)",
};

const pauseBar: React.CSSProperties = {
  width: 3,
  height: 10,
  borderRadius: 1,
  background: "var(--pc-warning-700)",
};
