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
/// The Issue tab (DESIGN.md#the-screens, #512): the issue as the vendor has it now, its body and
/// every comment in order, and what it says about itself. Nothing is copied or summarised; the tab
/// shows what is at the vendor.
/// <para>
/// A comment the harness wrote is marked with the kind its record names, read from the
/// <c>&lt;!-- aio:&lt;kind&gt; --&gt;</c> marker the scripts leave in it (<c>aio:evidence</c> from
/// <c>.aio/steps/evidence</c>, <c>aio:state</c> from the session report, <c>aio:journey</c> from the
/// engine, which the Journey reads when its own database has nothing, #696), so the screen can
/// show it as what it is. Images are listed with the comment they are in; their bytes come through
/// <see cref="ReadIssueAsset"/>, because the browser has no credential for them.
/// </para>
/// </summary>
sealed partial class ReadIssue : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/pbi/issue",
                async (string repository, string id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Query(repository, id), cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<IssuePage>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(ReadIssue))
            .WithTags("Pbis");

    /// <param name="Url">Where a person opens it at the vendor, which is where its body is edited.</param>
    /// <param name="Author">Who opened it, or null for an account that no longer exists.</param>
    /// <param name="Body">The vendor's markdown, as written.</param>
    /// <param name="Labels">Every label, the state's and the hold's among them.</param>
    /// <param name="Images">Every image in the body and the comments, in that order.</param>
    internal sealed record IssuePage(
        string Url,
        string? Author,
        DateTimeOffset CreatedAt,
        string Body,
        IReadOnlyList<string> Labels,
        IReadOnlyList<string> Assignees,
        IssueLink? Parent,
        IReadOnlyList<IssueLink> BlockedBy,
        IReadOnlyList<IssueComment> Comments,
        IReadOnlyList<IssueImage> Images
    );

    /// <param name="Harness">The kind of record the harness wrote it as, such as <c>evidence</c>, or null for a person's comment.</param>
    internal sealed record IssueComment(string Id, string? Author, DateTimeOffset CreatedAt, string Body, string? Url, string? Harness);

    internal sealed record IssueLink(string Repository, string Id, string Title, string Url, bool Closed);

    /// <param name="Name">The alt or link text, empty where the markup gave none.</param>
    /// <param name="CommentId">The comment it is in, or null for the body.</param>
    internal sealed record IssueImage(string Url, string Name, string? CommentId);

    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Query(string Repository, string Id) : IQuery<ErrorOr<IssuePage>>;

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Repository).NotEmpty();
            RuleFor(query => query.Id).NotEmpty();
        }
    }

    internal sealed class Handler(IFolderDirectory folders, IEnumerable<IPbiProvider> providers) : IAppQueryHandler<Query, ErrorOr<IssuePage>>
    {
        public async Task<ErrorOr<IssuePage>> Handle(Query query, CancellationToken cancellationToken)
        {
            var vendor = await VendorOf.Find(folders, providers, query.Repository, cancellationToken);
            if (vendor.IsError)
            {
                return vendor.Errors;
            }

            PbiThread? thread;
            try
            {
                thread = await vendor.Value.Provider.ReadThreadAsync(new PbiRef(vendor.Value.Folder.Repository, query.Id), cancellationToken);
            }
            catch (PbiProviderException failure)
            {
                return VendorOf.Unread(failure);
            }

            if (thread is null)
            {
                return Error.NotFound("Pbi.NotFound", $"{query.Repository} has no PBI {query.Id}.");
            }

            return Page(thread);
        }
    }

    internal static IssuePage Page(PbiThread thread) =>
        new(
            thread.Url.ToString(),
            thread.Author,
            thread.CreatedAt,
            thread.Body,
            thread.Labels,
            thread.Assignees,
            thread.Parent is { } parent ? Link(parent) : null,
            [.. thread.BlockedBy.Select(Link)],
            [
                .. thread.Comments.Select(comment => new IssueComment(
                    comment.Id,
                    comment.Author,
                    comment.CreatedAt,
                    comment.Body,
                    comment.Url?.ToString(),
                    Harness(comment.Body)
                )),
            ],
            [
                .. IssueAttachments.In(thread.Body).Select(found => new IssueImage(found.Url, found.Name, null)),
                .. thread.Comments.SelectMany(comment =>
                    IssueAttachments.In(comment.Body).Select(found => new IssueImage(found.Url, found.Name, comment.Id))
                ),
            ]
        );

    static IssueLink Link(LinkedPbi linked) =>
        new(linked.Ref.Repository, linked.Ref.Id, linked.Title, linked.Url.ToString(), linked.Closed);

    /// <summary>The kind of the first harness record in a comment, or null when a person wrote it.</summary>
    internal static string? Harness(string body) => Marker().Match(body) is { Success: true } found ? found.Groups["kind"].Value : null;

    [GeneratedRegex(@"<!--\s*aio:(?<kind>[a-z][a-z-]*)")]
    private static partial Regex Marker();
}
