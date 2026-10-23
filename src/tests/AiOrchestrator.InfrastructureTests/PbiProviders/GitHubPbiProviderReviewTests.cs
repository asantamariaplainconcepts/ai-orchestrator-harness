using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Infrastructure;
using AiOrchestrator.Infrastructure.PbiProviders.GitHub;

namespace AiOrchestrator.InfrastructureTests.PbiProviders;

/// <summary>
/// #513. The Review tab's half of <see cref="GitHubPbiProvider"/> against a <c>gh</c> that answers
/// from memory: the change and its files, the threads from GraphQL, a review written on the head and
/// read back, GitHub's refusal of an approval by the pull request's own author, a thread of another
/// pull request left alone, the checks of the head, and a credential that only reaches GitHub.
/// </summary>
public sealed class GitHubPbiProviderReviewTests
{
    readonly FakeGh _gh = new();
    readonly GitHubPbiProvider _provider;
    static readonly PbiRef Seven = new(FakeGh.Repository, "7");
    static readonly CancellationToken None = CancellationToken.None;

    public GitHubPbiProviderReviewTests()
    {
        _provider = new GitHubPbiProvider(_gh.Run);
        _gh.Issues[7] = new FakeGh.Issue("Revisar el diff");
        _gh.Issues[7].ClosedBy.Add((FakeGh.Repository, 20));
        _gh.Pulls[20] = new FakeGh.Pull { Head = "9b01a3c0000000000000000000000000000000ff", ReviewDecision = "REVIEW_REQUIRED" };
    }

    [Fact]
    public async Task The_change_names_its_base_and_head_and_every_file_with_its_patch()
    {
        var pull = _gh.Pulls[20];
        (pull.BaseRef, pull.BaseSha, pull.Branch, pull.Head) = ("main", "4f9c2e1", "change/7", "9b01a3c");
        (pull.Commits, pull.Additions, pull.Deletions) = (41, 1420, 212);
        pull.Files.Add(new FakeGh.File("src/Port.cs", "modified", 9, 3, "@@ -1,2 +1,3 @@\n a\n+b\n c"));
        pull.Files.Add(new FakeGh.File("docs/new.png", "renamed", 0, 0, null, "docs/old.png"));

        var change = (await _provider.ReadPullRequestReviewAsync(Seven, None)).ShouldNotBeNull().Change;

        change.PullRequest.Id.ShouldBe("20");
        (change.BaseRef, change.BaseSha, change.HeadRef, change.HeadSha).ShouldBe(("main", "4f9c2e1", "change/7", "9b01a3c"));
        (change.Commits, change.Additions, change.Deletions, change.ChangedFiles).ShouldBe((41, 1420, 212, 2));
        change.Files.ShouldBe([
            new ChangedFile("src/Port.cs", null, "modified", 9, 3, "@@ -1,2 +1,3 @@\n a\n+b\n c"),
            new ChangedFile("docs/new.png", "docs/old.png", "renamed", 0, 0, null),
        ]);

        // The Proposal tab's read is the same change without the patches and the previous paths (#621).
        var light = (await _provider.GetPullRequestChangeAsync(Seven, None)).ShouldNotBeNull();
        light.Files.ShouldBe([.. change.Files.Select(file => file with { Patch = null, PreviousPath = null })]);
    }

    [Fact]
    public async Task Threads_are_read_from_GraphQL_with_their_line_and_their_comments_in_order()
    {
        _gh.Pulls[20].Threads.AddRange([
            new
            {
                id = "PRRT_1",
                isResolved = false,
                isOutdated = false,
                path = "src/Port.cs",
                line = 12,
                comments = new
                {
                    nodes = new object[]
                    {
                        new { databaseId = 901L, author = new { login = "dev" }, body = "Does state stay a string?", createdAt = "2026-10-08T10:00:00Z" },
                        new { databaseId = 902L, author = new { login = "reviewer" }, body = "Yes.", createdAt = "2026-10-08T11:00:00Z" },
                    },
                },
            },
            new
            {
                id = "PRRT_2",
                isResolved = true,
                isOutdated = true,
                path = "src/Old.cs",
                line = (int?)null,
                comments = new { nodes = Array.Empty<object>() },
            },
        ]);

        var change = (await _provider.GetPullRequestChangeAsync(Seven, None)).ShouldNotBeNull();
        var threads = change.Threads;

        threads.Count.ShouldBe(2);
        change.Comments.Select(comment => $"{comment.Path}:{comment.Line} {comment.Id}").ShouldBe(["src/Port.cs:12 901", "src/Port.cs:12 902"], "what the Proposal tab lists");
        threads[0].ShouldSatisfyAllConditions(
            thread => thread.Id.ShouldBe("PRRT_1"),
            thread => thread.Line.ShouldBe(12),
            thread => thread.IsResolved.ShouldBeFalse(),
            thread => thread.Comments.Select(comment => $"{comment.Id} {comment.Author}: {comment.Body}").ShouldBe(["901 dev: Does state stay a string?", "902 reviewer: Yes."])
        );
        threads[1].ShouldSatisfyAllConditions(
            thread => thread.Line.ShouldBeNull(),
            thread => thread.IsResolved.ShouldBeTrue(),
            thread => thread.IsOutdated.ShouldBeTrue()
        );
    }

    /// <summary>#651. What <c>aio pr view</c> reads besides the threads: the title, the body, the comments and the reviews, in one call.</summary>
    [Fact]
    public async Task The_conversation_is_the_title_the_body_the_comments_and_every_review_from_one_GraphQL_call()
    {
        var pull = _gh.Pulls[20];
        (pull.Title, pull.Body, pull.State) = ("El port lee el estado", "Cierra #7.", "MERGED");
        pull.Conversation.Add(new { databaseId = 3_000_000_001L, author = new { login = "dev" }, createdAt = "2026-10-08T10:00:00Z", body = "Una pregunta.", url = "https://github.com/octo/harness/pull/20#issuecomment-3000000001" });
        pull.Submitted.AddRange([
            new { databaseId = 5_413_916_347L, author = new { login = "reviewer" }, state = "COMMENTED", body = "1. src/Port.cs:2", submittedAt = "2026-10-08T11:00:00Z" },
            new { databaseId = 5_413_916_348L, author = (object?)null, state = "APPROVED", body = "", submittedAt = "2026-10-08T12:00:00Z" },
            new { databaseId = 5_413_916_349L, author = (object?)new { login = "reviewer" }, state = "PENDING", body = "", submittedAt = (string?)null },
        ]);
        var review = (await _provider.ReadPullRequestReviewAsync(Seven, None)).ShouldNotBeNull();
        var read = review.Conversation;

        _gh.Calls.Count(call => call is ["api", "graphql", ..]).ShouldBe(1, "the conversation rides the review's one GraphQL call");
        (review.Change.PullRequest.Id, read.Title, read.State, read.Author, read.Body).ShouldBe(("20", "El port lee el estado", "merged", "dev", "Cierra #7."));
        var comment = read.Comments.ShouldHaveSingleItem();
        (comment.Id, comment.Author, comment.Body).ShouldBe(("3000000001", "dev", "Una pregunta."));
        comment.Url.ShouldBe(new Uri("https://github.com/octo/harness/pull/20#issuecomment-3000000001"));
        read.Reviews.Select(review => (review.Id, review.Verdict, review.Author, review.Body)).ShouldBe([
            ("5413916347", ReviewVerdict.Comment, "reviewer", "1. src/Port.cs:2"),
            ("5413916348", ReviewVerdict.Approve, null, ""),
            ("5413916349", null, "reviewer", ""),
        ]);
        read.Reviews[2].At.ShouldBe(default);

        _gh.Issues[7].ClosedBy.Clear();
        (await _provider.ReadPullRequestReviewAsync(Seven, None)).ShouldBeNull();
    }

    [Fact]
    public async Task An_approval_is_written_on_the_head_and_read_back()
    {
        _gh.Intercept = call => call switch
        {
            ["api", "repos/owner/repo/pulls/20/reviews", "--method", "POST", ..] => FakeGh.Ok(new { id = 555L }),
            ["api", "repos/owner/repo/pulls/20/reviews/555"] => FakeGh.Ok(new { id = 555L, state = "APPROVED", body = "", user = new { login = "aio-bot" } }),
            _ => null,
        };

        var written = await _provider.SubmitReviewAsync(Seven, ReviewVerdict.Approve, "", None);

        written.Matches.ShouldBeTrue(written.Mismatch);
        written.Observed.ShouldBe(new SubmittedReview("555", ReviewVerdict.Approve, "aio-bot", ""));
        _gh.Calls.ShouldContain(call => call.SequenceEqual(new[]
        {
            "api", "repos/owner/repo/pulls/20/reviews", "--method", "POST",
            "-f", "event=APPROVE", "-f", "commit_id=9b01a3c0000000000000000000000000000000ff",
        }));
    }

    /// <summary>What gh 2.99.0 printed against the sandbox on 9 October 2026: the body on stdout, its own generic line on stderr.</summary>
    static ProcessOutcome OwnPullRequest(string verdict) =>
        new(1, $$"""{"message":"Unprocessable Entity","errors":["Review Can not {{verdict}} your own pull request"],"status":"422"}""", "gh: Unprocessable Entity (HTTP 422)");

    [Fact]
    public async Task GitHub_refusing_an_approval_by_the_author_writes_it_as_a_comment_and_says_why()
    {
        _gh.Intercept = call => call switch
        {
            ["api", "repos/owner/repo/pulls/20/reviews", "--method", "POST", "-f", "event=APPROVE", ..] => OwnPullRequest("approve"),
            ["api", "repos/owner/repo/pulls/20/reviews", "--method", "POST", "-f", "event=COMMENT", ..] => FakeGh.Ok(new { id = 556L }),
            ["api", "repos/owner/repo/pulls/20/reviews/556"] => FakeGh.Ok(new { id = 556L, state = "COMMENTED", body = "Approval.", user = new { login = "aio-bot" } }),
            _ => null,
        };

        var written = await _provider.SubmitReviewAsync(Seven, ReviewVerdict.Approve, "", None);

        written.Matches.ShouldBeTrue(written.Mismatch);
        written.Observed.ShouldBe(new SubmittedReview("556", ReviewVerdict.Comment, "aio-bot", "Approval.", "Review Can not approve your own pull request"));
        _gh.Calls.ShouldContain(call => call.SequenceEqual(new[]
        {
            "api", "repos/owner/repo/pulls/20/reviews", "--method", "POST",
            "-f", "event=COMMENT", "-f", "commit_id=9b01a3c0000000000000000000000000000000ff", "-f", "body=Approval.",
        }));
    }

    [Fact]
    public async Task A_request_for_changes_by_the_author_goes_on_as_a_comment_that_carries_its_body()
    {
        _gh.Intercept = call => call switch
        {
            ["api", "repos/owner/repo/pulls/20/reviews", "--method", "POST", "-f", "event=REQUEST_CHANGES", ..] => OwnPullRequest("request changes on"),
            ["api", "repos/owner/repo/pulls/20/reviews", "--method", "POST", "-f", "event=COMMENT", ..] => FakeGh.Ok(new { id = 557L }),
            ["api", "repos/owner/repo/pulls/20/reviews/557"] => FakeGh.Ok(new { id = 557L, state = "COMMENTED", body = "Request for changes:\n\nRename it.", user = new { login = "aio-bot" } }),
            _ => null,
        };

        var written = await _provider.SubmitReviewAsync(Seven, ReviewVerdict.RequestChanges, "Rename it.", None);

        written.Matches.ShouldBeTrue(written.Mismatch);
        written.Observed.ShouldNotBeNull().AsComment.ShouldBe("Review Can not request changes on your own pull request");
    }

    [Fact]
    public async Task A_refusal_for_another_reason_is_the_mismatch_in_GitHubs_words()
    {
        _gh.Intercept = call => call is ["api", "repos/owner/repo/pulls/20/reviews", ..]
            ? new ProcessOutcome(1, """{"message":"Unprocessable Entity","errors":["Pull request is locked"],"status":"422"}""", "gh: Unprocessable Entity (HTTP 422)")
            : null;

        var written = await _provider.SubmitReviewAsync(Seven, ReviewVerdict.Approve, "", None);

        written.Observed.ShouldBeNull();
        written.Mismatch.ShouldBe("The approval of pull request 20 was not written: Pull request is locked");
        _gh.Calls.Count(call => call is ["api", "repos/owner/repo/pulls/20/reviews", ..]).ShouldBe(1);
    }

    [Fact]
    public async Task A_comment_in_place_of_a_refused_approval_that_is_refused_too_says_both()
    {
        _gh.Intercept = call => call switch
        {
            ["api", "repos/owner/repo/pulls/20/reviews", "--method", "POST", "-f", "event=APPROVE", ..] => OwnPullRequest("approve"),
            ["api", "repos/owner/repo/pulls/20/reviews", "--method", "POST", "-f", "event=COMMENT", ..] => FakeGh.Fail("gh: HTTP 502"),
            _ => null,
        };

        var written = await _provider.SubmitReviewAsync(Seven, ReviewVerdict.Approve, "", None);

        written.Observed.ShouldBeNull();
        written.Mismatch.ShouldBe(
            "The approval of pull request 20 was refused (Review Can not approve your own pull request), and the comment in its place was not written: gh: HTTP 502"
        );
    }

    [Fact]
    public async Task A_request_for_changes_needs_a_body()
    {
        await Should.ThrowAsync<ArgumentException>(() => _provider.SubmitReviewAsync(Seven, ReviewVerdict.RequestChanges, " ", None));
    }

    [Fact]
    public async Task A_reply_goes_under_the_thread_and_is_read_back()
    {
        _gh.Intercept = call => call switch
        {
            ["api", "repos/owner/repo/pulls/20/comments/901/replies", "--method", "POST", "-f", "body=Agreed."] => FakeGh.Ok(new { id = 903L }),
            ["api", "repos/owner/repo/pulls/comments/903"] => FakeGh.Ok(new
            {
                id = 903L,
                path = "src/Port.cs",
                line = 12,
                body = "Agreed.",
                user = new { login = "aio-bot" },
                created_at = "2026-10-08T12:00:00Z",
            }),
            _ => null,
        };

        var written = await _provider.AddReviewCommentAsync(Seven, new ReviewCommentDraft("Agreed.", InReplyTo: "901"), None);

        written.Matches.ShouldBeTrue(written.Mismatch);
        written.Observed!.ShouldSatisfyAllConditions(
            reply => reply.Id.ShouldBe("903"),
            reply => reply.Path.ShouldBe("src/Port.cs"),
            reply => reply.Line.ShouldBe(12)
        );
    }

    [Fact]
    public async Task A_thread_of_another_pull_request_is_not_resolved()
    {
        _gh.Intercept = call => call is ["api", "graphql", "-f", var query, "-f", "id=PRRT_9"] && query.Contains("node(id", StringComparison.Ordinal)
            ? FakeGh.Ok(new
            {
                data = new
                {
                    node = new
                    {
                        id = "PRRT_9",
                        isResolved = false,
                        isOutdated = false,
                        path = "src/A.cs",
                        line = 1,
                        comments = new { nodes = Array.Empty<object>() },
                        pullRequest = new { number = 99, repository = new { nameWithOwner = "owner/repo" } },
                    },
                },
            })
            : null;

        var written = await _provider.ResolveReviewThreadAsync(Seven, "PRRT_9", None);

        written.Observed.ShouldBeNull();
        written.Mismatch.ShouldBe("Thread PRRT_9 is not on pull request 20 of owner/repo, so it was not resolved.");
        _gh.Calls.ShouldNotContain(call => call.Any(argument => argument.Contains("resolveReviewThread", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Resolving_a_thread_reads_it_back_resolved()
    {
        var resolved = false;
        _gh.Intercept = call =>
        {
            if (call is ["api", "graphql", "-f", var query, "-f", "id=PRRT_1"])
            {
                if (query.Contains("resolveReviewThread", StringComparison.Ordinal))
                {
                    resolved = true;
                    return FakeGh.Ok(new { data = new { resolveReviewThread = new { thread = new { id = "PRRT_1" } } } });
                }

                return FakeGh.Ok(new
                {
                    data = new
                    {
                        node = new
                        {
                            id = "PRRT_1",
                            isResolved = resolved,
                            isOutdated = false,
                            path = "src/Port.cs",
                            line = 12,
                            comments = new { nodes = Array.Empty<object>() },
                            pullRequest = new { number = 20, repository = new { nameWithOwner = "owner/repo" } },
                        },
                    },
                });
            }
            return null;
        };

        var written = await _provider.ResolveReviewThreadAsync(Seven, "PRRT_1", None);

        written.Matches.ShouldBeTrue(written.Mismatch);
        written.Observed!.IsResolved.ShouldBeTrue();
    }

    [Fact]
    public async Task Resolving_a_thread_already_resolved_writes_nothing_and_reads_it_resolved()
    {
        var mutations = 0;
        _gh.Intercept = call =>
        {
            if (call is ["api", "graphql", "-f", var query, "-f", "id=PRRT_1"])
            {
                if (query.Contains("resolveReviewThread", StringComparison.Ordinal))
                {
                    mutations++;
                    return FakeGh.Ok(new { data = new { resolveReviewThread = new { thread = new { id = "PRRT_1" } } } });
                }

                return FakeGh.Ok(new
                {
                    data = new
                    {
                        node = new
                        {
                            id = "PRRT_1",
                            isResolved = true,
                            isOutdated = false,
                            path = "src/Port.cs",
                            line = 12,
                            comments = new { nodes = Array.Empty<object>() },
                            pullRequest = new { number = 20, repository = new { nameWithOwner = "owner/repo" } },
                        },
                    },
                });
            }
            return null;
        };

        var written = await _provider.ResolveReviewThreadAsync(Seven, "PRRT_1", None);

        written.Matches.ShouldBeTrue(written.Mismatch);
        written.Observed!.IsResolved.ShouldBeTrue();
        mutations.ShouldBe(0);
    }

    [Fact]
    public async Task Checks_of_the_head_come_from_its_check_runs_and_its_statuses()
    {
        const string Head = "9b01a3c0000000000000000000000000000000ff";
        _gh.Intercept = call => call switch
        {
            ["api", $"repos/owner/repo/commits/{Head}/check-runs?per_page=100", "--jq", ".check_runs[]"] => new ProcessOutcome(
                0,
                """
                {"name":"build","status":"completed","conclusion":"success","html_url":"https://github.com/owner/repo/runs/1"}
                {"name":"lint","status":"in_progress","conclusion":null,"html_url":null}
                {"name":"deploy","status":"completed","conclusion":"skipped"}
                """,
                ""
            ),
            ["api", $"repos/owner/repo/commits/{Head}/status"] => new ProcessOutcome(
                0,
                $$"""{"sha":"{{Head}}","statuses":[{"context":"aspire do ci","state":"failure","target_url":null}]}""",
                ""
            ),
            _ => null,
        };

        var checks = (await _provider.ReadPullRequestReviewAsync(Seven, None)).ShouldNotBeNull().Checks;

        checks.Select(check => $"{check.Name} {check.State}").ShouldBe(["build Passing", "lint Pending", "deploy Neutral", "aspire do ci Failing"]);
        checks[0].Url.ShouldBe(new Uri("https://github.com/owner/repo/runs/1"));
    }

    [Fact]
    public async Task An_image_is_read_with_the_credential_only_from_GitHub()
    {
        var asked = new List<IReadOnlyList<string>>();
        var provider = new GitHubPbiProvider(_gh.Run)
        {
            Bytes = (arguments, _) =>
            {
                asked.Add(arguments);
                return Task.FromResult<byte[]?>([0x89, 0x50, 0x4E, 0x47]);
            },
        };

        (await provider.ReadAttachmentAsync(new Uri("https://example.com/user-attachments/assets/1"), None)).ShouldBeNull();
        (await provider.ReadAttachmentAsync(new Uri("https://github.com/user-attachments/assets/8efe4e72"), None)).ShouldNotBeNull();

        asked.ShouldBe([["api", "https://github.com/user-attachments/assets/8efe4e72"]]);
    }
}
