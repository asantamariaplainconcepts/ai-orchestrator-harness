## Why

PR #408 demonstrates a PTY against a real ACA sandbox — resume from
Stopped, read-write from the browser — but opening a shell in the sandbox
is not following the agent: nothing ties a Task's Run to the agent's own
session. The person watching an issue has no way to see the run their
issue dispatched, in the environment it runs in, without a local checkout.

## What Changes

- The task detail opens the **agent's own session** for its Run in ACA —
  the process and terminal the run created — not an independent shell in
  the same sandbox.
- Task, Run, environment and session are identified together, by full
  identity; two repositories with the same key never cross.
- Observing is read-heavy and effect-free: querying status or history
  does not resume the sandbox, provision resources or start processes.
- Connecting is separate from querying. Where a connection needs a resume,
  the possibly-billable activation is named and confirmed first, and the
  server authorizes it.
- A resumed sandbox does not fake session survival: the original session
  exists or it does not, and its absence is declared — no new agent is
  started to simulate continuity.
- Disconnected, sandbox stopped, session absent and process finished are
  four different states.
- Errors are actionable and carry no secrets in UI or logs.
- Metadata and history survive the environment's removal, with the
  unrecoverable parts declared.

## Capabilities

### New Capabilities

- `observe-a-remote-agent-session`: read and attach to the agent session a
  run created in a remote environment, from the task, with the
  query/connect split and its authorization.

### Modified Capabilities

- `read-a-run`: the run's facts name their environment — local or remote —
  and a remote run opens its existing session rather than being refused a
  destination.

## Impact

- Task detail and run selection — #431's and #433's surfaces, reused.
- Terminal, transcript and session contracts that exist today — reused,
  extended only as declared.
- An ACA adapter behind the World seam, implemented in Infrastructure —
  the technical base inspected in `infra/pty-demo` (PR #408), imported
  deliberately, not blindly.
- Server-side authorization for query and connect; tests for
  association, permissions, connection and local regression.
- Depends on #433's walk and surfaces, and on PR #408's base (or its
  approved equivalent) being available; this change does not merge #408.
