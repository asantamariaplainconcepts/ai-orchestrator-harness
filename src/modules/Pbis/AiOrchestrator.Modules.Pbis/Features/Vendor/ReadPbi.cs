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
/// One PBI and its pull request, read from the vendor now: the header of the PBI screen
/// (DESIGN.md#the-screens). Its runs are <c>GET /api/runs</c> and the engine's decisions
/// about it are <c>GET /api/loop/journal</c>, each from the module that owns them.
/// <para>
/// The PBI is named by query, <c>?repository=owner/name&amp;id=7</c>, because a repository has a
/// slash on GitHub and none on Azure DevOps.
/// </para>
/// </summary>
sealed class ReadPbi : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/pbi",
                async (string repository, string id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Query(repository, id), cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<PbiDetail>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(ReadPbi))
            .WithTags("Pbis");

    /// <param name="PullRequest">The pull request that closes it, or null when there is none yet.</param>
    internal sealed record PbiDetail(VendorPbi Pbi, VendorPullRequest? PullRequest);

    /// <param name="Id">The vendor's id: the pull request number on GitHub.</param>
    /// <param name="Url">Where a person reviews it.</param>
    internal sealed record VendorPullRequest(string Id, string Url, bool IsDraft, bool IsMerged, bool IsApproved, bool IsClosed);

    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Query(string Repository, string Id) : IQuery<ErrorOr<PbiDetail>>;

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Repository).NotEmpty();
            RuleFor(query => query.Id).NotEmpty();
        }
    }

    internal sealed class Handler(IFolderDirectory folders, IEnumerable<IPbiProvider> providers) : IAppQueryHandler<Query, ErrorOr<PbiDetail>>
    {
        public async Task<ErrorOr<PbiDetail>> Handle(Query query, CancellationToken cancellationToken)
        {
            var folder = (await folders.ListAsync(cancellationToken)).FirstOrDefault(candidate =>
                string.Equals(candidate.Repository, query.Repository, StringComparison.OrdinalIgnoreCase)
            );
            if (folder is null)
            {
                return Error.NotFound("Pbi.NoFolder", $"No folder is a checkout of {query.Repository}. Add one from the rail.");
            }

            if (providers.FirstOrDefault(provider => provider.Name == folder.Vendor) is not { } provider)
            {
                return Error.NotFound("Pbi.NoAdapter", $"No adapter reads {folder.Vendor}.");
            }

            var reference = new PbiRef(folder.Repository, query.Id);
            try
            {
                // #617. Both asked at once: in sequence they cost a second on 10 October, the PBI one
                // call to the vendor and its pull request two. A PBI the vendor does not have is
                // NotFound whatever the pull request's read answered.
                var reading = provider.ReadThreadAsync(reference, cancellationToken);
                var pulling = provider.GetPullRequestAsync(reference, cancellationToken);
                if ((await reading)?.AsPbi is not { } pbi)
                {
                    return Error.NotFound("Pbi.NotFound", $"{folder.Repository} has no PBI {query.Id}.");
                }

                var pull = await pulling;

                return new PbiDetail(
                    VendorPbi.Of(folder, pbi),
                    pull is null ? null : new VendorPullRequest(pull.Id, pull.Url.ToString(), pull.IsDraft, pull.IsMerged, pull.IsApproved, pull.IsClosed)
                );
            }
            catch (PbiProviderException failure)
            {
                return Error.Conflict("Pbi.VendorUnread", failure.Message);
            }
        }
    }
}
