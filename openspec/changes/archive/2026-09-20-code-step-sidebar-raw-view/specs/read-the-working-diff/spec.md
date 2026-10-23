## ADDED Requirements

### Requirement: The Code step lists files in a sidebar beside the patch, not as a vertical column

The Code step of the Change tab SHALL list changed files in a sidebar on the left (fixed width,
`flex: none`) and render the selected file's patch in a panel on the right, the same structure the
Review tab uses. Selecting a file in the sidebar SHALL change the patch panel's contents without
remounting the sidebar.

Below a width the panel SHALL stack the sidebar as a strip across the top with the patch below it,
the same fallback the Review tab applies. Both layouts SHALL keep the claims breadcrumb on each
file row in the sidebar.

**This is a layout change, not a data change.** The Code step already reads `useFolderDiff`,
`useAnnotations` and `usePatch`; the sidebar reads the same data and renders it in a different
structure. No new query is introduced by this requirement.

#### Scenario: A 25-file change reads as a sidebar

- **WHEN** the Code step opens on a worktree with 25 changed files
- **THEN** the file list is rendered in a sidebar on the left, and the first file's patch is in a
  panel on the right, without any other file's patch pushing it down

#### Scenario: Selecting a file changes the panel without remounting the sidebar

- **WHEN** the reader clicks a file in the sidebar
- **THEN** the patch panel updates to that file's patch, and the sidebar's scroll position and
  selection highlight do not reset

#### Scenario: Narrow width stacks the sidebar as a strip

- **WHEN** the panel's width is below the breakpoint
- **THEN** the file list is rendered as a strip across the top with its own scroll, and the patch
  panel takes the full width below it

#### Scenario: Claims stay on every sidebar row

- **WHEN** a file was annotated with `req: 1, 2` in `tasks.md`
- **THEN** its row in the sidebar carries the claims breadcrumb, as it does today
