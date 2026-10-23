import {
  type QueryClient,
  queryOptions,
  useMutation,
  useQueries,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { useEffect, useRef } from "react";
import { api } from "./client";
import { calls, type JournalLine, type LiveSession, type PbiDetail } from "./api";
import type { components } from "./contracts";

/**
 * The reads the rail and the tray share (#509), one key each so every screen that later asks for
 * the same list reads the same cache.
 */
export const queryKeys = {
  pbis: ["pbis"] as const,
  folders: ["folders"] as const,
  sessions: ["sessions"] as const,
};

/** Every open PBI of every folder, read from the vendor on each request. */
export function usePbis() {
  return useQuery({ queryKey: queryKeys.pbis, queryFn: calls.pbis, refetchInterval: 30_000 });
}

export function useFolders() {
  return useQuery({ queryKey: queryKeys.folders, queryFn: calls.folders });
}

/**
 * The live sessions, polled: a run the loop starts appears in the tray without anybody asking,
 * and one that ends leaves it. Three seconds, because the list is read from memory on the server
 * and a chip that lingers after its run ended is a terminal that opens onto nothing.
 */
export function useSessions() {
  return useQuery({ queryKey: queryKeys.sessions, queryFn: calls.sessions, refetchInterval: 3_000 });
}

/**
 * #577. Ends a session from the tray, with the same call as *Stop run*. The chip goes at once
 * rather than on the next poll; a PBI's reads are asked again, so its Journey shows the run ended.
 */
export function useEndSession() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (session: LiveSession) => calls.stopRun(session.runId),
    onSuccess: (_, session) =>
      client.setQueryData<LiveSession[]>(queryKeys.sessions, (live) =>
        live?.filter((each) => each.runId !== session.runId),
      ),
    onSettled: (_, __, session) =>
      Promise.all([
        client.invalidateQueries({ queryKey: queryKeys.sessions }),
        session.repository !== null && session.pbi !== null
          ? client.invalidateQueries({ queryKey: pbiKeys.all(session.repository, session.pbi) })
          : null,
        client.invalidateQueries({ queryKey: queryKeys.pbis }),
      ]),
  });
}

// #510. The Loop screen's reads. Polled rather than pushed: the engine looks once a minute by
// default, so five seconds shows a pass within moments of it and costs two small reads.

/** The engine now: whether it runs, its last pass, its live runs and its latest decision. */
export function useLoopPresent() {
  return useQuery({
    queryKey: ["loop", "present"],
    queryFn: calls.present,
    refetchInterval: 5_000,
  });
}

/** The engine's journal across every folder, newest first. */
export function useLoopJournal(limit: number) {
  return useQuery({
    queryKey: ["loop", "journal", limit],
    queryFn: () => calls.journal({ limit }),
    refetchInterval: 5_000,
  });
}

// #511. The PBI screen: the PBI and its pull request, its runs, the journal lines about it and its
// folder's loop, each read from the module that owns it; and the three writes of its header.

export type LoopShape = components["schemas"]["LoopShape"];
export type LoopStateRow = components["schemas"]["LoopStateRow"];

export const pbiKeys = {
  all: (repository: string, id: string) => ["pbi", repository, id] as const,
  loop: (repository: string) => ["loop", "definition", repository] as const,
};

/** One PBI and its pull request, read from the vendor once; see `vendorRead` (#627). */
export function usePbi(repository: string | undefined, id: string) {
  return useQuery({ ...detailQuery(repository ?? "", id), enabled: Boolean(repository) });
}

function detailQuery(repository: string, id: string, fresh = false) {
  return queryOptions({
    queryKey: [...pbiKeys.all(repository, id), "detail"],
    queryFn: () => api.get<PbiDetail>(`/api/pbi?${pbiQuery(repository, id)}`, freshly(fresh)),
    ...vendorRead,
  });
}

/**
 * Every run of the PBI. Three seconds while one is live, so "wrote 4s ago" and a run that ends are
 * both seen; fifteen otherwise.
 */
export function usePbiRuns(repository: string | undefined, id: string) {
  return useQuery({
    queryKey: [...pbiKeys.all(repository ?? "", id), "runs"],
    queryFn: () => calls.runs(repository ?? "", id),
    enabled: Boolean(repository),
    refetchInterval: (query) =>
      (query.state.data ?? []).some((run) => run.endedAt === null) ? 3_000 : 15_000,
  });
}

/** The engine's lines about the PBI, newest first, as many as the journal answers at once. */
export function usePbiJournal(repository: string | undefined, id: string) {
  return useQuery({
    queryKey: [...pbiKeys.all(repository ?? "", id), "journal"],
    queryFn: () => calls.journal({ repository, pbi: id, limit: 500 }),
    enabled: Boolean(repository),
    refetchInterval: 15_000,
  });
}

function loopDefinition(repository: string) {
  return queryOptions({
    queryKey: pbiKeys.loop(repository),
    queryFn: () =>
      api.get<LoopShape>(`/api/loop/definition?${new URLSearchParams({ repository })}`),
  });
}

/** The folder's loop as the engine reads it: its states top to bottom. */
export function useLoopDefinition(repository: string | undefined) {
  return useQuery({ ...loopDefinition(repository ?? ""), enabled: Boolean(repository) });
}

/** #669. Each repository's loop for the Loop screen's rows, under the PBI screen's key for it. */
export function useLoopDefinitions(repositories: readonly string[]) {
  return useQueries({ queries: repositories.map((repository) => loopDefinition(repository)) });
}

export type PbiAction =
  | { kind: "release"; to?: string }
  | { kind: "move"; to: string }
  | { kind: "stop"; runId: string };

/**
 * The header's one button: release the hold, stop the live run, or move the PBI. Each settles once
 * the server has written it, and every read of the PBI is asked again, the rail's and the tray's too.
 */
export function usePbiAction(repository: string, id: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (action: PbiAction): Promise<unknown> => {
      switch (action.kind) {
        case "release":
          return api.post<JournalLine>("/api/pbi/release", {
            repository,
            id,
            to: action.to ?? null,
          });
        case "move":
          return api.post<JournalLine>("/api/pbi/move", { repository, id, to: action.to });
        case "stop":
          return calls.stopRun(action.runId);
      }
    },
    onSettled: () =>
      Promise.all([
        client.invalidateQueries({ queryKey: pbiKeys.all(repository, id) }),
        client.invalidateQueries({ queryKey: queryKeys.pbis }),
        client.invalidateQueries({ queryKey: queryKeys.sessions }),
      ]),
  });
}

// #514. The Files tab: the tree of the PBI's branch, one file of it, Save and Open in editor, each
// answered by Workspace. `asIs` reads the folder's checkout as it is, which the tab offers when the
// branch is gone. Not polled: an agent's writes show when the tab is opened again.

export type PbiFiles = components["schemas"]["PbiFiles"];
export type PbiFileText = components["schemas"]["ReadPbiFileResponse"];

export interface PbiFilesScope {
  folderId: string;
  pbi: string;
  asIs: boolean;
}

export const filesKeys = {
  tree: (scope: PbiFilesScope) => ["files", scope.folderId, scope.pbi, scope.asIs] as const,
  file: (scope: PbiFilesScope, path: string) => [...filesKeys.tree(scope), "file", path] as const,
};

function filesQuery(scope: PbiFilesScope, extra: Record<string, string> = {}): string {
  return new URLSearchParams({
    folderId: scope.folderId,
    pbi: scope.pbi,
    asIs: String(scope.asIs),
    ...extra,
  }).toString();
}

export function usePbiFiles(scope: PbiFilesScope, enabled = true) {
  return useQuery({
    queryKey: filesKeys.tree(scope),
    queryFn: () => api.get<PbiFiles>(`/api/files?${filesQuery(scope)}`),
    enabled,
  });
}

/** One file's text. Read once per choice: the draft on screen is the person's until it is saved. */
export function usePbiFile(scope: PbiFilesScope, path: string | null) {
  return useQuery({
    queryKey: filesKeys.file(scope, path ?? ""),
    queryFn: () =>
      api.get<PbiFileText>(`/api/files/content?${filesQuery(scope, { path: path ?? "" })}`),
    enabled: path !== null,
    staleTime: Infinity,
    refetchOnWindowFocus: false,
  });
}

/** Save: writes the file in the checkout that holds the branch; the tree it answers replaces the cached one. */
export function useSavePbiFile(scope: PbiFilesScope) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ path, text }: { path: string; text: string }) =>
      api.post<PbiFiles>("/api/files/content", { ...scope, path, text }),
    onSuccess: (tree, { path, text }) => {
      client.setQueryData(filesKeys.tree(scope), tree);
      client.setQueryData<PbiFileText>(filesKeys.file(scope, path), (file) =>
        file ? { ...file, text } : file,
      );
    },
  });
}

/**
 * Open in editor: the chosen file, or the checkout when none is chosen, with the editor Settings
 * names. The same route as Settings' (#515), with `pbi` so the file is opened in the checkout that
 * holds the PBI's branch.
 */
export function useOpenPbiFile(scope: PbiFilesScope) {
  return useMutation({
    mutationFn: (file: string | null) =>
      api.post<components["schemas"]["OpenInEditorResponse"]>(
        `/api/folders/${scope.folderId}/open-in-editor`,
        { file, pbi: scope.pbi, asIs: scope.asIs },
      ),
  });
}

// #513. The Review tab: one read of the pull request with its files, threads, checks and evidence;
// the thread writes, which ask the read again; and the two verdicts, which move the PBI too.

export type ReviewRead = components["schemas"]["ReviewRead"];
export type ReviewFile = components["schemas"]["ReviewFile"];
export type ReviewThreadRow = components["schemas"]["ReviewThreadRow"];
export type ReviewSubmitted = components["schemas"]["ReviewSubmitted"];

/** The review of one PBI, read from the vendor. Every write below asks it again. */
export function useReview(repository: string, id: string) {
  return useQuery(reviewQuery(repository, id));
}

function reviewQuery(repository: string, id: string, fresh = false) {
  return queryOptions({
    queryKey: [...pbiKeys.all(repository, id), "review"],
    queryFn: () =>
      api.get<ReviewRead>(`/api/pbi/review?${pbiQuery(repository, id)}`, freshly(fresh)),
    ...vendorRead,
  });
}

/** Where the browser reads an image of the PBI's comments through the server, which holds the credential. */
export function attachmentUrl(repository: string, id: string, url: string): string {
  return `/api/pbi/attachment?${new URLSearchParams({ repository, id, url })}`;
}

export type ReviewThreadWrite =
  | { kind: "comment"; path: string; line: number; body: string }
  | { kind: "reply"; commentId: string; body: string }
  | { kind: "resolve"; threadId: string };

/** A new thread on a line, a reply in one, or resolving one. Settles once the vendor read it back. */
export function useReviewThread(repository: string, id: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (write: ReviewThreadWrite): Promise<unknown> => {
      const { kind, ...rest } = write;
      return api.post<unknown>(`/api/pbi/review/${kind}`, { repository, id, ...rest });
    },
    onSettled: () =>
      client.invalidateQueries({ queryKey: [...pbiKeys.all(repository, id), "review"] }),
  });
}

export type ReviewVerdictWrite =
  | { kind: "approve"; body?: string }
  | { kind: "request-changes"; body: string };

/** Approve or Request changes: written at the vendor, then the loop moves; every read of the PBI is asked again. */
export function useReviewVerdict(repository: string, id: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (verdict: ReviewVerdictWrite) =>
      api.post<ReviewSubmitted>(`/api/pbi/review/${verdict.kind}`, {
        repository,
        id,
        body: verdict.body ?? null,
      }),
    onSettled: () =>
      Promise.all([
        client.invalidateQueries({ queryKey: pbiKeys.all(repository, id) }),
        client.invalidateQueries({ queryKey: queryKeys.pbis }),
      ]),
  });
}

// #512. The Issue and Proposal tabs: the issue as the vendor has it and the comment written on it;
// the OpenSpec change on the PBI's branch, a comment anchored to one of its lines, and the request
// for changes that sends the PBI back to spec-ready. Approving is the header's release.

export type IssuePage = components["schemas"]["IssuePage"];
export type IssueComment = components["schemas"]["IssueComment"];
export type ProposalPage = components["schemas"]["ProposalPage"];
export type ProposalPlan = components["schemas"]["ProposalPlan"];
export type ProposalFile = components["schemas"]["ProposalFile"];
export type ProposalComment = components["schemas"]["ProposalComment"];

function pbiQuery(repository: string, id: string, extra: Record<string, string> = {}): string {
  return new URLSearchParams({ repository, id, ...extra }).toString();
}

/** Where a file of the PBI's change is served from, at its pull request's head. */
export function proposalFileUrl(repository: string, id: string, path: string): string {
  return `/api/pbi/proposal/file?${pbiQuery(repository, id, { path })}`;
}

/** The issue, its comments and what it says about itself. */
export function useIssue(repository: string, id: string) {
  return useQuery(issueQuery(repository, id));
}

function issueQuery(repository: string, id: string, fresh = false) {
  return queryOptions({
    queryKey: [...pbiKeys.all(repository, id), "issue"],
    queryFn: () =>
      api.get<IssuePage>(`/api/pbi/issue?${pbiQuery(repository, id)}`, freshly(fresh)),
    ...vendorRead,
  });
}

/** An image dropped on the composer, as the server takes it. */
export interface DroppedImage {
  name: string;
  base64: string;
}

/** A comment on the issue, published at the vendor as the person. */
export function useIssueComment(repository: string, id: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (comment: { body: string; attachments: DroppedImage[] }) =>
      api.post<IssueComment>("/api/pbi/issue/comment", { repository, id, ...comment }),
    onSettled: () =>
      client.invalidateQueries({ queryKey: [...pbiKeys.all(repository, id), "issue"] }),
  });
}

/** The change on the PBI's branch with its threads. */
export function useProposal(repository: string, id: string) {
  return useQuery(proposalQuery(repository, id));
}

function proposalQuery(repository: string, id: string, fresh = false) {
  return queryOptions({
    queryKey: [...pbiKeys.all(repository, id), "proposal"],
    queryFn: () =>
      api.get<ProposalPage>(`/api/pbi/proposal?${pbiQuery(repository, id)}`, freshly(fresh)),
    ...vendorRead,
  });
}

/** A comment anchored to a line of the change: a review comment on the pull request. */
export function useProposalComment(repository: string, id: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (comment: { path: string; line: number; body: string }) =>
      api.post<ProposalComment>("/api/pbi/proposal/comment", { repository, id, ...comment }),
    onSettled: () =>
      client.invalidateQueries({ queryKey: [...pbiKeys.all(repository, id), "proposal"] }),
  });
}

/**
 * Request changes on the proposal: the review goes to the pull request and the PBI back to
 * spec-ready. The answer is the Review tab's: the review written, and the line the loop wrote or why
 * it did not move.
 */
export function useRequestProposalChanges(repository: string, id: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (body: string) =>
      api.post<ReviewSubmitted>("/api/pbi/proposal/request-changes", {
        repository,
        id,
        body,
      }),
    onSettled: () =>
      Promise.all([
        client.invalidateQueries({ queryKey: pbiKeys.all(repository, id) }),
        client.invalidateQueries({ queryKey: queryKeys.pbis }),
      ]),
  });
}

// #564. The Loop strip's one write: a folder's engine on or off. The folder page (#565) sends the
// same request.

export type EngineSwitched = components["schemas"]["SwitchEngineResponse"];

/**
 * Turns a folder's engine on or off. Every read under `["loop"]` is asked again once it is in, so
 * the switch, the strip's count and the journal's `switched` line come from the server, not from
 * what was pressed.
 */
export function useSwitchEngine() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ repository, on }: { repository: string; on: boolean }) =>
      api.put<EngineSwitched>("/api/loop/engine", { repository, on }),
    onSettled: () => client.invalidateQueries({ queryKey: ["loop"] }),
  });
}

// #611. The Issue tab's graph of an epic: its sub-issues as the vendor has them, and the journal of
// their repository, whose newest line per sub-issue says whether it waits and on whom.

export type IssueChild = components["schemas"]["IssueChild"];

/** The issue's sub-issues with their state, hold and whether closed. */
export function useIssueChildren(repository: string, id: string) {
  return useQuery(childrenQuery(repository, id));
}

function childrenQuery(repository: string, id: string, fresh = false) {
  return queryOptions({
    queryKey: [...pbiKeys.all(repository, id), "children"],
    queryFn: () =>
      api.get<IssueChild[]>(`/api/pbi/children?${pbiQuery(repository, id)}`, freshly(fresh)),
    ...vendorRead,
  });
}

/** The engine's lines about one repository, newest first: read only while `enabled`. */
export function useRepositoryJournal(repository: string, enabled: boolean) {
  return useQuery({
    queryKey: ["loop", "journal", "repository", repository],
    queryFn: () => calls.journal({ repository, limit: 500 }),
    enabled,
    refetchInterval: 15_000,
  });
}

// #617. The PBI screen asks for what its open tab reads as soon as the repository is known, beside
// `/api/pbi` and not after it: on 10 October the proposal of #597 waited 1.1 s for the PBI it does
// not need. The tab's own hooks then find the read in flight or done, under the same key.
//
// #627. Once the screen is up and the browser is idle, it asks for the other tabs' reads the same
// way, so a click on Issue, Proposal or Review paints from the cache instead of waiting 0.6 to 1.3 s
// for GitHub. A read that fails is kept as a failure without data, and the tab's hook asks again when
// it opens; nothing shows it before then.
//
// What these reads hold stays on screen once read: no polling, no refetch on focus. Entering the PBI
// again shows what is kept at once and reads it again behind it; the header's refresh button reads
// all of it again, past the server's ten-second copy too. The PBI's live reads (runs, journal, loop)
// keep their own polling.

/** The vendor reads of a PBI: kept until the screen or a write asks again, and half an hour unseen. */
const vendorRead = { staleTime: Infinity, gcTime: 30 * 60_000 } as const;

/** `Cache-Control: no-cache` makes the server skip its kept copy for this one read (#627). */
function freshly(fresh: boolean): RequestInit | undefined {
  return fresh ? { headers: { "Cache-Control": "no-cache" } } : undefined;
}

/** The last part of each vendor read's key, under `pbiKeys.all`. */
const vendorParts: readonly unknown[] = ["detail", "issue", "children", "proposal", "review"];

/**
 * Entering a PBI: every vendor read already kept is asked again in the background while the screen
 * shows the kept one. A read not kept yet, or in flight, is left alone.
 */
export function revalidatePbi(client: QueryClient, repository: string, id: string) {
  void client.refetchQueries({
    queryKey: pbiKeys.all(repository, id),
    type: "all",
    predicate: (query) =>
      query.queryKey.length === 4 &&
      vendorParts.includes(query.queryKey[3]) &&
      query.state.data !== undefined &&
      query.state.fetchStatus === "idle",
  });
}

/** The refresh button: every vendor read of the PBI, asked again past the server's copy. */
export function refreshPbi(client: QueryClient, repository: string, id: string): Promise<unknown> {
  const again = { staleTime: 0 } as const;
  return Promise.allSettled([
    client.fetchQuery({ ...detailQuery(repository, id, true), ...again }),
    client.fetchQuery({ ...issueQuery(repository, id, true), ...again }),
    client.fetchQuery({ ...childrenQuery(repository, id, true), ...again }),
    client.fetchQuery({ ...proposalQuery(repository, id, true), ...again }),
    client.fetchQuery({ ...reviewQuery(repository, id, true), ...again }),
  ]);
}

/** Asks for what the tab at `tab` reads from the vendor, into the cache its own hooks read. */
export function prefetchPbiTab(client: QueryClient, repository: string, id: string, tab: string) {
  switch (tab) {
    case "issue":
      void client.prefetchQuery(issueQuery(repository, id));
      void client.prefetchQuery(childrenQuery(repository, id));
      return;
    case "proposal":
      void client.prefetchQuery(proposalQuery(repository, id));
      return;
    case "review":
      void client.prefetchQuery(reviewQuery(repository, id));
      return;
  }
}

/** Starts the open tab's reads once the repository is known, without waiting for the PBI. */
export function usePbiTabPrefetch(repository: string | undefined, id: string, tab: string) {
  const client = useQueryClient();
  useEffect(() => {
    if (repository) prefetchPbiTab(client, repository, id, tab);
  }, [client, repository, id, tab]);
}

/**
 * #627. The header follows the loop without polling the vendor: the PBI's journal is read from this
 * host every fifteen seconds or faster, and a new line about the PBI (a move, a hold, a release, a
 * run that started or ended) asks `/api/pbi` again behind the screen. The first read of the journal
 * only sets the mark.
 */
export function usePbiFollowsJournal(repository: string | undefined, id: string) {
  const client = useQueryClient();
  const journal = usePbiJournal(repository, id);
  const newest = journal.data?.[0];
  const mark = journal.data ? (newest ? `${newest.at}\n${newest.kind}\n${newest.to ?? ""}` : "") : null;
  const seen = useRef<{ id: string; mark: string } | null>(null);
  useEffect(() => {
    if (!repository || mark === null) return;
    const before = seen.current;
    seen.current = { id, mark };
    if (before && before.id === id && before.mark !== mark)
      void client.invalidateQueries({ queryKey: [...pbiKeys.all(repository, id), "detail"], exact: true });
  }, [client, repository, id, mark]);
}

// #734. The Metrics screen of a folder: one read of the four blocks for a period and an epic. Not
// polled: a person reading numbers wants them to hold still, and the server keeps the vendor's
// part for ten seconds anyway.

export type Metrics = components["schemas"]["MetricsReport"];
export type MetricsRun = components["schemas"]["MetricsRun"];
export type MetricsHoldReason = components["schemas"]["MetricsHoldReason"];
export type MetricsNote = components["schemas"]["MetricsNote"];

/** The repository's metrics over the last `days`, or all of it when undefined, for one epic when named. */
export function useMetrics(repository: string, days: number | undefined, epic: string) {
  return useQuery({
    queryKey: ["loop", "metrics", repository, days ?? "all", epic],
    queryFn: () => {
      const query = new URLSearchParams({ repository });
      if (days !== undefined) query.set("days", String(days));
      if (epic) query.set("epic", epic);
      return api.get<Metrics>(`/api/loop/metrics?${query}`);
    },
    enabled: repository !== "",
    refetchOnWindowFocus: false,
  });
}
