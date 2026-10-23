using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.Modules.Loop.Engine;
using AiOrchestrator.Modules.Loop.Persistence;
using AiOrchestrator.Modules.Loop.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.Modules.Loop.Features;

/// <summary>
/// One pass of the loop engine over every folder (#505): observe, decide, apply, and write each
/// decision to the journal. Answers what the pass wrote. <see cref="LoopSchedule"/> sends it every
/// interval (#508), over the folders that are on (#562); a person or a test can run one pass by hand,
/// which walks every folder whether it is on or not, as a pass by hand always has.
/// </summary>
sealed class RunOnce : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/loop/run-once",
                async (ISender sender, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await sender.Send(new Command(), cancellationToken))
            )
            .WithName(nameof(RunOnce))
            .WithTags("Loop");

    /// <param name="Folders">Folders whose loop the pass followed.</param>
    /// <param name="Pbis">Open PBIs it looked at.</param>
    /// <param name="Skipped">Each folder it could not follow, and why.</param>
    /// <param name="Written">The journal lines it wrote, oldest first.</param>
    internal sealed record Response(int Folders, int Pbis, IReadOnlyList<string> Skipped, IReadOnlyList<ReadJournal.Line> Written);

    /// <param name="OnlyFoldersOn">Walk only the folders whose engine is on, as the schedule does.</param>
    // Nothing to scope to: the loop spans every folder, and this host has one person (AddIdentity).
    [Requires(Access.AnyCaller)]
    internal sealed record Command(bool OnlyFoldersOn = false) : ICommand<Response>;

    internal sealed class Handler(
        IEnumerable<IPbiProvider> providers,
        IRunRequests runs,
        IScriptRunner scripts,
        IEnumerable<IAgentProvider> agents,
        IFolderDirectory folders,
        LoopDbContext database,
        Passes passes,
        ControlSettings settings,
        ScriptOutputs outputs,
        TimeProvider clock,
        ILogger<Handler> logger
    ) : IAppCommandHandler<Command, Response>
    {
        public async Task<Response> Handle(Command command, CancellationToken cancellationToken)
        {
            var on = command.OnlyFoldersOn ? await settings.ReadAsync(cancellationToken) : null;
            var pass = await passes.OneAtATime(
                () =>
                    new LoopEngine(providers, runs, folders, database, passes, clock, logger, scripts, agents, outputs).RunOnceAsync(
                        on is null ? null : folder => on.IsOn(folder.Repository),
                        cancellationToken
                    ),
                clock.GetUtcNow(),
                cancellationToken
            );

            return new Response(pass.Folders, pass.Pbis, pass.Skipped, [.. pass.Written.Select(ReadJournal.Line.Of)]);
        }
    }
}
