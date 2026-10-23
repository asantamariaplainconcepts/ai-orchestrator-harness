## MODIFIED Requirements

### Requirement: quality measures the change against its base
`.harness/aio/quality <issue>` SHALL run in `verifying` after `gate` and `evidence`, in the PBI's
worktree, and measure the head against its merge base with the pull request's base branch
(`origin/HEAD` without a pull request). It SHALL measure eight checks, and nothing the gate did not
measure but the duplication and the three that read the diff, the issue and the old tests:

- **Coverage of changed lines**: the lines the diff adds that the Cobertura report can cover, and the
  share of them with hits, against `min`, 70 by default.
- **CRAP of touched methods**: through `checks/crap.sh` with the line at 0, the highest CRAP of the
  methods a hunk overlaps and how many pass `max`, 30 by default.
- **Mutation in touched files**: the score of a `summary.json` as the mutation step writes it, its
  killed mutants of the valid ones; reported only, never `over`.
- **New duplicated lines**: `npx --yes jscpd@4.0.5` with 10 lines and 70 tokens at least over `paths`,
  on the head and on the base's tree; per touched file, the lines inside a clone at the head less those
  of the same file at the base, as PlainConceptsPlatform/Lyceum#89 counts them, summed against `max`,
  0 by default.
- **Tests lost or skipped**: through `checks/tests-lost-or-skipped.sh`, by name, against `max`, 0 by
  default.
- **Tests touched**: through `checks/tests-touched.sh`, the test files that stood at the base and the
  change modified, deleted or renamed outside the issue's *Dónde cae*, against `touched.max`, 0 by
  default.
- **New code on old tests only**: through `checks/old-tests-new-code.sh`, the tests of every C# test
  file the change modified, run in their base form against the head; each that fails is counted
  against `old-tests.max`, 0 by default.
- **Files outside the issue's paths**: through `checks/out-of-seam.sh`, the paths of the diff that no
  path of the issue's *Dónde cae* covers, against `outside.max`, 0 by default.

The issue's *Dónde cae* SHALL be read from its body as `file-overlap` reads it, by the same code in
`.harness/aio/lib/`, and handed to the checks in `CHECK_DECLARED` as one pattern per declared path and
one for what sits under it. An issue that declares no path SHALL leave the outside row `not measured`
with the reason. The checks SHALL take the base in `CHECK_BASE`, and every variable they read SHALL
start with `CHECK_`.

Where the reports are and each line SHALL be read from the repository's own `.harness/quality.json`,
which may carry `//` comment lines; without it the defaults SHALL hold. Each check's result SHALL be
`ok`, `over` or `not measured`; a check that exits 2 or 3 SHALL be `not measured` with its last line.

#### Scenario: A method touched to CRAP 42
- **WHEN** the change rewrites a method and `crap.tsv`, stamped with the head, scores it 42.0
- **THEN** the CRAP row reads `highest 42.0 of 1 method, 1 over 30`, line `30`, `over`

#### Scenario: Added lines without a test
- **WHEN** the change adds four coverable lines and the Cobertura report has hits on one
- **THEN** the coverage row reads `25.0%: 1 of 4 changed lines covered`, line `70%`, `over`

#### Scenario: A copied block
- **WHEN** jscpd finds a 12-line clone in a file the change added, and none at the base
- **THEN** the duplication row reads `12 lines in 1 file`, with the file and its count, `over`

#### Scenario: A deleted test
- **WHEN** the change deletes a `[Fact]` that existed at the base
- **THEN** the tests row names it as lost, `over`

#### Scenario: A change that touches no test
- **WHEN** the change edits only `src/Thing.cs`
- **THEN** the Tests touched row is `ok`, line `0`, and so is the New code on old tests only row

#### Scenario: An old test edited outside Dónde cae
- **WHEN** the change edits `tests/ThingTests.cs`, which stood at the base, and the issue's *Dónde cae* names only `src/`
- **THEN** the Tests touched row names `tests/ThingTests.cs`, line `0`, `over`, and the Files outside the issue's paths row names it too, `over`

#### Scenario: New code the old test no longer agrees with
- **WHEN** the change edits `tests/ThingTests.cs` and its base form fails `ThingTests.Decides` against the head
- **THEN** the New code on old tests only row names `ThingTests.Decides`, line `0`, `over`

#### Scenario: An issue without Dónde cae
- **WHEN** the issue's body has no *Dónde cae* section
- **THEN** the Files outside the issue's paths row is `not measured` and says the issue declares no file-set
