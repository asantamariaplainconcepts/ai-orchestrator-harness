using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Infrastructure.PbiProviders.GitHub;

namespace AiOrchestrator.InfrastructureTests.PbiProviders;

/// <summary>
/// #500. The adapter against a real repository through the real <c>gh</c>, outside the gate: it
/// writes labels and comments, so it runs only when somebody names a throwaway issue to write on.
/// <code>
/// AIO_PBI_TEST_REPO=owner/sandbox AIO_PBI_TEST_ISSUE=1 \
///   dotnet test src/tests/AiOrchestrator.InfrastructureTests --filter GitHubPbiProviderLiveTests
/// </code>
/// <c>AIO_PBI_TEST_REVIEW_AT=path:line</c> also writes a review comment on that line of the issue's
/// pull request.
/// </summary>
public sealed class GitHubPbiProviderLiveTests
{
    static readonly CancellationToken None = CancellationToken.None;

    [LiveGitHubFact]
    public async Task Every_write_on_a_real_issue_reads_back_as_written()
    {
        var provider = new GitHubPbiProvider();
        var pbi = new PbiRef(Environment.GetEnvironmentVariable("AIO_PBI_TEST_REPO")!, Environment.GetEnvironmentVariable("AIO_PBI_TEST_ISSUE")!);

        var before = (await provider.ReadThreadAsync(pbi, None))?.Pbi;
        before.ShouldNotBeNull($"{pbi.Repository}#{pbi.Id} is the issue to write on");

        var moved = await provider.MoveAsync(pbi, new PbiMove("verifying"), None);
        moved.Mismatch.ShouldBeNull();
        moved.Observed!.State.ShouldBe("verifying");

        var held = await provider.MoveAsync(pbi, new PbiMove(Held: !before.Held), None);
        held.Mismatch.ShouldBeNull();
        var released = await provider.MoveAsync(pbi, new PbiMove(Held: before.Held), None);
        released.Mismatch.ShouldBeNull();

        var comment = await provider.CommentAsync(pbi, $"GitHubPbiProviderLiveTests, {DateTimeOffset.UtcNow:O}", [], None);
        comment.Mismatch.ShouldBeNull();

        if (Environment.GetEnvironmentVariable("AIO_PBI_TEST_REVIEW_AT") is { } at && at.Split(':') is [var path, var line])
        {
            var review = await provider.AddReviewCommentAsync(pbi, new ReviewCommentDraft("GitHubPbiProviderLiveTests", path, int.Parse(line, System.Globalization.CultureInfo.InvariantCulture)), None);
            review.Mismatch.ShouldBeNull();
            (await provider.GetPullRequestChangeAsync(pbi, None))!.Comments.ShouldContain(review.Observed!);
        }

        if (before.State is { } state)
        {
            (await provider.MoveAsync(pbi, new PbiMove(state), None)).Mismatch.ShouldBeNull();
        }
    }
}

/// <summary>Skipped unless <c>AIO_PBI_TEST_REPO</c> and <c>AIO_PBI_TEST_ISSUE</c> name an issue to write on.</summary>
sealed class LiveGitHubFactAttribute : FactAttribute
{
    public LiveGitHubFactAttribute()
    {
        if (
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AIO_PBI_TEST_REPO"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AIO_PBI_TEST_ISSUE"))
        )
        {
            Skip = "Writes to a real repository: set AIO_PBI_TEST_REPO and AIO_PBI_TEST_ISSUE to a throwaway issue.";
        }
    }
}
