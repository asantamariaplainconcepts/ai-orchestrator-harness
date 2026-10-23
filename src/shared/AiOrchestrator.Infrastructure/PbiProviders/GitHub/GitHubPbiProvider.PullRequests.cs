using System.Globalization;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.Infrastructure.PbiProviders.GitHub;

/// <summary>
/// The pull request half of the adapter: the pull request that closes a PBI, the one open from a
/// branch, the change with its files and threads, the review with its checks and conversation, and
/// the writes on it (#513, #621, #625, #651).
/// <para>
/// <b>One GraphQL call per read.</b> <see cref="PullRequest"/> asks for the pull request that closes
/// the issue and adds whatever the caller needs of it to the same query: the files, the threads, the
/// conversation. Only GraphQL knows a thread as a thing with an id that can be resolved and a flag
/// for code that moved under it; the REST comments are what a reply is addressed to. The query reads
/// 100 of each, more than a pull request of this loop carries; past that the vendor stops listing and
/// so does this.
/// </para>
/// <para>
/// <b>A review cannot be written by the author of the pull request.</b> GitHub refuses an approval
/// or a request for changes from the account that opened it. That one refusal is written again as a
/// comment, which GitHub takes from the author, and the review read back carries GitHub's sentence
/// (#544). Any other refusal is the mismatch.
/// </para>
/// </summary>
sealed partial class GitHubPbiProvider
{
    /// <summary>Where an image of a comment is read from; tests answer in its place.</summary>
    internal GhBytes Bytes { get; init; } = GhCli.ReadBytes;

    /// <summary>What every read of a PBI's pull request asks for; <see cref="PullRequest"/> adds the rest.</summary>
    const string PullFields =
        "number url isDraft state reviewDecision headRefOid headRefName baseRefName baseRefOid "
        + "additions deletions changedFiles commits { totalCount } repository { nameWithOwner }";

    const string ThreadFields =
        "id isResolved isOutdated path line "
        + "comments(first: 100) { nodes { databaseId author { login } body createdAt } }";

    /// <summary>What <see cref="PullRequest"/> adds to its query for the review threads.</summary>
    const string ThreadsSelection = "reviewThreads(first: 100) { nodes { " + ThreadFields + " } }";

    /// <summary>
    /// What <see cref="PullRequest"/> adds to its query for the files. GraphQL lists them without a
    /// patch or a previous path, which only the REST listing carries.
    /// </summary>
    const string FilesSelection = "files(first: 100) { pageInfo { hasNextPage } nodes { path changeType additions deletions } }";

    /// <summary>
    /// What <see cref="PullRequest"/> adds to its query for the conversation (#651). GraphQL's
    /// <c>comments</c> of a pull request are the ones on it as a whole; those anchored to a line are
    /// the threads'.
    /// </summary>
    const string ConversationSelection =
        "title body author { login } "
        + "comments(first: 100) { nodes { databaseId author { login } createdAt body url } } "
        + "reviews(first: 100) { nodes { databaseId author { login } state body submittedAt } }";

    const string ThreadQuery =
        "query($id: ID!) { node(id: $id) { ... on PullRequestReviewThread { " + ThreadFields
        + " pullRequest { number repository { nameWithOwner } } } } }";

    const string ResolveMutation =
        "mutation($id: ID!) { resolveReviewThread(input: { threadId: $id }) { thread { id } } }";

    public async Task<PullRequestInfo?> GetPullRequestAsync(PbiRef pbi, CancellationToken ct) =>
        (await PullRequest(pbi, ct))?.Info;

    public Task<PullRequestInfo?> GetBranchPullRequestAsync(string repository, string branch, CancellationToken ct)
    {
        Repository(repository);
        return FromBranch(repository, branch, ct);
    }

    /// <summary>
    /// One call: the pull request with its files and threads from GraphQL. One of more than a page of
    /// files lists them again from REST, which pages to the end.
    /// </summary>
    public async Task<PullRequestChange?> GetPullRequestChangeAsync(PbiRef pbi, CancellationToken ct)
    {
        var pull = await PullRequest(pbi, ct, FilesSelection + " " + ThreadsSelection);
        if (pull is null)
        {
            return null;
        }

        return Change(pull, pull.MoreFiles ? await ListFiles(pbi.Repository, pull.Info.Id, ct) : pull.Files ?? []);
    }

    /// <summary>
    /// Four calls at once when the pull request comes from <c>change/&lt;n&gt;</c>, the branch the
    /// loop claims a PBI with (ADR-0005): the pull request with its threads and conversation from
    /// GraphQL, and, by the branch's name, the change with its patches from the compare endpoint and
    /// both kinds of check (#625). An answer by the branch is kept only when it is of the pull
    /// request's head and base. A branch that is gone, as after a merge, or one named otherwise,
    /// falls back to #621's second wave: the files and the checks asked again by the pull request
    /// and its head.
    /// </summary>
    public async Task<PullRequestReview?> ReadPullRequestReviewAsync(PbiRef pbi, CancellationToken ct)
    {
        Number(pbi);
        var branch = $"change/{pbi.Id}";
        var pulling = PullRequest(pbi, ct, ThreadsSelection + " " + ConversationSelection);
        var comparing = Compared(pbi.Repository, branch, ct);
        var checkingBranch = BranchChecks(pbi.Repository, branch, ct);
        await Task.WhenAll(pulling, comparing, checkingBranch);
        var pull = await pulling;
        if (pull is null)
        {
            return null;
        }

        var ours = pull.HeadRef == branch;
        var compared = await comparing;
        var byBranch = await checkingBranch;
        var files = ours && compared is { } change && change.Head == pull.Head && change.MergeBase == pull.BaseSha && change.Files.Count == pull.ChangedFiles
            ? change.Files
            : null;
        var onHead = ours && byBranch is { } early && early.Of == pull.Head ? early.Checks : null;

        var listing = files is null ? ListFiles(pbi.Repository, pull.Info.Id, ct) : Task.FromResult(files);
        var checking = onHead is null ? ChecksOrWhy(pbi.Repository, pull.Head, ct) : Task.FromResult((onHead, (string?)null));
        await Task.WhenAll(listing, checking);
        var (checks, unread) = await checking;

        return new PullRequestReview(
            Change(pull, await listing),
            checks,
            unread,
            pull.Conversation ?? new PullRequestConversation(string.Empty, string.Empty, null, string.Empty, [], [])
        );
    }

    public async Task<Written<PullRequestInfo>> OpenPullRequestAsync(string repository, PullRequestDraft draft, CancellationToken ct)
    {
        Repository(repository);
        if (string.IsNullOrWhiteSpace(draft.Branch))
        {
            throw new ArgumentException("A pull request needs the branch it comes from.", nameof(draft));
        }

        if (draft.Closes is { } closes && await Issue(closes, ct) is null)
        {
            return new(null, $"{Named(closes)} does not exist, so no pull request was opened for it.");
        }

        string? refused;
        if (await FromBranch(repository, draft.Branch, ct) is { } open)
        {
            if (draft.Draft || !open.IsDraft)
            {
                return new(open, null);
            }

            refused = Refusal(await _gh(["pr", "ready", open.Id, "--repo", repository], ct));
        }
        else if (draft.Closes is { } pbi && !draft.Draft)
        {
            // The engine marks a PBI's pull request ready; it opens one only as a draft.
            return new(null, $"{Named(pbi)} has no pull request, so none was marked ready for review.");
        }
        else
        {
            // No --base: the repository's default branch, the only one GitHub records "Closes #n" for,
            // and the one the harness install started its branch from.
            List<string> create = ["pr", "create", "--repo", repository, "--head", draft.Branch];
            if (draft.Draft)
            {
                create.Add("--draft");
            }
            create.AddRange(["--title", draft.Title, "--body", Body(draft)]);
            refused = Refusal(await _gh(create, ct));
        }

        PullRequestInfo? after;
        try
        {
            after = await FromBranch(repository, draft.Branch, ct);
        }
        catch (PbiProviderException failure)
        {
            return new(null, $"The pull request from {draft.Branch} could not be read back: {failure.Message}");
        }

        return after switch
        {
            null => new(null, $"No pull request from {draft.Branch} is open after the create{Because(refused)}."),
            { IsDraft: false } when draft.Draft => new(after, $"The pull request from {draft.Branch} should be a draft, and reads ready for review."),
            { IsDraft: true } when !draft.Draft => new(after, $"The pull request from {draft.Branch} should be ready for review, and reads a draft{Because(refused)}."),
            _ => new(after, null),
        };
    }

    /// <summary>The body as sent: what the draft says, and the line GitHub records a closing pull request by.</summary>
    static string Body(PullRequestDraft draft) =>
        draft.Closes is { } closes
            ? draft.Body.Length == 0 ? $"Closes #{closes.Id}" : $"{draft.Body}\n\nCloses #{closes.Id}"
            : draft.Body;

    public async Task<Written<ReviewComment>> AddReviewCommentAsync(PbiRef pbi, ReviewCommentDraft draft, CancellationToken ct)
    {
        var reply = draft.InReplyTo is not null;
        if (string.IsNullOrWhiteSpace(draft.Body))
        {
            throw new ArgumentException("A review comment needs a body.", nameof(draft));
        }

        if (reply && !long.TryParse(draft.InReplyTo, NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            throw new ArgumentException("A reply needs the id of the thread's first comment.", nameof(draft));
        }

        if (!reply && (string.IsNullOrWhiteSpace(draft.Path) || draft.Line is null or < 1))
        {
            throw new ArgumentException("A review comment needs a path and a line from 1.", nameof(draft));
        }

        var what = reply ? $"reply to comment {draft.InReplyTo}" : $"comment on {draft.Path}:{draft.Line}";
        var pull = await PullRequest(pbi, ct);
        if (pull is null)
        {
            return new(null, $"{Named(pbi)} has no pull request, so the {what} was not written.");
        }

        var posted = await _gh(
            reply
                ? ["api", $"repos/{pbi.Repository}/pulls/{pull.Info.Id}/comments/{draft.InReplyTo}/replies", "--method", "POST", "-f", $"body={draft.Body}"]
                :
                [
                    "api", $"repos/{pbi.Repository}/pulls/{pull.Info.Id}/comments", "--method", "POST",
                    "-f", $"body={draft.Body}",
                    "-f", $"path={draft.Path}",
                    "-F", $"line={draft.Line!.Value.ToString(CultureInfo.InvariantCulture)}",
                    "-f", "side=RIGHT",
                    "-f", $"commit_id={pull.Head}",
                ],
            ct
        );
        if (!posted.Succeeded)
        {
            return new(null, $"The {what} of pull request {pull.Info.Id} was not written: {Why(posted)}");
        }

        var id = Id(posted.StandardOutput);
        ReviewComment observed;
        try
        {
            using var document = Parse(await Ask(["api", $"repos/{pbi.Repository}/pulls/comments/{id}"], ct));
            observed = Review(document.RootElement);
        }
        catch (PbiProviderException failure)
        {
            return new(null, $"The {(reply ? "reply" : "comment")} {id}{(reply ? "" : $" on {draft.Path}:{draft.Line}")} was written but could not be read back: {failure.Message}");
        }

        if (reply)
        {
            return Same(observed.Body, draft.Body)
                ? new(observed, null)
                : new(observed, $"The reply {id} reads back different from what was written.");
        }

        return observed.Path == draft.Path && observed.Line == draft.Line && Same(observed.Body, draft.Body)
            ? new(observed, null)
            : new(
                observed,
                $"The comment {id} was meant for {draft.Path}:{draft.Line} and reads back on "
                    + $"{observed.Path}:{observed.Line?.ToString(CultureInfo.InvariantCulture) ?? "no line"}"
                    + $"{(Same(observed.Body, draft.Body) ? "" : ", with a different body")}."
            );
    }

    public async Task<Written<ReviewThread>> ResolveReviewThreadAsync(PbiRef pbi, string threadId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(threadId))
        {
            throw new ArgumentException("Resolving needs the thread's id.", nameof(threadId));
        }

        var pull = await PullRequest(pbi, ct);
        if (pull is null)
        {
            return new(null, $"{Named(pbi)} has no pull request, so no thread of it was resolved.");
        }

        // A thread id is global, so it is checked against this pull request before anything is written.
        var before = await ThreadOf(threadId, ct);
        if (before is null || !string.Equals(before.Value.Repository, pbi.Repository, StringComparison.OrdinalIgnoreCase) || before.Value.Number != pull.Info.Id)
        {
            return new(null, $"Thread {threadId} is not on pull request {pull.Info.Id} of {pbi.Repository}, so it was not resolved.");
        }

        if (before.Value.Thread.IsResolved)
        {
            return new(before.Value.Thread, null);
        }

        var refused = Refusal(
            await _gh(["api", "graphql", "-f", $"query={ResolveMutation}", "-f", $"id={threadId}"], ct)
        );

        (ReviewThread Thread, string Repository, string Number)? after;
        try
        {
            after = await ThreadOf(threadId, ct);
        }
        catch (PbiProviderException failure)
        {
            return new(null, $"Thread {threadId} was resolved but could not be read back: {failure.Message}");
        }

        return after is { Thread.IsResolved: true }
            ? new(after.Value.Thread, null)
            : new(after?.Thread, $"Thread {threadId} should be resolved, and reads open after the edit{Because(refused)}.");
    }

    public async Task<Written<SubmittedReview>> SubmitReviewAsync(
        PbiRef pbi,
        ReviewVerdict verdict,
        string body,
        CancellationToken ct
    )
    {
        var what = verdict switch
        {
            ReviewVerdict.Approve => "approval",
            ReviewVerdict.RequestChanges => "request for changes",
            _ => "review",
        };
        if (verdict != ReviewVerdict.Approve && string.IsNullOrWhiteSpace(body))
        {
            // GitHub refuses one without a body, and the person asking for changes owes the coder a why.
            throw new ArgumentException("A request for changes needs a body.", nameof(body));
        }

        var pull = await PullRequest(pbi, ct);
        if (pull is null)
        {
            return new(null, $"{Named(pbi)} has no pull request, so the {what} was not written.");
        }

        var posted = await _gh(ReviewArguments(pbi.Repository, pull.Info.Id, pull.Head, verdict, body), ct);
        string? asComment = null;
        if (!posted.Succeeded && verdict != ReviewVerdict.Comment && Why(posted) is var refusal && IsOwnPullRequest(refusal))
        {
            // The loop opens its pull requests with the person's account, so the person reviewing is
            // their author. GitHub takes a comment from the author, and that is what goes on instead.
            asComment = refusal;
            verdict = ReviewVerdict.Comment;
            body = string.IsNullOrWhiteSpace(body) ? $"{Capitalized(what)}." : $"{Capitalized(what)}:\n\n{body}";
            posted = await _gh(ReviewArguments(pbi.Repository, pull.Info.Id, pull.Head, verdict, body), ct);
            if (!posted.Succeeded)
            {
                return new(
                    null,
                    $"The {what} of pull request {pull.Info.Id} was refused ({refusal}), and the comment in its place was not written: {Why(posted)}"
                );
            }
        }

        if (!posted.Succeeded)
        {
            return new(null, $"The {what} of pull request {pull.Info.Id} was not written: {Why(posted)}");
        }

        var id = Id(posted.StandardOutput);
        SubmittedReview observed;
        try
        {
            using var document = Parse(await Ask(["api", $"repos/{pbi.Repository}/pulls/{pull.Info.Id}/reviews/{id}"], ct));
            var row = document.RootElement;
            observed = new SubmittedReview(id, Verdict(Text(row, "state")), Author(row), Text(row, "body") ?? string.Empty, asComment);
        }
        catch (PbiProviderException failure)
        {
            return new(null, $"The {what} {id} was written but could not be read back: {failure.Message}");
        }

        return observed.Verdict == verdict && Same(observed.Body, body)
            ? new(observed, null)
            : new(observed, $"The {what} {id} of pull request {pull.Info.Id} reads back as something else.");
    }

    static List<string> ReviewArguments(string repository, string number, string head, ReviewVerdict verdict, string body)
    {
        List<string> arguments =
        [
            "api", $"repos/{repository}/pulls/{number}/reviews", "--method", "POST",
            "-f", $"event={verdict switch { ReviewVerdict.Approve => "APPROVE", ReviewVerdict.RequestChanges => "REQUEST_CHANGES", _ => "COMMENT" }}",
            "-f", $"commit_id={head}",
        ];
        if (!string.IsNullOrWhiteSpace(body))
        {
            arguments.AddRange(["-f", $"body={body}"]);
        }

        return arguments;
    }

    /// <summary>
    /// GitHub's refusal of a review by the pull request's author: "Can not approve your own pull
    /// request", or "Can not request changes on your own pull request".
    /// </summary>
    static bool IsOwnPullRequest(string refusal) => refusal.Contains("your own pull request", StringComparison.OrdinalIgnoreCase);

    static string Capitalized(string what) => char.ToUpperInvariant(what[0]) + what[1..];

    static ReviewVerdict? Verdict(string? state) =>
        state switch
        {
            "APPROVED" => ReviewVerdict.Approve,
            "CHANGES_REQUESTED" => ReviewVerdict.RequestChanges,
            "COMMENTED" => ReviewVerdict.Comment,
            _ => null,
        };

    public Task<byte[]?> ReadAttachmentAsync(Uri address, CancellationToken ct)
    {
        // The credential goes only to GitHub: `gh api` signs a request for the host it names.
        if (address.Scheme != Uri.UriSchemeHttps || !address.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<byte[]?>(null);
        }

        return Bytes(["api", address.AbsoluteUri], ct);
    }

    /// <summary>
    /// A PBI's pull request as one GraphQL answer gives it: what <see cref="PullRequestInfo"/> says, the
    /// change's ends and totals, and what the caller added to the query.
    /// </summary>
    /// <param name="Files">The files without their patches, or null when the query did not ask for them.</param>
    /// <param name="MoreFiles">GitHub stopped listing <paramref name="Files"/> at a page.</param>
    /// <param name="Threads">The review threads, or null when the query did not ask for them.</param>
    /// <param name="Conversation">The title, body, comments and reviews, or null when the query did not ask for them.</param>
    sealed record PullRead(
        PullRequestInfo Info,
        string Head,
        string HeadRef,
        string BaseRef,
        string BaseSha,
        int Commits,
        int Additions,
        int Deletions,
        int ChangedFiles,
        IReadOnlyList<ChangedFile>? Files = null,
        bool MoreFiles = false,
        IReadOnlyList<ReviewThread>? Threads = null,
        PullRequestConversation? Conversation = null
    );

    /// <summary>
    /// The pull request whose "Closes #n" GitHub recorded against the issue, the latest when there are
    /// several, or null. GitHub records it only for a pull request into the default branch.
    /// <para>
    /// One GraphQL call (#621): <c>gh issue view</c> and then <c>gh pr view</c> were two, one after the
    /// other, and every read of the Proposal and Review tabs began with them. <paramref name="also"/>
    /// adds fields of the pull request to the same query, so a caller that needs its files or its
    /// threads does not ask again.
    /// </para>
    /// </summary>
    async Task<PullRead?> PullRequest(PbiRef pbi, CancellationToken ct, string also = "")
    {
        Number(pbi);
        var (owner, name) = Split(pbi.Repository);

        // 25, not the 100 `gh issue view` asks for: GitHub refuses a query that could reach more than
        // 500,000 nodes, and 100 pull requests of 100 threads of 100 comments is a million. No issue
        // of this loop has more than a few pull requests closing it.
        var query =
            "query($owner: String!, $name: String!, $number: Int!) { repository(owner: $owner, name: $name) "
            + "{ issue(number: $number) { closedByPullRequestsReferences(first: 25) { nodes { "
            + PullFields + " " + also + " } } } } }";
        var run = await _gh(
            ["api", "graphql", "-f", $"query={query}", "-f", $"owner={owner}", "-f", $"name={name}", "-F", $"number={pbi.Id}"],
            ct
        );
        if (run.ExitCode != 0 && run.StandardError.Contains("Could not resolve to an", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        using var document = Parse(Answer(run));
        var latest = document
            .RootElement.GetProperty("data")
            .GetProperty("repository")
            .GetProperty("issue")
            .GetProperty("closedByPullRequestsReferences")
            .GetProperty("nodes")
            .EnumerateArray()
            .Where(node =>
                node.TryGetProperty("repository", out var repository)
                && string.Equals(Text(repository, "nameWithOwner"), pbi.Repository, StringComparison.OrdinalIgnoreCase)
            )
            .OrderByDescending(node => node.GetProperty("number").GetInt32())
            .Take(1)
            .ToArray();

        return latest is [var row] ? Pull(row) : null;
    }

    /// <summary>The open pull request from <paramref name="branch"/>, or null when there is none.</summary>
    async Task<PullRequestInfo?> FromBranch(string repository, string branch, CancellationToken ct)
    {
        using var list = Parse(
            await Ask(
                [
                    "pr", "list", "--repo", repository, "--head", branch, "--state", "open",
                    "--json", "number,url,isDraft,state,reviewDecision,headRefOid",
                ],
                ct
            )
        );

        return list.RootElement.EnumerateArray().Select(row => Pull(row).Info).FirstOrDefault();
    }

    /// <summary>A pull request as <c>gh pr list --json</c> prints it, or as a GraphQL node with whatever else the query asked for.</summary>
    static PullRead Pull(JsonElement row)
    {
        var number = row.GetProperty("number").GetInt32().ToString(CultureInfo.InvariantCulture);
        var files = row.TryGetProperty("files", out var listed) && listed.ValueKind == JsonValueKind.Object ? listed : default;
        var info = new PullRequestInfo(
            number,
            new Uri(Text(row, "url") ?? throw new PbiProviderException($"Pull request {number} has no address.")),
            row.TryGetProperty("isDraft", out var draft) && draft.ValueKind == JsonValueKind.True,
            string.Equals(Text(row, "state"), "MERGED", StringComparison.OrdinalIgnoreCase),
            string.Equals(Text(row, "reviewDecision"), "APPROVED", StringComparison.OrdinalIgnoreCase),
            string.Equals(Text(row, "state"), "CLOSED", StringComparison.OrdinalIgnoreCase)
        );

        return new(
            info,
            Text(row, "headRefOid") ?? string.Empty,
            Text(row, "headRefName") ?? string.Empty,
            Text(row, "baseRefName") ?? string.Empty,
            Text(row, "baseRefOid") ?? string.Empty,
            row.TryGetProperty("commits", out var commits) && commits.ValueKind == JsonValueKind.Object ? Number(commits, "totalCount") : 0,
            Number(row, "additions"),
            Number(row, "deletions"),
            Number(row, "changedFiles"),
            files.ValueKind == JsonValueKind.Object ? [.. files.GetProperty("nodes").EnumerateArray().Select(ListedFile)] : null,
            files.ValueKind == JsonValueKind.Object
                && files.TryGetProperty("pageInfo", out var page)
                && page.TryGetProperty("hasNextPage", out var more)
                && more.ValueKind == JsonValueKind.True,
            row.TryGetProperty("reviewThreads", out var threads) && threads.ValueKind == JsonValueKind.Object
                ? [.. threads.GetProperty("nodes").EnumerateArray().Select(Thread)]
                : null,
            row.TryGetProperty("reviews", out var reviews) && reviews.ValueKind == JsonValueKind.Object ? Conversation(info, row) : null
        );
    }

    /// <summary>The conversation of a pull request node that was asked for <see cref="ConversationSelection"/>.</summary>
    static PullRequestConversation Conversation(PullRequestInfo info, JsonElement row) =>
        new(
            Text(row, "title") ?? string.Empty,
            info.IsMerged ? "merged" : (Text(row, "state") ?? "open").ToLowerInvariant(),
            Login(row, "author"),
            Text(row, "body") ?? string.Empty,
            [
                .. Nodes(row, "comments")
                    .Select(comment => new PbiComment(DatabaseId(comment), Login(comment, "author"), When(comment, "createdAt"), Text(comment, "body") ?? string.Empty)
                    {
                        Url = Address(Text(comment, "url")),
                    }),
            ],
            [
                .. Nodes(row, "reviews")
                    .Select(review => new PullRequestReviewNote(
                        DatabaseId(review),
                        Verdict(Text(review, "state")),
                        Login(review, "author"),
                        When(review, "submittedAt"),
                        Text(review, "body") ?? string.Empty
                    )),
            ]
        );

    static IEnumerable<JsonElement> Nodes(JsonElement row, string name) =>
        row.TryGetProperty(name, out var connection) && connection.ValueKind == JsonValueKind.Object
            ? connection.GetProperty("nodes").EnumerateArray()
            : [];

    static string DatabaseId(JsonElement node) => node.GetProperty("databaseId").GetInt64().ToString(CultureInfo.InvariantCulture);

    static DateTimeOffset When(JsonElement node, string name) =>
        node.TryGetProperty(name, out var at) && at.ValueKind == JsonValueKind.String && at.TryGetDateTimeOffset(out var when) ? when : default;

    /// <summary>
    /// The change from the default branch to <paramref name="branch"/>, with every file's patch, or
    /// null when GitHub cannot say it in one answer: no such branch, more commits than one page lists,
    /// or any failure. The compare endpoint lists up to 300 files, which the caller checks against the
    /// pull request's count.
    /// </summary>
    async Task<(string Head, string MergeBase, IReadOnlyList<ChangedFile> Files)?> Compared(string repository, string branch, CancellationToken ct)
    {
        var run = await _gh(["api", $"repos/{repository}/compare/HEAD...{branch}"], ct);
        if (!run.Succeeded)
        {
            return null;
        }

        try
        {
            using var document = Parse(run.StandardOutput);
            var root = document.RootElement;
            var commits = root.TryGetProperty("commits", out var listed) && listed.ValueKind == JsonValueKind.Array ? listed.EnumerateArray().ToArray() : [];
            if (commits.Length == 0 || Number(root, "total_commits") != commits.Length || !root.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return (
                Text(commits[^1], "sha") ?? string.Empty,
                root.TryGetProperty("merge_base_commit", out var mergeBase) && mergeBase.ValueKind == JsonValueKind.Object ? Text(mergeBase, "sha") ?? string.Empty : string.Empty,
                [.. files.EnumerateArray().Select(RestFile)]
            );
        }
        catch (PbiProviderException)
        {
            return null;
        }
    }

    /// <summary>The checks on the tip of <paramref name="branch"/> with the commit they are of, or null when they cannot be read.</summary>
    async Task<(IReadOnlyList<PullRequestCheck> Checks, string Of)?> BranchChecks(string repository, string branch, CancellationToken ct)
    {
        try
        {
            return await Checks(repository, branch, ct);
        }
        catch (PbiProviderException)
        {
            return null;
        }
    }

    async Task<(IReadOnlyList<PullRequestCheck> Checks, string? Unread)> ChecksOrWhy(string repository, string head, CancellationToken ct)
    {
        if (head.Length == 0)
        {
            return ([], null);
        }

        try
        {
            return ((await Checks(repository, head, ct)).Checks, null);
        }
        catch (PbiProviderException failure)
        {
            return ([], failure.Message);
        }
    }

    /// <summary>
    /// The check runs and the commit statuses of a commit, a SHA or a branch, asked at once, and the
    /// commit GitHub read them of: the combined status names it, and every run must agree, or it is
    /// empty.
    /// </summary>
    async Task<(IReadOnlyList<PullRequestCheck> Checks, string Of)> Checks(string repository, string reference, CancellationToken ct)
    {
        var running = Ask(["api", $"repos/{repository}/commits/{reference}/check-runs?per_page=100", "--jq", ".check_runs[]"], ct);
        var stating = Ask(["api", $"repos/{repository}/commits/{reference}/status"], ct);
        await Task.WhenAll(running, stating);
        var runs = Lines(await running);
        using var status = Parse(await stating);
        var of = Text(status.RootElement, "sha") ?? string.Empty;
        var statuses = status.RootElement.TryGetProperty("statuses", out var listed) && listed.ValueKind == JsonValueKind.Array
            ? listed.EnumerateArray().ToArray()
            : [];

        IReadOnlyList<PullRequestCheck> checks =
        [
            .. runs.Select(line =>
            {
                using var document = Parse(line);
                var row = document.RootElement;
                if (Text(row, "head_sha") is { } ran && ran != of)
                {
                    of = string.Empty;
                }

                var state = Text(row, "status") != "completed"
                    ? CheckState.Pending
                    : Text(row, "conclusion") switch
                    {
                        "success" => CheckState.Passing,
                        "neutral" or "skipped" or "cancelled" or "stale" => CheckState.Neutral,
                        _ => CheckState.Failing,
                    };
                return new PullRequestCheck(Text(row, "name") ?? string.Empty, state, Address(Text(row, "html_url")));
            }),
            .. statuses.Select(row =>
            {
                var state = Text(row, "state") switch
                {
                    "success" => CheckState.Passing,
                    "pending" => CheckState.Pending,
                    _ => CheckState.Failing,
                };
                return new PullRequestCheck(Text(row, "context") ?? string.Empty, state, Address(Text(row, "target_url")));
            }),
        ];

        return (checks, of);
    }

    /// <summary>Every file of a pull request with its patch, from REST across every page.</summary>
    async Task<IReadOnlyList<ChangedFile>> ListFiles(string repository, string number, CancellationToken ct)
    {
        var printed = await Ask(["api", $"repos/{repository}/pulls/{number}/files?per_page=100", "--paginate", "--jq", ".[]"], ct);
        return
        [
            .. Lines(printed).Select(line =>
            {
                using var file = Parse(line);
                return RestFile(file.RootElement);
            }),
        ];
    }

    /// <summary>A file as REST lists it, for a pull request and for a compare alike.</summary>
    static ChangedFile RestFile(JsonElement row) =>
        new(
            Text(row, "filename") ?? string.Empty,
            Text(row, "previous_filename"),
            Text(row, "status") ?? "modified",
            Number(row, "additions"),
            Number(row, "deletions"),
            Text(row, "patch")
        );

    static PullRequestChange Change(PullRead pull, IReadOnlyList<ChangedFile> files) =>
        new(pull.Info, pull.BaseRef, pull.BaseSha, pull.HeadRef, pull.Head, pull.Commits, pull.Additions, pull.Deletions, files, pull.ChangedFiles, pull.Threads ?? []);

    /// <summary>A file as GraphQL lists it, its <c>changeType</c> worded as REST's <c>status</c>.</summary>
    static ChangedFile ListedFile(JsonElement node) =>
        new(
            Text(node, "path") ?? string.Empty,
            null,
            Text(node, "changeType") switch
            {
                "DELETED" => "removed",
                { } type => type.ToLowerInvariant(),
                null => "modified",
            },
            Number(node, "additions"),
            Number(node, "deletions"),
            null
        );

    /// <summary>A thread by its global id, with the pull request GitHub says it is on, or null when there is none.</summary>
    async Task<(ReviewThread Thread, string Repository, string Number)?> ThreadOf(string threadId, CancellationToken ct)
    {
        using var document = Parse(await Ask(["api", "graphql", "-f", $"query={ThreadQuery}", "-f", $"id={threadId}"], ct));
        if (
            !document.RootElement.GetProperty("data").TryGetProperty("node", out var node)
            || node.ValueKind != JsonValueKind.Object
            || !node.TryGetProperty("pullRequest", out var pull)
        )
        {
            return null;
        }

        return (
            Thread(node),
            Text(pull.GetProperty("repository"), "nameWithOwner") ?? string.Empty,
            pull.GetProperty("number").GetInt32().ToString(CultureInfo.InvariantCulture)
        );
    }

    static ReviewThread Thread(JsonElement node)
    {
        var path = Text(node, "path") ?? string.Empty;
        int? line = node.TryGetProperty("line", out var at) && at.ValueKind == JsonValueKind.Number ? at.GetInt32() : null;

        return new ReviewThread(
            Text(node, "id") ?? string.Empty,
            path,
            line,
            node.TryGetProperty("isResolved", out var resolved) && resolved.ValueKind == JsonValueKind.True,
            node.TryGetProperty("isOutdated", out var outdated) && outdated.ValueKind == JsonValueKind.True,
            [
                .. node.GetProperty("comments")
                    .GetProperty("nodes")
                    .EnumerateArray()
                    .Select(comment => new ReviewComment(DatabaseId(comment), path, line, Login(comment, "author"), When(comment, "createdAt"), Text(comment, "body") ?? string.Empty)),
            ]
        );
    }

    static ReviewComment Review(JsonElement row) =>
        new(
            Id(row),
            Text(row, "path") ?? string.Empty,
            row.TryGetProperty("line", out var line) && line.ValueKind == JsonValueKind.Number ? line.GetInt32() : null,
            Author(row),
            Created(row),
            Text(row, "body") ?? string.Empty
        );

    /// <summary>
    /// Why a write was refused: GitHub's own <c>errors</c>, which <c>gh api</c> prints as the body on
    /// standard output, ahead of its generic first line. "Can not approve your own pull request" is
    /// the sentence a person can act on; "Unprocessable Entity (HTTP 422)" is not.
    /// </summary>
    static string Why(ProcessOutcome run)
    {
        try
        {
            using var document = JsonDocument.Parse(run.StandardOutput);
            if (document.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            {
                var said = errors
                    .EnumerateArray()
                    .Select(error => error.ValueKind == JsonValueKind.String ? error.GetString() : Text(error, "message"))
                    .OfType<string>()
                    .ToArray();
                if (said.Length > 0)
                {
                    return string.Join(" ", said);
                }
            }
        }
        catch (JsonException)
        {
            // Not GitHub's body; gh's own first line says what there is to say.
        }

        return Said(run);
    }

    static Uri? Address(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var address) ? address : null;
}
