## MODIFIED Requirements

### Requirement: One MSBuild property is the single source of the version

`src/Directory.Build.props` SHALL declare `<Version>` in its existing `PropertyGroup`, so every project under `src/` inherits the same value into `AssemblyInformationalVersion` without a `.csproj` edit. The property SHALL be a semver string of three parts, starting at `0.1.0`.

The version SHALL move when work lands: a pull request merged to master SHALL carry, in its close-out edit, the patch's next revision — or the minor's, where the pull request's title is feature-shaped and the person chose to say so — so that the tag the merge creates names a master whose source already carries the number the tag says. A master whose `<Version>` did not move across a merge is a state the release step names rather than fixes: no script SHALL edit `Directory.Build.props` on master after the merge.

#### Scenario: Every assembly carries the version

- **WHEN** the solution is built
- **THEN** the Server, the Shell and every module assembly carries `AssemblyInformationalVersion` set to the value of `<Version>`

#### Scenario: Changing the version in one site changes it everywhere

- **WHEN** `<Version>` is changed in `src/Directory.Build.props` and the solution is rebuilt
- **THEN** every assembly's `InformationalVersion`, the bundle's `CFBundleShortVersionString` and the value shown in the settings screen all reflect the new value

#### Scenario: A merge carries its own bump

- **WHEN** a pull request is squash-merged to master through the loop's close-out
- **THEN** the squash commit's `Directory.Build.props` carries the next patch revision (or the next minor, where the person said so), and the tag the merge creates names that number

#### Scenario: A forgotten bump is named, not invented

- **WHEN** a merge lands whose close-out did not move `<Version>`
- **THEN** the release step says the version did not move rather than editing master after the merge, and no tag is created over a number the source does not carry
