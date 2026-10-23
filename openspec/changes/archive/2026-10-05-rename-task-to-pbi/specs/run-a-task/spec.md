# run-a-task Delta

## MODIFIED Requirements

### Requirement: A run needs something to say, and the agent is checked before the row exists
This capability is superseded by `run-a-pbi` (#448). Every requirement of `run-a-task` SHALL live on
in `run-a-pbi` with the behaviour unchanged and the vocabulary renamed from task to PBI. Nothing new
SHALL be added to this capability: it exists only to name its replacement.

#### Scenario: Neither a workflow nor a prompt is refused
- **WHEN** a run is started with both a workflow and a prompt
- **THEN** the run starts and the prompt is what the agent is asked

#### Scenario: A prompt alone is a run
- **WHEN** a run is started with a prompt and no workflow
- **THEN** the run starts

#### Scenario: An unusable agent leaves no run behind
- **WHEN** the agent named for a run is not usable
- **THEN** the run row is not created
