import { useMutation, useQueries, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "./client";
import { calls, type Get, type Post, type Schemas } from "./api";
import { queryKeys } from "./queries";

/**
 * The reads and writes of the PBIs screen and Settings (#515). A module of their own rather than
 * the ends of `api.ts` and `queries.ts`, which the rail loads before anything is drawn: these load
 * with the two screens, behind their `lazy()` routes, and the first screen stays inside its budget
 * (vite.config.ts).
 */

export type LoopCheck = Schemas["ValidateLoopResponse"];
export type MachineTool = Schemas["Tool"];
export type MachineRequirement = Schemas["Requirement"];
export type DirectoryListing = Schemas["DirectoryListing"];

export const settingsCalls = {
  /** The latest closed PBIs of every folder, which only the PBIs screen's Done filter shows. */
  donePbis: () => api.get<Get<"/api/pbis/done">>("/api/pbis/done"),
  tools: () => api.get<Get<"/api/machine/tools">>("/api/machine/tools"),
  /** Checks a loop's text against the rules the engine follows; nothing is read from disk. */
  checkLoop: (yaml: string) =>
    api.post<Post<"/api/loop/validate">>("/api/loop/validate", { yaml }),
  preferences: () => api.get<Get<"/api/preferences">>("/api/preferences"),
  setEditor: (editor: string) =>
    api.put<Schemas["SavePreferencesResponse"]>("/api/preferences", { editor }),
  /** #680. Stops the requirements screen opening on start. */
  setRequirementsSeen: () =>
    api.put<Schemas["SavePreferencesResponse"]>("/api/preferences", { requirementsSeen: true }),
  addFolder: (path: string) => api.post<Schemas["AddFolderResponse"]>("/api/folders", { path }),
  /** #692. The subdirectories of a directory on this machine; with no path, of the home directory. */
  directories: (path: string | null) =>
    api.get<DirectoryListing>(
      path ? `/api/directories?path=${encodeURIComponent(path)}` : "/api/directories",
    ),
  removeFolder: (id: string) => api.delete<void>(`/api/folders/${id}`),
  setDefaults: (id: string, agent: string, model: string | null) =>
    api.put<Schemas["SetFolderDefaultsResponse"]>(`/api/folders/${id}/defaults`, {
      agent,
      model,
    }),
  /** Opens a file of the folder, relative to it, in the editor Settings names. */
  openInEditor: (id: string, file: string | null) =>
    api.post<Schemas["OpenInEditorResponse"]>(`/api/folders/${id}/open-in-editor`, { file }),
  /** #562. The engine's interval and each folder's switch, as the next pass follows them. */
  engineSettings: () => api.get<Get<"/api/loop/settings">>("/api/loop/settings"),
  setInterval: (intervalSeconds: number) =>
    api.put<Schemas["ReadEngineSettingsResponse"]>("/api/loop/settings", { intervalSeconds }),
  /** #562. How long a live run may be silent before it reads as stale. */
  runsSettings: () => api.get<Get<"/api/runs/settings">>("/api/runs/settings"),
  setStaleAfter: (staleAfterSeconds: number) =>
    api.put<Schemas["ReadRunsSettingsResponse"]>("/api/runs/settings", { staleAfterSeconds }),
  /** #578. What the agents may do without asking, read when each run or session starts. */
  setPermissionMode: (permissionMode: PermissionMode) =>
    api.put<Schemas["ReadRunsSettingsResponse"]>("/api/runs/settings", { permissionMode }),
};

/** #578. The modes as the server spells them, in the order Settings lists them. */
export const permissionModes = ["allow-list", "auto", "yolo"] as const;
export type PermissionMode = (typeof permissionModes)[number];

/** The closed PBIs, read only while the Done or All filter asks for them. */
export function useDonePbis(enabled: boolean) {
  return useQuery({ queryKey: ["pbis", "done"], queryFn: settingsCalls.donePbis, enabled });
}

export function useMachine() {
  return useQuery({ queryKey: ["machine"], queryFn: calls.machine });
}

/** Asked of every adapter on the server, so a CLI installed a minute ago shows on the next visit. */
export function useTools() {
  return useQuery({ queryKey: ["machine", "tools"], queryFn: settingsCalls.tools });
}

export function usePreferences() {
  return useQuery({ queryKey: ["preferences"], queryFn: settingsCalls.preferences });
}

/**
 * Each folder's loop checked against the engine's rules, keyed by its text: two folders with the
 * same loop ask once, and an edited file is a new question.
 */
export function useLoopChecks(yamls: readonly string[]) {
  return useQueries({
    queries: yamls.map((yaml) => ({
      queryKey: ["loop", "check", yaml],
      queryFn: () => settingsCalls.checkLoop(yaml),
      staleTime: Infinity,
    })),
  });
}

export function useAddFolder() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: settingsCalls.addFolder,
    onSuccess: () => client.invalidateQueries(),
  });
}

/**
 * #692. One directory of the add folder dialog's browser. Not retried: a 403 or a 404 is the
 * answer, and the dialog shows it as the server wrote it.
 */
export function useDirectories(path: string | null) {
  return useQuery({
    queryKey: ["directories", path ?? "~"],
    queryFn: () => settingsCalls.directories(path),
    retry: false,
  });
}

export function useRemoveFolder() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: settingsCalls.removeFolder,
    onSuccess: () => client.invalidateQueries(),
  });
}

export function useSetFolderDefaults() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, agent, model }: { id: string; agent: string; model: string | null }) =>
      settingsCalls.setDefaults(id, agent, model),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.folders }),
  });
}

export function useSetEditor() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: settingsCalls.setEditor,
    onSuccess: () => client.invalidateQueries({ queryKey: ["preferences"] }),
  });
}

/** #680. Records that the requirements screen was seen with every required CLI present. */
export function useSetRequirementsSeen() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: settingsCalls.setRequirementsSeen,
    onSuccess: () => client.invalidateQueries({ queryKey: ["preferences"] }),
  });
}

export function useOpenInEditor() {
  return useMutation({
    mutationFn: ({ id, file }: { id: string; file: string | null }) =>
      settingsCalls.openInEditor(id, file),
  });
}

/** #562. The engine's interval and switches; ["loop", ...] so a change refreshes the strip too. */
export function useEngineSettings() {
  return useQuery({ queryKey: ["loop", "settings"], queryFn: settingsCalls.engineSettings });
}

export function useRunsSettings() {
  return useQuery({ queryKey: ["runs", "settings"], queryFn: settingsCalls.runsSettings });
}

/**
 * Saves what changed of the two, one after the other, and refreshes every read once both are in:
 * staleness shows on the Loop screen, the tray and the Journey, and a refresh between the two saves
 * would put the value not yet saved back in its field.
 */
export function useSaveEngineSettings() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: async ({ interval, staleAfter }: { interval?: number; staleAfter?: number }) => {
      if (interval !== undefined) await settingsCalls.setInterval(interval);
      if (staleAfter !== undefined) await settingsCalls.setStaleAfter(staleAfter);
    },
    onSettled: () => client.invalidateQueries(),
  });
}

/** #578. Saves the permission mode; only Runs' settings read it, so only they are refreshed. */
export function useSetPermissionMode() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: settingsCalls.setPermissionMode,
    onSettled: () => client.invalidateQueries({ queryKey: ["runs", "settings"] }),
  });
}
