# Delta: read-a-run

## MODIFIED Requirements

### Requirement: A live run carries its screen as a frame
A live run SHALL carry the current screen of its terminal as a frame, tracked in memory from the same bytes
the transcript persists — one tracker per run, created on the first bytes seen and sized to what the agent
was last told, resized when a viewer's terminal reports its size. A run with no viewer SHALL still track,
at the last size anybody set. A finished run SHALL carry **no** frame, and finishing SHALL forget it. The
frame SHALL NOT be persisted: `RunLogLine` is the store of record, a frame is derivable state, and a
server restarted mid-run SHALL degrade to the ring tail said out loud as truncated rather than to invented
history. The screen SHALL be modeled by a complete VT emulator rather than a hand-written subset of
escape sequences, so that a TUI which repaints with scroll regions, insert/delete-line, erase-chars,
tab stops or autowrap produces the screen the agent drew. Only stdout SHALL feed the tracker; stderr stays
raw and out of the screen.

#### Scenario: A late attacher gets one clean frame
- **WHEN** a browser attaches to a run already ten minutes in
- **THEN** it receives the screen as it stands, not a replay of deltas with no background

#### Scenario: A TUI that repaints with scroll regions is shown whole
- **WHEN** the agent's TUI maintains its list through a DECSTBM scroll region
- **THEN** the serialized frame shows the list as the agent drew it, with no shifted or duplicated lines

#### Scenario: Insert and delete line move what was there
- **WHEN** the agent's TUI opens or closes a list entry with IL or DL
- **THEN** the serialized frame shows the entries above and below moved, not overwritten in place

#### Scenario: Finishing forgets the frame
- **WHEN** a run finishes
- **THEN** it carries no frame
