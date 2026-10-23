## ADDED Requirements

### Requirement: A closed change's result is read from its pull request, not reconstructed locally

When a change's pull request is closed — merged, or closed without merge —
the change's result SHALL be read through the remote pull request reader
(`review-a-remote-pull-request`) against its pinned comparison, and SHALL
NOT be reconstructed from the local worktree: no local diff, file,
OpenSpec-change or evidence reads SHALL serve the closed result. The state
of the worktree — clean, dirty or deleted — SHALL have no effect on what
the closed result shows.

#### Scenario: A merged pull request with a deleted worktree

- **WHEN** a change is merged and its worktree is gone
- **THEN** its result is read from the pull request, and the missing directory changes nothing about it

#### Scenario: A merged pull request with a dirty worktree

- **WHEN** a change is merged and uncommitted changes sit in its worktree
- **THEN** the closed result shows the pull request's comparison, not the uncommitted work

### Requirement: Unpublished local work remains a distinct reading

A change with no pull request, and an open pull request whose worktree
carries work not yet pushed, SHALL keep their local reading: reading what
is true in a working directory now — uncommitted and unpushed work — is a
distinct question from reading a closed result, and it is not reduced by
this change.

#### Scenario: A worktree with unpushed work behind an open pull request

- **WHEN** an open pull request's head is behind its worktree's unpushed commits
- **THEN** the local working diff remains readable, and the pull request review remains readable, as separate surfaces
