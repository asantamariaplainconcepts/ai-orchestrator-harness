## Context

`docs/design/current/` holds 23 PNGs. Eleven are 1440×900 and `capture.sh` retakes them. Twelve carry
`390` in the name and **nothing retakes those** — they were taken by hand, and `capture.sh` cannot
take them for two measured reasons written into `docs/design/README.md:24-31`: headless Chrome on
macOS lays a `--window-size=390,844` page out at 500×757 and scales into a 390px PNG, and
`capture.sh:142` actively refuses a dark shot on a luminance check.

Playwright is already in the repository for `AiOrchestrator.EndToEndTests`, it sets the viewport
through the browser's device-metrics override, and `AppFixture.Phone` (`AppFixture.cs:107`) already
holds 390×844. `PhoneTests` already drives four routes at that size and already asserts
`innerWidth == 390` on each. What did not exist anywhere in the tree: **a screenshot**. `grep -rn
'Screenshot' src/tests --include=*.cs` matched only Playwright's own bundled JavaScript, and
`grep -in 'theme|dark|light'` over the e2e project matched nothing at all.

So the gap this change closes is narrow and specific: the suite can already reach the width and prove
it; it could not photograph what it saw, and it had no way to reach the second theme.

## Goals / Non-Goals

**Goals:**

- The Change tab and each of its five steps in `docs/design/current/`, both themes, retakeable by one
  command.
- The width proved in the same run that takes the pictures, so a capture cannot silently become a
  shot of a 500px or a 1280px layout.
- The theme reached through the product's own control at a width where that control has moved.
- Retaking the set written into `/aio:implement`, so it happens where the app is already running.

**Non-Goals:**

- Fixing anything the set finds. Three findings came out of the first run and none is fixed here.
- Comparing screenshots. Nothing here fails because a label was reworded.
- Asserting what a panel says. The capture's content is whichever change is open on the machine.
- Adding a dependency, a route, a query, an error code, a migration or an i18n key.

## Decisions

**One test, ten files, and the tab's resting step is the tab's own file.** `change-390.png` is the
Change tab as it opens, which is the Proposal step; `change-code-390.png`, `-tests-`, `-evidence-`
and `-app-` are the other four, each with a `-dark` twin. A sixth `change-proposal-390.png` would be
byte-identical to the first and a second thing to keep in step. *Alternative rejected*: one file per
step plus a separate "whole tab" shot — there is no whole-tab view, the tab shows one step at a time.

**The PNGs are written straight into `docs/design/current/`, on every run, with no flag.**
`docs/design/README.md:11-15` already makes retaking the current set a step of `/aio:implement` and
argues that a stale "today" is worse than no screenshot: a producer that only runs when somebody
remembers a flag is the state this change is fixing. The cost is real and is stated as a debt: after
`aspire do ci` these ten files are modified in the working tree, because the capture's content is the
machine's own open change. *Alternatives rejected*: an environment variable (an escape hatch, and a
test that does nothing by default is a skipped test wearing a green tick); writing into the test's
own output directory (a record nobody can find is not a record).

**The step is reached by clicking the rail, because the step is not in the URL.** `ChangeTab.tsx:193`
holds it in `useState`; there is no `?step=`. So the driver clicks the *n*-th button of the rail —
found by its group role and `aria-label`, not by a layout selector — and reads `aria-pressed` back.
*Alternative rejected*: adding `?step=` to the tab so a capture could deep-link. That is product
code, and this ticket's boundary forbids it; it is also a worse product decision to take for a
screenshot's convenience.

**The theme is switched through the overflow menu, and the switch is read back.** Below 520px
`AppShell` folds four header icons into one menu (`AppShell.tsx:687`), so the theme button a wide
driver would click does not exist at 390 — the item is a `menuitem` named *"Switch between light and
dark"* inside the menu named *"Workflows, history, settings and theme"*. The theme is read from the
page first and toggled only if it is not the one wanted, because the starting theme is
`prefers-color-scheme` while nothing is stored and that belongs to the browser. Reading `.dark` back
afterwards is the positive half: a menu item that silently stopped toggling would otherwise produce a
light capture named `-dark`. *Alternative rejected*: Playwright's `ColorScheme` context option. It
would work — `useTheme` reads `matchMedia` — but it photographs a theme nobody chose through the
product, and `docs/design/README.md:36` names the header's control on purpose.

**The overflow measurement is the one `PhoneTests` already holds, widened rather than copied.**
`MeasureOverflow` is 45 lines of JavaScript carrying a documented rule about which elements count and
which scroll containers are exempt. A second copy is a second place that rule is written and free to
disagree. Five members of `PhoneTests` go from private to `internal`; no assertion and no behaviour
changes. *Alternative rejected*: lifting the script into a third file. A larger diff for the same
result, in a file two other tests already read.

**The panel is waited for by watching it stop changing, not by sleeping.** `NetworkIdle` answers
immediately when a click fires no request, and each panel reads its own query, so the frame after a
click is still the previous step's. Two consecutive equal readings of the body's text is the wait,
and the reading is returned so the five steps can be compared: five clicks must produce five
different screens, or one of the ten PNGs is a picture of the wrong panel. `drive.js` records making
exactly that mistake five times against the prototype, and each shot looked like evidence.

**The heading is deliberately not asserted here, and that is a measurement rather than a
preference.** The `h1` box is **172px** at this width. Measured, by writing candidate names into it
on a live page:

| heading | needs | verdict |
| --- | --- | --- |
| `ai-orchestator-new` | 172px | fits, with nothing to spare |
| `ai-orchestator-new master` | 209px | cut by 37px |
| `ai-orchestator-new change/262` | 247px | cut by 75px |
| `portal change/482-focus` | 206px | cut by 34px |
| `epic-288 epic/288` | ≤172px | fits |
| `agent-a1eb87343b0673987` | 199px | cut by 27px |

At a worktree scope the heading is the project's name *and* the branch (`AppShell.tsx:536-556`), and
172px does not hold both for any ordinary pair — `portal change/482-focus` is the example that file's
own comment gives. So a heading assertion on a worktree-scoped route measures the length of whatever
branch the machine happens to have, which is not a property of the layout. `PhoneTests` keeps its
heading assertion on its four routes, where the heading is the folder's own name, and this file
asserts where controls end instead.

**A precondition is asserted rather than reached.** The Change tab is worktree-scoped: it has no
version on a checkout. The worktree is read from the API — waited for, because a seeder cannot shell
out to git and the probe fills the rows in on its first sweep — and its absence fails with a sentence
saying why. *Alternative rejected*: creating a worktree through `CreateWorktree` or `git worktree
add`. The fixture goes to real lengths to touch nothing a developer is using (`AppFixture.cs:177-196`
is a paragraph about a broken database), and adding and removing a worktree in the developer's own
repository is a larger side effect than the one it would remove.

## Risks / Trade-offs

- **The gate leaves ten modified PNGs behind** → Stated, in `README.md` and in the debt report. It is
  the documented policy working: a change that moves this screen now shows it in the diff.
- **A fresh clone has no worktree, so this test fails there** → The failure says so in a sentence.
  Widening it later (the suite makes its own worktree) is a change that adds; narrowing a test that
  had been creating worktrees would be a change that withdraws.
- **The capture's content is the machine's** → Nothing is asserted about content. The measurements
  that *are* asserted — 390, controls inside the window, five distinct panels — hold on any machine.
- **Playwright's Chromium version moves and the pictures change** → Already true of the twelve PNGs
  this joins, and a picture nothing compares cannot fail for it.

## Migration Plan

None. Adding a test and ten files; reverting is deleting them.

## What the retake found

Four findings, reported with their captures and their numbers, and **none of them fixed here**.

1. **The step panel draws 2px of the 154px it asks for at 390.** The tab shows one step's panel at a
   time and at phone width it shows none of it: `flex: 1` with `minHeight: 0` inside a column with no
   free space to give resolves to a 2px strip between the rail and the footer, and `overflow: auto`
   clips the rest. Hiding the whole scope rail — the row of worktree chips above the tab strip —
   raises it only to 69px of 154px, so the worktree count is not the cause. At 1280 the same panel
   gets 131px of the 133px it wants. Measured on `8143b42`, before #296, so it is nobody's regression.
   Captures: all ten `change-*-390*.png`.

2. **#296's Merge change button squeezes the verdict row to 37px at 390.** Measured with the button
   and with it hidden on the same page: the fact column goes **180px → 37px** and the header box
   **238px → 585px**, which is 69% of an 844px viewport, with the seven facts rendering roughly one
   word per line. At 1280 the header is 88px either way, so nothing about it is visible at the width
   the button was reviewed at. This is #217's own defect returning — an action side spending the row
   while the facts beside it are squeezed — and #217's numbers are quoted in `AppShell.tsx` for the
   header's version of it. Captures: `change-390.png`, `change-code-390.png` and their dark twins.

3. **The header's heading is cut whenever the folder screen is scoped to a worktree.** The `h1` box is
   172px; `ai-orchestator-new master` needs 209px, `portal change/482-focus` needs 206px. See the
   table in Decisions. This is the mechanism behind the two `PhoneTests` failures every worktree of
   this repository sees, and it is a real finding rather than an artefact of long directory names.

4. **A badge naming issue #262 sits beside the sentence "No issue resolved for this worktree".** The
   badge is resolved from the branch and the sentence is drawn because the mirror has no title for it
   yet, so the screen makes two contradictory claims at once. Not width-specific; found by running.
   Capture: `change-390.png`.

## Open Questions

None.
