using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Runs.Persistence.Migrations
{
    /// <summary>
    /// #365. The Workflows module is gone, and with it the module that owned its schema. The
    /// tables it leaves behind in every existing database are removed here, by the one module
    /// that survives and already owns migrations for this database — Runs, which reads a run's
    /// plan from the loop file now and never from these tables again. Sqlite's <c>DROP TABLE
    /// IF EXISTS</c> makes this idempotent: a fresh database holds none of them, and the drop of
    /// a schema nobody created is a no-op rather than a failure.
    /// </summary>
    public partial class DropTheWorkflowsTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sqlite carries EF's "schemas" as plain table-name prefixes: what the Workflows
            // module's schema "workflows" left in the file is these four names, verified against
            // a live database before this migration was written.
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS "workflow_steps";
                DROP TABLE IF EXISTS "palette_commands";
                DROP TABLE IF EXISTS "workflows";
                DROP TABLE IF EXISTS "__EFMigrationsHistory_workflows";
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The module is deleted; there is no schema to restore to, and inventing its tables
            // back would be a resurrection nobody asked for. The reverse of this migration is
            // nothing, on purpose.
        }
    }
}
