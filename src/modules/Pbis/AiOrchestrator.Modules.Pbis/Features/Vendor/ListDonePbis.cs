using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Ports;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Pbis.Features.Vendor;

/// <summary>
/// The PBIs the vendor has closed, the latest <see cref="Limit"/> of each folder (#515): what the
/// PBIs screen lists behind its Done filter and nowhere else (DESIGN.md#the-screens). A list of
/// its own rather than a flag on <see cref="ListVendorPbis"/>, because the rail and every other
/// screen read the open ones every 30 seconds and have no use for the closed.
/// </summary>
sealed class ListDonePbis : IUseCase
{
    /// <summary>How many closed PBIs of one folder the screen lists. History is the vendor's to keep.</summary>
    internal const int Limit = 100;

    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/pbis/done",
                async (ISender sender, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await sender.Send(new Query(), cancellationToken))
            )
            .WithName(nameof(ListDonePbis))
            .WithTags("Pbis");

    // Nothing to scope to: the list spans every folder, and this host has one person (AddIdentity).
    [Requires(Access.AnyCaller)]
    internal sealed record Query : IQuery<ListVendorPbis.PbiList>;

    internal sealed class Handler(IFolderDirectory folders, IEnumerable<IPbiProvider> providers)
        : IAppQueryHandler<Query, ListVendorPbis.PbiList>
    {
        public async Task<ListVendorPbis.PbiList> Handle(Query query, CancellationToken cancellationToken)
        {
            var pbis = new List<VendorPbi>();
            var unread = new List<UnreadFolder>();

            foreach (var folder in await folders.ListAsync(cancellationToken))
            {
                if (providers.FirstOrDefault(provider => provider.Name == folder.Vendor) is not { } provider)
                {
                    unread.Add(new UnreadFolder(folder.Path, folder.Repository, $"no adapter reads {folder.Vendor}"));
                    continue;
                }

                try
                {
                    var closed = await provider.ListAsync(folder.Repository, new PbiQuery(PbiSearchState.Closed, Limit), cancellationToken);
                    pbis.AddRange(closed.Select(pbi => VendorPbi.Of(folder, pbi)));
                }
                catch (PbiProviderException failure)
                {
                    unread.Add(new UnreadFolder(folder.Path, folder.Repository, failure.Message));
                }
            }

            return new ListVendorPbis.PbiList(pbis, unread);
        }
    }
}
