using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Infrastructure.PbiProviders;
using AiOrchestrator.Infrastructure.PbiProviders.GitHub;

namespace AiOrchestrator.InfrastructureTests.PbiProviders;

/// <summary>
/// #634. What <c>aio issue create</c>, <c>edit</c> and <c>list --search</c> ask
/// <see cref="GitHubPbiProvider"/>, against a <c>gh</c> that answers from memory: a create filed under
/// its parent by the numeric id the create answered, and read back among the parent's children; a body
/// replaced and read back; a search with its state. Behind <see cref="CachedPbiProvider"/>, a create
/// forgets the repository like every other write.
/// </summary>
public sealed class GitHubPbiProviderIssuesTests
{
    readonly FakeGh _gh = new();
    readonly GitHubPbiProvider _provider;
    readonly List<string> _subIssues = [];
    static readonly CancellationToken None = CancellationToken.None;

    public GitHubPbiProviderIssuesTests()
    {
        _provider = new GitHubPbiProvider(_gh.Run);
        _gh.Issues[1] = new FakeGh.Issue("La épica");
        _gh.Intercept = call =>
            call switch
            {
                ["api", "repos/owner/repo/issues", "--method", "POST", "-f", var title, "-f", _] => Created(title["title=".Length..]),
                ["api", "repos/owner/repo/issues/1/sub_issues", "--method", "POST", "-F", var id] => Linked(id["sub_issue_id=".Length..]),
                ["api", "repos/owner/repo/issues/1/sub_issues?per_page=100"] => FakeGh.Ok(_subIssues.Select(Child)),
                _ => null,
            };
    }

    [Fact]
    public async Task A_create_with_a_parent_links_the_new_issue_by_its_numeric_id_and_reads_it_back_among_the_children()
    {
        var written = await _provider.CreateAsync(FakeGh.Repository, "Una hija", "El cuerpo.", "1", None);

        written.Matches.ShouldBeTrue(written.Mismatch);
        written.Observed!.Ref.ShouldBe(new PbiRef(FakeGh.Repository, "8"));
        written.Observed.State.ShouldBeNull();
        _gh.Calls.ShouldContain(call => call.SequenceEqual(new[] { "api", "repos/owner/repo/issues/1/sub_issues", "--method", "POST", "-F", "sub_issue_id=9000008" }));
    }

    [Fact]
    public async Task A_link_github_refuses_is_a_mismatch_naming_the_issue_that_was_created()
    {
        var refused = _gh.Intercept!;
        _gh.Intercept = call => call is [_, "repos/owner/repo/issues/1/sub_issues", "--method", "POST", ..] ? FakeGh.Fail("gh: Validation Failed (HTTP 422)") : refused(call);

        var written = await _provider.CreateAsync(FakeGh.Repository, "Huérfana", "", "1", None);

        written.Observed!.Ref.Id.ShouldBe("8");
        written.Mismatch.ShouldBe("owner/repo#8 was created but is not among the children of #1 (gh said: gh: Validation Failed (HTTP 422)).");
    }

    [Fact]
    public async Task An_edit_replaces_the_body_and_reads_it_back()
    {
        string? body = null;
        _gh.Intercept = call =>
            call switch
            {
                ["api", "repos/owner/repo/issues/1", "--method", "PATCH", "-f", var sent] => FakeGh.Ok(new { number = 1, body = body = sent["body=".Length..] }),
                ["api", "repos/owner/repo/issues/1"] => FakeGh.Ok(new { number = 1, title = "La épica", body }),
                _ => null,
            };

        var written = await _provider.EditBodyAsync(new PbiRef(FakeGh.Repository, "1"), "El cuerpo, cerrado.\n", None);

        written.Matches.ShouldBeTrue(written.Mismatch);
        written.Observed.ShouldBe("El cuerpo, cerrado.\n");
    }

    [Fact]
    public async Task A_search_asks_gh_with_the_words_and_the_state()
    {
        _gh.Intercept = call =>
            call is ["issue", "list", "--repo", FakeGh.Repository, "--state", "all", "--limit", "100", "--search", "épica", ..]
                ? FakeGh.Ok(new[] { new { number = 1, title = "La épica", labels = Array.Empty<object>(), assignees = Array.Empty<object>(), state = "CLOSED" } })
                : null;

        var found = await _provider.ListAsync(FakeGh.Repository, new PbiQuery(PbiSearchState.All, 100, "épica"), None);

        found.ShouldHaveSingleItem().Closed.ShouldBeTrue();
    }

    [Fact]
    public async Task Behind_the_cache_a_create_forgets_the_parents_children()
    {
        var cached = CachedPbiProviderTests.Over(_provider, TimeProvider.System);
        (await cached.ListChildrenAsync(new PbiRef(FakeGh.Repository, "1"), None)).ShouldBeEmpty();

        await cached.CreateAsync(FakeGh.Repository, "Una hija", "", "1", None);

        (await cached.ListChildrenAsync(new PbiRef(FakeGh.Repository, "1"), None)).ShouldHaveSingleItem().Ref.Id.ShouldBe("8");
    }

    FakeGh.Issue Next(string title)
    {
        var issue = new FakeGh.Issue(title);
        _gh.Issues[8] = issue;
        return issue;
    }

    Infrastructure.ProcessOutcome Created(string title)
    {
        Next(title);
        return FakeGh.Ok(new { number = 8, id = 9_000_008L, title });
    }

    Infrastructure.ProcessOutcome Linked(string id)
    {
        id.ShouldBe("9000008");
        _subIssues.Add("8");
        return FakeGh.Ok(new { number = 1 });
    }

    object Child(string number) =>
        new
        {
            number = int.Parse(number, System.Globalization.CultureInfo.InvariantCulture),
            title = _gh.Issues[8].Title,
            state = "open",
            repository_url = "https://api.github.com/repos/owner/repo",
            labels = Array.Empty<object>(),
            assignees = Array.Empty<object>(),
        };
}
