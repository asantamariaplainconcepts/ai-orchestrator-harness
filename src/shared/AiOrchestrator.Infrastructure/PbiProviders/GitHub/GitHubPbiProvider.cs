using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.Infrastructure.PbiProviders.GitHub;

/// <summary>
/// <see cref="IPbiProvider"/> over GitHub, asked through <c>gh</c> with the machine's own credential.
/// This half is the PBIs: an issue, its page, its children, its labels and its comments. The other
/// half, <c>GitHubPbiProvider.PullRequests.cs</c>, is the pull request and its review.
/// <para>
/// <b>A state is a label.</b> The <c>loop.yaml</c> state <c>verifying</c> is the label
/// <c>status:verifying</c>, and the hold is <c>status:holding</c>. Both names live in this file and nowhere above it, so Azure DevOps can map the same
/// states to its state field.
/// </para>
/// <para>
/// <b>Every write is read back.</b> On 7 October 2026 two tags landed on the wrong commit and nobody
/// saw it, because the write was taken as its own proof. Here a write is followed by a read of the
/// same thing, and the answer says whether the read matched. Nothing retries: a mismatch is the
/// caller's to stop on.
/// </para>
/// <para>
/// The move is transplanted from the first tree's <c>.harness/aio/status</c>: one edit that removes every other
/// <c>status:*</c> label and adds the target, so the PBI never carries two states, with the target
/// label created first because an edit naming an unknown label fails.
/// </para>
/// </summary>
sealed partial class GitHubPbiProvider : IPbiProvider
{
    internal const string StatePrefix = "status:";
    internal const string HoldLabel = "status:holding";

    /// <summary>The colour a state label is created with, the one the first tree's <c>status</c> script used.</summary>
    const string LabelColour = "5319e7";

    /// <summary>More labels than one repository has.</summary>
    const string LabelLimit = "1000";

    const string IssueFields = "number,title,url,body,author,createdAt,labels,assignees,parent,blockedBy,comments,state";

    readonly Gh _gh;

    public GitHubPbiProvider()
        : this(GhCli.Run) { }

    internal GitHubPbiProvider(Gh gh) => _gh = gh;

    /// <summary>Where the CLI is probed; tests answer in its place, because the probe runs the real executable.</summary>
    internal Func<CancellationToken, Task<CliProbe>> Probing { get; init; } =
        ct => CliProbing.Probe("gh", LoginShellEnvironment.Resolve("gh"), ct);

    public string Name => "github";

    public async Task<CliProbe> ProbeAsync(CancellationToken ct)
    {
        var probe = await Probing(ct);
        if (probe.Path is null)
        {
            return probe;
        }

        try
        {
            return probe with { Account = await Identity(ct) };
        }
        catch (PbiProviderException failure)
        {
            return probe with { Problem = failure.Message };
        }
    }

    public async Task<IReadOnlyList<string>> ListStatesAsync(string repository, CancellationToken ct)
    {
        Repository(repository);
        return States(await Labels(repository, ct));
    }

    public async Task<Written<IReadOnlyList<string>>> CreateStatesAsync(
        string repository,
        IReadOnlyList<string> states,
        CancellationToken ct
    )
    {
        Repository(repository);
        if (states.FirstOrDefault(state => string.IsNullOrWhiteSpace(state) || Label(state).Equals(HoldLabel, StringComparison.OrdinalIgnoreCase)) is { } wrong)
        {
            throw new ArgumentException($"'{wrong}' is not a state a repository can have.", nameof(states));
        }

        var wanted = states.Select(Label).Append(HoldLabel).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var had = await Labels(repository, ct);

        // Only the ones it lacks: a create of a label that exists fails, and would hide a real refusal.
        var refusals = new List<string>();
        foreach (var label in wanted.Where(label => !had.Contains(label)))
        {
            if (Refusal(await _gh(["label", "create", label, "--repo", repository, "--color", LabelColour], ct)) is { } refused)
            {
                refusals.Add($"{label}: {refused}");
            }
        }

        HashSet<string> after;
        try
        {
            after = await Labels(repository, ct);
        }
        catch (PbiProviderException failure)
        {
            return new(null, $"The states of {repository} were created but could not be read back: {failure.Message}");
        }

        var missing = wanted.Where(label => !after.Contains(label)).ToList();
        return missing.Count == 0
            ? new(States(after), null)
            : new(
                States(after),
                $"{repository} should have {string.Join(", ", missing)} after the create, and does not"
                    + (refusals.Count == 0 ? "." : $" (gh said: {string.Join("; ", refusals)}).")
            );
    }

    public async Task<IReadOnlyList<Pbi>> ListAsync(string repository, PbiQuery query, CancellationToken ct)
    {
        Repository(repository);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.Limit);

        // Without a sort, `gh issue list` answers the most recently created first. Closing an issue
        // updates it, and GitHub's search has no sort by closing, so the latest updated is the
        // nearest it says to the latest done.
        List<string> arguments =
        [
            "issue", "list", "--repo", repository, "--state", query.State.ToString().ToLowerInvariant(),
            "--limit", query.Limit.ToString(CultureInfo.InvariantCulture),
        ];
        if (query.Text is not null)
        {
            arguments.AddRange(["--search", query.Text]);
        }
        else if (query.State == PbiSearchState.Closed)
        {
            arguments.AddRange(["--search", "sort:updated-desc"]);
        }
        arguments.AddRange(["--json", "number,title,labels,assignees,state"]);

        using var document = Parse(await Ask(arguments, ct));
        return [.. document.RootElement.EnumerateArray().Select(row => Read(repository, row).Pbi)];
    }

    public async Task<PbiThread?> ReadThreadAsync(PbiRef pbi, CancellationToken ct)
    {
        Number(pbi);

        // `issue view` reads the parent and the blocking issues, which the REST issue does not carry,
        // and every comment in one call.
        var run = await _gh(["issue", "view", pbi.Id, "--repo", pbi.Repository, "--json", IssueFields], ct);
        if (run.ExitCode != 0 && run.StandardError.Contains("Could not resolve to an", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        using var document = Parse(Answer(run));
        var row = document.RootElement;
        var url = Text(row, "url") ?? $"https://github.com/{pbi.Repository}/issues/{pbi.Id}";
        var issue = Read(pbi.Repository, row);
        return new PbiThread(
            issue.Pbi,
            new Uri(url),
            Login(row, "author"),
            row.TryGetProperty("createdAt", out var at) && at.TryGetDateTimeOffset(out var created) ? created : default,
            Text(row, "body") ?? string.Empty,
            row.TryGetProperty("labels", out var labels) && labels.ValueKind == JsonValueKind.Array
                ? [.. labels.EnumerateArray().Select(label => Text(label, "name")).OfType<string>()]
                : [],
            issue.Assignees,
            row.TryGetProperty("parent", out var parent) && parent.ValueKind == JsonValueKind.Object ? Linked(parent) : null,
            row.TryGetProperty("blockedBy", out var blocked) && blocked.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array
                ? [.. nodes.EnumerateArray().Select(Linked).OfType<LinkedPbi>()]
                : [],
            row.TryGetProperty("comments", out var comments) && comments.ValueKind == JsonValueKind.Array
                ? [.. comments.EnumerateArray().Select(ThreadComment).OrderBy(comment => comment.CreatedAt)]
                : []
        )
        {
            // `issue view` answers for a pull request too, at its /pull/ address.
            IsPullRequest = url.Contains("/pull/", StringComparison.Ordinal),
        };
    }

    public async Task<IReadOnlyList<Pbi>> ListChildrenAsync(PbiRef pbi, CancellationToken ct)
    {
        Number(pbi);

        // The REST sub-issues endpoint, because `issue view --json` has no field for them. GitHub caps
        // an issue at 100 sub-issues, so one page of 100 is all of them.
        var run = await _gh(["api", $"repos/{pbi.Repository}/issues/{pbi.Id}/sub_issues?per_page=100"], ct);
        if (NotFound(run))
        {
            return [];
        }

        using var document = Parse(Answer(run));
        return
        [
            .. document.RootElement.EnumerateArray()
                .Select(row => Read(RepositoryOf(row) ?? pbi.Repository, row).Pbi),
        ];
    }

    public async Task<Written<Pbi>> CreateAsync(string repository, string title, string body, string? parent, CancellationToken ct)
    {
        Repository(repository);
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("An issue needs a title.", nameof(title));
        }

        if (parent is not null)
        {
            Number(new PbiRef(repository, parent));
        }

        // The REST call rather than `gh issue create`, because its answer carries the numeric id the
        // sub-issues endpoint asks for, where `gh issue create` prints only the address.
        var created = await _gh(
            ["api", $"repos/{repository}/issues", "--method", "POST", "-f", $"title={title}", "-f", $"body={body}"],
            ct
        );
        if (!created.Succeeded)
        {
            return new(null, $"The issue '{title}' was not created in {repository}: {Said(created)}");
        }

        string number;
        long id;
        using (var document = Parse(created.StandardOutput))
        {
            number = document.RootElement.GetProperty("number").GetInt32().ToString(CultureInfo.InvariantCulture);
            id = document.RootElement.GetProperty("id").GetInt64();
        }

        var pbi = new PbiRef(repository, number);
        string? unlinked = null;
        if (parent is not null)
        {
            unlinked = Refusal(
                await _gh(
                    [
                        "api", $"repos/{repository}/issues/{parent}/sub_issues", "--method", "POST",
                        "-F", $"sub_issue_id={id.ToString(CultureInfo.InvariantCulture)}",
                    ],
                    ct
                )
            );
        }

        IssueRead? after;
        IReadOnlyList<Pbi> children = [];
        try
        {
            after = await Issue(pbi, ct);
            if (parent is not null)
            {
                children = await ListChildrenAsync(new PbiRef(repository, parent), ct);
            }
        }
        catch (PbiProviderException failure)
        {
            return new(null, $"{Named(pbi)} was created but could not be read back: {failure.Message}");
        }

        if (after is null)
        {
            return new(null, $"{Named(pbi)} was created and then could not be found.");
        }

        if (parent is not null && !children.Any(child => child.Ref.Id == number && string.Equals(child.Ref.Repository, repository, StringComparison.OrdinalIgnoreCase)))
        {
            return new(after.Pbi, $"{Named(pbi)} was created but is not among the children of #{parent}{Because(unlinked)}.");
        }

        return new(after.Pbi, null);
    }

    public async Task<Written<string>> EditBodyAsync(PbiRef pbi, string body, CancellationToken ct)
    {
        Number(pbi);

        var edited = await _gh(
            ["api", $"repos/{pbi.Repository}/issues/{pbi.Id}", "--method", "PATCH", "-f", $"body={body}"],
            ct
        );
        if (!edited.Succeeded)
        {
            return new(null, $"The body of {Named(pbi)} was not edited: {Said(edited)}");
        }

        string observed;
        try
        {
            using var document = Parse(await Ask(["api", $"repos/{pbi.Repository}/issues/{pbi.Id}"], ct));
            observed = Text(document.RootElement, "body") ?? string.Empty;
        }
        catch (PbiProviderException failure)
        {
            return new(null, $"The body of {Named(pbi)} was edited but could not be read back: {failure.Message}");
        }

        return Same(observed, body)
            ? new(observed, null)
            : new(observed, $"The body of {Named(pbi)} reads back different from what was written.");
    }

    /// <summary>
    /// One <c>issue edit</c> for the labels and the assignee, then <c>issue close</c> when asked, and
    /// one read of the issue to check all of it. A label the repository lacks is created first,
    /// because an edit naming an unknown label fails.
    /// </summary>
    public async Task<Written<Pbi>> MoveAsync(PbiRef pbi, PbiMove move, CancellationToken ct)
    {
        if (move.To is { } to && (string.IsNullOrWhiteSpace(to) || Label(to).Equals(HoldLabel, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"'{to}' is not a state a PBI can be moved to.", nameof(move));
        }

        if (move is { To: null, Held: null, AssignToSelf: false, Close: false })
        {
            throw new ArgumentException("A move has to change something.", nameof(move));
        }

        var before = await Issue(pbi, ct);
        if (before is null)
        {
            return new(null, $"{Named(pbi)} does not exist, so it was not {Worded(move)}.");
        }

        var me = move.AssignToSelf ? await Identity(ct) : null;
        var target = move.To is null ? null : Label(move.To);
        List<string> edit = ["issue", "edit", pbi.Id, "--repo", pbi.Repository];
        if (target is not null)
        {
            foreach (var label in before.States.Where(label => !label.Equals(target, StringComparison.OrdinalIgnoreCase)))
            {
                edit.AddRange(["--remove-label", label]);
            }
            edit.AddRange(["--add-label", target]);
        }
        if (move.Held is { } held && held != before.Pbi.Held)
        {
            edit.AddRange([held ? "--add-label" : "--remove-label", HoldLabel]);
        }
        if (me is not null && !before.Assignees.Contains(me, StringComparer.OrdinalIgnoreCase))
        {
            edit.AddRange(["--add-assignee", me]);
        }

        var editing = edit.Count > 5;
        var closing = move.Close && !before.Pbi.Closed;
        if (!editing && !closing)
        {
            return new(before.Pbi, null);
        }

        // Fails when the label exists, which is the usual case and not worth a question first.
        foreach (var label in new[] { target, move.Held == true ? HoldLabel : null }.OfType<string>())
        {
            await _gh(["label", "create", label, "--repo", pbi.Repository, "--color", LabelColour], ct);
        }

        var refusals = new List<string>();
        if (editing && Refusal(await _gh(edit, ct)) is { } refusedEdit)
        {
            refusals.Add(refusedEdit);
        }
        if (closing && Refusal(await _gh(["issue", "close", pbi.Id, "--repo", pbi.Repository], ct)) is { } refusedClose)
        {
            refusals.Add(refusedClose);
        }

        IssueRead? after;
        try
        {
            after = await Issue(pbi, ct);
        }
        catch (PbiProviderException failure)
        {
            return new(null, $"{Named(pbi)} was edited but could not be read back: {failure.Message}");
        }

        if (after is null)
        {
            return new(null, $"{Named(pbi)} was edited and then could not be found.");
        }

        var matches =
            (target is null || (after.States.Count == 1 && after.Pbi.State == move.To))
            && after.Pbi.Held == (move.Held ?? before.Pbi.Held)
            && (me is null || after.Assignees.Contains(me, StringComparer.OrdinalIgnoreCase))
            && (!move.Close || after.Pbi.Closed);
        if (matches)
        {
            return new(after.Pbi, null);
        }

        var wanted = Describe(move.To ?? before.Pbi.State, move.Held ?? before.Pbi.Held)
            + (me is null ? "" : $", assigned to {me}")
            + (move.Close ? ", closed" : "");
        var observed = Describe(after)
            + (me is null ? "" : $", assigned to {(after.Assignees.Count == 0 ? "nobody" : string.Join(" and ", after.Assignees))}")
            + (move.Close ? (after.Pbi.Closed ? ", closed" : ", open") : "");
        return new(after.Pbi, $"{Named(pbi)} should be {wanted}, but reads {observed} after the edit{Because(refusals)}.");
    }

    /// <summary>A move in words, for the PBI that was not there to move: "moved to verifying and held".</summary>
    static string Worded(PbiMove move)
    {
        List<string> parts = [];
        if (move.To is { } to)
        {
            parts.Add($"moved to {to}");
        }
        if (move.Held is { } held)
        {
            parts.Add(held ? "held" : "released");
        }
        if (move.AssignToSelf)
        {
            parts.Add("assigned");
        }
        if (move.Close)
        {
            parts.Add("closed");
        }

        return string.Join(" and ", parts);
    }

    public async Task<Written<PbiComment>> CommentAsync(
        PbiRef pbi,
        string body,
        IReadOnlyList<string> attachments,
        CancellationToken ct
    )
    {
        Number(pbi);
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("A comment needs a body.", nameof(body));
        }

        if (attachments.Count == 0)
        {
            var posted = await _gh(
                [
                    "api", $"repos/{pbi.Repository}/issues/{pbi.Id}/comments", "--method", "POST",
                    "-f", $"body={body}",
                ],
                ct
            );
            if (!posted.Succeeded)
            {
                return new(null, $"The comment on {Named(pbi)} was not written: {Said(posted)}");
            }

            return await CommentReadBack(pbi, Id(posted.StandardOutput), body, 0, ct);
        }

        if (attachments.FirstOrDefault(path => !File.Exists(path)) is { } missing)
        {
            throw new ArgumentException($"There is no file at {missing} to attach.", nameof(attachments));
        }

        // `gh issue comment --attach` (2.99.0) is the only way to put an image into a comment: the
        // upload endpoint behind it is not in the REST API. It links each file the body does not
        // name at the end of the comment.
        var directory = Directory.CreateTempSubdirectory("aio-comment-");
        try
        {
            var bodyFile = Path.Combine(directory.FullName, "body.md");
            await File.WriteAllTextAsync(bodyFile, body, ct);

            List<string> arguments = ["issue", "comment", pbi.Id, "--repo", pbi.Repository, "--body-file", bodyFile];
            foreach (var path in attachments)
            {
                arguments.AddRange(["--attach", path]);
            }

            var posted = await _gh(arguments, ct);
            if (!posted.Succeeded)
            {
                return new(null, $"The comment on {Named(pbi)} was not written: {Said(posted)}");
            }

            if (CommentAddress().Match(posted.StandardOutput) is not { Success: true } address)
            {
                return new(null, $"The comment on {Named(pbi)} was sent, and gh named no comment to read back: {Said(posted)}");
            }

            return await CommentReadBack(pbi, address.Groups["id"].Value, body, attachments.Count, ct);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// #696. One PATCH of the comment by its REST id, the number <see cref="ReadThreadAsync"/> reads
    /// from the comment's address, then the same read-back a new comment gets.
    /// </summary>
    public async Task<Written<PbiComment>> EditCommentAsync(PbiRef pbi, string commentId, string body, CancellationToken ct)
    {
        Number(pbi);
        if (!long.TryParse(commentId, NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            throw new ArgumentException($"'{commentId}' is not a GitHub comment id.", nameof(commentId));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("A comment needs a body.", nameof(body));
        }

        var edited = await _gh(
            ["api", $"repos/{pbi.Repository}/issues/comments/{commentId}", "--method", "PATCH", "-f", $"body={body}"],
            ct
        );
        if (!edited.Succeeded)
        {
            return new(null, $"The comment {commentId} on {Named(pbi)} was not edited: {Said(edited)}");
        }

        return await CommentReadBack(pbi, commentId, body, 0, ct);
    }

    /// <summary>
    /// The comment by id, matched against what was sent: the same text for a plain comment, and the
    /// text written followed by every image linked for one with attachments.
    /// </summary>
    async Task<Written<PbiComment>> CommentReadBack(PbiRef pbi, string id, string body, int attached, CancellationToken ct)
    {
        PbiComment observed;
        try
        {
            using var document = Parse(await Ask(["api", $"repos/{pbi.Repository}/issues/comments/{id}"], ct));
            observed = Comment(document.RootElement);
        }
        catch (PbiProviderException failure)
        {
            return new(null, $"The comment {id} on {Named(pbi)} was written but could not be read back: {failure.Message}");
        }

        if (attached == 0)
        {
            return Same(observed.Body, body)
                ? new(observed, null)
                : new(observed, $"The comment {id} on {Named(pbi)} reads back different from what was written.");
        }

        var linked = Regex.Count(observed.Body, "user-attachments/assets/", RegexOptions.None, TimeSpan.FromSeconds(1));
        return Begins(observed.Body, body) && linked >= attached
            ? new(observed, null)
            : new(
                observed,
                $"The comment {id} on {Named(pbi)} reads back with {linked} of {attached} images"
                    + $"{(Begins(observed.Body, body) ? "" : ", and its text differs from what was written")}."
            );
    }

    /// <summary>
    /// One file by itself from REST, which answers the bytes whatever the size; several in one
    /// GraphQL call (#621), each an alias of <c>object(expression: "ref:path")</c>, where a call per
    /// file was the Proposal tab's second. <c>HEAD</c> is the default branch. GraphQL gives a blob as
    /// text, so one it calls binary or cut short is read again from REST.
    /// </summary>
    public async Task<IReadOnlyList<byte[]?>> ReadFilesAsync(string repository, IReadOnlyList<RepositoryFile> files, CancellationToken ct)
    {
        Repository(repository);
        foreach (var file in files)
        {
            InsideRepository(file.Path);
        }

        if (files.Count == 0)
        {
            return [];
        }

        if (files is [var only])
        {
            return [await ReadFile(repository, only, ct)];
        }

        // The expressions go as variables, so no path or branch name is ever spliced into the query.
        var (owner, name) = Split(repository);
        var aliases = files.Select((_, index) => $"f{index}: object(expression: $e{index}) {{ ... on Blob {{ text isBinary isTruncated }} }}");
        var variables = files.Select((_, index) => $", $e{index}: String!");
        List<string> arguments =
        [
            "api", "graphql",
            "-f", $"query=query($owner: String!, $name: String!{string.Concat(variables)}) {{ repository(owner: $owner, name: $name) {{ {string.Join(' ', aliases)} }} }}",
            "-f", $"owner={owner}", "-f", $"name={name}",
        ];
        for (var index = 0; index < files.Count; index++)
        {
            arguments.AddRange(["-f", $"e{index}={files[index].Reference ?? "HEAD"}:{files[index].Path}"]);
        }

        using var document = Parse(await Ask(arguments, ct));
        var found = document.RootElement.GetProperty("data").GetProperty("repository");

        return await Task.WhenAll(files.Select((file, index) =>
        {
            if (!found.TryGetProperty($"f{index}", out var blob) || blob.ValueKind != JsonValueKind.Object)
            {
                return Task.FromResult<byte[]?>(null);
            }

            var text = Text(blob, "text");
            var whole = text is not null && !True(blob, "isBinary") && !True(blob, "isTruncated");
            return whole ? Task.FromResult<byte[]?>(Encoding.UTF8.GetBytes(text!)) : ReadFile(repository, file, ct);
        }));

        static bool True(JsonElement blob, string flag) => blob.TryGetProperty(flag, out var value) && value.ValueKind == JsonValueKind.True;
    }

    Task<byte[]?> ReadFile(string repository, RepositoryFile file, CancellationToken ct)
    {
        var escaped = string.Join('/', file.Path.Split('/').Select(Uri.EscapeDataString));
        var at = file.Reference is null ? "" : $"?ref={Uri.EscapeDataString(file.Reference)}";

        // The raw media type answers the file's bytes whatever its size, where the JSON answer stops
        // carrying the content above a megabyte.
        return Bytes(["api", $"repos/{repository}/contents/{escaped}{at}", "-H", "Accept: application/vnd.github.raw"], ct);
    }

    /// <summary>
    /// A PBI with every label and assignee it carries: a move has to see the labels it removes, and an
    /// assignment the people already on it.
    /// </summary>
    sealed record IssueRead(Pbi Pbi, IReadOnlyList<string> States, IReadOnlyList<string> Assignees);

    async Task<IssueRead?> Issue(PbiRef pbi, CancellationToken ct)
    {
        Number(pbi);

        var run = await _gh(["api", $"repos/{pbi.Repository}/issues/{pbi.Id}"], ct);
        if (NotFound(run))
        {
            return null;
        }

        using var document = Parse(Answer(run));

        // The issues endpoint answers for pull requests too; a pull request is never a PBI.
        return document.RootElement.TryGetProperty("pull_request", out _) ? null : Read(pbi.Repository, document.RootElement);
    }

    async Task<string> Identity(CancellationToken ct)
    {
        var login = (await Ask(["api", "user", "--jq", ".login"], ct)).Trim();
        return login.Length > 0 ? login : throw new PbiProviderException("The GitHub CLI named no account for its credential.");
    }

    /// <summary>
    /// Every <c>status:*</c> label of the repository, the hold among them. Listed whole and not with
    /// <c>--search</c>: the search answers from an index that lags a create, and on 9 October 2026 the
    /// read-back a second after creating <c>status:holding</c> did not list it (#566).
    /// </summary>
    async Task<HashSet<string>> Labels(string repository, CancellationToken ct)
    {
        var printed = await Ask(["label", "list", "--repo", repository, "--limit", LabelLimit, "--json", "name"], ct);

        // An empty answer prints nothing at all, not []: measured on 9 October 2026 with a search that
        // found no label, which the install read as a refusal (#566).
        if (string.IsNullOrWhiteSpace(printed))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        using var document = Parse(printed);
        return document
            .RootElement.EnumerateArray()
            .Select(row => Text(row, "name") ?? "")
            .Where(name => name.StartsWith(StatePrefix, StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The states among a repository's labels, without the prefix and without the hold, by name.</summary>
    static IReadOnlyList<string> States(HashSet<string> labels) =>
        [
            .. labels
                .Where(name => !name.Equals(HoldLabel, StringComparison.OrdinalIgnoreCase))
                .Select(name => name[StatePrefix.Length..])
                .Order(StringComparer.Ordinal),
        ];

    static IssueRead Read(string repository, JsonElement row)
    {
        var labels = row.TryGetProperty("labels", out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray().Select(label => Text(label, "name")).OfType<string>().ToArray()
            : [];
        var states = labels
            .Where(label => label.StartsWith(StatePrefix, StringComparison.OrdinalIgnoreCase))
            .Where(label => !label.Equals(HoldLabel, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var people = row.TryGetProperty("assignees", out var assignees) && assignees.ValueKind == JsonValueKind.Array
            ? assignees.EnumerateArray().Select(person => Text(person, "login")).OfType<string>().ToArray()
            : [];

        return new(
            new Pbi(
                new PbiRef(repository, row.GetProperty("number").GetInt32().ToString(CultureInfo.InvariantCulture)),
                Text(row, "title") ?? string.Empty,
                states.FirstOrDefault()?[StatePrefix.Length..],
                labels.Any(label => label.Equals(HoldLabel, StringComparison.OrdinalIgnoreCase)),
                people.FirstOrDefault(),
                // `closed` from the REST API, `CLOSED` from `issue list`.
                string.Equals(Text(row, "state"), "closed", StringComparison.OrdinalIgnoreCase)
            ),
            states,
            people
        );
    }

    /// <summary>The <c>owner/name</c> a REST issue belongs to, which for a sub-issue may be another repository.</summary>
    static string? RepositoryOf(JsonElement row)
    {
        // https://api.github.com/repos/owner/name
        var url = Text(row, "repository_url");
        var at = url?.IndexOf("/repos/", StringComparison.Ordinal) ?? -1;
        return at < 0 ? null : url![(at + "/repos/".Length)..];
    }

    static LinkedPbi? Linked(JsonElement row)
    {
        if (Text(row, "url") is not { } url || !Uri.TryCreate(url, UriKind.Absolute, out var address))
        {
            return null;
        }

        // https://github.com/owner/name/issues/7
        var segments = address.AbsolutePath.Trim('/').Split('/');
        var repository = segments.Length >= 2 ? $"{segments[0]}/{segments[1]}" : string.Empty;
        var number = row.TryGetProperty("number", out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32().ToString(CultureInfo.InvariantCulture)
            : segments[^1];

        return new LinkedPbi(
            new PbiRef(repository, number),
            Text(row, "title") ?? string.Empty,
            address,
            string.Equals(Text(row, "state"), "CLOSED", StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>A comment as <c>issue view</c> prints it, with the numeric id its address carries.</summary>
    static PbiComment ThreadComment(JsonElement row)
    {
        var url = Text(row, "url");
        var id = url is null ? null : CommentAddress().Match(url) is { Success: true } found ? found.Groups["id"].Value : null;

        return new PbiComment(
            id ?? Text(row, "id") ?? string.Empty,
            Login(row, "author"),
            row.TryGetProperty("createdAt", out var at) && at.TryGetDateTimeOffset(out var created) ? created : default,
            Text(row, "body") ?? string.Empty
        )
        {
            Url = url is null ? null : new Uri(url),
        };
    }

    static PbiComment Comment(JsonElement row) =>
        new(Id(row), Author(row), Created(row), Text(row, "body") ?? string.Empty)
        {
            Url = Text(row, "html_url") is { } url ? new Uri(url) : null,
        };

    static string? Author(JsonElement row) =>
        row.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object ? Text(user, "login") : null;

    static string? Login(JsonElement row, string name) =>
        row.TryGetProperty(name, out var person) && person.ValueKind == JsonValueKind.Object ? Text(person, "login") : null;

    static DateTimeOffset Created(JsonElement row) =>
        row.TryGetProperty("created_at", out var at) && at.TryGetDateTimeOffset(out var created) ? created : default;

    static string Id(string printed)
    {
        using var document = Parse(printed);
        return Id(document.RootElement);
    }

    static string Id(JsonElement row) =>
        row.GetProperty("id").GetInt64().ToString(CultureInfo.InvariantCulture);

    /// <summary>Written, once both are <see cref="Saved"/>.</summary>
    static bool Same(string observed, string meant) => Saved(observed) == Saved(meant);

    /// <summary>The comment with images reads back as the text written and the images linked after it.</summary>
    static bool Begins(string observed, string meant) => Saved(observed).StartsWith(Saved(meant), StringComparison.Ordinal);

    /// <summary>
    /// The text without what GitHub may change when it saves it (#710): <c>\r\n</c> and <c>\r</c> become
    /// <c>\n</c>, the spaces at the end of each line go, and so do the blank lines at either end.
    /// </summary>
    static string Saved(string text) =>
        string.Join('\n', text.ReplaceLineEndings("\n").Split('\n').Select(line => line.TrimEnd())).Trim();

    static string Label(string state) => StatePrefix + state;

    static string Named(PbiRef pbi) => $"{pbi.Repository}#{pbi.Id}";

    static string Describe(IssueRead read) =>
        read.States.Count switch
        {
            0 => Describe(null, read.Pbi.Held),
            1 => Describe(read.Pbi.State, read.Pbi.Held),
            _ => $"{string.Join(" and ", read.States)}{(read.Pbi.Held ? ", held" : "")}",
        };

    static string Describe(string? state, bool held) =>
        (state ?? "no state") + (held ? ", held" : ", not held");

    static string Because(string? refused) => refused is null ? "" : $" (gh said: {refused})";

    static string Because(List<string> refusals) => refusals.Count == 0 ? "" : $" (gh said: {string.Join("; ", refusals)})";

    static string? Refusal(ProcessOutcome run) => run.Succeeded ? null : Said(run);

    static bool NotFound(ProcessOutcome run) =>
        run.ExitCode != 0 && !run.Missing && !run.TimedOut && run.StandardError.Contains("HTTP 404", StringComparison.Ordinal);

    async Task<string> Ask(IReadOnlyList<string> arguments, CancellationToken ct) => Answer(await _gh(arguments, ct));

    /// <summary>The CLI's output, or the reason it has none as a <see cref="PbiProviderException"/>.</summary>
    static string Answer(ProcessOutcome run) =>
        run.Succeeded ? run.StandardOutput : throw new PbiProviderException(Said(run));

    /// <summary>Why a run gave no answer, in the CLI's own first line where it said one.</summary>
    internal static string Said(ProcessOutcome run)
    {
        if (run.Missing)
        {
            return $"The GitHub CLI could not be started: {run.NotStarted}";
        }

        if (run.TimedOut)
        {
            return $"The GitHub CLI did not answer within {GhCli.Timeout.TotalSeconds} seconds.";
        }

        return (run.StandardError + "\n" + run.StandardOutput)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault()
            ?? $"The GitHub CLI exited with {run.ExitCode} and said nothing about why.";
    }

    static JsonDocument Parse(string printed)
    {
        try
        {
            return JsonDocument.Parse(printed);
        }
        catch (JsonException failure)
        {
            throw new PbiProviderException($"The GitHub CLI printed something that is not JSON: {failure.Message}");
        }
    }

    static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    static int Number(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : 0;

    static string[] Lines(string printed) =>
        printed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    static (string Owner, string Name) Split(string repository) =>
        repository.Split('/') is [var owner, var name] ? (owner, name) : throw new ArgumentException($"'{repository}' is not owner/name.", nameof(repository));

    static void Repository(string repository)
    {
        if (repository.Split('/') is not [{ Length: > 0 }, { Length: > 0 }])
        {
            throw new ArgumentException($"'{repository}' is not owner/name.", nameof(repository));
        }
    }

    static void Number(PbiRef pbi)
    {
        Repository(pbi.Repository);
        if (!int.TryParse(pbi.Id, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1)
        {
            throw new ArgumentException($"'{pbi.Id}' is not an issue number.", nameof(pbi));
        }
    }

    static void InsideRepository(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException($"'{path}' is not a path inside a repository.", nameof(path));
        }
    }

    [GeneratedRegex(@"#issuecomment-(?<id>\d+)")]
    private static partial Regex CommentAddress();
}
