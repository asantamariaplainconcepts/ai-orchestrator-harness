## 1. Scripts

- [x] 1.1 `merge` reads the merge commit from the vendor, fetches it and releases that SHA; no merge commit, nothing tagged.
      req: land-a-change/1
      files: .harness/scripts/merge
- [x] 1.2 `release` reads the version from the tagged commit's props with msbuild, and refuses to pack a checkout that does not hold the commit's files.
      req: land-a-change/1
      files: .harness/scripts/release

## 2. Verification

- [x] 2.1 Run `release` on 93d7f02b, 8ac6f113 and 1febf577 from a checkout whose props say 0.2.15: it names v0.2.13 (refused, the checkout is not that commit), v0.2.14 and v0.2.15 (both existing, untouched).
- [x] 2.2 `aspire do ci` on the branch, 18/18. Landing it through the fixed `merge` is the check that its own release sits on the squash; that is said on the issue after the merge.
