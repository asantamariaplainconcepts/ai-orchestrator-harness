## ADDED Requirements

### Requirement: The review opens on a requested step and yields the heading when it is a tab

The PR review component SHALL accept the step it opens on, and SHALL accept being drawn as a tab of
the PBI screen. As a tab it SHALL NOT set the shell's heading — the PBI owns it — and SHALL keep its
own step switcher, reads, draft and leave-guard. The direct external-PR route SHALL keep its
behaviour: it opens on Code and sets the heading to the PR.

#### Scenario: The Proposal tab opens on Proposal

- **WHEN** the PBI screen's Proposal tab is opened for a joined PR
- **THEN** the review component is drawn on its Proposal step, and the shell heading still names the PBI

#### Scenario: The direct route is unchanged

- **WHEN** `/pbis/pull-requests/{owner}/{repo}/{number}` is opened
- **THEN** the review opens on Code and the heading names the PR
