## 1. Sidebar layout in CodeStep

- [x] 1.1 Replace the vertical file list in `CodeStep.tsx` with the sidebar+panel structure from `DiffTab.tsx`: file list on the left (240px, `flex: none`), patch panel on the right (`flex: 1`). Use `useNarrowContainer(660)` to stack as a strip across the top at narrow widths.
      req: read-the-working-diff/1
      files: src/frontend/features/folders/CodeStep.tsx
- [x] 1.2 Move the claims breadcrumb (`Claim` component) and the per-file markers (`+N −M`, binary, new, moved) into the sidebar rows, identical to how `DiffTab` renders them.
      req: read-the-working-diff/1
      files: src/frontend/features/folders/CodeStep.tsx
- [x] 1.3 Move the labelled summary (`{labelled} of {total} files say what they are for`) and the `Show N more files` control to the sidebar's footer or below the file list.
      req: read-the-working-diff/1
      files: src/frontend/features/folders/CodeStep.tsx
- [x] 1.4 Keep the untouched claims section below the sidebar, unchanged in content and position.
      req: read-the-working-diff/1
      files: src/frontend/features/folders/CodeStep.tsx
- [x] 1.5 Verify that selecting a file in the sidebar changes the patch panel without remounting the sidebar (the `chosen` state follows the list the way `DiffTab` does).
      req: read-the-working-diff/1
      files: src/frontend/features/folders/CodeStep.tsx

## 2. Raw view toggle

- [x] 2.1 Add a "Raw" toggle (using the `Toggle` from `kit.tsx` or a `Button` variant, whichever matches the existing pattern) in the patch panel's header, next to the file path. Default is off (patch view).
      req: review-a-change-in-one-tab/1
      files: src/frontend/features/folders/CodeStep.tsx
- [x] 2.2 When the toggle is on and the selected file is not binary, fetch the file's full text via `useFileContents(folderId, open, worktree)`.
      req: review-a-change-in-one-tab/1
      files: src/frontend/features/folders/CodeStep.tsx
- [x] 2.3 Build a `RawView` component (in `PatchView.tsx` beside `PatchView`, or in a new `RawView.tsx` — match the existing file organisation) that renders the full file text line-by-line, with each line's change status derived from the patch's `DiffLine` array:
  - A line whose `newLine` matches but has no corresponding `oldLine` → added (green tint).
  - A line whose `oldLine` matches but has no corresponding `newLine` → removed (red tint).
  - Otherwise → context (untinted).
  - Use the same `color-mix(in srgb, var(--pc-success-100) 60%, transparent)` and `--pc-error-100` tints `PatchView` already uses.
      req: review-a-change-in-one-tab/1
      files: src/frontend/features/folders/PatchView.tsx
- [x] 2.4 When the selected file changes, reset the toggle to off (patch view).
      req: review-a-change-in-one-tab/1
      files: src/frontend/features/folders/CodeStep.tsx
- [x] 2.5 When the selected file is binary, do not offer the toggle and do not fetch file contents.
      req: review-a-change-in-one-tab/1
      files: src/frontend/features/folders/CodeStep.tsx
- [x] 2.6 When `FileContents.truncated` is true on the raw view, show the same kind of sentence `PatchView` shows for `patch.data?.truncated` — "this file is too long to show whole."
      req: review-a-change-in-one-tab/1
      files: src/frontend/features/folders/PatchView.tsx

## 3. i18n keys

- [x] 3.1 Add keys in `en.ts` under the `codeStep` block: `codeStep.raw` ("Raw"), `codeStep.patch` ("Patch"), `codeStep.rawTruncated` (a sentence saying the raw view stops short of the end of the file).
      req: review-a-change-in-one-tab/1
      files: src/frontend/shared/i18n/en.ts

## 4. Verification

- [x] 4.1 Run `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj` and confirm it is green.
- [x] 4.2 Open the Code step on a worktree with 15+ changed files. Confirm the sidebar lists all files, selecting one shows its patch on the right, and the sidebar scroll does not reset.
- [x] 4.3 At a narrow width (below 660px), confirm the sidebar stacks as a strip across the top.
      req: read-the-working-diff/1
      files: docs/design/current/change-code-390.png, docs/design/current/change-code-390-dark.png
- [x] 4.4 Select a text file, toggle Raw, confirm the full file renders with added/removed lines tinted. Toggle back to Patch, confirm hunks return.
- [x] 4.5 Select a binary file, confirm the toggle is not offered and "binary file" is shown.
- [x] 4.6 Switch from file A (Raw on) to file B, confirm file B opens in Patch view.
- [x] 4.7 Check both light and dark themes — no invented colour values, no hex literals.
