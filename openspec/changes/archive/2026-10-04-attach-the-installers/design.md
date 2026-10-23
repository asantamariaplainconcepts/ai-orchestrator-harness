## Context

Measured on master 8a19f7f4, minutes after #441 landed. The finding that opened this: `v0.2.3`
exists as a release with notes and no assets. `v0.1.3`, the previous release, has one asset —
`AiOrchestrator-osx-Setup.pkg`, uploaded by hand. `pack.sh` already does everything: reads the
version from `Directory.Build.props` via `dotnet msbuild -getProperty:Version`, publishes Server
and MigrationService self-contained, builds the SPA first, and leaves under
`artifacts/releases/osx/` a Setup.pkg, an `AiOrchestrator-<version>-osx-full.nupkg`, a
Portable.zip and the `releases.osx.json` feed (Velopack owns the shape).

What `release` (from #441) does: tag + notes + idempotency. No pack, no upload.

## Goals / Non-Goals

**Goals:**

- Every release carries what a person installs: pkg, full nupkg, feed.
- One command: pack-before-publish, in `release`; `merge` inherits through its call.
- The backfill: existing assetless releases (v0.2.3 today) are completed without moving tags.
- A failed pack fails the release — no half-landings.

**Non-Goals:**

- Signed/notarized packages; the feed as a live update channel (pack.sh's own comment).
- Windows/Linux builds.
- Portable.zip in the release (exists; attach it is its own decision, not taken).

## Decisions

- **Pack lives inside `release`, not in `merge`.** `release` already owns "one version, one
  tag, one release"; the pkg is the version's body. `merge` stays the caller it already is.
- **The full nupkg decides "already packed"** — its name carries the version. The Setup.pkg's
  name is fixed by Velopack on purpose: it is the URL an installer/feed relies on, so it is
  rewritten every pack and cannot witness anything.
- **Backfill rather than reproduce**: the tag exists, history is not moved (ADR-0005); the
  assets are uploaded to the existing release with `gh release upload --clobber` — clobber for
  the same reason the fixed pkg name exists: a re-uploaded installer replaces its name.
- **`--no-pack`** for the settle path: tagging states that already shipped their binaries, and
  any other use where the artifacts are deliberately out of scope.

## Risks / Trade-offs

- **A twenty-minute build in the merge's path.** The first merge after this lands takes the pack
  time; every one after it skips when the nupkg of that version is already there (it is not —
  the bump means a new version, and a new version means a pack; the skip only pays when a
  release is re-run, e.g. the backfill after an interrupted publish).
- **The fixed pkg name overwrites the previous release's asset on upload.** Harmless today:
  each release uploads its own pkg built from its own staging (Velopack version-pins the pkg
  contents, the name is just the front door); the risk is a copy of an older version's pkg
  placed by a human out of process — said here, guarded by nothing.

## Verification plan

1. The backfill over v0.2.3: assets present (Setup.pkg, full nupkg, feed); a second run
   idempotent ("already exists … Nothing was touched").
2. A fresh release path (the dry-run of the next landing): the pack runs before the tag when
   the nupkg is absent.
3. `--no-pack` answer names zero assets and touches existing releases' state only idempotently.
