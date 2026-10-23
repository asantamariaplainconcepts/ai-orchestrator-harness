# Design: laya-scores-the-risk

## Context

`issue-loop.yaml` declares the `risk` node as a bash fact whose script does not exist; its fallback answers
`medium` / `0` for every issue, and the two nodes above the fallback (`respec` on high, `hold-spec` on
medium-or-unsure-low) route on answers no model has ever given. The comment credits Jev, a hosted API this
repository has never called. Everything else in the loop's WORK section is real — the gate runs, the
tripwires run — so the risk node is the one place the loop declares behaviour it cannot produce.

laya (Apache-2.0) answers the same typed questions over HTTP: `choice`, `score`, `noul`, one forward pass,
no text generation. Measured warm on this machine (M-series, MPS): 0.2–0.5 s per call, one call per issue.
Cold start: a one-time ~1.7 GB weight download and ~40 s of loading, both amortised by a resident server.

## Goals / Non-Goals

Goals:

- The `risk` node's script exists and answers the typed JSON the loop already declares.
- Provisioning is one command a person (or a chosen command) can run, idempotent, loopback-only.
- Every fallback has a reason in `why` — the loop's existing medium band, never a crash, never a retry.
- The questions asked are the ones laya answers well, measured here: atomic `noul` and the final `choice`.

Non-Goals:

- No client in `src/`, no World seam, no C# — the node is a bash fact and stays one.
- No Docker, no AppHost resource — the supervisor is a script under `.harness/`, on the same terms as the
  tripwires it sits beside.
- No fine-tuning. The shipped checkpoints answer zero-shot; the numbers above are the zero-shot numbers.
- No classification of issues at `refine` — a separate concern, another issue, if the node earns it.
- The `when:` routing of the loop does not move by one token.

## Decisions

### The server is a resident script-supervised process, not an AppHost resource

ADR-0003 kept the AppHost free of containers for *persistence*; an inference server is a different concern
but the AppHost is still the wrong owner: the loop's other facts (tripwires, gate) run as plain scripts
against the working tree, and `aspire do ci` must not depend on a model server being up. So the supervisor
is `.harness/scripts/laya` — `ensure|start|stop|status`, pidfile under `.harness/data/laya/`, port
`127.0.0.1:8317` fixed in the script. Alternative rejected: a Docker container (adds a build and a daemon
for a process that is one `laya-serve` away, and Docker on this machine cannot pass MPS anyway).

### The pin is the requirements file, and the venv lives in gitignored data

`laya-requirements.txt` pins `laya[serve]==0.3.20`. The venv is built under `.harness/data/laya/venv`
(already gitignored) so a worktree builds its own rather than sharing a main checkout's interpreter, and
deleting a worktree deletes its venv with it. Weights are the one thing shared deliberately
(`~/.cache/huggingface`): 1.7 GB fixed, identical for every checkout, not per-worktree state.

### Questions: `noul` for the repository's collision rules, `choice` for the band — no `score`

Measured on #365 itself: `noul` `touches_migrations` 0.92 (true), `new_dependency` 0.93 (true), band `choice`
`medium` 0.83; `score` on blast radius answered 1.29 of 3 for a change deleting 3,013 lines — its documented
weak spot. So the script asks laya `noul` questions over the collision rules a repository states (shared
file ownership — `World.cs` in this one —, migrations, a new dependency) and one final `choice` for the
band, then weights the answers in code, in the script, visible. Alternative rejected: asking laya for the
weighted score itself — that moves the weighting into prompt material nobody reviews.

### `confidence` is `answer_confidence`, never laya's `confidence`

laya's `confidence` is 1-minus-normalised-entropy — distribution shape, not probability of the answer, and
a threshold carried over from any other system does not transfer. `answer_confidence` is the probability of
the reported answer and is the number the loop's hold gate (`< 0.85`) reads. The script maps one onto the
other by name and uses only the second.

### Fallbacks are the loop's existing answer, with the reason attached

Server down, weights missing, Python missing, issue unreadable, JSON unparsable → `medium` / `0` / `0` /
`why: <the prerequisite and its words>`. The script never installs anything and never retries; provisioning
is `ensure`'s job, run by choice. This keeps the node's contract with the loop: a fact step that always
answers, routing on a typed output.

### The supervisor pins `SSL_CERT_FILE` to the venv's certifi bundle

The Homebrew Python on macOS ships without the system trust store, and `snapshot_download` fails with
`CERTIFICATE_VERIFY_FAILED` on a fresh machine — hit and fixed during this change's first run. The launcher
exports `SSL_CERT_FILE` (and `REQUESTS_CA_BUNDLE`) to the venv's own bundle when starting the server. One
line, and without it the download fails on the platform this repository is developed on.

## Risks / Trade-offs

- [The band a model gives is an opinion, not a fact] → The loop already says so in the node's own
  extension: `decides: model`. The commands this adds carry no `gate`, and the hold routing treats low
  confidence as *more* human attention, not less.
- [A second checkout's `ensure` finds the port taken by the first's server] → Servers are per-checkout but
  the port is fixed: `start` refuses to start a second server on a live port and says whose pid holds it;
  the scorer does not care whose server answers, only that one answers on loopback.
- [Weights download (1.7 GB) on a metered link] → Once per machine, at `ensure` time, explicit — the scorer
  never downloads; the fallback names weights as the missing prerequisite when they are.
- [laya's English checkpoint fails on Spanish text and the router picks multilingual] → The router does
  this per request; observed correct on this repository's own Spanish bodies. No pinning; a routing column
  is printed in `why` for the person who wants to check.
- [A new laya release changes answer shapes] → The pin (`0.3.20`) is the whole defence; upgrading is a
  requirements-file edit and a re-`ensure`, visible in review.

## Migration Plan

Additive only: new files under `.harness/`, one comment line replaced in `issue-loop.yaml`, two new entries
in `commands.json`. A checkout without a venv simply falls back to the loop's existing medium band with a
`why` saying so; `ensure` is what opts a machine in. Rollback is `git revert` of the one commit.

## Open Questions

None blocking. Whether `refine` should classify issues before a proposal is written is a separate question,
deliberately not decided here.
