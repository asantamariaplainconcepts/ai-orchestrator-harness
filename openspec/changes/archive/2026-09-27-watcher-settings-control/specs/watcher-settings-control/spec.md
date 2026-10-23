# watcher-settings-control Specification

## ADDED Requirements

### Requirement: The watcher's operative state is readable and shown on the settings screen
The host SHALL expose the label watcher's operative state over HTTP — effective mode, tick interval,
last tick at, and the last tick's refusals as typed rows carrying the task key and the reason — and
the settings screen SHALL show it as a section composed from the design kit. The refusal reasons a
tick already logs (wave reasons, suppressed identities, unreadable loop files, missing registries,
foreign claims) SHALL reach the screen through this read rather than staying log-only, because
"nothing is launching" having no on-screen answer is the cost this capability exists to stop paying.

#### Scenario: A machine that has never configured the watcher
- **WHEN** the settings screen is opened and no mode has ever been set
- **THEN** the section shows the mode as disabled with the plain-words explanation of what disabled
  means, and no tick has ever run

#### Scenario: Refusals from the last tick are named with their task
- **WHEN** the watcher runs in an active mode and its last tick discarded candidates
- **THEN** the read returns one typed row per refusal, each carrying the task key and one reason,
  and the screen lists them under the state

### Requirement: The mode lowers freely, one rung at a time
The system SHALL accept a command that lowers the mode exactly one rung — `dispatch-claims` to
`observe-claims`, `observe-claims` to `observe`, `observe` to `disabled` — after a confirmation, and
the command SHALL be refused when it names the rung the mode already sits on or any rung above it.
Lowering SHALL NOT stop runs already started; it stops the next tick from starting new ones. The
change SHALL be applied by the next tick without restarting the host, because the mode is read per
tick, and the host log SHALL record the lowering.

#### Scenario: Lowering one rung takes effect without a restart
- **WHEN** the mode is `observe-claims` and the lowering command runs
- **THEN** the next tick observes without acquiring, and no restart happened in between

#### Scenario: Lowering never interrupts a journey already running
- **WHEN** the mode is lowered while a dispatched run is in progress
- **THEN** that run is left to finish on its own, and only subsequent ticks are governed by the new
  mode

#### Scenario: A lowering that names no rung below is refused
- **WHEN** the mode is `disabled` and any lowering command arrives
- **THEN** it is refused with a 4xx that names disabled as the bottom rung

### Requirement: The mode raises one rung behind a confirmation that names the consequence
The system SHALL accept a command that raises the mode exactly one rung from the rung it holds —
including from a `disabled` the screen itself persisted, where the one rung is `observe` — and the
command SHALL carry no destination, so a skip cannot be asked for and the only refusal is the top
rung. The screen SHALL ask a confirmation whose copy names, in plain words, what the next rung does
— for `observe-claims` and `dispatch-claims`, that the watcher will acquire, and dispatch, the
tasks the tick finds eligible — before the command is sent. The ladder's friction is deliberate:
raising is one deliberate act per rung, never a dropdown.

#### Scenario: Raising climbs at most one rung
- **WHEN** the mode is `observe` and the raising command runs repeatedly
- **THEN** each command climbs exactly one rung — `observe-claims`, then `dispatch-claims` — and
  never further in one act

#### Scenario: Raising from a persisted disabled reaches the first rung
- **WHEN** the mode is a persisted `disabled` and the raising command runs
- **THEN** the mode becomes `observe`, behind the confirmation that names what observation does

#### Scenario: Raising off the top is refused
- **WHEN** the mode is `dispatch-claims` and the raising command runs
- **THEN** it is refused with a 4xx that names `dispatch-claims` as the top rung

#### Scenario: The confirmation names what will happen
- **WHEN** the screen offers to raise the mode to `dispatch-claims`
- **THEN** the confirmation states that agents will be seated on the tasks the next tick finds
  ready, in the words of the screen's own copy, before the command can be sent

### Requirement: The pilot task is editable and empty means every eligible task
The system SHALL accept setting and clearing the pilot task (`LabelWatcher:PilotTask` semantics) as a
persisted value, and an empty pilot SHALL mean every eligible task, which is the watcher's behaviour
when no pilot is configured. The value SHALL apply from the next tick without a restart.

#### Scenario: A named pilot restricts the dispatch cut to itself
- **WHEN** the pilot task is set and the mode is `dispatch-claims`
- **THEN** the tick dispatches only the named task and logs every other eligible as
  would-have-dispatched, until the pilot is cleared

#### Scenario: Clearing the pilot returns the full cut
- **WHEN** the pilot task is cleared from the screen
- **THEN** the next tick treats every eligible task as the pilot treated the named one

### Requirement: The state persists where a velopack update cannot erase it
The watcher's persisted state — mode and pilot task — SHALL live in machine-local storage outside
the installed bundle, so a velopack update that replaces the application directory wholesale leaves
it intact, and SHALL fall back to the configuration value on a machine that has never written one,
so existing installations keep behaving as they did. The persisted mode SHALL be validated against
the same rung set the configuration accepts, and a persisted value outside it SHALL be refused the
same way an unknown configuration value is, with the refusal named in the log.

#### Scenario: An update does not reset the mode
- **WHEN** the installed app is updated and the mode was set from the screen
- **THEN** the new process reads the same mode, and the tick resumes under it

#### Scenario: A machine that configured nothing behaves as before
- **WHEN** the watcher starts on a machine with no persisted state and `LabelWatcher:Mode` in
  configuration
- **THEN** the effective mode is that configuration value, exactly as before this capability existed

#### Scenario: A corrupted persisted mode is refused, not guessed
- **WHEN** the persisted mode holds a value outside the rung set
- **THEN** the watcher refuses it with the same refusal an unknown configuration value gets, and
  the next tick does not run
