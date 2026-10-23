import { useQueryClient } from "@tanstack/react-query";
import { Pause } from "lucide-react";
import { useEffect, useId, useMemo, useState, type CSSProperties, type ReactNode } from "react";
import { Link } from "react-router";
import { explain } from "@/shared/http/client";
import { scriptOutputUrl, type EnginePresent, type JournalLine, type LoopFolder } from "@/shared/http/api";
import {
  useFolders,
  useLoopDefinitions,
  useLoopJournal,
  useLoopPresent,
  usePbis,
  useSessions,
  useSwitchEngine,
  queryKeys,
} from "@/shared/http/queries";
import { t, type TranslationKey } from "@/shared/i18n";
import { ago } from "@/shared/ui/ago";
import { ErrorState, Loading, Select, Switch } from "@/shared/ui/kit";
import { useNarrowContainer } from "@/shared/ui/useNarrowContainer";
import { bands, loopRows, pbiKey, type Band, type Detail, type LoopRow } from "./loopBands";

/** How many journal lines the screen reads: enough for every hold's reason, and the list's twenty. */
const journalDepth = 200;
const decisionsShown = 20;

/**
 * The Loop screen (#510, DESIGN.md#the-screens): what waits for a person, what is moving, and
 * what the engine is doing. The strip on top is the engine now, the three bands are one row per
 * open PBI, and the list at the bottom is the engine's journal with each decision's reason.
 *
 * Its one write is each folder's engine switch (#564): releasing a hold is the PBI's, and a row
 * only opens its Journey. Everything is polled, so a pass of the engine shows here within seconds without a socket.
 */
export function LoopScreen() {
  const pbis = usePbis();
  const folders = useFolders();
  const present = useLoopPresent();
  const journal = useLoopJournal(journalDepth);
  const sessions = useSessions();
  const [folderPath, setFolderPath] = useState("");
  const [host, narrow] = useNarrowContainer<HTMLDivElement>(820);
  useTick(5_000);

  // A decision moves labels at the vendor, and the PBI list is polled slowly; a new decision is
  // the moment to read it again, so a row changes band with the line that explains why.
  const queryClient = useQueryClient();
  const newest = present.data?.lastDecision?.at;
  useEffect(() => {
    if (newest) void queryClient.invalidateQueries({ queryKey: queryKeys.pbis });
  }, [newest, queryClient]);

  const known = useMemo(() => folders.data ?? [], [folders.data]);
  // A folder with no loop file or no repository declares no state, and is not asked.
  const looped = known.filter((folder) => folder.loopYaml !== null && folder.repository !== null);
  const definitions = useLoopDefinitions(looped.map((folder) => folder.repository ?? ""));
  const loops = new Map(looped.map((folder, index) => [folder.path, definitions[index]?.data]));
  const rows = loopRows(
    pbis.data?.pbis ?? [],
    known,
    present.data,
    sessions.data ?? [],
    journal.data ?? [],
    loops,
  );

  if (pbis.isError) {
    return <ErrorState detail={explain(pbis.error)} onRetry={() => void pbis.refetch()} />;
  }

  const chosen = known.find((folder) => folder.path === folderPath);
  const shown = chosen ? rows.filter((row) => row.pbi.folderPath === chosen.path) : rows;
  const decisions = (journal.data ?? [])
    .filter((line) => !chosen || line.repository === chosen.repository)
    .slice(0, decisionsShown);

  return (
    <div
      ref={host}
      style={{
        padding: narrow ? "16px" : "24px 40px 40px",
        display: "flex",
        flexDirection: "column",
        gap: 24,
      }}
    >
      <header
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
            {t("loop.title")}
          </h1>
          {pbis.data ? (
            <p style={{ margin: 0, color: "var(--muted-foreground)" }}>
              {shown.length === 1
                ? t("loop.leadOne")
                : t("loop.leadMany", { count: String(shown.length) })}
            </p>
          ) : null}
          {/* #734. Each looped folder's Metrics, or the chosen folder's alone. */}
          {looped.length > 0 ? (
            <nav
              aria-label={t("loop.metrics")}
              style={{ display: "flex", gap: "4px 12px", flexWrap: "wrap", fontSize: "var(--text-xs)" }}
            >
              {(chosen ? looped.filter((folder) => folder.id === chosen.id) : looped).map((folder) => (
                <Link key={folder.id} to={`/folders/${folder.id}/metrics`}>
                  {t("loop.metricsOf", { folder: folder.name })}
                </Link>
              ))}
            </nav>
          ) : null}
        </div>
        {known.length > 1 ? (
          <label
            style={{
              display: "flex",
              alignItems: "center",
              gap: 8,
              color: "var(--muted-foreground)",
            }}
          >
            {t("loop.folder")}
            <Select
              value={folderPath}
              onChange={(event) => setFolderPath(event.target.value)}
              style={{ height: 32 }}
            >
              <option value="">{t("loop.allFolders")}</option>
              {known.map((folder) => (
                <option key={folder.id} value={folder.path}>
                  {folder.name}
                </option>
              ))}
            </Select>
          </label>
        ) : null}
      </header>

      <Strip present={present.data} folders={known} narrow={narrow} />

      {present.error ? (
        <Alert problem>
          {t("loop.partial", {
            what: t("loop.partial.present"),
            reason: explain(present.error),
          })}
        </Alert>
      ) : null}
      {journal.error ? (
        <Alert problem>
          {t("loop.partial", {
            what: t("loop.partial.journal"),
            reason: explain(journal.error),
          })}
        </Alert>
      ) : null}

      {!pbis.data ? (
        <Loading />
      ) : rows.length === 0 ? (
        <Quiet>{t("loop.empty")}</Quiet>
      ) : shown.length === 0 ? (
        <Quiet>{t("loop.emptyFiltered")}</Quiet>
      ) : (
        bands.map((band) => (
          <BandSection
            key={band}
            band={band}
            rows={shown.filter((row) => row.band === band)}
            narrow={narrow}
          />
        ))
      )}

      <Decisions
        lines={decisions}
        folders={known}
        loaded={journal.data !== undefined}
        narrow={narrow}
      />
    </div>
  );
}

/** Re-renders every `ms`, so "12 s ago" keeps counting between polls that changed nothing. */
function useTick(ms: number) {
  const [, tick] = useState(0);
  useEffect(() => {
    const timer = setInterval(() => tick((n) => n + 1), ms);
    return () => clearInterval(timer);
  }, [ms]);
}

const clock = new Intl.DateTimeFormat(undefined, {
  hour: "2-digit",
  minute: "2-digit",
  hourCycle: "h23",
});

function time(iso: string): string {
  return clock.format(new Date(iso));
}

/** A journal line's transition, as the list and the strip print it. */
function transition(line: JournalLine): string {
  const values = { from: line.from, to: line.to ?? "" };
  switch (line.kind) {
    case "moved":
      return t("loop.kind.moved", values);
    case "held":
      return t("loop.kind.held", values);
    case "released":
      return t("loop.kind.released", values);
    case "started":
      return t("loop.kind.started", values);
    case "ran":
      return t("loop.kind.ran", values);
    case "switched":
      return t("loop.kind.switched", values);
    default:
      return t("loop.kind.waited", values);
  }
}

function folderName(folders: readonly LoopFolder[], path: string, repository: string): string {
  return folders.find((folder) => folder.path === path)?.name ?? repository;
}

const label: CSSProperties = {
  fontSize: "var(--text-xs)",
  color: "var(--muted-foreground)",
  textTransform: "uppercase",
  letterSpacing: "0.04em",
};

/**
 * The engine now: a switch per folder, whether it looks and when it last did, its runs against the
 * WIP, and its last decision. The switches are the strip's one write (#564).
 */
function Strip({
  present,
  folders,
  narrow,
}: {
  present: EnginePresent | undefined;
  folders: readonly LoopFolder[];
  narrow: boolean;
}) {
  const switching = useSwitchEngine();

  if (!present) {
    return (
      <section aria-label={t("loop.strip")} className="pc-card" style={{ padding: "12px 16px" }}>
        <Loading />
      </section>
    );
  }

  const seconds = String(present.intervalSeconds);
  const pass = present.lastPass;
  const looking = !present.enabled
    ? t("loop.engine.off")
    : pass
      ? t("loop.engine.on", { seconds, ago: ago(pass.at) })
      : t("loop.engine.noPass", { seconds });

  const on = present.folders.filter((folder) => folder.on).length;
  const engine =
    present.folders.length === 0
      ? t("loop.engine.noFolders")
      : present.folders.length === 1
        ? t("loop.engine.countOne", { on: String(on) })
        : t("loop.engine.count", { on: String(on), count: String(present.folders.length) });

  const runs = present.liveRuns.map((run) => `${run.state} #${run.pbi}`).join(", ");
  const running =
    present.liveRuns.length === 0
      ? t("loop.running.none")
      : present.liveRuns.length === 1
        ? t("loop.running.one", { runs })
        : t("loop.running.many", { count: String(present.liveRuns.length), runs });

  const walked = present.folders.filter((folder) => folder.problem === null);
  const wip =
    walked.length === 0
      ? null
      : t("loop.wip", {
          value: walked
            .map((folder) => {
              const live = String(
                present.liveRuns.filter((run) => run.repository === folder.repository).length,
              );
              const value =
                folder.wip === null
                  ? t("loop.wip.free", { live })
                  : t("loop.wip.of", { live, wip: String(folder.wip) });
              return walked.length === 1
                ? value
                : t("loop.wip.folder", {
                    folder: folderName(folders, folder.path, folder.repository),
                    value,
                  });
            })
            .join(" · "),
        });

  const last = present.lastDecision;
  const decision = !last
    ? t("loop.lastDecision.none")
    : last.kind === "switched"
      ? t("loop.lastSwitch", {
          time: time(last.at),
          folder: folders.find((entry) => entry.repository === last.repository)?.name ?? last.repository,
          transition: transition(last),
        })
      : t("loop.lastLine", { time: time(last.at), pbi: last.pbi, transition: transition(last) });

  const failed = switching.error ? switching.variables : undefined;

  return (
    <section
      aria-label={t("loop.strip")}
      style={{ display: "flex", flexDirection: "column", gap: 8 }}
    >
      <div
        style={{
          display: "grid",
          gridTemplateColumns: narrow ? "minmax(0, 1fr)" : "repeat(4, minmax(0, 1fr))",
          gap: 12,
          padding: "12px 16px",
          border: "1px solid var(--border)",
          borderRadius: "var(--radius-xl)",
          background: "var(--muted)",
        }}
      >
        <div
          role="group"
          aria-label={engine}
          style={{ display: "flex", flexDirection: "column", gap: 6, minWidth: 0 }}
        >
          <span style={label}>{engine}</span>
          {present.folders.length === 0 ? (
            <span style={{ color: "var(--muted-foreground)" }}>{t("loop.engine.noFoldersHow")}</span>
          ) : (
            present.folders.map((folder) => (
              <FolderSwitch
                key={folder.path}
                name={folderName(folders, folder.path, folder.repository)}
                on={folder.on}
                problem={folder.problem}
                busy={switching.isPending && switching.variables?.repository === folder.repository}
                onChange={(next) => switching.mutate({ repository: folder.repository, on: next })}
              />
            ))
          )}
        </div>
        <StripCell
          name={t("loop.looking")}
          sub={<Link to="/settings">{t("loop.engine.settings")}</Link>}
        >
          {looking}
        </StripCell>
        <StripCell name={t("loop.running")} sub={wip}>
          {running}
        </StripCell>
        <StripCell name={t("loop.lastDecision")}>{decision}</StripCell>
      </div>

      <p style={{ margin: 0, fontSize: "var(--text-xs)", color: "var(--muted-foreground)" }}>
        {t("loop.engine.note")}
      </p>
      {failed ? (
        <Alert problem>
          {t("loop.engine.switchFailed", {
            to: failed.on ? "on" : "off",
            folder: folderName(
              folders,
              present.folders.find((folder) => folder.repository === failed.repository)?.path ?? "",
              failed.repository,
            ),
            reason: explain(switching.error),
          })}
        </Alert>
      ) : null}
      {pass?.failure ? (
        <Alert problem>
          {t("loop.engine.failed", { ago: ago(pass.at), reason: pass.failure })}
        </Alert>
      ) : null}
      {present.folders
        .filter((folder) => folder.on && folder.problem !== null)
        .map((folder) => (
          <Alert key={folder.path} problem>
            {t("loop.folderProblem", {
              folder: folderName(folders, folder.path, folder.repository),
              problem: folder.problem ?? "",
            })}
          </Alert>
        ))}
    </section>
  );
}

/**
 * One folder's engine switch. A folder that is off and whose loop cannot be read cannot be turned
 * on: the server refuses it, so the switch says why beside it instead of offering what would fail.
 * One that is on keeps its switch live, so it can still be turned off.
 */
function FolderSwitch({
  name,
  on,
  problem,
  busy,
  onChange,
}: {
  name: string;
  on: boolean;
  problem: string | null;
  busy: boolean;
  onChange: (on: boolean) => void;
}) {
  const reason = useId();
  const blocked = !on && problem !== null;
  return (
    <div style={{ display: "flex", alignItems: "flex-start", gap: 8, minWidth: 0 }}>
      <Switch
        on={on}
        label={t("loop.engine.switch", { folder: name })}
        blocked={blocked}
        busy={busy}
        describedBy={blocked ? reason : undefined}
        onChange={onChange}
      />
      <span style={{ display: "flex", flexDirection: "column", minWidth: 0 }}>
        <span
          style={{ ...ellipsis, color: blocked ? "var(--muted-foreground)" : "var(--foreground)" }}
          title={name}
        >
          {name}
        </span>
        {blocked ? (
          <span
            id={reason}
            style={{
              fontSize: "var(--text-xs)",
              color: "var(--muted-foreground)",
              overflowWrap: "anywhere",
            }}
          >
            {problem}
          </span>
        ) : null}
      </span>
    </div>
  );
}

/**
 * A sentence under the strip, in the kit's alert. A problem is something to fix and is announced;
 * a folder that is off is a state, which its switch already shows.
 */
function Alert({ problem = false, children }: { problem?: boolean; children: ReactNode }) {
  return (
    <div
      className={problem ? "pc-alert pc-alert--destructive" : "pc-alert"}
      role={problem ? "alert" : undefined}
    >
      <span className={problem ? "pc-alert__title" : "pc-alert__desc"}>{children}</span>
    </div>
  );
}

function StripCell({
  name,
  sub,
  children,
}: {
  name: string;
  sub?: ReactNode;
  children: ReactNode;
}) {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 2, minWidth: 0 }}>
      <span style={label}>{name}</span>
      <span style={{ display: "flex", alignItems: "center", gap: 8 }}>{children}</span>
      {sub ? (
        <span style={{ fontSize: "var(--text-xs)", color: "var(--muted-foreground)" }}>{sub}</span>
      ) : null}
    </div>
  );
}

const bandHeading: Record<Band, TranslationKey> = {
  needsYou: "loop.band.needsYou",
  inMotion: "loop.band.inMotion",
  rest: "loop.band.rest",
};

const bandEmpty: Record<Band, TranslationKey> = {
  needsYou: "loop.empty.needsYou",
  inMotion: "loop.empty.inMotion",
  rest: "loop.empty.rest",
};

const bandColour: Record<Band, string> = {
  needsYou: "var(--pc-warning-700)",
  inMotion: "var(--pc-info-700)",
  rest: "var(--muted-foreground)",
};

function BandSection({ band, rows, narrow }: { band: Band; rows: LoopRow[]; narrow: boolean }) {
  return (
    <section
      aria-label={t(bandHeading[band], { count: String(rows.length) })}
      style={{ display: "flex", flexDirection: "column", gap: 8 }}
    >
      <h2 style={{ ...label, margin: 0, fontWeight: 600, color: bandColour[band] }}>
        {t(bandHeading[band], { count: String(rows.length) })}
      </h2>
      {rows.length === 0 ? (
        <Quiet>{t(bandEmpty[band])}</Quiet>
      ) : (
        rows.map((row) => (
          <Row key={pbiKey(row.pbi.repository, row.pbi.id)} row={row} narrow={narrow} />
        ))
      )}
    </section>
  );
}

/** What a row says after its state, and whether that is a warning. */
function describe(detail: Detail): { text: string; warn: boolean } {
  switch (detail.kind) {
    case "held":
      return {
        text:
          detail.why && detail.since
            ? t("loop.held", { why: detail.why, ago: ago(detail.since) })
            : t("loop.heldByPerson"),
        warn: true,
      };
    case "running": {
      const { session, run } = detail;
      const stale = (session?.stale ?? false) || (run?.stale ?? false);
      if (session) {
        const values = {
          agent: session.provider ?? t("loop.terminal"),
          command: session.command,
          ago: ago(session.startedAt),
          silent: ago(session.lastOutputAt),
        };
        return { text: t(stale ? "loop.runStale" : "loop.run", values), warn: stale };
      }
      const state = run?.state ?? "";
      if (!run?.lastOutputAt) return { text: t("loop.runEngineQuiet", { state }), warn: stale };
      return {
        text: t(stale ? "loop.runEngineStale" : "loop.runEngine", {
          state,
          silent: ago(run.lastOutputAt),
        }),
        warn: stale,
      };
    }
    case "script": {
      const { script } = detail;
      const values = { script: script.script, for: ago(script.since) };
      return { text: t(script.stale ? "loop.scriptStale" : "loop.script", values), warn: script.stale };
    }
    case "next": {
      const next = !detail.step
        ? t("loop.next.person")
        : detail.step.kind === "command"
          ? t("loop.next.command", { command: detail.step.command })
          : t("loop.next.scripts", { scripts: detail.step.scripts.join(", ") });
      return {
        text: detail.waited ? t("loop.waited", { next, why: detail.waited }) : next,
        warn: false,
      };
    }
    default:
      return {
        text:
          detail.state === null
            ? t("loop.outside.none")
            : t("loop.outside.state", { state: detail.state }),
        warn: false,
      };
  }
}

function Row({ row, narrow }: { row: LoopRow; narrow: boolean }) {
  const { pbi, folder, band, detail } = row;
  const { text, warn } = describe(detail);
  const held = band === "needsYou";

  const mark = detail.kind === "held" ? (
    <Pause
      size={14}
      role="img"
      aria-label={t("loop.heldMark")}
      style={{ color: "var(--pc-warning-700)" }}
    />
  ) : detail.kind === "running" || detail.kind === "script" ? (
    <span
      role="img"
      aria-label={
        detail.kind === "script"
          ? t(warn ? "loop.scriptStaleMark" : "loop.scriptMark")
          : t(warn ? "loop.staleMark" : "loop.liveMark")
      }
      style={{
        width: 8,
        height: 8,
        borderRadius: "var(--radius-full)",
        background: warn ? "var(--warning)" : "var(--primary)",
      }}
    />
  ) : null;

  const cells = (
    <>
      <span style={{ ...ellipsis, color: "var(--muted-foreground)", fontSize: "var(--text-xs)" }}>
        {folder?.name ?? t("loop.noFolder")}
      </span>
      <span style={{ fontFamily: "var(--font-mono)", color: "var(--muted-foreground)" }}>
        #{pbi.id}
      </span>
      <span style={{ ...ellipsis, fontWeight: 500 }} title={pbi.title}>
        {pbi.title}
      </span>
      <span style={{ ...ellipsis, fontFamily: "var(--font-mono)", fontSize: "var(--text-xs)" }}>
        {pbi.state ?? t("loop.noState")}
      </span>
      <span
        style={{
          fontSize: "var(--text-xs)",
          color: held || warn ? "var(--pc-warning-700)" : "var(--muted-foreground)",
          minWidth: 0,
          overflowWrap: "anywhere",
        }}
      >
        {text}
      </span>
      <span style={{ display: "flex", justifyContent: "flex-end" }}>{mark}</span>
    </>
  );

  const style: CSSProperties = {
    display: "grid",
    gridTemplateColumns: narrow
      ? "minmax(0, 1fr) auto"
      : "160px 64px minmax(0, 1fr) 150px minmax(0, 1.4fr) 16px",
    gap: narrow ? "4px 12px" : 16,
    alignItems: "center",
    padding: "12px 16px",
    border: `1px solid ${held ? "var(--warning)" : "var(--border)"}`,
    borderRadius: "var(--radius-xl)",
    background: held ? "color-mix(in srgb, var(--warning) 8%, var(--card))" : "var(--card)",
    color: "var(--foreground)",
    textDecoration: "none",
  };

  // At a narrow width the same six cells fall into two columns: folder and id, title and state,
  // the reason and the mark.
  if (!folder) return <div style={style}>{cells}</div>;
  return (
    <Link
      to={`/pbis/${folder.id}/${encodeURIComponent(pbi.id)}/journey`}
      className="row-hover aio-loop-row"
      aria-label={`${t("loop.open", { pbi: pbi.id })}: ${pbi.title}`}
      style={style}
    >
      {cells}
    </Link>
  );
}

const ellipsis: CSSProperties = {
  minWidth: 0,
  overflow: "hidden",
  textOverflow: "ellipsis",
  whiteSpace: "nowrap",
};

/** The engine's latest decisions: when, which PBI, from which state to which, and why. */
function Decisions({
  lines,
  folders,
  loaded,
  narrow,
}: {
  lines: JournalLine[];
  folders: readonly LoopFolder[];
  loaded: boolean;
  narrow: boolean;
}) {
  return (
    <section
      aria-label={t("loop.decisions")}
      style={{ display: "flex", flexDirection: "column", gap: 8 }}
    >
      <h2 style={{ ...label, margin: 0, fontWeight: 600 }}>{t("loop.decisions")}</h2>
      {!loaded ? (
        <Loading />
      ) : lines.length === 0 ? (
        <Quiet>{t("loop.decisions.empty")}</Quiet>
      ) : (
        <ol
          style={{
            listStyle: "none",
            margin: 0,
            padding: 0,
            border: "1px solid var(--border)",
            borderRadius: "var(--radius-xl)",
            overflow: "hidden",
          }}
        >
          {lines.map((line, index) => (
            <li
              key={`${line.at}-${line.repository}-${line.pbi}-${index}`}
              style={{ borderTop: index === 0 ? "none" : "1px solid var(--border)" }}
            >
              <Decision line={line} folders={folders} narrow={narrow} />
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}

function Decision({
  line,
  folders,
  narrow,
}: {
  line: JournalLine;
  folders: readonly LoopFolder[];
  narrow: boolean;
}) {
  const folder = folders.find((entry) => entry.repository === line.repository);
  const switched = line.kind === "switched";
  const mono: CSSProperties = { fontFamily: "var(--font-mono)", color: "var(--muted-foreground)" };
  const style: CSSProperties = {
    display: "grid",
    gridTemplateColumns: narrow ? "auto auto minmax(0, 1fr)" : "56px 64px 300px minmax(0, 1fr)",
    gap: narrow ? "2px 12px" : 16,
    alignItems: "baseline",
    padding: "9px 14px",
    color: "var(--foreground)",
    textDecoration: "none",
  };
  const cells = (
    <>
      <span style={mono}>{time(line.at)}</span>
      <span style={{ ...mono, overflowWrap: "anywhere" }}>
        {switched ? (folder?.name ?? line.repository) : `#${line.pbi}`}
      </span>
      <span
        style={{
          fontFamily: "var(--font-mono)",
          fontSize: "var(--text-xs)",
          overflowWrap: "anywhere",
        }}
      >
        {transition(line)}
      </span>
      <span
        style={{
          color: "var(--muted-foreground)",
          gridColumn: narrow ? "1 / -1" : undefined,
          overflowWrap: "anywhere",
        }}
      >
        {line.why}
      </span>
    </>
  );

  // A switch is the folder's, not a PBI's, so it opens no Journey.
  const row =
    !folder || switched ? (
      <div style={{ ...style, flex: 1, minWidth: 0 }}>{cells}</div>
    ) : (
      <Link
        to={`/pbis/${folder.id}/${encodeURIComponent(line.pbi)}/journey`}
        className="row-hover aio-loop-row"
        style={{ ...style, flex: 1, minWidth: 0 }}
      >
        {cells}
      </Link>
    );

  // A ran line links to everything its script printed, as the Journey does (#641). The link sits
  // beside the row and not inside it, because an anchor inside the row's anchor is invalid HTML.
  const log = scriptOutputUrl(line);
  if (!log) return row;
  return (
    <div style={{ display: "flex", alignItems: "baseline" }}>
      {row}
      <a
        href={log}
        target="_blank"
        rel="noreferrer"
        aria-label={t("journey.logOf", { script: line.why.split(":")[0] ?? line.why })}
        style={{ padding: "9px 14px 9px 0" }}
      >
        {t("journey.log")}
      </a>
    </div>
  );
}

function Quiet({ children }: { children: ReactNode }) {
  return <p style={{ margin: 0, color: "var(--muted-foreground)" }}>{children}</p>;
}
