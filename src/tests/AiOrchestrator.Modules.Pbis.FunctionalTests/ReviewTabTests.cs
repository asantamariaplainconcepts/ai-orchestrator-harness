using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.SharedFunctionalTests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiOrchestrator.Modules.Pbis.FunctionalTests;

/// <summary>The real host, with one folder and its vendor answered from memory.</summary>
public sealed class ReviewTabFixture : ApiServiceFixtureBase
{
    internal ReviewVendor Vendor { get; } = new();

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
/// #513. What the Review tab reads and writes through the API: the change, its threads, the checks
/// and the newest evidence in one answer; checks that fail alone; and a write the vendor refuses said in
/// its words. An image read only when the PBI carries it is IssueAndProposalTests' (#668).
/// </summary>
public sealed class ReviewTabTests(ReviewTabFixture fixture) : IClassFixture<ReviewTabFixture>
{
    const string Image = "https://github.com/user-attachments/assets/8efe4e72-07ae-4422-aff0-54cb3a7b5dec";
    const string Head = "58b62b4def72ca26f13e2797fe3b63ccada0c01c";

    [Fact]
    public async Task The_review_is_the_change_its_threads_its_checks_and_the_newest_evidence()
    {
        fixture.Vendor.Reset();
        fixture.Vendor.Comments.AddRange([
            Evidence("1", "0000000000000000000000000000000000000000", "inbox 1440x900 dark"),
            new PbiComment("2", "dev", DateTimeOffset.UtcNow, $"Not evidence.\n\n![a sketch]({Image})"),
            Evidence("3", Head, "settings 1440x900 light"),
        ]);
        using var client = fixture.CreateClient();

        var review = await client.GetFromJsonAsync<JsonElement>($"/api/pbi/review?repository={ReviewVendor.Repository}&id=7");

        review.GetProperty("pullRequest").GetProperty("id").GetString().ShouldBe("20");
        var change = review.GetProperty("change");
        change.GetProperty("baseRef").GetString().ShouldBe("main");
        change.GetProperty("headSha").GetString().ShouldBe(Head);
        change.GetProperty("commits").GetInt32().ShouldBe(41);
        change.GetProperty("files").EnumerateArray().Select(file => file.GetProperty("path").GetString()).ShouldBe(["src/Port.cs"]);

        var thread = review.GetProperty("threads").EnumerateArray().Single();
        thread.GetProperty("line").GetInt32().ShouldBe(2);
        thread.GetProperty("comments")[0].GetProperty("body").GetString().ShouldBe("Does state stay a string?");

        review.GetProperty("checks").GetProperty("rows")[0].GetProperty("state").GetString().ShouldBe("passing");

        var evidence = review.GetProperty("evidence");
        evidence.GetProperty("headSha").GetString().ShouldBe(Head);
        evidence.GetProperty("problem").ValueKind.ShouldBe(JsonValueKind.Null);
        var capture = evidence.GetProperty("captures").EnumerateArray().Single();
        capture.GetProperty("name").GetString().ShouldBe("settings 1440x900 light");
        capture.GetProperty("screen").GetString().ShouldBe("settings");
        capture.GetProperty("url").GetString().ShouldBe(Image);
        capture.GetProperty("width").GetInt32().ShouldBe(1440);
        capture.GetProperty("height").GetInt32().ShouldBe(900);
        capture.GetProperty("theme").GetString().ShouldBe("light");
    }

    /// <summary>
    /// #719. The comment <c>.aio/steps/evidence</c> posted on aio-dogfood#43, as GitHub keeps it: eight
    /// captures of two routes, whose alt texts start with the route. The tab read none of them while a
    /// screen had to be a word.
    /// </summary>
    [Fact]
    public async Task The_captures_of_routes_another_repository_published_are_read()
    {
        fixture.Vendor.Reset();
        fixture.Vendor.Comments.Add(new PbiComment("6165692197", "asantamariaplainconcepts", DateTimeOffset.Parse("2026-10-14T09:54:58Z", System.Globalization.CultureInfo.InvariantCulture), Dogfood43));
        using var client = fixture.CreateClient();

        var review = await client.GetFromJsonAsync<JsonElement>($"/api/pbi/review?repository={ReviewVendor.Repository}&id=7");

        var evidence = review.GetProperty("evidence");
        evidence.GetProperty("headSha").GetString().ShouldBe("9730f7fcfe7bdd1a39ca1e9da4ef541bb3215def");
        var captures = evidence.GetProperty("captures").EnumerateArray().ToList();
        captures.Count.ShouldBe(8);
        captures.Select(capture => capture.GetProperty("screen").GetString()).Distinct().ShouldBe(["/", "/?day=2026-10-15"]);
        captures.Select(capture => $"{capture.GetProperty("width").GetInt32()}x{capture.GetProperty("height").GetInt32()} {capture.GetProperty("theme").GetString()}")
            .Take(4)
            .ShouldBe(["1440x900 light", "1440x900 dark", "390x844 light", "390x844 dark"]);
        captures[4].GetProperty("name").GetString().ShouldBe("/?day=2026-10-15 1440x900 light");
        captures[4].GetProperty("url").GetString().ShouldBe("https://github.com/user-attachments/assets/ff6e4f85-76e0-4c91-bac8-d44be63ee272");
    }

    /// <summary>The body of comment 6165692197 on aio-dogfood#43, as <c>gh api</c> answered it on 14 October.</summary>
    const string Dogfood43 = """
        <!-- aio:evidence 9730f7fcfe7bdd1a39ca1e9da4ef541bb3215def -->
        Evidence for #43: 8 captures of `9730f7f`, the routes .aio/config.json (evidence) names.

        ![/ 1440x900 light](https://github.com/user-attachments/assets/f53bcc4a-15fd-4f36-809e-87382f8a79c2)

        ![/ 1440x900 dark](https://github.com/user-attachments/assets/7d24e624-ee2b-46d3-b6f5-4317dc6640fa)

        ![/ 390x844 light](https://github.com/user-attachments/assets/6c7fd39f-1fc9-4c6c-befc-1c873d058a2b)

        ![/ 390x844 dark](https://github.com/user-attachments/assets/d6268ffe-d473-46b7-931b-077225cb6749)

        ![/?day=2026-10-15 1440x900 light](https://github.com/user-attachments/assets/ff6e4f85-76e0-4c91-bac8-d44be63ee272)

        ![/?day=2026-10-15 1440x900 dark](https://github.com/user-attachments/assets/d62302e4-859c-4a6f-bf63-3357cbc07bdf)

        ![/?day=2026-10-15 390x844 light](https://github.com/user-attachments/assets/f54b28f8-716c-4aef-8d8d-cc375dcf85ee)

        ![/?day=2026-10-15 390x844 dark](https://github.com/user-attachments/assets/50a3cd24-4962-40e2-9822-32417962356d)
        """;

    [Fact]
    public async Task Checks_that_cannot_be_read_say_why_and_the_diff_is_still_read()
    {
        fixture.Vendor.Reset();
        fixture.Vendor.ChecksFail = "HTTP 403: Resource not accessible by integration";
        // An image whose alt text does not say what it shows is not a capture.
        fixture.Vendor.Comments.Add(new PbiComment("3", "dev", DateTimeOffset.UtcNow, $"<!-- aio:evidence {Head} -->\n\n![shot.png]({Image})"));
        using var client = fixture.CreateClient();

        var review = await client.GetFromJsonAsync<JsonElement>($"/api/pbi/review?repository={ReviewVendor.Repository}&id=7");

        review.GetProperty("checks").GetProperty("problem").GetString().ShouldBe("HTTP 403: Resource not accessible by integration");
        review.GetProperty("change").GetProperty("files").GetArrayLength().ShouldBe(1);
        review.GetProperty("evidence").GetProperty("problem").ValueKind.ShouldBe(JsonValueKind.Null);
        review.GetProperty("evidence").GetProperty("headSha").GetString().ShouldBe(Head);
        review.GetProperty("evidence").GetProperty("captures").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task A_thread_the_vendor_refuses_is_said_in_its_words()
    {
        fixture.Vendor.Reset();
        using var client = fixture.CreateClient();

        using var refused = await client.PostAsJsonAsync(
            "/api/pbi/review/comment",
            new { repository = ReviewVendor.Repository, id = "7", path = "src/Port.cs", line = 40, body = "Why?" }
        );
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString()
            .ShouldBe("The comment on src/Port.cs:40 of pull request 20 was not written: line must be part of the diff");

        using var written = await client.PostAsJsonAsync(
            "/api/pbi/review/comment",
            new { repository = ReviewVendor.Repository, id = "7", path = "src/Port.cs", line = 2, body = "Why?" }
        );
        written.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await written.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("body").GetString().ShouldBe("Why?");
    }

    /// <summary>
    /// #746. The quality is the newest comment marked <c>aio:quality</c>, an older one never read in its
    /// place: its rows typed as the script wrote them, a reason where one was not measured, and its
    /// head the pull request's, so it is not stale.
    /// </summary>
    [Fact]
    public async Task The_quality_is_the_newest_marked_comment_read_as_typed_rows()
    {
        fixture.Vendor.Reset();
        fixture.Vendor.Comments.AddRange([Quality("1", "0000000000000000000000000000000000000000", "ok"), Quality("2", Head, "over")]);
        using var client = fixture.CreateClient();

        var review = await client.GetFromJsonAsync<JsonElement>($"/api/pbi/review?repository={ReviewVendor.Repository}&id=7");

        var quality = review.GetProperty("quality");
        quality.GetProperty("headSha").GetString().ShouldBe(Head);
        quality.GetProperty("stale").GetBoolean().ShouldBeFalse();
        quality.GetProperty("author").GetString().ShouldBe("aio-bot");
        quality.GetProperty("problem").ValueKind.ShouldBe(JsonValueKind.Null);
        var rows = quality.GetProperty("rows").EnumerateArray()
            .Select(row => string.Join(" / ", new[] { "check", "measured", "line", "result" }.Select(name => row.GetProperty(name).GetString())))
            .ToList();
        rows.ShouldBe([
            "Coverage of changed lines / 25.0%: 1 of 4 changed lines covered / 70% / over",
            "CRAP of touched methods / highest 42.0 of 1 method, 1 over 30 / 30 / over",
            "Mutation in touched files / no summary.json at src/artifacts/mutation/summary.json / reported only / not measured",
        ]);
        review.GetProperty("evidence").GetProperty("captures").GetArrayLength().ShouldBe(0, "a quality comment carries no captures");
    }

    /// <summary>#746. A quality measured on a commit that is not the pull request's head is marked stale.</summary>
    [Fact]
    public async Task A_quality_of_another_commit_than_the_head_is_marked_stale()
    {
        fixture.Vendor.Reset();
        fixture.Vendor.Comments.Add(Quality("1", "1a2b3c4d5e6f708192a3b4c5d6e7f8091a2b3c4d", "ok"));
        using var client = fixture.CreateClient();

        var review = await client.GetFromJsonAsync<JsonElement>($"/api/pbi/review?repository={ReviewVendor.Repository}&id=7");

        var quality = review.GetProperty("quality");
        quality.GetProperty("headSha").GetString().ShouldBe("1a2b3c4d5e6f708192a3b4c5d6e7f8091a2b3c4d");
        quality.GetProperty("stale").GetBoolean().ShouldBeTrue();
        quality.GetProperty("rows").GetArrayLength().ShouldBe(3);
    }

    /// <summary>A comment as <c>.aio/steps/quality</c> posts it; <paramref name="crap"/> is the CRAP row's result.</summary>
    static PbiComment Quality(string id, string sha, string crap) =>
        new(
            id,
            "aio-bot",
            DateTimeOffset.UtcNow,
            $"""
            <!-- aio:quality {sha} -->
            Quality of `{sha[..7]}` against `4f9c2e1` for #7: 2 over, 1 not measured. It reports and blocks nothing; each line is quality in .aio/config.json.

            | Check | Measured | Line | Result |
            | --- | --- | --- | --- |
            | Coverage of changed lines | 25.0%: 1 of 4 changed lines covered | 70% | over |
            | CRAP of touched methods | highest 42.0 of 1 method, 1 over 30 | 30 | {crap} |
            | Mutation in touched files | no summary.json at src/artifacts/mutation/summary.json | reported only | not measured |
            """
        );

    /// <summary>A comment as <c>.aio/steps/evidence</c> posts it, with gh's attachment appended.</summary>
    static PbiComment Evidence(string id, string sha, string alt) =>
        new(
            id,
            "aio-bot",
            DateTimeOffset.UtcNow,
            $"""
            <!-- aio:evidence {sha} -->
            Evidence for #7: 1 capture of `{sha[..7]}`, the screens this change touched.

            ![{alt}]({Image})
            """
        );
}

/// <summary>One PBI with one pull request, answered from memory.</summary>
sealed class ReviewVendor : VendorStub, IPbiProvider
{
    public const string Repository = "owner/repo";

    public List<PbiComment> Comments { get; } = [];

    public string? ChecksFail { get; set; }

    public void Reset()
    {
        Comments.Clear();
        ChecksFail = null;
    }

    static readonly PullRequestInfo Pull = new("20", new Uri("https://github.com/owner/repo/pull/20"), false, false, false);

    public override Task<PullRequestChange?> GetPullRequestChangeAsync(PbiRef pbi, CancellationToken ct) =>
        Task.FromResult<PullRequestChange?>(
            new PullRequestChange(
                Pull,
                "main",
                "4f9c2e1",
                "change/7",
                "58b62b4def72ca26f13e2797fe3b63ccada0c01c",
                41,
                9,
                3,
                [new ChangedFile("src/Port.cs", null, "modified", 9, 3, "@@ -1,2 +1,3 @@\n a\n+b\n c")],
                1,
                [new ReviewThread("T1", "src/Port.cs", 2, false, false, [new ReviewComment("901", "src/Port.cs", 2, "dev", DateTimeOffset.UtcNow, "Does state stay a string?")])]
            )
        );

    /// <summary>The checks fail alone, as GitHubPbiProvider reads them: the change and its threads still answer.</summary>
    public override async Task<PullRequestReview?> ReadPullRequestReviewAsync(PbiRef pbi, CancellationToken ct) =>
        await GetPullRequestChangeAsync(pbi, ct) is { } change
            ? new PullRequestReview(
                change,
                ChecksFail is null ? [new PullRequestCheck("build", CheckState.Passing, null)] : [],
                ChecksFail,
                new PullRequestConversation("PBI", "open", "dev", string.Empty, [], [])
            )
            : null;

    public override Task<PbiThread?> ReadThreadAsync(PbiRef pbi, CancellationToken ct) =>
        Task.FromResult<PbiThread?>(
            new PbiThread(
                new Pbi(pbi, "PBI", "code-review", false, null),
                new Uri($"https://github.com/{pbi.Repository}/issues/{pbi.Id}"),
                "dev",
                DateTimeOffset.UtcNow,
                string.Empty,
                [],
                [],
                null,
                [],
                [.. Comments]
            )
        );

    public override Task<Written<ReviewComment>> AddReviewCommentAsync(PbiRef pbi, ReviewCommentDraft draft, CancellationToken ct) =>
        Task.FromResult(
            draft.Line == 2
                ? new Written<ReviewComment>(new ReviewComment("904", draft.Path!, draft.Line, "aio-bot", DateTimeOffset.UtcNow, draft.Body), null)
                : new Written<ReviewComment>(null, $"The comment on {draft.Path}:{draft.Line} of pull request 20 was not written: line must be part of the diff")
        );

    public override Task<PullRequestInfo?> GetPullRequestAsync(PbiRef pbi, CancellationToken ct) => Task.FromResult<PullRequestInfo?>(Pull);
}
