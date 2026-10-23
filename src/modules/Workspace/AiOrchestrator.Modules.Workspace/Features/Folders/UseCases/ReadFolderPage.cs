using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Files;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Modules.Workspace.Features.Harness;
using AiOrchestrator.Modules.Workspace.Persistence;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Workspace.Features.Folders.UseCases;

/// <summary>
/// The folder page (#565): how one checkout is set up for the loop. Its loop file as written, the
/// harness version it has against the one this build carries, each file of the package and whether
/// the checkout has it, and the branch git reads now. No PBI, run or file of the work is here.
/// <para>
/// Everything is read from disk on every request, because the page polls it: a <c>loop.yaml</c>
/// broken in an editor shows on the next read without a reload. The loop is validated by Loop's
/// <c>POST /api/loop/validate</c>, since this module cannot read that module's rules.
/// </para>
/// </summary>
sealed class ReadFolderPage : IUseCase
{
    const string LoopFile = "loop.yaml";
    const string VersionFile = "version";
    const string GateScript = ".aio/gate";
    const string GitHub = "github.com/";

    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/folders/{id:guid}",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Query(id), cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<FolderPage>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName(nameof(ReadFolderPage))
            .WithTags("Workspace");

    /// <param name="Path">The checkout, absolute.</param>
    /// <param name="Vendor">The vendor its PBIs come from, such as <c>github</c>, or null.</param>
    /// <param name="Repository">The repository at that vendor, such as <c>owner/name</c>, or null.</param>
    /// <param name="Branch">The branch git reads now, or null when git does not describe the folder.</param>
    /// <param name="LoopYaml">Its <c>.aio/loop.yaml</c> as written, or null when it has none.</param>
    /// <param name="HarnessVersion">The first line of its <c>.aio/version</c>, or null when the harness is not installed.</param>
    /// <param name="ShippedVersion">The harness version this build carries.</param>
    /// <param name="Harness">Each file and link of the package, ordered by path.</param>
    /// <param name="Trust">
    /// Each agent CLI that asks to trust a folder before it starts, and whether it trusts this one
    /// (#606), as the CLI keeps it. The page shows the ones the folder's loop runs.
    /// </param>
    internal sealed record FolderPage(
        Guid Id,
        string Name,
        string Path,
        string? Vendor,
        string? Repository,
        string? Branch,
        string DefaultAgent,
        string? DefaultModel,
        string? LoopYaml,
        string? HarnessVersion,
        string ShippedVersion,
        IReadOnlyList<FolderHarnessFile> Harness,
        IReadOnlyList<FolderTrust> Trust
    );

    /// <param name="Provider">The CLI, as <c>loop.yaml</c> spells it, such as <c>claude-code</c>.</param>
    /// <param name="Trusted">Whether a run starts in the folder without a question nobody answers.</param>
    /// <param name="Why">The sentence the page shows.</param>
    /// <param name="Remedy">The command that answers the question once, or null when trusted.</param>
    internal sealed record FolderTrust(string Provider, bool Trusted, string Why, string? Remedy);

    /// <param name="Path">From the repository's top, with forward slashes.</param>
    /// <param name="Present">Whether the checkout has it, as a file, a directory or a link.</param>
    /// <param name="Stub">
    /// The gate still byte for byte as the package installs it, which fails every PBI in
    /// <c>verifying</c> until the repository says how it is tested. False for every other file.
    /// </param>
    internal sealed record FolderHarnessFile(string Path, bool Present, bool Stub);

    [Requires(WorkspacePermissions.Read)]
    internal sealed record Query(Guid FolderId) : IQuery<ErrorOr<FolderPage>>, IScopedToProject
    {
        public Guid ProjectId => FolderId;
    }

    internal sealed class Handler(WorkspaceDbContext database, ICheckoutProbe probe, IEnumerable<IAgentProvider> agents) : IAppQueryHandler<Query, ErrorOr<FolderPage>>
    {
        public async Task<ErrorOr<FolderPage>> Handle(Query query, CancellationToken cancellationToken)
        {
            var folder = await database.Folders.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == query.FolderId, cancellationToken);
            if (folder is null)
            {
                return FolderErrors.NotFound(query.FolderId);
            }

            var root = LocalPath.Expand(folder.Path);
            var repository = RepositoryIdentity.Of(folder.RemoteUrl);
            var github = repository?.StartsWith(GitHub, StringComparison.Ordinal) == true;
            var checkout = folder.IsRepository ? await probe.ReadAsync(root, cancellationToken) : null;

            return new FolderPage(
                folder.Id,
                folder.Name,
                root,
                github ? "github" : null,
                github ? repository![GitHub.Length..] : null,
                checkout?.Branch,
                folder.DefaultAgentId,
                folder.DefaultModel,
                await Text(HarnessScripts.Own(root, LoopFile), cancellationToken),
                (await Text(HarnessScripts.Own(root, VersionFile), cancellationToken))?.Split('\n', 2)[0].Trim(),
                HarnessPackage.Version,
                [.. HarnessPackage.Files.Select(file => Has(root, file))],
                [
                    .. agents
                        .Select(agent => (agent.Name, Trust: agent.Trust(root)))
                        .Where(pair => pair.Trust is not null)
                        .Select(pair => new FolderTrust(pair.Name, pair.Trust!.Trusted, pair.Trust.Why, pair.Trust.Remedy))
                        .OrderBy(trust => trust.Provider, StringComparer.Ordinal),
                ]
            );
        }

        static async Task<string?> Text(string path, CancellationToken ct)
        {
            return File.Exists(path) ? await File.ReadAllTextAsync(path, ct) : null;
        }

        static FolderHarnessFile Has(string root, HarnessFile file)
        {
            var path = System.IO.Path.Combine(root, file.Path);
            // A link counts when it is there, even dangling: the link is what the package writes.
            var present = File.Exists(path) || Directory.Exists(path) || new FileInfo(path).LinkTarget is not null;
            var stub = present && file.Path == GateScript && File.ReadAllBytes(path).AsSpan().SequenceEqual(file.Content);
            return new FolderHarnessFile(file.Path, present, stub);
        }
    }
}
