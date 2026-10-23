# folder-page Specification

## Purpose
Each folder has a page that answers how its checkout is set up for the loop and what it lacks. It has
no work on it: no PBI, no run and no file of the repository. Written with the code that ships it
(`src/frontend/features/folder/`, the screen and one file per block,
`src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/UseCases/ReadFolderPage.cs`,
`ReadFolderStates.cs`); installing the harness from it is #566's, in the harness-package capability.

## Requirements
### Requirement: The folder's name in the rail opens its page
The route `/folders/:folderId` SHALL show the folder's page, loaded on its own. The folder's name in the
rail SHALL be a link to it, and the chevron beside the name SHALL show or hide the folder's PBIs as
before. A folder added from the rail's `+` SHALL open on its page. The header SHALL name the folder,
its repository and vendor, and offer *Terminal* and *Explore with agent* on the checkout.

#### Scenario: Opening a folder
- **WHEN** a person clicks the name of a folder in the rail
- **THEN** its page opens, and its PBIs stay listed under it in the rail

### Requirement: The page is read from the checkout on every request
`GET /api/folders/{id}` SHALL answer the folder's name and path, its vendor and repository when its
origin is on github.com, the branch git reads now, its default agent and model, its
`.aio/loop.yaml` as written or none, the first line of its `.aio/version` or none, the harness
version this build carries, each file and link of the package with whether the checkout has it, and
for each agent CLI whose adapter asks to trust a folder, whether it trusts this one, the sentence to
show and the command that asks the question once.
`.aio/gate` SHALL be marked as the stub while it is byte for byte the package's. Nothing
SHALL be cached between two requests. An unknown id SHALL be 404 `Folder.NotFound`.

#### Scenario: A file edited on disk
- **WHEN** a person changes `.aio/loop.yaml` in the checkout
- **THEN** the next `GET /api/folders/{id}` answers the new text

### Requirement: The strip says the engine, the harness version and what is missing
Above the blocks, the page SHALL show three cells. *Engine*: a switch that sends
`PUT /api/loop/engine {repository, on}`, with "On · looks every <n> s" and the last pass, or "Off";
a folder the engine does not walk has no switch and says why, and a folder without a loop shows the
switch blocked with "No loop declared. Add .aio/loop.yaml to the repository." *Harness*: the installed version against
the one the app ships, or "Not installed", with *Install harness* when there is none and *Reinstall* when
the app ships another version, each sending `POST /api/folders/{id}/harness/install` and naming the
repository and the branch it opens the pull request from. While `GET /api/folders/{id}/harness/install`
answers an open pull request, polled every 15 seconds, the cell SHALL link it in place of the button, and
the engine cell SHALL say to switch it on once the pull request is merged in place of a switch that is
off. *Ready for the loop*: each thing the folder lacks, in this order: an origin on
github.com, a loop, a loop that validates (with its problems), a gate, a gate that is not the stub, an
installed harness, the package's other files, and each CLI below that does not trust the folder; or
"Nothing missing". For each CLI that asks to trust a folder and that the folder runs, as its default
agent or a provider its loop names, the strip SHALL add a cell *Trust · <provider>* that says it trusts
the folder, or that it does not and its runs wait, with the command to run once in a terminal. The
page SHALL NOT answer the question for the person.

#### Scenario: The install pull request is open
- **WHEN** a person has pressed *Install harness* and the pull request is not merged
- **THEN** the harness cell links the pull request, and the engine cell offers no switch

#### Scenario: A repository still on the gate stub
- **WHEN** a folder's `.aio/gate` is the one the package installs
- **THEN** the strip counts it as missing and says every PBI holds in verifying until it is edited

#### Scenario: A folder Claude Code does not trust
- **WHEN** the folder's default agent is `claude-code` and `~/.claude.json` trusts neither the checkout
  nor a directory between it and the repository's top
- **THEN** the strip lists it as missing, and its *Trust · claude-code* cell shows `cd '<path>' && claude`

### Requirement: The loop is validated again when it changes on disk
The page SHALL read the folder every 2 seconds and validate its loop with `POST /api/loop/validate`
whenever the text changes, without a reload. The loop block SHALL show the file as written,
read-only, "Valid · <n> states" or "Does not validate" with every problem `ValidateLoop` gives, and
*Open in editor*. The console SHALL NOT write the loop.

#### Scenario: Breaking the loop in an editor
- **WHEN** a person removes a required `max_loops` from `.aio/loop.yaml` and saves
- **THEN** within seconds the page says "Does not validate" with ValidateLoop's problem, and the strip lists it

### Requirement: The harness block lists the package file by file, and the states at the vendor
The harness block SHALL list the package grouped by directory, with `.aio/loop.yaml` and
`.aio/gate` on rows of their own, each with what is missing. Its last row SHALL be the
loop's states the vendor already has, from `GET /api/folders/{id}/states`, which asks the folder's
vendor adapter and answers the states, or the problem when it cannot; a declared state the vendor lacks
SHALL be named, with the note that the engine creates it on the first move there.

#### Scenario: A state not yet at the vendor
- **WHEN** the loop declares `rebasing` and the repository has no label for it yet
- **THEN** the row says it is not yet on github and that the engine creates it on the first move there

### Requirement: The checkout block holds the defaults and the removal
The checkout block SHALL show the path, the branch, the default agent and model to change with
`PUT /api/folders/{id}/defaults`, and *Remove this folder*, which after a confirmation forgets it and
returns to PBIs.

#### Scenario: Changing the default model
- **WHEN** a person sets the model to `claude-opus-5-5` on the folder's page and saves
- **THEN** *Explore with agent* on that folder preselects it

### Requirement: The environments block places each agent state
Between the harness and the checkout, the page SHALL show one row per state of the loop that runs an
agent, as `/api/loop/validate` marks it with `agent`, with a selector of the environments
`/api/machine` lists, set to the folder's choice from `GET /api/runs/environments` or else to the
default environment. A change SHALL be stored at once with `PUT /api/runs/environments`. A folder with
no repository, a loop that does not validate and a loop with no agent state SHALL each say so instead
of the rows, and a stored environment the machine no longer has SHALL show as not on this machine.
For an environment some state runs in, the block SHALL show the note `/api/machine` gives for it, as a
caution when it is offered and as unavailable when it is not, with its command.

#### Scenario: sbx with the global GitHub token
- **WHEN** a person places `in-progress` in `sbx` and sbx holds only its global `github` secret
- **THEN** beside the selector the block says the token reaches all their repositories and shows the command that stores a fine-grained one for the PBI's sandbox

#### Scenario: One environment on the machine
- **WHEN** the loop's `ready-for-proposal` runs an agent and the machine has only `worktree`
- **THEN** its row shows `worktree` as the one option, and no row is shown for its states of scripts
