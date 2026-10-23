import { ChevronDown, ChevronRight, CircleCheck, Pause, Play } from "lucide-react";
import { lazy, Suspense, useEffect, useState, type ReactNode } from "react";
import { Link } from "react-router";
import { explain } from "@/shared/http/client";
import { evidenceUrl, recordingUrl, scriptOutputUrl, type JournalLine, type RunRow } from "@/shared/http/api";
import { useIssue, useIssueChildren, useLoopDefinition, useLoopPresent, usePbiJournal, usePbiRuns } from "@/shared/http/queries";
import { t } from "@/shared/i18n";
import { ago } from "@/shared/ui/ago";
import { Badge, Button, Callout, ErrorState, Loading } from "@/shared/ui/kit";
import { count, dollars, span, when } from "../format";
import { fromIssue, issueJourney, journey, type EngineScript, type JourneyRun, type JourneyState } from "../journey";
import type { PbiTabProps } from "../PbiTabProps";

// Both are paid for when a person opens a row, not when the Journey draws.
const Recording = lazy(() => import("../Recording"));
const SessionTerminal = lazy(() =>
  import("@/shared/terminal/SessionTerminal").then((module) => ({ default: module.SessionTerminal })),
);

/**
 * #698. An epic does not walk the loop: its sub-issues do, and the engine closes it with the last of
 * them. Its Journey says so and sends the person to the Issue tab, where the graph is, instead of
 * nine states in grey. The sub-issues are the ones the Issue tab reads for that graph.
 */
export default function JourneyTab(props: PbiTabProps) {
  const children = useIssueChildren(props.repository, props.id);
  if (children.isPending) return <Loading />;
  if (!Array.isArray(children.data) || children.data.length === 0) return <LoopJourney {...props} />;
  return (
    <div className="pc-empty" style={{ maxWidth: 1100 }}>
      <div className="pc-empty__header">
        <span className="pc-empty__desc">
          {t("journey.epic")}{" "}
          <Link to={`/pbis/${props.folder.id}/${props.id}/issue`}>{t("journey.epicLink")}</Link>
        </span>
      </div>
    </div>
  );
}

/**
 * The Journey (DESIGN.md#the-screens, the PBI's tabs; #511): where this PBI has been and where
 * it is. The states of the folder's loop top to bottom, each with its runs — the loop's and the
 * manual ones — and the engine's lines about it. A run unfolds into its end record as `aio done`
 * wrote it and its recording; a live one says since when it runs and how long ago it wrote, and
 * opens its terminal. A score is a state like any other, with its band and its why.
 */
function LoopJourney({ repository, id, detail }: PbiTabProps) {
  const runs = usePbiRuns(repository, id);
  const journal = usePbiJournal(repository, id);
  const loop = useLoopDefinition(repository);
  // #597. A script the engine is running has no run row; the present names it. Optional: a
  // Journey whose present cannot be read still shows everything else.
  const present = useLoopPresent();
  // #696. With no run and no line of its own, this instance draws the journey the engine of another
  // one wrote on the issue. The issue is already read for the Issue tab; local data always wins.
  const issue = useIssue(repository, id);
  const scripts = (present.data?.liveScripts ?? []).filter(
    (script) => script.repository === repository && script.pbi === id,
  );

  // "wrote 4s ago" has to keep counting between two polls that bring back the same rows.
  const live = (runs.data ?? []).some((run) => run.endedAt === null) || scripts.length > 0;
  const [, tick] = useState(0);
  useEffect(() => {
    if (!live) return;
    const timer = setInterval(() => tick((n) => n + 1), 1_000);
    return () => clearInterval(timer);
  }, [live]);

  const failed = runs.error ?? journal.error ?? loop.error;
  if (failed)
    return (
      <ErrorState
        detail={t("journey.failed", { reason: explain(failed) })}
        onRetry={() => {
          void runs.refetch();
          void journal.refetch();
          void loop.refetch();
        }}
      />
    );
  if (!runs.data || !journal.data || !loop.data) return <Loading />;

  const local = runs.data.length > 0 || journal.data.length > 0;
  if (!local && issue.isPending) return <Loading />;
  // An answer without its comments is read as no journey comment, never as a Journey that throws.
  const fromIssue =
    local || !Array.isArray(issue.data?.comments) ? null : issueJourney(issue.data.comments, repository, id);

  const states = journey(
    loop.data,
    fromIssue?.runs ?? runs.data,
    fromIssue?.journal ?? journal.data,
    detail.pbi.state,
    scripts,
  );

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16, maxWidth: 1100 }}>
      {loop.data.problem ? (
        <Callout>
          <span>{t("journey.problem", { problem: loop.data.problem })}</span>
        </Callout>
      ) : null}

      {fromIssue ? (
        <Callout tone="info">
          <span>
            {t("journey.fromIssue")}{" "}
            {fromIssue.url ? (
              <a href={fromIssue.url} target="_blank" rel="noreferrer">
                {t("journey.fromIssueLink")}
              </a>
            ) : null}
          </span>
        </Callout>
      ) : null}

      {states.length === 0 ? (
        <div className="pc-empty">
          <div className="pc-empty__header">
            <span className="pc-empty__desc">{t("journey.empty")}</span>
          </div>
        </div>
      ) : (
        <ol aria-label={t("journey.label")} style={{ listStyle: "none", margin: 0, padding: 0 }}>
          {states.map((state, index) => (
            <Step
              key={state.name}
              state={state}
              held={detail.pbi.held}
              last={index === states.length - 1}
              repository={repository}
            />
          ))}
        </ol>
      )}
    </div>
  );
}

function Step({
  state,
  held,
  last,
  repository,
}: {
  state: JourneyState;
  held: boolean;
  last: boolean;
  repository: string;
}) {
  const ahead = state.where === "ahead";
  const current = state.where === "current";
  const declared = state.declared;

  const plan = declared
    ? declared.score
      ? t("journey.plan.score", { score: declared.score })
      : [declared.agent, declared.command, ...declared.scripts].filter(Boolean).join(" · ")
    : t("journey.plan.undeclared");

  return (
    <li
      aria-current={current ? "step" : undefined}
      style={{ display: "grid", gridTemplateColumns: "20px minmax(0, 1fr)", gap: "0 14px" }}
    >
      <div style={{ display: "flex", flexDirection: "column", alignItems: "center" }}>
        <Marker where={state.where} held={current && held} />
        {last ? null : (
          <span style={{ flex: 1, width: 2, margin: "4px 0", background: "var(--border)" }} />
        )}
      </div>

      <div
        style={{
          paddingBottom: last ? 0 : 20,
          display: "flex",
          flexDirection: "column",
          gap: 8,
          minWidth: 0,
          color: ahead ? "var(--muted-foreground)" : undefined,
        }}
      >
        <div style={{ display: "flex", alignItems: "baseline", gap: 10, flexWrap: "wrap" }}>
          <span style={{ fontFamily: "var(--font-mono)", fontWeight: current ? 600 : 500 }}>{state.name}</span>
          {current ? <Badge variant="info">{t("journey.current")}</Badge> : null}
          <span style={{ fontSize: "var(--text-xs)", color: "var(--muted-foreground)" }}>{plan}</span>
        </div>

        {declared?.score && !ahead ? <Score name={declared.score} score={state.score} /> : null}

        {state.runs.map((entry) => (
          <Run key={entry.run.runId} entry={entry} repository={repository} />
        ))}

        {state.scripts.map((script) => (
          <ScriptRun key={`${script.script}-${script.since}`} script={script} />
        ))}

        {current && held ? <HoldNote lines={state.lines} /> : null}

        {state.lines.length > 0 ? <Lines lines={state.lines} /> : null}
      </div>
    </li>
  );
}

function Marker({ where, held }: { where: JourneyState["where"]; held: boolean }) {
  const box = { width: 20, height: 20, flex: "none" } as const;
  if (where === "visited")
    return <CircleCheck style={{ ...box, color: "var(--success)" }} aria-label={t("journey.visited")} />;
  if (where === "current" && held)
    return (
      <span
        className="pc-badge pc-badge--warning"
        style={{ ...box, padding: 0, borderRadius: "var(--radius-full)" }}
        role="img"
        aria-label={t("journey.held")}
      >
        <Pause aria-hidden />
      </span>
    );
  if (where === "current")
    return (
      <span
        style={{ ...box, borderRadius: "var(--radius-full)", border: "5px solid var(--info)", boxSizing: "border-box" }}
        role="img"
        aria-label={t("journey.current")}
      />
    );
  return (
    <span
      style={{ ...box, borderRadius: "var(--radius-full)", border: "2px solid var(--border)", boxSizing: "border-box" }}
      role="img"
      aria-label={t("journey.ahead")}
    />
  );
}

function Card({ children, tone }: { children: ReactNode; tone?: "warning" }) {
  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        border: `1px solid ${tone === "warning" ? "var(--warning)" : "var(--border)"}`,
        borderRadius: "var(--radius-xl)",
        background: "var(--card)",
        fontSize: "var(--text-sm)",
        minWidth: 0,
      }}
    >
      {children}
    </div>
  );
}

function Score({ name, score }: { name: string; score: JourneyState["score"] }) {
  return (
    <Card>
      <div style={{ display: "flex", gap: 12, padding: "10px 14px", flexWrap: "wrap", alignItems: "baseline" }}>
        <span style={{ fontFamily: "var(--font-mono)" }}>{name}</span>
        {score ? (
          <>
            <Badge variant={score.band === "low" ? "success" : "warning"}>{score.band}</Badge>
            <span style={{ color: "var(--muted-foreground)", minWidth: 0 }}>{score.why}</span>
          </>
        ) : (
          <span style={{ color: "var(--muted-foreground)" }}>{t("journey.notScored")}</span>
        )}
      </div>
    </Card>
  );
}

function HoldNote({ lines }: { lines: JournalLine[] }) {
  const hold = [...lines].reverse().find((line) => line.kind === "held");
  return (
    <Card tone="warning">
      <div style={{ padding: "10px 14px" }}>
        {hold
          ? hold.to
            ? t("journey.holdNext", { since: when(hold.at), next: hold.to })
            : t("journey.holdChoose", { since: when(hold.at) })
          : t("journey.holdByPerson")}
      </div>
    </Card>
  );
}

/**
 * The engine's lines from this state: each transition, start and wait, with its reason. A `ran`
 * line links to everything its script printed (#638): the why of a red gate is one sentence, and
 * the line that says which test failed is in the file.
 */
function Lines({ lines }: { lines: JournalLine[] }) {
  return (
    <ul
      aria-label={t("journey.lines")}
      style={{
        listStyle: "none",
        margin: 0,
        padding: 0,
        display: "flex",
        flexDirection: "column",
        gap: 4,
        fontSize: "var(--text-xs)",
        color: "var(--muted-foreground)",
      }}
    >
      {lines.map((line, index) => {
        const log = scriptOutputUrl(line);
        return (
          <li key={`${line.at}-${index}`} style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
            <span style={{ fontFamily: "var(--font-mono)" }}>{when(line.at)}</span>
            <span style={{ color: "var(--foreground)" }}>
              {line.to && line.kind !== "started"
                ? t("journey.lineTo", { kind: line.kind, to: line.to })
                : line.kind}
            </span>
            <span style={{ minWidth: 0 }}>{line.why}</span>
            {log ? (
              <a
                href={log}
                target="_blank"
                rel="noreferrer"
                aria-label={t("journey.logOf", { script: line.why.split(":")[0] ?? line.why })}
              >
                {t("journey.log")}
              </a>
            ) : null}
          </li>
        );
      })}
    </ul>
  );
}

function Run({ entry, repository }: { entry: JourneyRun; repository: string }) {
  const { run, ledTo } = entry;
  const [open, setOpen] = useState(false);
  const live = run.endedAt === null;
  const command = run.command || t("journey.shell");
  const who = [run.provider ?? t("journey.terminal"), run.model].filter(Boolean).join(" · ");

  return (
    <Card>
      <button
        type="button"
        className="aio-journey-run"
        aria-expanded={open}
        aria-label={t("journey.runRow", { command })}
        onClick={() => setOpen((shown) => !shown)}
        style={{
          display: "flex",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "6px 16px",
          width: "100%",
          padding: "10px 14px",
          border: 0,
          borderRadius: "var(--radius-xl)",
          color: "inherit",
          font: "inherit",
          textAlign: "left",
          cursor: "pointer",
        }}
      >
        <span style={{ display: "flex", alignItems: "baseline", gap: 10, minWidth: 0, flex: "1 1 240px", flexWrap: "wrap" }}>
          {open ? <ChevronDown size={14} aria-hidden /> : <ChevronRight size={14} aria-hidden />}
          <span style={{ fontFamily: "var(--font-mono)", overflowWrap: "anywhere" }}>{command}</span>
          <span style={{ color: "var(--muted-foreground)" }}>{who}</span>
          {run.manual ? <Badge variant="outline">{t("journey.manual")}</Badge> : null}
        </span>
        {live ? (
          <span style={{ color: "var(--muted-foreground)" }}>
            {run.stale
              ? t("journey.silent", { for: span(run.startedAt, null), ago: ago(run.lastOutputAt ?? run.startedAt) })
              : t("journey.live", { for: span(run.startedAt, null), ago: ago(run.lastOutputAt ?? run.startedAt) })}
          </span>
        ) : (
          <span style={{ color: "var(--muted-foreground)" }}>{span(run.startedAt, run.endedAt)}</span>
        )}
        <span style={{ fontFamily: "var(--font-mono)", color: "var(--muted-foreground)" }}>
          {run.costUsd === null ? t("journey.noCost") : dollars(run.costUsd)}
        </span>
        <Outcome run={run} />
      </button>

      {open ? <Details run={run} ledTo={ledTo} repository={repository} /> : null}
    </Card>
  );
}

/**
 * A script the engine is running at this state (#597), drawn as a live run's row is. It does not
 * unfold: a script writes to no terminal, so there is no recording, and its log stays in the file
 * its `why` names once it ends.
 */
function ScriptRun({ script }: { script: EngineScript }) {
  return (
    <Card tone={script.stale ? "warning" : undefined}>
      <div
        role="group"
        aria-label={t("journey.scriptRow", { script: script.script })}
        style={{ display: "flex", alignItems: "center", flexWrap: "wrap", gap: "6px 16px", padding: "10px 14px" }}
      >
        <span style={{ display: "flex", alignItems: "baseline", gap: 10, minWidth: 0, flex: "1 1 240px", flexWrap: "wrap" }}>
          <span style={{ fontFamily: "var(--font-mono)", overflowWrap: "anywhere" }}>{script.script}</span>
          <span style={{ color: "var(--muted-foreground)" }}>{t("journey.script")}</span>
        </span>
        <span style={{ color: script.stale ? "var(--pc-warning-700)" : "var(--muted-foreground)" }}>
          {t(script.stale ? "journey.scriptStale" : "journey.scriptLive", { for: span(script.since, null) })}
        </span>
        <Badge variant="info">{script.stale ? t("journey.stale") : t("journey.running")}</Badge>
      </div>
    </Card>
  );
}

function Outcome({ run }: { run: RunRow }) {
  if (run.endedAt === null) return <Badge variant="info">{run.stale ? t("journey.stale") : t("journey.running")}</Badge>;
  if (run.failure) return <Badge variant="error">{run.failure}</Badge>;
  if (run.outcome) return <Badge variant="success">{run.outcome}</Badge>;
  return <Badge variant="secondary">{t("journey.ended")}</Badge>;
}

function Details({ run, ledTo, repository }: { run: RunRow; ledTo: JournalLine | null; repository: string }) {
  const [playing, setPlaying] = useState(false);
  const live = run.endedAt === null;
  // #697. A run read from the issue's comment plays its copy on aio/evidence, through the vendor.
  const remote = fromIssue(run);
  const url = !remote ? recordingUrl(run) : run.recorded ? evidenceUrl(repository, run.recordingPath) : null;
  const command = run.command || t("journey.shell");

  const rows: [string, ReactNode][] = [];
  if (!live) {
    rows.push([
      t("journey.record"),
      run.endRecord ? (
        <pre style={{ margin: 0, fontFamily: "var(--font-mono)", fontSize: "var(--text-xs)", whiteSpace: "pre-wrap", overflowWrap: "anywhere" }}>
          {run.endRecord.trim()}
        </pre>
      ) : (
        <span style={{ color: "var(--muted-foreground)" }}>{t("journey.noRecord")}</span>
      ),
    ]);
  }
  if (run.failure) rows.push([t("journey.failure"), <span style={{ fontFamily: "var(--font-mono)" }}>{run.failure}</span>]);
  if (run.reason) rows.push([t("journey.reason"), run.reason]);
  if (run.problems.length > 0)
    rows.push([
      t("journey.problems"),
      <ul style={{ margin: 0, paddingLeft: 18 }}>
        {run.problems.map((problem, index) => (
          <li key={index}>{problem}</li>
        ))}
      </ul>,
    ]);
  if (ledTo)
    rows.push([
      t("journey.ledTo"),
      <span>
        <span style={{ fontFamily: "var(--font-mono)" }}>{ledTo.kind === "held" ? t("journey.hold") : ledTo.to}</span>
        <span style={{ color: "var(--muted-foreground)" }}> · {ledTo.why} · {when(ledTo.at)}</span>
      </span>,
    ]);
  if (run.inputTokens !== null || run.outputTokens !== null || run.costUsd !== null)
    rows.push([
      t("journey.tokens"),
      <span style={{ fontFamily: "var(--font-mono)", fontSize: "var(--text-xs)" }}>
        {[
          run.inputTokens === null ? null : t("journey.tokensIn", { count: count(run.inputTokens) }),
          run.outputTokens === null ? null : t("journey.tokensOut", { count: count(run.outputTokens) }),
          run.costUsd === null ? null : dollars(run.costUsd),
        ]
          .filter(Boolean)
          .join(" · ")}
      </span>,
    ]);
  if (live)
    rows.push([
      t("journey.since"),
      t("journey.sinceValue", { at: when(run.startedAt), ago: ago(run.lastOutputAt ?? run.startedAt) }),
    ]);

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        gap: 10,
        padding: "10px 14px 12px",
        borderTop: "1px solid var(--border)",
      }}
    >
      <dl style={{ display: "grid", gridTemplateColumns: "minmax(80px, 110px) minmax(0, 1fr)", gap: "6px 16px", margin: 0 }}>
        {rows.map(([label, value]) => (
          <div key={label} style={{ display: "contents" }}>
            <dt style={{ color: "var(--muted-foreground)" }}>{label}</dt>
            <dd style={{ margin: 0, minWidth: 0 }}>{value}</dd>
          </div>
        ))}
        <dt style={{ color: "var(--muted-foreground)" }}>{live ? t("journey.terminalLabel") : t("journey.recording")}</dt>
        <dd style={{ margin: 0, minWidth: 0 }}>
          {live ? (
            <Button variant="outline" size="xs" onClick={() => setPlaying((shown) => !shown)} aria-expanded={playing}>
              {playing ? t("journey.closeTerminal") : t("journey.openTerminal")}
            </Button>
          ) : url ? (
            <Button variant="outline" size="xs" onClick={() => setPlaying((shown) => !shown)} aria-expanded={playing}>
              <Play size={12} aria-hidden />
              {playing ? t("journey.hideRecording") : t("journey.play", { length: span(run.startedAt, run.endedAt) })}
            </Button>
          ) : (
            <span style={{ color: "var(--muted-foreground)" }}>
              {remote ? t("journey.noEvidence") : t("journey.noRecording")}
            </span>
          )}
        </dd>
      </dl>

      {playing ? (
        <Suspense fallback={<Loading />}>
          {live ? (
            <div style={{ height: 320, display: "flex", flexDirection: "column" }}>
              <SessionTerminal runId={run.runId} />
            </div>
          ) : url ? (
            <Recording url={url} label={t("journey.recordingOf", { command })} />
          ) : null}
        </Suspense>
      ) : null}
    </div>
  );
}
