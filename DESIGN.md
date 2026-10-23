# DESIGN.md — the design contract

Read this before any UI work. The system is **Platform Foundations**, from
[PlainConceptsPlatform/Foundations](https://github.com/PlainConceptsPlatform/Foundations), vendored
into [`src/frontend/styles/`](src/frontend/styles/). `tokens.css` and `components.css` are generated:
they carry a provenance header and sit in `.prettierignore`. To change a value, change it upstream
and re-import; a local edit is drift the next import silently reverts.

## Rules

1. **Compose the kit, do not inline styles.** Use `shared/ui/kit.tsx` and the `.pc-*` classes. If
   the kit lacks what a screen needs, add it there, not in the screen. Inline `style` is for layout,
   never for colour, radius, type or motion.
2. **Every value is a token.** No raw hex, no raw font stack, no invented radius: a literal cannot
   theme. Pixel values for layout are fine.
3. **Never branch on the theme.** [`useTheme`](src/frontend/shared/ui/useTheme.ts) puts `.dark` on
   one element and nothing else reads it. Both themes are first-class and both are checked.
4. **All user-facing copy comes from [`shared/i18n/en.ts`](src/frontend/shared/i18n/en.ts).**
   Hardcoded text is a bug: a second locale would not find it, and neither would an audit.
5. **Use the vocabulary exactly**, as [README.md](README.md#the-concepts) defines it.
6. **Every interactive element shows the shared focus treatment**: `--focus-ring`, 3px at 50% of
   `--ring`. Do not remove an outline without replacing it.
7. **Look before pushing**: the screen you changed, in both themes and at a narrow width.

## What the system looks like

- **Colour.** One brand hue, `--pc-blue-500`, for brand and primary actions; a cool-neutral ramp for
  the rest. Status uses error, warning, success and info, each with a 100 tint, a 500 base and a 700
  for text on the tint.
- **Type.** Outfit, weights 400 to 700; UI text 14px, meta 12px. JetBrains Mono for paths, branches,
  commands, keys and transcripts: the things a person would copy.
- **Elevation is a border plus a surface, never a shadow**: the shadow scale is transparent.
- **Corners.** Buttons and inputs 4px, `--radius` 6px for alerts and dialogs, cards 10px, badges round.
- **Motion.** 150ms ease-out on colour, background, border, opacity and transform only, and only
  after a state change. No scroll reveals, no count-ups. `prefers-reduced-motion` drops it to 1ms.
  Hover and press are colour only: nothing lifts, scales or grows a shadow.
- **Backgrounds are flat colour.** No gradients, imagery or blur. The one deliberate exception: a
  terminal sits on `--pc-neutral-900` in both themes, because one that inverts stops reading as a
  terminal.

## The screens

Seven screens and a tray, each answering one question. What each one holds, requirement by
requirement, is its capability in [`openspec/specs/`](openspec/specs/).

| Screen | The question it answers |
| --- | --- |
| PBIs | What work is there? One list of every PBI of every folder, grouped by state in the loop's order. Closed PBIs are a filter, not a screen, and there is no board. |
| PBI | Where is this PBI, and what is missing? A header with the state, the hold and its reason, the pull request and one button; five tabs. |
| Loop | What waits for me, what is moving, and what is the engine doing? The engine's present, the open PBIs in three bands (needs you, in motion, the rest), and its journal. |
| Metrics | How does the loop work this repository? Per folder, from Loop, for the last 7 or 30 days or all of it and one epic: the agents by step, what people did at each hold, the flow from label to done, and the notes that change how a number reads. Every number lists the PBIs or runs it counts. |
| Folder | How is this repository set up for the loop, and what does it lack? The engine's switch, the harness version against the one the app ships, the loop validated as the file has it, the harness file by file, where each agent state runs, and the checkout with its defaults. No work: no PBIs, runs or files. |
| Settings | What does the console work with? The tools on the machine, the editor, the engine's interval and stale threshold, updates, *Start at login* and *Stop console*. No folders: each has its page. No sources screen: the credential is the one `gh` holds. |
| Requirements | What does this machine lack for the loop? Each CLI it needs, the required ones first, present, missing or signed out, with the command that installs it here and a button that copies it. It opens on start until the required ones are present, and from Settings always. It installs nothing. |
| The tray | What is live right now? The live sessions, each once. A click opens the terminal over the screen and another closes it without stopping the run. *End*, beside the chip or over the terminal, kills the session after one line saying what dies. |

### The PBI

#### The one button

One place for an action, whose words change: three buttons are three chances to press the wrong one.

| The PBI is | The button says | What it does |
| --- | --- | --- |
| On hold | *Release hold* | The loop goes on to the hold's `next`, or asks which state when there is none |
| With a live loop run | *Stop run* | Kills the session after a confirmation; the run fails and the loop holds with "Stopped by a person" |
| In a state the loop does not declare, or none | *Move to…* | Puts the label the person chooses |
| Anywhere else | Nothing | The loop is on it |

#### Sessions by hand

Beside the button, *Terminal* opens a shell and *Explore with agent* an agent (one on the machine,
with a model; the folder's defaults preselected), on the PBI's branch. Both appear in the tray and
stay in the Journey as manual runs of the current state, with their recording; neither moves the
loop. From a folder's `…` menu the same two open in the checkout and leave nothing behind.

#### The tabs

| Tab | Question | What it has |
| --- | --- | --- |
| Journey | Where has it been, and where is it? | The states top to bottom with their runs. A run unfolds to its end record as `aio done` wrote it, the state it led to, its tokens and its recording. A score shows its band and reason. A run has no screen of its own. |
| Issue | What was asked, and what has been said since? | The vendor's issue, body and comments, with the harness's own comments marked. A comment written here is published as the person. |
| Proposal | What does the change say it will do? | The OpenSpec change on the PR, rendered with line numbers; a line takes a review comment. *Approve* releases `proposal-review`'s hold; *Request changes* sends the PBI back to `spec-ready`. |
| Review | What did it touch, and what was seen? | The diff with its threads, the checks, the quality of the change as `quality` measured it, and the evidence's captures. *Approve* approves at the vendor and releases `merge-review`'s hold; *Request changes* publishes the review, copies it to the issue and sends a held PBI back to `in-progress`. |
| Files | What is in the repository on this branch? | The branch's tree and a file to read and edit. |

The tab marked is the one the PBI's state names with `tab:` in the loop, so in this repository
Proposal is marked in `proposal-review`, Review in `code-review` and `merge-review`: "now", or
"needs you" when held.

#### Issue and Proposal show what is elsewhere

Neither copies nor summarises: the Issue is the vendor's and the Proposal is files on the branch, so
neither has an editor or a comment format of its own. What a person writes goes where an agent reads
it next, a comment on the issue or a review comment on a line. Images are shown inline and enlarged
with a click.

#### What the engine did and why

"What did this run do?" is its row in the Journey, its end record unsummarised. "What is the loop
doing, and what did it decide?" is Loop: the strip is the engine's present and the list its journal,
one line per decision with its reason. Both read what the engine wrote; no screen deduces.

#### Files

Always from a PBI, on its branch. When no checkout holds that branch, the tab says so and offers the
folder's checkout as it is. Save writes to disk; the commit is a person's or an agent's. No terminal,
no execution, no language: that is what *Open in editor* is for.

### The rail

PBIs with the number of open ones, and Loop with *N need you*. Below, the folders with their PBIs in
the loop and a `+` to add one; a folder's name opens its page and the chevron beside it folds its PBIs; a held PBI carries a pause, one with a live session a dot. Each
folder's `…` menu has *Terminal* and *Explore with agent*. The footer is Settings and the theme.

## Writing

The product's copy and this repository's prose share one voice.

- Plain declarative sentences. Claims are stated, not sold. Reasons travel with rules.
- Say what a thing is **not** when the boundary matters.
- Sentence case everywhere: headings, buttons, nav, table headers.
- Numbers are specific and unrounded: "2 modified", "step 3 of 5", "412 passed".
- Buttons are one or two words, verb first: "Release hold", "Stop run", "Add folder".
- Empty and error states **name the fix**: "No loop declared. Add .aio/loop.yaml to the
  repository." Not "No results".
- **No emoji, no exclamation marks, no rhetorical questions.**
