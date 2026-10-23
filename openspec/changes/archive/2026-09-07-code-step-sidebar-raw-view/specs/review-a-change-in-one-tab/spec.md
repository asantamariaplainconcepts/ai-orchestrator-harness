## ADDED Requirements

### Requirement: The Code step offers a raw view of the selected file beside the patch view

The Code step's patch panel SHALL offer a toggle between **Patch** (the default, unchanged) and
**Raw** — the full file contents with each line tinted by its change status. The toggle SHALL be
per-file and SHALL reset to Patch when the selected file changes, so a reader who switched to Raw
on one file does not read the next file as "all context" without noticing.

**The raw view composes two existing endpoints** — `GET /api/folders/{id}/file` for the file text
and `GET /api/folders/{id}/patch` for the line statuses — and introduces no new endpoint, no new
query, and no new seam. The change status of each line SHALL be derived from the patch's line
numbers: a line with a `newLine` and no `oldLine` is added, a line with an `oldLine` and no
`newLine` is removed, and a line with both is context.

A binary file SHALL NOT offer the raw toggle, the same way the patch view says "binary file" rather
than showing hunks. A file whose text is truncated by the file endpoint SHALL say so — a preview
that quietly ends lies about what is in the file.

**The raw view does not edit.** It is read-only. It is not a tree browser: it shows the file that
is in the diff, not the filesystem.

#### Scenario: The toggle is Patch by default

- **WHEN** a text file is selected in the Code step
- **THEN** the panel shows its patch (hunks with gutters and tints), and the toggle reads "Raw"

#### Scenario: Switching to Raw shows the whole file with change tints

- **WHEN** the reader presses the Raw toggle on a selected text file
- **THEN** the panel shows the full file contents, with added lines tinted green, removed lines
  tinted red, and context lines untinted

#### Scenario: The toggle resets when the file changes

- **WHEN** the reader switches to Raw on file A, then selects file B in the sidebar
- **THEN** file B's panel opens in Patch view, not in Raw

#### Scenario: A binary file offers no raw toggle

- **WHEN** the selected file is binary
- **THEN** the panel says "binary file," the toggle is not offered, and no file contents are fetched

#### Scenario: A truncated file says so

- **WHEN** the file endpoint truncates the text
- **THEN** the raw view shows the text it has and says it stops short of the end, rather than ending
  quietly
