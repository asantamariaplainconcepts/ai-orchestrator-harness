using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Modules.Workspace.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Workspace.Features.Folders.UseCases;

/// <summary>
/// The folders as Settings shows them (DESIGN.md#the-screens): each checkout, the
/// repository its PBIs come from, and its loop's YAML as the file has it. A folder the loop cannot
/// walk is listed too, with what it lacks, because that is what the row is for.
/// </summary>
sealed class ListLoopFolders : IUseCase
{
    const string LoopFile = "loop.yaml";
    const string GitHub = "github.com/";

    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/folders",
                async (ISender sender, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await sender.Send(new Query(), cancellationToken))
            )
            .WithName(nameof(ListLoopFolders))
            .WithTags("Workspace");

    /// <param name="Path">The checkout, absolute.</param>
    /// <param name="Vendor">The vendor its PBIs come from, such as <c>github</c>, or null when the loop cannot read its origin.</param>
    /// <param name="Repository">The repository at that vendor, such as <c>owner/name</c>, or null.</param>
    /// <param name="LoopYaml">Its <c>.aio/loop.yaml</c> as written, or null when it has none.</param>
    /// <param name="DefaultAgent">The agent a session here starts with, as <c>loop.yaml</c> names a provider.</param>
    /// <param name="DefaultModel">The model it starts with, or null for the agent's own default.</param>
    /// <param name="HasGate">Whether the checkout has a <c>gate</c> script, which the loop's <c>verifying</c> runs: <c>.aio/gate</c>, or where a harness before 24 has it.</param>
    internal sealed record LoopFolderRow(
        Guid Id,
        string Name,
        string Path,
        string? Vendor,
        string? Repository,
        string? LoopYaml,
        string DefaultAgent,
        string? DefaultModel,
        bool HasGate
    );

    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Query : IQuery<IReadOnlyList<LoopFolderRow>>;

    internal sealed class Handler(WorkspaceDbContext database) : IAppQueryHandler<Query, IReadOnlyList<LoopFolderRow>>
    {
        public async Task<IReadOnlyList<LoopFolderRow>> Handle(Query query, CancellationToken cancellationToken)
        {
            var folders = await database
                .Folders.AsNoTracking()
                .OrderBy(folder => folder.Path)
                .Select(folder => new
                {
                    folder.Id,
                    folder.Name,
                    folder.Path,
                    folder.RemoteUrl,
                    folder.DefaultAgentId,
                    folder.DefaultModel,
                })
                .ToListAsync(cancellationToken);

            var rows = new List<LoopFolderRow>();
            foreach (var folder in folders)
            {
                var path = LocalPath.Expand(folder.Path);
                var repository = RepositoryIdentity.Of(folder.RemoteUrl);
                var github = repository?.StartsWith(GitHub, StringComparison.Ordinal) == true;
                var loop = HarnessScripts.Own(path, LoopFile);

                rows.Add(
                    new LoopFolderRow(
                        folder.Id,
                        folder.Name,
                        path,
                        github ? "github" : null,
                        github ? repository![GitHub.Length..] : null,
                        File.Exists(loop) ? await File.ReadAllTextAsync(loop, cancellationToken) : null,
                        folder.DefaultAgentId,
                        folder.DefaultModel,
                        HarnessScripts.Find(path, "gate") is not null
                    )
                );
            }

            return rows;
        }
    }
}
