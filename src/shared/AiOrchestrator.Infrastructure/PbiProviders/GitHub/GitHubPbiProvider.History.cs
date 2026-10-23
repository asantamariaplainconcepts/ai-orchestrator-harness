using System.Globalization;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.Infrastructure.PbiProviders.GitHub;

/// <summary>
/// #734. What the Metrics screen reads of a repository: every issue with its parent and when it closed,
/// and the latest pull requests with their commits, in two calls at once.
/// <para>
/// The pull requests come from GraphQL because <c>gh pr list --json commits</c> asks for every author
/// of 100 commits of each pull request: on 14 October, 200 pull requests of this repository were
/// refused as a query of a million nodes. 100 pull requests of 100 commits of 5 authors is 50,000.
/// </para>
/// </summary>
sealed partial class GitHubPbiProvider
{
    /// <summary>More issues than a repository of this loop has; past it the oldest are left out.</summary>
    const string HistoryIssues = "1000";

    const string HistoryQuery =
        "query($owner: String!, $name: String!) { repository(owner: $owner, name: $name) { "
        + "pullRequests(first: 100, orderBy: { field: CREATED_AT, direction: DESC }) { pageInfo { hasNextPage } "
        + "nodes { number headRefName mergedAt commits(first: 100) { nodes { commit { oid committedDate message committer { name } authors(first: 5) { nodes { name } } } } } } } } }";

    public async Task<VendorHistory> ReadHistoryAsync(string repository, CancellationToken ct)
    {
        var (owner, name) = Split(repository);
        var issues = Ask(
            ["issue", "list", "--repo", repository, "--state", "all", "--limit", HistoryIssues, "--json", "number,title,closedAt,parent"],
            ct
        );
        var pulls = Ask(["api", "graphql", "-f", $"query={HistoryQuery}", "-f", $"owner={owner}", "-f", $"name={name}"], ct);
        await Task.WhenAll(issues, pulls);

        using var issueList = Parse(await issues);
        using var pullList = Parse(await pulls);
        var pullRequests = pullList.RootElement.GetProperty("data").GetProperty("repository").GetProperty("pullRequests");

        return new VendorHistory(
            [.. issueList.RootElement.EnumerateArray().Select(row => Lineage(repository, row))],
            [.. pullRequests.GetProperty("nodes").EnumerateArray().Select(BranchPull)],
            pullRequests.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean()
        );
    }

    static PbiLineage Lineage(string repository, JsonElement row) =>
        new(
            new PbiRef(repository, Number(row, "number").ToString(CultureInfo.InvariantCulture)),
            Text(row, "title") ?? string.Empty,
            row.TryGetProperty("parent", out var parent) && parent.ValueKind == JsonValueKind.Object && Number(parent, "number") is > 0 and var number
                ? number.ToString(CultureInfo.InvariantCulture)
                : null,
            When(row, "closedAt") is var closed && closed != default ? closed : null
        );

    static BranchPullRequest BranchPull(JsonElement node) =>
        new(
            Number(node, "number").ToString(CultureInfo.InvariantCulture),
            Text(node, "headRefName") ?? string.Empty,
            When(node, "mergedAt") is var merged && merged != default ? merged : null,
            [
                .. Nodes(node, "commits")
                    .Select(each => each.GetProperty("commit"))
                    .Select(commit => new BranchCommit(
                        Text(commit, "oid") ?? string.Empty,
                        When(commit, "committedDate"),
                        [.. Nodes(commit, "authors").Select(author => Text(author, "name")).OfType<string>()],
                        commit.TryGetProperty("committer", out var committer) && committer.ValueKind == JsonValueKind.Object ? Text(committer, "name") : null,
                        CommitMark.TrailersOf(Text(commit, "message"))
                    )),
            ]
        );
}
