using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AiOrchestrator.Modules.Loop.Persistence;

/// <summary>
/// Design-time only: lets <c>dotnet ef migrations</c> build the model without booting a host. The
/// connection string is never connected to.
/// </summary>
sealed class LoopDbContextFactory : IDesignTimeDbContextFactory<LoopDbContext>
{
    public LoopDbContext CreateDbContext(string[] args) =>
        new(
            new DbContextOptionsBuilder<LoopDbContext>()
                .UseSqlite(
                    "Data Source=design-placeholder.db",
                    sqlite => sqlite.MigrationsHistoryTable("__EFMigrationsHistory_" + LoopDbContext.Schema)
                )
                .Options
        );
}
