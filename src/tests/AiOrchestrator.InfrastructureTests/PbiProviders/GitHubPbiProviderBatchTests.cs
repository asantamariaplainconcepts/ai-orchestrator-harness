using System.Text;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Infrastructure;
using AiOrchestrator.Infrastructure.PbiProviders;
using AiOrchestrator.Infrastructure.PbiProviders.GitHub;

namespace AiOrchestrator.InfrastructureTests.PbiProviders;

/// <summary>
/// #621. What the Proposal and Review tabs read of <see cref="GitHubPbiProvider"/>, counted against a
/// <c>gh</c> that records every call: a proposal of six files and the two specs it changes is two
/// calls and reads the same bytes the call-per-file read did; the Review tab is four calls and
/// answers what its members answer one at a time. #625: the four go at once when the pull request
/// comes from <c>change/&lt;n&gt;</c>, and a branch that is gone falls back to two waves.
/// </summary>
public sealed class GitHubPbiProviderBatchTests
{
    const string Head = "c63cd4e29906141c4207199a67b9472e37779b3f";
    const string Root = "openspec/changes/archive/2026-10-10-refine-leaves-a-closing-comment/";

    readonly FakeGh _gh = new();
    readonly List<IReadOnlyList<string>> _byteCalls = [];
    readonly GitHubPbiProvider _provider;
    static readonly PbiRef Seven = new(FakeGh.Repository, "7");
    static readonly CancellationToken None = CancellationToken.None;

    public GitHubPbiProviderBatchTests()
    {
        // The raw read answers from the same files the GraphQL one does, so the two can be compared.
        _provider = new GitHubPbiProvider(_gh.Run) { Bytes = RawRead };
        _gh.Issues[7] = new FakeGh.Issue("El comando deja un comentario de cierre");
        _gh.Issues[7].ClosedBy.Add((FakeGh.Repository, 20));
        var pull = new FakeGh.Pull { Head = Head, Branch = "change/7", BaseSha = "afd668de", State = "MERGED", Commits = 7 };
        _gh.Pulls[20] = pull;

        string[] change = ["proposal.md", "design.md", "specs/agent-commands-and-hooks/spec.md", "specs/harness-package/spec.md", "tasks.md", ".openspec.yaml"];
        foreach (var name in change)
        {
            pull.Files.Add(new FakeGh.File(Root + name, "added"));
            _gh.Blobs[$"{Head}:{Root}{name}"] = $"# {name}\n\nLínea con acento y\r\nun CRLF.\n";
        }

        pull.Files.Add(new FakeGh.File("src/Port.cs", "modified"));
        _gh.Blobs["HEAD:openspec/specs/agent-commands-and-hooks/spec.md"] = "# agent-commands-and-hooks\n";
        _gh.Blobs["HEAD:openspec/specs/harness-package/spec.md"] = "# harness-package\n";
    }

    [Fact]
    public async Task A_proposal_of_six_files_and_the_specs_it_changes_is_read_in_two_calls()
    {
        var change = (await _provider.GetPullRequestChangeAsync(Seven, None)).ShouldNotBeNull();
        RepositoryFile[] wanted =
        [
            .. change.Files.Where(file => file.Path.StartsWith(Root, StringComparison.Ordinal)).Select(file => new RepositoryFile(change.HeadSha, file.Path)),
            new(null, "openspec/specs/agent-commands-and-hooks/spec.md"),
            new(null, "openspec/specs/harness-package/spec.md"),
        ];
        var read = await _provider.ReadFilesAsync(FakeGh.Repository, wanted, None);

        _gh.Calls.Count.ShouldBe(2, "the change with its pull request, then every file at once");
        _byteCalls.ShouldBeEmpty();
        wanted.Length.ShouldBe(8);

        // The same bytes the call-per-file read gave before #621.
        for (var index = 0; index < wanted.Length; index++)
        {
            var before = (await _provider.ReadFilesAsync(FakeGh.Repository, [wanted[index]], None))[0];
            read[index].ShouldBe(before, wanted[index].Path);
        }

        _byteCalls.Count.ShouldBe(8, "each file was one call before");
    }

    [Fact]
    public async Task The_change_without_patches_is_the_change_with_them_less_the_patches()
    {
        _gh.Pulls[20].Files.Add(new FakeGh.File("docs/gone.md", "removed"));

        var light = (await _provider.GetPullRequestChangeAsync(Seven, None)).ShouldNotBeNull();
        var full = (await _provider.ReadPullRequestReviewAsync(Seven, None)).ShouldNotBeNull().Change;

        light.ShouldBe(full with { Files = light.Files, Threads = light.Threads });
        light.Files.ShouldBe([.. full.Files.Select(file => file with { Patch = null })]);
        light.Files.Single(file => file.Path == "docs/gone.md").Status.ShouldBe("removed");
    }

    [Fact]
    public async Task A_file_that_is_missing_reads_null_and_one_GraphQL_calls_binary_is_read_raw()
    {
        _gh.Intercept = call =>
            call is ["api", "graphql", "-f", var query, ..] && query.Contains("object(expression:", StringComparison.Ordinal)
                ? FakeGh.Ok(new { data = new { repository = new { f0 = (object?)null, f1 = new { text = (string?)null, isBinary = true, isTruncated = false } } } })
                : null;

        var read = await _provider.ReadFilesAsync(FakeGh.Repository, [new(Head, "nope.md"), new(Head, Root + "states.png")], None);

        read[0].ShouldBeNull();
        _byteCalls.ShouldHaveSingleItem()[1].ShouldBe($"repos/owner/repo/contents/{Root}states.png?ref={Head}");
    }

    [Fact]
    public async Task The_paths_go_as_variables_and_the_default_branch_is_HEAD()
    {
        await _provider.ReadFilesAsync(FakeGh.Repository, [new(null, "openspec/specs/harness-package/spec.md"), new("change/7", "a \"b\".md")], None);

        var call = _gh.Calls.ShouldHaveSingleItem();
        call.ShouldContain("e0=HEAD:openspec/specs/harness-package/spec.md");
        call.ShouldContain("e1=change/7:a \"b\".md");
        call[3].ShouldNotContain("harness-package");
        await Should.ThrowAsync<ArgumentException>(() => _provider.ReadFilesAsync(FakeGh.Repository, [new(null, "../secret")], None));
    }

    [Fact]
    public async Task The_review_of_a_change_branch_is_four_calls_at_once_and_answers_what_its_members_do()
    {
        AddThread();
        _gh.Intercept = Checks;

        var review = (await _provider.ReadPullRequestReviewAsync(Seven, None)).ShouldNotBeNull();
        _gh.Calls.Select(Named).ShouldBe(
            [
                "graphql",
                "repos/owner/repo/compare/HEAD...change/7",
                "repos/owner/repo/commits/change/7/check-runs?per_page=100",
                "repos/owner/repo/commits/change/7/status",
            ],
            "the pull request with its threads, and by the branch its change and its checks, all asked before any answer is read (#625)"
        );

        // GitHub refuses a query that could reach more than 500,000 nodes (#621): 25 pull requests,
        // each with 100 threads of 100 comments, is 252,525 with the threads and the pull requests.
        var query = _gh.Calls[0][3];
        query.ShouldContain("closedByPullRequestsReferences(first: 25)");
        query.ShouldContain("reviewThreads(first: 100)");
        query.ShouldContain("comments(first: 100)");
        query.ShouldContain("reviews(first: 100)");
        query.Split("(first: ").Length.ShouldBe(6, "those three connections, the conversation's two (#651), and no other");

        var change = (await _provider.GetPullRequestChangeAsync(Seven, None)).ShouldNotBeNull();
        review.Change.ShouldBe(change with { Files = review.Change.Files, Threads = review.Change.Threads });
        review.Change.Files.ShouldBe([.. change.Files.Select(file => file with { Patch = "@@ -0,0 +1 @@\n+a" })], "the change of the Proposal tab is the review's without the patches");
        review.Change.Threads.ShouldHaveSingleItem().Comments.ShouldHaveSingleItem().Body.ShouldBe("¿Seguro?");
        review.Change.Threads.ShouldBeEquivalentTo(change.Threads);
        review.Checks.Select(check => $"{check.Name} {check.State}").ShouldBe(["build Passing", "aspire do ci Failing"]);
        review.ChecksUnread.ShouldBeNull();
    }

    [Theory]
    [InlineData("gone")]
    [InlineData("named otherwise")]
    public async Task A_branch_that_is_gone_or_named_otherwise_falls_back_to_two_waves_with_the_same_answer(string branch)
    {
        AddThread();
        _gh.Intercept = Checks;
        var once = (await _provider.ReadPullRequestReviewAsync(Seven, None)).ShouldNotBeNull();
        _gh.Calls.Clear();

        if (branch == "gone")
        {
            _gh.Pulls[20].Compares = false;
        }
        else
        {
            _gh.Pulls[20].Branch = "feature/refine-comment";
        }

        var twice = (await _provider.ReadPullRequestReviewAsync(Seven, None)).ShouldNotBeNull();

        _gh.Calls.Count.ShouldBe(7, "the four of the first wave, then the files and both checks by the pull request and its head");
        _gh.Calls.Skip(4).Select(Named).ShouldBe(
            [
                "repos/owner/repo/pulls/20/files?per_page=100",
                $"repos/owner/repo/commits/{Head}/check-runs?per_page=100",
                $"repos/owner/repo/commits/{Head}/status",
            ]
        );
        twice.Change.ShouldBe(once.Change with { HeadRef = twice.Change.HeadRef, Files = twice.Change.Files, Threads = twice.Change.Threads }, "the branch's name is all that differs");
        twice.Change.Files.ShouldBe(once.Change.Files);
        twice.Change.Threads.ShouldBeEquivalentTo(once.Change.Threads);
        twice.Checks.ShouldBe(once.Checks);
        twice.ChecksUnread.ShouldBeNull();
    }

    [Fact]
    public async Task A_branch_that_moved_past_the_pull_requests_head_is_not_taken_for_it()
    {
        _gh.Intercept = call =>
            call is ["api", "repos/owner/repo/compare/HEAD...change/7"]
                ? FakeGh.Ok(new { merge_base_commit = new { sha = "afd668de" }, total_commits = 1, commits = new[] { new { sha = "pushed-after" } }, files = Array.Empty<object>() })
                : Checks(call);

        var review = (await _provider.ReadPullRequestReviewAsync(Seven, None)).ShouldNotBeNull();

        review.Change.Files.Count.ShouldBe(7, "the files of the pull request, asked again by its number");
        _gh.Calls.Select(Named).ShouldContain("repos/owner/repo/pulls/20/files?per_page=100");
        _gh.Calls.Select(Named).ShouldNotContain($"repos/owner/repo/commits/{Head}/status", "the branch's checks were of the head, so they stand");
    }

    [Fact]
    public async Task Checks_that_cannot_be_read_fail_alone_and_the_change_is_still_read()
    {
        _gh.Intercept = call =>
            call is ["api", var path, ..] && path.Contains("/check-runs", StringComparison.Ordinal) ? FakeGh.Fail("gh: HTTP 403: Resource not accessible") : Checks(call);

        var review = (await _provider.ReadPullRequestReviewAsync(Seven, None)).ShouldNotBeNull();

        review.Change.Files.Count.ShouldBe(7);
        review.Checks.ShouldBeEmpty();
        review.ChecksUnread.ShouldBe("gh: HTTP 403: Resource not accessible");
    }

    [Fact]
    public async Task A_PBI_without_a_pull_request_has_no_review_and_no_change_to_read()
    {
        _gh.Issues[7].ClosedBy.Clear();

        (await _provider.ReadPullRequestReviewAsync(Seven, None)).ShouldBeNull();
        (await _provider.GetPullRequestChangeAsync(Seven, None)).ShouldBeNull();
        _gh.Calls.Count.ShouldBe(4 + 1, "the review asks by the branch at the same time as for the pull request");
    }

    [Fact]
    public async Task The_cache_keeps_the_batched_reads_too()
    {
        var cached = CachedPbiProviderTests.Over(_provider, TimeProvider.System);
        RepositoryFile[] wanted = [new(Head, Root + "proposal.md"), new(null, "openspec/specs/harness-package/spec.md")];
        _gh.Intercept = Checks;

        for (var twice = 0; twice < 2; twice++)
        {
            await cached.GetPullRequestChangeAsync(Seven, None);
            await cached.ReadFilesAsync(FakeGh.Repository, wanted, None);
            await cached.ReadPullRequestReviewAsync(Seven, None);
        }

        _gh.Calls.Count.ShouldBe(1 + 1 + 4);
    }

    /// <summary>The check runs and statuses of the head, asked by its SHA or by the branch it is the tip of while that branch exists.</summary>
    ProcessOutcome? Checks(IReadOnlyList<string> call)
    {
        var pull = _gh.Pulls[20];
        string? Of(string path)
        {
            var reference = path["repos/owner/repo/commits/".Length..path.LastIndexOf('/')];
            return reference == Head || (reference == pull.Branch && pull.Compares) ? pull.Head : null;
        }

        return call switch
        {
            ["api", var path, "--jq", ".check_runs[]"] when path.EndsWith("/check-runs?per_page=100", StringComparison.Ordinal) && Of(path) is { } sha =>
                new ProcessOutcome(0, $$"""{"name":"build","status":"completed","conclusion":"success","html_url":"https://github.com/owner/repo/runs/1","head_sha":"{{sha}}"}""", ""),
            ["api", var path] when path.EndsWith("/status", StringComparison.Ordinal) && Of(path) is { } sha =>
                new ProcessOutcome(0, $$"""{"sha":"{{sha}}","statuses":[{"context":"aspire do ci","state":"failure","target_url":null}]}""", ""),
            _ => null,
        };
    }

    void AddThread() =>
        _gh.Pulls[20].Threads.Add(new
        {
            id = "PRRT_1",
            isResolved = false,
            isOutdated = false,
            path = "src/Port.cs",
            line = 3,
            comments = new { nodes = new object[] { new { databaseId = 901L, author = new { login = "dev" }, body = "¿Seguro?", createdAt = "2026-10-08T10:00:00Z" } } },
        });

    /// <summary>What a call asks for: <c>graphql</c>, or the REST path.</summary>
    static string Named(IReadOnlyList<string> call) => call[1];

    /// <summary>The raw contents endpoint over the fake's files: <c>repos/owner/repo/contents/{path}?ref={ref}</c>.</summary>
    Task<byte[]?> RawRead(IReadOnlyList<string> arguments, CancellationToken ct)
    {
        _byteCalls.Add(arguments);
        var address = arguments[1]["repos/owner/repo/contents/".Length..];
        var (path, reference) = address.Split("?ref=") is [var at, var named] ? (at, Uri.UnescapeDataString(named)) : (address, "HEAD");
        return Task.FromResult(_gh.Blobs.TryGetValue($"{reference}:{Uri.UnescapeDataString(path)}", out var text) ? Encoding.UTF8.GetBytes(text) : null);
    }
}
