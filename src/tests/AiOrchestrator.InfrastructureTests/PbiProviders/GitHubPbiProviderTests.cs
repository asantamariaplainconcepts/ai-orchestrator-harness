using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Infrastructure;
using AiOrchestrator.Infrastructure.PbiProviders.GitHub;

namespace AiOrchestrator.InfrastructureTests.PbiProviders;

/// <summary>
/// #500. <see cref="GitHubPbiProvider"/> against a <c>gh</c> that answers from memory. The assertion
/// that matters is the read-back: a write the repository did not keep has to come back as a
/// mismatch, never as the state that was asked for.
/// </summary>
public sealed class GitHubPbiProviderTests
{
    readonly FakeGh _gh = new();
    readonly GitHubPbiProvider _provider;
    static readonly PbiRef Seven = new(FakeGh.Repository, "7");
    static readonly CancellationToken None = CancellationToken.None;

    public GitHubPbiProviderTests()
    {
        _provider = new GitHubPbiProvider(_gh.Run);
        _gh.Labels.UnionWith(["status:in-progress", "status:holding", "bug"]);
        _gh.Issues[7] = new FakeGh.Issue("Releer cada escritura") { Assignee = "dev" };
        _gh.Issues[7].Labels.AddRange(["bug", "status:in-progress"]);
    }

    [Fact]
    public async Task The_states_are_the_state_labels_without_their_prefix_and_without_the_hold()
    {
        // #565. A label whose name only contains the prefix is not a state.
        _gh.Labels.UnionWith(["status:verifying", "not-a-status:thing"]);

        (await _provider.ListStatesAsync(FakeGh.Repository, None)).ShouldBe(["in-progress", "verifying"]);
    }

    [Fact]
    public async Task A_vendor_that_cannot_list_the_states_says_why()
    {
        _gh.Intercept = call => call is ["label", "list", ..] ? FakeGh.Fail("HTTP 404: Not Found") : null;

        var failure = await Should.ThrowAsync<PbiProviderException>(() => _provider.ListStatesAsync(FakeGh.Repository, None));

        failure.Message.ShouldContain("HTTP 404");
    }

    [Fact]
    public async Task Moving_swaps_the_state_label_in_one_edit_and_reads_it_back()
    {
        _gh.Issues[7].Labels.Add("status:holding");

        var written = await _provider.MoveAsync(Seven, new PbiMove("verifying"), None);

        written.Matches.ShouldBeTrue(written.Mismatch);
        written.Observed.ShouldBe(new Pbi(Seven, "Releer cada escritura", "verifying", true, "dev"));
        _gh.Issues[7].Labels.ShouldBe(["bug", "status:holding", "status:verifying"]);
        _gh.Calls.ShouldContain(call =>
            call.SequenceEqual(new[]
            {
                "issue", "edit", "7", "--repo", FakeGh.Repository,
                "--remove-label", "status:in-progress", "--add-label", "status:verifying",
            })
        );
        _gh.Calls[^1].ShouldBe(["api", "repos/owner/repo/issues/7"], "the last call is the read-back");
    }

    [Fact]
    public async Task A_label_the_repository_lacks_is_created_before_the_edit()
    {
        await _provider.MoveAsync(Seven, new PbiMove("merge-review"), None);

        _gh.Labels.ShouldContain("status:merge-review");
        var create = _gh.Calls.FindIndex(call => call is ["label", "create", "status:merge-review", ..]);
        var edit = _gh.Calls.FindIndex(call => call is ["issue", "edit", ..]);
        create.ShouldBeLessThan(edit);
    }

    [Fact]
    public async Task A_read_back_that_does_not_match_is_a_mismatch()
    {
        // Somebody else's label lands between the edit and the read.
        _gh.AfterEdit = issue => issue.Labels.Add("status:code-review");

        var written = await _provider.MoveAsync(Seven, new PbiMove("verifying"), None);

        written.Matches.ShouldBeFalse();
        written.Mismatch.ShouldBe(
            "owner/repo#7 should be verifying, not held, but reads status:verifying and status:code-review after the edit."
        );
        written.Observed.ShouldNotBeNull().State.ShouldBe("verifying");
    }

    [Fact]
    public async Task An_edit_gh_reports_done_but_the_vendor_did_not_keep_is_a_mismatch()
    {
        _gh.Intercept = call => call is ["issue", "edit", ..] ? FakeGh.Ok("") : null;

        var written = await _provider.MoveAsync(Seven, new PbiMove("verifying"), None);

        written.Mismatch.ShouldBe("owner/repo#7 should be verifying, not held, but reads in-progress, not held after the edit.");
        written.Observed.ShouldNotBeNull().State.ShouldBe("in-progress");
    }

    [Fact]
    public async Task A_refused_edit_says_what_gh_said()
    {
        _gh.Intercept = call => call is ["issue", "edit", ..] ? FakeGh.Fail("HTTP 403: Resource not accessible by integration") : null;

        var written = await _provider.MoveAsync(Seven, new PbiMove("verifying"), None);

        written.Mismatch.ShouldBe(
            "owner/repo#7 should be verifying, not held, but reads in-progress, not held after the edit "
                + "(gh said: HTTP 403: Resource not accessible by integration)."
        );
    }

    [Fact]
    public async Task A_move_that_lost_the_hold_is_a_mismatch()
    {
        _gh.Issues[7].Labels.Add("status:holding");
        _gh.AfterEdit = issue => issue.Labels.Remove("status:holding");

        var written = await _provider.MoveAsync(Seven, new PbiMove("verifying"), None);

        written.Mismatch.ShouldBe("owner/repo#7 should be verifying, held, but reads verifying, not held after the edit.");
    }

    [Fact]
    public async Task A_read_back_that_fails_is_reported_without_an_observation()
    {
        var reads = 0;
        _gh.Intercept = call => call is ["api", "repos/owner/repo/issues/7"] && ++reads == 2 ? FakeGh.Fail("error connecting to api.github.com") : null;

        var written = await _provider.MoveAsync(Seven, new PbiMove("verifying"), None);

        written.Observed.ShouldBeNull();
        written.Mismatch.ShouldBe("owner/repo#7 was edited but could not be read back: error connecting to api.github.com");
    }

    [Fact]
    public async Task Moving_a_PBI_the_vendor_does_not_have_writes_nothing()
    {
        var written = await _provider.MoveAsync(new PbiRef(FakeGh.Repository, "99"), new PbiMove("verifying"), None);

        written.Observed.ShouldBeNull();
        written.Mismatch.ShouldBe("owner/repo#99 does not exist, so it was not moved to verifying.");
        _gh.Calls.ShouldNotContain(call => call.Count > 1 && call[0] == "issue" && call[1] == "edit");
    }

    [Theory]
    [InlineData("")]
    [InlineData("holding")]
    public async Task The_hold_is_not_a_state(string state) =>
        await Should.ThrowAsync<ArgumentException>(() => _provider.MoveAsync(Seven, new PbiMove(state), None));

    [Fact]
    public async Task A_move_that_names_nothing_is_refused() =>
        await Should.ThrowAsync<ArgumentException>(() => _provider.MoveAsync(Seven, new PbiMove(), None));

    [Fact]
    public async Task The_hold_goes_on_and_comes_off_without_touching_the_state()
    {
        var on = await _provider.MoveAsync(Seven, new PbiMove(Held: true), None);
        var off = await _provider.MoveAsync(Seven, new PbiMove(Held: false), None);

        on.Matches.ShouldBeTrue(on.Mismatch);
        on.Observed.ShouldNotBeNull().Held.ShouldBeTrue();
        off.Matches.ShouldBeTrue(off.Mismatch);
        off.Observed.ShouldNotBeNull().Held.ShouldBeFalse();
        off.Observed.State.ShouldBe("in-progress");
        _gh.Calls.ShouldContain(call => call.SequenceEqual(new[] { "issue", "edit", "7", "--repo", FakeGh.Repository, "--add-label", "status:holding" }));
    }

    /// <summary>The engine's release (#666): the hold comes off and the PBI moves on in the one edit.</summary>
    [Fact]
    public async Task A_release_and_a_move_are_one_edit()
    {
        _gh.Issues[7].Labels.Add("status:holding");

        var written = await _provider.MoveAsync(Seven, new PbiMove("verifying", Held: false), None);

        written.Matches.ShouldBeTrue(written.Mismatch);
        written.Observed.ShouldBe(new Pbi(Seven, "Releer cada escritura", "verifying", false, "dev"));
        _gh.Calls.Count(call => call is ["issue", "edit", ..]).ShouldBe(1);
        _gh.Calls.ShouldContain(call =>
            call.SequenceEqual(new[]
            {
                "issue", "edit", "7", "--repo", FakeGh.Repository,
                "--remove-label", "status:in-progress", "--add-label", "status:verifying", "--remove-label", "status:holding",
            })
        );
    }

    [Fact]
    public async Task A_hold_already_in_place_is_not_written_again()
    {
        var written = await _provider.MoveAsync(Seven, new PbiMove(Held: false), None);

        written.Matches.ShouldBeTrue();
        _gh.Calls.ShouldNotContain(call => call.Count > 1 && call[0] == "issue" && call[1] == "edit");
    }

    [Fact]
    public async Task A_hold_the_vendor_did_not_keep_is_a_mismatch()
    {
        _gh.AfterEdit = issue => issue.Labels.Remove("status:holding");

        var written = await _provider.MoveAsync(Seven, new PbiMove(Held: true), None);

        written.Mismatch.ShouldBe("owner/repo#7 should be in-progress, held, but reads in-progress, not held after the edit.");
    }

    [Fact]
    public async Task Holding_a_PBI_the_vendor_does_not_have_writes_nothing()
    {
        var written = await _provider.MoveAsync(new PbiRef(FakeGh.Repository, "99"), new PbiMove(Held: true), None);

        written.Mismatch.ShouldBe("owner/repo#99 does not exist, so it was not held.");
    }

    [Fact]
    public async Task A_PBI_reads_its_state_hold_and_assignee_from_its_labels()
    {
        _gh.Issues[7].Labels.Add("status:holding");

        var pbi = (await _provider.ReadThreadAsync(Seven, None))?.Pbi;

        pbi.ShouldBe(new Pbi(Seven, "Releer cada escritura", "in-progress", true, "dev"));
    }

    [Fact]
    public async Task A_PBI_without_a_state_label_has_no_state()
    {
        _gh.Issues[8] = new FakeGh.Issue("Sin estado");

        var pbi = (await _provider.ReadThreadAsync(new PbiRef(FakeGh.Repository, "8"), None))?.Pbi;

        pbi.ShouldBe(new Pbi(new PbiRef(FakeGh.Repository, "8"), "Sin estado", null, false, null));
    }

    [Fact]
    public async Task An_absent_PBI_is_null_and_a_pull_request_is_no_PBI()
    {
        _gh.Issues[8] = new FakeGh.Issue("Un PR") { IsPullRequest = true };

        (await _provider.ReadThreadAsync(new PbiRef(FakeGh.Repository, "99"), None)).ShouldBeNull();
        var pull = (await _provider.ReadThreadAsync(new PbiRef(FakeGh.Repository, "8"), None)).ShouldNotBeNull();
        pull.IsPullRequest.ShouldBeTrue();
        pull.AsPbi.ShouldBeNull();
    }

    [Fact]
    public async Task A_read_gh_cannot_answer_throws_with_its_line()
    {
        _gh.Intercept = _ => new ProcessOutcome(4, "", "To get started with GitHub CLI, please run:  gh auth login\nmore");

        var failure = await Should.ThrowAsync<PbiProviderException>(() => _provider.ReadThreadAsync(Seven, None));

        failure.Message.ShouldBe("To get started with GitHub CLI, please run:  gh auth login");
    }

    [Fact]
    public async Task A_missing_gh_and_a_silent_one_are_named()
    {
        _gh.Intercept = _ => new ProcessOutcome(-1, "", "") { NotStarted = "No such file or directory" };
        (await Should.ThrowAsync<PbiProviderException>(() => _provider.ReadThreadAsync(Seven, None)))
            .Message.ShouldBe("The GitHub CLI could not be started: No such file or directory");

        _gh.Intercept = _ => new ProcessOutcome(-1, "", "") { TimedOut = true };
        (await Should.ThrowAsync<PbiProviderException>(() => _provider.ReadThreadAsync(Seven, None)))
            .Message.ShouldBe("The GitHub CLI did not answer within 20 seconds.");

        _gh.Intercept = _ => new ProcessOutcome(2, "", "");
        (await Should.ThrowAsync<PbiProviderException>(() => _provider.ReadThreadAsync(Seven, None)))
            .Message.ShouldBe("The GitHub CLI exited with 2 and said nothing about why.");

        _gh.Intercept = _ => new ProcessOutcome(0, "not json", "");
        (await Should.ThrowAsync<PbiProviderException>(() => _provider.ReadThreadAsync(Seven, None)))
            .Message.ShouldStartWith("The GitHub CLI printed something that is not JSON");
    }

    [Theory]
    [InlineData("owner", "7")]
    [InlineData("owner/repo", "seven")]
    [InlineData("owner/repo", "0")]
    public async Task A_reference_that_is_not_an_issue_is_refused(string repository, string id) =>
        await Should.ThrowAsync<ArgumentException>(() => _provider.ReadThreadAsync(new PbiRef(repository, id), None));

    [Fact]
    public async Task The_open_PBIs_are_listed()
    {
        _gh.Issues[8] = new FakeGh.Issue("Otro");

        var pbis = await _provider.ListAsync(FakeGh.Repository, new PbiQuery(), None);

        pbis.Select(pbi => (pbi.Ref.Id, pbi.State)).ShouldBe([("7", "in-progress"), ("8", (string?)null)]);
        _gh.Calls[0].ShouldBe(["issue", "list", "--repo", FakeGh.Repository, "--state", "open", "--limit", "1000", "--json", "number,title,labels,assignees,state"]);
    }

    [Fact]
    public async Task The_closed_PBIs_are_listed_apart_from_the_open_ones()
    {
        _gh.Issues[8] = new FakeGh.Issue("Hecho") { Closed = true };
        _gh.Issues[8].Labels.Add("status:done");

        var closed = await _provider.ListAsync(FakeGh.Repository, new PbiQuery(PbiSearchState.Closed, 100), None);
        var open = await _provider.ListAsync(FakeGh.Repository, new PbiQuery(), None);

        closed.ShouldBe([new Pbi(new PbiRef(FakeGh.Repository, "8"), "Hecho", "done", false, null, Closed: true)]);
        open.Select(pbi => pbi.Ref.Id).ShouldBe(["7"]);
        _gh.Calls[0].ShouldBe(
            [
                "issue", "list", "--repo", FakeGh.Repository, "--state", "closed", "--limit", "100",
                "--search", "sort:updated-desc", "--json", "number,title,labels,assignees,state",
            ]
        );
    }

    [Fact]
    public async Task A_comment_is_written_and_read_back()
    {
        var written = await _provider.CommentAsync(Seven, "Dos líneas\r\nde texto", [], None);

        written.Matches.ShouldBeTrue(written.Mismatch);
        written.Observed.ShouldNotBeNull().Body.ShouldBe("Dos líneas\r\nde texto");
        written.Observed.Author.ShouldBe("dev");
        written.Observed.CreatedAt.ShouldBe(DateTimeOffset.Parse("2026-10-08T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        _gh.Calls[^1].ShouldBe(["api", $"repos/owner/repo/issues/comments/{written.Observed.Id}"]);
    }

    [Fact]
    public async Task A_comment_that_reads_back_different_is_a_mismatch()
    {
        _gh.Intercept = call =>
            call is ["api", var path] && path.StartsWith("repos/owner/repo/issues/comments/", StringComparison.Ordinal)
                ? FakeGh.Ok(new { id = 1, body = "otra cosa" })
                : null;

        var written = await _provider.CommentAsync(Seven, "Hola", [], None);

        written.Mismatch.ShouldBe("The comment 9000000001 on owner/repo#7 reads back different from what was written.");
    }

    [Fact]
    public async Task A_comment_gh_refused_or_could_not_read_back_says_so()
    {
        _gh.Intercept = call => call is ["api", _, "--method", "POST", ..] ? FakeGh.Fail("gh: Not Found (HTTP 404)") : null;
        (await _provider.CommentAsync(Seven, "Hola", [], None)).Mismatch
            .ShouldBe("The comment on owner/repo#7 was not written: gh: Not Found (HTTP 404)");

        _gh.Intercept = call => call is ["api", var path] && path.Contains("/comments/", StringComparison.Ordinal) ? FakeGh.Fail("timeout") : null;
        (await _provider.CommentAsync(Seven, "Hola", [], None)).Mismatch
            .ShouldBe("The comment 9000000001 on owner/repo#7 was written but could not be read back: timeout");
    }

    [Fact]
    public async Task A_comment_is_edited_in_place_and_read_back()
    {
        // #696. The journey comment: one PATCH by the id the thread reads, and the same read-back.
        var written = await _provider.CommentAsync(Seven, "<!-- aio:journey -->\none row", [], None);
        var id = written.Observed.ShouldNotBeNull().Id;

        var edited = await _provider.EditCommentAsync(Seven, id, "<!-- aio:journey -->\ntwo rows", None);

        edited.Matches.ShouldBeTrue(edited.Mismatch);
        edited.Observed.ShouldNotBeNull().Id.ShouldBe(id);
        edited.Observed.Body.ShouldBe("<!-- aio:journey -->\ntwo rows");
        _gh.Comments.Count.ShouldBe(1, "an edit adds no comment");
        _gh.Calls[^2].ShouldBe(["api", $"repos/owner/repo/issues/comments/{id}", "--method", "PATCH", "-f", "body=<!-- aio:journey -->\ntwo rows"]);
        _gh.Calls[^1].ShouldBe(["api", $"repos/owner/repo/issues/comments/{id}"]);
    }

    /// <summary>
    /// #710. On aio-dogfood#30 the journey comment was edited four times, and four times it read back
    /// "different" though it was right. What GitHub may change when it saves is not a difference:
    /// <c>\r\n</c> for <c>\n</c>, spaces at the end of a line, and blank lines at either end.
    /// </summary>
    [Fact]
    public async Task A_journey_comment_that_reads_back_with_CRLF_and_trailing_spaces_is_written()
    {
        const string Body = "<!-- aio:journey -->\n### Journey\n\n| When | State |\n| --- | --- |\n| 2026-10-13T22:47:34Z | verifying |\n";
        var id = (await _provider.CommentAsync(Seven, "<!-- aio:journey -->\none row", [], None)).Observed.ShouldNotBeNull().Id;
        _gh.Intercept = call =>
            call is ["api", var path] && path == $"repos/owner/repo/issues/comments/{id}"
                ? FakeGh.Ok(new { id = long.Parse(id, System.Globalization.CultureInfo.InvariantCulture), body = "\r\n" + Body.Replace("\n", "  \r\n", StringComparison.Ordinal) + "\r\n" })
                : null;

        var edited = await _provider.EditCommentAsync(Seven, id, Body, None);

        edited.Matches.ShouldBeTrue(edited.Mismatch);
    }

    [Fact]
    public async Task An_edit_of_a_comment_gh_does_not_have_says_so()
    {
        (await _provider.EditCommentAsync(Seven, "404", "Hola", None)).Mismatch
            .ShouldBe("The comment 404 on owner/repo#7 was not edited: gh: Not Found (HTTP 404)");
        await Should.ThrowAsync<ArgumentException>(() => _provider.EditCommentAsync(Seven, "IC_kwDO", "Hola", None));
    }

    [Fact]
    public async Task An_empty_comment_is_refused() =>
        await Should.ThrowAsync<ArgumentException>(() => _provider.CommentAsync(Seven, " ", [], None));

    [Fact]
    public async Task The_pull_request_is_the_latest_one_closing_the_PBI_in_its_repository()
    {
        _gh.Issues[7].ClosedBy.AddRange([(FakeGh.Repository, 10), ("someone/else", 99), (FakeGh.Repository, 12)]);
        _gh.Pulls[12] = new FakeGh.Pull { State = "MERGED", ReviewDecision = "APPROVED", IsDraft = false };

        var pull = await _provider.GetPullRequestAsync(Seven, None);

        pull.ShouldBe(new PullRequestInfo("12", new Uri("https://github.com/owner/repo/pull/12"), false, true, true));
    }

    [Fact]
    public async Task A_draft_not_yet_approved_reads_as_such()
    {
        _gh.Issues[7].ClosedBy.Add((FakeGh.Repository, 10));
        _gh.Pulls[10] = new FakeGh.Pull { IsDraft = true, ReviewDecision = "REVIEW_REQUIRED" };

        var pull = await _provider.GetPullRequestAsync(Seven, None);

        pull.ShouldBe(new PullRequestInfo("10", new Uri("https://github.com/owner/repo/pull/10"), true, false, false));
    }

    /// <summary>#719. Closed without a merge: the Review tab offers no verdict on it.</summary>
    [Fact]
    public async Task A_pull_request_closed_without_a_merge_reads_as_closed()
    {
        _gh.Issues[7].ClosedBy.Add((FakeGh.Repository, 10));
        _gh.Pulls[10] = new FakeGh.Pull { State = "CLOSED", IsDraft = false };

        var pull = await _provider.GetPullRequestAsync(Seven, None);

        pull.ShouldBe(new PullRequestInfo("10", new Uri("https://github.com/owner/repo/pull/10"), false, false, false, IsClosed: true));
    }

    [Fact]
    public async Task No_pull_request_and_no_PBI_are_both_null()
    {
        (await _provider.GetPullRequestAsync(Seven, None)).ShouldBeNull();
        (await _provider.GetPullRequestAsync(new PbiRef(FakeGh.Repository, "99"), None)).ShouldBeNull();
        (await _provider.GetPullRequestChangeAsync(Seven, None)).ShouldBeNull();
    }

    [Fact]
    public async Task A_review_comment_is_written_on_the_head_and_read_back()
    {
        var pull = WithPullRequest();

        var written = await _provider.AddReviewCommentAsync(Seven, new ReviewCommentDraft("Esto relee.", "src/A.cs", 12), None);

        written.Matches.ShouldBeTrue(written.Mismatch);
        written.Observed.ShouldNotBeNull().ShouldBe(
            new ReviewComment(written.Observed.Id, "src/A.cs", 12, "dev", DateTimeOffset.Parse("2026-10-08T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture), "Esto relee.")
        );
        _gh.Calls.ShouldContain(call => call.Contains($"commit_id={pull.Head}"));
        _gh.Calls[^1].ShouldBe(["api", $"repos/owner/repo/pulls/comments/{written.Observed.Id}"], "the last call is the read-back");
    }

    [Fact]
    public async Task A_review_comment_on_a_line_outside_the_diff_is_not_written()
    {
        WithPullRequest();

        var written = await _provider.AddReviewCommentAsync(Seven, new ReviewCommentDraft("Fuera.", "src/A.cs", 400), None);

        written.Observed.ShouldBeNull();
        written.Mismatch.ShouldBe("The comment on src/A.cs:400 of pull request 10 was not written: gh: Validation Failed (HTTP 422)");
    }

    [Fact]
    public async Task A_review_comment_that_reads_back_elsewhere_is_a_mismatch()
    {
        WithPullRequest();
        _gh.Intercept = call =>
            call is ["api", var path] && path.StartsWith("repos/owner/repo/pulls/comments/", StringComparison.Ordinal)
                ? FakeGh.Ok(new { id = 1, path = "src/A.cs", line = (int?)null, body = "Otra." })
                : null;

        var written = await _provider.AddReviewCommentAsync(Seven, new ReviewCommentDraft("Esto relee.", "src/A.cs", 12), None);

        written.Mismatch.ShouldBe("The comment 9000000001 was meant for src/A.cs:12 and reads back on src/A.cs:no line, with a different body.");
    }

    [Fact]
    public async Task A_review_comment_that_cannot_be_read_back_says_so()
    {
        WithPullRequest();
        _gh.Intercept = call => call is ["api", var path] && path.Contains("/pulls/comments/", StringComparison.Ordinal) ? FakeGh.Fail("timeout") : null;

        var written = await _provider.AddReviewCommentAsync(Seven, new ReviewCommentDraft("Esto relee.", "src/A.cs", 12), None);

        written.Mismatch.ShouldBe("The comment 9000000001 on src/A.cs:12 was written but could not be read back: timeout");
    }

    [Fact]
    public async Task A_review_comment_without_a_pull_request_writes_nothing()
    {
        var written = await _provider.AddReviewCommentAsync(Seven, new ReviewCommentDraft("Nada.", "src/A.cs", 12), None);

        written.Observed.ShouldBeNull();
        written.Mismatch.ShouldBe("owner/repo#7 has no pull request, so the comment on src/A.cs:12 was not written.");
    }

    [Theory]
    [InlineData("", 1, "body")]
    [InlineData("a.cs", 0, "body")]
    [InlineData("a.cs", 1, "")]
    public async Task A_review_comment_needs_a_path_a_line_and_a_body(string path, int line, string body) =>
        await Should.ThrowAsync<ArgumentException>(() => _provider.AddReviewCommentAsync(Seven, new ReviewCommentDraft(body, path, line), None));

    [Fact]
    public async Task A_reply_needs_the_id_of_a_comment() =>
        await Should.ThrowAsync<ArgumentException>(() => _provider.AddReviewCommentAsync(Seven, new ReviewCommentDraft("body", InReplyTo: "nine"), None));

    [Fact]
    public void The_adapter_speaks_for_github() => _provider.Name.ShouldBe("github");

    FakeGh.Pull WithPullRequest()
    {
        _gh.Issues[7].ClosedBy.Add((FakeGh.Repository, 10));
        var pull = new FakeGh.Pull { Head = "f00d" };
        pull.DiffLines.Add("src/A.cs:12");
        _gh.Pulls[10] = pull;
        return pull;
    }
}
