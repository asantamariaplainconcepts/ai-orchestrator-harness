using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Workspace.Persistence;

/// <summary>
/// Tables whose owner has no schema left to migrate them away, read by nothing: the Tasks mirror
/// (#410), <c>review_steps</c>, whose creating migration #300 removed from Workspace's history rather
/// than dropping it, the Workflows module's tables (#367) and the palette's (#123), and the
/// Coordination module's journal, which went with the first tree (#518).
/// <para>
/// Dropped from <see cref="WorkspaceModule.Migrate"/> because that is the one call every host that
/// migrates makes. It lived in the MigrationService until #548, so the installed app, which has no
/// MigrationService and migrates itself, kept them. Idempotent, so it can stay until no such
/// database is left.
/// </para>
/// </summary>
static class RetiredTables
{
    public static Task Drop(WorkspaceDbContext database, CancellationToken cancellationToken) =>
        database.Database.ExecuteSqlRawAsync(
            """
            DROP TABLE IF EXISTS "tasks";
            DROP TABLE IF EXISTS "connectors";
            DROP TABLE IF EXISTS "__EFMigrationsHistory_tasks";
            DROP TABLE IF EXISTS "review_steps";
            DROP TABLE IF EXISTS "workflow_steps";
            DROP TABLE IF EXISTS "workflows";
            DROP TABLE IF EXISTS "__EFMigrationsHistory_workflows";
            DROP TABLE IF EXISTS "palette_commands";
            DROP TABLE IF EXISTS "coordination_dispatch_intents";
            DROP TABLE IF EXISTS "__EFMigrationsHistory_coordination";
            """,
            cancellationToken
        );
}
