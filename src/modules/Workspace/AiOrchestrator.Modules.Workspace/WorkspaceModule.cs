using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Workspace.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.Modules.Workspace;

/// <summary>
/// The local checkouts this product may act on, and what Settings and the Files tab need of them:
/// each folder's defaults, the files of a PBI's branch, the machine's tools and the editor.
/// </summary>
public sealed class WorkspaceModule : ModuleBase
{
    public const string ConnectionStringName = "aiorchestratordb";

    public override string Name => "Workspace";

    public override async Task Migrate(
        IServiceProvider services,
        CancellationToken cancellationToken
    )
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<WorkspaceDbContext>();

        // A database with no migration applied is a first start. Asked before migrating, because
        // afterwards every database has them.
        var first = !(await database.Database.GetAppliedMigrationsAsync(cancellationToken)).Any();

        await database.Database.MigrateAsync(cancellationToken);

        // Tables nothing migrates any more, gone from every database this process migrates: the
        // MigrationService's and the installed app's alike (#548).
        await RetiredTables.Drop(database, cancellationToken);

        // Seeding rides with the migration rather than with the Server's startup, and only with
        // the first one: a folder a person removed stays removed across restarts.
        if (!first)
        {
            return;
        }

        await WorkspaceSeed.Apply(
            database,
            scope.ServiceProvider.GetService<BuildingBlocks.Files.ICheckoutProbe>(),
            cancellationToken
        );
    }

    public override void Add(IServiceCollection services, IConfiguration configuration)
    {
        // Reading a folder and running its own commands are what anybody working here does.
        // Creating and removing worktrees changes the repository, so it stays with Admin.
        services.AddPermissionGrants(
            ProjectRole.Member,
            WorkspacePermissions.Read,
            WorkspacePermissions.OpenOnDesktop,
            WorkspacePermissions.Configure
        );

        services.AddDbContext<WorkspaceDbContext>(options =>
            options.UseSqlite(
                configuration.GetConnectionString(ConnectionStringName),
                // One file holds every module, so each keeps its own history table — the schema
                // name lives on in the suffix (ADR-0003). No EnableRetryOnFailure: a local file
                // has no transient failures to retry.
                sqlite => sqlite.MigrationsHistoryTable("__EFMigrationsHistory_" + WorkspaceDbContext.Schema)
            )
        );

        // #505. The folders the loop engine walks, answered from these rows for a module that may
        // not read them.
        services.AddScoped<BuildingBlocks.Folders.IFolderDirectory, Features.Folders.FolderDirectory>();

        services.AddHealthChecks().AddDbContextCheck<WorkspaceDbContext>("workspace-db");
    }
}
