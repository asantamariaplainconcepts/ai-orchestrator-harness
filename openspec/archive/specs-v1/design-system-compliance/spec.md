# design-system-compliance Specification

## Purpose
The design contract every screen is held to: compose the kit, every value a token, never branch on the theme,
and all user-facing copy from the typed catalogue. The rules are checkable by a reviewer reading a diff rather
than re-derivable from taste. Reverse-engineered from the shipped code, not negotiated ahead of it.

## Requirements
### Requirement: Screens compose the kit, and the kit invents no values
UI work SHALL compose `src/frontend/shared/ui/kit.tsx` and the vendored `.pc-*` classes rather than inlining
styles. The kit SHALL be thin wrappers over those classes so the one mapping from "primary button" to
`.pc-btn .pc-btn--default` lives in a single place, and SHALL contain **no styling the design system does not
already define** — a wrapper that invented a value would be exactly the drift the vendored CSS exists to
prevent. Where the kit lacks what a screen needs, it SHALL be added to the kit and not to the screen. Inline
`style` SHALL be for layout only — flex, grid, gaps — and never for colour, radius, type or motion.

#### Scenario: A screen needing a new control gains it in the kit
- **WHEN** a screen needs a control the kit does not export
- **THEN** the control is added to `kit.tsx` and the screen composes it

#### Scenario: Layout inline, appearance never
- **WHEN** a component sets an inline style
- **THEN** it sets flex, grid or gap values, and no colour, radius, type or motion

### Requirement: Every value is a token and the vendored CSS is generated
Colour, type, radius and motion values SHALL be tokens: no raw hex, no raw font stack, no invented radius,
because a literal cannot theme. Layout pixel values SHALL be allowed. `src/frontend/styles/` SHALL be treated
as generated and vendored — edited upstream and re-imported, never patched in place.

#### Scenario: A hex literal is a bug
- **WHEN** a component writes `#2f69ff` instead of `--pc-blue-500`
- **THEN** it is a defect against this contract, whatever it looks like on screen

### Requirement: The theme is a class, and nothing reads it
`useTheme` SHALL follow the OS until somebody chooses otherwise, SHALL store that choice — a person who picked
dark did not mean "until I close the tab" — and SHALL put `.dark` on one ancestor and nothing else. No
component SHALL branch on the theme: it consumes the variable and the theme swaps its value. Both themes SHALL
be first-class and both SHALL be checked.

#### Scenario: A component never asks which theme it is in
- **WHEN** a component needs a colour that differs by theme
- **THEN** it consumes the semantic token and reads no theme state

### Requirement: All user-facing copy comes from the typed catalogue, in the house voice
Every user-facing string SHALL come from `src/frontend/shared/i18n/en.ts`; hardcoded text in a component SHALL
be a bug, because a second locale would not find it and neither would anybody auditing what the product says.
The catalogue SHALL be sentence case throughout, verb-first on buttons, with no emoji and no exclamation marks,
and empty and error states SHALL name the fix rather than report that something is wrong.

#### Scenario: A new label lands in the catalogue
- **WHEN** a screen gains a button
- **THEN** its text is a key in `en.ts` and the component reads it through `t()`

#### Scenario: An error state names the fix
- **WHEN** a screen has nothing to show or a request failed
- **THEN** the copy says what to do next rather than only what went wrong

### Requirement: The locked vocabulary is used exactly
Copy and code SHALL use the locked vocabulary exactly — Folder, Worktree, Task, Connector, Workflow, Hold, Run,
Agent — as defined in `README.md`. An Agent SHALL be a CLI in a terminal, never a "pod" or a "bot".

#### Scenario: A screen calls a worktree a worktree
- **WHEN** copy refers to a branch's own checkout
- **THEN** it says worktree, not "branch folder" or "workspace"

### Requirement: Interactive elements keep the shared focus treatment, and state has four families
Every interactive element SHALL show the shared focus treatment (`--focus-ring`, 3px at 50% of `--ring`), and
an outline SHALL NOT be removed without being replaced. State SHALL use the four functional families — error,
warning, success, info — each with a 100 tint, a 500 base and a 700 for text on the tint. Elevation SHALL be a
border or a surface change and never a shadow, motion SHALL be 150ms ease-out on colour, background, border,
opacity and transform only, hover and press SHALL be colour only, and backgrounds SHALL be flat — with the one
deliberate exception that the run transcript and command output sit on `--pc-neutral-900` in both themes,
because a terminal that inverts with the app stops reading as a terminal.

#### Scenario: Focus survives a restyle
- **WHEN** a control's appearance is changed
- **THEN** the focus ring is still shown, or replaced by an equivalent

#### Scenario: The terminal does not invert with the app
- **WHEN** the app is in light theme
- **THEN** the transcript surface stays dark
