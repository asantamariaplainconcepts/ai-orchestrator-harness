import { lazy, type ComponentType, type LazyExoticComponent } from "react";
import type { TranslationKey } from "@/shared/i18n";
import type { PbiTabProps } from "./PbiTabProps";

type TabModule = Promise<{ default: ComponentType<PbiTabProps> }>;

/**
 * The PBI's five tabs, in the order of DESIGN.md#the-screens (#511). Each is one lazy file
 * under `tabs/`, so the issue that fills a tab (#512 Issue and Proposal, #513 Review, #514 Files)
 * changes its own file and nothing here. Which tab is the one to look at is the loop's to say: the
 * PBI's state names it with `tab:` in `.aio/loop.yaml` (#595), matched against `path`.
 *
 * `load` is the import behind `Component`, so the screen fetches the open tab's chunk while it still
 * waits for the PBI rather than after it (#617). `warm` marks the tabs that read from the vendor, whose
 * reads and chunks the screen asks for in the background while another tab is open (#627). Files reads
 * the local checkout and answers fast, so it waits for its click.
 */
export const pbiTabs: readonly {
  path: string;
  label: TranslationKey;
  warm: boolean;
  load: () => TabModule;
  Component: LazyExoticComponent<ComponentType<PbiTabProps>>;
}[] = [
  tab("journey", "tab.journey", false, () => import("./tabs/JourneyTab")),
  tab("issue", "tab.issue", true, () => import("./tabs/IssueTab")),
  tab("proposal", "tab.proposal", true, () => import("./tabs/ProposalTab")),
  tab("review", "tab.review", true, () => import("./tabs/ReviewTab")),
  tab("files", "tab.files", false, () => import("./tabs/FilesTab")),
];

function tab(path: string, label: TranslationKey, warm: boolean, load: () => TabModule) {
  return { path, label, warm, load, Component: lazy(load) };
}
