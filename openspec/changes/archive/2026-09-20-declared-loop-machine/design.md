## Context

See `proposal.md` — Why. Four facts from the current codebase shape everything below.

1. **The decision is already a pure function, and the machine is already isolated inside it.**
   `LabelLaunchPlan.For` takes the world and returns launches; the only compiled knowledge of the
   loop is `CommandFor`, a five-line `switch`. Replacing that switch is a small surface, which is
   why this change is worth doing before the plane grows.
2. **Three declared files already exist and share one doctrine.** `HarnessCommandFile`,
   `HarnessWorkflowFile` and `HarnessConfigFile` each open a file from a checkout with a size bound,
   a comment-tolerant parser, and a malformed file answered as `null`. All three treat their input
   as untrusted, because it arrives from a repository through a pull request.
   `IDeclaredCommandFile` states the rule this change inherits: *"nothing read here is ever executed
   on its own"*.
3. **The main checkout is the one read, and in this product's ordinary shape it is on the trunk.**
   A Folder is the checkout; `/aio:propose`'s worktrees carry the feature branches. So reading a
   Folder's working copy reads what a person would see on the trunk, without a ref-aware read.
4. **`WorkflowsSeed.cs` already names two workflows as one shape.** `ship` is *"the same shape with
   the holds removed"* as `staged`, stored as two ordered lists.

## Goals / Non-Goals

**Goals:**
- A checkout declares its loop, and the harness obeys that declaration rather than a compiled one.
- Changing the loop is a pull request in the working repository, with no product release.
- A definition the harness cannot read makes it act less, never more.
- One definition can express both `staged` and `ship`.

**Non-Goals:**
- The branch claim and the lease from ADR-0005. Both are real and both are separate: the claim needs
  a `git push` capability that no World interface has today.
- Moving the gate from the agent to the harness. That is ADR-0005's first open question and stays
  open.
- Writing `status:blocked` on failure — #316's task 3.6, blocked on `SetTaskHold` refusing
  `held: true` by design.
- Changing the `/aio:*` prompts. Thinning them once the machine owns the order is the natural
  follow-on, not this change.
- A general expression language. See the first decision.

## Decisions

### The definition is data with a closed vocabulary, not a small language
**Decision:** states name predicates from a fixed set the product understands — the task's status,
whether it is held, whether a Folder resolves, whether the Folder has WIP room, whether a live Run
exists — combined only by "all of" and "none of". Transitions name a target, a guard list drawn from
the same set, and one effect from a fixed set. Prompt text may interpolate only a fixed set of
tokens, the task key among them.

**Why:** this file comes from a repository through a pull request, exactly like the other three
declared files, and `IDeclaredCommandFile`'s doctrine is that nothing read there executes on its
own. A closed vocabulary keeps that true by construction: the worst a hostile or mistaken file can
do is describe a loop that starts nothing or starts the wrong ordinary run, both of which a person
can read in the diff. It is also what keeps the definition drawable — a graph of named predicates
renders; arbitrary code does not.

**Alternatives considered:**
- *An embedded expression language (CEL, JsonLogic, a scripting host).* More expressive, and it
  turns a reviewed data file into a program with a sandbox to get right. Nothing in the loop needs
  it yet, and adopting one is much harder to walk back than extending a vocabulary.
- *C# with a plugin interface.* Would mean every repository compiling against the product's
  assemblies, which is the release coupling this change exists to remove.

### It is read from the Folder's working copy, like every other declared file
**Decision:** read `.harness/lifecycle.json` from the Folder's checkout, through a new
`IDeclaredLoopMachine` seam with an implementation beside `HarnessConfigFile` — same size bound,
same parser, same reading of malformed as `null`.

**Why:** it is the fourth of a family, and a fourth that behaved differently would be the surprise.
In the product's ordinary shape the Folder sits on the trunk (Context #3), so this reads the trunk
without inventing a ref-aware read.

**Consequence accepted:** a Folder deliberately left on a feature branch reads that branch's machine,
and two checkouts on different commits can decide differently. This is ADR-0005's recorded skew risk
and the same caveat `IDeclaredCommandFile` already states for commands — but the cost is higher here,
because for tunables it is a wrong number and for the machine it is two harnesses disagreeing about
what happens next. If that turns out to bite, reading at a named ref is the fix, and it is a change
to one seam rather than to this design.

**Alternatives considered:**
- *Read at `origin/<trunk>` through git.* Removes the skew, and needs a ref-aware file read that
  `IWorkspaceProbe.Contents` does not offer — it answers for a working directory, deliberately, as
  its own doc comment explains. A new capability for a risk the ordinary shape already avoids.

### No definition means the harness starts nothing, and this repository ships its own
**Decision:** absent, oversized, malformed, or out-of-vocabulary answers `null`, and the watcher
starts nothing for that Folder. There is no built-in default machine. This change adds this
repository's own `.harness/lifecycle.json` describing the loop it runs today.

**Why:** the same rule `HarnessConfigFile` already enforces for `wipLimit`, for the same reason: a
file read as "no rules" starts a run per eligible task on one tick, and that is the single mistake
here that stopping the loop does not undo. A product-shipped default would also re-introduce the
compiled machine this change removes — the fallback would become the real machine, and the file a
suggestion.

**Consequence accepted:** a repository that upgrades and declares nothing loses automatic launching
until it adds the file. That is visible, recoverable, and safer than the alternative.

### One file may declare several machines, and that is how `ship` stops being a copy
**Decision:** the file declares named machines. `staged` and `ship` are two names over the same
states, differing in whether their transitions carry the not-held guard.

**Why:** it is what `WorkflowsSeed.cs`'s comment already claims they are. Two files, or two
top-level definitions with duplicated states, would be the same duplication in a new place.

### The file takes the shape of XState JSON, without the runtime or a round trip
**Decision:** the declared file borrows XState's JSON shape — `states`, `always`, `target`, `guard`,
`actions` — where guards and actions are **names**, plus one field of our own per state carrying the
observation that recognises it. We adopt neither the runtime nor any promise of editing the file in
an external tool and reading it back.

**Why:** three reasons, and the first is the one that decides it.

*Named references are already what this design requires.* XState's JSON does not carry expressions:
a guard is `"hasRoom"` and the implementations are registered by the host at setup, so a file cannot
invoke anything the product did not define. That is the closed vocabulary of the first decision,
arrived at independently.

*Its eventless transitions are level-triggered.* An `always` transition is taken whenever its guard
holds, which is what a tick of this loop does. Modelling our reconciler as a machine of `always`
transitions is a translation, not a distortion.

*A shape somebody else already draws.* States, transitions and their labels are all a viewer needs,
and both XState's own graph tooling and Stately's editor read that shape. We get a picture without
building one.

**What it deliberately does not buy:** a round trip. **Our machine recognises its state; XState's
remembers it.** The per-state recogniser has no equivalent in that vocabulary — nor in SCXML's — so
an external editor would draw the states and either drop the recognisers or not understand them. An
editor that silently eats the half that makes this a reconciler is worse than no editor, so
compatibility is claimed for *viewing* and refused for *editing*.

**Alternatives considered:**
- *W3C SCXML, or SCJSON.* The standard, with more independent tooling — including a VS Code
  extension that edits graphically and writes back. Rejected because a transition's `cond` is a
  boolean **expression evaluated by a datamodel**, not a name. Its three datamodels are ECMAScript
  and XPath, which are the evaluator this design refuses to put behind a file that arrives through a
  pull request, and Null, whose only predicate is `In(state)` and cannot express `hasRoom` or
  `hasFolder`. A custom datamodel would leave the standard, which was the reason to adopt it.
  Executable content (`<send>`, `<assign>`, `<script>`) is expression-based for the same reason.
  SCJSON is also not a W3C artefact — it is a community JSON representation of SCXML, so it carries
  less of the standardisation its name suggests.
- *A shape of our own owing nothing to anyone.* Fewer constraints, and nothing reads it. The cost of
  the borrowed shape is being a subset of a format we do not control; the cost of our own is drawing
  every picture ourselves forever.
- *Adopting the XState runtime (MIT).* Permitted by its licence and wrong for the host: the decision
  lives in C#, and running it would mean a JavaScript engine or a Node sidecar for a function that
  is thirty lines over a closed vocabulary.

## Risks / Trade-offs

- **[Risk] A repository ships a definition that quietly stops its own loop.** → Mitigation: the
  refusal is logged with the reason and the file path, and "declares no machine" is distinguishable
  from "declares a machine that matches nothing". The diagnosis has to be readable without a debugger,
  because the symptom — nothing happens — is identical to the watcher being switched off.
- **[Risk] Two checkouts on different commits decide differently.** → Mitigation: recorded above and
  in ADR-0005; the ordinary shape keeps the Folder on the trunk. Not eliminated.
- **[Risk] The vocabulary turns out too small and grows ad hoc.** → Mitigation: it is versioned in
  the file, and an unknown predicate is a refusal rather than a silent skip — a file written for a
  newer product says so instead of behaving as though the condition were false.
- **[Risk] Building on #316 before it merges.** → Mitigation: this change edits `LabelLaunchPlan` and
  `LabelWatcher`, which land there. It is declared as a blocker on the issue; if #316's shape changes
  in review, this change follows it rather than the reverse.
- **[Trade-off] A JSON file is less approachable than a markdown prompt.** Accepted: the prompts stay
  where they are and keep describing the work; what moves is only the order and the guards, which are
  precisely the part that was already unreadable because it was spread across six files.

## Migration Plan

1. Land the seam and its reader with no caller — inert, and covered by its own unit tests.
2. Add this repository's `.harness/lifecycle.json` describing the loop as it runs today.
3. Switch `LabelLaunchPlan` from the `switch` to the interpreter. The behaviour on this repository
   must be identical before and after, which is the test that says the translation was faithful.
4. Remove `CommandFor`.

Rollback is the revert of step 3: the switch is the last thing to go and the first thing to come
back.

## Open Questions

- ADR-0005's first question is untouched and still open: whether the harness or the agent runs the
  gate. It decides whether a transition can be fired by an exit code rather than by an agent's claim,
  and the vocabulary should not be frozen against it by accident.
- Whether the machine should eventually be read at a named ref rather than the working copy, once
  there is evidence of two checkouts disagreeing in practice.
