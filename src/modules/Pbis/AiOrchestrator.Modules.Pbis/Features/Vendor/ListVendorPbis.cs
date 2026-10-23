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
/// The open PBIs of every folder, with their state and hold, read from the vendor now (#508): what
/// the PBIs screen groups by state and the rail lists under each folder (DESIGN.md#the-screens).
/// <para>
/// Read through <see cref="IPbiProvider"/> and kept nowhere in this module, because the vendor is
/// the truth (principle 1); the adapter keeps a read a few seconds and forgets it on a write (#617). A folder whose vendor does not answer is named with the reason, and the
/// others are still listed.
/// </para>
/// </summary>
sealed class ListVendorPbis : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/pbis",
                async (ISender sender, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await sender.Send(new Query(), cancellationToken))
            )
            .WithName(nameof(ListVendorPbis))
            .WithTags("Pbis");

    /// <param name="Pbis">Every open PBI, folder by folder in path order, each in the vendor's order.</param>
    /// <param name="Unread">Each folder whose PBIs could not be read, and why.</param>
    internal sealed record PbiList(IReadOnlyList<VendorPbi> Pbis, IReadOnlyList<UnreadFolder> Unread);

    // Nothing to scope to: the list spans every folder, and this host has one person (AddIdentity).
    [Requires(Access.AnyCaller)]
    internal sealed record Query : IQuery<PbiList>;

    internal sealed class Handler(IFolderDirectory folders, IEnumerable<IPbiProvider> providers) : IAppQueryHandler<Query, PbiList>
    {
        public async Task<PbiList> Handle(Query query, CancellationToken cancellationToken)
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
                    var open = await provider.ListAsync(folder.Repository, new PbiQuery(), cancellationToken);
                    pbis.AddRange(open.Select(pbi => VendorPbi.Of(folder, pbi)));
                }
                catch (PbiProviderException failure)
                {
                    unread.Add(new UnreadFolder(folder.Path, folder.Repository, failure.Message));
                }
            }

            return new PbiList(pbis, unread);
        }
    }
}

/// <summary>A PBI as the vendor has it now, with the folder it is worked in.</summary>
/// <param name="Vendor">The vendor, such as <c>github</c>.</param>
/// <param name="Repository">The repository or project, such as <c>owner/name</c>.</param>
/// <param name="Id">The vendor's id: the issue number on GitHub.</param>
/// <param name="State">Its <c>loop.yaml</c> state, or null when it carries none.</param>
/// <param name="Held">Whether a person must act before the loop does. The reason is the hold's journal line.</param>
/// <param name="Assignee">Who it is assigned to, or null.</param>
/// <param name="FolderPath">The checkout its runs start from.</param>
sealed record VendorPbi(string Vendor, string Repository, string Id, string Title, string? State, bool Held, string? Assignee, bool Closed, string FolderPath)
{
    public static VendorPbi Of(LoopFolder folder, Pbi pbi) =>
        new(folder.Vendor, pbi.Ref.Repository, pbi.Ref.Id, pbi.Title, pbi.State, pbi.Held, pbi.Assignee, pbi.Closed, folder.Path);
}

/// <summary>A folder whose PBIs could not be read.</summary>
/// <param name="Why">The vendor's or the adapter's words.</param>
sealed record UnreadFolder(string FolderPath, string Repository, string Why);
