using System.Text;
using System.Text.RegularExpressions;
using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Modules.Pbis.Features.Inbox;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Pbis.Features.Vendor;

/// <summary>
/// The Proposal tab (DESIGN.md#the-screens, #512): the OpenSpec change on the PBI's branch, read
/// from the vendor at its pull request's head without a worktree. Proposal, design, the spec deltas
/// and tasks, each as written; a spec delta beside the capability's spec on the default branch, so
/// it reads against master; tasks with their count; and the review comments on the change's files,
/// which are the threads a person anchors to a line.
/// <para>
/// A repository that does not plan in OpenSpec has its plan in the issue (#726): <c>/aio:propose</c>
/// writes it as a comment headed <c>## Plan</c>, and only this repository's <c>.aio/context.md</c>
/// asks for a change instead. So with no change on the pull request, or no pull request, the plan is
/// the latest such comment, with the earlier ones counted: a second pass writes a new one rather
/// than editing the first.
/// </para>
/// <para>
/// A PBI with neither is an answer with the reason in <c>Missing</c>, not a failure: before
/// <c>spec-ready</c> ends there is nothing to read.
/// </para>
/// </summary>
sealed partial class ReadProposal : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/pbi/proposal",
                async (string repository, string id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Query(repository, id), cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<ProposalPage>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(ReadProposal))
            .WithTags("Pbis");

    /// <param name="PullRequestId">The pull request the change is read from, or null when there is none yet.</param>
    /// <param name="Head">The commit the files are read at.</param>
    /// <param name="Change">The change's name, or null when the pull request carries none.</param>
    /// <param name="Root">The change's folder, relative to the repository root.</param>
    /// <param name="Missing">Why there is no change to show, or null when there is one.</param>
    /// <param name="Comments">The review comments on the change's files, oldest first.</param>
    /// <param name="Plan">The issue's latest <c>## Plan</c> comment, when there is no change to show.</param>
    /// <param name="EarlierPlans">The <c>## Plan</c> comments before it, oldest first.</param>
    internal sealed record ProposalPage(
        string? PullRequestId,
        string? PullRequestUrl,
        string? Head,
        string? Change,
        string? Root,
        string? Missing,
        IReadOnlyList<ProposalFile> Files,
        IReadOnlyList<ProposalComment> Comments,
        ProposalPlan? Plan,
        IReadOnlyList<ProposalPlan> EarlierPlans
    );

    /// <param name="Url">Where a person opens the comment at the vendor, when the vendor said.</param>
    /// <param name="Images">The vendor attachments in the body, which <c>/api/pbi/attachment</c> serves.</param>
    internal sealed record ProposalPlan(string Id, string? Author, DateTimeOffset CreatedAt, string Body, string? Url, IReadOnlyList<string> Images);

    /// <param name="Path">Relative to the repository root, as a review comment names it.</param>
    /// <param name="Name">Relative to the change's folder.</param>
    /// <param name="Kind"><c>proposal</c>, <c>design</c>, <c>spec</c>, <c>tasks</c>, <c>image</c> or <c>other</c>.</param>
    /// <param name="Text">The file as written, or null for an image, whose bytes are <see cref="ReadProposalFile"/>'s.</param>
    /// <param name="Capability">The capability a spec delta changes.</param>
    /// <param name="Master">The capability's spec on the default branch, or null when the delta adds a new one.</param>
    /// <param name="Delta">How many requirements a spec delta adds, modifies, removes and renames.</param>
    /// <param name="Tasks">How many tasks <c>tasks.md</c> has and how many are done.</param>
    internal sealed record ProposalFile(
        string Path,
        string Name,
        string Kind,
        string? Text,
        string? Capability,
        string? Master,
        ProposalDelta? Delta,
        ProposalTasks? Tasks
    );

    internal sealed record ProposalDelta(int Added, int Modified, int Removed, int Renamed);

    internal sealed record ProposalTasks(int Done, int Total);

    /// <param name="Line">The line in the file at the head, or null when the code it was written on has changed since.</param>
    internal sealed record ProposalComment(string Id, string Path, int? Line, string? Author, DateTimeOffset CreatedAt, string Body);

    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Query(string Repository, string Id) : IQuery<ErrorOr<ProposalPage>>;

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Repository).NotEmpty();
            RuleFor(query => query.Id).NotEmpty();
        }
    }

    internal sealed class Handler(IFolderDirectory folders, IEnumerable<IPbiProvider> providers) : IAppQueryHandler<Query, ErrorOr<ProposalPage>>
    {
        public async Task<ErrorOr<ProposalPage>> Handle(Query query, CancellationToken cancellationToken)
        {
            var vendor = await VendorOf.Find(folders, providers, query.Repository, cancellationToken);
            if (vendor.IsError)
            {
                return vendor.Errors;
            }

            var (folder, provider) = vendor.Value;
            var pbi = new PbiRef(folder.Repository, query.Id);
            try
            {
                // Two waves (#621): the change with its pull request and its threads, then every file
                // and every spec they change in one read. The patches are the Review tab's, and not
                // asked for here.
                var files = await provider.GetPullRequestChangeAsync(pbi, cancellationToken);
                var pull = files?.PullRequest;
                if (pull is null || files is null || ProposalChange.Of(files) is not { } change)
                {
                    var plans = await Plans(provider, pbi, cancellationToken);
                    var missing = plans.Length > 0 ? null
                        : pull is null ? $"#{query.Id} has no pull request yet and no ## Plan comment. The engine opens the pull request when spec-ready ends, and /aio:propose writes the plan."
                        : $"#{query.Id} has no OpenSpec change on pull request #{pull.Id} and no ## Plan comment yet. /aio:propose writes one of the two.";
                    return new ProposalPage(pull?.Id, pull?.Url.ToString(), files?.HeadSha, null, null, missing, [], [], plans.LastOrDefault(), plans.Length > 1 ? plans[..^1] : []);
                }

                var read = await Read(provider, folder.Repository, files.HeadSha, change, cancellationToken);
                var comments = files.Comments;

                return new ProposalPage(
                    pull.Id,
                    pull.Url.ToString(),
                    files.HeadSha,
                    change.Name,
                    change.Root,
                    null,
                    read,
                    [
                        .. comments
                            .Where(comment => comment.Path.StartsWith(change.Root, StringComparison.Ordinal))
                            .OrderBy(comment => comment.CreatedAt)
                            .Select(comment => new ProposalComment(comment.Id, comment.Path, comment.Line, comment.Author, comment.CreatedAt, comment.Body)),
                    ],
                    null,
                    []
                );
            }
            catch (PbiProviderException failure)
            {
                return VendorOf.Unread(failure);
            }
        }

        /// <summary>The issue's comments headed <c>## Plan</c>, oldest first.</summary>
        static async Task<ProposalPlan[]> Plans(IPbiProvider provider, PbiRef pbi, CancellationToken ct) =>
            (await provider.ReadThreadAsync(pbi, ct))?.Comments
                .Where(comment => PlanHeading().IsMatch(comment.Body))
                .OrderBy(comment => comment.CreatedAt)
                .Select(comment => new ProposalPlan(
                    comment.Id,
                    comment.Author,
                    comment.CreatedAt,
                    comment.Body,
                    comment.Url?.ToString(),
                    [.. IssueAttachments.In(comment.Body).Select(found => found.Url)]
                ))
                .ToArray() ?? [];

        /// <summary>
        /// Every file of the change at the head, but its images, and the default branch's spec of each
        /// capability a delta changes, in one read of the vendor.
        /// </summary>
        static async Task<ProposalFile[]> Read(IPbiProvider provider, string repository, string head, ProposalChange.Found change, CancellationToken ct)
        {
            var rows = change
                .Files.Select(file =>
                {
                    var name = file.Path[change.Root.Length..];
                    var kind = ProposalChange.Kind(name);
                    return (file.Path, Name: name, Kind: kind, Capability: kind == "image" ? null : ProposalChange.Capability(name));
                })
                .ToArray();

            List<RepositoryFile> wanted = [];
            foreach (var row in rows.Where(row => row.Kind != "image"))
            {
                wanted.Add(new RepositoryFile(head, row.Path));
                if (row.Capability is not null)
                {
                    wanted.Add(new RepositoryFile(null, $"openspec/specs/{row.Capability}/spec.md"));
                }
            }

            var read = new Dictionary<RepositoryFile, string?>();
            foreach (var (file, bytes) in wanted.Zip(await provider.ReadFilesAsync(repository, wanted, ct)))
            {
                read[file] = bytes is null ? null : Encoding.UTF8.GetString(bytes);
            }

            return
            [
                .. rows.Select(row =>
                {
                    if (row.Kind == "image")
                    {
                        return new ProposalFile(row.Path, row.Name, row.Kind, null, null, null, null, null);
                    }

                    var text = read[new RepositoryFile(head, row.Path)];
                    var master = row.Capability is null ? null : read[new RepositoryFile(null, $"openspec/specs/{row.Capability}/spec.md")];
                    return new ProposalFile(
                        row.Path,
                        row.Name,
                        row.Kind,
                        text,
                        row.Capability,
                        master,
                        row.Kind == "spec" && text is not null ? Delta(text) : null,
                        row.Kind == "tasks" && text is not null ? Tasks(text) : null
                    );
                }),
            ];
        }
    }

    /// <summary>
    /// The requirements under each of a delta's sections: <c>## ADDED Requirements</c>,
    /// <c>## MODIFIED Requirements</c>, <c>## REMOVED Requirements</c> and <c>## RENAMED Requirements</c>,
    /// as OpenSpec writes them.
    /// </summary>
    internal static ProposalDelta Delta(string text)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        string? section = null;
        foreach (var line in text.ReplaceLineEndings("\n").Split('\n'))
        {
            if (Section().Match(line) is { Success: true } heading)
            {
                section = heading.Groups["kind"].Value;
            }
            else if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                section = null;
            }
            else if (section is not null && (line.StartsWith("### Requirement:", StringComparison.Ordinal) || (section.Equals("RENAMED", StringComparison.OrdinalIgnoreCase) && line.TrimStart().StartsWith("- FROM:", StringComparison.Ordinal))))
            {
                counts[section] = counts.GetValueOrDefault(section) + 1;
            }
        }

        return new ProposalDelta(
            counts.GetValueOrDefault("ADDED"),
            counts.GetValueOrDefault("MODIFIED"),
            counts.GetValueOrDefault("REMOVED"),
            counts.GetValueOrDefault("RENAMED")
        );
    }

    /// <summary>The checkboxes of <c>tasks.md</c>, and how many are ticked.</summary>
    internal static ProposalTasks Tasks(string text)
    {
        var boxes = Checkbox().Matches(text);
        return new ProposalTasks(boxes.Count(box => box.Groups["mark"].Value is "x" or "X"), boxes.Count);
    }

    /// <summary><c>## Plan</c> as the comment's first line, as <c>/aio:propose</c> writes it.</summary>
    [GeneratedRegex(@"^\s*##\s+Plan\s*(\r?\n|$)")]
    private static partial Regex PlanHeading();

    [GeneratedRegex(@"^##\s+(?<kind>ADDED|MODIFIED|REMOVED|RENAMED)\s+Requirements\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex Section();

    [GeneratedRegex(@"^\s*[-*]\s+\[(?<mark>[ xX])\]", RegexOptions.Multiline)]
    private static partial Regex Checkbox();
}
