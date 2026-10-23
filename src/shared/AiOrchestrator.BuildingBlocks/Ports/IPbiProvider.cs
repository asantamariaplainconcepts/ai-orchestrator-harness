namespace AiOrchestrator.BuildingBlocks.Ports;

/// <summary>
/// The vendor a PBI lives in: reads PBIs, moves their state, comments on them, and reads their pull
/// request and its review. The vendor is the truth: an adapter may keep a read a few seconds,
/// but every write through the port forgets what it kept of that repository (#617).
/// <para>
/// The state is a label on GitHub and a field on Azure DevOps, and the mapping between a
/// <c>loop.yaml</c> state and either is the adapter's. No caller names a label or a CLI.
/// </para>
/// <para>
/// Every write reads back what it wrote and returns a <see cref="Written{T}"/>: what the vendor shows
/// afterwards, and the mismatch when that is not what was meant. A write does not count until it has
/// been observed, and nothing here retries: the caller decides what a mismatch stops. The one
/// exception is a review the vendor refuses from the pull request's author, see <see cref="SubmitReviewAsync"/>.
/// </para>
/// <para>
/// A read the vendor could not answer — no credential, no network, a CLI that is not installed —
/// throws <see cref="PbiProviderException"/>. Absence is not a failure: it is null or an empty list.
/// </para>
/// <para>
/// Twenty-one doors, grouped by what a screen or a command reads, not by how the vendor answers (#666):
/// a read answers everything its screen paints in one call, and a write takes everything one edit
/// changes. Adapters: GitHub; later Azure DevOps. Several are registered at once and told apart by
/// <see cref="Name"/>.
/// </para>
/// </summary>
public interface IPbiProvider
{
    /// <summary>The vendor this adapter speaks to: <c>github</c>, later <c>azure-devops</c>.</summary>
    string Name { get; }

    // The vendor itself: how it is reached, and the states it has.

    /// <summary>
    /// Where the CLI this adapter reaches the vendor through is on this machine, which version, and
    /// the account it acts as, read from the vendor every time. The loop works a PBI only when it is
    /// unassigned or assigned to that account. A CLI that is there and names no account says why in
    /// <see cref="CliProbe.Problem"/>.
    /// </summary>
    Task<CliProbe> ProbeAsync(CancellationToken ct);

    /// <summary>
    /// The <c>loop.yaml</c> states the vendor already has for a repository, the hold not among them,
    /// ordered by name: what the folder page (#565) shows. A state not listed is created by the first
    /// <see cref="MoveAsync"/> to it, so a missing one is a fact, not a fault.
    /// <para>
    /// The default answers that this vendor does not say, so an adapter that cannot list its states
    /// is reported on the page rather than read as having none.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<string>> ListStatesAsync(string repository, CancellationToken ct) =>
        throw new PbiProviderException($"{Name} does not list the states it has.");

    /// <summary>
    /// Creates the <c>loop.yaml</c> states a repository lacks, and the hold, and returns every state it
    /// has afterwards, as <see cref="ListStatesAsync"/> reads them (#566). A state it already has is
    /// left as it is. The mismatch names each state still missing after the write.
    /// </summary>
    Task<Written<IReadOnlyList<string>>> CreateStatesAsync(string repository, IReadOnlyList<string> states, CancellationToken ct) =>
        throw new PbiProviderException($"{Name} does not create states.");

    // PBIs: what the PBIs screen, the Issue tab, the engine and `aio issue` read and write.

    /// <summary>
    /// The PBIs of a repository, such as <c>owner/name</c> on GitHub, that <paramref name="query"/>
    /// names: the open ones for the PBIs screen and the engine, the closed ones behind the screen's
    /// Done filter, or those the vendor's own search finds for a text (#634), in the vendor's order.
    /// </summary>
    Task<IReadOnlyList<Pbi>> ListAsync(string repository, PbiQuery query, CancellationToken ct);

    /// <summary>
    /// The PBI as its page at the vendor has it: the body, what it says about itself and every
    /// comment in the order it was written. Null when the vendor does not have it. A pull request
    /// under that id answers its page with <see cref="PbiThread.IsPullRequest"/>, and is never a PBI:
    /// a caller that needs only the PBI reads <see cref="PbiThread.AsPbi"/>. The Review tab reads the
    /// evidence from its comments.
    /// </summary>
    Task<PbiThread?> ReadThreadAsync(PbiRef pbi, CancellationToken ct);

    /// <summary>
    /// The PBIs the vendor files under this one as its children, sub-issues on GitHub, in the vendor's
    /// order and closed ones among them: the Issue tab paints each one's state on the epic's graph
    /// (#611). Empty when it has none or the vendor does not have the PBI.
    /// <para>
    /// The default answers that this vendor does not say, so the tab draws the body without the
    /// states rather than reading the PBI as having no children.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Pbi>> ListChildrenAsync(PbiRef pbi, CancellationToken ct) =>
        throw new PbiProviderException($"{Name} does not list a PBI's children.");

    /// <summary>
    /// Creates a PBI with no state and, when <paramref name="parent"/> is given, files it under that
    /// PBI as its child, and returns it as read back (#634). The mismatch says when the PBI was
    /// created but is not among the parent's children afterwards.
    /// </summary>
    /// <param name="parent">The vendor's id of the parent PBI in the same repository, or null.</param>
    Task<Written<Pbi>> CreateAsync(string repository, string title, string body, string? parent, CancellationToken ct) =>
        throw new PbiProviderException($"{Name} does not create PBIs.");

    /// <summary>Replaces the PBI's body and returns the body as read back (#634).</summary>
    Task<Written<string>> EditBodyAsync(PbiRef pbi, string body, CancellationToken ct) =>
        throw new PbiProviderException($"{Name} does not edit a PBI's body.");

    /// <summary>
    /// Moves the PBI to where <paramref name="move"/> says in one edit: its <c>loop.yaml</c> state,
    /// its hold, its assignment to <see cref="ProbeAsync"/>'s account, and whether it is closed, each
    /// left as it was when the move does not name it. Returns the PBI as read back. A move that
    /// changes nothing the vendor does not already show is not written.
    /// </summary>
    Task<Written<Pbi>> MoveAsync(PbiRef pbi, PbiMove move, CancellationToken ct);

    /// <summary>
    /// Adds a comment to the PBI, with <paramref name="attachments"/> uploaded by the vendor as the
    /// account this control plane acts as, and returns it as read back. The attachments are paths on
    /// this machine, none for a plain comment; the vendor links each one into the comment.
    /// </summary>
    Task<Written<PbiComment>> CommentAsync(PbiRef pbi, string body, IReadOnlyList<string> attachments, CancellationToken ct);

    /// <summary>
    /// Replaces the text of one comment on the PBI, as the account this control plane acts as, and
    /// returns it as read back (#696). The engine keeps one journey comment per PBI and edits it at
    /// every transition rather than adding another. A comment the vendor does not have is a mismatch.
    /// <para>
    /// The default says this vendor cannot, like <see cref="ListStatesAsync"/>'s.
    /// </para>
    /// </summary>
    /// <param name="commentId">The vendor's id for the comment, as <see cref="PbiComment.Id"/> gives it.</param>
    Task<Written<PbiComment>> EditCommentAsync(PbiRef pbi, string commentId, string body, CancellationToken ct) =>
        throw new PbiProviderException($"{Name} does not edit a comment.");

    /// <summary>
    /// The bytes of an image the vendor holds for the PBI's body or one of its comments, read with the machine's
    /// credential because a private repository's images answer nobody else. Null when the vendor does
    /// not have it. The caller decides which addresses may be read.
    /// </summary>
    Task<byte[]?> ReadAttachmentAsync(Uri address, CancellationToken ct);

    /// <summary>
    /// The bytes of each file of a repository at a commit or branch, or at the default branch when
    /// the <see cref="RepositoryFile.Reference"/> is null, in the order asked; null where there is no
    /// such file. GitHub reads several in one call (#621), and one by itself whatever its size.
    /// </summary>
    Task<IReadOnlyList<byte[]?>> ReadFilesAsync(string repository, IReadOnlyList<RepositoryFile> files, CancellationToken ct);

    // Pull requests: what the Proposal and Review tabs, `aio pr`, the engine and the harness install
    // read and write.

    /// <summary>The pull request that closes the PBI, or null when there is none yet.</summary>
    Task<PullRequestInfo?> GetPullRequestAsync(PbiRef pbi, CancellationToken ct);

    /// <summary>
    /// The open pull request from <paramref name="branch"/> of a repository, or null when there is
    /// none: what installing the harness (#566) asks about its own branch. The default says the
    /// vendor cannot, like <see cref="ListStatesAsync"/>'s.
    /// </summary>
    Task<PullRequestInfo?> GetBranchPullRequestAsync(string repository, string branch, CancellationToken ct) =>
        throw new PbiProviderException($"{Name} does not read a branch's pull request.");

    /// <summary>
    /// The PBI's pull request as a change: its base and head, every file it touches without its
    /// patch, and the review threads on it. What the Proposal tab paints and a comment on it checks
    /// against; GitHub answers it in one call. Null when the PBI has no pull request.
    /// </summary>
    Task<PullRequestChange?> GetPullRequestChangeAsync(PbiRef pbi, CancellationToken ct);

    /// <summary>
    /// What the Review tab paints and <c>aio pr view</c> reads of the PBI's pull request: the change
    /// with every file's patch and its threads, the checks on its head, and its conversation. Null
    /// when the PBI has no pull request. The checks can fail alone: then they are empty and
    /// <see cref="PullRequestReview.ChecksUnread"/> says why, because a reviewer with the diff and no
    /// checks can still read.
    /// </summary>
    Task<PullRequestReview?> ReadPullRequestReviewAsync(PbiRef pbi, CancellationToken ct);

    /// <summary>
    /// Opens the pull request <paramref name="draft"/> describes from its branch into the repository's
    /// default branch, and returns it as read back. A pull request already open from that branch is
    /// the answer rather than a second one; one still a draft when the draft asks for a ready one is
    /// marked ready for review, which is what the engine does when a PBI leaves <c>verifying</c>.
    /// </summary>
    Task<Written<PullRequestInfo>> OpenPullRequestAsync(string repository, PullRequestDraft draft, CancellationToken ct);

    /// <summary>
    /// Writes a review comment on the PBI's pull request, on its current head, and returns it as read
    /// back: on a line of its diff, or under the first comment of one of its threads when the draft
    /// names one. A PBI with no pull request is a mismatch with nothing observed.
    /// </summary>
    Task<Written<ReviewComment>> AddReviewCommentAsync(PbiRef pbi, ReviewCommentDraft draft, CancellationToken ct);

    /// <summary>
    /// Resolves a review thread of the PBI's pull request and returns it as read back. A thread on
    /// another pull request is a mismatch with nothing written.
    /// </summary>
    Task<Written<ReviewThread>> ResolveReviewThreadAsync(PbiRef pbi, string threadId, CancellationToken ct);

    /// <summary>
    /// Submits a review of the PBI's pull request on its current head, approving it or requesting
    /// changes or only commenting, and returns the review as read back. An approval is what releases
    /// the hold of <c>merge-review</c> (README.md#the-loop); a comment is what a request for changes
    /// on the proposal sends (#512). An approval or a request for changes the vendor refuses only
    /// because the reviewer wrote the pull request is written as a comment instead, and the review read
    /// back says why in <see cref="SubmittedReview.AsComment"/> (#544).
    /// </summary>
    Task<Written<SubmittedReview>> SubmitReviewAsync(PbiRef pbi, ReviewVerdict verdict, string body, CancellationToken ct);

    /// <summary>
    /// #734. What the Metrics screen reads of a repository at the vendor: every PBI with the one it is
    /// part of and when it closed, and the latest pull requests with their commits, whose committer and
    /// trailers tell a person's commit on a branch from the loop's (#741). The default says the vendor has no history to
    /// give, and the screen says so in its notes.
    /// </summary>
    Task<VendorHistory> ReadHistoryAsync(string repository, CancellationToken ct) =>
        throw new PbiProviderException($"{Name} does not read a repository's history.");
}

/// <summary>#734. A repository's PBIs and pull requests, as <see cref="IPbiProvider.ReadHistoryAsync"/> reads them.</summary>
/// <param name="Pbis">Every PBI, open and closed.</param>
/// <param name="PullRequests">The latest pull requests, newest first.</param>
/// <param name="MorePullRequests">The vendor has older pull requests than these, which were not read.</param>
public sealed record VendorHistory(IReadOnlyList<PbiLineage> Pbis, IReadOnlyList<BranchPullRequest> PullRequests, bool MorePullRequests);

/// <summary>A PBI, the PBI it is part of, and when it closed.</summary>
/// <param name="Parent">The id of the PBI it is part of, in the same repository, or null.</param>
/// <param name="ClosedAt">When the vendor closed it, or null while it is open.</param>
public sealed record PbiLineage(PbiRef Ref, string Title, string? Parent, DateTimeOffset? ClosedAt);

/// <summary>A pull request by the branch it comes from, with its commits oldest first.</summary>
/// <param name="Id">The vendor's id for it, such as <c>49</c>.</param>
/// <param name="Branch">The branch it comes from, such as <c>change/44</c>.</param>
/// <param name="MergedAt">When it was merged, or null when it was not.</param>
public sealed record BranchPullRequest(string Id, string Branch, DateTimeOffset? MergedAt, IReadOnlyList<BranchCommit> Commits);

/// <summary>One commit of a pull request.</summary>
/// <param name="At">When it was committed, as git recorded it.</param>
/// <param name="Authors">Each author's name, the co-authors included.</param>
/// <param name="Committer">The committer's name, which carries the loop's mark (<see cref="CommitMark"/>), or null.</param>
/// <param name="Trailers">The <c>Key: value</c> trailers of its message, which can carry the mark too.</param>
public sealed record BranchCommit(string Sha, DateTimeOffset At, IReadOnlyList<string> Authors, string? Committer = null, IReadOnlyList<string>? Trailers = null)
{
    /// <summary>Whether the loop made it: a run's or a script's mark, in the committer or a trailer (#741).</summary>
    public bool Marked => CommitMark.Marked(Committer, Trailers ?? []);
}

/// <summary>Which PBIs of a repository <see cref="IPbiProvider.ListAsync"/> answers.</summary>
/// <param name="State">Open, closed or both.</param>
/// <param name="Limit">At most this many, the latest first. 1,000 is more open PBIs than one repository is worth listing on one screen or working in one pass.</param>
/// <param name="Text">Words the vendor's own search matches, or null for every PBI in the state.</param>
public sealed record PbiQuery(PbiSearchState State = PbiSearchState.Open, int Limit = 1000, string? Text = null);

/// <summary>Which PBIs a <see cref="PbiQuery"/> looks among.</summary>
public enum PbiSearchState
{
    Open,
    Closed,
    All,
}

/// <summary>
/// Where <see cref="IPbiProvider.MoveAsync"/> leaves a PBI. What is null or false stays as it was:
/// the engine moves a state and leaves the hold, puts the hold on and leaves the state, or releases
/// and moves in the one edit.
/// </summary>
/// <param name="To">The <c>loop.yaml</c> state to move it to, or null to leave its state.</param>
/// <param name="Held">Whether a person must act before the loop does, or null to leave the hold.</param>
/// <param name="AssignToSelf">Assign it to the account this control plane acts as; one already assigned to it stays.</param>
/// <param name="Close">Close it; a PBI already closed is not written again.</param>
public sealed record PbiMove(string? To = null, bool? Held = null, bool AssignToSelf = false, bool Close = false);

/// <summary>A pull request still to be opened, as <see cref="IPbiProvider.OpenPullRequestAsync"/> takes it.</summary>
/// <param name="Branch">The branch it comes from; it merges into the repository's default branch.</param>
/// <param name="Title">Its title.</param>
/// <param name="Body">What it says. The vendor adds what makes it close <paramref name="Closes"/>.</param>
/// <param name="Draft">Whether it opens as a draft. One already open and still a draft is marked ready when this is false.</param>
/// <param name="Closes">The PBI it closes when merged, or null for a pull request that is no PBI's, such as the harness install's (#566).</param>
public sealed record PullRequestDraft(string Branch, string Title, string Body, bool Draft, PbiRef? Closes = null);

/// <summary>A pull request as its page at the vendor shows it, beside the diff and the threads on it.</summary>
/// <param name="Title">Its title.</param>
/// <param name="State"><c>open</c>, <c>closed</c> or <c>merged</c>.</param>
/// <param name="Author">Who opened it, or null for an account that no longer exists.</param>
/// <param name="Body">What it says, in the vendor's markdown.</param>
/// <param name="Comments">Every comment on the pull request as a whole, oldest first.</param>
/// <param name="Reviews">Every review submitted, oldest first, those with no body among them.</param>
public sealed record PullRequestConversation(
    string Title,
    string State,
    string? Author,
    string Body,
    IReadOnlyList<PbiComment> Comments,
    IReadOnlyList<PullRequestReviewNote> Reviews
);

/// <summary>A review of a pull request as the vendor recorded it, with when it was submitted.</summary>
/// <param name="Id">The vendor's id for it.</param>
/// <param name="Verdict">What it says, or null when the vendor recorded something that is neither, such as a review still pending.</param>
/// <param name="Author">Who wrote it, or null for an account that no longer exists.</param>
/// <param name="At">When it was submitted.</param>
/// <param name="Body">What it says beside the verdict; empty for a bare approval.</param>
public sealed record PullRequestReviewNote(string Id, ReviewVerdict? Verdict, string? Author, DateTimeOffset At, string Body);

/// <summary>A PBI as its page at the vendor shows it.</summary>
/// <param name="Pbi">The PBI.</param>
/// <param name="Url">Where a person opens it at the vendor.</param>
/// <param name="Author">Who opened it, or null for an account that no longer exists.</param>
/// <param name="CreatedAt">When it was opened.</param>
/// <param name="Body">What it says, in the vendor's markdown.</param>
/// <param name="Labels">Every label it carries, the state's and the hold's among them.</param>
/// <param name="Assignees">Everybody it is assigned to.</param>
/// <param name="Parent">The PBI it is part of, or null.</param>
/// <param name="BlockedBy">The PBIs it waits for.</param>
/// <param name="Comments">Every comment, oldest first.</param>
public sealed record PbiThread(
    Pbi Pbi,
    Uri Url,
    string? Author,
    DateTimeOffset CreatedAt,
    string Body,
    IReadOnlyList<string> Labels,
    IReadOnlyList<string> Assignees,
    LinkedPbi? Parent,
    IReadOnlyList<LinkedPbi> BlockedBy,
    IReadOnlyList<PbiComment> Comments
)
{
    /// <summary>Whether the vendor answered a pull request's page under that id.</summary>
    public bool IsPullRequest { get; init; }

    /// <summary>The PBI, or null when the page is a pull request's.</summary>
    public Pbi? AsPbi => IsPullRequest ? null : Pbi;
}

/// <summary>Another PBI one PBI points to, as its parent or as one that blocks it.</summary>
public sealed record LinkedPbi(PbiRef Ref, string Title, Uri Url, bool Closed);

/// <summary>Which PBI, in the vendor's own terms.</summary>
/// <param name="Repository">The repository or project, such as <c>owner/name</c>.</param>
/// <param name="Id">The vendor's id for it: the issue number on GitHub, the work item id on Azure DevOps.</param>
public sealed record PbiRef(string Repository, string Id);

/// <summary>A PBI as the vendor has it right now.</summary>
/// <param name="Ref">Which PBI.</param>
/// <param name="Title">Its title.</param>
/// <param name="State">Its <c>loop.yaml</c> state, or null when it carries none.</param>
/// <param name="Held">Whether a person must act before the loop does.</param>
/// <param name="Assignee">Who it is assigned to, or null. The loop leaves another person's PBI alone.</param>
/// <param name="Closed">Whether the vendor has closed it. An open <see cref="PbiQuery"/> lists only open ones.</param>
public sealed record Pbi(PbiRef Ref, string Title, string? State, bool Held, string? Assignee, bool Closed = false);

/// <summary>A PBI's pull request as the vendor has it right now.</summary>
/// <param name="Id">The vendor's id for it: the pull request number on GitHub.</param>
/// <param name="Url">Where a person reviews it.</param>
/// <param name="IsDraft">Whether it is still a draft.</param>
/// <param name="IsMerged">Whether it has been merged.</param>
/// <param name="IsApproved">Whether a reviewer approved it, which releases the hold of <c>merge-review</c>.</param>
/// <param name="IsClosed">Whether it was closed without a merge, which leaves nothing to review (#719).</param>
public sealed record PullRequestInfo(string Id, Uri Url, bool IsDraft, bool IsMerged, bool IsApproved, bool IsClosed = false);

/// <summary>A comment on a PBI.</summary>
/// <param name="Id">The vendor's id for it.</param>
/// <param name="Author">Who wrote it, or null for an account that no longer exists.</param>
/// <param name="CreatedAt">When it was written.</param>
/// <param name="Body">What it says, in the vendor's markdown.</param>
public sealed record PbiComment(string Id, string? Author, DateTimeOffset CreatedAt, string Body)
{
    /// <summary>Where a person opens it at the vendor, when the vendor said.</summary>
    public Uri? Url { get; init; }
}

/// <summary>A review comment anchored to a line of a pull request's diff.</summary>
/// <param name="Id">The vendor's id for it.</param>
/// <param name="Path">The file, relative to the repository root.</param>
/// <param name="Line">
/// The line in the file's new version, or null when the code it was written on has since changed and
/// the vendor no longer places it.
/// </param>
/// <param name="Author">Who wrote it, or null for an account that no longer exists.</param>
/// <param name="CreatedAt">When it was written.</param>
/// <param name="Body">What it says, in the vendor's markdown.</param>
public sealed record ReviewComment(
    string Id,
    string Path,
    int? Line,
    string? Author,
    DateTimeOffset CreatedAt,
    string Body
);

/// <summary>
/// A review comment still to be written: anchored to <paramref name="Path"/> and <paramref name="Line"/>
/// of the diff, or, when <paramref name="InReplyTo"/> is given, under the thread that comment opened.
/// </summary>
/// <param name="Body">What it says.</param>
/// <param name="Path">The file, relative to the repository root, for a comment on a line.</param>
/// <param name="Line">The line in the file's new version. It has to be part of the pull request's diff.</param>
/// <param name="InReplyTo">The id of a thread's first comment, as <see cref="ReviewComment.Id"/> gives it, for a reply.</param>
public sealed record ReviewCommentDraft(string Body, string? Path = null, int? Line = null, string? InReplyTo = null);

/// <summary>
/// What a write left behind: the thing as the vendor shows it afterwards, and whether that is what
/// was meant.
/// </summary>
/// <param name="Observed">What the read-back found, or null when there was nothing to read back.</param>
/// <param name="Mismatch">
/// Null when the read-back matches the write. Otherwise one sentence naming what was meant and what
/// was observed, or why the write did not happen, for the person the loop stops for.
/// </param>
public sealed record Written<T>(T? Observed, string? Mismatch)
    where T : class
{
    /// <summary>The vendor shows what was written.</summary>
    public bool Matches => Mismatch is null;
}

/// <summary>
/// A read the vendor could not answer: no credential, no network, or no CLI to ask with. The message
/// is the vendor's own first line, so the person reading it sees the cause and not a paraphrase.
/// </summary>
public sealed class PbiProviderException : Exception
{
    public PbiProviderException() { }

    public PbiProviderException(string message)
        : base(message) { }

    public PbiProviderException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>A PBI's pull request as a change: where it starts and ends, what it touches, and the threads on it.</summary>
/// <param name="PullRequest">The pull request itself.</param>
/// <param name="BaseRef">The branch it merges into, such as <c>main</c>.</param>
/// <param name="BaseSha">The commit that branch points at.</param>
/// <param name="HeadRef">The branch it comes from.</param>
/// <param name="HeadSha">The commit the branch points at, which a review is written on.</param>
/// <param name="Commits">How many commits it carries.</param>
/// <param name="Additions">Lines added across every file.</param>
/// <param name="Deletions">Lines removed across every file.</param>
/// <param name="Files">The files it touches, in the vendor's order.</param>
/// <param name="ChangedFiles">How many files the vendor says it touches, which is more than <paramref name="Files"/> when the vendor stopped listing.</param>
/// <param name="Threads">The review threads on it, each anchored to a file and a line, with its comments in order.</param>
public sealed record PullRequestChange(
    PullRequestInfo PullRequest,
    string BaseRef,
    string BaseSha,
    string HeadRef,
    string HeadSha,
    int Commits,
    int Additions,
    int Deletions,
    IReadOnlyList<ChangedFile> Files,
    int ChangedFiles,
    IReadOnlyList<ReviewThread> Threads
)
{
    /// <summary>Every comment of every thread, as the Proposal tab lists them beside the change's files.</summary>
    public IEnumerable<ReviewComment> Comments => Threads.SelectMany(thread => thread.Comments);
}

/// <summary>One file of a change.</summary>
/// <param name="Path">The file, relative to the repository root.</param>
/// <param name="PreviousPath">Where it was before a rename, or null when the change was read without it.</param>
/// <param name="Status"><c>added</c>, <c>removed</c>, <c>modified</c>, <c>renamed</c>, <c>copied</c>, <c>changed</c> or <c>unchanged</c>.</param>
/// <param name="Additions">Lines added.</param>
/// <param name="Deletions">Lines removed.</param>
/// <param name="Patch">
/// Its hunks in unified form, which only <see cref="IPbiProvider.ReadPullRequestReviewAsync"/> reads,
/// or null when the vendor sends none: a binary file, or one too large.
/// </param>
public sealed record ChangedFile(string Path, string? PreviousPath, string Status, int Additions, int Deletions, string? Patch);

/// <summary>A conversation anchored to a line of a pull request's diff.</summary>
/// <param name="Id">The vendor's id for the thread, which resolving names.</param>
/// <param name="Path">The file, relative to the repository root.</param>
/// <param name="Line">The line in the file's new version, or null when the vendor no longer places it.</param>
/// <param name="IsResolved">Whether somebody resolved it.</param>
/// <param name="IsOutdated">Whether the code it was written on has changed since.</param>
/// <param name="Comments">Its comments, the first one opening it.</param>
public sealed record ReviewThread(
    string Id,
    string Path,
    int? Line,
    bool IsResolved,
    bool IsOutdated,
    IReadOnlyList<ReviewComment> Comments
);

/// <summary>What a review says of a pull request as a whole.</summary>
public enum ReviewVerdict
{
    Approve,
    RequestChanges,

    /// <summary>
    /// Neither: a review that only comments. What a request for changes on the proposal sends,
    /// because GitHub refuses a request for changes from the author of the pull request (#512).
    /// </summary>
    Comment,
}

/// <summary>A review as the vendor recorded it.</summary>
/// <param name="Id">The vendor's id for it.</param>
/// <param name="Verdict">What it says, or null when the vendor recorded something that is neither.</param>
/// <param name="Author">Who wrote it.</param>
/// <param name="Body">What it says beside the verdict.</param>
/// <param name="AsComment">
/// Why the vendor kept it as a comment and not as the verdict asked for, in the vendor's words, or null
/// when it kept the verdict. GitHub refuses an approval or a request for changes from the pull request's
/// author, and the loop opens its pull requests as the person who reviews them (#544).
/// </param>
public sealed record SubmittedReview(string Id, ReviewVerdict? Verdict, string? Author, string Body, string? AsComment = null);

/// <summary>One file of a repository to read, as <see cref="IPbiProvider.ReadFilesAsync"/> names it.</summary>
/// <param name="Reference">A commit or branch, or null for the default branch.</param>
/// <param name="Path">Relative to the repository root.</param>
public sealed record RepositoryFile(string? Reference, string Path);

/// <summary>What the Review tab reads of a pull request, as <see cref="IPbiProvider.ReadPullRequestReviewAsync"/> answers it.</summary>
/// <param name="Change">The change, with every file's patch and its threads.</param>
/// <param name="Checks">The checks on its head; empty when they could not be read.</param>
/// <param name="ChecksUnread">Why the checks could not be read, in the vendor's words, or null.</param>
/// <param name="Conversation">Its title, body, comments and reviews, as <c>aio pr view</c> prints them (#651).</param>
public sealed record PullRequestReview(
    PullRequestChange Change,
    IReadOnlyList<PullRequestCheck> Checks,
    string? ChecksUnread,
    PullRequestConversation Conversation
);

/// <summary>Where a check on a pull request's head stands.</summary>
public enum CheckState
{
    Passing,
    Failing,
    Pending,

    /// <summary>Ended without a verdict: skipped, neutral or cancelled.</summary>
    Neutral,
}

/// <summary>One check the vendor reports on a pull request's head.</summary>
/// <param name="Name">Its name.</param>
/// <param name="State">Where it stands.</param>
/// <param name="Url">Where a person reads it, or null.</param>
public sealed record PullRequestCheck(string Name, CheckState State, Uri? Url);
