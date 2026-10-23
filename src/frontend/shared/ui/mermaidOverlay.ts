/**
 * #611. The live state of an epic's sub-issues, written into its Mermaid graph as text before the
 * graph is drawn. Everything here is a pure function of its arguments, so the same children, journal
 * and palette always give the same text, and the diagram is drawn again only when that text changes.
 *
 * A node takes part when its id is `i<number>` of a sub-issue, the convention an epic's body uses
 * (`i611[#611 …]`). Nothing else in the diagram is touched.
 */

/** A sub-issue as the vendor has it: the shape of `GET /api/pbi/children`. */
export interface ChildIssue {
  repository: string;
  id: string;
  title: string;
  state: string | null;
  held: boolean;
  closed: boolean;
}

/** What the loop is running now, as `GET /api/loop/present` lists it. */
export interface LiveWork {
  liveRuns: readonly { repository: string; pbi: string }[];
  liveScripts: readonly { repository: string; pbi: string }[];
}

/** One journal line, as `GET /api/loop/journal` answers it, newest first. */
export interface JournalEntry {
  repository: string;
  pbi: string;
  kind: string;
  why: string;
}

/**
 * The class a sub-issue's node takes: `done`, `running`, `held`, `waiting`, or its loop state as it
 * is when none of those applies, or null when it carries no state at all.
 */
export type ChildStatus = "done" | "running" | "held" | "waiting" | (string & {}) | null;

/** A sub-issue with what the graph paints on it. */
export interface LiveChild {
  id: string;
  title: string;
  status: ChildStatus;
  /** The sub-issues it waits for because they declare the same files. */
  waitsFor: string[];
}

/** One colour role of the graph, each value a token's computed colour. */
export interface Paint {
  fill: string;
  stroke: string;
  color: string;
}

/** The colours of the four live states and of a plain loop state, read from DESIGN.md's tokens. */
export interface StatusPalette {
  done: Paint;
  running: Paint;
  held: Paint;
  waiting: Paint;
  plain: Paint;
}

/**
 * What `file-overlap` writes when another PBI in flight declares the same files: `#612 is in flight
 * and declares src/x too.`, or the same PBI named with no section to compare. The engine journals
 * that `why` on the waiting PBI's `waited` line, so the number at its start is who it waits for.
 */
const FILE_OVERLAP = /^#(\d+) is in flight\b/;

/**
 * Each sub-issue's status, from the vendor's facts and the loop's. Closed is done whatever else is
 * true; a live run or script is running; then the hold; then a wait, when the PBI's newest journal
 * line is `waited`; otherwise the loop state it carries.
 */
export function liveChildren(
  children: readonly ChildIssue[],
  live: LiveWork,
  journal: readonly JournalEntry[],
): LiveChild[] {
  const same = (a: string, b: string) => a.toLowerCase() === b.toLowerCase();
  const working = (child: ChildIssue) =>
    [...live.liveRuns, ...live.liveScripts].some(
      (work) => work.pbi === child.id && same(work.repository, child.repository),
    );

  return children.map((child) => {
    const newest = journal.find((line) => line.pbi === child.id && same(line.repository, child.repository));
    const waited = newest?.kind === "waited" ? newest : null;
    const status: ChildStatus = child.closed || child.state === "done"
      ? "done"
      : working(child)
        ? "running"
        : child.held
          ? "held"
          : waited
            ? "waiting"
            : child.state;
    const blocker = status === "waiting" ? FILE_OVERLAP.exec(waited?.why ?? "")?.[1] : undefined;
    return {
      id: child.id,
      title: child.title,
      status,
      waitsFor: blocker && children.some((other) => other.id === blocker) ? [blocker] : [],
    };
  });
}

/** How many sub-issues are in each live state; `other` holds those at a plain loop state or none. */
export interface ChildCounts {
  done: number;
  running: number;
  waiting: number;
  held: number;
  other: number;
}

/**
 * #636. The counts the Issue tab shows beside an epic's graph, read from the statuses the graph is
 * painted with, so the two never disagree.
 */
export function countChildren(children: readonly Pick<LiveChild, "status">[]): ChildCounts {
  const counts: ChildCounts = { done: 0, running: 0, waiting: 0, held: 0, other: 0 };
  for (const { status } of children) {
    const key = status !== null && status in counts && status !== "other" ? (status as keyof ChildCounts) : "other";
    counts[key]++;
  }
  return counts;
}

/** Whether the diagram is a flowchart, the only kind an epic's graph is and the only kind touched. */
export function isFlowchart(diagram: string): boolean {
  const first = diagram
    .split("\n")
    .map((line) => line.trim())
    .find((line) => line !== "" && !line.startsWith("%%"));
  return first !== undefined && /^(flowchart|graph)\b/.test(first);
}

/**
 * The diagram with the live state appended: one `classDef` per class used, from the palette, a
 * `class` line per node of a sub-issue the diagram names, and a dotted edge labelled `waits` from
 * a sub-issue to the one it waits for on files. A diagram that is not a flowchart, or that names no
 * sub-issue, comes back as it was.
 */
export function overlay(
  diagram: string,
  children: readonly LiveChild[],
  palette: StatusPalette,
  waits: string,
): string {
  if (!isFlowchart(diagram)) return diagram;

  const named = new Set(
    children.filter((child) => new RegExp(`(^|[^\\w])i${child.id}($|[^\\w])`, "m").test(diagram)).map((child) => child.id),
  );
  const painted = children.filter((child) => named.has(child.id) && child.status !== null);
  const edges = children.flatMap((child) =>
    named.has(child.id) ? child.waitsFor.filter((id) => named.has(id)).map((id) => [child.id, id] as const) : [],
  );
  if (painted.length === 0 && edges.length === 0) return diagram;

  const classes = [...new Set(painted.map((child) => className(child.status!)))];
  const lines = [
    ...classes.map((name) => `  classDef ${name} ${style(paintOf(name, palette))}`),
    ...painted.map((child) => `  class i${child.id} ${className(child.status!)}`),
    ...edges.map(([from, to]) => `  i${from} -.->|${label(waits)}| i${to}`),
  ];
  return `${diagram.replace(/\s+$/, "")}\n${lines.join("\n")}\n`;
}

/**
 * A graph for an issue whose body draws none: the issue, an arrow to each of its sub-issues, and
 * each node labelled with its number and title, ready for {@link overlay}.
 */
export function basicDiagram(id: string, title: string, children: readonly Pick<LiveChild, "id" | "title">[]): string {
  return [
    "flowchart LR",
    `  e${id}["${label(`#${id} ${short(title)}`)}"]`,
    ...children.map((child) => `  e${id} --> i${child.id}["${label(`#${child.id} ${short(child.title)}`)}"]`),
    "",
  ].join("\n");
}

const LIVE = new Set(["done", "running", "held", "waiting"]);

/** A loop state such as `proposal-review` as a class name Mermaid's grammar takes. */
function className(status: string): string {
  return LIVE.has(status) ? status : `state_${status.replace(/\W/g, "_")}`;
}

function paintOf(name: string, palette: StatusPalette): Paint & { dashed?: boolean } {
  switch (name) {
    case "done":
      return palette.done;
    case "running":
      return palette.running;
    case "held":
      return palette.held;
    case "waiting":
      return { ...palette.waiting, dashed: true };
    default:
      return palette.plain;
  }
}

function style(paint: Paint & { dashed?: boolean }): string {
  return `fill:${paint.fill},stroke:${paint.stroke},color:${paint.color}${paint.dashed ? ",stroke-dasharray:4 3" : ""}`;
}

/** Text inside a quoted Mermaid label or an edge label, with what would end it written as an entity. */
function label(text: string): string {
  return text.replace(/"/g, "#quot;").replace(/\|/g, "#124;");
}

function short(title: string): string {
  return title.length > 48 ? `${title.slice(0, 47).trimEnd()}…` : title;
}
