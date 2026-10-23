# change-quality Specification

## Purpose
What the gate measured of a change, shown where its review is read: the coverage of the lines it added,
the CRAP of the methods it touched, mutation in the files it touched, the duplicated lines it added and
the tests it lost. It reports and blocks nothing: blocking on baselines that only shrink waits for the
numbers of a few pull requests. Written with the code of #746 (`.aio/steps/quality`,
`.aio/checks/`, `src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Vendor/Review/QualityComments.cs`,
`src/frontend/features/pbi/tabs/ReviewTab.tsx`).
## Requirements
### Requirement: quality measures the change against its base
`.aio/steps/quality <issue>` SHALL run in `verifying` after `gate` and `evidence`, in the PBI's
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
`.aio/lib/`, and handed to the checks in `CHECK_DECLARED` as one pattern per declared path and
one for what sits under it. An issue that declares no path SHALL leave the outside row `not measured`
with the reason. The checks SHALL take the base in `CHECK_BASE`, and every variable they read SHALL
start with `CHECK_`.

Where the reports are and each line SHALL be read from the repository's own `quality` in `.aio/config.json`,
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

### Requirement: A test is named by the type that encloses it
`checks/tests-lost-or-skipped.sh` SHALL name each `[Fact]` and `[Theory]` of a C# test file
`<Type>.<method>`, where the type is the innermost `class` or `record` whose braces enclose the test,
counted from the source with no build. A type declared without a block, such as a positional record
ending in `;`, SHALL name nothing, and so SHALL a nested type whose block has closed before the test.
A file-scoped namespace and a block namespace SHALL give the same names. Before #754 the name took
the last type declared above the test, and on aio-dogfood#65 a second nested record renamed five
untouched tests and reported them lost.

#### Scenario: A record declared inside the test class
- **WHEN** a change adds `record Detail(...);` below `record Forecast(...);` inside `WeatherForecastTests`, and every test stays
- **THEN** the check prints no lost test and exits 0

#### Scenario: A block namespace with a nested positional record
- **WHEN** `namespace Tiny { class ThingTests { record Probe(int Value); ... } }` loses its test `Decides`
- **THEN** the check prints `lost test: ThingTests.Decides`

#### Scenario: A skip added
- **WHEN** a change turns `[Fact]` into `[Fact(Skip = "flaky")]`
- **THEN** the check prints `skip added in` the file with that line, and no lost test

### Requirement: A check without its report says why it was not measured
A check whose report `quality` in `.aio/config.json` does not name, is missing, or was measured on another
commit than the head SHALL be `not measured`, with the reason in the measured cell, never a pass. A
Cobertura report SHALL take the commit of the `# commit` line of the crap report, which the same
step writes beside it; a report with no stamp SHALL be stale when it is older than the head's commit.
A mutation summary measured since another base than the change's SHALL be stale too.

#### Scenario: No Cobertura.xml
- **WHEN** `quality` in `.aio/config.json` names `src/artifacts/coverage/Cobertura.xml` and the gate left none
- **THEN** the coverage row reads `no Cobertura.xml at src/artifacts/coverage/Cobertura.xml`, `not measured`

#### Scenario: A crap.tsv of another commit
- **WHEN** `crap.tsv` names commit `1a2b3c4…` and the head is another
- **THEN** the CRAP row says it was measured on `1a2b3c4`, and so does the coverage row, both `not measured`

### Requirement: quality publishes one marked comment and blocks nothing
`quality` SHALL publish one comment on the issue that opens with `<!-- aio:quality <head sha> -->`,
then a line with the head, the base and how many checks are over and not measured, then a table of
check, measured, line and result. It SHALL answer one line `{"outcome": "reported", "published",
"comment", "why"}` and exit 0 whatever it measured, so the gate's outcome stays the state's. A comment
gh could not write SHALL be said in the why, and still exit 0. The package's `quality` in `.aio/config.json`
SHALL be an example whose every line is a comment, written only where the repository has none; this
repository's SHALL name the gate's reports under `src/artifacts/` and scan `src`.

#### Scenario: The comment carries its mark
- **WHEN** `quality 7` runs on head `abc…` and gh answers the comment's address
- **THEN** the comment starts `<!-- aio:quality abc… -->`, and the line is `outcome: reported` with that address, exit 0

### Requirement: The Review tab shows the newest quality between the gate and the evidence
`GET /api/pbi/review` SHALL answer `quality`: the rows of the newest comment of the issue carrying
`<!-- aio:quality`, never an older one, each `{check, measured, line, result}`, with the commit it
measured, its author and time, `stale` when that commit is not the pull request's head, and the
comments' problem when they could not be read. The Review tab SHALL show it as a section between the
gate and the evidence: a row per check with a green dot for `ok`, a red one for `over` and a grey one
for `not measured`, the measure, or `not measured:` and the reason, and its line. A stale measure SHALL
say `Measured on <sha>, the head is <head>.`; without a comment the section SHALL say the PBI has no
quality measure yet and that the loop's quality step publishes one in verifying.

#### Scenario: A measure of an earlier commit
- **WHEN** the newest quality comment names `5d6e7f8…` and the pull request's head is `9b01a3c…`
- **THEN** the API answers `stale: true`, and the section says `Measured on 5d6e7f8, the head is 9b01a3c.`

#### Scenario: The reviewer reads it
- **WHEN** `/aio:review-diff` reads the issue
- **THEN** its command tells it to read the newest `aio:quality` comment, and that a row `over` alone is not a finding

