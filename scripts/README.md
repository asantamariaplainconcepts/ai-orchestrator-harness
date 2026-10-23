# scripts/

What only this repository runs, by hand or from a workflow: the harness package carries none of it, and another repository has none of it. None is a step of the loop, so none is in `.aio/`; `archive`, which `merging` runs first, is a step of this repository's loop and stays at the root of `.aio/` (#779).

- `keep-committer` gives a replayed commit back its committer. The `rebasing` run calls it as `git rebase -x scripts/keep-committer <upstream>`.
- `release` packs, pushes to nuget.org and tags `v<version>`. A person runs it, or `.github/workflows/release.yml` when a person starts that workflow.
- `pack` builds the frontend and packs `artifacts/aio.<version>.nupkg`. `release` runs it, or a person from the repository's top.
- `trust-sweep` lists, and with `--remove` takes out, the Claude Code trust left for folders that are gone. A person runs it.

The harness package's own files, and `package.props`, which `Modules.Workspace` imports to embed the package, are in `src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Harness/Package/`.
