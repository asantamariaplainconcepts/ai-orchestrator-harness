## 1. The resolution

- [x] 1.1 In `MergeGate.tsx`, change `taskFor` to take the branch and return `{ key, source }` where
      source is `run` or `branch`. Keep it the single answer both surfaces read, which is what #185
      made it for.
- [x] 1.2 Propose the candidate from the **last run of digits** in the branch name, and nothing when
      there is none. No pattern for `change/<n>`, and no entry in `.harness/config.json`.
- [x] 1.3 Point the Seam tile at the new answer. The confirmation is the `useTask(key, repository)`
      call it already makes: a candidate the mirror does not hold answers 404 and the tile keeps its
      unknown state.

## 2. The screen

- [x] 2.1 `IssueTab.tsx` takes the branch and uses the same answer.
- [x] 2.2 Say when the key was derived, in one line above the issue. A derived answer is a guess the
      mirror agreed with, not something observed in that directory.
- [x] 2.3 Add the branch prop at the `<IssueTab>` call in `FolderDetailScreen.tsx` — that one line
      only. **Not `fixedTabs`, not the `Tab` union**, which `change/264` is in flight over; and say on
      #275 that its `files` prop lands on the same line.
- [x] 2.4 `shared/i18n/en.ts`: own block, contiguous, appended.

## 3. Tests

- [x] 3.1 The resolution moved to its own module, `features/folders/task.ts`, beside `seam.ts` and
      `check.ts` — where this repository already keeps pure readings of this kind, and what makes it
      runnable outside a browser. Exercised with `node --experimental-strip-types` against **nine**
      shapes, six of them the real data on this machine: `change/174` with two runs of `#104`,
      `change/6` → `#104`, `change/182` → `#174`, `change/275` with no runs, `master`,
      `fix/narrow-layout`, `feature/2026/issue-275`, `release/24`, and no branch at all. All nine
      pass.
- [x] 3.2 **No test is committed, and that is the honest answer.** The frontend has no runner —
      `package.json` declares `dev`, `build`, `preview`, `lint`, `typecheck` and `format:check` — so a
      test file here would be a file nothing executes. The nine shapes above were run against the real
      module, not against a copy of it; the script is in the check line rather than in the tree.

## 4. The gate and the evidence

- [x] 4.1 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj` —
      full assembly count, zero failures.
- [x] 4.2 `openspec validate "resolve-a-worktrees-task-from-its-branch" --strict`.
- [x] 4.3 **Run the product and look at it**, which this change is what makes possible. Opened
      `change/275` — zero runs — in the app: the Issue tab draws issue #275, its labels, its held
      badge, its body and its 6 comments, above the line *«Resolved from the branch change/275. No run
      has been recorded in this worktree.»* Before this change that tab said *«No run has worked here
      yet»*. This is the observation #275's own task 5.3 could not make.
      **The Seam tile still reads Unknown**, and not because of this change: the mirror truncates the
      body at 4,000 characters and #275's `## Seam` sits past the cut. Measured, and written up below.
- [x] 4.4 The regression this must not cause. `change/174` has been merged and its worktree is gone,
      so it cannot be opened — it was exercised against the module directly instead, with the real
      key and branch: two runs of `#104` on branch `change/174` resolve `#104` from the run, not
      `#174` from the branch. Same for `change/6` → `#104` and `change/182` → `#174`.
- [x] 4.5 Both themes and a narrow width — one of the two, and the other is a finding rather than a
      skip.
      **Both themes: done.** The dark shot needs `--blink-settings=preferredColorScheme=0`. The
      repository's own note says both themes cannot be checked with Chrome headless at all, on the
      strength of `=1` rendering byte-identical to light; `=1` is not the dark end of that enum and
      `=0` is.
      **Narrow width: not checkable for this tab, and that is structural.** A 390 shot from Chrome
      headless is an artefact here — it lays out at 500 and scales — and the reliable route is
      `AppFixture.Phone` in `AiOrchestrator.EndToEndTests`. That suite has phone journeys for
      `/folders`, `/folders/{id}`, `?tab=diff` and a run, and **cannot have one for the Issue tab**:
      the tab is worktree-scoped and `WorkspaceSeed` creates no worktrees on purpose — *"a seeder
      cannot ask git"*. So the one surface this change touches is the one the narrow set cannot
      reach. What is checkable was checked: the addition is a single `<span>` inside a column that
      already existed, its colour is `--muted-foreground`, and nothing added reads the theme.
      **This becomes possible once this change merges**, which is worth its own issue: a branch is now
      enough to resolve a task, so a phone journey over the Issue tab no longer needs a run.
- [x] 4.6 What the bundle grew by, in KB.

## What running it taught

**The mirror truncates an issue body to 4,000 characters, and two parsers read the truncated one.**
`SourcedText.Trim` (`limit = 4_000`) was written when the body was only ever shown as a paragraph —
its own doc says so. Since #149 and #185 the body is *parsed*: `parseSeam` looks for `## Seam` and
`parseCheck` for `## El check`, and on a long issue both sections live past the cut.

Measured: **32 of the 60 most recent issues in this repository exceed 4,000 characters**; #275 is
13,121. All three live worktrees are over it. So on #275 the Seam tile still reads *Unknown* and the
check list still says the issue does not say how anybody would know it is done — both false, and
neither caused by this change. The column already allows 20,000 (`HasMaxLength(20000)`), so the
schema is not what is limiting it.

Named here and opened as its own issue rather than widened quietly: the bound has a stated reason,
and changing it is a decision about what the mirror is for.
