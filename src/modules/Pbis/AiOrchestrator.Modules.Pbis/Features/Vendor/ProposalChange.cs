using System.Text.RegularExpressions;
using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.Modules.Pbis.Features.Vendor;

/// <summary>
/// Which OpenSpec change a PBI's pull request carries (#512): the folder under
/// <c>openspec/changes/</c> its files are in. One PBI, one change, one pull request, so the change
/// is the one the pull request adds; a change already archived on the branch is read from
/// <c>openspec/changes/archive/</c>. Read from the vendor at the pull request's head, which is the
/// commit a review comment anchors to.
/// </summary>
static partial class ProposalChange
{
    /// <param name="Name">The change's name, its folder.</param>
    /// <param name="Root">The folder, relative to the repository root, ending in a slash.</param>
    /// <param name="Files">The change's files the pull request carries, in reading order.</param>
    internal sealed record Found(string Name, string Root, IReadOnlyList<ChangedFile> Files);

    public static Found? Of(PullRequestChange pull)
    {
        var present = pull.Files.Where(file => file.Status != "removed").ToArray();
        var change =
            present.Select(file => Active().Match(file.Path)).FirstOrDefault(match => match.Success)
            ?? present.Select(file => Archived().Match(file.Path)).FirstOrDefault(match => match.Success);
        if (change is null)
        {
            return null;
        }

        var root = change.Groups["root"].Value;
        return new Found(
            change.Groups["name"].Value,
            root,
            [.. present.Where(file => file.Path.StartsWith(root, StringComparison.Ordinal)).OrderBy(file => Order(file.Path[root.Length..])).ThenBy(file => file.Path, StringComparer.Ordinal)]
        );
    }

    /// <summary>What a file of the change is: <c>proposal</c>, <c>design</c>, <c>spec</c>, <c>tasks</c>, <c>image</c> or <c>other</c>.</summary>
    public static string Kind(string name) =>
        name switch
        {
            "proposal.md" => "proposal",
            "design.md" => "design",
            "tasks.md" => "tasks",
            _ when Spec().IsMatch(name) => "spec",
            _ when Image().IsMatch(name) => "image",
            _ => "other",
        };

    /// <summary>The capability a spec delta changes, such as <c>loop</c> for <c>specs/loop/spec.md</c>.</summary>
    public static string? Capability(string name) => Spec().Match(name) is { Success: true } found ? found.Groups["capability"].Value : null;

    /// <summary>proposal, design, the spec deltas, tasks, then the rest: the order the change is read in.</summary>
    static int Order(string name) =>
        Kind(name) switch
        {
            "proposal" => 0,
            "design" => 1,
            "spec" => 2,
            "tasks" => 3,
            _ => 4,
        };

    [GeneratedRegex(@"^(?<root>openspec/changes/(?!archive/)(?<name>[^/]+)/)")]
    private static partial Regex Active();

    [GeneratedRegex(@"^(?<root>openspec/changes/archive/(?<name>[^/]+)/)")]
    private static partial Regex Archived();

    [GeneratedRegex(@"^specs/(?<capability>[^/]+)/spec\.md$")]
    private static partial Regex Spec();

    [GeneratedRegex(@"\.(png|jpe?g|gif|webp|svg)$", RegexOptions.IgnoreCase)]
    private static partial Regex Image();
}
