import type { LoopFolder, PbiDetail } from "@/shared/http/api";

/**
 * What every tab of the PBI screen receives (#511), so a tab reads the PBI it is about without
 * reading the route again: the folder it is worked in, its repository at the vendor, and the PBI
 * with its pull request as the header last read them.
 */
export interface PbiTabProps {
  folder: LoopFolder;
  repository: string;
  id: string;
  detail: PbiDetail;
}
