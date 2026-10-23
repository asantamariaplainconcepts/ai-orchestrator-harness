## 1. The measurement the new file reuses

- [x] 1.1 `PhoneTests.cs` — `MeasureOverflow`, its `Overflow`/`Heading`/`Past` records, `Settled` and
      the two `Read` helpers widened from private to `internal`. Visibility only: no assertion, no
      script and no behaviour changes, so the four existing routes measure exactly as before. A
      second copy of that 45-line script would be a second place its rule about scroll containers is
      written
      req: the-narrow-design-set/4
      files: src/tests/AiOrchestrator.EndToEndTests/PhoneTests.cs

## 2. The capture

- [x] 2.1 `NarrowSetTests.cs` — the worktree read from the API and waited for (a seeder cannot shell
      out to git; the probe fills the rows in on its first sweep), the route
      `/folders/<id>?worktree=<path>&tab=change` opened at `AppFixture.Phone`, and the rail found by
      its group role rather than by a layout selector
      req: the-narrow-design-set/1
      files: src/tests/AiOrchestrator.EndToEndTests/NarrowSetTests.cs
- [x] 2.2 `NarrowSetTests.cs` — `innerWidth` asserted to be 390 inside the loop, before anything it
      measures. This is the assertion that separates the set from anything headless Chrome can take
      req: the-narrow-design-set/2
      files: src/tests/AiOrchestrator.EndToEndTests/NarrowSetTests.cs
- [x] 2.3 `NarrowSetTests.cs` — `Wearing`, which reads the theme off the page, opens the overflow
      menu when it has to, chooses the theme item, waits for the menu to detach and reads the class
      back. At 390 the header's theme button does not exist, so the menu is the only path
      req: the-narrow-design-set/3
      files: src/tests/AiOrchestrator.EndToEndTests/NarrowSetTests.cs
- [x] 2.4 `NarrowSetTests.cs` — `Steady`, two consecutive equal readings of the screen rather than a
      sleep, and the five readings compared: five clicks must produce five different screens
      req: the-narrow-design-set/4
      files: src/tests/AiOrchestrator.EndToEndTests/NarrowSetTests.cs
- [x] 2.5 `NarrowSetTests.cs` — the ten `page.ScreenshotAsync` calls into `docs/design/current/`,
      the path resolved through `SelfCheckout.Root()` rather than counted in `..` segments, and each
      file checked to be big enough to be a screen. Every shot is framed on the tab's own top first
      (`FrameTheTab`): without it the framing is wherever the last click left the scroller, and the
      first capture — the one that is meant to be the tab at 390 — began half-way down and cut off
      the header's action box, which is the control behind finding 2
      req: the-narrow-design-set/1
      files: src/tests/AiOrchestrator.EndToEndTests/NarrowSetTests.cs
- [x] 2.6 `NarrowSetTests.cs` — the per-capture measurement written to the test's output (heading
      width against its box, control count, what is past the edge), so whoever retakes the set reads
      the numbers behind each picture with `--logger "console;verbosity=detailed"`
      req: the-narrow-design-set/4, the-narrow-design-set/6
      files: src/tests/AiOrchestrator.EndToEndTests/NarrowSetTests.cs

## 3. The record

- [x] 3.1 `docs/design/current/` — the ten PNGs, committed
      req: the-narrow-design-set/1
      files: docs/design/current/change-390.png
- [x] 3.2 `docs/design/README.md` — what the narrow set covers now, and the one command that retakes
      it. The two measured reasons `capture.sh` is not it stay where they are
      req: the-narrow-design-set/1, the-narrow-design-set/2
      files: docs/design/README.md
- [x] 3.3 `.ai/commands/aio/implement.md` — retaking the set as a step, appended beside the
      annotation step #292 added rather than restructuring the file. One source, symlinked to three
      runtimes
      req: the-narrow-design-set/5
      files: .ai/commands/aio/implement.md

## 4. What the set found

- [x] 4.1 The four findings reported with their captures and their numbers, and **none of them
      fixed here**: the step panel drawing 2px of the 154px it asks for at 390, #296's merge button
      squeezing the fact column from 180px to 37px, the heading cut at a worktree scope, and a badge
      naming an issue beside a sentence saying none was resolved
      req: the-narrow-design-set/6
      files: openspec/changes/the-narrow-set-reaches-the-change-tab/design.md
- [x] 4.2 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj` —
      **727 tests, 7 assemblies, 2 failures, pipeline 1m 10s**, EndToEnd grown 9 → 10. Both failures
      are the pre-existing ones every worktree of this repository sees: `PhoneTests` asserts a
      heading fits 390px and the worktree's directory name is what renders into it —
      `"agent-a1eb87343b0673987" needs 199px and has 172px`, which is the third row of the table in
      `design.md`. `epic-288` fits, which is why the same two pass on the shared branch. Neither is
      loosened and neither is this change's; `Runs.FunctionalTests` ran 62/62
      req: the-narrow-design-set/1
      files: src/tests/AiOrchestrator.EndToEndTests/NarrowSetTests.cs
