using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Infrastructure.PbiProviders.GitHub;

namespace AiOrchestrator.InfrastructureTests.PbiProviders;

/// <summary>
/// #505. The writes the loop engine makes on a transition: the claim's assignment, the draft pull
/// request when a PBI leaves <c>spec-ready</c>, marking it ready when it leaves <c>verifying</c>, and
/// closing the PBI when it leaves <c>merging</c>. Each is read back like every other write. And the
/// probe the engine reads its account from before any of them (#666).
/// </summary>
public sealed class GitHubLoopTransitionTests
{
    readonly FakeGh _gh = new();
    readonly GitHubPbiProvider _provider;
    static readonly PbiRef Seven = new(FakeGh.Repository, "7");
    static readonly CancellationToken None = CancellationToken.None;
    static readonly CliProbe Installed = new("gh", "/usr/local/bin/gh", "2.99.0");

    public GitHubLoopTransitionTests()
    {
        _provider = new GitHubPbiProvider(_gh.Run) { Probing = _ => Task.FromResult(Installed) };
        _gh.Issues[7] = new FakeGh.Issue("El motor del loop");
    }

    [Fact]
    public async Task The_probe_carries_the_login_of_the_credential() =>
        (await _provider.ProbeAsync(None)).ShouldBe(Installed with { Account = FakeGh.Login });

    [Fact]
    public async Task A_gh_that_is_not_there_is_not_asked_for_an_account()
    {
        var provider = new GitHubPbiProvider(_gh.Run) { Probing = _ => Task.FromResult(new CliProbe("gh", null, null)) };

        (await provider.ProbeAsync(None)).ShouldBe(new CliProbe("gh", null, null));
        _gh.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_gh_signed_out_names_no_account_and_says_why()
    {
        _gh.Intercept = call => call is ["api", "user", ..] ? FakeGh.Fail("To get started with GitHub CLI, please run:  gh auth login") : null;

        (await _provider.ProbeAsync(None)).ShouldBe(Installed with { Problem = "To get started with GitHub CLI, please run:  gh auth login" });
    }

    [Fact]
    public async Task Assigning_to_self_names_the_login_and_reads_it_back()
    {
        var written = await _provider.MoveAsync(Seven, new PbiMove(AssignToSelf: true), None);

        written.Matches.ShouldBeTrue(written.Mismatch);
        written.Observed.ShouldNotBeNull().Assignee.ShouldBe(FakeGh.Login);
        _gh.Calls.ShouldContain(call => call.SequenceEqual(new[] { "issue", "edit", "7", "--repo", FakeGh.Repository, "--add-assignee", FakeGh.Login }));
    }

    [Fact]
    public async Task An_assignment_the_vendor_did_not_keep_is_a_mismatch()
    {
        _gh.Intercept = call => call is ["issue", "edit", _, _, _, "--add-assignee", _] ? FakeGh.Fail("HTTP 422: cannot assign") : null;

        var written = await _provider.MoveAsync(Seven, new PbiMove(AssignToSelf: true), None);

        written.Mismatch.ShouldBe(
            "owner/repo#7 should be no state, not held, assigned to aio-bot, but reads no state, not held, assigned to nobody after the edit (gh said: HTTP 422: cannot assign)."
        );
    }

    [Fact]
    public async Task A_PBI_already_assigned_to_self_is_not_written_again()
    {
        _gh.Issues[7].Assignee = FakeGh.Login;

        var written = await _provider.MoveAsync(Seven, new PbiMove(AssignToSelf: true), None);

        written.Matches.ShouldBeTrue();
        _gh.Calls.Any(call => call is ["issue", "edit", ..]).ShouldBeFalse();
    }

    [Fact]
    public async Task The_draft_pull_request_closes_the_PBI_and_reads_back_as_a_draft()
    {
        var written = await _provider.OpenPullRequestAsync(FakeGh.Repository, Draft(true), None);

        written.Matches.ShouldBeTrue(written.Mismatch);
        written.Observed.ShouldNotBeNull().IsDraft.ShouldBeTrue();
        var pull = _gh.Pulls[int.Parse(written.Observed.Id, System.Globalization.CultureInfo.InvariantCulture)];
        pull.Body.ShouldBe("Closes #7");
        pull.Title.ShouldBe("El motor del loop");
        (await _provider.GetPullRequestAsync(Seven, None)).ShouldBe(written.Observed);
    }

    [Fact]
    public async Task A_pull_request_for_a_PBI_the_vendor_does_not_have_is_not_opened()
    {
        var written = await _provider.OpenPullRequestAsync(FakeGh.Repository, Draft(true) with { Closes = new PbiRef(FakeGh.Repository, "99") }, None);

        written.Observed.ShouldBeNull();
        written.Mismatch.ShouldBe("owner/repo#99 does not exist, so no pull request was opened for it.");
        _gh.Calls.Any(call => call is ["pr", "create", ..]).ShouldBeFalse();
    }

    [Fact]
    public async Task A_pull_request_already_open_from_the_branch_is_the_answer()
    {
        _gh.Pulls[30] = new FakeGh.Pull { Branch = "change/7", IsDraft = true };

        var written = await _provider.OpenPullRequestAsync(FakeGh.Repository, Draft(true), None);

        written.Observed.ShouldNotBeNull().Id.ShouldBe("30");
        _gh.Calls.Any(call => call is ["pr", "create", ..]).ShouldBeFalse();
    }

    [Fact]
    public async Task A_create_that_left_no_pull_request_says_what_gh_said()
    {
        _gh.Intercept = call => call is ["pr", "create", ..] ? FakeGh.Fail("pull request create failed: No commits between master and change/7") : null;

        var written = await _provider.OpenPullRequestAsync(FakeGh.Repository, Draft(true), None);

        written.Observed.ShouldBeNull();
        written.Mismatch.ShouldBe(
            "No pull request from change/7 is open after the create (gh said: pull request create failed: No commits between master and change/7)."
        );
    }

    [Fact]
    public async Task Marking_ready_reads_the_pull_request_back_out_of_draft()
    {
        await _provider.OpenPullRequestAsync(FakeGh.Repository, Draft(true), None);

        var written = await _provider.OpenPullRequestAsync(FakeGh.Repository, Draft(false), None);

        written.Matches.ShouldBeTrue(written.Mismatch);
        written.Observed.ShouldNotBeNull().IsDraft.ShouldBeFalse();
        _gh.Calls.ShouldContain(call => call.SequenceEqual(new[] { "pr", "ready", "20", "--repo", FakeGh.Repository }));
        _gh.Pulls.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_ready_the_vendor_did_not_keep_is_a_mismatch()
    {
        await _provider.OpenPullRequestAsync(FakeGh.Repository, Draft(true), None);
        _gh.Intercept = call => call is ["pr", "ready", ..] ? FakeGh.Ok("") : null;

        var written = await _provider.OpenPullRequestAsync(FakeGh.Repository, Draft(false), None);

        written.Mismatch.ShouldBe("The pull request from change/7 should be ready for review, and reads a draft.");
    }

    [Fact]
    public async Task Marking_ready_a_PBI_with_no_pull_request_writes_nothing()
    {
        var written = await _provider.OpenPullRequestAsync(FakeGh.Repository, Draft(false), None);

        written.Mismatch.ShouldBe("owner/repo#7 has no pull request, so none was marked ready for review.");
        _gh.Calls.Any(call => call is ["pr", "create", ..]).ShouldBeFalse();
    }

    [Fact]
    public async Task Closing_reads_the_PBI_back_closed_and_a_closed_one_is_not_written_again()
    {
        var written = await _provider.MoveAsync(Seven, new PbiMove(Close: true), None);

        written.Matches.ShouldBeTrue(written.Mismatch);
        written.Observed.ShouldNotBeNull().Closed.ShouldBeTrue();

        var calls = _gh.Calls.Count;
        (await _provider.MoveAsync(Seven, new PbiMove(Close: true), None)).Matches.ShouldBeTrue();
        _gh.Calls.Skip(calls).Any(call => call is ["issue", "close", ..]).ShouldBeFalse();
    }

    [Fact]
    public async Task A_close_the_vendor_did_not_keep_is_a_mismatch()
    {
        _gh.Intercept = call => call is ["issue", "close", ..] ? FakeGh.Fail("HTTP 403") : null;

        (await _provider.MoveAsync(Seven, new PbiMove(Close: true), None)).Mismatch.ShouldBe(
            "owner/repo#7 should be no state, not held, closed, but reads no state, not held, open after the edit (gh said: HTTP 403)."
        );
    }

    static PullRequestDraft Draft(bool draft) => new("change/7", "El motor del loop", "", draft, Seven);
}
