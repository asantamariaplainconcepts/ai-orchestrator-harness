# Proposal: laya-scores-the-risk

## Why

The declared loop's `risk` node calls `.harness/scripts/risk-score`, which does not exist (#365 names it as not built), and its comment credits Jev (TypeSafe) — a hosted API this repository never integrated: no client, no key, no seam. The loop therefore routes every issue through its fallback band today. A local scorer exists that needs neither an account nor a rate limit: laya serves the same typed-decisions contract (`choice`, `score`, `noul`) over HTTP on localhost, Apache-2.0, one call per issue.

Measured on this machine against issue #365 itself (M-series, laya 0.3.20, laya-serve warm on 127.0.0.1): atomic `noul` questions over the change's facts answered `touches_migrations` 0.92 and `new_dependency` 0.93 — both true — and a `choice` risk band `medium` at 0.83. Cold start is a one-time ~1.7 GB weight download plus ~40 s of loading; a warm call is 0.2–0.5 s. The `score` primitive underestimated blast radius (its documented weak spot), which fixes the shape of the questions: atomic `noul` facts, weights in code.

## What Changes

- `.harness/scripts/laya` — a supervisor for a local laya server: `ensure` (build a pinned venv under `.harness/data/laya/`, start `laya-serve` on 127.0.0.1:8317, wait for `/health`), `start`, `stop`, `status`. Includes the Homebrew SSL fix (`SSL_CERT_FILE` pinned to the venv's certifi bundle) without which the Python of Homebrew cannot download the weights. Declared prerequisite: Python 3.10+ on PATH.
- `.harness/laya-requirements.txt` — the pin: `laya[serve]==0.3.20`.
- `.harness/scripts/risk-score <issue>` — builds the state from what the loop node actually has at that point (the OpenSpec change directory, `git diff --stat origin/master...HEAD`, the issue's own body via `gh`), asks the atomic questions, weights the answers in code, and prints the typed JSON `issue-loop.yaml` declares: `{band, score, confidence, why}`. `confidence` is laya's `answer_confidence`, `score` the weighted combination, `why` names the questions that fired.
- `.harness/issue-loop.yaml` — the `risk` node's comment says laya, the fallback line it echoes says the script file name rather than a bare issue number. The `when:` routing is untouched.
- Fallback unchanged in shape: laya down, weights missing, Python missing or `gh` unable to read the issue → `band: medium`, `confidence: 0`, and a `why` saying which prerequisite failed. Nothing retries; the fallback is the existing behaviour the node already declares.

## Capabilities

### New Capabilities

- `risk-score`: the declared loop's `risk` node answered — one script that scores an issue's change on localhost with laya and answers the loop's typed output, with a fallback that is the loop's existing medium band.

### Modified Capabilities

None. `run-the-declared-loop` is untouched: the node type, its `output_format` and its `when:` routing are exactly as shipped; this change fills in the script the loop already calls.

## Impact

- New files under `.harness/` only; `src/` untouched, no World seam, no migrations, no frontend copy.
- `.harness/data/laya/` is already gitignored with its parent.
- Shared weights cache in the user's home (`~/.cache/huggingface`): ~1.7 GB, once per machine, not per worktree.
