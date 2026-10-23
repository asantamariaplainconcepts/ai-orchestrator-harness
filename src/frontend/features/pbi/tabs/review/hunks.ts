/**
 * GitHub's patch text as hunks, transplanted from the first tree's `toPatchResponse` (#419), so the
 * Review tab draws a file the way that diff view did (#513).
 *
 * GitHub sends the hunks only, with no `diff --git` header, so the parse starts at the first `@@`.
 * A `\ No newline at end of file` marker describes the line before it and is not a line.
 */

export type DiffLineKind = "added" | "removed" | "context";

export interface DiffLine {
  kind: DiffLineKind;
  oldLine: number | null;
  newLine: number | null;
  text: string;
}

export interface Hunk {
  header: string;
  lines: DiffLine[];
}

export function hunksOf(patch: string | null): Hunk[] {
  const hunks: Hunk[] = [];
  let current: Hunk | null = null;
  let oldLine = 0;
  let newLine = 0;

  for (const raw of (patch ?? "").split("\n")) {
    const header = /^@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@/.exec(raw);
    if (header) {
      oldLine = Number(header[1]);
      newLine = Number(header[2]);
      current = { header: raw, lines: [] };
      hunks.push(current);
      continue;
    }
    if (!current || raw.length === 0 || raw.startsWith("\\")) continue;

    const text = raw.slice(1);
    if (raw[0] === "+") {
      current.lines.push({ kind: "added", oldLine: null, newLine: newLine++, text });
    } else if (raw[0] === "-") {
      current.lines.push({ kind: "removed", oldLine: oldLine++, newLine: null, text });
    } else {
      current.lines.push({ kind: "context", oldLine: oldLine++, newLine: newLine++, text });
    }
  }

  return hunks;
}
