using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.Modules.Loop.FunctionalTests;

/// <summary>
/// #513. The Review tab's two verdicts through the API, against the vendor held in memory: an
/// approval is written at the vendor and the hold of <c>merge-review</c> goes on to <c>retro</c>;
/// a request for changes is written, copied to the issue with its open threads, and sends the PBI
/// back to <c>in-progress</c>; a review the vendor refuses moves nothing.
/// </summary>
public sealed class ReviewVerdictTests(LoopFixture fixture) : IClassFixture<LoopFixture>
{
    [Fact]
    public async Task Approving_writes_the_approval_and_the_hold_of_merge_review_goes_on_to_retro()
    {
        Pbi(31, "merge-review");
        fixture.Vendor.Pulls["31"] = new PullRequestInfo("131", new Uri("https://github.com/owner/repo/pull/131"), false, false, false);
        fixture.Scripts.Answers["diff-risk"] = (0, """{"outcome": "high", "why": "touches the IPbiProvider port"}""");
        using var client = fixture.CreateClient();
        await Post(client, "/api/loop/run-once", null, HttpStatusCode.OK);
        fixture.Vendor.Pbis.Single(pbi => pbi.Ref.Id == "31").Held.ShouldBeTrue();

        var approved = await Post(client, "/api/pbi/review/approve", new { repository = FakeFolders.Repository, id = "31" }, HttpStatusCode.OK);

        approved.GetProperty("unmoved").ValueKind.ShouldBe(JsonValueKind.Null);
        var line = approved.GetProperty("line");
        line.GetProperty("kind").GetString().ShouldBe("released");
        line.GetProperty("from").GetString().ShouldBe("merge-review");
        line.GetProperty("to").GetString().ShouldBe("retro");
        line.GetProperty("why").GetString().ShouldBe("a person approved the pull request");
        fixture.Vendor.Writes.ShouldContain("approve #31");
        fixture.Vendor.Pulls["31"].IsApproved.ShouldBeTrue();
        var pbi = fixture.Vendor.Pbis.Single(row => row.Ref.Id == "31");
        pbi.Held.ShouldBeFalse();
        pbi.State.ShouldBe("retro");
    }

    [Fact]
    public async Task A_review_the_vendor_refuses_is_said_in_its_words_and_moves_nothing()
    {
        Pbi(32, "merge-review", held: true);
        fixture.Vendor.RefusesReviews = "The approval of pull request 132 was not written: Can not approve your own pull request";
        try
        {
            using var client = fixture.CreateClient();

            var refused = await Post(client, "/api/pbi/review/approve", new { repository = FakeFolders.Repository, id = "32" }, HttpStatusCode.Conflict);

            refused.GetProperty("detail").GetString().ShouldBe(fixture.Vendor.RefusesReviews);
            fixture.Vendor.Pbis.Single(pbi => pbi.Ref.Id == "32").ShouldSatisfyAllConditions(
                pbi => pbi.Held.ShouldBeTrue(),
                pbi => pbi.State.ShouldBe("merge-review")
            );
        }
        finally
        {
            fixture.Vendor.RefusesReviews = null;
        }
    }

    [Fact]
    public async Task An_approval_by_the_author_goes_on_as_a_comment_releases_the_hold_and_the_journal_says_so()
    {
        Pbi(35, "merge-review");
        fixture.Vendor.Pulls["35"] = new PullRequestInfo("135", new Uri("https://github.com/owner/repo/pull/135"), false, false, false);
        fixture.Scripts.Answers["diff-risk"] = (0, """{"outcome": "high", "why": "touches the IPbiProvider port"}""");
        using var client = fixture.CreateClient();
        await Post(client, "/api/loop/run-once", null, HttpStatusCode.OK);
        fixture.Vendor.ReviewsAsAuthor = true;
        try
        {
            var approved = await Post(client, "/api/pbi/review/approve", new { repository = FakeFolders.Repository, id = "35" }, HttpStatusCode.OK);

            approved.GetProperty("asComment").GetString().ShouldBe("Review Can not approve your own pull request");
            var line = approved.GetProperty("line");
            line.GetProperty("kind").GetString().ShouldBe("released");
            line.GetProperty("to").GetString().ShouldBe("retro");
            line.GetProperty("why").GetString().ShouldBe("a person approved the pull request, written as a comment: Review Can not approve your own pull request");
            fixture.Vendor.Writes.ShouldContain("review #35");
            fixture.Vendor.Pbis.Single(pbi => pbi.Ref.Id == "35").ShouldSatisfyAllConditions(
                pbi => pbi.Held.ShouldBeFalse(),
                pbi => pbi.State.ShouldBe("retro")
            );

            var journal = await client.GetStringAsync($"/api/loop/journal?repository={FakeFolders.Repository}&pbi=35");
            journal.ShouldContain("written as a comment: Review Can not approve your own pull request");
        }
        finally
        {
            fixture.Vendor.ReviewsAsAuthor = false;
        }
    }

    [Fact]
    public async Task A_request_for_changes_by_the_author_goes_on_as_a_comment_and_sends_the_PBI_back_to_in_progress()
    {
        Pbi(36, "merge-review", held: true);
        fixture.Vendor.Pulls["36"] = new PullRequestInfo("136", new Uri("https://github.com/owner/repo/pull/136"), false, false, false);
        fixture.Vendor.ReviewsAsAuthor = true;
        try
        {
            using var client = fixture.CreateClient();

            var requested = await Post(
                client,
                "/api/pbi/review/request-changes",
                new { repository = FakeFolders.Repository, id = "36", body = "Rename it." },
                HttpStatusCode.OK
            );

            requested.GetProperty("asComment").GetString().ShouldBe("Review Can not request changes on your own pull request");
            requested.GetProperty("line").GetProperty("to").GetString().ShouldBe("in-progress");
            fixture.Vendor.Writes.ShouldContain("review #36");
            fixture.Vendor.Pbis.Single(pbi => pbi.Ref.Id == "36").State.ShouldBe("in-progress");
        }
        finally
        {
            fixture.Vendor.ReviewsAsAuthor = false;
        }
    }

    [Fact]
    public async Task Requesting_changes_copies_the_review_and_its_open_threads_to_the_issue_and_goes_back_to_in_progress()
    {
        Pbi(33, "merge-review", held: true);
        fixture.Vendor.Pulls["33"] = new PullRequestInfo("133", new Uri("https://github.com/owner/repo/pull/133"), false, false, false);
        fixture.Vendor.Threads["33"] =
        [
            new ReviewThread("T1", "src/Port.cs", 12, false, false, [new ReviewComment("901", "src/Port.cs", 12, "dev", DateTimeOffset.UtcNow, "Keep the state a string.")]),
            new ReviewThread("T2", "src/Done.cs", 3, true, false, [new ReviewComment("902", "src/Done.cs", 3, "dev", DateTimeOffset.UtcNow, "Already fixed.")]),
        ];
        using var client = fixture.CreateClient();

        var requested = await Post(
            client,
            "/api/pbi/review/request-changes",
            new { repository = FakeFolders.Repository, id = "33", body = "The port leaks a label." },
            HttpStatusCode.OK
        );

        requested.GetProperty("line").GetProperty("to").GetString().ShouldBe("in-progress");
        requested.GetProperty("line").GetProperty("why").GetString().ShouldBe("a person requested changes on the pull request");
        fixture.Vendor.Writes.ShouldContain("request changes on #33");
        fixture.Vendor.Writes.ShouldContain(
            "comment #33: Changes requested on pull request #133:\n\nThe port leaks a label.\n\nOpen threads:\n\n- `src/Port.cs:12` dev: Keep the state a string.\n"
        );
        fixture.Vendor.Pbis.Single(pbi => pbi.Ref.Id == "33").State.ShouldBe("in-progress");
    }

    [Fact]
    public async Task A_verdict_on_a_PBI_the_loop_is_working_on_is_written_and_says_why_the_loop_did_not_move()
    {
        Pbi(34, "code-review", held: false);
        using var client = fixture.CreateClient();

        var requested = await Post(
            client,
            "/api/pbi/review/request-changes",
            new { repository = FakeFolders.Repository, id = "34", body = "Rename it." },
            HttpStatusCode.OK
        );

        requested.GetProperty("line").ValueKind.ShouldBe(JsonValueKind.Null);
        requested.GetProperty("unmoved").GetString().ShouldNotBeNull().ShouldContain("code-review is not in hold");
        fixture.Vendor.Writes.ShouldContain("request changes on #34");
        fixture.Vendor.Pbis.Single(pbi => pbi.Ref.Id == "34").State.ShouldBe("code-review");

        await Post(client, "/api/pbi/review/request-changes", new { repository = FakeFolders.Repository, id = "34", body = "" }, HttpStatusCode.BadRequest);
    }

    void Pbi(int id, string state, bool held = false) =>
        fixture.Vendor.Pbis.Add(new Pbi(new PbiRef(FakeFolders.Repository, $"{id}"), $"PBI {id}", state, held, FakeVendor.Me));

    static async Task<JsonElement> Post(HttpClient client, string path, object? body, HttpStatusCode expected)
    {
        using var response = body is null ? await client.PostAsync(path, null) : await client.PostAsJsonAsync(path, body);
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expected, text);
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
    }
}
