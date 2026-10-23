using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Infrastructure;
using AiOrchestrator.Infrastructure.PbiProviders.GitHub;

namespace AiOrchestrator.InfrastructureTests.PbiProviders;

/// <summary>
/// #512. What the Issue and Proposal tabs ask <see cref="GitHubPbiProvider"/>, against a <c>gh</c>
/// that answers from memory: the issue with its parent, blocking issues and comments; a comment with
/// images uploaded by <c>--attach</c> and read back; a file of a branch read raw; and a review that
/// only comments, read back.
/// </summary>
public sealed class GitHubPbiProviderPagesTests
{
    readonly FakeGh _gh = new();
    readonly List<IReadOnlyList<string>> _byteCalls = [];
    readonly GitHubPbiProvider _provider;
    static readonly PbiRef Seven = new(FakeGh.Repository, "7");
    static readonly CancellationToken None = CancellationToken.None;
    static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0];

    public GitHubPbiProviderPagesTests()
    {
        _provider = new GitHubPbiProvider(_gh.Run)
        {
            Bytes = (arguments, _) =>
            {
                _byteCalls.Add(arguments);
                return Task.FromResult<byte[]?>(Png);
            },
        };
    }

    [Fact]
    public async Task The_issue_is_read_with_its_parent_its_blockers_and_its_comments_in_order()
    {
        _gh.Intercept = call =>
            call is ["issue", "view", "7", "--repo", FakeGh.Repository, "--json", _]
                ? FakeGh.Ok(new
                {
                    number = 7,
                    title = "Loop as states",
                    url = "https://github.com/owner/repo/issues/7",
                    body = "Do it.",
                    author = new { login = "dev" },
                    createdAt = "2026-10-08T09:00:00Z",
                    state = "OPEN",
                    labels = new[] { new { name = "status:proposal-review" }, new { name = "status:holding" } },
                    assignees = new[] { new { login = "dev" } },
                    parent = new { number = 1, title = "Epic", url = "https://github.com/owner/repo/issues/1", state = "OPEN" },
                    blockedBy = new { nodes = new[] { new { number = 3, title = "Before", url = "https://github.com/other/repo/issues/3", state = "CLOSED" } } },
                    comments = new[]
                    {
                        new { author = new { login = "aio-bot" }, body = "Later", createdAt = "2026-10-08T11:00:00Z", url = "https://github.com/owner/repo/issues/7#issuecomment-502" },
                        new { author = new { login = "dev" }, body = "Earlier", createdAt = "2026-10-08T10:00:00Z", url = "https://github.com/owner/repo/issues/7#issuecomment-501" },
                    },
                })
                : null;

        var thread = (await _provider.ReadThreadAsync(Seven, None)).ShouldNotBeNull();

        thread.Pbi.ShouldBe(new Pbi(Seven, "Loop as states", "proposal-review", true, "dev"));
        thread.Labels.ShouldBe(["status:proposal-review", "status:holding"]);
        thread.Parent.ShouldBe(new LinkedPbi(new PbiRef(FakeGh.Repository, "1"), "Epic", new Uri("https://github.com/owner/repo/issues/1"), false));
        thread.BlockedBy.Single().Ref.ShouldBe(new PbiRef("other/repo", "3"));
        thread.BlockedBy.Single().Closed.ShouldBeTrue();
        thread.Comments.Select(comment => (comment.Id, comment.Body)).ShouldBe([("501", "Earlier"), ("502", "Later")]);
        thread.Comments[0].Url.ShouldBe(new Uri("https://github.com/owner/repo/issues/7#issuecomment-501"));
    }

    [Fact]
    public async Task The_sub_issues_are_read_from_the_REST_endpoint_each_with_its_own_repository_state_and_hold()
    {
        _gh.Intercept = call =>
            call is ["api", "repos/owner/repo/issues/7/sub_issues?per_page=100"]
                ? FakeGh.Ok(new object[]
                {
                    new
                    {
                        number = 8,
                        title = "Mermaid",
                        state = "closed",
                        repository_url = "https://api.github.com/repos/owner/repo",
                        labels = new[] { new { name = "status:done" } },
                        assignees = Array.Empty<object>(),
                    },
                    new
                    {
                        number = 3,
                        title = "Probe",
                        state = "open",
                        repository_url = "https://api.github.com/repos/other/repo",
                        labels = new[] { new { name = "status:in-progress" }, new { name = "status:holding" } },
                        assignees = new[] { new { login = "dev" } },
                    },
                })
                : null;

        var children = await _provider.ListChildrenAsync(Seven, None);

        children.ShouldBe(
        [
            new Pbi(new PbiRef(FakeGh.Repository, "8"), "Mermaid", "done", false, null, Closed: true),
            new Pbi(new PbiRef("other/repo", "3"), "Probe", "in-progress", true, "dev"),
        ]);
    }

    [Fact]
    public async Task An_issue_the_vendor_does_not_have_has_no_sub_issues()
    {
        _gh.Intercept = call => call is ["api", ..] ? FakeGh.Fail("gh: Not Found (HTTP 404)") : null;

        (await _provider.ListChildrenAsync(new PbiRef(FakeGh.Repository, "70"), None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task An_issue_the_vendor_does_not_have_is_null()
    {
        _gh.Intercept = call =>
            call is ["issue", "view", ..]
                ? FakeGh.Fail("GraphQL: Could not resolve to an issue or pull request with the number of 70. (repository.issue)")
                : null;

        (await _provider.ReadThreadAsync(new PbiRef(FakeGh.Repository, "70"), None)).ShouldBeNull();
    }

    [Fact]
    public async Task A_comment_with_images_is_sent_with_attach_and_read_back_with_them_linked()
    {
        var image = Path.Combine(Directory.CreateTempSubdirectory("aio-test-").FullName, "narrow.png");
        await File.WriteAllBytesAsync(image, Png);
        string? sentBody = null;
        _gh.Intercept = call =>
        {
            if (call is ["issue", "comment", "7", "--repo", FakeGh.Repository, "--body-file", var file, "--attach", var attached])
            {
                sentBody = File.ReadAllText(file);
                attached.ShouldBe(image);
                return new ProcessOutcome(0, "https://github.com/owner/repo/issues/7#issuecomment-9001\n", "");
            }
            return call is ["api", "repos/owner/repo/issues/comments/9001"]
                ? FakeGh.Ok(new
                {
                    id = 9001,
                    body = "Overlaps.\n\n![narrow.png](https://github.com/user-attachments/assets/1d6c1d3a-0000-4000-8000-000000000001)",
                    user = new { login = "dev" },
                    created_at = "2026-10-08T12:00:00Z",
                    html_url = "https://github.com/owner/repo/issues/7#issuecomment-9001",
                })
                : null;
        };

        var written = await _provider.CommentAsync(Seven, "Overlaps.", [image], None);

        written.Matches.ShouldBeTrue(written.Mismatch);
        sentBody.ShouldBe("Overlaps.");
        written.Observed.ShouldNotBeNull().Author.ShouldBe("dev");
        written.Observed.Url.ShouldBe(new Uri("https://github.com/owner/repo/issues/7#issuecomment-9001"));
    }

    [Fact]
    public async Task A_comment_that_reads_back_without_its_image_is_a_mismatch()
    {
        var image = Path.Combine(Directory.CreateTempSubdirectory("aio-test-").FullName, "narrow.png");
        await File.WriteAllBytesAsync(image, Png);
        _gh.Intercept = call =>
            call switch
            {
                ["issue", "comment", ..] => new ProcessOutcome(0, "https://github.com/owner/repo/issues/7#issuecomment-9002\n", ""),
                ["api", "repos/owner/repo/issues/comments/9002"] => FakeGh.Ok(new { id = 9002, body = "Overlaps." }),
                _ => null,
            };

        var written = await _provider.CommentAsync(Seven, "Overlaps.", [image], None);

        written.Mismatch.ShouldBe("The comment 9002 on owner/repo#7 reads back with 0 of 1 images.");
    }

    [Fact]
    public async Task A_file_is_read_raw_at_the_reference_named_or_the_default_branch()
    {
        // One file by itself is read raw, in one call, whatever it is: an image, or one too large for GraphQL.
        await _provider.ReadFilesAsync(FakeGh.Repository, [new("change/7", "openspec/changes/a b/states.svg")], None);
        await _provider.ReadFilesAsync(FakeGh.Repository, [new(null, "openspec/specs/loop/spec.md")], None);

        _gh.Calls.ShouldBeEmpty("no GraphQL for one file");
        _byteCalls[0].ShouldBe(["api", "repos/owner/repo/contents/openspec/changes/a%20b/states.svg?ref=change%2F7", "-H", "Accept: application/vnd.github.raw"]);
        _byteCalls[1][1].ShouldBe("repos/owner/repo/contents/openspec/specs/loop/spec.md");
        await Should.ThrowAsync<ArgumentException>(() => _provider.ReadFilesAsync(FakeGh.Repository, [new(null, "../secret")], None));
    }

    [Fact]
    public async Task A_review_that_only_comments_is_submitted_as_COMMENT_and_read_back()
    {
        _gh.Issues[7] = new FakeGh.Issue("Loop as states");
        _gh.Issues[7].ClosedBy.Add((FakeGh.Repository, 12));
        _gh.Pulls[12] = new FakeGh.Pull { Head = "4f1c2b7" };
        _gh.Intercept = call =>
            call switch
            {
                ["api", "repos/owner/repo/pulls/12/reviews", "--method", "POST", "-f", "event=COMMENT", "-f", "commit_id=4f1c2b7", "-f", "body=Split it."] =>
                    FakeGh.Ok(new { id = 77 }),
                ["api", "repos/owner/repo/pulls/12/reviews/77"] => FakeGh.Ok(new { id = 77, state = "COMMENTED", body = "Split it.", user = new { login = "dev" } }),
                _ => null,
            };

        var written = await _provider.SubmitReviewAsync(Seven, ReviewVerdict.Comment, "Split it.", None);

        written.Matches.ShouldBeTrue(written.Mismatch);
        written.Observed.ShouldBe(new SubmittedReview("77", ReviewVerdict.Comment, "dev", "Split it."));
        await Should.ThrowAsync<ArgumentException>(() => _provider.SubmitReviewAsync(Seven, ReviewVerdict.Comment, " ", None));
    }
}
