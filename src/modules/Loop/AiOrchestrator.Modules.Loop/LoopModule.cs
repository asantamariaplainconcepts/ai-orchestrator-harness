using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.Modules.Loop.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiOrchestrator.Modules.Loop;

/// <summary>
/// The loop engine: reads <c>.aio/loop.yaml</c>, observes each PBI, decides its next step,
/// applies it, and keeps the journal of every decision. It reaches the vendor and the scripts
/// through the ports and asks <c>Modules.Runs</c> for a run through <c>IRunRequests</c>.
/// <para>
/// #498 gave it <c>Definition/</c>, the loop file read and validated; #505 the engine in
/// <c>Engine/</c>, its journal and the two use cases, <c>RunOnce</c> and <c>ReadJournal</c>. It is
/// the only loop: the first tree's watcher, its <c>LoopEngine</c> and <c>Coordination</c> went at #518.
/// </para>
/// </summary>
public sealed class LoopModule : ModuleBase
{
    public const string ConnectionStringName = "aiorchestratordb";

    public override string Name => "Loop";

    public override async Task Migrate(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<LoopDbContext>().Database.MigrateAsync(cancellationToken);
    }

    public override void Add(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<LoopDbContext>(options =>
            options.UseSqlite(
                configuration.GetConnectionString(ConnectionStringName),
                // One file holds every module, so each keeps its own history table (ADR-0003).
                sqlite => sqlite.MigrationsHistoryTable("__EFMigrationsHistory_" + LoopDbContext.Schema)
            )
        );

        services.Configure<LoopSettings>(configuration.GetSection(LoopSettings.Section));

        services.TryAddScoped<IRunRequests, RunsNotWired>();

        // #508. The engine on its own, and what the Loop screen's strip reads about it. Off unless
        // Loop:Enabled says otherwise; see LoopSettings. #562: what a person changed in the console
        // wins over that configuration, read on every pass.
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<Settings.ControlSettings>();
        services.AddSingleton<Engine.Passes>();
        // #638. Where each script's output is kept, read from the same connection string as the journal.
        services.AddSingleton<Engine.ScriptOutputs>();
        services.AddHostedService<Engine.LoopSchedule>();

        // #511. A person's release and move from the PBI's header, decided and applied by the engine.
        services.AddScoped<Features.PersonDecisions>();

        // #697. A run that ended writes its PBI's journey comment again, with the path of its recording.
        services.AddScoped<IRunEndings, Engine.RunEndings>();
    }
}
