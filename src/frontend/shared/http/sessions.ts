import { type QueryClient, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "./client";
import type { Post } from "./api";
import { queryKeys, pbiKeys } from "./queries";

/**
 * The writes of the manual sessions (#516): Terminal and Explore with agent, on a folder or on a
 * PBI. A module of their own, like `settings.ts`, so they load with the dialog and not with the
 * first screen (vite.config.ts).
 */

/** What a manual session opens: an agent with its model and command, or none for a terminal. */
export type SessionAgent = { provider: string; model: string | null; command: string | null };

/** Where it opens: a folder's checkout, or a PBI's branch and environment. */
export type SessionPlace =
  { kind: "folder"; path: string } | { kind: "pbi"; repository: string; id: string };

export const sessionCalls = {
  onFolder: (path: string, agent: SessionAgent | null) =>
    api.post<Post<"/api/folders/sessions">>("/api/folders/sessions", { path, ...agent }),
  onPbi: (repository: string, id: string, agent: SessionAgent | null) =>
    api.post<Post<"/api/pbi/sessions">>("/api/pbi/sessions", { repository, id, ...agent }),
};

/**
 * Opens a session and answers its run id. The tray is asked again at once, so the new chip and
 * its terminal appear without waiting for the next poll; on a PBI its runs are too, so the
 * Journey shows the manual run.
 */
export async function openSession(
  client: QueryClient,
  place: SessionPlace,
  agent: SessionAgent | null,
): Promise<{ runId: string }> {
  const opened =
    place.kind === "folder"
      ? await sessionCalls.onFolder(place.path, agent)
      : await sessionCalls.onPbi(place.repository, place.id, agent);
  await Promise.all([
    client.invalidateQueries({ queryKey: queryKeys.sessions }),
    place.kind === "pbi"
      ? client.invalidateQueries({ queryKey: pbiKeys.all(place.repository, place.id) })
      : null,
  ]);
  return opened;
}

/** {@link openSession} for a form that submits it. */
export function useOpenSession() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ place, agent }: { place: SessionPlace; agent: SessionAgent | null }) =>
      openSession(client, place, agent),
  });
}
