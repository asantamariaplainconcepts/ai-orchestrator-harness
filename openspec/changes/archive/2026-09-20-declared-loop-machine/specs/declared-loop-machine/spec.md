# Spec Delta

## Purpose

A checkout declares the loop it runs — the states, what observation recognises each one, and the
guarded transitions between them — and the harness obeys that declaration instead of a compiled
copy, refusing to act at all when it cannot read it.

## ADDED Requirements

### Requirement: A checkout declares its loop, and the harness obeys the declaration
The system SHALL read a declared machine from the Folder's checkout and SHALL decide which Run to
start for a task by interpreting that declaration. It SHALL NOT carry a compiled table of which
status starts which command. A declaration SHALL name its states, the observation that recognises
each one, and the transitions between them with their guards and the effect each applies. Changing
the declared file SHALL change what the harness does on the next detection tick, with no rebuild and
no release of the product.

#### Scenario: Two declarations produce two behaviours
- **WHEN** the same set of open tasks is decided against two different declarations
- **THEN** the launches differ according to the declarations, because no part of the decision is
  compiled

#### Scenario: Editing the file changes the loop
- **WHEN** a person edits the declared machine in a checkout and the next detection tick runs
- **THEN** the harness acts on the edited declaration without the product being rebuilt

### Requirement: An unreadable declaration makes the harness start nothing
Where the declared file is absent, larger than its bound, unparsable, or names anything outside the
vocabulary the product understands, the system SHALL treat the Folder as declaring no machine and
SHALL start no Run for it. It SHALL NOT fall back to a built-in machine, and SHALL NOT read the
absence as permission to act without guards. The refusal SHALL be reported with its reason and the
file it was read from, because the symptom — nothing happens — is otherwise indistinguishable from
the watcher being switched off.

#### Scenario: No file declares no machine
- **WHEN** a Folder has no declared machine
- **THEN** no Run starts for any of its tasks, and the reason is recorded

#### Scenario: A malformed file is not an empty one
- **WHEN** the declared file is mid-edit, carries conflict markers, or does not parse
- **THEN** the Folder is treated as declaring no machine rather than as declaring no guards

#### Scenario: A declaration from a newer product is refused, not guessed
- **WHEN** the declaration names a predicate or an effect this product does not understand
- **THEN** it is refused with that term named, rather than that condition being treated as false

### Requirement: The declared vocabulary is closed, and nothing read from it executes
The predicates a state or a guard may name, the effects a transition may apply, and the tokens a
prompt may interpolate SHALL each be drawn from a fixed set the product defines. The system SHALL
NOT evaluate expressions, code, or templates supplied by the file beyond substituting those tokens.
The file arrives from a repository through a pull request and SHALL be bounded and parsed on the
same terms as every other declared file — a size limit, a comment-tolerant parser, and a malformed
document answered as no declaration.

#### Scenario: A declaration cannot describe an action the product does not offer
- **WHEN** a declaration names an effect outside the fixed set
- **THEN** it is refused, and no part of the file is executed to find out what the effect meant

#### Scenario: The same bounds as its sibling files
- **WHEN** the declared file exceeds the size a declaration can reasonably be
- **THEN** it is refused on the same terms `.harness/commands.json` and `.harness/config.json` are

### Requirement: One declaration may carry several machines over the same states
A declared file SHALL be able to name more than one machine over one set of states, so that two
loops differing only in their guards are one declaration rather than two copies. The harness SHALL
select the machine a Run is being started under by name.

#### Scenario: Two loops, one set of states
- **WHEN** a repository runs one loop that stops at its holds and another that does not
- **THEN** both are declared over the same states, differing in the guards their transitions carry,
  rather than as two duplicated lists of steps
