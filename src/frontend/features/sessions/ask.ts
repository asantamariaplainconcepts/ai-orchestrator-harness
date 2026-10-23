import { useSyncExternalStore } from "react";
import type { LoopFolder } from "@/shared/http/api";

/**
 * What a person asked to open by hand (DESIGN.md, "Sessions by hand", #516):
 * Terminal or Explore with agent, on a folder from the rail's `…` menu or on a PBI from its header.
 *
 * The two places that ask are far apart in the tree and the one that answers, `SessionsHost`,
 * lives in the shell, so the ask goes through this one-slot store instead of being threaded through
 * props. It is a few lines on purpose: the shell loads it on the first screen.
 */
export type SessionAsk = {
  kind: "terminal" | "explore";
  folder: LoopFolder;
  /** The PBI it opens on, in its branch and environment; absent for the folder's checkout. */
  pbi?: {
    repository: string;
    id: string;
    /** The current state's command with the PBI's number, offered as a suggestion. */
    suggestion: string | null;
  };
};

let current: SessionAsk | null = null;
const listeners = new Set<() => void>();

function set(next: SessionAsk | null) {
  current = next;
  listeners.forEach((listener) => listener());
}

export function askSession(ask: SessionAsk) {
  set(ask);
}

export function clearSessionAsk() {
  set(null);
}

export function useSessionAsk(): SessionAsk | null {
  return useSyncExternalStore(
    (listener) => {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    () => current,
  );
}
