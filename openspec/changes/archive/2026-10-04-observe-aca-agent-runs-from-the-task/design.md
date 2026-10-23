## Context

Measured at propose time. The technical base is PR #408 (OPEN, mergeable,
draft-turned-ready on `explore/aca-sandbox-backend`): `infra/pty-demo`
holds a Node backend that resumes an ACA sandbox before each PTY
connection, writes its probe as a script into the sandbox (because
`aca sandbox shell -c` takes an executable, not a shell line), and serves
the PTY to the browser. Its README records PIM — the Owner role arrives
as an eligible assignment to activate — and the resource group
`netcoreconf-dev-rg-01`. What it demonstrates: resume from Stopped works,
the shell answers (`Linux 6.12.8+`), and the browser writes and reads
through it. What it does not demonstrate: Task → Run → the agent's own
session, in the product, authorized.

What exists in the product: runs carry transcripts and their own PTY
sessions locally (`open-a-terminal`, `read-a-run`); #431 proposes the
identity join and the task detail's execution entry; #433 proposes the
observed walk and run selection. The World seam
(`BuildingBlocks/World`) is where every external reach is registered, and
an ACA adapter belongs behind it, in Infrastructure.

## Goals / Non-Goals

**Goals:**

- From the task, open the agent's own session for its Run in an ACA
  sandbox — the process the run created, its terminal.
- Query/connect split: status and history reads are effect-free;
  connecting, where it needs a resume, is named, confirmed and
  authorized server-side.
- Honest session survival: a resumed sandbox without its original session
  says so; no substitute agent.
- Four states named: disconnected, sandbox stopped, session absent,
  process finished.
- Metadata and history survive the environment; limits declared.

**Non-Goals:**

- Merging PR #408 — the base is used, the PR's fate is a person's
  decision.
- AKS, extra providers, a universal terminal, generic remote agent
  controls (cancel/restart).
- Automatic provisioning or reactivation on errors.
- Recovering logs that were never persisted.

## Decisions

- **The session, not a shell.** The run's session is identified from the
  run's own metadata — the environment it was dispatched to and the
  session id its process established — and the detail opens **that**
  session's terminal. A new shell in the same sandbox is refused as an
  answer: it shows the machine, not the work. The demo's `resume`+
  PTY mechanics are the transport, not the identity.
- **Query and connect are two doors.** Status and history reads go
  through the adapter's read side and SHALL NOT resume, provision or
  spawn — the sandbox's state is read as a fact. The connect path is the
  only one that may call `aca sandbox resume`, and it does so after the
  server has authorized the caller and the UI has named the possible
  activation cost and taken an explicit confirmation. The split lives in
  the adapter's contracts, not in the caller's discipline.
- **Authorization is server-side.** The adapter checks the caller's
  permission for both doors (in the product's declared-permission terms),
  and refuses rather than degrading to an unauthorized read. Secrets —
  credentials, signed URLs — stay in Infrastructure; the UI and logs
  carry names and states, never tokens.
- **Resume does not fake survival.** After a resume, the adapter asks for
  the session the run named: it exists and is attached to, or it does not
  and the state is session-absent with the history it still has. Starting
  a new agent to fill the terminal would be inventing the work — the
  recorded failure mode #361 names from the other side.
- **The adapter is the fifth backend, promoted.** `infra/pty-demo`'s
  backends join through the same shapes the local PTY session uses; what
  moves is the transport (resume fencing, script-as-probe, sandbox
  lifecycle), registered behind World in Infrastructure with its own
  block. Nothing is imported blindly: each piece is re-justified against
  the product's contracts (typed revisions, session ids, transcript
  persistence) rather than the demo's convenience.
- **Local regression is a first-class check.** The local PTY, terminal and
  transcript paths keep their behaviour; the adapter adds a platform, it
  does not replace the local one.

## Risks / Trade-offs

- **Cost and blast radius.** A resume may bill; a mistimed one may wake a
  sandbox nobody asked for. Mitigated by the query/connect split and the
  explicit confirmation — the failure mode is a person saying no, not a
  surprise invoice.
- **The base is unmerged.** #408 may change or be closed; the design
  names its mechanics (resume fencing, probe-as-script, PIM activation)
  rather than its files, so an equivalent base can serve. Integration
  waits for the base, and says so.
- **Session identity is a contract the product does not fully have
  yet.** The run metadata needs the environment and session id; if the
  contracts lack them, this change declares the extension (a migration is
  claimed, its module's ownership named) rather than string-matching a
  sandbox out of a working path.
- **Real verification needs Azure.** The end-to-end proof runs only in an
  authorized environment with explicit cost and scope, registering what
  was activated and its final state — and it does not run during
  propose; the local and authorization surfaces are what this change can
  test on its own tree.

## Alternatives rejected

- An independent shell in the sandbox: shows the machine, not the work —
  the issue's named non-goal.
- Automatic resume on query: turns every read into a possibly-billable
  activation; the exact behaviour the issue forbids.
- A universal remote-terminal abstraction across providers: AKS and
  others are excluded; the abstraction would be speculative today.

## Error and temporal consistency

Disconnected, sandbox stopped, session absent and process finished are
four states; each has its own copy and its own remedy. A late answer for
another task's or run's session is dropped by the identity join. History
reads from the persisted store of record and declares what it does not
have.

## Verification plan

Maps to the delta scenarios:

1. From the task, the observed session is the run's own process.
2. Same key in two repositories, several sessions: no crossings.
3. Remote access with no local checkout; PR and execution independent.
4. Querying a Stopped sandbox: zero resume/provision/spawn (measured at
   the adapter's call boundary — a test double counts the calls).
5. Connecting with resume: explicit confirmation, server-side
   authorization.
6. Session lost after resume: absence declared, no new agent.
7. Disconnection: last observation, nothing invented.
8. Environment removed: history's limits explicit, nothing recreated.
9. Actionable errors without secrets; local terminal regression green.
10. The real Azure proof: only in an authorized environment, cost and
    scope explicit, resources activated and final state registered — not
    during propose.

Plus the transversal checks and the documented gate at implementation
time.

## Questions to resolve before implementation

- Does the run metadata carry environment and session id today, or is a
  contracts extension (and its migration, with the module's ownership
  claimed) needed?
- Which of `infra/pty-demo`'s mechanics survive the promotion, and what
  does the World contract for a remote session adapter look like in the
  product's terms?
- Which permissions name the query and connect doors, and where are they
  declared?
