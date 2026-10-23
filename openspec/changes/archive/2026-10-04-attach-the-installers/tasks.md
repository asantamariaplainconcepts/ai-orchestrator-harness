## 1. The release step carries the installable

- [x] 1.1 `release` packages before it publishes: `pack.sh` is called when the version is not packed, its three artifacts (Setup.pkg, full nupkg, feed) are attached to the release `gh release create` makes.
      req: land-a-change/1
      files: .harness/scripts/release
      — Measured on the backfill of v0.2.3: the release carries the Setup.pkg, the 0.2.3 full
      nupkg and `releases.osx.json`; the JSON line answered `assets: 3`.
- [x] 1.2 The skip's witness is the full nupkg's name — the version-carrying one. The Setup.pkg's name is fixed by Velopack and rewritten every pack, so it cannot witness a version.
      files: .harness/scripts/release
      — `AiOrchestrator-0.2.3-osx-full.nupkg` beside `AiOrchestrator-osx-Setup.pkg`; the check
      is `[[ -f "$full" && -f "$pkg" ]]`.
- [x] 1.3 A pack that fails fails the release: nothing tagged, nothing published, the pack's own words in the `why`.
      req: land-a-change/1
      files: .harness/scripts/release
      — Both failure paths guard before `gh release create`: pack failure and "succeeded but
      the artifact is not there".
- [x] 1.4 The backfill: a release existing with zero assets is completed in place; the tag is never moved.
      req: land-a-change/1
      files: .harness/scripts/release
      — Run on v0.2.3: "already existed at c0304a72 with 0 assets; backfilled to 3. The tag was
      not moved." — the second run answered "already exists … Nothing was touched."
- [x] 1.5 `--no-pack` keeps the tag-and-notes path for already-tagged states.
      files: .harness/scripts/release
      — Measured: `release --no-pack origin/master` answered with the 3 existing assets and
      touched nothing.

## 2. Verification

- [x] 2.1 The release of a merge carries its installer — v0.2.3's backfill is the proof on record, and the next merge proves the fresh path (its bump means a new nupkg, which means the pack runs before the tag).
      files: .harness/scripts/release
- [x] 2.2 Re-runs are idempotent: existing tag + existing assets are named, nothing duplicated, nothing moved.
      files: .harness/scripts/release
- [x] 2.3 The gate over the correct tree; `openspec validate attach-the-installers --strict`.
      — `aspire do ci` green; validation valid (below).
