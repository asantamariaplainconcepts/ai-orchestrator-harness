# Attach the installers to every release

## Why

The release machinery lands tags and notes, but a person who downloads one gets nothing they can
run: `v0.2.3` has zero assets. The one release that ever carried an installer — `v0.1.3` — had
its `AiOrchestrator-osx-Setup.pkg` uploaded by hand. A release that names a state of the
software and hands you no way to install it is a changelog.

## What Changes

- `.harness/scripts/release` packages before it publishes: `pack.sh` builds the Setup.pkg (and
  the full nupkg and the release feed) for the version the props carry, and all three are
  attached to the release `gh release create` makes — one command, tag + notes + installers.
- The skip's witness is the full nupkg's name, which carries the version: the Setup.pkg's name
  is fixed by Velopack (an installer URL a future feed relies on) and is rewritten by every
  pack, so it cannot say what version it holds.
- A pack that fails fails the release: nothing is tagged, nothing published — a release without
  its installer is half of what was asked for.
- The backfill: a release that already exists with zero assets — #440's finding on its own
  landing — is completed in place. `v0.2.3` gets its installers without hand-run mercy.
- `--no-pack` keeps the tag-and-notes-only path (the settle of an already-tagged state).
- The JSON line gains an `assets` count; `merge`'s line inherits it through the folded `why`
  with no contract change beyond what #441 already made additive.

## Capabilities

### Modified Capabilities

- `land-a-change`: the landing's release step carries the installable, not only the name — the
  release's assets are part of what landing one change means.

## Impact

- `.harness/scripts/release` only. `pack.sh` is called, not changed; no backend, no specs'
  code, no migration. `merge` is not touched (its call site already inherits whatever `release`
  answers).
- Shared state: the upload to existing releases (the backfill) touches only the vendor's
  release objects, never a tag, never master.
