## ADDED Requirements

### Requirement: A run names its environment, and a remote run keeps its destination

A run's facts SHALL name the environment it works in — a local working
directory or a remote environment — and a run in a remote environment
SHALL open its existing session from the surfaces that open runs, rather
than being refused a destination for having no local path. The local
terminal, transcript and session behaviour SHALL change nothing for local
runs: the remote environment is another place a run works, not a
replacement for the places it works today.

#### Scenario: A remote run opens from an observation surface

- **WHEN** a run that works in a remote environment is opened from the task detail or the walk
- **THEN** it opens its own session in that environment, through the observe-a-remote-agent-session capability's two doors

#### Scenario: A local run is unchanged

- **WHEN** a run works in a local working directory
- **THEN** its terminal, transcript and session behave exactly as they do today
