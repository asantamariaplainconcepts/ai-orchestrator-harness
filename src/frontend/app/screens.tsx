import { Navigate, type RouteObject } from "react-router";

/**
 * The screens under the shell (#509): the four of DESIGN.md#the-screens and the PBI with its
 * five tabs. Each module is lazy, so the first screen's byte budget pays for none of them.
 *
 * The PBI is `pbis/:folderId/:pbiId/:tab`: under PBIs, because that is the list it is opened from,
 * and with the folder because an id is unique only within its repository. A PBI without a tab
 * opens on its Journey.
 */

export const screens: RouteObject[] = [
  { index: true, element: <Navigate to="/pbis" replace /> },
  {
    path: "pbis",
    lazy: async () => ({ Component: (await import("@/features/pbis/PbisScreen")).PbisScreen }),
  },
  {
    path: "loop",
    lazy: async () => ({
      Component: (await import("@/features/loop/LoopScreen")).LoopScreen,
    }),
  },
  {
    path: "settings",
    lazy: async () => ({
      Component: (await import("@/features/settings/SettingsScreen")).SettingsScreen,
    }),
  },
  {
    path: "requirements",
    // #680. What the machine needs, opened on first start and from Settings.
    lazy: async () => ({
      Component: (await import("@/features/requirements/RequirementsScreen")).RequirementsScreen,
    }),
  },
  {
    path: "folders/:folderId",
    // #565. How the folder is set up for the loop, opened from its name in the rail.
    lazy: async () => ({ Component: (await import("@/features/folder/FolderScreen")).FolderScreen }),
  },
  {
    path: "folders/:folderId/metrics",
    // #734. How the loop works the folder's repository, linked from the Loop screen.
    lazy: async () => ({ Component: (await import("@/features/metrics/MetricsScreen")).MetricsScreen }),
  },
  { path: "pbis/:folderId/:pbiId", element: <Navigate to="journey" replace /> },
  {
    path: "pbis/:folderId/:pbiId/:tab",
    // #511. The PBI's header and its tabs, each tab a lazy file of its own.
    lazy: async () => ({ Component: (await import("@/features/pbi/PbiScreen")).PbiScreen }),
  },
];
