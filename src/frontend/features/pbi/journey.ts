import type { EnginePresent, JournalLine, RunRow } from "@/shared/http/api";
import type { LoopShape, LoopStateRow } from "@/shared/http/queries";

/**
 * The Journey, put together from what the server wrote (DESIGN.md, "What the engine did
 * and why"): the loop's states top to bottom, each with its runs and the engine's lines about
 * it. Nothing here guesses at what happened; each field is a run row or a journal line, sorted into
 * the state it belongs to.
 */
export interface JourneyState {
  name: string;
  /** Null for a state the loop does not declare, such as `done`. */
  declared: LoopStateRow | null;
  where: "current" | "visited" | "ahead";
  runs: JourneyRun[];
  /** The scripts the engine is running at this state now (#597), from its present. */
  scripts: EngineScript[];
  /** The engine's lines from this state, oldest first. */
  lines: JournalLine[];
  /** For a score state, the band its latest decision took, and why. */
  score: { band: string; why: string } | null;
}

export interface JourneyRun {
  run: RunRow;
  /** The transition the engine wrote once the run had ended, or null while it has none. */
  ledTo: JournalLine | null;
}

export type EngineScript = EnginePresent["liveScripts"][number];

/** The kinds of line that move a PBI or stop it: the ones a run or a score can lead to. */
const transitions = new Set(["moved", "held", "released"]);

export function journey(
  loop: LoopShape | undefined,
  runs: readonly RunRow[],
  journalNewestFirst: readonly JournalLine[],
  current: string | null,
  scripts: readonly EngineScript[] = [],
): JourneyState[] {
  const lines = [...journalNewestFirst].reverse();
  const declared = loop?.states ?? [];

  // A loop that cannot be read still has a history: its runs are drawn under the states they
  // name, in the order they first ran.
  const names = [
    ...declared.map((state) => state.name),
    ...(loop?.undeclared ?? []),
    ...runs.map((run) => run.state),
    ...(current ? [current] : []),
  ].filter((name, index, all) => all.indexOf(name) === index);

  return names.map((name) => {
    const state = declared.find((entry) => entry.name === name) ?? null;
    const own = lines.filter((line) => line.from === name);
    const stateRuns = runs.filter((run) => run.state === name);

    return {
      name,
      declared: state,
      where:
        name === current
          ? "current"
          : stateRuns.length > 0 || own.length > 0 || lines.some((line) => line.to === name)
            ? "visited"
            : "ahead",
      runs: stateRuns.map((run) => ({ run, ledTo: ledTo(run, own) })),
      scripts: scripts.filter((script) => script.state === name),
      lines: own,
      score: state?.score ? scoreOf(state, own) : null,
    };
  });
}

/** The first move, hold or release from the run's state written once it had ended. */
function ledTo(run: RunRow, own: readonly JournalLine[]): JournalLine | null {
  if (run.manual || run.endedAt === null) return null;
  const ended = Date.parse(run.endedAt);
  return own.find((line) => transitions.has(line.kind) && Date.parse(line.at) >= ended) ?? null;
}

/**
 * A score's band and its `why`, from the engine's latest decision at that state. The engine writes
 * a move as `<band>: <why>` and a hold as `<the hold's reason>: <why>`, and the hold's reason is the
 * loop's, so the band is the outcome that declares it.
 */
function scoreOf(state: LoopStateRow, own: readonly JournalLine[]): JourneyState["score"] {
  const decided = [...own].reverse().find((line) => line.kind === "moved" || line.kind === "held");
  if (!decided) return null;

  for (const transition of state.transitions) {
    const prefix =
      decided.kind === "held" ? transition.hold : decided.kind === "moved" ? transition.outcome : null;
    if (transition.outcome && prefix && decided.why.startsWith(`${prefix}: `)) {
      return { band: transition.outcome, why: decided.why.slice(prefix.length + 2) };
    }
  }

  return null;
}

/** The hold the PBI is in, from the engine's latest line about its state, or null when a person put it on. */
export function holdOf(
  journalNewestFirst: readonly JournalLine[],
  state: string | null,
): JournalLine | null {
  const last = journalNewestFirst.find((line) => transitions.has(line.kind));
  return last && last.kind === "held" && last.from === state ? last : null;
}

/** The live run of the loop, if any: the one *Stop run* stops. A manual session is the person's own. */
export function liveLoopRun(runs: readonly RunRow[]): RunRow | null {
  return runs.find((run) => run.endedAt === null && !run.manual) ?? null;
}

/**
 * The loop's entry for the PBI's state (#595): its `tab`, the tab marked while the PBI is there, and
 * its `back`, where Request changes sends it. Undefined outside the loop, or before the loop is read.
 */
export function loopState(loop: LoopShape | undefined, state: string | null): LoopStateRow | undefined {
  return state === null ? undefined : loop?.states.find((entry) => entry.name === state);
}

/** How a run read from the issue's comment is told apart from this instance's: its id starts with it. */
const issueRun = "issue-";

/** Whether a run was read from the issue's journey comment, so its recording is on `aio/evidence`. */
export function fromIssue(run: RunRow): boolean {
  return run.runId.startsWith(issueRun);
}

/** What the Journey draws from the issue's `aio:journey` comment, shaped as the local reads are. */
export interface IssueJourney {
  runs: RunRow[];
  /** Newest first, as `/api/loop/journal` answers. */
  journal: JournalLine[];
  /** Where a person opens the comment at the vendor, or null when the vendor did not say. */
  url: string | null;
}

/**
 * #696. The journey the engine keeps as a comment on the issue, read back into runs and lines when
 * this instance's database has none for the PBI: another instance ran the engine. The table is
 * written by `JourneyComment.cs`; its columns are `When | State | Kind | To | Agent or script |
 * Result | Why | Took | Cost | Recording`, a run's kind is `run`, or `manual` for one a person
 * opened, and a failure's result is `failed: <cause>`. Recording is the path of the run's copy on the
 * `aio/evidence` branch (#697), which such a run carries as its `recordingPath`; a comment written
 * before that column has nine. Null when the issue carries no such comment.
 */
export function issueJourney(
  comments: readonly { body: string; harness: string | null; url: string | null }[],
  repository: string,
  pbi: string,
): IssueJourney | null {
  const comment = comments.find((each) => each.harness === "journey");
  if (!comment) return null;

  const runs: RunRow[] = [];
  const lines: JournalLine[] = [];
  const rows = comment.body
    .split(/\r?\n/)
    .filter((line) => /^\|\s*\d{4}-\d{2}-\d{2}T/.test(line))
    .map(cellsOf);

  rows.forEach(([at = "", state = "", kind = "", to = "", by = "", result = "", why = "", took = "", cost = "", recording = ""], index) => {
    if (Number.isNaN(Date.parse(at)) || !state) return;
    if (kind === "run" || kind === "manual") {
      const [command = "", provider = null, model = null] = by.split(" · ");
      const failed = result.startsWith("failed: ") ? result.slice("failed: ".length) : null;
      const dollars = /^\$(\d+(?:\.\d+)?)$/.exec(cost);
      runs.push({
        runId: `${issueRun}${index}`,
        state,
        manual: kind === "manual",
        provider,
        model,
        command,
        startedAt: at,
        endedAt: new Date(Date.parse(at) + seconds(took) * 1000).toISOString(),
        lastOutputAt: null,
        stale: false,
        outcome: failed === null && result ? result : null,
        reason: why || null,
        failure: failed,
        problems: [],
        endRecord: null,
        inputTokens: null,
        outputTokens: null,
        costUsd: dollars ? Number(dollars[1]) : null,
        recordingPath: recording,
        recorded: recording !== "",
      });
      return;
    }

    if (!transitions.has(kind)) return;
    lines.push({ id: index, at, repository, pbi, from: state, kind, to: to || null, why, output: null });
  });

  return { runs, journal: lines.reverse(), url: comment.url };
}

/** A row's cells, the pipes a cell carries escaped as `\|`. */
function cellsOf(row: string): string[] {
  return row
    .trim()
    .replace(/^\|/, "")
    .replace(/(?<!\\)\|$/, "")
    .split(/(?<!\\)\|/)
    .map((cell) => cell.trim().replaceAll("\\|", "|"));
}

/** "1h 2m 3s" as seconds; nothing, as no time at all. */
function seconds(took: string): number {
  let total = 0;
  for (const [, amount, unit] of took.matchAll(/(\d+)([hms])/g)) {
    total += Number(amount) * (unit === "h" ? 3600 : unit === "m" ? 60 : 1);
  }
  return total;
}
