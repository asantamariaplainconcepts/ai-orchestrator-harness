using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Ports;
using ErrorOr;

namespace AiOrchestrator.Modules.Pbis.Features.Vendor;

/// <summary>
/// The folder a repository is checked out in and the adapter that reads its PBIs: what every read
/// and write of the Issue and Proposal tabs (#512) starts from, and refuses without.
/// </summary>
static class VendorOf
{
    public static async Task<ErrorOr<(LoopFolder Folder, IPbiProvider Provider)>> Find(
        IFolderDirectory folders,
        IEnumerable<IPbiProvider> providers,
        string repository,
        CancellationToken ct
    )
    {
        var folder = (await folders.ListAsync(ct)).FirstOrDefault(candidate =>
            string.Equals(candidate.Repository, repository, StringComparison.OrdinalIgnoreCase)
        );
        if (folder is null)
        {
            return Error.NotFound("Pbi.NoFolder", $"No folder is a checkout of {repository}. Add one from the rail.");
        }

        if (providers.FirstOrDefault(provider => provider.Name == folder.Vendor) is not { } provider)
        {
            return Error.NotFound("Pbi.NoAdapter", $"No adapter reads {folder.Vendor}.");
        }

        return (folder, provider);
    }

    /// <summary>A read the vendor could not answer, in its own words.</summary>
    public static Error Unread(PbiProviderException failure) => Error.Conflict("Pbi.VendorUnread", failure.Message);

    /// <summary>A write the vendor did not keep as it was meant, in the read-back's words.</summary>
    public static Error Refused(string mismatch) => Error.Conflict("Pbi.VendorRefused", mismatch);
}
