# risk-score Specification

## ADDED Requirements

### Requirement: The loop's risk node is answered by a local script that exists

The declared loop's `risk` node SHALL be answered by `.harness/scripts/risk-score <issue>`, which SHALL exist, be
executable, and print on stdout exactly the typed object the node's `output_format` declares: `band` (`low`,
`medium`, `high`), `score` (a number), `confidence` (a number), and a `why` (a string). The script SHALL NOT
implement retry of any kind: a failed prerequisite is answered once, not worked around by trying again.

#### Scenario: The loop's node finds its script

- **WHEN** the `risk` node runs `.harness/scripts/risk-score <issue>` on a checkout that carries this change
- **THEN** the script exists, is executable, and its stdout is one JSON object with `band`, `score`,
  `confidence` and `why`

#### Scenario: The routing the loop declares is untouched

- **WHEN** the script answers any band
- **THEN** `issue-loop.yaml`'s `when:` expressions route on it exactly as before this change, because the
  node's `output_format` and the expressions above it are unchanged

### Requirement: The score combines typed answers from a local laya server, weighted in code

The script SHALL build its state from what exists in the working tree at the moment the node runs it — the
issue's body read through `gh`, the OpenSpec change directory under `openspec/changes/`, and the diffstat of
`origin/master...HEAD` — and SHALL ask questions of the two primitive kinds laya answers well: `noul` facts
(one question each for the collision rules a repository states — shared file ownership, migrations, a new
dependency) and one `choice` for the band itself. It SHALL NOT ask a laya `score` question, because the
primitive underestimates blast radius (measured on this repository's own issue #365: answer 1.29 of 3 for a
change that deletes a module). The combination — which answers exist, which fired, what each weighs — SHALL
be code in the script, not prompt material. `confidence` SHALL be laya's `answer_confidence` of the band
answer; `why` SHALL name the questions that fired and the confidence of each.

#### Scenario: The state is what the node has, not a re-derivation

- **WHEN** the script builds the state for an issue
- **THEN** it reads the issue's own body, the change directory and the diffstat — material a checkout holds
  at that node — and invents no other provenance

#### Scenario: Every fired fact appears in why

- **WHEN** a `noul` question answers above its threshold and raises the band
- **THEN** the printed `why` names that question and its confidence, so a person reads which facts caused the
  band they are being asked to approve

### Requirement: A missing or broken prerequisite falls back to the loop's existing band

Where the laya server is down, its weights are not downloaded, Python 3.10+ is not on PATH, or `gh` cannot
read the issue, the script SHALL answer `band: medium`, `score: 0`, `confidence: 0` and a `why` naming the
prerequisite that failed — the same answer the loop's fallback line gives today, arrived at for a stated
reason. The script SHALL NOT install Python, download weights, or start a server on its own: provisioning is
`.harness/scripts/laya ensure`, run by a person or a command that chose to.

#### Scenario: The server is down

- **WHEN** nothing answers on the port the script expects
- **THEN** the answer is the medium band with `confidence: 0`, and `why` names the server as the missing
  prerequisite, with the port it tried

#### Scenario: The issue cannot be read

- **WHEN** `gh issue view` fails — no network, no auth, no such issue
- **THEN** the answer is the medium band with `confidence: 0`, and `why` says the issue could not be read,
  in the words `gh` used

#### Scenario: An unparsable answer is a fallback, not a crash

- **WHEN** the laya server answers something the script cannot parse as its typed contract
- **THEN** the script answers the medium band with `confidence: 0` and a `why` naming the parse failure,
  rather than exiting non-zero with no JSON on stdout

### Requirement: The server the script talks to is provisioned by one declared command

`.harness/scripts/laya` SHALL offer `ensure`, `start`, `stop` and `status` over a laya server pinned by
`.harness/laya-requirements.txt`, its venv under gitignored `.harness/data/laya/`, bound to
`127.0.0.1:8317`. `ensure` SHALL be idempotent: on a healthy server it changes nothing, and on a missing
venv it builds one from the pin, starts the server, and does not answer until `/health` responds. The
Homebrew Python on macOS cannot verify Hugging Face's certificate, so the launcher SHALL pin
`SSL_CERT_FILE` to the venv's own certifi bundle when starting the server — the one-line fix without which
the weight download fails on a fresh machine. The launcher SHALL refuse to bind anything but loopback: the
API it starts has no authentication.

#### Scenario: Ensure is idempotent

- **WHEN** `.harness/scripts/laya ensure` runs with a server already healthy on the port
- **THEN** it prints that it found one healthy and changes nothing

#### Scenario: A fresh machine downloads once

- **WHEN** `.harness/scripts/laya ensure` runs with no venv and no weights
- **THEN** it builds the venv from the pin, starts the server with `SSL_CERT_FILE` set, and answers only
  after `/health` responds, with the weights cached in the user's home

#### Scenario: Only loopback

- **WHEN** the launcher starts the server
- **THEN** it binds 127.0.0.1 and not 0.0.0.0, and a `stop` on the recorded pid ends it

### Requirement: The supervisor and the scorer are declared as folder commands

`.harness/commands.json` SHALL list `.harness/scripts/laya status` (one-shot, no tab) so a person can read
the server's state from the Commands tab, and the risk band a change carried — one line, the band and
confidence — SHALL be printed by the scorer in a form `verify` can show. Neither carries `gate`: a model's
opinion is not a merge verdict, on the same terms the loop's `decides: model` keeps the routing and the
score apart.

#### Scenario: The Commands tab shows the server state

- **WHEN** a person opens the Commands tab of a folder carrying this change
- **THEN** the laya server's status is listed, and running it prints server state without starting anything

#### Scenario: A model's score never gates a merge

- **WHEN** the merge verdict is assembled
- **THEN** neither the risk band nor the server status contributed an exit code to it, because neither
  command is declared with a gate
