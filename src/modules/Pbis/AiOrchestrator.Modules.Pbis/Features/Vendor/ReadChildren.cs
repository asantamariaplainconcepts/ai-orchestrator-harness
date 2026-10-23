using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Ports;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Pbis.Features.Vendor;

/// <summary>
/// #611. The children of an issue as the vendor has them now, each with its state, its hold and
/// whether it is closed: what the Issue tab paints on the epic's graph. Whether one is running or
/// waiting is the loop's to say, and the screen reads that from the loop's own routes.
/// </summary>
sealed class ReadChildren : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/pbi/children",
                async (string repository, string id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Query(repository, id), cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<IReadOnlyList<IssueChild>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(ReadChildren))
            .WithTags("Pbis");

    /// <param name="Repository">Where it lives, which for a sub-issue may be another repository.</param>
    /// <param name="State">Its <c>loop.yaml</c> state, or null when it carries none.</param>
    internal sealed record IssueChild(string Repository, string Id, string Title, string? State, bool Held, bool Closed);

    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Query(string Repository, string Id) : IQuery<ErrorOr<IReadOnlyList<IssueChild>>>;

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Repository).NotEmpty();
            RuleFor(query => query.Id).NotEmpty();
        }
    }

    internal sealed class Handler(IFolderDirectory folders, IEnumerable<IPbiProvider> providers)
        : IAppQueryHandler<Query, ErrorOr<IReadOnlyList<IssueChild>>>
    {
        public async Task<ErrorOr<IReadOnlyList<IssueChild>>> Handle(Query query, CancellationToken cancellationToken)
        {
            var vendor = await VendorOf.Find(folders, providers, query.Repository, cancellationToken);
            if (vendor.IsError)
            {
                return vendor.Errors;
            }

            IReadOnlyList<Pbi> children;
            try
            {
                children = await vendor.Value.Provider.ListChildrenAsync(new PbiRef(vendor.Value.Folder.Repository, query.Id), cancellationToken);
            }
            catch (PbiProviderException failure)
            {
                return VendorOf.Unread(failure);
            }

            IReadOnlyList<IssueChild> rows =
            [
                .. children.Select(child => new IssueChild(child.Ref.Repository, child.Ref.Id, child.Title, child.State, child.Held, child.Closed)),
            ];
            return rows.ToErrorOr();
        }
    }
}
