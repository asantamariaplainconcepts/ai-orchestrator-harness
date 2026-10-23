using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiOrchestrator.Modules.Runs;

/// <summary>
/// Runs â starting them, watching them, and the history of every one that finished â plus the
/// registry of agents that can execute them.
/// <para>
/// The agent registry lives here rather than in its own module for the same reason the connector
/// lives with Tasks: everything that reads one is a Runs concern. Nothing else in the product
/// needs to know what a CLI agent is.
/// </para>
/// </summary>
public sealed class RunsModule : ModuleBase
{
    public const string ConnectionStringName = "aiorchestratordb";

    public override string Name => "Runs";

    public override async Task Migrate(
        IServiceProvider services,
        CancellationToken cancellationToken
    )
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        await database.Database.MigrateAsync(cancellationToken);
    }

    public override void Add(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<RunsDbContext>(options =>
            options.UseSqlite(
                configuration.GetConnectionString(ConnectionStringName),
                // One file holds every module, so each keeps its own history table — the schema
                // name lives on in the suffix (ADR-0003). No EnableRetryOnFailure: a local file
                // has no transient failures to retry.
                sqlite => sqlite.MigrationsHistoryTable("__EFMigrationsHistory_" + RunsDbContext.Schema)
            )
        );

        services.AddHealthChecks().AddDbContextCheck<RunsDbContext>("runs-db");

        // #506. What the loop asks for through IRunRequests, and what a person opens
        // by hand on a PBI. Singletons, because a run outlives every request that reads it.
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<Features.PbiRuns.RunsOptions>().Bind(configuration.GetSection(Features.PbiRuns.RunsOptions.Section));
        services.AddSingleton<Features.PbiRuns.LiveRuns>();
        services.AddSingleton<Features.PbiRuns.StaleThreshold>();
        services.AddSingleton<Features.PbiRuns.PermissionModeSetting>();
        services.AddSingleton<Features.PbiRuns.StateEnvironments>();
        // #697. A run's recording copied to the repository's aio/evidence branch once it ended.
        services.AddSingleton<Features.PbiRuns.RecordingEvidence>();
        services.AddSingleton<Features.PbiRuns.RunExecution>();
        services.AddSingleton<Features.PbiRuns.PbiScripts>();
        services.AddSingleton<Features.PbiRuns.RunBook>();
        services.AddSingleton<BuildingBlocks.RunRequests.IRunRequests>(provider =>
            provider.GetRequiredService<Features.PbiRuns.RunBook>()
        );

        // #516. The sessions a person opens on a folder, which live only in memory.
        services.AddSingleton<Features.PbiRuns.FolderSessions>();
    }
}
