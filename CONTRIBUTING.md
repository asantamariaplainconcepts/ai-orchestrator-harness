# Contributing

Work moves through GitHub issues, and issues move through [`.aio/loop.yaml`](.aio/loop.yaml).
The `status:*` label is the only record of where an item is; an issue carries at most one, and the
engine's move removes the others in the same edit. With none, the issue is outside the loop, and the
PBI's *Move to…* puts it in. Run `gh auth login` once: the app reads issues through `gh`.

`.aio/` is this repository's installation, and [`.aio/README.md`](.aio/README.md) says what in it is
whose, what each step of the package's loop answers and how to make the gate and the rest your own.
Beyond what every installation has, `archive` is this repository's own step, at the root of `.aio/`,
and [`scripts/`](scripts/README.md) holds what only a person or a workflow runs: `pack`, `release`,
`trust-sweep` and `keep-committer`, each one line of its README.
`src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Harness/Package/` is the package the app installs elsewhere: the `loop.yaml` template, the gate stub, the `config.json` example,
`version`, `links` and `package.props`, which embeds the rest from this repository's own
files, `.aio/steps/`, `.aio/aio` and the six commands of `.ai/commands/aio/` among them.

| Label | What happens there |
| --- | --- |
| `ready-for-proposal` | A person let it in. `/aio:refine` takes it to the Definition of Ready: `ready` goes on, `gaps` to `needs-refinement`, where the loop does nothing. |
| `spec-ready` | `file-overlap` waits while an issue in flight shares a file, `claim` pushes `change/<n>` and assigns the issue, and `/aio:propose` writes the OpenSpec change. Leaving, the draft PR opens. |
| `proposal-review` | `risk-score` reads the change: `low` goes on, `high` holds for a person first. |
| `in-progress` | `/aio:implement` writes the code. |
| `verifying` | `gate` runs `aspire do ci`, `evidence` captures the screens when the frontend changed, and `quality` publishes on the issue the coverage, CRAP, mutation, duplication and lost tests of the change, holding nothing. Green goes on and the PR is marked ready; red holds. |
| `code-review` | `/aio:review-diff` reviews the diff against the change: `approved` goes on, `rejected` back to `in-progress`, twice at most. |
| `merge-review` | `diff-risk` reads the reviewed diff: `low` goes on, `high` holds first. |
| `retro` | `/aio:review` appends what the change taught to `docs/process/retro-log.md`, on the branch. |
| `merging` | `archive` folds the change into `openspec/specs/`, `merge` squash-merges and the issue closes; `conflict` goes to `rebasing`. |
| `rebasing` | The `rebase-safely` skill resolves the conflict, and the PBI goes back to `merging`, once at most. |

**The hold is not a status.** The label says where the work is; `status:holding` says whether anyone
may take it further. The engine applies it on a high score, a red gate, a failed or blocked run, an
unknown outcome or a used-up `max_loops`, with the reason in its journal. Removing it is a person's
act: *Release hold* in the PBI's header, or approving the pull request at the vendor.

**A release is made by hand.** Raise `<Version>` in `src/Directory.Build.props`, merge it, and run
`NUGET_API_KEY=<your nuget.org key> scripts/release <sha of that merge>`; `merge` no longer
releases (#652). It packs the .NET tool `aio`, pushes `artifacts/aio.<Version>.nupkg` to nuget.org,
and as soon as nuget.org accepts the push tags `v<Version>` and creates a GitHub release of notes with
no files, stopping at the first step that fails (#681). It does not wait for nuget.org to list the
version, which takes 15 to 30 minutes of validation; its JSON line says so (#724). Without
`NUGET_API_KEY` it uses the key NuGet already stores for the source. The key never goes in the
repository, and the script prints it nowhere. A version nuget.org already has is not pushed again, but
its tag and release are made when origin lacks the tag, so a run that stopped before tagging is
finished by running it again (#711). A tag already on that commit is said to be done; one on another
commit stops the script and is not moved. `--source ./artifacts/feed --no-tag` runs the same steps against a
local folder, publishes nothing and tags nothing; `dnx aio --add-source ./artifacts/feed` then runs
what it packed. The usual way is the `release` workflow (Actions, *Run workflow* on master):
nuget.org's Trusted Publishing trades the run's GitHub OIDC token for a key that lives an hour, so no
key is stored anywhere, and the workflow runs this same script on the commit it checked out and writes
its JSON line to the run's summary.

**Who takes an issue.** `claim` pushes `change/<n>`, which git refuses twice, and assigns the issue to
the control plane's identity. The engine works only PBIs unassigned or assigned to itself, so
assigning an issue to somebody else keeps it out. `wip` in the loop caps live agent runs at four.

**A fault in a step**, a command or script whose instruction did not match what was there, goes to
`.aio/steps/feedback`: one issue per step and subject, labelled `harness-feedback`, and
`eval-candidate` when an agent failed repeatedly or a person took over.

Topic labels (`terminal`, `sessions`, `platform`, `bug`, …) say what the work touches, never where it
is. GitHub Projects are label-filtered views with no automation: an empty view means a missing label.
