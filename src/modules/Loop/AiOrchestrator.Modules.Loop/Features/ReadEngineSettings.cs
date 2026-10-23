using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Loop.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Loop.Features;

/// <summary>
/// The engine's settings as the next pass will follow them (#562): the interval Settings shows, and
/// whether the engine is on for each folder it walks, which the Loop strip and the folder page switch.
/// A setting nobody changed reads as configured.
/// </summary>
sealed class ReadEngineSettings : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/loop/settings",
                async (ISender sender, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await sender.Send(new Query(), cancellationToken))
            )
            .WithName(nameof(ReadEngineSettings))
            .WithTags("Loop");

    /// <param name="IntervalSeconds">Seconds between two scheduled passes.</param>
    /// <param name="Folders">Each folder the engine walks, with its switch.</param>
    internal sealed record Response(int IntervalSeconds, IReadOnlyList<FolderSwitch> Folders);

    /// <param name="Path">The checkout, absolute.</param>
    /// <param name="Repository">The repository at the vendor, which the switch is kept by.</param>
    /// <param name="On">Whether the scheduled passes work this folder.</param>
    internal sealed record FolderSwitch(string Path, string Repository, bool On);

    // Nothing to scope to: the engine spans every folder, and this host has one person (AddIdentity).
    [Requires(Access.AnyCaller)]
    internal sealed record Query : IQuery<Response>;

    internal sealed class Handler(ControlSettings settings, IFolderDirectory folders) : IAppQueryHandler<Query, Response>
    {
        public async Task<Response> Handle(Query query, CancellationToken cancellationToken) =>
            await Read(settings, folders, cancellationToken);

        internal static async Task<Response> Read(ControlSettings settings, IFolderDirectory folders, CancellationToken ct)
        {
            var control = await settings.ReadAsync(ct);
            var listed = await folders.ListAsync(ct);
            return new Response(
                control.IntervalSeconds,
                [.. listed.Select(folder => new FolderSwitch(folder.Path, folder.Repository, control.IsOn(folder.Repository)))]
            );
        }
    }
}
