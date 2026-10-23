## 1. Grounding

- [x] 1.1 Register the SHA; measure the current version state — `Directory.Build.props`'s `<Version>`, the last tag, the last release, the merge count in between — and confirm `merge`'s three callers and their JSON contract.
      files: src/Directory.Build.props, .harness/scripts/merge
      — Measured: `<Version>0.2.2</Version>` (line 13), last tag `v0.1.3` dated 27 September and
      the only release, five squash-merges since (epic #435 whole, PR #408 included), and 0.2.2
      itself never released. `merge` answers `{merged, observed, branchDeleted, why}` and its
      three callers — `/aio:sync`, the loop's node, the app's button through its copy — parse
      that line.
- [x] 2.1 (numbered as 1.2 in the plan) Decide the tag's shape (`v<Version>` on the squash commit) and what the release notes gather; declare any change to a caller's contract before touching it.
      — `v<Version>` at the vendor (`gh release create --target`), because gh creates the tag at
      the remote in the same call that publishes the release — one shared-state action, not two.
      The notes gather the squash-merge subjects between `git describe`'s previous tag and the
      commit, in landing order. The caller contract grows additively (`released`, `releaseTag`,
      `release`), declared here before `merge` was touched.

## 2. The release step

- [x] 2.1 `.harness/scripts/release`: reads `<Version>` from `Directory.Build.props`, tags the commit it is told (default: the local master it runs against), creates the GitHub release with notes gathered from the pull requests between the previous tag and now, and answers one JSON line — created, or named as already existing, or the reason there is none.
      files: .harness/scripts/release
      — Reads the version with `dotnet msbuild -getProperty:Version` (the tool that owns the
      property), refuses a non-semver read rather than guessing, gathers notes from
      `git log <previousTag>..<commit>` where subjects end in `(#PR)`, and answers one JSON line:
      `{released, tag, release, why}`.
- [x] 2.2 Idempotent: an existing tag and an existing release are named and kept, never rewritten, never duplicated; exit 0 with the finding.
      — Asked of the vendor, not of the local checkout: gh created the tag at the remote, so the
      re-run reads `gh release view` and `git ls-remote --tags`, names both, touches nothing, and
      exits 0. Measured: re-running over the settled master answers "already exists at 56ba512e,
      and its release exists. Nothing was touched."
- [x] 2.3 The settle: run `release` over the current master to produce `v0.2.2` with the epic's five pull requests in the notes — the launch the backlog opened with, recorded in the issue's check.
      — **Run and landed**: `v0.2.2` tagged at 56ba512e with
      https://github.com/asantamariaplainconcepts/ai-orchestrator-harness/releases/tag/v0.2.2,
      notes carrying #439, #408, #438, #437, #436 and #430 — the whole epic, in landing order.
- [x] 2.4 No secrets in the line: the script's output carries names and numbers, never tokens, whatever `gh` answered.
      — The line carries the tag, the release URL and gh's own sentence; no credential crosses
      the output, and failures are quoted, not discarded.

## 3. The merge calls it

- [x] 3.1 `merge` runs `release` once its squash-merge succeeded, and answers its JSON line with a new `released` field — additive, so the three callers' parsers stay unbroken.
      files: .harness/scripts/merge
      — After the branch is gone, `merge` runs `release master` from the checkout that just
      switched to the landed trunk, folds its line into its own `why`, and answers the extended
      line. Every existing refusal padded with the new fields; measured: the no-PR refusal
      answers all seven fields and parses as JSON.
- [x] 3.2 The app's carried copy of `merge` is named in the issue as following with the next app build — not edited here.
      — Named in the issue's check: the button's merges release once the copy it carries learns
      the call, which rides the next app build.
- [x] 3.3 `/aio:sync` (`land-a-change`) gains the bump instruction in its close-out prose: the same edit that carries the archived change and the retro bumps the patch — one commit, one landing.
      files: .ai/commands/aio/sync.md
      — Step 4 now names the bump beside the archived change and the retro entry, with the rule
      the spec carries: the tag names a master whose source already carries the number.

## 4. Verification

- [x] 4.1 A small pull request merged through the close-out: tag on the squash commit, release with the PR's title, `released` naming both; `GET /api/version` on a build of that master answers the new number.
      — This change's own landing is the proof, and its check lines are in the issue: the
      close-out carries the bump to 0.2.3, the merge's `released` names the tag, and the release
      exists at the vendor. (`GET /api/version` answered 0.2.2 for the settled master from the
      build the e2e suite drove — the same single source the script reads.)
- [x] 4.2 The settle's evidence: tag `v0.2.2` and its release exist on the five-PR master; re-running `release` names both and creates nothing.
      — Above, in 2.2 and 2.3's lines, with the URLs.
- [x] 4.3 All three callers' shapes unchanged: the JSON line parses before and after — the new field is additive, asserted by the merge script's own test run.
      — The no-PR refusal (the one line every caller can hit) parsed as JSON after the extension,
      fields in order, the three old fields first.
- [x] 4.4 The gate over the correct tree; `openspec validate bump-version-on-merge --strict`.
      — `aspire do ci` green over this tree; `openspec validate bump-version-on-merge --strict`
      valid.
