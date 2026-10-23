## Why

The **Code** step of the Change tab lists diff files in a vertical column and renders each file's
patch **below its own row**, pushing every other file down. A 25-file change is a long scroll with
no overview, and the only way to see what else changed is to scroll past the patch of whatever is
open. The **Review** tab (`DiffTab`) already solved this: a sidebar on the left, the patch on the
right, and a breakpoint that stacks them at narrow widths. The Code step does not need to invent
this — it needs to adopt it.

Beyond layout, there is no way to see **the whole file** with its changes marked. `PatchView`
shows hunks — the regions that changed, with 3 lines of context — and a hunk header
(`@@ -34,7 +34,9 @@`). To understand a hunk in its surroundings a reader has to leave the app for
a terminal or an editor. A "raw" view that shows the entire file with added and removed lines
tinted — the way every diff viewer in an editor does — closes that gap without a new endpoint,
because `GET /api/folders/{id}/file` already returns the full text.

## What Changes

- **CodeStep adopts the sidebar layout** that `DiffTab` already uses: file list on the left (240px,
  `flex: none`), patch panel on the right, `useNarrowContainer(660)` to stack them as a strip
  across the top at narrow widths. The vertical list-with-inline-patch is replaced.
- **A "Raw" toggle** on the patch panel switches between two views of the selected file:
  - **Patch** (default, unchanged): hunks with line gutters and tinted backgrounds.
  - **Raw**: the full file contents from `GET /api/folders/{id}/file`, with each line tinted by its
    change status — added, removed, or context — derived by cross-referencing the patch's line
    numbers against the file's line array.
- **Binary files do not offer the toggle**, the same way `PatchView` today says "binary file"
  rather than showing hunks.
- **The claims breadcrumb** (`req: 1, 2`) stays on each file row in the sidebar, unchanged.
- **`Show N more files`** and the labelled/untouched summary move to the sidebar or its footer,
  since they were part of the vertical list.

## Capabilities

### New Capabilities

None. The raw view composes two existing endpoints (`diff` + `file`) into a new screen shape; it
does not introduce a new capability the spec catalogue does not already cover.

### Modified Capabilities

- `read-the-working-diff`: adds a requirement for the Code step to present the file list as a
  sidebar beside the patch rather than a vertical column, matching the layout the Review tab
  already uses. The delta is narrow: the existing requirement covers what the patch endpoint
  returns, and this adds what the Code step's layout SHALL be.
- `review-a-change-in-one-tab`: adds a requirement for a raw view of the selected file alongside
  the patch view. The step panel already draws its own skeleton while pending; this adds the
  second view mode the panel SHALL offer.

## Impact

- `src/frontend/features/folders/CodeStep.tsx` — rewritten layout, from vertical column to
  sidebar+panel. The core data flow (`useFolderDiff`, `useAnnotations`, `usePatch`) is unchanged;
  the `useFileContents` hook joins it for the raw view.
- `src/frontend/features/folders/PatchView.tsx` — unchanged for the patch view. A new `RawView`
  component (in the same file or beside it, whichever matches the existing pattern) renders the
  full file with change statuses derived from the patch.
- `src/frontend/shared/http/queries.ts` — no new query; `useFileContents` already exists.
- `src/frontend/shared/i18n/en.ts` — new keys for the raw toggle label and annotations.
- `src/frontend/shared/ui/useNarrowContainer.ts` — already exists, reused.
- No backend changes, no new endpoints, no migrations, no World seam.
