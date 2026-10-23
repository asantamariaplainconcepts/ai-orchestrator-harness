// Managed by aio. Install harness overwrites it.
// The paths an issue declares in its Where it lands section, read the same way by file-overlap, which
// compares them with the issues in flight, and by quality, which hands them to the out-of-seam check
// (#758). CommonJS, because both scripts load it with require.
//
// The section is Dónde cae, Port o zona, or the Seam that issues written before #576 carry. Its paths
// are every backtick path in it, wherever it sits in the line, and every line that is nothing but a
// path, plus `seam.paths` from a Controlled Task input block. The section's title is a heading, a bold
// line, or a bold lead-in such as `**Dónde cae.** text`.
'use strict';

// A heading, a bold line, or a bold lead-in whose text goes on in the same line ("**Dónde cae.** En
// ..."): the text after a lead-in belongs to its section.
const TITLE = [/^#{1,6}\s*(.+?)\s*()$/, /^\*\*(.+?)\*\*:?\s*()$/, /^\*\*([^*]+?)[.:]?\*\*[.:]?\s+(.+)$/];
const titled = (line) => TITLE.map((p) => line.match(p)).find(Boolean) ?? null;
const title = (line) => titled(line)?.[1].replace(/[.:]$/, '') ?? null;

function section(lines, matches) {
  const start = lines.findIndex((line) => matches(title(line) ?? ''));
  if (start === -1) return null;
  const rest = lines.slice(start + 1);
  const end = rest.findIndex((line) => title(line));
  return [titled(lines[start])[2], ...(end === -1 ? rest : rest.slice(0, end))];
}

const WHERE = /seam|where it lands|d[oó]nde cae|port o zona|port or zone/i;

const looksLikePath = (raw) => !/\s/.test(raw) && (raw.includes('/') || /\.[A-Za-z0-9]{1,5}$/.test(raw));

/** The declared paths, or null when the issue declares none. */
function declared(body) {
  const lines = body.split(/\r?\n/);
  const paths = new Set();
  let found = false;

  const where = section(lines, (t) => WHERE.test(t));
  if (where) {
    found = true;
    for (const line of where) {
      // #575 names its file inside a sentence, so every backtick span counts; a span with a space or
      // without a slash or an extension (`aio done`, `IPbiProvider`) is prose and does not.
      for (const [, span] of line.matchAll(/`([^`]+)`/g)) {
        if (looksLikePath(span)) paths.add(span);
      }
      const bare = line.match(/^\s*(?:[-*]\s+)?([^\s`]+)\s*$/)?.[1];
      if (bare && looksLikePath(bare)) paths.add(bare);
    }
  }

  const input = section(lines, (t) => /^task input$/i.test(t));
  const json = input?.join('\n').match(/```json\s*\n([\s\S]*?)\n```/)?.[1];
  if (json) {
    try {
      for (const path of JSON.parse(json)?.seam?.paths ?? []) {
        found = true;
        paths.add(path);
      }
    } catch {
      // A block that does not parse declares nothing here; the backend's validator says why.
    }
  }

  return found ? [...paths] : null;
}

module.exports = { WHERE, declared };
