# Spec Delta

## Purpose

Advise a person what the manual loop would start next, without starting anything. The watcher
observes the same signals the manual loop reads by hand (`ready-for-proposal`, released holds,
WIP room) and logs the matching prompt. A fenced dispatch journal keeps every reserved
dispatch intent durable, and its unresolved entries suppress what observation would advise —
the latch a later trusted authority will consume before any Run starts.

## ADDED Requirements

### Requirement: Observation is opt-in and never dispatches
The watcher SHALL be disabled by default and SHALL only observe when `LabelWatcher:Mode` is
`observe`. Every tick SHALL be observation-only: it logs the `/aio:propose` or `/aio:implement`
prompt a person could start, and it SHALL NOT create a Run, apply a label, write a hold, or
call any write API of any module. A mode value other than `observe` or `disabled` SHALL be
refused with a log that names controlled admission and trusted authority as what is missing.

#### Scenario: Default configuration observes nothing
- **WHEN** `LabelWatcher:Mode` is unset
- **THEN** the watcher starts no tick loop and issues no request at all

#### Scenario: Observe mode advises a prompt
- **WHEN** a task is `ready-for-proposal` and `LabelWatcher:Mode` is `observe`
- **THEN** the watcher logs an `Observed` candidate with the `/aio:propose #<key>` prompt and every request it makes is a `GET`

#### Scenario: An unknown mode is refused
- **WHEN** `LabelWatcher:Mode` is set to a value other than `observe` or `disabled`
- **THEN** the loop does not run and an error names controlled admission and trusted authority

### Requirement: Tunables are read per folder and null is never no-limit
A Folder's `.harness/config.json` SHALL be read through a bound, comment-tolerant declared-file
seam, per folder, and a read that returns null (missing, malformed or unreadable) SHALL suppress
that folder's candidates rather than treat the folder as unlimited, because null never means
"no limit". The WIP room SHALL be counted per folder from the same task rows the manual loop
counts, prefixing the label the way the tunables file spells it, and a limit out of bounds
SHALL be refused rather than clamped.

#### Scenario: An unreadable config suppresses the folder
- **WHEN** a Folder's `.harness/config.json` is malformed while the watcher is in `observe` mode
- **THEN** the Folder's tasks appear in no `Observed` candidate

#### Scenario: A held-back task is retried by the next tick, not queued
- **WHEN** a task was not advised on one tick because its folder's WIP room was exhausted
- **THEN** the same task can be advised on a later tick without anyone re-triggering it

#### Scenario: Several tasks never exceed one folder's limit in one tick
- **WHEN** three tasks in one folder are eligible and only one slot is free under `wipLimit`
- **THEN** at most one is advised on that tick

### Requirement: A reserved intent is durable and idempotent per effect
A dispatch intent reservation at `POST /api/coordination/dispatch-intents` SHALL persist the
effect id, folder scope, canonical Task identity and operation before returning, and a replay
of the same reservation SHALL return the same row rather than creating a second dispatch,
arbitrated by the database. The journal SHALL be scoped to the Folder: one folder's intents
SHALL NOT be readable or writable through another folder's scope.

#### Scenario: Replay returns the same row, not a second dispatch
- **WHEN** the same effect id is reserved twice
- **THEN** the second call returns the existing record and no new intent exists

#### Scenario: Concurrent reservation is arbitrated by the database
- **WHEN** two callers reserve the same effect id at the same time
- **THEN** exactly one row exists afterwards and both read the same record

#### Scenario: One folder cannot read another's journal
- **WHEN** a journal read is scoped to Folder A
- **THEN** no intent reserved under Folder B's scope appears

### Requirement: An outcome is written once and history is protected
`PUT /api/coordination/dispatch-intents/{effectId}/outcome` SHALL move a `pending` intent
exactly once to `started`, `failed`, `refused` or `unknown`, and SHALL refuse to rewrite an
already-recorded outcome. A `started` outcome SHALL bind a non-empty Run id; every other
outcome SHALL carry a failure code and reason instead. An unknown effect id or an outcome
that clears a failure SHALL be refused.

#### Scenario: An outcome is recorded once
- **WHEN** an intent's outcome is recorded and a second outcome is attempted for the same effect id
- **THEN** the second call is refused and the first outcome stands

#### Scenario: Started requires a run and refuses a failure code
- **WHEN** an outcome of `started` arrives
- **THEN** it is accepted only with a non-empty run id and no failure code or reason; otherwise it is refused

#### Scenario: A failure cannot be cleared to a second failure
- **WHEN** an intent already carries a `failed` outcome
- **THEN** a later outcome write for that effect id is refused rather than rewriting the history

### Requirement: Unresolved journal entries suppress observation, fail closed
On every tick in `observe` mode the watcher SHALL read the journal through the folder-scoped
read API and SHALL suppress from its candidates every task whose canonical identity matches
a journal entry, logging the suppression. A journal that cannot be read, or returns something
that is not a valid journal, SHALL fail the whole tick rather than be treated as empty. The
suppression set SHALL be read from the journal rather than remembered in memory, so it
survives the process.

#### Scenario: A journaled task is suppressed from advice
- **WHEN** a task's identity matches a journal entry and the mode is `observe`
- **THEN** the tick logs `Suppressed` for it and it appears in no `Observed` candidate

#### Scenario: An unavailable journal is not an empty journal
- **WHEN** the journal read fails or returns an invalid body
- **THEN** the tick fails, nothing is advised, and no candidate is logged

#### Scenario: Suppression survives a restart
- **WHEN** the host restarts and a journal entry is still unresolved
- **THEN** the task remains suppressed on the first tick after restart

### Requirement: Folder and task identity are resolved exactly, never guessed
A task's Folder SHALL be resolved by comparing the task's URL against every known Folder's
remote URL under a host-aware repository identity that reads both `https://` and
`git@host:owner/name.git` forms, and a first-match folder wins. A task that matches no Folder
SHALL produce no candidate and SHALL be retried on a later tick, because a Folder can be added
after the task was first seen. A task whose identity cannot be established exactly — an
unsupported provider, an Enterprise API host, or a URL that is not a github.com issue — SHALL
be excluded and logged, never guessed by key alone.

#### Scenario: A task with no checkout on this machine starts nothing
- **WHEN** an eligible task's repository matches no known Folder's remote URL
- **THEN** no candidate is produced for it, and it is reconsidered on the next tick

#### Scenario: Adding the Folder later lets a previously-unresolved task launch
- **WHEN** a Folder for a task's repository is added after that task first became eligible
- **THEN** the task is advised on the next detection tick after the Folder exists

#### Scenario: Equal keys in another repository or host do not collide
- **WHEN** a journal entry for `key 313` in `another/repo`, or in `owner/repo` on an Enterprise host, suppresses tasks
- **THEN** github.com's `owner/repo` issue `313` is not suppressed by either

#### Scenario: An ambiguous provider identity is not guessed
- **WHEN** a task's source is not GitHub, or its URL is an Enterprise host, a non-default port or a non-github.com host
- **THEN** its control identity is reported unavailable and no candidate is produced for it
