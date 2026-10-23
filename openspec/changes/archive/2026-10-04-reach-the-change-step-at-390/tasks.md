## 1. Layout

- [x] 1.1 Measure the Change tab's own root with `useNarrowContainer(560)` — the convention
       `ProposalStep` and `TasksScreen` already hold — and, when narrow, lay the tab out as one
       scrollable column: the root stops demanding the panel be `flex: 1`, the ancestors' clipping
       keeps its horizontal job only, and header, rail, panel and footer scroll together.
      req: app-shell-and-navigation/1
      files: src/frontend/features/folders/ChangeTab.tsx
- [x] 1.2 When narrow, make the step panel's root `overflow: visible` so the column is the one
       scroller on the chain; when wide, keep today's `overflow: auto` and fixed header row exactly.
      req: app-shell-and-navigation/1
      files: src/frontend/features/folders/ChangeTab.tsx

## 2. The narrow set

- [x] 2.1 In `NarrowSetTests`, after each step's capture and in both themes, bring the selected
       step's first panel row into view through the page's own scroller (Playwright
       `ScrollIntoViewIfNeeded` on the panel's header row) and assert its bounding rect intersects
       the viewport, naming the step in the failure.
      req: the-narrow-design-set/1
      files: src/tests/AiOrchestrator.EndToEndTests/NarrowSetTests.cs

## 3. Verification

- [x] 3.1 Run `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`
       and record the assemblies it counted; the reachability assertion runs inside it.
      files: src/tests/AiOrchestrator.EndToEndTests/NarrowSetTests.cs
- [x] 3.2 Retake the narrow set (`change-*-390.png`, both themes) into ignored artifacts, confirm
       the panel is visible in the Evidence step's capture where it was not, and stage the captures
       under the `evidence-427-` convention.
      files: src/artifacts/playwright/design/
