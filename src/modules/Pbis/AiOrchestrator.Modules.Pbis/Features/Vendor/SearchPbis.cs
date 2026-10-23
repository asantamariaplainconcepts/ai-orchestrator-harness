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
/// #634. <c>aio issue list --search</c>: the PBIs of a folder the vendor's own search finds, open,
/// closed or both, so a refine or an explore can tell whether the idea was already asked or shipped.
/// </summary>
sealed class SearchPbis : IUseCase
{
    /// <summary>The most a search answers: more than any agent reads before it narrows the words.</summary>
    internal const int Limit = 100;

    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/pbi/search",
                async (string repository, string text, string? state, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Query(repository, text, state ?? "open"), cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<IReadOnlyList<VendorPbi>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(SearchPbis))
            .WithTags("Pbis");

    /// <param name="Text">The words, in the vendor's search syntax.</param>
    /// <param name="State"><c>open</c>, <c>closed</c> or <c>all</c>.</param>
    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Query(string Repository, string Text, string State) : IQuery<ErrorOr<IReadOnlyList<VendorPbi>>>;

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Repository).NotEmpty();
            RuleFor(query => query.Text).NotEmpty();
            RuleFor(query => query.State)
                .Must(state => Enum.TryParse<PbiSearchState>(state, ignoreCase: true, out _))
                .WithMessage("The state is open, closed or all.");
        }
    }

    internal sealed class Handler(IFolderDirectory folders, IEnumerable<IPbiProvider> providers)
        : IAppQueryHandler<Query, ErrorOr<IReadOnlyList<VendorPbi>>>
    {
        public async Task<ErrorOr<IReadOnlyList<VendorPbi>>> Handle(Query query, CancellationToken cancellationToken)
        {
            var vendor = await VendorOf.Find(folders, providers, query.Repository, cancellationToken);
            if (vendor.IsError)
            {
                return vendor.Errors;
            }

            var (folder, provider) = vendor.Value;
            try
            {
                var found = await provider.ListAsync(
                    folder.Repository,
                    new PbiQuery(Enum.Parse<PbiSearchState>(query.State, ignoreCase: true), Limit, query.Text),
                    cancellationToken
                );
                IReadOnlyList<VendorPbi> rows = [.. found.Select(pbi => VendorPbi.Of(folder, pbi))];
                return rows.ToErrorOr();
            }
            catch (PbiProviderException failure)
            {
                return VendorOf.Unread(failure);
            }
        }
    }
}
