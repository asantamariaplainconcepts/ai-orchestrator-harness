using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Infrastructure.PbiProviders.GitHub;

namespace AiOrchestrator.InfrastructureTests.PbiProviders;

/// <summary>
/// #566. What installing the harness asks of <see cref="GitHubPbiProvider"/>: the pull request from
/// <c>harness/install</c>, ready and closing no issue, found again rather than opened twice; and the
/// state labels with the hold, created only where missing and read back.
/// </summary>
public sealed class GitHubPbiProviderInstallTests
{
    readonly FakeGh _gh = new();
    readonly GitHubPbiProvider _provider;
    static readonly CancellationToken None = CancellationToken.None;
    static readonly PullRequestDraft Install = new("harness/install", "Install harness 2", "Installs it.", Draft: false);

    public GitHubPbiProviderInstallTests() => _provider = new GitHubPbiProvider(_gh.Run);

    [Fact]
    public async Task The_install_pull_request_is_ready_closes_nothing_and_is_opened_once()
    {
        (await _provider.GetBranchPullRequestAsync(FakeGh.Repository, "harness/install", None)).ShouldBeNull();

        var opened = await _provider.OpenPullRequestAsync(FakeGh.Repository, Install, None);

        opened.Matches.ShouldBeTrue(opened.Mismatch);
        opened.Observed.ShouldNotBeNull().IsDraft.ShouldBeFalse();
        _gh.Calls.ShouldContain(call =>
            call.SequenceEqual(new[]
            {
                "pr", "create", "--repo", FakeGh.Repository, "--head", "harness/install",
                "--title", "Install harness 2", "--body", "Installs it.",
            })
        );

        var again = await _provider.OpenPullRequestAsync(FakeGh.Repository, Install, None);

        again.Observed.ShouldBe(opened.Observed);
        _gh.Pulls.Count.ShouldBe(1);
        (await _provider.GetBranchPullRequestAsync(FakeGh.Repository, "harness/install", None)).ShouldBe(opened.Observed);
    }

    [Fact]
    public async Task A_pull_request_that_does_not_read_back_is_a_mismatch()
    {
        _gh.Intercept = call => call is ["pr", "create", ..] ? FakeGh.Fail("pull request create failed: GraphQL: No commits between main and harness/install") : null;

        var opened = await _provider.OpenPullRequestAsync(FakeGh.Repository, Install, None);

        opened.Observed.ShouldBeNull();
        opened.Mismatch.ShouldBe(
            "No pull request from harness/install is open after the create (gh said: pull request create failed: GraphQL: No commits between main and harness/install)."
        );
    }

    [Fact]
    public async Task The_states_and_the_hold_are_created_where_missing_and_read_back()
    {
        _gh.Labels.UnionWith(["status:verifying", "bug"]);

        var created = await _provider.CreateStatesAsync(FakeGh.Repository, ["spec-ready", "verifying", "done"], None);

        created.Matches.ShouldBeTrue(created.Mismatch);
        created.Observed.ShouldBe(["done", "spec-ready", "verifying"]);
        _gh.Labels.ShouldContain("status:holding");
        _gh.Calls.Count(call => call is ["label", "create", ..]).ShouldBe(3, "verifying was already there");
    }

    [Fact]
    public async Task A_repository_with_no_state_label_is_read_as_none_and_gets_them_all()
    {
        // gh prints nothing, not [], for a repository with no label.
        var listed = 0;
        _gh.Intercept = call => call is ["label", "list", ..] && listed++ == 0 ? new(0, "", "") : null;

        var created = await _provider.CreateStatesAsync(FakeGh.Repository, ["spec-ready"], None);

        created.Matches.ShouldBeTrue(created.Mismatch);
        created.Observed.ShouldBe(["spec-ready"]);
        _gh.Intercept = call => call is ["label", "list", ..] ? new(0, "", "") : null;
        (await _provider.ListStatesAsync(FakeGh.Repository, None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_state_the_vendor_did_not_keep_is_named()
    {
        _gh.Intercept = call => call is ["label", "create", "status:done", ..] ? FakeGh.Fail("HTTP 403: Must have admin rights") : null;

        var created = await _provider.CreateStatesAsync(FakeGh.Repository, ["spec-ready", "done"], None);

        created.Observed.ShouldBe(["spec-ready"]);
        created.Mismatch.ShouldBe("owner/repo should have status:done after the create, and does not (gh said: status:done: HTTP 403: Must have admin rights).");
    }
}
