using System.Globalization;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.SharedFunctionalTests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiOrchestrator.Modules.Pbis.FunctionalTests;

/// <summary>The host with a vendor held in memory that has one pull request, for issue #7.</summary>
public sealed class AioPrFixture : ApiServiceFixtureBase
{
    internal PullRequestVendor Vendor { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPbiProvider>();
            services.AddSingleton<IPbiProvider>(Vendor);
            services.RemoveAll<IFolderDirectory>();
            services.AddSingleton<IFolderDirectory>(new OneFolder());
        });
    }
}

/// <summary>
/// #651. <c>.aio/aio pr</c> run for real through <see cref="AioScript"/>: the pull request
/// read and reviewed through the control plane and the folder's adapter, as <c>aio issue</c> reads
/// the issue, so no command calls <c>gh pr</c> or <c>gh api</c>.
/// </summary>
public sealed class AioPrTests(AioPrFixture fixture) : IClassFixture<AioPrFixture>, IDisposable
{
    const string Repo = PullRequestVendor.Repository;

    readonly string _checkout = Directory.CreateTempSubdirectory("aio-pr-").FullName;

    public void Dispose() => Directory.Delete(_checkout, recursive: true);

    /// <summary>
    /// The reason for <c>aio pr</c>: <c>gh pr view --comments</c> showed 0 of the 2 comments anchored
    /// to a line on PR #457 (#648). Each one is printed with its file and line.
    /// </summary>
    [Fact]
    public async Task View_markdown_shows_every_line_comment_with_its_file_and_line()
    {
        fixture.Vendor.Arrange();

        var (code, markdown) = await Aio("pr", "view", "7", "--markdown");

        code.ShouldBe(0, markdown);
        markdown.ShouldStartWith("# El port lee el estado");
        markdown.ShouldContain("Pull request #20 of owner/repo#7 · open · opened by dev");
        markdown.ShouldContain("change/7 into main at 58b62b4 · 3 commits · +9 −3 in 1 files");
        markdown.ShouldContain("Checks: build passing");
        markdown.ShouldContain("Cierra #7.");
        markdown.ShouldContain("**dev** at 2026-10-11T10:00:00+00:00\n\nUna pregunta sobre el PR.");
        markdown.ShouldContain("review: comment\n\n1. src/Port.cs:2 — el estado sigue siendo un string.");
        markdown.ShouldContain("### src/Port.cs:2 (open)");
        markdown.ShouldContain("**reviewer** at 2026-10-11T11:00:00+00:00 on src/Port.cs:2\n\nDoes state stay a string?");
        markdown.ShouldContain("**dev** at 2026-10-11T11:30:00+00:00 on src/Port.cs:2\n\nYes, on purpose.");
        markdown.ShouldContain("### src/Old.cs:outdated (resolved, outdated)");
        markdown.ShouldNotContain("(no text)", customMessage: "a comment review with no body is the envelope of its line comments, not a finding");
    }

    [Fact]
    public async Task View_answers_one_json_line_with_the_threads_the_comments_and_the_reviews()
    {
        fixture.Vendor.Arrange();

        var (code, line) = await Aio("pr", "view", "7");

        code.ShouldBe(0, line);
        line.ShouldNotContain("\n", customMessage: "one JSON line");
        using var answer = JsonDocument.Parse(line);
        var pr = answer.RootElement.GetProperty("pr");
        pr.GetProperty("number").GetString().ShouldBe("20");
        pr.GetProperty("state").GetString().ShouldBe("open");
        pr.GetProperty("head").GetString().ShouldBe("change/7");
        pr.GetProperty("checks").EnumerateArray().Single().GetProperty("state").GetString().ShouldBe("passing");
        var thread = pr.GetProperty("threads")[0];
        thread.GetProperty("path").GetString().ShouldBe("src/Port.cs");
        thread.GetProperty("line").GetInt32().ShouldBe(2);
        thread.GetProperty("comments").GetArrayLength().ShouldBe(2);
        pr.GetProperty("reviews").EnumerateArray().Select(review => review.GetProperty("verdict").GetString()).ShouldBe(["comment", "comment"]);
        answer.RootElement.GetProperty("why").GetString()
            .ShouldBe($"Pull request #20 of {Repo}#7 is open, with 1 comments, 2 reviews and 3 comments on lines in 2 threads.");
    }

    [Fact]
    public async Task Diff_prints_the_change_as_git_prints_it()
    {
        fixture.Vendor.Arrange();

        var (code, diff) = await Aio("pr", "diff", "7");

        code.ShouldBe(0, diff);
        diff.ShouldBe(
            """
            diff --git a/src/Port.cs b/src/Port.cs
            --- a/src/Port.cs
            +++ b/src/Port.cs
            @@ -1,2 +1,3 @@
             a
            +b
             c
            """
        );
    }

    [Fact]
    public async Task Review_posts_the_body_as_a_comment_review_and_removes_the_file()
    {
        fixture.Vendor.Arrange();
        var body = Body("review.md", "1. src/Port.cs:2 — falta un test.\n");

        var (code, line) = await Aio("pr", "review", "7", "--verdict", "comment", "--body-file", body);

        code.ShouldBe(0, line);
        fixture.Vendor.Submitted.ShouldBe([("7", ReviewVerdict.Comment, "1. src/Port.cs:2 — falta un test.\n")]);
        using var answer = JsonDocument.Parse(line);
        answer.RootElement.GetProperty("review").GetProperty("verdict").GetString().ShouldBe("comment");
        answer.RootElement.GetProperty("why").GetString().ShouldBe($"The review is on the pull request of {Repo}#7, a comment review.");
        File.Exists(body).ShouldBeFalse("a body under .aio/ is removed once posted, so the worktree stays clean");
    }

    /// <summary>
    /// #544. The loop opens its pull requests as the person until it has an account of its own
    /// (#550), and GitHub refuses an approval from the author: it is kept as a comment, and said.
    /// </summary>
    [Fact]
    public async Task An_approval_of_ones_own_pull_request_is_kept_as_a_comment_and_said()
    {
        fixture.Vendor.Arrange();

        var (code, line) = await Aio("pr", "review", "7", "--verdict", "approve", "--body-file", Body("review.md", "Bien."));

        code.ShouldBe(0, line);
        using var answer = JsonDocument.Parse(line);
        answer.RootElement.GetProperty("review").GetProperty("verdict").GetString().ShouldBe("comment");
        answer.RootElement.GetProperty("review").GetProperty("asComment").GetString().ShouldBe("Can not approve your own pull request");
        answer.RootElement.GetProperty("why").GetString()
            .ShouldBe($"The review is on the pull request of {Repo}#7, kept as a comment instead of an approval, because the vendor said: Can not approve your own pull request.");
    }

    [Fact]
    public async Task A_verdict_it_does_not_know_is_refused_and_nothing_is_sent()
    {
        fixture.Vendor.Arrange();
        var body = Body("review.md", "x");

        var (code, line) = await Aio("pr", "review", "7", "--verdict", "lgtm", "--body-file", body);

        code.ShouldBe(2, line);
        JsonDocument.Parse(line).RootElement.GetProperty("why").GetString()
            .ShouldStartWith("aio: --verdict is comment, approve or request-changes, not 'lgtm'");
        fixture.Vendor.Submitted.ShouldBeEmpty();
        File.Exists(body).ShouldBeTrue("a body that was not posted stays for the next try");
    }

    [Fact]
    public async Task An_issue_with_no_pull_request_yet_is_null_and_said()
    {
        fixture.Vendor.Arrange();
        fixture.Vendor.HasPullRequest = false;

        var (code, line) = await Aio("pr", "view", "7", "--markdown");

        code.ShouldBe(0, line);
        using var answer = JsonDocument.Parse(line);
        answer.RootElement.GetProperty("pr").ValueKind.ShouldBe(JsonValueKind.Null);
        answer.RootElement.GetProperty("why").GetString().ShouldBe($"{Repo}#7 has no pull request yet.");
    }

    string Body(string name, string text)
    {
        var path = Path.Combine(_checkout, ".aio", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    Task<(int Code, string Said)> Aio(params string[] arguments) => AioScript.Run(fixture.CreateClient(), _checkout, Repo, arguments);
}

/// <summary>Issue #7 with pull request #20, opened by the account that reviews it, answered from memory.</summary>
sealed class PullRequestVendor : VendorStub, IPbiProvider
{
    public const string Repository = "owner/repo";

    static readonly PullRequestInfo Pull = new("20", new Uri("https://github.com/owner/repo/pull/20"), false, false, false);

    public bool HasPullRequest { get; set; } = true;

    public List<(string Id, ReviewVerdict Verdict, string Body)> Submitted { get; } = [];

    public void Arrange()
    {
        HasPullRequest = true;
        Submitted.Clear();
    }

    static DateTimeOffset At(string when) => DateTimeOffset.Parse(when, CultureInfo.InvariantCulture);

    public override Task<PullRequestChange?> GetPullRequestChangeAsync(PbiRef pbi, CancellationToken ct) =>
        Task.FromResult(
            HasPullRequest
                ? new PullRequestChange(
                    Pull,
                    "main",
                    "4f9c2e1",
                    "change/7",
                    "58b62b4def72ca26f13e2797fe3b63ccada0c01c",
                    3,
                    9,
                    3,
                    [new ChangedFile("src/Port.cs", null, "modified", 9, 3, "@@ -1,2 +1,3 @@\n a\n+b\n c")],
                    1,
                    Threads
                )
                : null
        );

    static readonly IReadOnlyList<ReviewThread> Threads =
        [
            new ReviewThread(
                "T1",
                "src/Port.cs",
                2,
                false,
                false,
                [
                    new ReviewComment("901", "src/Port.cs", 2, "reviewer", At("2026-10-11T11:00:00Z"), "Does state stay a string?"),
                    new ReviewComment("902", "src/Port.cs", 2, "dev", At("2026-10-11T11:30:00Z"), "Yes, on purpose."),
                ]
            ),
            new ReviewThread("T2", "src/Old.cs", null, true, true, [new ReviewComment("903", "src/Old.cs", null, "reviewer", At("2026-10-11T09:00:00Z"), "Gone now.")]),
        ];

    public override async Task<PullRequestReview?> ReadPullRequestReviewAsync(PbiRef pbi, CancellationToken ct) =>
        await GetPullRequestChangeAsync(pbi, ct) is { } change
            ? new PullRequestReview(change, [new PullRequestCheck("build", CheckState.Passing, null)], null, Conversation)
            : null;

    static readonly PullRequestConversation Conversation =
        new PullRequestConversation(
            "El port lee el estado",
            "open",
            "dev",
            "Cierra #7.",
            [new PbiComment("501", "dev", At("2026-10-11T10:00:00Z"), "Una pregunta sobre el PR.")],
            [
                new PullRequestReviewNote("601", ReviewVerdict.Comment, "reviewer", At("2026-10-11T10:30:00Z"), "1. src/Port.cs:2 — el estado sigue siendo un string."),
                new PullRequestReviewNote("602", ReviewVerdict.Comment, "reviewer", At("2026-10-11T11:00:00Z"), ""),
            ]
        );

    /// <summary>As GitHub answers the author of the pull request: an approval or a request for changes is kept as a comment.</summary>
    public override Task<Written<SubmittedReview>> SubmitReviewAsync(PbiRef pbi, ReviewVerdict verdict, string body, CancellationToken ct)
    {
        Submitted.Add((pbi.Id, verdict, body));
        var refusal = verdict switch
        {
            ReviewVerdict.Approve => "Can not approve your own pull request",
            ReviewVerdict.RequestChanges => "Can not request changes on your own pull request",
            _ => null,
        };
        return Task.FromResult(new Written<SubmittedReview>(new SubmittedReview("701", ReviewVerdict.Comment, "dev", body, refusal), null));
    }

    public override Task<PbiThread?> ReadThreadAsync(PbiRef pbi, CancellationToken ct) =>
        Task.FromResult<PbiThread?>(
            new PbiThread(new Pbi(pbi, "PBI", "code-review", false, null), new Uri($"https://github.com/{pbi.Repository}/issues/{pbi.Id}"), "dev", At("2026-10-11T09:00:00Z"), "", [], [], null, [], [])
        );

    public override Task<PullRequestInfo?> GetPullRequestAsync(PbiRef pbi, CancellationToken ct) => Task.FromResult(HasPullRequest ? Pull : null);

    // What `aio pr` never asks.
}
