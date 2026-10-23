using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AiOrchestrator.Modules.Workspace.Persistence;

/// <summary>
/// Design-time only: lets `dotnet ef migrations` build the model without booting a host.
/// <para>
/// The connection string is a placeholder and is never connected to — EF needs a provider to know
/// which SQL to emit, not a database to emit it against.
/// </para>
/// </summary>
sealed class WorkspaceDbContextFactory : IDesignTimeDbContextFactory<WorkspaceDbContext>
{
    public WorkspaceDbContext CreateDbContext(string[] args) =>
        new(
            new DbContextOptionsBuilder<WorkspaceDbContext>()
                .UseSqlite(
                    "Data Source=design-placeholder.db",
                    sqlite => sqlite.MigrationsHistoryTable("__EFMigrationsHistory_" + WorkspaceDbContext.Schema)
                )
                .Options
        );
}
