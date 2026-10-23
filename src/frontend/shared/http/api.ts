/**
 * The API, typed by its generated contract (#508).
 *
 * `contracts.ts` beside this file is generated, never written: `dotnet build` writes the Server's
 * OpenAPI document to `openapi.json`, and `pnpm contracts` (run by `build` and `typecheck`) turns it
 * into types. A route that goes or a field that changes on the server fails the typecheck here and
 * in every screen that reads it.
 */
import { api } from "./client";
import type { components, paths } from "./contracts";

export type Schemas = components["schemas"];

export type VendorPbi = Schemas["VendorPbi"];
export type PbiList = Schemas["PbiList"];
export type PbiDetail = Schemas["PbiDetail"];
export type RunRow = Schemas["RunRow"];
export type JournalLine = Schemas["Line"];
export type EnginePresent = Schemas["ReadPresentResponse"];
export type LiveSession = Schemas["LiveSession"];
export type LoopFolder = Schemas["LoopFolderRow"];
export type Machine = Schemas["Machine"];

/** What a route answers with 200, read from the contract so a renamed route is a type error. */
type Answer<Operation> = Operation extends {
  responses: { 200: { content: { "application/json": infer Body } } };
}
  ? Body
  : never;

export type Get<Path extends keyof paths> = Answer<paths[Path]["get"]>;
export type Post<Path extends keyof paths> = Answer<paths[Path]["post"]>;

function query(values: Record<string, string | number | undefined>): string {
  const defined = Object.entries(values).filter(
    (entry): entry is [string, string | number] => entry[1] !== undefined,
  );
  return defined.length === 0
    ? ""
    : `?${new URLSearchParams(defined.map(([key, value]) => [key, String(value)]))}`;
}

export const calls = {
  pbis: () => api.get<Get<"/api/pbis">>("/api/pbis"),
  pbi: (repository: string, id: string) =>
    api.get<Get<"/api/pbi">>(`/api/pbi${query({ repository, id })}`),
  runs: (repository: string, pbi: string) =>
    api.get<Get<"/api/runs">>(`/api/runs${query({ repository, pbi })}`),
  journal: (filter: { repository?: string; pbi?: string; limit?: number } = {}) =>
    api.get<Get<"/api/loop/journal">>(`/api/loop/journal${query(filter)}`),
  present: () => api.get<Get<"/api/loop/present">>("/api/loop/present"),
  runOnce: () => api.post<Post<"/api/loop/run-once">>("/api/loop/run-once"),
  sessions: () => api.get<Get<"/api/sessions">>("/api/sessions"),
  folders: () => api.get<Get<"/api/folders">>("/api/folders"),
  machine: () => api.get<Get<"/api/machine">>("/api/machine"),
  /** Kills a live session: a loop run's, which the loop then holds on, or one a person opened (#577). */
  stopRun: (runId: string) => api.post<undefined>(`/api/runs/${runId}/stop`),
};

/** Where `asciinema-player` loads a run's recording from, or null when its session never wrote one. */
export function recordingUrl(run: RunRow): string | null {
  return run.recorded ? `/api/runs/${run.runId}/recording` : null;
}

/**
 * #697. Where it loads a run's recording from when the run was read from the issue's journey comment:
 * the copy on the repository's `aio/evidence` branch, at the path the comment names, read from the
 * vendor by the server and served as the local one is.
 */
export function evidenceUrl(repository: string, path: string): string {
  return `/api/pbi/recording${query({ repository, path })}`;
}

/** The output the engine kept of the script a `ran` line is about (#638), or null for every other line. */
export function scriptOutputUrl(line: JournalLine): string | null {
  return line.output ? `/api/loop/journal/${line.id}/output` : null;
}

/**
 * The WebSocket of a live session, same-origin. Frames from the server are binary, the bytes the
 * session printed; a binary frame sent is keystrokes, and a text frame `{"columns": n, "rows": n}`
 * resizes it. Closing the socket detaches and leaves the run running.
 */
export function attachUrl(session: Pick<LiveSession, "runId">, at: Location = window.location): string {
  const scheme = at.protocol === "https:" ? "wss:" : "ws:";
  return `${scheme}//${at.host}/api/sessions/${session.runId}/attach`;
}

/**
 * A loop run's session that has written nothing for longer than `Runs:StaleAfter` (#546): the Loop
 * screen and the rail count it as needing a person. A terminal a person opened is theirs to leave
 * quiet, so a manual session never is.
 */
export function stalled(session: LiveSession | null | undefined): boolean {
  return session != null && !session.manual && session.stale;
}

/** #679. Whether nuget.org has a newer aio: the last check's answer, and a check now. */
export const updates = {
  status: () => api.get<Get<"/api/updates">>("/api/updates"),
  check: () => api.post<Post<"/api/updates/check">>("/api/updates/check"),
  /** #703. Runs the update for the installed tool and, when it installs, restarts into it. */
  install: () => api.post<Post<"/api/updates/install">>("/api/updates/install"),
  /** The version that answers now: the restart is over when it is no longer the installed one. */
  version: () => api.get<{ version?: string | null }>("/api/version"),
};

/** #703. Whether the installed tool starts at login, as a LaunchAgent or a systemd user unit. */
export const startAtLogin = {
  read: () => api.get<Get<"/api/app/service">>("/api/app/service"),
  set: (registered: boolean) =>
    api.put<Schemas["StartAtLogin"]>("/api/app/service", { registered }),
};

/** #548. Stops the Server, from Settings: the one way to end the installed app short of the OS. */
export const consoleProcess = {
  stop: () => api.post<Post<"/api/console/stop">>("/api/console/stop"),
};
