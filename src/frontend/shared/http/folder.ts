import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "./client";
import type { Schemas } from "./api";

/**
 * The reads of the folder page (#565). A module of its own, like `settings.ts`, so it loads with the
 * page behind its `lazy()` route and not with the rail. The switch is #564's `useSwitchEngine` in
 * `queries.ts`.
 */

export type FolderPage = Schemas["FolderPage"];
export type FolderHarnessFile = Schemas["FolderHarnessFile"];
export type FolderStates = Schemas["FolderStates"];
export type InstallState = Schemas["InstallState"];
export type Installation = Schemas["Installation"];

/**
 * How often the page reads the checkout again. The server reads `.aio/loop.yaml` from disk on
 * each request, so a file broken in an editor shows within this many milliseconds, with no watcher.
 */
export const folderPoll = 2_000;

export const folderCalls = {
  page: (id: string) => api.get<FolderPage>(`/api/folders/${id}`),
  states: (id: string) => api.get<FolderStates>(`/api/folders/${id}/states`),
  install: (id: string) => api.get<InstallState>(`/api/folders/${id}/harness/install`),
  installHarness: (id: string) => api.post<Installation>(`/api/folders/${id}/harness/install`),
};

/** The folder as its checkout is now, polled. */
export function useFolderPage(id: string) {
  return useQuery({
    queryKey: ["folders", id],
    queryFn: () => folderCalls.page(id),
    refetchInterval: folderPoll,
  });
}

/** The states the vendor already has, asked once per visit: a label rarely appears by hand. */
export function useFolderStates(id: string) {
  return useQuery({ queryKey: ["folders", id, "states"], queryFn: () => folderCalls.states(id) });
}

/**
 * How often the page asks the vendor whether the install pull request is still open (#566). Slower
 * than the checkout's poll because each read is a call to GitHub; a merge shows within this long.
 */
export const installPoll = 15_000;

/** The install pull request still open for the folder's repository, which the page links. */
export function useHarnessInstall(id: string) {
  return useQuery({
    queryKey: ["folders", id, "install"],
    queryFn: () => folderCalls.install(id),
    refetchInterval: installPoll,
  });
}

/** *Install harness* and *Reinstall*: one pull request from `harness/install`, and the states. */
export function useInstallHarness(id: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => folderCalls.installHarness(id),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ["folders", id, "install"] });
      void client.invalidateQueries({ queryKey: ["folders", id, "states"] });
    },
  });
}

export type StateEnvironments = Schemas["ReadStateEnvironmentsResponse"];

/**
 * #612. Where each agent state of the folder's repository runs, as somebody chose it; a state left
 * out runs in the machine's default environment.
 */
export function useStateEnvironments(repository: string | null) {
  return useQuery({
    queryKey: ["runs", "environments", repository],
    queryFn: () =>
      api.get<StateEnvironments>(
        `/api/runs/environments?${new URLSearchParams({ repository: repository ?? "" })}`,
      ),
    enabled: repository !== null,
  });
}

/** #612. Chooses the environment of one state; the next run of that state is placed there. */
export function useSetStateEnvironment() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (choice: { repository: string; state: string; environment: string }) =>
      api.put<StateEnvironments>("/api/runs/environments", choice),
    onSuccess: (answer, choice) =>
      client.setQueryData(["runs", "environments", choice.repository], answer),
  });
}
