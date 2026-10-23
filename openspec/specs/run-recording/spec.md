# run-recording Specification

## Purpose
Every run leaves a recording of its terminal that can be replayed in the Journey, so what a run did is
readable after its session is gone. The format is asciicast v2 and the player is `asciinema-player`;
nothing about either is ours. Reverse-engineered from the shipped code
(`src/shared/AiOrchestrator.Infrastructure/Sessions/AsciicastRecording.cs`,
`src/modules/Runs/AiOrchestrator.Modules.Runs/Features/PbiRuns/UseCases/GetRecording.cs`), not
negotiated ahead of it.

## Requirements
### Requirement: The session is recorded as asciicast v2 while it runs
A run's session SHALL write `<runs directory>/<run id>/session.cast`, created before the process
starts: a header `{"version": 2, "width", "height", "timestamp", "env": {"TERM": "xterm-256color"}}`,
then one line per output chunk `[seconds, "o", text]` and one per resize `[seconds, "r", "COLSxROWS"]`,
decoded as UTF-8 across chunk boundaries. Each line SHALL be flushed as written and the file shared for
reading, so a live run's recording can be followed. The recording SHALL be complete when the session's
kill returns. A session opened on a folder SHALL NOT be recorded.

#### Scenario: A recording survives the session
- **WHEN** a run ends and its worktree is removed
- **THEN** its `session.cast` is still in the runs directory and replays what the terminal showed

### Requirement: The recording is served and played in the Journey
`GET /api/runs/{runId}/recording` SHALL answer the file as `application/x-asciicast`, including the
partial file of a live run; 404 `Run.NotFound` for an unknown run and 404 `Recording.NotFound` when the
file is gone. The Journey SHALL play it with `asciinema-player`, loaded only when a person asks for it.

#### Scenario: Replaying a finished run
- **WHEN** a person unfolds a finished run in the Journey and presses play
- **THEN** the player loads the recording and replays it with its own bar, speed and seeking

### Requirement: A finished run's recording is copied to the repository's aio/evidence branch
After a run's row is saved as ended, the control plane SHALL copy its `session.cast` to the branch
`aio/evidence` at the folder's `origin`, at `<pbi>/<run id>/session.cast` (the run id as 32 hex digits),
with one commit per recording whose message is `<pbi> · <state> · <run id>` (`no state` for a PBI in
none) (#697). The branch SHALL be an orphan, with no history of the default branch, created by the first
push. The commits SHALL be built in a temporary worktree of the folder's repository under the instance's
runs directory, removed afterwards, so the folder's checkout, its branch and its index and every PBI's
worktree are untouched. The push SHALL NOT be forced: refused because another instance pushed first, the
branch SHALL be fetched and the commits built again on it, up to three times. Every recording of the
repository's ended runs in this instance that the branch lacks SHALL go in the same push, so a push that
failed is made by the next run's. A push that does not land SHALL be a warning in the server's log and
SHALL NOT change the run's end or delay the PBI. Nothing local SHALL be deleted or moved: the branch is a
copy. `claim`, `merge` and `cleanup` SHALL leave it alone, since they name only `change/<n>`. Nothing
SHALL be pushed for a run whose folder has no `.aio/version`, read when the run ends from the folder
path the run has: a client's repository opened as a folder keeps its recordings local, which a debug
line in the log says, and no setting changes it.

#### Scenario: Two runs and a push from another instance in between
- **WHEN** a run of PBI 12 is pushed, another clone pushes a run of PBI 14, and a second run of PBI 12 is
  pushed from the first clone, whose copy of the branch is now behind
- **THEN** the second push is refused once, fetched, built on the other clone's commit and pushed, and
  the branch holds three commits and the three recordings, with no commit of `main`

#### Scenario: A folder without the harness
- **WHEN** a run ends in a folder whose checkout has no `.aio/version`
- **THEN** nothing is pushed to its `origin`, and the loop is told the run ended all the same

#### Scenario: The scripts of the loop leave the branch alone
- **WHEN** `aio/evidence` is at origin and as a local branch, and `claim 42`, `merge 7` and `cleanup 9` run
- **THEN** it is at the same commit at origin and locally, and `refs/heads/change/*` does not list it

### Requirement: A run another instance recorded is played from the vendor
`GET /api/pbi/recording?repository=<r>&path=<p>` SHALL read `<p>` from the repository's `aio/evidence`
branch through `IPbiProvider.ReadFilesAsync` and answer it as `application/x-asciicast` (#697). A path that
is not `<pbi>/<32 hex digits>/session.cast` SHALL be refused with 400 before the vendor is asked; a file
the branch does not have SHALL be 404 `Pbi.NoRecording`, which says the instance that ran it pushes it
when its next run ends. A run the Journey read from the issue's journey comment SHALL be played from this
route with the path its row names, by the same `asciinema-player`, and one whose row names none SHALL say
"No recording: the comment names none on aio/evidence."

#### Scenario: An instance with an empty database replays another's run
- **WHEN** the Journey is drawn from the issue's comment and a person presses play on its run, whose row
  names `495/<run id>/session.cast`
- **THEN** the player loads `/api/pbi/recording?repository=owner/sandbox&path=495/<run id>/session.cast`
  and replays it

