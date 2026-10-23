import type {
  EnginePresent,
  JournalLine,
  LiveSession,
  LoopFolder,
  VendorPbi,
} from "@/shared/http/api";
import { stalled } from "@/shared/http/api";
import type { LoopShape, LoopStateRow } from "@/shared/http/queries";

/** The three bands of the Loop screen, in the order they are drawn. */
export type Band = "needsYou" | "inMotion" | "rest";

export const bands: readonly Band[] = ["needsYou", "inMotion", "rest"];

type EngineRun = EnginePresent["liveRuns"][number];
export type EngineScript = EnginePresent["liveScripts"][number];

/** What a row says after its state: the hold's reason, the live run or script, or the next step. */
export type Detail =
  | { kind: "held"; why: string | null; since: string | null }
  | { kind: "running"; session: LiveSession | null; run: EngineRun | null }
  | { kind: "script"; script: EngineScript }
  | { kind: "next"; step: Step | null; waited: string | null }
  | { kind: "outside"; state: string | null };

/** What a declared state does: an agent's command, or the scripts and score it runs. */
export type Step = { kind: "command"; command: string } | { kind: "scripts"; scripts: string[] };

export interface LoopRow {
  pbi: VendorPbi;
  folder: LoopFolder | undefined;
  band: Band;
  detail: Detail;
}

/** The same PBI across the four reads: the vendor's repository and id. */
export function pbiKey(repository: string, id: string): string {
  return `${repository}#${id}`;
}

/**
 * The open PBIs as the screen draws them: a hold or a stale run needs a person, then a live run or
 * a script the engine is running is in motion, and everything else is the rest. Each reason is one the engine or the vendor wrote: the hold's is
 * the journal's latest `held` line for that PBI, the run's is its session, and the next step is the
 * state as the engine reads the folder's loop, with the engine's latest wait when it is about that
 * state. Nothing here guesses what the engine would do.
 *
 * `journal` is newest first, as `/api/loop/journal` answers it. `loops` is each folder's
 * `/api/loop/definition` by its path: a folder with no loop, or one that breaks its rules, declares
 * no state, so its PBIs are outside the loop and the strip says why.
 */
export function loopRows(
  pbis: readonly VendorPbi[],
  folders: readonly LoopFolder[],
  present: EnginePresent | undefined,
  sessions: readonly LiveSession[],
  journal: readonly JournalLine[],
  loops: ReadonlyMap<string, LoopShape | undefined>,
): LoopRow[] {
  const runs = new Map(
    (present?.liveRuns ?? []).map((run) => [pbiKey(run.repository, run.pbi), run]),
  );
  const scripts = new Map(
    (present?.liveScripts ?? []).map((script) => [pbiKey(script.repository, script.pbi), script]),
  );
  const live = new Map(
    // A session on a folder (#516) belongs to no PBI.
    sessions.flatMap((session) =>
      session.repository !== null && session.pbi !== null
        ? [[pbiKey(session.repository, session.pbi), session] as const]
        : [],
    ),
  );
  const latest = new Map<string, JournalLine>();
  const holds = new Map<string, JournalLine>();
  for (const line of journal) {
    const key = pbiKey(line.repository, line.pbi);
    if (!latest.has(key)) latest.set(key, line);
    if (line.kind === "held" && !holds.has(key)) holds.set(key, line);
  }
  const rows = pbis
    .filter((pbi) => !pbi.closed)
    .map((pbi): LoopRow => {
      const key = pbiKey(pbi.repository, pbi.id);
      const folder = folders.find((entry) => entry.path === pbi.folderPath);

      if (pbi.held) {
        const hold = holds.get(key);
        // A hold the engine put on at another state is an older one; this one a person put on.
        const current = hold && hold.from === pbi.state ? hold : undefined;
        return {
          pbi,
          folder,
          band: "needsYou",
          detail: { kind: "held", why: current?.why ?? null, since: current?.at ?? null },
        };
      }

      const run = runs.get(key) ?? null;
      const session = live.get(key) ?? null;
      if (run || session) {
        // A loop run silent past Runs:StaleAfter needs a person to look at it, and is back in
        // motion the moment it writes. It is not a hold: nothing is written at the vendor (#546).
        // A terminal a person opened is theirs to leave quiet.
        const stale = (run?.stale ?? false) || stalled(session);
        return {
          pbi,
          folder,
          band: stale ? "needsYou" : "inMotion",
          detail: { kind: "running", session, run },
        };
      }

      // #597. A script runs inside the engine's pass and has no session. Past the threshold a
      // silent run is held to, it needs a person as that run would.
      const script = scripts.get(key);
      if (script) {
        return {
          pbi,
          folder,
          band: script.stale ? "needsYou" : "inMotion",
          detail: { kind: "script", script },
        };
      }

      const declared = loops.get(pbi.folderPath)?.states.find((state) => state.name === pbi.state);
      if (!declared) {
        return { pbi, folder, band: "rest", detail: { kind: "outside", state: pbi.state } };
      }
      const last = latest.get(key);
      return {
        pbi,
        folder,
        band: "rest",
        detail: {
          kind: "next",
          step: stepOf(declared),
          waited: last?.kind === "waited" && last.from === pbi.state ? last.why : null,
        },
      };
    });

  return bands.flatMap((band) => rows.filter((row) => row.band === band));
}

/** What a declared state does: its agent's command, or else the scripts and the score it runs. */
function stepOf(state: LoopStateRow): Step | null {
  if (state.command !== null) return { kind: "command", command: state.command };
  const scripts = [...state.scripts, ...(state.score === null ? [] : [state.score])];
  return scripts.length > 0 ? { kind: "scripts", scripts } : null;
}
