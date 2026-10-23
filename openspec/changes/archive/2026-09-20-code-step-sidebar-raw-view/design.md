## Context

The Code step of the Change tab (`CodeStep.tsx`) currently renders a vertical list of diff files
with each file's patch inline below its own row. This layout was adequate when a change touched
4–5 files, but a real change touches 15–25, and the vertical column becomes an unscrollable list
where every open patch pushes the rest off-screen.

The Review tab (`DiffTab.tsx`) solved the same problem in #26: a sidebar on the left (240px,
`flex: none`), the patch panel on the right, and a breakpoint (`useNarrowContainer(660)`) that
stacks them as a strip across the top at narrow widths. The Code step does not need to invent
this; it needs to adopt it.

A second gap: `PatchView` shows only hunks — the regions that changed, with 3 lines of context
on each side — and a hunk header. To see a hunk in its surroundings, a reader has to leave the
app. A "raw" view of the whole file, with added and removed lines tinted, closes that gap. The
data already exists: `GET /api/folders/{id}/file` returns the full text, and `GET /api/folders/{id}/patch`
returns line numbers (`oldLine`, `newLine`) that say which lines are added, removed, or context.

## Goals / Non-Goals

**Goals:**
- Code step matches the sidebar layout the Review tab already uses.
- A toggle switches the selected file's panel between patch view (hunks) and raw view (whole file
  with change tints).
- Binary files do not offer the raw toggle, the same way they say "binary file" instead of showing
  hunks.
- No new endpoints, no new World seam, no new migrations.

**Non-Goals:**
- No editing. The raw view is read-only.
- No tree browser. The sidebar lists diff files, not the filesystem.
- No change to `DiffTab` (the Review tab). It already has the sidebar; this change does not touch it.
- No change to the patch endpoint or the file endpoint. Both already exist and return what is
  needed.
- No syntax highlighting. The raw view is mono text with change tints, not a code editor.

## Decisions

### 1. Reuse `DiffTab`'s layout pattern, not its code

CodeStep adopts the same sidebar+panel structure and the same `useNarrowContainer(660)` breakpoint
that `DiffTab` uses. It does not extend `DiffTab` or share a component between the two: they are
different steps with different headers (claims breadcrumb, labelled count, untouched claims), and
a shared component would force a union of two surfaces that have no reason to grow together. The
precedent in this repo is that `PatchView` was lifted out of `DiffTab` for the same reason — to
share rendering, not structure.

**Alternative considered:** extract a shared `FileSidebar` component for both DiffTab and CodeStep.
Rejected because DiffTab's sidebar carries different metadata (no claims, no labelled count) and
the two have different selection behaviour (DiffTab always shows all files; CodeStep has
`Show N more files`).

### 2. Raw view built from two existing endpoints, no new endpoint

The raw view fetches the file's full text from `GET /api/folders/{id}/file` (already exposed as
`useFileContents` in `queries.ts`) and the patch from `GET /api/folders/{id}/patch` (already
exposed as `usePatch`). To tint each line of the raw file:

1. Build a set of line ranges from the patch's hunks. Each `DiffLine` in the patch carries
   `oldLine` and `newLine` — context lines have both, added lines have only `newLine`, removed
   lines have only `oldLine`.
2. For each line in the raw file (by 1-based index), check whether it falls within an added range
   (it has a `newLine` but no `oldLine` in that hunk) or a removed range (it has an `oldLine` but
   no `newLine`).
3. Tint the line background using the same semantic colour families `PatchView` already uses:
   `color-mix(in srgb, var(--pc-success-100) 60%, transparent)` for added, same with
   `--pc-error-100` for removed, transparent for context.

**Alternative considered:** add a new endpoint `GET /api/folders/{id}/file-with-changes` that
returns the full file with per-line status. Rejected because the data already exists on the client
from the patch; a new endpoint would duplicate what `usePatch` already returns, and it would cross
a module boundary that does not need crossing.

**Alternative considered:** render the raw view using only the patch hunks (concatenate context +
changes in order). Rejected because hunks are truncated at `MaxPatchLines` and don't represent the
whole file — the raw view exists precisely to show what the hunks left out.

### 3. The toggle is per-file, not persistent

The raw/patch toggle resets to "patch" when the selected file changes. A file you just opened
should show its hunks first — that is what the step is for — and raw is the alternative you switch
to when the hunks lack context. Making the toggle persistent across files would save one click but
would also make it invisible: a reader who switched to raw on file A and forgot would read file B
as "all context" and miss what changed.

### 4. Claims and labelled count move to the sidebar footer

The summary that sits today above the file list (`{labelled} of {total} files say what they are
for`) and the `Show N more files` control move to the sidebar's footer or below the file list. They
are metadata about the list, not about the patch, so they belong with the list. The untouched
claims section stays below the sidebar, as it is today — it is a separate concern that does not
move with the selected file.

## Risks / Trade-offs

- **Raw view line mapping can be wrong if the file changed between the diff fetch and the file
  fetch.** The diff is read at request time and the file contents are read at request time; if the
  agent is writing while the reader is reading, the line numbers can drift. → Mitigation: the raw
  view is a reading aid, not a source of truth. If a line tint does not match the actual content,
  the patch view is still there and still correct. The raw view's header can name the file's line
  count and the patch's line count, and if they differ by more than the hunks imply, show a
  warning. For now, do not over-engineer: simply fetch both on the same render and accept the race.

- **Large files in raw view.** A 5000-line generated file would render 5000 rows. → Mitigation:
  the file endpoint already truncates (`FileContents.truncated`) and the raw view should honour
  that the same way it honours `PatchResponse.truncated` — with a sentence saying the text stops
  short of the end.

- **Two network requests instead of one.** Today the Code step fetches the diff list and one patch
  per selected file. The raw view adds one more request (`useFileContents`) only when the toggle is
  on. → Mitigation: the toggle is off by default, so the extra request only fires when the reader
  explicitly asks for the raw view.

- **Sidebar width on a worktree panel.** The Change tab's panel is inside the worktree's content
  area, which is narrower than a full-screen Review tab. 240px for the sidebar plus 420px for the
  patch = 660px, which is the same breakpoint `DiffTab` uses. If the Change tab's panel is narrower
  than 660px in practice, the layout will stack (list strip on top, patch below), which is the
  same fallback and is acceptable.
