// Managed by aio. Install harness overwrites it.
// The aio:state comment: one comment per issue, holding the facts a change accumulates — its change
// name, its pull request, what each session cost, what each phase reported and the feedback filed
// about it. The first product's push-session-report skill wrote it; .aio/steps/feedback is
// what still does. It lives on the issue because the issue outlives every worktree: a file in
// the checkout died with the worktree the merge removes, before /aio:review could read it.
//
// A comment and not the issue body: the body belongs to the person who wrote the issue and already
// carries the Task input block, and an edit to it is last-writer-wins. Nobody else edits this one.

import { execFileSync } from 'node:child_process';
import { realpathSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

export const MARKER = '<!-- aio:state v1 -->';

const HEADER =
  'Estado del loop, escrito por `push-session-report`. No lo edites a mano: la próxima fase lo reescribe.';

function gh(args, input) {
  return execFileSync('gh', args, {
    encoding: 'utf8',
    input,
    stdio: ['pipe', 'pipe', 'pipe'],
  });
}

export function emptyState(issue) {
  return { issue: Number(issue), change: null, pr: null, sessions: {}, phases: [] };
}

function parseBody(body) {
  const match = /```json\n([\s\S]*?)\n```/.exec(body);
  if (!match) throw new Error('the aio:state comment carries no json block');
  return JSON.parse(match[1]);
}

function renderBody(state) {
  return `${MARKER}\n${HEADER}\n\n\`\`\`json\n${JSON.stringify(state, null, 2)}\n\`\`\`\n`;
}

/** The issue's state comment, or `{ id: null, state: emptyState }` when it has none yet. */
export function readState(issue) {
  const out = gh([
    'api',
    '--paginate',
    `repos/{owner}/{repo}/issues/${issue}/comments`,
    '--jq',
    `.[] | select(.body | startswith("${MARKER}")) | {id, body}`,
  ]);
  const first = out.split('\n').find(Boolean);
  if (!first) return { id: null, state: emptyState(issue) };
  const { id, body } = JSON.parse(first);
  return { id, state: parseBody(body) };
}

export function writeState(issue, id, state) {
  const payload = JSON.stringify({ body: renderBody(state) });
  if (id) gh(['api', '-X', 'PATCH', `repos/{owner}/{repo}/issues/comments/${id}`, '--input', '-'], payload);
  else gh(['api', '-X', 'POST', `repos/{owner}/{repo}/issues/${issue}/comments`, '--input', '-'], payload);
}

/**
 * One figure per session, summed. A session's entry is replaced on every push rather than added
 * to, because each push re-reads the whole transcript — so the sum here never counts twice.
 */
export function totals(state) {
  const tokens = { input_tokens: 0, output_tokens: 0, cache_creation_tokens: 0, cache_read_tokens: 0 };
  let cost = 0;
  let costKnown = true;
  let durationMs = 0;
  const sessions = Object.values(state.sessions ?? {});
  for (const s of sessions) {
    for (const k of Object.keys(tokens)) tokens[k] += s.tokens?.[k] ?? 0;
    if (s.cost_usd_estimated == null) costKnown = false;
    else cost += s.cost_usd_estimated;
    durationMs += s.duration_ms ?? 0;
  }
  return {
    sessions: sessions.length,
    tokens,
    duration_ms: durationMs,
    cost_usd_estimated: sessions.length && costKnown ? Number(cost.toFixed(4)) : null,
    phases_without_usage: (state.phases ?? []).filter((p) => p.usage_unavailable).length,
    // What went wrong with a step while this issue was worked: the harness-feedback issues
    // `.aio/steps/feedback` opened or added to. Phases pushed before it existed carried
    // the note themselves, and still count.
    friction: [
      ...(state.feedback ?? []),
      ...(state.phases ?? []).filter((p) => p.status && p.status !== 'ok'),
    ],
  };
}

// `node issue-state.mjs <issue>` prints the state and its totals.
if (process.argv[1] && realpathSync(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const issue = process.argv[2];
  if (!/^\d+$/.test(issue ?? '')) {
    process.stderr.write('issue-state: usage: issue-state.mjs <issue number>\n');
    process.exit(1);
  }
  let id;
  let state;
  try {
    ({ id, state } = readState(issue));
  } catch (error) {
    process.stderr.write(`issue-state: ${String(error.stderr || error.message).trim().split('\n')[0]}\n`);
    process.exit(1);
  }
  process.stdout.write(`${JSON.stringify({ found: id !== null, state, totals: totals(state) }, null, 2)}\n`);
}
