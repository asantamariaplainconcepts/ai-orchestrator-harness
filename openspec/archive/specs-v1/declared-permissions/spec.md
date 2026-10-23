# declared-permissions Specification

## Purpose
Every request says what it requires of its caller, on the request type, enforced by the shared pipeline — so a
use case that forgets cannot thereby become public. Most name a permission and a project; a few name an access
value because no permission could describe them. What none of them may do is say nothing.
Reverse-engineered from the shipped code, not negotiated ahead of it.

## Requirements
### Requirement: A request declares a permission or an access value, and never both or neither
`[Requires]` SHALL be declared on the request type and enforced by the CQS pipeline. A request SHALL name
either a permission — with a project to check it against — or one of the two access values, and never both.
`Access.FiltersToCaller` SHALL be the claim "reaches across projects, and narrows its own answer to the
caller"; `Access.AnyCaller` SHALL be the claim that no permission could describe the operation, such as adding
the first folder or listing what is asking for attention on this machine. A refusal SHALL be the pipeline's own
sentence rather than a handler's.

#### Scenario: A request that says nothing fails the suite
- **WHEN** a request type carries no `[Requires]`
- **THEN** `PermissionTests` fails, because every request declares what it requires

#### Scenario: A permission without a project fails the suite
- **WHEN** a request names a permission and no project to check it against
- **THEN** the test fails

### Requirement: Every named permission is a known constant, granted from the module that owns it
Every permission a request names SHALL be a known constant, and every permission granted to a bundle SHALL be
one too. Permissions SHALL be spelled `<module>:<verb>` and SHALL live in the owning module's permission class
— `workspace:read`, `workspace:manage-worktrees`, `workspace:manage-folders`, `workspace:run-commands`,
`workspace:open-on-desktop`, `workspace:configure`, `workspace:chat`, `tasks:read`, `tasks:sync`,
`tasks:configure`, `runs:read`, `runs:start`, `runs:control`, `workflows:read`, `workflows:edit`. No module
SHALL grant anything to the administrator bundle explicitly, and the permissions **no** operation requires
SHALL be the ones the suite already knows about.

#### Scenario: A typo in a permission name fails the suite
- **WHEN** a request names a permission string that is not a declared constant
- **THEN** the test fails rather than the operation silently never being permitted

#### Scenario: An ungranted permission is an accounted-for gap
- **WHEN** a module declares a permission nothing requires
- **THEN** the suite passes only because that permission is in the known list, so a new one has to be
  acknowledged

### Requirement: A permission is a fact about a project, and the machine's owner is the local case
`IProjectPermissions` SHALL answer what the caller may do on a **named** project, because roles are per project
and "their role" is only a fact once a project is named. Where one person owns the machine there SHALL be
nothing to look up; where people sign in, the answer SHALL be rows plus the configured bootstrap
administrators. `ICurrentPrincipal` SHALL identify the local owner as `local-owner` and an unauthenticated
caller as `anonymous`.

#### Scenario: The local owner is not a lookup
- **WHEN** the app runs on the machine one person owns
- **THEN** the permission answer is the local owner's, with no rows consulted
