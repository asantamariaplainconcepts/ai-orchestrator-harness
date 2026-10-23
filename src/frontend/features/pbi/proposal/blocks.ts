/**
 * A markdown file cut into the pieces the Proposal tab numbers and anchors comments to (#512): a
 * heading, a paragraph, one list item, a fenced block, a table. Each knows the lines it spans, so a
 * comment the vendor places on a line lands under the piece that holds it, and a comment written on
 * a piece is anchored to its first line.
 *
 * One list item is one piece rather than the whole list, because `tasks.md` is all list and a
 * comment on "1.3" has to say 1.3. Nesting is kept as an indent, not as a nested list.
 */
export interface Block {
  /** The first line, from 1. */
  line: number;
  /** The last line. */
  end: number;
  text: string;
  /** How far a list item is indented, in levels of two spaces. */
  indent: number;
}

const fence = /^\s{0,3}(```|~~~)/;
const heading = /^\s{0,3}#{1,6}\s/;
const item = /^(\s*)([-*+]|\d+[.)])\s/;

export function blocks(source: string): Block[] {
  const lines = source.replace(/\r\n?/g, "\n").split("\n");
  const out: Block[] = [];
  let current: Block | null = null;

  const flush = () => {
    if (current) out.push(current);
    current = null;
  };
  const at = (index: number): string => lines[index] ?? "";
  const start = (index: number, indent = 0): Block => ({ line: index + 1, end: index + 1, text: at(index), indent });

  for (let index = 0; index < lines.length; index++) {
    const line = at(index);

    const opened = fence.exec(line);
    if (opened) {
      flush();
      const block = start(index);
      while (++index < lines.length) {
        block.text += `\n${at(index)}`;
        block.end = index + 1;
        if (at(index).trimStart().startsWith(opened[1] ?? "```")) break;
      }
      out.push(block);
      continue;
    }

    if (line.trim() === "") {
      flush();
      continue;
    }

    if (heading.test(line)) {
      flush();
      out.push(start(index));
      continue;
    }

    const listed = item.exec(line);
    if (listed) {
      flush();
      const indent = Math.floor((listed[1] ?? "").length / 2);
      // The item is drawn on its own, so its own indentation would make it a code block.
      current = { ...start(index, indent), text: line.trimStart() };
      continue;
    }

    if (current) {
      const block: Block = current;
      block.text += `\n${block.indent > 0 ? line.trimStart() : line}`;
      block.end = index + 1;
    } else {
      current = start(index);
    }
  }

  flush();
  return out;
}

/** Where a path inside a change's file points, relative to that file, as a path from the repository root. */
export function resolve(from: string, target: string): string {
  const parts = from.split("/").slice(0, -1);
  for (const part of target.split("/")) {
    if (part === "" || part === ".") continue;
    if (part === "..") parts.pop();
    else parts.push(part);
  }
  return parts.join("/");
}
