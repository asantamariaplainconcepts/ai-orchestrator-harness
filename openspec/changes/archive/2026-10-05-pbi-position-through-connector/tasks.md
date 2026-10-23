## 1. Contract on the seam

- [x] 1.1 Create `LoopPosition` (closed enum, BuildingBlocks/World, divine position order) and the position answer type: known position + hold, or unknown with reason
      req: pbi-position-connector/1, the-world-seam/1
      files: src/shared/AiOrchestrator.BuildingBlocks/World/IPbiSource.cs
- [x] 1.2 Add `GetPosition` to `IPbiSource` and its XML doc — answers a position, never an exception for "cannot say"
      req: pbi-position-connector/1, the-world-seam/1
      files: src/shared/AiOrchestrator.BuildingBlocks/World/IPbiSource.cs
- [x] 1.3 Add `MoveTo` to `IPbiSource` and its XML doc — one position move, `unknown` refused at the contract
      req: pbi-position-connector/2, the-world-seam/1
      files: src/shared/AiOrchestrator.BuildingBlocks/World/IPbiSource.cs
- [x] 1.4 Keep `World.cs` append-only: registrations only at the end of the block that owns them (AGENTS.md rule 1)
      req: the-world-seam/1
      files: src/shared/AiOrchestrator.Infrastructure/World.cs

## 2. Adapters

- [x] 2.1 `GitHubPbiSource`: translation table `status:kebab` ↔ `LoopPosition`, covering all ten declared positions, precedence documented in code where the loop declares it (furthest along wins)
      req: pbi-position-connector/3, pbi-status-and-holds/1, pbi-status-and-holds/3
      files: src/shared/AiOrchestrator.Infrastructure/PbiSources/GitHubPbiSource.cs
- [x] 2.2 `GitHubPbiSource.GetPosition`: read labels, return position + hold flag, no-loop-label → unknown; unrecognised label changes nothing, extract the misparse tests carried over from `ListPbis.Handler.Today`'s table
      req: pbi-position-connector/1, pbi-position-connector/3
      files: src/shared/AiOrchestrator.Infrastructure/PbiSources/GitHubPbiSource.cs, src/tests/AiOrchestrator.Modules.Pbis.FunctionalTests/StatusTests.cs, src/tests/AiOrchestrator.Modules.Pbis.FunctionalTests/StatusWriteTests.cs
- [x] 2.3 `GitHubPbiSource.MoveTo`: new label written before the old ones are removed, all other loop labels removed, `status:holding` alone, write nothing when already there, `unknown` target refused
      req: pbi-position-connector/2, pbi-status-and-holds/2
      files: src/shared/AiOrchestrator.Infrastructure/PbiSources/GitHubPbiSource.cs, src/tests/AiOrchestrator.Modules.Pbis.FunctionalTests/StatusWriteTests.cs
- [x] 2.4 `AzureDevOpsPbiSource`: `System.State` → `LoopPosition` mapping table; `GetPosition` reads column 270 area, unmapped state → unknown with raw value, `MoveTo` writes state column when mapping exists, refuses with remedy otherwise
      req: pbi-position-connector/4
      files: src/shared/AiOrchestrator.Infrastructure/PbiSources/AzureDevOpsPbiSource.cs
- [x] 2.5 `JiraPbiSource` and `LinearPbiSource`: `GetPosition` answers unknown with reason; `MoveTo` refuses with remedy sentence; no tag, label or field write happens in either
      req: pbi-position-connector/5
      files: src/shared/AiOrchestrator.Infrastructure/PbiSources/JiraPbiSource.cs, src/shared/AiOrchestrator.Infrastructure/PbiSources/LinearPbiSource.cs, src/tests/AiOrchestrator.Modules.Pbis.FunctionalTests/PbisFixture.cs
- [x] 2.6 Tests carry over today's `pbi-status-and-holds` scenarios (furthest-along, unrecognised label, hold survives move, new-before-old, moving-to-same writes nothing) and new ones for the three new positions and unknown
      req: pbi-position-connector/1, pbi-position-connector/2, pbi-position-connector/3, pbi-position-connector/5, pbi-status-and-holds/1, pbi-status-and-holds/2
      files: src/tests/AiOrchestrator.Modules.Pbis.FunctionalTests/StatusTests.cs, src/tests/AiOrchestrator.Modules.Pbis.FunctionalTests/StatusWriteTests.cs, src/tests/AiOrchestrator.Modules.Pbis.FunctionalTests/HoldWriteTests.cs, src/tests/AiOrchestrator.Modules.Pbis.FunctionalTests/InboxTests.cs, src/tests/AiOrchestrator.Modules.Pbis.FunctionalTests/PbisFixture.cs

## 3. Module

- [x] 3.1 `ListPbis.Handler`: stop maintaining the dictionary, `StatusOf`, `HeldBy`, `Hold` const and `LabelFor`/`Name`/`Parse` helpers; keep only the enum→wire-string projection, called against the Connector's answer. No exception, no literal `status:` in the module
      req: pbi-status-and-holds/1, the-world-seam/1
      files: src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Inbox/UseCases/ListPbis.cs, src/modules/Pbis/AiOrchestrator.Modules.Pbis/Domain/LoopStatus.cs
- [x] 3.2 `GetPbi` and `ListPbis`: answer `PbiStatus` (wire string, now including `unknown`) from the Connector's read; the response keeps its shape — the watcher consumes the same string
      req: pbi-status-and-holds/1, pbi-details/1
      files: src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Inbox/UseCases/ListPbis.cs, src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Inbox/UseCases/GetPbi.cs, src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Inbox/PbiSnapshot.cs
- [x] 3.3 `SetPbiStatus`: resolve PBI (source + repository + key, as today), call `MoveTo`, keep permission, admission and hold rules and routes; report a vendor refusal in its own words as a 4xx; `unknown` refused before any vendor call
      req: pbi-position-connector/2, pbi-status-and-holds/2
      files: src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Inbox/UseCases/SetPbiStatus.cs
- [x] 3.4 `SetPbiHold`: release-only semantics preserved, made through the translated hold release at the adapter (GitHub label removal); nothing else written, unchanged 404 and repository disambiguation
      req: pbi-status-and-holds/4
      files: src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Inbox/UseCases/SetPbiHold.cs
- [x] 3.5 Snapshot: store the position the vendor's answer gave; a vendor whose position read fails keeps the rest of its PBIs with the position unknown and the failure named; no write-back path added
      req: read-the-vendors/1
      files: src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Inbox/PbiSnapshot.cs, src/shared/AiOrchestrator.BuildingBlocks/World/IPbiSource.cs
- [x] 3.6 Verify MOD001–005, CQS001 and ArchitectureTests: no cross-module reference, the contract type internal to BuildingBlocks, a module that no longer references any label string
      req: the-world-seam/1
      files: src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Inbox/UseCases/ListPbis.cs, src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs

## 4. Wire and UI

- [x] 4.1 `contracts.ts`: open `PbiStatus` to the declared vocabulary + `"unknown"` (append in this change's block, not in a neighbour's)
      req: pbi-status-and-holds/1, pbi-details/1
      files: src/frontend/shared/http/contracts.ts
- [x] 4.2 `en.ts`: copy for `unknown` position and its reason, beside the existing lanes; no literal `status:{...}` left in the screen
      req: pbi-status-and-holds/1, pbi-status-and-holds/3, pbi-details/1
      files: src/frontend/shared/i18n/en.ts, src/frontend/features/pbis/PbisScreen.tsx
- [x] 4.3 `PbisScreen.tsx` board: unknown renders as its own state (not a lane, not a `backlog` card), not an error; no drag target offers `unknown`; the three new positions have lanes with their heading from `en.ts`, not from a `status:` string
      req: pbi-position-connector/1, pbi-status-and-holds/3, pbi-details/1
      files: src/frontend/features/pbis/PbisScreen.tsx, src/frontend/features/folders/LoopFlow.tsx
- [x] 4.4 Queries/cache: a PBI whose status is `unknown` does not collide between repositories in the query cache (read-the-vendors rule, browser side)
      req: read-the-vendors/1
      files: src/frontend/shared/http/contracts.ts, src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Inbox/UseCases/ListPbis.cs
- [x] 4.5 Frontend verifies tokens and copy, no theme branch, keyboard accessible, double theme and narrow session of the board and detail states
      req: pbi-details/1, pbi-status-and-holds/3
      files: src/frontend/features/pbis/PbisScreen.tsx

## 5. Loop-declared vocabulary

- [x] 5.1 Confirm the set the loop declares (`.harness/issue-loop.yaml`, `x-harness: { status: … }`) and that the contract's vocabulary matches it byte-for-byte — the seven prior plus `needs-refinement`, `proposal-review`, `blocked`, exclusive of `unknown`
      req: pbi-position-connector/3, pbi-status-and-holds/3
      files: src/shared/AiOrchestrator.Infrastructure/PbiSources/GitHubPbiSource.cs
- [x] 5.2 Confirm the proposal extends no exclusive sequence of this repo: a loop that declares fewer positions keeps reading correctly (positions absent from the declaration are not offered in this folder's menus)
      req: pbi-status-and-holds/3
      files: src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs

## 6. Verification

- [x] 6.1 Focused tests run and documented, but the gate runs in `/aio:implement`'s phase with the full number it counted, not here
      req: pbi-position-connector/1, pbi-position-connector/2, pbi-position-connector/3, pbi-position-connector/5, pbi-status-and-holds/1, pbi-status-and-holds/2, pbi-status-and-holds/4
      files: src/tests/AiOrchestrator.Modules.Pbis.FunctionalTests/StatusTests.cs, src/tests/AiOrchestrator.Modules.Pbis.FunctionalTests/StatusWriteTests.cs, src/tests/AiOrchestrator.Modules.Pbis.FunctionalTests/HoldWriteTests.cs
- [x] 6.2 Manual check with a real authorized PBI: read position and hold, change the state at the vendor (browser), verify the next read reflects it without manual sync if within the webhook flow
      req: pbi-position-connector/1, pbi-position-connector/3
      files: src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Inbox/UseCases/GetPbi.cs
- [x] 6.3 Check pending: register what was observed with the PBI of proof — one allowed transition and one refused — and run `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj` in `/aio:implement`, recording the numbers it printed
      req: pbi-position-connector/1, pbi-position-connector/2, pbi-position-connector/3
      files: src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Inbox/UseCases/GetPbi.cs
