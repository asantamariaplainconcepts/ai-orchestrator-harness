import { useState, type CSSProperties, type ReactNode } from "react";
import { Link, useParams, useSearchParams } from "react-router";
import { explain } from "@/shared/http/client";
import {
  type Metrics,
  type MetricsHoldReason,
  type MetricsNote,
  type MetricsRun,
  useFolders,
  useMetrics,
} from "@/shared/http/queries";
import { t, type TranslationKey } from "@/shared/i18n";
import { Dialog, Empty, ErrorState, Loading, Select } from "@/shared/ui/kit";
import { useNarrowContainer } from "@/shared/ui/useNarrowContainer";
import { BarRows, Legend, StackedBar } from "./charts";
import { cost, duration, tokens, when } from "./format";

/**
 * The Metrics screen of a folder (#734): how the loop works its repository, from what the engine
 * wrote, what the runs kept and what the vendor says. Four blocks, after the Platform's Project Health
 * board: the agents by step, what people did at each hold, the flow, and the notes that change how a
 * number reads. The period and the epic are in the address, so a view can be sent as a link.
 *
 * Every number is a button that lists the PBIs or runs it counts, each opening its Journey: a number
 * nobody can take apart is one nobody can check.
 */
export function MetricsScreen() {
  const { folderId = "" } = useParams();
  const folders = useFolders();
  const [search, setSearch] = useSearchParams();
  const period = search.get("days") ?? "30";
  const epic = search.get("epic") ?? "";
  const folder = folders.data?.find((each) => each.id === folderId);
  const metrics = useMetrics(
    folder?.repository ?? "",
    period === "all" ? undefined : Number(period),
    epic,
  );
  const [host, narrow] = useNarrowContainer<HTMLDivElement>(820);

  const choose = (name: string, value: string) =>
    setSearch(
      (current) => {
        const next = new URLSearchParams(current);
        next.set(name, value);
        if (name === "epic" && value === "") next.delete("epic");
        return next;
      },
      { replace: true },
    );

  if (folders.isError) {
    return <ErrorState detail={explain(folders.error)} onRetry={() => void folders.refetch()} />;
  }
  if (!folders.data) return <Loading />;
  // Titled even when there is nothing to count, like every screen: one h1, then the fix.
  if (!folder || !folder.repository) {
    return (
      <div style={{ padding: "24px clamp(16px, 4vw, 40px)" }}>
        <h1 style={{ margin: 0, fontSize: "var(--text-lg)", fontWeight: 600 }}>
          {t("loop.metrics")}
        </h1>
        <Empty messageKey={folder ? "metrics.noRepository" : "metrics.noFolder"} />
      </div>
    );
  }

  const report = metrics.data;
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
        <div style={{ display: "flex", flexDirection: "column", gap: 4, minWidth: 0 }}>
          <h1
            style={{
              margin: 0,
              fontSize: "var(--text-lg)",
              fontWeight: 600,
              overflowWrap: "anywhere",
            }}
          >
            {t("metrics.title", { folder: folder.name })}
          </h1>
          <p style={{ margin: 0, color: "var(--muted-foreground)" }}>
            {t("metrics.lead")} <Link to="/loop">{t("metrics.backToLoop")}</Link>
          </p>
        </div>
        <div style={{ display: "flex", gap: 12, flexWrap: "wrap" }}>
          <label style={control}>
            {t("metrics.period")}
            <Select
              value={period}
              onChange={(event) => choose("days", event.target.value)}
              style={{ height: 32 }}
            >
              <option value="7">{t("metrics.period.7")}</option>
              <option value="30">{t("metrics.period.30")}</option>
              <option value="all">{t("metrics.period.all")}</option>
            </Select>
          </label>
          <label style={control}>
            {t("metrics.epic")}
            <Select
              value={epic}
              onChange={(event) => choose("epic", event.target.value)}
              style={{ height: 32, maxWidth: 240 }}
            >
              <option value="">{t("metrics.epic.all")}</option>
              {(report?.epics ?? []).map((each) => (
                <option key={each.id} value={each.id}>
                  #{each.id} {each.title}
                </option>
              ))}
              {epic && report && !report.epics.some((each) => each.id === epic) ? (
                <option value={epic}>#{epic}</option>
              ) : null}
            </Select>
          </label>
        </div>
      </header>

      {metrics.isError ? (
        <ErrorState detail={explain(metrics.error)} onRetry={() => void metrics.refetch()} />
      ) : !report ? (
        <Loading />
      ) : (
        <Blocks report={report} folderId={folder.id} narrow={narrow} />
      )}
    </div>
  );
}

const control: CSSProperties = {
  display: "flex",
  alignItems: "center",
  gap: 8,
  color: "var(--muted-foreground)",
};

/** What a number lists when pressed: PBIs, or runs, under a title. */
type Listing = { title: string; pbis?: readonly string[]; runs?: readonly string[] };

function Blocks({
  report,
  folderId,
  narrow,
}: {
  report: Metrics;
  folderId: string;
  narrow: boolean;
}) {
  const [listing, setListing] = useState<Listing | null>(null);
  const show = (title: string, list: Omit<Listing, "title">) => setListing({ title, ...list });

  return (
    <>
      <Agents report={report} folderId={folderId} narrow={narrow} show={show} />
      <People report={report} narrow={narrow} show={show} />
      <Flow report={report} narrow={narrow} show={show} />
      <Notes notes={report.notes} show={show} />
      {listing ? (
        <ListDialog
          listing={listing}
          report={report}
          folderId={folderId}
          onClose={() => setListing(null)}
        />
      ) : null}
    </>
  );
}

type Show = (title: string, list: Omit<Listing, "title">) => void;

/** A number that lists what it counts. A count of nothing is plain text: there is nothing to list. */
function Figure({
  value,
  label,
  list,
  show,
}: {
  value: number;
  label: string;
  list: Omit<Listing, "title">;
  show: Show;
}) {
  if (value === 0) return <span style={{ fontVariantNumeric: "tabular-nums" }}>0</span>;
  return (
    <button
      type="button"
      className="aio-figure"
      aria-label={t("metrics.list", { what: label, count: String(value) })}
      onClick={() => show(label, list)}
    >
      {value}
    </button>
  );
}

function Block({ title, lead, children }: { title: string; lead?: string; children: ReactNode }) {
  return (
    <section
      aria-label={title}
      className="pc-card"
      style={{ padding: 16, display: "flex", flexDirection: "column", gap: 16, minWidth: 0 }}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
        <h2 style={{ margin: 0, fontSize: "var(--text-base)", fontWeight: 600 }}>{title}</h2>
        {lead ? (
          <p style={{ margin: 0, color: "var(--muted-foreground)", fontSize: "var(--text-xs)" }}>
            {lead}
          </p>
        ) : null}
      </div>
      {children}
    </section>
  );
}

function Sub({ children }: { children: ReactNode }) {
  return <h3 style={{ margin: 0, fontSize: "var(--text-sm)", fontWeight: 600 }}>{children}</h3>;
}

function Quiet({ children }: { children: ReactNode }) {
  return <p style={{ margin: 0, color: "var(--muted-foreground)" }}>{children}</p>;
}

function Columns({ narrow, children }: { narrow: boolean; children: ReactNode }) {
  return (
    <div
      style={{
        display: "grid",
        gridTemplateColumns: narrow ? "minmax(0, 1fr)" : "repeat(2, minmax(0, 1fr))",
        gap: 24,
      }}
    >
      {children}
    </div>
  );
}

// ---- 1. Agents

function Agents({
  report,
  folderId,
  narrow,
  show,
}: {
  report: Metrics;
  folderId: string;
  narrow: boolean;
  show: Show;
}) {
  const { steps, runs } = report.agents;
  return (
    <Block title={t("metrics.agents")} lead={t("metrics.agents.lead")}>
      {steps.length === 0 ? (
        <Quiet>{t("metrics.agents.none")}</Quiet>
      ) : (
        <>
          <div className="pc-table-container">
            <table className="pc-table">
              <thead>
                <tr>
                  <th>{t("metrics.step")}</th>
                  <th style={numeric}>{t("metrics.runs")}</th>
                  <th style={numeric}>{t("metrics.succeeded")}</th>
                  <th style={numeric}>{t("metrics.median")}</th>
                  <th style={numeric}>{t("metrics.p90")}</th>
                  <th style={numeric}>{t("metrics.cost")}</th>
                  <th style={numeric}>{t("metrics.costPerRun")}</th>
                  <th style={numeric}>{t("metrics.tokens")}</th>
                  <th style={numeric}>{t("metrics.tokensPerRun")}</th>
                </tr>
              </thead>
              <tbody>
                {steps.map((step) => (
                  <tr key={step.state}>
                    <td>
                      <span style={{ fontFamily: "var(--font-mono)" }}>{step.state}</span>
                      {step.command ? (
                        <span
                          style={{ color: "var(--muted-foreground)", fontSize: "var(--text-xs)" }}
                        >
                          {" "}
                          · {step.command}
                        </span>
                      ) : null}
                    </td>
                    <td style={numeric}>
                      <Figure
                        value={step.runs.length}
                        label={t("metrics.runsOf", { step: step.state })}
                        list={{ runs: step.runs }}
                        show={show}
                      />
                    </td>
                    <td style={numeric}>
                      <Figure
                        value={step.succeeded.length}
                        label={t("metrics.succeededOf", { step: step.state })}
                        list={{ runs: step.succeeded }}
                        show={show}
                      />
                    </td>
                    <td style={numeric}>{duration(step.medianSeconds)}</td>
                    <td style={numeric}>{duration(step.p90Seconds)}</td>
                    <td style={numeric}>{cost(step.costUsd)}</td>
                    <td style={numeric}>{cost(step.costPerRun)}</td>
                    <td style={numeric}>{tokens(step.tokens)}</td>
                    <td style={numeric}>{tokens(step.tokensPerRun)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Columns narrow={narrow}>
            <div style={{ display: "flex", flexDirection: "column", gap: 8, minWidth: 0 }}>
              <Sub>{t("metrics.chart.cost")}</Sub>
              <BarRows
                label={t("metrics.chart.cost")}
                rows={steps.map((step) => ({
                  name: step.state,
                  values: [step.costUsd],
                  text: cost(step.costUsd),
                }))}
                colours={["var(--chart-1)"]}
              />
            </div>
            <div style={{ display: "flex", flexDirection: "column", gap: 8, minWidth: 0 }}>
              <Sub>{t("metrics.chart.duration")}</Sub>
              <Legend
                items={[
                  { name: t("metrics.median"), colour: "var(--chart-1)" },
                  { name: t("metrics.p90"), colour: "var(--chart-2)" },
                ]}
              />
              <BarRows
                label={t("metrics.chart.duration")}
                rows={steps.map((step) => ({
                  name: step.state,
                  values: [step.medianSeconds ?? 0, step.p90Seconds ?? 0],
                  text: `${duration(step.medianSeconds)} · ${duration(step.p90Seconds)}`,
                }))}
                colours={["var(--chart-1)", "var(--chart-2)"]}
              />
            </div>
          </Columns>
          <Sub>{t("metrics.runsTable")}</Sub>
          <RunsTable runs={runs} folderId={folderId} />
        </>
      )}
    </Block>
  );
}

const numeric: CSSProperties = { textAlign: "right", fontVariantNumeric: "tabular-nums" };

function RunsTable({ runs, folderId }: { runs: readonly MetricsRun[]; folderId: string }) {
  return (
    <div className="pc-table-container" style={{ maxHeight: 420, overflowY: "auto" }}>
      <table className="pc-table">
        <thead>
          <tr>
            <th>{t("metrics.step")}</th>
            <th>{t("metrics.pbi")}</th>
            <th>{t("metrics.started")}</th>
            <th style={numeric}>{t("metrics.duration")}</th>
            <th>{t("metrics.result")}</th>
            <th style={numeric}>{t("metrics.cost")}</th>
          </tr>
        </thead>
        <tbody>
          {runs.map((run) => (
            <tr key={run.runId}>
              <td style={{ fontFamily: "var(--font-mono)" }}>{run.state}</td>
              <td>
                <Link
                  to={journey(folderId, run.pbi)}
                  aria-label={t("metrics.openJourney", { pbi: run.pbi })}
                >
                  #{run.pbi}
                </Link>
              </td>
              <td>{when(run.startedAt)}</td>
              <td style={numeric}>{duration(run.seconds)}</td>
              <td style={{ color: run.succeeded ? "var(--foreground)" : "var(--pc-warning-700)" }}>
                {run.result}
              </td>
              <td style={numeric}>{cost(run.costUsd)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function journey(folderId: string, pbi: string): string {
  return `/pbis/${folderId}/${encodeURIComponent(pbi)}/journey`;
}

// ---- 2. People

const endings: readonly {
  key: keyof Pick<MetricsHoldReason, "unchanged" | "afterCommit" | "returned" | "closed" | "open">;
  name: TranslationKey;
  colour: string;
}[] = [
  { key: "unchanged", name: "metrics.hold.unchanged", colour: "var(--chart-2)" },
  { key: "afterCommit", name: "metrics.hold.afterCommit", colour: "var(--chart-1)" },
  { key: "returned", name: "metrics.hold.returned", colour: "var(--chart-5)" },
  { key: "closed", name: "metrics.hold.closed", colour: "var(--muted-foreground)" },
  { key: "open", name: "metrics.hold.open", colour: "var(--border)" },
];

function People({ report, narrow, show }: { report: Metrics; narrow: boolean; show: Show }) {
  const { holds, decisions, mergedWithPerson, mergedAgentOnly } = report.people;
  return (
    <Block title={t("metrics.people")} lead={t("metrics.people.lead")}>
      <Sub>{t("metrics.holds")}</Sub>
      {holds.length === 0 ? (
        <Quiet>{t("metrics.holds.none")}</Quiet>
      ) : (
        <>
          <Legend
            items={endings.map((ending) => ({ name: t(ending.name), colour: ending.colour }))}
          />
          <ul
            style={{
              listStyle: "none",
              margin: 0,
              padding: 0,
              display: "flex",
              flexDirection: "column",
              gap: 14,
            }}
          >
            {holds.map((hold) => {
              const all = endings.flatMap((ending) => hold[ending.key]);
              const ended = all.length - hold.open.length;
              return (
                <li
                  key={hold.reason}
                  style={{ display: "flex", flexDirection: "column", gap: 6, minWidth: 0 }}
                >
                  <div
                    style={{
                      display: "flex",
                      justifyContent: "space-between",
                      gap: 12,
                      flexWrap: "wrap",
                    }}
                  >
                    <span style={{ overflowWrap: "anywhere", minWidth: 0 }}>
                      {hold.reason} ·{" "}
                      <Figure
                        value={all.length}
                        label={t("metrics.holdsOf", { reason: hold.reason })}
                        list={{ pbis: all }}
                        show={show}
                      />
                    </span>
                    <span style={{ color: "var(--muted-foreground)", fontSize: "var(--text-xs)" }}>
                      {t("metrics.hold.time", {
                        median: duration(hold.medianSeconds),
                        max: duration(hold.maxSeconds),
                      })}
                    </span>
                  </div>
                  <StackedBar
                    label={hold.reason}
                    parts={endings.map((ending) => ({
                      name: t(ending.name),
                      value: hold[ending.key].length,
                      colour: ending.colour,
                    }))}
                  />
                  <div
                    style={{
                      display: "flex",
                      gap: 12,
                      flexWrap: "wrap",
                      fontSize: "var(--text-xs)",
                      color: "var(--muted-foreground)",
                    }}
                  >
                    {endings
                      .filter((ending) => hold[ending.key].length > 0)
                      .map((ending) => (
                        <span key={ending.key}>
                          {t(ending.name)}{" "}
                          <Figure
                            value={hold[ending.key].length}
                            label={`${hold.reason} · ${t(ending.name)}`}
                            list={{ pbis: hold[ending.key] }}
                            show={show}
                          />
                        </span>
                      ))}
                  </div>
                  {hold.alwaysUnchanged ? (
                    <span
                      className="pc-badge pc-badge--warning"
                      style={{ alignSelf: "flex-start" }}
                    >
                      {t("metrics.hold.always", { count: String(ended) })}
                    </span>
                  ) : null}
                </li>
              );
            })}
          </ul>
        </>
      )}

      <Columns narrow={narrow}>
        <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
          <Sub>{t("metrics.decisions")}</Sub>
          {decisions.length === 0 ? (
            <Quiet>{t("metrics.decisions.none")}</Quiet>
          ) : (
            decisions.map((point) => {
              const name =
                point.tab === "proposal"
                  ? t("metrics.decision.plan")
                  : point.tab === "review"
                    ? t("metrics.decision.merge")
                    : point.state;
              return (
                <dl key={point.state} style={facts}>
                  <dt style={{ fontWeight: 500, gridColumn: "1 / -1" }}>
                    {name}{" "}
                    <span
                      style={{
                        fontFamily: "var(--font-mono)",
                        color: "var(--muted-foreground)",
                        fontSize: "var(--text-xs)",
                      }}
                    >
                      {point.state}
                    </span>
                  </dt>
                  <dt>{t("metrics.decision.asIs")}</dt>
                  <dd style={dd}>
                    <Figure
                      value={point.asIs.length}
                      label={`${name} · ${t("metrics.decision.asIs")}`}
                      list={{ pbis: point.asIs }}
                      show={show}
                    />
                  </dd>
                  <dt>{t("metrics.decision.afterChanges")}</dt>
                  <dd style={dd}>
                    <Figure
                      value={point.afterChanges.length}
                      label={`${name} · ${t("metrics.decision.afterChanges")}`}
                      list={{ pbis: point.afterChanges }}
                      show={show}
                    />
                  </dd>
                  <dt>{t("metrics.decision.withoutPerson")}</dt>
                  <dd style={dd}>
                    <Figure
                      value={point.withoutPerson.length}
                      label={`${name} · ${t("metrics.decision.withoutPerson")}`}
                      list={{ pbis: point.withoutPerson }}
                      show={show}
                    />
                  </dd>
                </dl>
              );
            })
          )}
        </div>
        <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
          <Sub>{t("metrics.merged")}</Sub>
          <dl style={facts}>
            <dt>{t("metrics.merged.person")}</dt>
            <dd style={dd}>
              <Figure
                value={mergedWithPerson.length}
                label={t("metrics.merged.person")}
                list={{ pbis: mergedWithPerson }}
                show={show}
              />
            </dd>
            <dt>{t("metrics.merged.agent")}</dt>
            <dd style={dd}>
              <Figure
                value={mergedAgentOnly.length}
                label={t("metrics.merged.agent")}
                list={{ pbis: mergedAgentOnly }}
                show={show}
              />
            </dd>
          </dl>
          <p style={{ margin: 0, color: "var(--muted-foreground)", fontSize: "var(--text-xs)" }}>
            {t("metrics.merged.how")}
          </p>
        </div>
      </Columns>
    </Block>
  );
}

const facts: CSSProperties = {
  display: "grid",
  gridTemplateColumns: "minmax(0, 1fr) auto",
  gap: "6px 16px",
  margin: 0,
};
const dd: CSSProperties = { margin: 0, textAlign: "right", fontVariantNumeric: "tabular-nums" };

// ---- 3. Flow

function Flow({ report, narrow, show }: { report: Metrics; narrow: boolean; show: Show }) {
  const { done, p50Seconds, p75Seconds, p90Seconds, epics, rework, weeks } = report.flow;
  const rounds = rework.reduce((sum, each) => sum + each.rounds, 0);
  const reworked = rework.map((each) => each.pbi);
  return (
    <Block title={t("metrics.flow")} lead={t("metrics.flow.lead")}>
      <Columns narrow={narrow}>
        <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
          <Sub>{t("metrics.labelToDone")}</Sub>
          <dl style={facts}>
            <dt>{t("metrics.doneCount")}</dt>
            <dd style={dd}>
              <Figure
                value={done.length}
                label={t("metrics.doneCount")}
                list={{ pbis: done }}
                show={show}
              />
            </dd>
            <dt>p50</dt>
            <dd style={dd}>{duration(p50Seconds)}</dd>
            <dt>p75</dt>
            <dd style={dd}>{duration(p75Seconds)}</dd>
            <dt>p90</dt>
            <dd style={dd}>{duration(p90Seconds)}</dd>
          </dl>
          <Sub>{t("metrics.rework")}</Sub>
          <dl style={facts}>
            <dt>{t("metrics.rework.pbis")}</dt>
            <dd style={dd}>
              <Figure
                value={reworked.length}
                label={t("metrics.rework.pbis")}
                list={{ pbis: reworked }}
                show={show}
              />
            </dd>
            <dt>{t("metrics.rework.rounds")}</dt>
            <dd style={dd}>{rounds}</dd>
          </dl>
          <p style={{ margin: 0, color: "var(--muted-foreground)", fontSize: "var(--text-xs)" }}>
            {t("metrics.rework.how")}
          </p>
        </div>
        <div style={{ display: "flex", flexDirection: "column", gap: 8, minWidth: 0 }}>
          <Sub>{t("metrics.weeks")}</Sub>
          {weeks.length === 0 ? (
            <Quiet>{t("metrics.weeks.none")}</Quiet>
          ) : (
            <BarRows
              label={t("metrics.weeks")}
              rows={weeks.map((week) => ({
                name: t("metrics.week", { date: day(week.week) }),
                values: [week.pbis.length],
                text: String(week.pbis.length),
              }))}
              colours={["var(--chart-1)"]}
            />
          )}
        </div>
      </Columns>
      <Sub>{t("metrics.epics")}</Sub>
      {epics.length === 0 ? (
        <Quiet>{t("metrics.epics.none")}</Quiet>
      ) : (
        <ul
          style={{
            listStyle: "none",
            margin: 0,
            padding: 0,
            display: "flex",
            flexDirection: "column",
            gap: 6,
          }}
        >
          {epics.map((epic) => (
            <li
              key={epic.id}
              style={{
                display: "flex",
                justifyContent: "space-between",
                gap: 12,
                flexWrap: "wrap",
              }}
            >
              <span style={{ overflowWrap: "anywhere", minWidth: 0 }}>
                <span style={{ fontFamily: "var(--font-mono)", color: "var(--muted-foreground)" }}>
                  #{epic.id}
                </span>{" "}
                {epic.title} ·{" "}
                <Figure
                  value={epic.pbis.length}
                  label={t("metrics.epicPbis", { epic: epic.id })}
                  list={{ pbis: epic.pbis }}
                  show={show}
                />
              </span>
              <span style={{ fontVariantNumeric: "tabular-nums" }}>
                {epic.seconds === null
                  ? t("metrics.epic.open", { since: when(epic.started) })
                  : duration(epic.seconds)}
              </span>
            </li>
          ))}
        </ul>
      )}
    </Block>
  );
}

const dayFormat = new Intl.DateTimeFormat(undefined, { day: "numeric", month: "short" });

function day(iso: string): string {
  return dayFormat.format(new Date(iso));
}

// ---- 4. Notes

function Notes({ notes, show }: { notes: readonly MetricsNote[]; show: Show }) {
  return (
    <Block title={t("metrics.notes")} lead={t("metrics.notes.lead")}>
      {notes.length === 0 ? (
        <Quiet>{t("metrics.notes.none")}</Quiet>
      ) : (
        <ul
          style={{
            listStyle: "none",
            margin: 0,
            padding: 0,
            display: "flex",
            flexDirection: "column",
            gap: 8,
          }}
        >
          {notes.map((note) => (
            <li key={note.kind} style={{ display: "flex", gap: 12, alignItems: "baseline" }}>
              <span
                className={`pc-badge ${informational.has(note.kind) ? "pc-badge--secondary" : "pc-badge--outline"}`}
                style={{ flex: "none" }}
              >
                {t(informational.has(note.kind) ? "metrics.note.context" : "metrics.note.warning")}
              </span>
              <span
                style={{ color: "var(--muted-foreground)", minWidth: 0, overflowWrap: "anywhere" }}
              >
                {sentence(note)}{" "}
                {note.runs.length > 0 || note.pbis.length > 0 ? (
                  <button
                    type="button"
                    className="aio-figure"
                    onClick={() =>
                      show(
                        sentence(note),
                        note.runs.length > 0 ? { runs: note.runs } : { pbis: note.pbis },
                      )
                    }
                  >
                    {t("metrics.note.which")}
                  </button>
                ) : null}
              </span>
            </li>
          ))}
        </ul>
      )}
    </Block>
  );
}

/** Notes that describe the repository rather than a gap in the numbers. */
const informational = new Set(["firstRun", "noRuns", "manualRuns"]);

function sentence(note: MetricsNote): string {
  const count = String(note.count);
  switch (note.kind) {
    case "unpricedRuns":
      return t(note.count === 1 ? "metrics.note.unpricedOne" : "metrics.note.unpriced", { count });
    case "liveRuns":
      return t(note.count === 1 ? "metrics.note.liveOne" : "metrics.note.live", { count });
    case "openHolds":
      return t(note.count === 1 ? "metrics.note.openHoldOne" : "metrics.note.openHolds", { count });
    case "manualRuns":
      return t(note.count === 1 ? "metrics.note.manualOne" : "metrics.note.manual", { count });
    case "firstRun":
      return t("metrics.note.firstRun", { date: note.at ? when(note.at) : "" });
    case "noRuns":
      return t("metrics.note.noRuns");
    case "olderPullRequests":
      return t("metrics.note.olderPullRequests", { count });
    case "unmarkedCommits":
      return t(note.count === 1 ? "metrics.note.unmarkedCommitOne" : "metrics.note.unmarkedCommits", {
        count,
        date: note.at ? when(note.at) : "",
      });
    default:
      return t("metrics.note.vendorUnread", { reason: note.detail ?? "" });
  }
}

// ---- What a number lists

function ListDialog({
  listing,
  report,
  folderId,
  onClose,
}: {
  listing: Listing;
  report: Metrics;
  folderId: string;
  onClose: () => void;
}) {
  const titles = new Map(report.pbis.map((pbi) => [pbi.id, pbi.title]));
  const runs = new Map(report.agents.runs.map((run) => [run.runId, run]));
  return (
    <Dialog
      title={listing.title}
      onClose={onClose}
      footer={
        <span style={{ color: "var(--muted-foreground)", fontSize: "var(--text-xs)" }}>
          {t("metrics.listed")}
        </span>
      }
    >
      <ul
        aria-label={listing.title}
        style={{
          listStyle: "none",
          margin: 0,
          padding: 0,
          display: "flex",
          flexDirection: "column",
          gap: 6,
        }}
      >
        {listing.runs
          ? listing.runs.map((id) => {
              const run = runs.get(id);
              return (
                <li key={id}>
                  {run ? (
                    <Link to={journey(folderId, run.pbi)}>
                      #{run.pbi} · {run.state} · {when(run.startedAt)} · {run.result}
                    </Link>
                  ) : (
                    <span style={{ fontFamily: "var(--font-mono)" }}>{id}</span>
                  )}
                </li>
              );
            })
          : (listing.pbis ?? []).map((pbi, index) => (
              <li key={`${pbi}-${index}`}>
                <Link to={journey(folderId, pbi)}>
                  #{pbi} {titles.get(pbi) ?? ""}
                </Link>
              </li>
            ))}
      </ul>
    </Dialog>
  );
}
