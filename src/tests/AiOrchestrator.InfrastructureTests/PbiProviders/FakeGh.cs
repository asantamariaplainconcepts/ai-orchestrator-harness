using System.Globalization;
using System.Text.Json;
using AiOrchestrator.Infrastructure;

namespace AiOrchestrator.InfrastructureTests.PbiProviders;

/// <summary>
/// A GitHub repository held in memory, answering the arguments <c>GitHubPbiProvider</c> gives
/// <c>gh</c> the way the CLI does: the same JSON shapes, exit 1 with <c>HTTP 404</c> for absence, and
/// an edit that fails when it names a label the repository does not have. <see cref="Intercept"/>
/// answers in its place, and <see cref="AfterEdit"/> changes the repository behind the writer's back,
/// which is what the read-back exists to notice.
/// </summary>
sealed class FakeGh
{
    public const string Repository = "owner/repo";

    /// <summary>The account the credential belongs to.</summary>
    public const string Login = "aio-bot";

    public Dictionary<int, Issue> Issues { get; } = [];

    public HashSet<string> Labels { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<int, Pull> Pulls { get; } = [];

    public Dictionary<long, (int Issue, string Body)> Comments { get; } = [];

    public List<IReadOnlyList<string>> Calls { get; } = [];

    /// <summary>Answers a call instead of the repository when it returns non-null.</summary>
    public Func<IReadOnlyList<string>, ProcessOutcome?>? Intercept { get; set; }

    /// <summary>Runs after every successful <c>issue edit</c>.</summary>
    public Action<Issue>? AfterEdit { get; set; }

    long _nextId = 9_000_000_001;

    public sealed class Issue(string title)
    {
        public string Title { get; } = title;

        public List<string> Labels { get; } = [];

        public string? Assignee { get; set; }

        public bool IsPullRequest { get; set; }

        public bool Closed { get; set; }

        public List<(string Repository, int Number)> ClosedBy { get; } = [];
    }

    public sealed class Pull
    {
        public bool IsDraft { get; set; }

        public string State { get; set; } = "OPEN";

        public string ReviewDecision { get; set; } = "";

        public string Head { get; set; } = "abc123";

        /// <summary>The branch it comes from, which <c>pr list --head</c> finds it by.</summary>
        public string Branch { get; set; } = "";

        public string Title { get; set; } = "";

        public string Body { get; set; } = "";

        /// <summary>The lines of the diff a review comment may sit on, as <c>path:line</c>.</summary>
        public HashSet<string> DiffLines { get; } = [];

        public List<(long Id, string Path, int? Line, string Body)> Reviews { get; } = [];

        public string BaseRef { get; set; } = "main";

        public string BaseSha { get; set; } = "";

        public int Commits { get; set; }

        public int Additions { get; set; }

        public int Deletions { get; set; }

        /// <summary>The files it touches, which GraphQL lists without the patch and the previous path and REST with them.</summary>
        public List<File> Files { get; } = [];

        /// <summary>Its review threads as GraphQL prints them.</summary>
        public List<object> Threads { get; } = [];

        /// <summary>Its comments on the pull request as a whole, as GraphQL prints them.</summary>
        public List<object> Conversation { get; } = [];

        /// <summary>Its submitted reviews, as GraphQL prints them.</summary>
        public List<object> Submitted { get; } = [];

        /// <summary>Its branch still exists, so the compare endpoint answers for it; false once it is deleted after the merge.</summary>
        public bool Compares { get; set; } = true;
    }

    /// <param name="Status">REST's word: <c>added</c>, <c>removed</c>, <c>modified</c>, <c>renamed</c>.</param>
    public sealed record File(string Path, string Status, int Additions = 1, int Deletions = 0, string? Patch = "@@ -0,0 +1 @@\n+a", string? PreviousPath = null);

    /// <summary>The text of each file GraphQL reads, by its expression <c>ref:path</c>; <c>HEAD</c> is the default branch.</summary>
    public Dictionary<string, string> Blobs { get; } = new(StringComparer.Ordinal);

    public Task<ProcessOutcome> Run(IReadOnlyList<string> arguments, CancellationToken ct)
    {
        Calls.Add(arguments);
        return Task.FromResult(Intercept?.Invoke(arguments) ?? Answer([.. arguments]));
    }

    public static ProcessOutcome Ok(object value) => new(0, JsonSerializer.Serialize(value), "");

    public static ProcessOutcome Fail(string said) => new(1, "", said);

    static ProcessOutcome NotFound() =>
        new(1, """{"message":"Not Found","status":"404"}""", "gh: Not Found (HTTP 404)");

    ProcessOutcome Answer(string[] a)
    {
        string Field(string flag, string name) =>
            a.Select((value, index) => (value, index))
                .Where(pair => pair.value == flag && a[pair.index + 1].StartsWith(name + "=", StringComparison.Ordinal))
                .Select(pair => a[pair.index + 1][(name.Length + 1)..])
                .Single();

        switch (a)
        {
            case ["issue", "list", "--repo", Repository, "--state", var state, ..]:
                return Ok(
                    Issues
                        .Where(pair => !pair.Value.IsPullRequest && (state == "all" || pair.Value.Closed == (state == "closed")))
                        .Select(pair => Row(pair.Key, pair.Value))
                );

            // The issue as its page has it (#512), which answers for a pull request too, at its /pull/ address.
            case ["issue", "view", var id, "--repo", Repository, "--json", _]:
                {
                    var number = int.Parse(id, CultureInfo.InvariantCulture);
                    if (!Issues.TryGetValue(number, out var viewed))
                    {
                        return new(1, "", $"GraphQL: Could not resolve to an issue or pull request with the number of {number}. (repository.issue)");
                    }

                    return Ok(new
                    {
                        number,
                        title = viewed.Title,
                        url = $"https://github.com/{Repository}/{(viewed.IsPullRequest ? "pull" : "issues")}/{number}",
                        body = "",
                        author = new { login = "dev" },
                        createdAt = "2026-10-08T09:00:00Z",
                        state = viewed.Closed ? "CLOSED" : "OPEN",
                        labels = viewed.Labels.Select(name => new { name }),
                        assignees = viewed.Assignee is null ? [] : new[] { new { login = viewed.Assignee } },
                        comments = Array.Empty<object>(),
                    });
                }

            case ["label", "list", "--repo", Repository, "--search", var search, "--limit", _, "--json", "name"]:
                return Ok(Labels.Where(label => label.Contains(search, StringComparison.OrdinalIgnoreCase)).Select(label => new { name = label }));

            case ["label", "list", "--repo", Repository, "--limit", _, "--json", "name"]:
                return Ok(Labels.Select(label => new { name = label }));

            case ["label", "create", var label, "--repo", Repository, ..]:
                return Labels.Add(label) ? Ok("") : Fail($"label with name \"{label}\" already exists; use `--force` to update its color and description");

            case ["api", "user", "--jq", ".login"]:
                return new(0, Login + "\n", "");

            case ["issue", "edit", var id, "--repo", Repository, "--add-assignee", var login]:
                Issues[int.Parse(id, CultureInfo.InvariantCulture)].Assignee ??= login;
                return Ok("");

            case ["issue", "close", var id, "--repo", Repository]:
                Issues[int.Parse(id, CultureInfo.InvariantCulture)].Closed = true;
                return Ok("");

            case ["pr", "list", "--repo", Repository, "--head", var branch, "--state", "open", "--json", _]:
                return Ok(Pulls.Where(pair => pair.Value.Branch == branch && pair.Value.State == "OPEN").Select(pair => PullRow(pair.Key, pair.Value)));

            case ["pr", "create", "--repo", Repository, "--head", var branch, "--draft", "--title", var title, "--body", var body]:
                {
                    var number = Pulls.Count == 0 ? 20 : Pulls.Keys.Max() + 1;
                    Pulls[number] = new Pull { Branch = branch, Title = title, Body = body, IsDraft = true };
                    if (body.StartsWith("Closes #", StringComparison.Ordinal))
                    {
                        Issues[int.Parse(body["Closes #".Length..], CultureInfo.InvariantCulture)].ClosedBy.Add((Repository, number));
                    }
                    return new(0, $"https://github.com/{Repository}/pull/{number}\n", "");
                }

            // #566. The install's pull request: ready for review, closing no issue.
            case ["pr", "create", "--repo", Repository, "--head", var branch, "--title", var title, "--body", var body]:
                {
                    var number = Pulls.Count == 0 ? 20 : Pulls.Keys.Max() + 1;
                    Pulls[number] = new Pull { Branch = branch, Title = title, Body = body };
                    return new(0, $"https://github.com/{Repository}/pull/{number}\n", "");
                }

            case ["pr", "ready", var id, "--repo", Repository]:
                Pulls[int.Parse(id, CultureInfo.InvariantCulture)].IsDraft = false;
                return Ok("");

            case ["issue", "edit", var id, "--repo", Repository, .. var flags]:
                {
                    var issue = Issues[int.Parse(id, CultureInfo.InvariantCulture)];
                    var adds = Pairs(flags, "--add-label");
                    if (adds.FirstOrDefault(label => !Labels.Contains(label)) is { } unknown)
                    {
                        return Fail($"failed to update https://github.com/{Repository}/issues/{id}: '{unknown}' not found");
                    }

                    issue.Labels.RemoveAll(label => Pairs(flags, "--remove-label").Contains(label, StringComparer.OrdinalIgnoreCase));
                    issue.Labels.AddRange(adds.Where(label => !issue.Labels.Contains(label)));
                    AfterEdit?.Invoke(issue);
                    return Ok("");
                }

            // The pull requests that close an issue, each with whatever the query asked of it (#621).
            case ["api", "graphql", "-f", var query, "-f", "owner=owner", "-f", "name=repo", "-F", var number]
                when query.Contains("closedByPullRequestsReferences", StringComparison.Ordinal):
                {
                    var id = int.Parse(number["number=".Length..], CultureInfo.InvariantCulture);
                    if (!Issues.TryGetValue(id, out var viewed))
                    {
                        return new(1, """{"data":{"repository":{"issue":null}}}""", $"gh: Could not resolve to an Issue with the number of {id}.");
                    }

                    var nodes = viewed.ClosedBy.Select(closing => ClosingNode(closing.Repository, closing.Number, query));
                    return Ok(new { data = new { repository = new { issue = new { closedByPullRequestsReferences = new { nodes } } } } });
                }

            // Blobs by expression, one alias each: f0 for e0, and so on.
            case ["api", "graphql", "-f", var query, "-f", "owner=owner", "-f", "name=repo", .. var expressions]
                when query.Contains("object(expression:", StringComparison.Ordinal):
                {
                    var found = new Dictionary<string, object?>();
                    for (var index = 0; index + 1 < expressions.Length; index += 2)
                    {
                        var (alias, expression) = (expressions[index + 1].Split('=', 2)[0], expressions[index + 1].Split('=', 2)[1]);
                        found["f" + alias[1..]] = Blobs.TryGetValue(expression, out var text) ? new { text, isBinary = false, isTruncated = false } : null;
                    }

                    return Ok(new { data = new { repository = found } });
                }

            case ["api", var path, .. var rest]:
                return Api(path.Split('/')[3..], rest, Field);

            default:
                throw new InvalidOperationException($"The fake has no answer for: gh {string.Join(' ', a)}");
        }
    }

    ProcessOutcome Api(string[] path, string[] rest, Func<string, string, string> field)
    {
        var post = rest is ["--method", "POST", ..];

        switch (path)
        {
            case ["issues", "comments", var id] when rest is ["--method", "PATCH", ..]:
                {
                    var edited = long.Parse(id, CultureInfo.InvariantCulture);
                    if (!Comments.TryGetValue(edited, out var kept))
                    {
                        return NotFound();
                    }

                    Comments[edited] = (kept.Issue, field("-f", "body"));
                    return Ok(CommentRow(edited, field("-f", "body")));
                }

            case ["issues", "comments", var id]:
                return Comments.TryGetValue(long.Parse(id, CultureInfo.InvariantCulture), out var comment)
                    ? Ok(CommentRow(long.Parse(id, CultureInfo.InvariantCulture), comment.Body))
                    : NotFound();

            case ["issues", var number, "comments"] when post:
                {
                    var id = _nextId++;
                    Comments[id] = (int.Parse(number, CultureInfo.InvariantCulture), field("-f", "body"));
                    return Ok(CommentRow(id, field("-f", "body")));
                }

            case ["issues", var number]:
                return Issues.TryGetValue(int.Parse(number, CultureInfo.InvariantCulture), out var issue)
                    ? Ok(issue.IsPullRequest ? new Dictionary<string, object> { ["number"] = number, ["pull_request"] = new { } } : Row(int.Parse(number, CultureInfo.InvariantCulture), issue))
                    : NotFound();

            case ["pulls", "comments", var id]:
                {
                    var found = Pulls.Values.SelectMany(pull => pull.Reviews).Where(review => review.Id == long.Parse(id, CultureInfo.InvariantCulture)).ToArray();
                    return found is [var review] ? Ok(ReviewRow(review)) : NotFound();
                }

            case ["pulls", var number, "comments"] when post:
                {
                    var pull = Pulls[int.Parse(number, CultureInfo.InvariantCulture)];
                    var at = $"{field("-f", "path")}:{field("-F", "line")}";
                    if (field("-f", "commit_id") != pull.Head || !pull.DiffLines.Contains(at))
                    {
                        return Fail("gh: Validation Failed (HTTP 422)");
                    }

                    var review = (_nextId++, field("-f", "path"), (int?)int.Parse(field("-F", "line"), CultureInfo.InvariantCulture), field("-f", "body"));
                    pull.Reviews.Add(review);
                    return Ok(ReviewRow(review));
                }

            case ["pulls", var number, "files?per_page=100"]:
                return new(
                    0,
                    string.Join(
                        '\n',
                        Pulls[int.Parse(number, CultureInfo.InvariantCulture)].Files.Select(file => JsonSerializer.Serialize(new
                        {
                            filename = file.Path,
                            previous_filename = file.PreviousPath,
                            status = file.Status,
                            additions = file.Additions,
                            deletions = file.Deletions,
                            patch = file.Patch,
                        }))
                    ),
                    ""
                );

            // #625. The default branch against a branch, by its name: what the pull request from it
            // carries, if one does, with the head as the last commit and the base it forked from.
            case ["compare", var basis, .. var tail] when basis.StartsWith("HEAD...", StringComparison.Ordinal):
                {
                    var branch = string.Join('/', [basis["HEAD...".Length..], .. tail]);
                    if (Pulls.Values.FirstOrDefault(pull => pull.Branch == branch && pull.Compares) is not { } pull)
                    {
                        return NotFound();
                    }

                    return Ok(new
                    {
                        merge_base_commit = new { sha = pull.BaseSha },
                        total_commits = pull.Commits,
                        commits = Enumerable.Range(1, pull.Commits).Select(index => new { sha = index == pull.Commits ? pull.Head : $"parent{index}" }),
                        files = pull.Files.Select(file => new
                        {
                            filename = file.Path,
                            previous_filename = file.PreviousPath,
                            status = file.Status,
                            additions = file.Additions,
                            deletions = file.Deletions,
                            patch = file.Patch,
                        }),
                    });
                }

            // The checks of a commit are the test's to answer through Intercept; a branch or a SHA it
            // does not answer is one GitHub does not know.
            case ["commits", ..]:
                return NotFound();

            case ["pulls", var number, "comments"]:
                return new(0, string.Join('\n', Pulls[int.Parse(number, CultureInfo.InvariantCulture)].Reviews.Select(review => JsonSerializer.Serialize(ReviewRow(review)))), "");

            default:
                throw new InvalidOperationException($"The fake has no answer for: gh api {string.Join('/', path)}");
        }
    }

    static List<string> Pairs(string[] flags, string flag) =>
        [.. flags.Select((value, index) => (value, index)).Where(pair => pair.value == flag).Select(pair => flags[pair.index + 1])];

    static object Row(int number, Issue issue) =>
        new
        {
            number,
            title = issue.Title,
            labels = issue.Labels.Select(name => new { name }),
            assignees = issue.Assignee is null ? [] : new[] { new { login = issue.Assignee } },
            state = issue.Closed ? "closed" : "open",
        };

    Dictionary<string, object> ClosingNode(string repository, int number, string query)
    {
        var pull = repository == Repository ? Pulls.GetValueOrDefault(number) ?? new Pull() : new Pull();
        var node = new Dictionary<string, object>
        {
            ["number"] = number,
            ["url"] = $"https://github.com/{repository}/pull/{number}",
            ["isDraft"] = pull.IsDraft,
            ["state"] = pull.State,
            ["reviewDecision"] = pull.ReviewDecision,
            ["headRefOid"] = pull.Head,
            ["headRefName"] = pull.Branch,
            ["baseRefName"] = pull.BaseRef,
            ["baseRefOid"] = pull.BaseSha,
            ["additions"] = pull.Additions,
            ["deletions"] = pull.Deletions,
            ["changedFiles"] = pull.Files.Count,
            ["commits"] = new { totalCount = pull.Commits },
            ["repository"] = new { nameWithOwner = repository },
        };
        if (query.Contains("files(first: 100)", StringComparison.Ordinal))
        {
            node["files"] = new
            {
                pageInfo = new { hasNextPage = false },
                nodes = pull.Files.Select(file => new
                {
                    path = file.Path,
                    changeType = file.Status == "removed" ? "DELETED" : file.Status.ToUpperInvariant(),
                    additions = file.Additions,
                    deletions = file.Deletions,
                }),
            };
        }

        if (query.Contains("reviewThreads(first: 100)", StringComparison.Ordinal))
        {
            node["reviewThreads"] = new { nodes = pull.Threads };
        }

        if (query.Contains("reviews(first: 100)", StringComparison.Ordinal))
        {
            node["title"] = pull.Title;
            node["body"] = pull.Body;
            node["author"] = new { login = "dev" };
            node["comments"] = new { nodes = pull.Conversation };
            node["reviews"] = new { nodes = pull.Submitted };
        }

        return node;
    }

    static object PullRow(int number, Pull pull) =>
        new
        {
            number,
            url = $"https://github.com/{Repository}/pull/{number}",
            isDraft = pull.IsDraft,
            state = pull.State,
            reviewDecision = pull.ReviewDecision,
            headRefOid = pull.Head,
        };

    static object CommentRow(long id, string body) =>
        new { id, body, user = new { login = "dev" }, created_at = "2026-10-08T10:00:00Z" };

    static object ReviewRow((long Id, string Path, int? Line, string Body) review) =>
        new
        {
            id = review.Id,
            path = review.Path,
            line = review.Line,
            body = review.Body,
            user = new { login = "dev" },
            created_at = "2026-10-08T10:00:00Z",
        };
}
