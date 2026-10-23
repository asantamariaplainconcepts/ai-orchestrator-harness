## ADDED Requirements

### Requirement: What may hold an action back is a person's own checklist or a mechanical precondition, never a fact in the line

The rule that no action on this screen is disabled by a fact SHALL stand, and this requirement says
what is therefore left. Two things, and neither of them is the product's reading of the facts.

**A person's own ticks.** An action whose whole meaning is "the review is finished" MAY require the
review steps to be marked, because a tick is a statement somebody made and not a condition a machine
measured. Where an action does require them, the count outstanding SHALL be **said in words, beside
the control, in the place where it is held back** — a control greyed with no sentence is the thing
this screen removed, and the number alone is not the sentence. **Nothing in the header's actions box
SHALL be disabled at all**: an action with a precondition SHALL open and state it.

**A mechanical precondition of the operation itself.** An action MAY be refused by the server for a
fact about the branch that would make the operation *wrong* rather than merely inadvisable — work
that must be committed first, a change that must be archived first. Such a refusal SHALL name the
remedy, and where the remedy is a command it SHALL name that command. It SHALL be a refusal that
happens when the action is taken, and SHALL NOT become a disabled control.

**Neither of these is one of the facts in the line.** None of Base, Merge, Checks, Tree, Seam or the
pull request, as the line states them, SHALL disable or gate any action. A failing check, an unclean
merge, a branch behind the trunk and a pull request nobody could ask about SHALL all leave every
action exactly as available as they found it — a person overriding a stale check with a reason is
ordinary work, and the facts are stated so the reading can be the reader's.

#### Scenario: An unfinished checklist is said rather than merely shown
- **WHEN** an action requires the review steps and some are unmarked
- **THEN** the action is still reachable, and where it is held back the outstanding count is stated in
  words as the reason

#### Scenario: A mechanical precondition refuses when taken, and names its remedy
- **WHEN** an action is taken against a branch whose state would make the operation wrong
- **THEN** it is refused with a sentence naming the remedy, rather than having been disabled

#### Scenario: The facts in the line still disable nothing
- **WHEN** a check has failed, the merge is not clean, and the pull request could not be asked about
- **THEN** every action is as available as before, and none of the three is given as a reason to
  disable one
