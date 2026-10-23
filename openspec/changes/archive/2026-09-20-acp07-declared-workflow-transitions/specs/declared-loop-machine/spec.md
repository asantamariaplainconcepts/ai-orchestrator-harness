## MODIFIED Requirements

### Requirement: The declared vocabulary is closed, and nothing read from it executes
The predicates a state or a guard may name, the effects a transition may apply, and the tokens a
prompt may interpolate SHALL be drawn from a fixed set the product defines. The system SHALL
NOT evaluate expressions, code, or templates supplied by the file beyond substituting those tokens.
The file arrives from a repository through a pull request and SHALL be bounded and parsed on the
same terms as every other declared file — a size limit, a comment-tolerant parser, and a malformed
document answered as no declaration.

A transition's `startRun` effect SHALL name a declared Workflow key — not a prompt, not a
command, not free text. A declaration arriving with any other payload in the effect SHALL be
refused naming the shape it must carry, and a Workflow key that resolves to no declared workflow
is a typed stop at use, never a fallback to a prompt-only run.

#### Scenario: A declaration cannot describe an action the product does not offer
- **WHEN** a declaration names an effect outside the fixed set
- **THEN** it is refused, and no part of the file is executed to find out what the effect meant

#### Scenario: The same bounds as its sibling files
- **WHEN** the declared file exceeds the size a declaration can reasonably be
- **THEN** it is refused on the same terms `.harness/commands.json` and `.harness/config.json` are

#### Scenario: An effect carrying a prompt is refused with the remedy named
- **WHEN** a lifecycle file's startRun effect carries a prompt where a workflow key belongs
- **THEN** the declaration is refused naming the workflow-key shape, not the prompt it found

#### Scenario: A workflow key nobody declared is a stop, not a guess
- **WHEN** a transition's named workflow does not resolve in the folder
- **THEN** the transition launches nothing and the stop names the workflow key it tried
