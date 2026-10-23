## MODIFIED Requirements

### Requirement: Retaking the narrow set is a step of the implement command

The command that implements an approved change SHALL name retaking the design record as one of its
steps, with the command that does it, so that it happens where the product is already running and the
reviewer sees the picture and the diff together.

A change that moves a screen the set covers SHALL commit the retaken capture with its diff. A capture
of a screen that is no longer there SHALL be treated as worse than no capture, because it argues
against something that does not exist.

The retake SHALL also leave its captures where the evidence step pairs them: each new or changed
capture copied to `.harness/evidence/` in the worktree, named by the issue's convention
(`evidence-<n>-<screen>.png`), so the product reads them without being told where they are. The copy
SHALL be local files only — no app, no network, nothing that can fail the step for a reason the
captures themselves did not.

#### Scenario: A change moves a covered screen

- **WHEN** an implemented change alters a screen the narrow or wide set covers
- **THEN** the implement step retakes that set and the new files are committed with the change

#### Scenario: A change touches no covered screen

- **WHEN** an implemented change alters nothing the set covers
- **THEN** the step is a no-op and no capture changes

#### Scenario: The copy lands under the convention

- **WHEN** the retake produced captures
- **THEN** `.harness/evidence/` holds them named `evidence-<n>-<screen>.png` for the issue being
  implemented, and `git status` stays clean because the directory is ignored

#### Scenario: The app is down and the wide half cannot run

- **WHEN** no AppHost reports the `server` resource healthy
- **THEN** the wide retake degrades as `capture.sh` already does — said, with the remedy — and the
  command does not start the app itself
