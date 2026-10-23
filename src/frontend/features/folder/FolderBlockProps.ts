import type { FolderPage, FolderStates, InstallState } from "@/shared/http/folder";
import type { LoopCheck } from "@/shared/http/settings";

/** The loop's verdict as the page polls it; undefined while the folder has no loop file. */
export type Check = { data?: LoopCheck; isPending: boolean; error: Error | null } | undefined;

/**
 * What every block of the folder page receives (#665), read once by the screen so no block asks the
 * server twice: the page, the loop's verdict, the harness install and the states its repository
 * already has at the vendor. A block reads the fields it needs and ignores the rest.
 */
export interface FolderBlockProps {
  page: FolderPage;
  check: Check;
  install: InstallState | undefined;
  states: { data?: FolderStates; error: Error | null };
}

export const loopFile = ".aio/loop.yaml";
export const gateScript = ".aio/gate";
