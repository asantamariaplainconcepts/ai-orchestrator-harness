using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.SharedFunctionalTests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiOrchestrator.Modules.Pbis.FunctionalTests;

/// <summary>
/// The host with a vendor held in memory and one folder, for the Issue and Proposal tabs (#512).
/// </summary>
public sealed class VendorPagesFixture : ApiServiceFixtureBase
{
    internal PageVendor Vendor { get; } = new();

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
/// #512. The Issue and Proposal tabs through the API: the issue as the vendor has it with the
/// harness's comments marked, its images served only when the issue carries them, a comment written
/// with images arriving at the vendor as files, the OpenSpec change read at the pull request's head
/// in reading order with its spec deltas against master and its tasks counted, and a comment
/// anchored to a line arriving as a review comment on that file and line.
/// </summary>
public sealed class IssueAndProposalTests(VendorPagesFixture fixture) : IClassFixture<VendorPagesFixture>
{
    const string Repo = PageVendor.Repository;
    const string Image = "https://github.com/user-attachments/assets/0f8a3c1e-1111-4222-8333-944455556666";
    const string Evidence = "https://github.com/user-attachments/assets/7b2d9e4f-aaaa-4bbb-8ccc-dddd00001111";

    [Fact]
    public async Task The_issue_is_read_as_the_vendor_has_it_with_the_harness_comments_marked_and_every_image_listed()
    {
        fixture.Vendor.Arrange();
        using var client = fixture.CreateClient();

        var page = await client.GetFromJsonAsync<JsonElement>($"/api/pbi/issue?repository={Repo}&id=7");

        page.GetProperty("body").GetString().ShouldBe($"Do the thing.\n\n![the screen]({Image})");
        page.GetProperty("url").GetString().ShouldBe("https://github.com/owner/repo/issues/7");
        page.GetProperty("labels").EnumerateArray().Select(label => label.GetString()).ShouldBe(["status:proposal-review", "status:holding"]);
        page.GetProperty("parent").GetProperty("id").GetString().ShouldBe("1");
        page.GetProperty("blockedBy").EnumerateArray().Single().GetProperty("closed").GetBoolean().ShouldBeTrue();

        var comments = page.GetProperty("comments").EnumerateArray().ToArray();
        comments.Select(comment => comment.GetProperty("id").GetString()).ShouldBe(["101", "102"], "oldest first");
        comments[0].GetProperty("harness").ValueKind.ShouldBe(JsonValueKind.Null);
        comments[1].GetProperty("harness").GetString().ShouldBe("evidence");

        page.GetProperty("images").EnumerateArray()
            .Select(image => $"{image.GetProperty("url").GetString()} {image.GetProperty("commentId")}")
            .ShouldBe([$"{Image} ", $"{Evidence} 102"]);
    }

    [Fact]
    public async Task The_children_of_an_issue_come_with_their_repository_state_hold_and_whether_closed()
    {
        fixture.Vendor.Arrange();
        using var client = fixture.CreateClient();

        var children = await client.GetFromJsonAsync<JsonElement>($"/api/pbi/children?repository={Repo}&id=1");

        children.EnumerateArray()
            .Select(child => $"{child.GetProperty("repository").GetString()}#{child.GetProperty("id").GetString()} {child.GetProperty("state").GetString()} held={child.GetProperty("held").GetBoolean()} closed={child.GetProperty("closed").GetBoolean()}")
            .ShouldBe([$"{Repo}#7 proposal-review held=True closed=False", "other/repo#8 done held=False closed=True"]);
        (await client.GetFromJsonAsync<JsonElement>($"/api/pbi/children?repository={Repo}&id=7")).GetArrayLength().ShouldBe(0);

        using var failed = await client.GetAsync($"/api/pbi/children?repository={Repo}&id=70");
        failed.StatusCode.ShouldBe(HttpStatusCode.Conflict, "a vendor that does not answer is said, not read as no children");
        using var unknown = await client.GetAsync("/api/pbi/children?repository=nobody/else&id=1");
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_image_is_served_only_when_the_issue_carries_it()
    {
        fixture.Vendor.Arrange();
        using var client = fixture.CreateClient();

        using var served = await client.GetAsync($"/api/pbi/attachment?repository={Repo}&id=7&url={Uri.EscapeDataString(Image)}");
        served.StatusCode.ShouldBe(HttpStatusCode.OK, "an image of the body is served as the comments' are");
        served.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        (await served.Content.ReadAsByteArrayAsync()).ShouldBe(PageVendor.Png);

        var elsewhere = "https://github.com/user-attachments/assets/99999999-0000-4000-8000-000000000000";
        using var refused = await client.GetAsync($"/api/pbi/attachment?repository={Repo}&id=7&url={Uri.EscapeDataString(elsewhere)}");
        refused.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        fixture.Vendor.Assets.ShouldBe([Image], "an address the issue does not carry is never fetched");
    }

    [Fact]
    public async Task A_comment_with_an_image_reaches_the_vendor_as_the_text_and_the_file_dropped()
    {
        fixture.Vendor.Arrange();
        using var client = fixture.CreateClient();

        var written = await Post(
            client,
            "/api/pbi/issue/comment",
            new
            {
                repository = Repo,
                id = "7",
                body = "The header overlaps at 390.",
                attachments = new[] { new { name = "../narrow screen.png", base64 = Convert.ToBase64String(PageVendor.Png) } },
            },
            HttpStatusCode.OK
        );

        written.GetProperty("body").GetString().ShouldStartWith("The header overlaps at 390.");
        var (body, files) = fixture.Vendor.Commented.Single();
        body.ShouldBe("The header overlaps at 390.");
        files.Single().Name.ShouldBe("narrow-screen.png", "a dropped name keeps no directory and no space");
        files.Single().Bytes.ShouldBe(PageVendor.Png);

        await Post(client, "/api/pbi/issue/comment", new { repository = Repo, id = "7", body = "" }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_change_is_read_at_the_head_in_reading_order_with_its_deltas_against_master_and_its_tasks_counted()
    {
        fixture.Vendor.Arrange();
        using var client = fixture.CreateClient();

        var page = await client.GetFromJsonAsync<JsonElement>($"/api/pbi/proposal?repository={Repo}&id=7");

        page.GetProperty("missing").ValueKind.ShouldBe(JsonValueKind.Null);
        page.GetProperty("change").GetString().ShouldBe("loop-states");
        page.GetProperty("head").GetString().ShouldBe(PageVendor.Head);
        var files = page.GetProperty("files").EnumerateArray().ToArray();
        files.Select(file => $"{file.GetProperty("name").GetString()} {file.GetProperty("kind").GetString()}")
            .ShouldBe(["proposal.md proposal", "design.md design", "specs/loop/spec.md spec", "specs/pbis/spec.md spec", "tasks.md tasks", "states.png image"]);

        var loop = files[2];
        loop.GetProperty("master").GetString().ShouldBe("# loop\n\n### Requirement: Old\n");
        var delta = loop.GetProperty("delta");
        (delta.GetProperty("added").GetInt32(), delta.GetProperty("modified").GetInt32(), delta.GetProperty("removed").GetInt32()).ShouldBe((2, 1, 1));
        files[3].GetProperty("master").ValueKind.ShouldBe(JsonValueKind.Null, "pbis is a new capability");
        var tasks = files[4].GetProperty("tasks");
        (tasks.GetProperty("done").GetInt32(), tasks.GetProperty("total").GetInt32()).ShouldBe((1, 3));
        files[5].GetProperty("text").ValueKind.ShouldBe(JsonValueKind.Null);

        page.GetProperty("comments").EnumerateArray().Select(comment => comment.GetProperty("path").GetString())
            .ShouldBe(["openspec/changes/loop-states/proposal.md"], "a comment on code outside the change is Review's");

        using var image = await client.GetAsync($"/api/pbi/proposal/file?repository={Repo}&id=7&path=openspec/changes/loop-states/states.png");
        image.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var outside = await client.GetAsync($"/api/pbi/proposal/file?repository={Repo}&id=7&path=src/secret.cs");
        outside.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// #697. A run another instance recorded plays from its copy on aio/evidence: read from the vendor
    /// at that branch and served as the local recording is. Only a path shaped as a recording is read.
    /// </summary>
    [Fact]
    public async Task A_recording_on_the_evidence_branch_is_served_as_asciicast_and_nothing_else_is_read()
    {
        const string Recording = "8/0123456789abcdef0123456789abcdef/session.cast";
        fixture.Vendor.Evidence[Recording] = "{\"version\": 2, \"width\": 80, \"height\": 6}\n[0.1, \"o\", \"implementing\"]\n";
        using var client = fixture.CreateClient();

        using var played = await client.GetAsync($"/api/pbi/recording?repository={Repo}&path={Uri.EscapeDataString(Recording)}");
        played.StatusCode.ShouldBe(HttpStatusCode.OK);
        played.Content.Headers.ContentType!.MediaType.ShouldBe("application/x-asciicast");
        (await played.Content.ReadAsStringAsync()).ShouldContain("implementing");

        using var missing = await client.GetAsync($"/api/pbi/recording?repository={Repo}&path={Uri.EscapeDataString(Recording.Replace("8/", "9/", StringComparison.Ordinal))}");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var outside = await client.GetAsync($"/api/pbi/recording?repository={Repo}&path={Uri.EscapeDataString("src/secret.cs")}");
        outside.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var climbing = await client.GetAsync($"/api/pbi/recording?repository={Repo}&path={Uri.EscapeDataString("../0123456789abcdef0123456789abcdef/session.cast")}");
        climbing.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_comment_anchored_to_a_line_is_a_review_comment_on_that_file_and_line()
    {
        fixture.Vendor.Arrange();
        using var client = fixture.CreateClient();

        var comment = await Post(
            client,
            "/api/pbi/proposal/comment",
            new { repository = Repo, id = "7", path = "openspec/changes/loop-states/design.md", line = 9, body = "Link the ADR here." },
            HttpStatusCode.OK
        );

        comment.GetProperty("line").GetInt32().ShouldBe(9);
        fixture.Vendor.Anchored.ShouldBe([new ReviewCommentDraft("Link the ADR here.", "openspec/changes/loop-states/design.md", 9)]);

        await Post(
            client,
            "/api/pbi/proposal/comment",
            new { repository = Repo, id = "7", path = "src/secret.cs", line = 1, body = "no" },
            HttpStatusCode.NotFound
        );
        fixture.Vendor.Anchored.Count.ShouldBe(1);
    }

    /// <summary>#726. A pull request with no OpenSpec change reads the issue's ## Plan comment, with its author and date.</summary>
    [Fact]
    public async Task Without_an_OpenSpec_change_the_proposal_is_the_issues_plan_comment()
    {
        fixture.Vendor.Arrange();
        fixture.Vendor.WithoutChange();
        fixture.Vendor.Plan("201", "2026-10-08T12:00:00Z", "## Plan\n\n**What changes.** The card of the warmest day stands out.");
        using var client = fixture.CreateClient();

        var page = await client.GetFromJsonAsync<JsonElement>($"/api/pbi/proposal?repository={Repo}&id=7");

        page.GetProperty("missing").ValueKind.ShouldBe(JsonValueKind.Null);
        page.GetProperty("pullRequestId").GetString().ShouldBe("12");
        page.GetProperty("files").GetArrayLength().ShouldBe(0);
        var plan = page.GetProperty("plan");
        plan.GetProperty("id").GetString().ShouldBe("201");
        plan.GetProperty("author").GetString().ShouldBe("coder");
        plan.GetProperty("createdAt").GetDateTimeOffset().ShouldBe(DateTimeOffset.Parse("2026-10-08T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        plan.GetProperty("body").GetString().ShouldStartWith("## Plan\n\n**What changes.**");
        plan.GetProperty("url").GetString().ShouldBe("https://github.com/owner/repo/issues/7#issuecomment-201");
        page.GetProperty("earlierPlans").GetArrayLength().ShouldBe(0);
    }

    /// <summary>#726. A second pass writes a second ## Plan: the latest is shown and the earlier ones are linked.</summary>
    [Fact]
    public async Task With_two_plan_comments_the_latest_is_shown_and_the_earlier_one_counted()
    {
        fixture.Vendor.Arrange();
        fixture.Vendor.WithoutChange();
        fixture.Vendor.Plan("201", "2026-10-08T12:00:00Z", "## Plan\n\nFirst pass.");
        fixture.Vendor.Plan("203", "2026-10-09T12:00:00Z", "## Plan\n\nSecond pass, after the review.");
        fixture.Vendor.Plan("202", "2026-10-08T13:00:00Z", "A plan of my own, said in passing.\n\n## Plan\n\nNot one.");
        using var client = fixture.CreateClient();

        var page = await client.GetFromJsonAsync<JsonElement>($"/api/pbi/proposal?repository={Repo}&id=7");

        page.GetProperty("plan").GetProperty("id").GetString().ShouldBe("203");
        page.GetProperty("earlierPlans").EnumerateArray()
            .Select(plan => $"{plan.GetProperty("id").GetString()} {plan.GetProperty("url").GetString()}")
            .ShouldBe(["201 https://github.com/owner/repo/issues/7#issuecomment-201"], "only a comment that starts with the heading is a plan");
    }

    /// <summary>#726. Neither an OpenSpec change nor a ## Plan comment: the answer says both are missing.</summary>
    [Fact]
    public async Task Without_a_change_or_a_plan_comment_the_proposal_says_both_are_missing()
    {
        fixture.Vendor.Arrange();
        fixture.Vendor.WithoutChange();
        using var client = fixture.CreateClient();

        var page = await client.GetFromJsonAsync<JsonElement>($"/api/pbi/proposal?repository={Repo}&id=7");

        page.GetProperty("missing").GetString().ShouldBe("#7 has no OpenSpec change on pull request #12 and no ## Plan comment yet. /aio:propose writes one of the two.");
        page.GetProperty("plan").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    /// <summary>#726. An OpenSpec change wins over a ## Plan comment, and is read as before.</summary>
    [Fact]
    public async Task An_OpenSpec_change_is_shown_even_when_the_issue_has_a_plan_comment()
    {
        fixture.Vendor.Arrange();
        fixture.Vendor.Plan("201", "2026-10-08T12:00:00Z", "## Plan\n\nIn the issue.");
        using var client = fixture.CreateClient();

        var page = await client.GetFromJsonAsync<JsonElement>($"/api/pbi/proposal?repository={Repo}&id=7");

        page.GetProperty("change").GetString().ShouldBe("loop-states");
        page.GetProperty("files").GetArrayLength().ShouldBe(6);
        page.GetProperty("plan").ValueKind.ShouldBe(JsonValueKind.Null);
        page.GetProperty("earlierPlans").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Without_a_pull_request_the_proposal_says_why_there_is_nothing_to_read()
    {
        fixture.Vendor.Arrange();
        fixture.Vendor.Pull = null;
        using var client = fixture.CreateClient();

        var page = await client.GetFromJsonAsync<JsonElement>($"/api/pbi/proposal?repository={Repo}&id=7");

        page.GetProperty("missing").GetString().ShouldNotBeNull().ShouldContain("no pull request yet");
        page.GetProperty("files").GetArrayLength().ShouldBe(0);
    }

    static async Task<JsonElement> Post(HttpClient client, string path, object body, HttpStatusCode expected)
    {
        using var response = await client.PostAsJsonAsync(path, body);
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expected, text);
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
    }
}

/// <summary>One issue with a pull request that carries an OpenSpec change, held in memory, and what was written to it.</summary>
sealed class PageVendor : VendorStub, IPbiProvider
{
    public const string Repository = "owner/repo";
    public const string Head = "4f1c2b7e9a0d3c5b8e6f7a1b2c3d4e5f6a7b8c9d";
    const string Root = "openspec/changes/loop-states/";

    public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    public PbiThread? Thread { get; set; }

    public PullRequestInfo? Pull { get; set; }

    public Dictionary<string, string> Files { get; } = [];

    public List<string> Assets { get; } = [];

    public List<(string Body, List<(string Name, byte[] Bytes)> Files)> Commented { get; } = [];

    public List<ReviewCommentDraft> Anchored { get; } = [];

    public void Arrange()
    {
        Assets.Clear();
        Commented.Clear();
        Anchored.Clear();
        var seven = new PbiRef(Repository, "7");
        Thread = new PbiThread(
            new Pbi(seven, "Loop as states", "proposal-review", true, null),
            new Uri("https://github.com/owner/repo/issues/7"),
            "dev",
            DateTimeOffset.Parse("2026-10-08T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            $"Do the thing.\n\n![the screen]({IssueAndProposalTestsImage})",
            ["status:proposal-review", "status:holding"],
            [],
            new LinkedPbi(new PbiRef(Repository, "1"), "The epic", new Uri("https://github.com/owner/repo/issues/1"), false),
            [new LinkedPbi(new PbiRef(Repository, "3"), "Before", new Uri("https://github.com/owner/repo/issues/3"), true)],
            [
                new PbiComment("102", "aio-bot", DateTimeOffset.Parse("2026-10-08T11:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
                    $"Evidence for #7.\n\n![wide-light.png]({IssueAndProposalTestsEvidence})\n\n<!-- aio:evidence {{\"schemaVersion\": 1}} -->"),
                new PbiComment("101", "dev", DateTimeOffset.Parse("2026-10-08T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture), "A question."),
            ]
        );
        Thread = Thread with { Comments = [.. Thread.Comments.OrderBy(comment => comment.CreatedAt)] };
        Pull = new PullRequestInfo("12", new Uri("https://github.com/owner/repo/pull/12"), true, false, false);
        Files.Clear();
        Files[Root + "tasks.md"] = "## 1. Do\n\n- [x] 1.1 One\n- [ ] 1.2 Two\n- [ ] 1.3 Three\n";
        Files[Root + "proposal.md"] = "# Loop as states\n\n## Why\n\nBecause.\n";
        Files[Root + "specs/pbis/spec.md"] = "## ADDED Requirements\n\n### Requirement: New\n";
        Files[Root + "design.md"] = "# Design\n";
        Files[Root + "specs/loop/spec.md"] =
            "## ADDED Requirements\n\n### Requirement: A\n\n### Requirement: B\n\n## MODIFIED Requirements\n\n### Requirement: Old\n\n## REMOVED Requirements\n\n### Requirement: Gone\n";
        Files[Root + "states.png"] = "png";
        Files["src/secret.cs"] = "class Secret {}";
    }

    /// <summary>The pull request keeps its code and loses its OpenSpec change.</summary>
    public void WithoutChange()
    {
        foreach (var path in Files.Keys.Where(path => path.StartsWith(Root, StringComparison.Ordinal)).ToArray())
        {
            Files.Remove(path);
        }
    }

    /// <summary>One more comment on issue 7, by the coder, kept oldest first.</summary>
    public void Plan(string id, string createdAt, string body) =>
        Thread = Thread! with
        {
            Comments =
            [
                .. Thread.Comments
                    .Append(new PbiComment(id, "coder", DateTimeOffset.Parse(createdAt, System.Globalization.CultureInfo.InvariantCulture), body)
                    {
                        Url = new Uri($"https://github.com/owner/repo/issues/7#issuecomment-{id}"),
                    })
                    .OrderBy(comment => comment.CreatedAt),
            ],
        };

    const string IssueAndProposalTestsImage = "https://github.com/user-attachments/assets/0f8a3c1e-1111-4222-8333-944455556666";
    const string IssueAndProposalTestsEvidence = "https://github.com/user-attachments/assets/7b2d9e4f-aaaa-4bbb-8ccc-dddd00001111";

    public override Task<PbiThread?> ReadThreadAsync(PbiRef pbi, CancellationToken ct) => Task.FromResult(pbi.Id == "7" ? Thread : null);

    /// <summary>#611. Issue 1, the epic, has two sub-issues; asking for 70's children reads as the vendor failing.</summary>
    public Task<IReadOnlyList<Pbi>> ListChildrenAsync(PbiRef pbi, CancellationToken ct) =>
        pbi.Id switch
        {
            "1" => Task.FromResult<IReadOnlyList<Pbi>>(
                [
                    new Pbi(new PbiRef(Repository, "7"), "Loop as states", "proposal-review", true, null),
                    new Pbi(new PbiRef("other/repo", "8"), "Probe", "done", false, null, Closed: true),
                ]
            ),
            "70" => throw new PbiProviderException("gh: HTTP 502"),
            _ => Task.FromResult<IReadOnlyList<Pbi>>([]),
        };

    public override Task<byte[]?> ReadAttachmentAsync(Uri address, CancellationToken ct)
    {
        Assets.Add(address.AbsoluteUri);
        return Task.FromResult<byte[]?>(Png);
    }

    public override async Task<Written<PbiComment>> CommentAsync(PbiRef pbi, string body, IReadOnlyList<string> attachments, CancellationToken ct)
    {
        var files = new List<(string, byte[])>();
        foreach (var path in attachments)
        {
            files.Add((Path.GetFileName(path), await File.ReadAllBytesAsync(path, ct)));
        }
        Commented.Add((body, files));
        var linked = string.Concat(attachments.Select(path => $"\n\n![{Path.GetFileName(path)}](https://github.com/user-attachments/assets/{Guid.NewGuid()})"));
        return new(new PbiComment("103", "dev", DateTimeOffset.UtcNow, body + linked), null);
    }

    public override Task<PullRequestChange?> GetPullRequestChangeAsync(PbiRef pbi, CancellationToken ct) =>
        Task.FromResult(
            Pull is null
                ? null
                : new PullRequestChange(
                    Pull,
                    "main",
                    "0000000",
                    "change/7",
                    Head,
                    1,
                    Files.Count * 3,
                    0,
                    [.. Files.Keys.Select(path => new ChangedFile(path, null, "added", 3, 0, null))],
                    Files.Count,
                    [
                        new ReviewThread("T9", Root + "proposal.md", 5, false, false, [new ReviewComment("9", Root + "proposal.md", 5, "dev", DateTimeOffset.UtcNow, "Why only this?")]),
                        new ReviewThread("T8", "src/Engine.cs", 40, false, false, [new ReviewComment("8", "src/Engine.cs", 40, "dev", DateTimeOffset.UtcNow, "Code.")]),
                    ]
                )
        );

    public override async Task<IReadOnlyList<byte[]?>> ReadFilesAsync(string repository, IReadOnlyList<RepositoryFile> files, CancellationToken ct) =>
        await Task.WhenAll(files.Select(file => ReadFileAsync(file.Reference, file.Path)));

    /// <summary>What the repository's aio/evidence branch holds, by path (#697).</summary>
    public Dictionary<string, string> Evidence { get; } = [];

    Task<byte[]?> ReadFileAsync(string? reference, string path)
    {
        if (reference == "aio/evidence")
        {
            return Task.FromResult(Evidence.TryGetValue(path, out var cast) ? Encoding.UTF8.GetBytes(cast) : null);
        }

        if (reference is null)
        {
            return Task.FromResult(path == "openspec/specs/loop/spec.md" ? Encoding.UTF8.GetBytes("# loop\n\n### Requirement: Old\n") : null);
        }

        reference.ShouldBe(Head, "the change is read at the pull request's head");
        return Task.FromResult(Files.TryGetValue(path, out var text) ? Encoding.UTF8.GetBytes(text) : null);
    }

    public override Task<PullRequestInfo?> GetPullRequestAsync(PbiRef pbi, CancellationToken ct) => Task.FromResult(Pull);

    public override Task<Written<ReviewComment>> AddReviewCommentAsync(PbiRef pbi, ReviewCommentDraft draft, CancellationToken ct)
    {
        Anchored.Add(draft);
        return Task.FromResult(new Written<ReviewComment>(new ReviewComment("10", draft.Path!, draft.Line, "dev", DateTimeOffset.UtcNow, draft.Body), null));
    }
}
