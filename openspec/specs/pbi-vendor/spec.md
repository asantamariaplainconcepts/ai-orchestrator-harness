# pbi-vendor Specification

## Purpose
The vendor is the truth: a PBI is a GitHub issue, its state is a label, and the console reads both from
the vendor and keeps no copy that counts as the answer, only a read of a few seconds that any write
through the port forgets. Every write is read back before it
counts. Only the adapter behind `IPbiProvider` knows that the vendor is reached through `gh` and that a
state is a `status:*` label. Reverse-engineered from the shipped code
(`src/shared/AiOrchestrator.Infrastructure/PbiProviders/GitHub/`,
`src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Vendor/`), not negotiated ahead of it.

## Requirements
### Requirement: The PBIs are the open issues of every folder's repository
`GET /api/pbis` SHALL list, for every folder whose `origin` is on github.com (one folder per repository,
the first by path), every open issue, up to 1,000, with its vendor, repository, id, title, state,
whether it is held, its assignee, whether it is closed, and the folder's path. A folder the vendor did
not answer for SHALL be listed under `unread` with the reason, and the others SHALL still be answered.
`GET /api/pbis/done` SHALL list up to 100 closed issues per folder, most recently updated first. The
list SHALL be read from the vendor, or from a read of it at most ten seconds old that no write has
forgotten.

#### Scenario: One folder's vendor is unreachable
- **WHEN** `gh` fails for one of two folders
- **THEN** the other folder's PBIs are listed and the first is under `unread` with what `gh` said

### Requirement: The state is a label, and the hold is another
A PBI's state SHALL be its first label starting with `status:` other than `status:holding`, without the
prefix, or none. It SHALL be held when it carries `status:holding`. Moving a PBI SHALL create the target
label when it does not exist, then remove every other `status:*` label except the hold and add the
target in one edit. Setting the hold SHALL add or remove `status:holding` and nothing else. Listing a
repository's states SHALL answer the names of its `status:*` labels without the prefix, the hold left
out, ordered by name; an adapter that cannot list them SHALL say so rather than answer none. No module
and no contract outside the adapter SHALL name `gh` or a `status:` label, and `VendorTests` SHALL fail
the gate when one does. The port SHALL have twenty methods or fewer, grouped by what a screen or a
command reads (#666): one move carries the state, the hold, the assignment and the close, and one read
of a pull request answers everything its tab paints.

#### Scenario: A move leaves one state label
- **WHEN** the engine moves a PBI from `in-progress` to `verifying`
- **THEN** the issue carries `status:verifying` and no other `status:*` label besides the hold, if it had one

### Requirement: A write counts only after it is read back
Every write of the adapter SHALL read what it wrote back from the vendor and answer `Written<T>` with
what it observed and the mismatch, if any: a move SHALL find exactly the target state with the hold
unchanged; a hold SHALL find the hold as asked and the state unchanged; an assignment SHALL find the
assignee; an opened pull request SHALL exist as a draft; a pull request marked ready SHALL no longer be
a draft; a closed issue SHALL be closed; a comment, an edited comment, a review comment, a reply and a review
SHALL be fetched again by id and match what was sent. A comment SHALL be edited in place by its id with
one `PATCH`, never replaced by a new one (#696). A mismatch SHALL be returned to the caller, never retried.
The one exception is a review: an approval or a request for changes GitHub refuses because the account
wrote the pull request SHALL be written once more as a comment, whose body names the verdict, and the
review read back SHALL carry GitHub's sentence in `AsComment`.

#### Scenario: GitHub refuses an approval by the author
- **WHEN** the review submitted is an approval by the pull request's own author
- **THEN** a comment "Approval." is written on the head and read back, with `AsComment` "Review Can not approve your own pull request"

#### Scenario: GitHub refuses an approval for another reason
- **WHEN** GitHub refuses an approval with any other sentence
- **THEN** the write answers the mismatch in GitHub's words, and nothing else is written

### Requirement: The credential is the one gh already holds
The adapter SHALL run the `gh` CLI found on the login shell's PATH, with no pager, no prompt and no
terminal prompt for git, and SHALL never read or store the token itself. The identity the control plane
acts as SHALL be `gh api user`'s login. There SHALL be no screen to configure a source or a credential.

#### Scenario: A machine where gh is signed out
- **WHEN** `gh` is installed and not signed in
- **THEN** Settings shows the GitHub row as signed out with what `gh` said, and the PBIs screen names the folder as unread

### Requirement: A PBI's pull request is the one that closes it
`GET /api/pbi?repository&id` SHALL answer the issue, or 404 `Pbi.NotFound` for a missing issue or a
pull request, with its pull request: the highest-numbered pull request of the same repository that
GitHub records as closing it, with whether it is a draft, merged or approved. On GitHub it SHALL be
one GraphQL call, with the change's base, head and totals in the same answer (#621).

The issue and its pull request SHALL be asked for at once, not one after the other.

#### Scenario: A PBI before spec-ready ends
- **WHEN** a PBI has no pull request yet
- **THEN** its pull request is null and the header shows none

### Requirement: A read is kept ten seconds and forgotten on every write
Every `IPbiProvider` adapter SHALL be registered behind a cache that keeps each read of a repository,
a list of its PBIs (open, closed or found by a search), a PBI's page with comments, its children, its
pull request, its change with files and threads, its review with patches, checks and conversation, and
the files read at a reference, for ten seconds, keyed by the repository and what was read. Any write through the port to a repository, the person's or the engine's, SHALL forget
every read of that repository kept so far, including one still in flight. Two callers asking the same
read at once SHALL share one call to the vendor. A read that failed SHALL not be kept. The probe with the
identity, an attachment, the states and a branch's pull request SHALL be read from the vendor every time.
A request sent with `Cache-Control: no-cache` SHALL read the vendor again whatever is kept, and what it
reads SHALL replace the kept copy (#627).

#### Scenario: A fresh read
- **WHEN** a PBI is read, it changes on GitHub, and it is read again within ten seconds with `Cache-Control: no-cache`
- **THEN** `gh` is asked again, the read shows the change, and a plain read right after is answered from the new copy

#### Scenario: A repeated read
- **WHEN** a PBI and its pull request are read twice within ten seconds
- **THEN** `gh` is asked once for each

#### Scenario: A move then a read
- **WHEN** the engine moves a PBI and the PBI is read again at once
- **THEN** `gh` is asked again and the read shows the new state

### Requirement: An agent creates, edits and searches issues through aio issue
`IPbiProvider` SHALL create a PBI with no state, filed under a parent when one is named, edit a PBI's
body, and search a repository's PBIs open, closed or both, each write read back. The GitHub adapter
SHALL create with `gh api repos/<r>/issues`, file the new issue with
`POST repos/<r>/issues/<parent>/sub_issues` and the numeric id the create answered, and read it back
among the parent's children; a link GitHub refuses SHALL be a mismatch naming the issue that was
created. It SHALL edit with a `PATCH` of the issue's body and search with `gh issue list --search`.
`Modules.Pbis` SHALL serve them as `POST /api/pbi/create`, `POST /api/pbi/edit` and
`GET /api/pbi/search`. `.aio/aio issue view|list|create|edit|comment|children|move`
SHALL call these and the existing `/api/pbi`, `/api/pbi/issue`, `/api/pbi/issue/comment`,
`/api/pbi/children` and `/api/pbi/move` at `AIO_API` for `AIO_REPOSITORY`, and answer one JSON line,
`{issue | issues | comment, why}`, with exit 0 when the control plane answered, 1 when it refused or
could not be reached, and 2 when the arguments or the environment are refused; without `AIO_API` it
SHALL say what is missing and how to set it. `view --markdown` SHALL print the issue as text, body then
comments. A `--body-file` under the checkout's `.aio/` SHALL be removed once posted.

#### Scenario: A child is created
- **WHEN** an agent runs `aio issue create --title "Una hija" --body-file .aio/issue.md --parent 1`
- **THEN** the vendor holds a new issue with that title and body among #1's children, the script answers `{"issue": {…, "parent": "1"}, "why": "owner/repo#8 is created, under #1, with no state."}` with exit 0, and `.aio/issue.md` is gone

#### Scenario: No address
- **WHEN** `aio issue view 1` runs in a terminal without `AIO_API`
- **THEN** it answers exit 2 with a `why` that names `AIO_API` and the `export` that sets it

### Requirement: Installing the harness opens a pull request and creates the states
For installing the harness (#566), `IPbiProvider` SHALL read the open pull request from a branch of a
repository, open a pull request from a branch into the default branch, ready for review and closing no
PBI, and create the states of a loop with the hold. On GitHub the pull request SHALL be opened once: one
already open from the branch is the answer. A state label SHALL be created only where the repository
lacks it, and the write SHALL be read back from the repository's `status:*` labels, with every label
still missing named in the mismatch.

#### Scenario: A repository with one state already
- **WHEN** the states `spec-ready`, `verifying` and `done` are created on a repository that has `status:verifying`
- **THEN** `status:spec-ready`, `status:done` and `status:holding` are created, and the read-back has all three states
