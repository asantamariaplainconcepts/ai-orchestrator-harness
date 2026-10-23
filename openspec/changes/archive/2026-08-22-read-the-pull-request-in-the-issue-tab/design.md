## Context

`IPullRequestLookup.FindUrl` answers with a nullable string: the address of the pull request a
branch has, or `null`. Six code paths return that `null` — no directory, `gh` not startable, a
timeout, a non-zero exit, an unparseable address, a scheme that is not `http(s)` — and the interface
documents why they are one answer: "the screen's response to all of them is the same disabled button,
and distinguishing them would be a tooltip nobody can act on." That was true while the only consumer
was a button whose whole job was to leave.

The consumer changes, so the reasoning has to be redone rather than inherited. A tab that draws the
pull request would draw *this branch has no pull request* over a `gh` that was merely signed out.

**What the repository actually holds**, measured before designing rather than assumed
(`gh pr list --state all --limit 300 --json number,comments,reviews,body`): 101 pull requests,
183,344 characters of body, **3,524 characters of comments across 5 of them**, and zero reviews. The
issue's premise leaned on `task-details`' opposite measurement — 351,931 characters of issue body
against 514,934 of issue comments — and the ratio here runs 52:1 the other way. The loop explains it:
`/aio:*` writes on the issue, the pull request opens as a draft, and review is a person reading a
diff. That does not remove the conversation from scope — the product manages any folder and any
tracker, and other repositories are not this one — but it decides which half carries the weight and
what the empty state means.

## Goals / Non-Goals

**Goals:**

- One process call per open, no state kept between opens, answering the pull request of a worktree's
  branch: number, title, state, draft, address, body, conversation.
- Three answers a screen can tell apart without parsing a message: **found**, **none**, **could not
  ask** — the last carrying the vendor's own words.
- The Issue tab reads all three, and the run header loses three buttons.

**Non-Goals:**

- Any write. No comment, no review, no merge.
- Cached or pushed pull request state, and badges in the rail. Declined in #114 and still declined.
- Inline review comments anchored to diff lines, which live under the vendor's `reviews`.
- Anything in the merge gate. Its Checks tile reads local commands (#188, #189).

## Decisions

### `gh pr list --head`, not `gh pr view` — the discriminator is structural, not a string

The problem is telling "no pull request" from "could not ask" when both leave `gh` with a non-zero
exit. Verified against the real CLI in this checkout:

```
$ gh pr view no-such-branch-xyz --json url --jq .url
no pull requests found for branch "no-such-branch-xyz"          # exit 1
$ gh pr list --head no-such-branch-xyz --state all --json number --limit 1
[]                                                              # exit 0
```

`gh pr list --head <branch>` reports absence as **exit 0 with an empty array**. Absence stops being
a failure to classify and becomes data. Every non-zero exit is then, by construction, "could not
ask" — no `gh`, not signed in, no GitHub remote, a network refusal, a timeout — and none of them can
be mistaken for an empty branch.

*Alternative considered and rejected:* keep `pr view` and classify its stderr, matching
`no pull requests found for branch`. It works today and reads the vendor's English. A message
reworded upstream would silently reclassify "no pull request" as "could not ask" or the reverse, and
the second direction is the one that invents a fact. Rejected for a discriminator that cannot drift.

One call still answers everything, so the seam keeps the property its doc claims. Verified:

```
$ gh pr list --head change/233 --state all --limit 1 \
    --json number,title,state,isDraft,url,body,comments
[{"number":234,"title":"…","state":"MERGED","isDraft":false,"url":"…","body":"…","comments":[]}]
```

`--state all` on purpose: a merged or closed pull request is exactly what somebody opening a
worktree after the fact needs to see, and filtering to open would answer "none" about a branch that
has already landed.

### Three answers as a record and an enum, following `DesktopResult`

`World` holds no `ErrorOr` — that is the use-case layer's vocabulary. The seam that already models
"succeeded, or failed for one of these named reasons" is `IDesktopIntegration`, returning
`DesktopResult(DesktopFailure? Reason)` over a `DesktopFailure` enum, and `OpenOnDesktop` turns each
value into its own sentence. This follows it rather than inventing a second shape:

```csharp
public sealed record PullRequestResult(PullRequest? PullRequest, PullRequestUnreachable? Reason)
{
    public bool Found => PullRequest is not null;
    public static PullRequestResult Of(PullRequest pullRequest);
    public static PullRequestResult None { get; }                  // both null: asked, none there
    public static PullRequestResult Unreachable(PullRequestUnreachable reason);
}

public enum PullRequestUnreachable { CliMissing = 1, NotSignedIn = 2, NoGitHubRemote = 3, TimedOut = 4, Failed = 5 }
```

`None` as *both fields null* is `DesktopResult.Ok`'s trick read the other way, and it keeps the three
states in one type with no nullable-of-nullable. `FindUrl` goes; nothing else called it.

*On classifying the unreachable reasons:* the exit code alone cannot separate them, so this is the
one place a message is read — but the fallback is `Failed`, carrying `gh`'s own stderr line. An
unrecognised message degrades to a sentence in the tool's words, which is what `AGENTS.md` asks for
anyway, and never to "there is none". The direction of the safe default is the whole point.

### A query on the folder, addressed like `GetDiff`

`GET /api/folders/{id:guid}/pull-request?worktree=<path>`, one use case in
`Modules/Workspace/Features/Folders/UseCases/`, `Access.FiltersToCaller`. It matches `GetDiff`'s
route and parameter exactly — the same question shape about the same scope — and it lands in
Workspace because Workspace owns folders and worktrees (`GetFolder`, `CreateWorktree`,
`RemoveWorktree`, `OpenOnDesktop`).

*Alternative considered and rejected:* keep it in `Modules/Runs` beside the code being deleted. A
pull request belongs to a branch; several runs of one issue share that branch; asking through
whichever run is open is the error #185 already corrected for the issue itself. Runs stops being
asked about pull requests at all, which is why `OpenRunPullRequest.cs` goes rather than being ported.

### The empty conversation is a sentence, and the body is the payload

The measurement above decides the shape. The body is rendered in full through `Prose` — no truncation
behind a "show more", because at 183,344 characters against 3,524 it is what a reader came for. The
conversation is drawn by the same component shape `IssueComments` uses, and its empty state is one
plain sentence, matching the reasoning already written there for the 33 of 92 issues with no
comments: "Empty is the ordinary answer here, so it is a sentence and not an empty state with a
picture." Here it is the ordinary answer 96 times out of 101.

### The pull request goes above the issue, and the tab keeps one heading each

The Issue tab already composes `IssueView` with a `children` slot between the body and the thread,
which the check list occupies. The pull request is not that: it is a second subject, so it goes
*above* `IssueView` rather than into its slot — the tab reads pull request, then issue, which is the
order somebody opening a worktree mid-review wants. `IssueView` is untouched, and no third caller of
it changes.

### The run header loses three buttons and gains nothing

Unconditional, per the issue: every run, both habitats, no test on how the run reached the screen.
`openTarget`, `openPullRequest` and their imports go with them. The header keeps the badge, the task,
the path, the cost and Stop/Resume — and Stop/Resume stays because it acts on the run, which is what
the header is for. The `compact` branch loses three of its four cases and keeps working unchanged for
the one that is left.

## Risks / Trade-offs

**A run opened at `/runs/:runId`, outside any worktree screen, loses the editor and the file manager
with nothing beside it** → Accepted, and it is the issue's explicit decision. It is the same cost
#185 took for the issue tab and wrote down rather than discovered: the answer is one navigation to
the worktree screen, not one press. Recorded here so the next reader finds the reasoning instead of
the gap.

**Finder moves from one press to two**, since the worktree screen keeps it inside the `SplitButton`'s
menu → Accepted, not compensated. Promoting it to the row would collide with the reason that menu
gives for Finder not being the main press: the main press stores its value as the editor preference,
and the server would then try to launch Finder as an editor.

**`--state all` can answer with a pull request from a branch name reused after a merge** → The branch
is the worktree's, and a worktree whose branch was merged and recreated is a state this product
already treats as one branch. Showing the merged pull request is the true answer about that branch,
and its state says `MERGED` on the tile.

**Reading a body and a thread from the vendor on every tab open costs a network call where the old
button cost one per click** → The Issue tab is already a vendor read: `useTask` plus
`IssueComments`' own fetch. One more read on the same open, on the same rule — on demand, never
mirrored — and no poll.

**The 20-second timeout now blocks content rather than a button** → It stays 20 seconds, and
`TimedOut` is a distinct `PullRequestUnreachable` so the tab says the vendor did not answer in time
rather than showing an empty section. `GitCli`'s number for `GitCli`'s reason.

## Migration Plan

No data and no stored state, so there is nothing to migrate and nothing to roll back but code. The
removals are the only breaking part: `POST /api/runs/{id:guid}/pull-request` disappears in the same
change as its single caller, so no client is left asking for it.

## Open Questions

None. The two the issue left open were settled above with the measurement and the CLI check: which
half of the pull request carries the weight, and how absence is told apart from failure.
