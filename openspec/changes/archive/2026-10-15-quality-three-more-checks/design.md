## Context

`quality` (#746) runs in `verifying` after `gate` and `evidence`, in the PBI's worktree. It runs the package's checks in `.harness/aio/checks/` through `checked(name, extra)` and maps each check's exit code to a row. The checks' contract, from `_lib.sh`, is exit 0 for clear, 1 for one finding per line, 2 when the check could not tell and 3 when it does not apply. The three tripwires that move already keep this contract. They only source `_lib.sh` through `../aio/checks/` and read `TRIPWIRE_*`.

## Goals / Non-Goals

**Goals:** eight rows in the `aio:quality` comment. No script or variable with "tripwire" in its name, and no `review-evidence`. The three checks measure what they measured before.

**Non-Goals:** blocking a PR on any row. Changing what `diff-risk` or `protected-paths` measure. Changing the Review tab's shape.

## Decisions

**The new variable prefix is `CHECK_`.** It names the folder the scripts live in: `CHECK_BASE`, `CHECK_TRUNK`, `CHECK_DECLARED`, `CHECK_CRAP` and `CHECK_CRAP_MAX`. `TRIPWIRE_PROTECTED` and `TRIPWIRE_FROM_BASE` disappear with `protected-paths.sh` and `all.sh`, so they get no new name. I considered `AIO_`, but the runtime already uses it for engine variables (`AIO_DONE_FILE`), and these are the checks' inputs.

**The scripts keep their names.** `tests-touched.sh`, `old-tests-new-code.sh` and `out-of-seam.sh` move with `git mv`, so their history follows them. Their rows carry the names the issue gives.

**The *Dónde cae* parser is shared, not copied.** `file-overlap`'s `section`, `title`, `looksLikePath` and `declared(body)` move to `.harness/aio/lib/where-it-lands.js`, a CommonJS module, because both `file-overlap` and `quality` use `require`. `file-overlap` requires it and keeps behaving the same. `quality` reads the issue body with `gh issue view <issue> --json body --jq .body` and turns the declared paths into the pattern list `CHECK_DECLARED` expects, one per line. Each path is written as itself and as `<path>/*` with any trailing `/` stripped first, so a declared folder covers what sits under it and a declared file covers itself. `matches_any`'s `*` already crosses directories. An issue with no *Dónde cae* gets no `CHECK_DECLARED`, and `out-of-seam` then exits 3, which is `not measured` with its reason. `tests-touched` receives the same list, as it did under `all.sh`'s caller.

**Exit codes map to rows the way `tests` does.** Exit 0 is `ok` with a sentence saying nothing was found. Exit 1 is `over` when the finding count is above the row's `max` (0 by default), and its measured cell is the count followed by the findings joined. Exit 2 or 3 is `not measured` with the check's last line. A check that cannot run at all, such as `dotnet` missing for old tests, is also `not measured`. The new `quality.json` keys are `touched`, `old-tests` and `outside`, each `{"max": 0}`. They are written commented out in the package's example and set in this repository's file.

**`old-tests-new-code` builds.** It takes minutes, and that is the cost of the question. It runs last, in a throwaway worktree, after the gate has already built the head. `quality` stays exit 0 whatever happens.

**`review-evidence` goes whole.** `ReviewEvidenceStep`, `ReviewDashboard` and their step name are deleted, and the `evidence/` folder at the root has no writer left, so its `.gitignore` entry goes too. The `GateTests` test for the CRAP-42 tripwire goes with the step. `CrapScriptTests` and `QualityScriptTests` already cover `crap.sh` and the CRAP row.

**The `git grep` check also excludes `openspec/changes/`.** The issue excludes `openspec/archive` and `docs/process/retro-log.md`. This change's own artifacts, including the REMOVED requirement, have to name what they remove. Archived changes live in `openspec/changes/archive/`, and they are history just like `openspec/archive/`. The check is therefore `git grep -i "tripwire\|review-evidence" -- . ':!openspec/archive' ':!openspec/changes' ':!docs/process/retro-log.md'`.

## Risks / Trade-offs

- [Someone sets `TRIPWIRE_TRUNK` by hand for `MutationStep`] → The rename is in the package's version bump. `AGENTS.md` names `MUTATION_SINCE`, the documented override, which stays the same.
- [Glob patterns differ from `file-overlap`'s suffix matching] → An issue that writes a path from another root, like `Modules/Workspace`, matches `file-overlap` but not `out-of-seam`, so it shows up as files outside. That makes the row noisier, not wrong, and the row reports only. Issues here write paths from the top.
- [`QualityScriptTests` gets slower because of `old-tests-new-code`] → Its fixture's change edits no existing test, so the check exits 0 before it looks for `dotnet`. The test for the old-tests row puts a fake `dotnet` on PATH, next to the fake `gh` and `npx`. That fake answers the build and a failing `Failed ThingTests.Decides`, so the script runs for real and nothing gets built.
