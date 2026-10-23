# declared-loop-machine Specification

## Purpose
A checkout declares the loop it runs — the states, what observation recognises each one, and the
guarded transitions between them — and the harness obeys that declaration instead of a compiled
copy, refusing to act at all when it cannot read it.
## Requirements
### Requirement: An unreadable loop makes the harness start nothing
Where the declared loop is absent, larger than its bound, unparsable, or names anything outside the
vocabulary the product understands, the system SHALL treat the Folder as declaring no loop and
SHALL start no Run for it. It SHALL NOT fall back to `lifecycle.json`, to a built-in machine, or to
the workflows of `workflows.json`. The refusal SHALL be reported with its reason and the file it
was read from, because the symptom — nothing happens — is otherwise indistinguishable from the
watcher being switched off.

#### Scenario: No file declares no loop
- **WHEN** a Folder has no declared loop
- **THEN** no Run starts for any of its tasks, and the reason is recorded

#### Scenario: A malformed file is not an empty one
- **WHEN** the declared loop is mid-edit, carries conflict markers, or does not parse
- **THEN** the Folder is treated as declaring no loop rather than as declaring no guards

#### Scenario: A declaration from a newer product is refused, not guessed
- **WHEN** the loop names a node type or expression this product does not understand
- **THEN** it is refused with that term named, rather than that condition being treated as false

### Requirement: The loop's vocabulary is closed, and nothing read from it executes
The node types a loop may carry, the comparisons a `when` may apply, and the tokens an interpolation
may substitute SHALL each be drawn from a fixed set the product defines — four node types, the
typed-output comparisons of `run-the-declared-loop`, and the declared inputs. The system SHALL NOT
evaluate expressions, code, or templates supplied by the file beyond substituting those tokens. The
file arrives from a repository through a pull request and SHALL be bounded and parsed on the same
terms as every other declared file — a size limit, a comment-tolerant parser, and a malformed
document answered as no declaration.

#### Scenario: A declaration cannot describe an action the product does not offer
- **WHEN** a declaration names a node type outside the fixed set
- **THEN** it is refused, and no part of the file is executed to find out what the node meant

#### Scenario: The same bounds as its sibling files
- **WHEN** the declared loop exceeds the size a declaration can reasonably be
- **THEN** it is refused on the same terms `.harness/commands.json` and `.harness/config.json` are

