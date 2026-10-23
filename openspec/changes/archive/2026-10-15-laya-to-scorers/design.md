## Context

`laya` and `laya-requirements.txt` sit at the root of `.harness/aio/` because five things assume a flat folder (measured in #759): `HarnessScripts.Find` looks for a flat name in `.harness`, `.harness/aio` and `.harness/scripts`; `HarnessPackage.Executable` gives the execute bit only to an extension-less file straight in `aio/`; `package.props` embeds `aio\*` without recursion; `HarnessPackage.Former` maps a file in a subfolder of `aio/` to `.harness/<rest>`; and an install writes its entries and removes only `.harness/scripts/`, so an old `aio/laya` would survive it. The engine starts laya itself: `StateScripts.EnsureModel` asks `Find` for `laya` and runs `laya ensure` once a pass before the first score (#596). The script finds its requirements beside itself (`here=$(dirname "$0")`), and its pin is the SHA-256 of their content.

## Goals / Non-Goals

**Goals:**
- Opening `.harness/aio/` shows the loop: the steps flat, and each tool a step uses in a folder of its own.
- laya runs from `.harness/aio/scorers/laya/`, executable, on the same pin.
- An install over the old layout leaves only the new one, with no step by hand.
- The README names every folder of `aio/`, and a test keeps it so.

**Non-Goals:**
- How `loop.yaml` resolves names, or moving any step into a subfolder.
- What laya does.

## Decisions

**D1. `Find` knows where each tool lives, and looks there first.** `HarnessScripts` gains `Scorers = ".harness/aio/scorers"` and a map of tools to their folder, `laya` → `.harness/aio/scorers/laya`. `Find(checkout, name)` for a name in the map looks in that folder first and then in `Folders`, as any script; any other name is found as today. So `StateScripts.EnsureModel` and `LocalScriptRunner` keep asking for the name `laya`, a name still never carries a slash, and an `aio/laya` at the root is used only where the new one is not, which is a repository that has not installed 36. Changed after the person's review on #759: finding laya only in its folder left every such repository unscored (`unknown`) until it installed, and the issue asks that the new place be the package's, not that the old one stop working. *Alternative:* add `.harness/aio/scorers/<name>` to `Folders` for every name: it would find an old `aio/laya` first, and it would let any step hide in a scorer's folder.

**D2. The execute bit follows the same shape.** `Executable` is true for an extension-less file straight in `aio/`, or straight in `aio/scorers/<tool>/`. `laya-requirements.txt` has an extension and stays plain.

**D3. `package.props` embeds `aio\scorers\laya\*`** with the logical name `harness/.harness/aio/scorers/laya/%(Filename)%(Extension)`, one line beside `lib\*`, `checks\*` and `schemas\*`. `aio\*` still does not recurse.

**D4. The old files move by `Was`.** `Former` maps `.harness/aio/scorers/<tool>/<file>` to `.harness/aio/<file>`, the harness 24–34 layout, so git shows a rename. A harness-23 repository had laya in `.harness/scripts/`, which goes whole as today, and its requirements at `.harness/laya-requirements.txt`, which no `Was` names any more and which stays. Changed after the person's review on #759: no repository is on 23 (aio-dogfood is on 30, this one on 34), and teaching `Removed` to delete a file would change `GitBranchPublisher` and `IBranchPublisher`, which the issue does not name, to take away a file nothing reads.

**D5. The README test reads the folders from the package.** The table row for `aio/` names each folder of `.harness/aio/` the package carries (`checks/`, `lib/`, `schemas/`, `scorers/laya/`), and `HarnessPackageTests` takes the folders from `HarnessPackage.Files` and fails naming the one the row leaves out.

## Risks / Trade-offs

- [A repository on 34 or earlier keeps `aio/laya`, and the engine still starts it] → only where `scorers/laya/laya` is not; the install moves it, so after the merge there is no `laya` at the root of `aio/` to find.
- [A repository still on 23 keeps a stray `.harness/laya-requirements.txt` after installing] → nothing reads it; a person removes it when they see it.
