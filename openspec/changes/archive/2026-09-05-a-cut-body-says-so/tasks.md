## 1. The trim, and the fact it now answers

- [x] 1.1 Raise `SourcedText.Trim`'s limit to 20,000 and return `TrimmedText(string Text, bool Truncated)`, naming the column that admits the number and keeping the surrogate-pair guard's own doc intact
- [x] 1.2 Add `BodyTruncated` to `SourcedTask`, trailing and defaulted to `false`, with the doc saying absence means whole
- [x] 1.3 Read the new answer in all four readers — `GitHubTaskSource`, `JiraTaskSource`, `AzureDevOpsTaskSource`, `LinearTaskSource`
- [x] 1.4 Cover `Trim` in `InfrastructureTests`: a body under the limit stored whole, a body over it cut to 20,000 plus an ellipsis, the flag both ways, and an emoji straddling the cut leaving valid Unicode

## 2. The mirror stores it

- [x] 2.1 Add `MirroredTask.BodyTruncated`, set in `Absorb` beside `Body` so a shortened issue stops reporting truncation
- [x] 2.2 Map it in `TasksDbContext` — `Progress` left alone deliberately
- [x] 2.3 Generate the Tasks migration (never hand-write it) and confirm the model snapshot and the migration's own designer snapshot were regenerated together

## 3. The wire

- [x] 3.1 Carry `BodyTruncated` on `ListTasks`'s row and `GetTask.Response`
- [x] 3.2 Add `bodyTruncated?: boolean` to `TaskItem` and `TaskDetail` in `contracts.ts`, appended beside the tasks block, absence meaning whole

## 4. The third state

- [x] 4.1 Add `unreadable`, `Unreadable`, `ReadFromBody<T>` and `isUnreadable` to `seam.ts`, beside `sectionLines` where the shared reading already lives
- [x] 4.2 Add `readSection(lines, truncated, matches)` over an internal scan that reports whether the section ran to the end of the text — `sectionLines` keeps its exact signature
- [x] 4.3 `parseSeam(body, truncated = false): ReadFromBody<SeamEntry[]>` through `readSection`
- [x] 4.4 `parseCheck(body, truncated = false): ReadFromBody<string[]>` through `readSection`

## 5. The screens draw it

- [x] 5.1 `MergeGate.tsx`: pass the task's `bodyTruncated` into `parseSeam`, and give `seam()` one contiguous branch drawing the existing Unknown with its own reason
- [x] 5.2 `IssueTab.tsx`: pass `bodyTruncated` into `CheckList`, and one contiguous branch saying the body could not be read whole
- [x] 5.3 Two i18n lines in `en.ts`, each beside its own screen's block

## 6. The check the issue asked for

- [x] 6.1 Run `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj` and report the test count
- [x] 6.2 Report the count of the 60 most recent issues stored whole, before and after
- [x] 6.3 Report whether `pnpm format:check` and `dotnet format --verify-no-changes` are still red by the same amount as on `master`
- [x] 6.4 Rebase onto `epic/288`, typecheck first, then re-run the gate in full
- [x] 6.5 Archive the change and sync the spec into `openspec/specs/`
