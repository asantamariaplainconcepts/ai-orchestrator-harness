using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Runs.Persistence.Migrations
{
    /// <summary>
    /// Widens <c>runs_settings.Name</c> to 320 for the environment of each state (#612). SQLite keeps
    /// every string as TEXT whatever its length, so there is nothing to alter: the migration carries
    /// the model forward and the snapshot with it.
    /// </summary>
    public partial class EnvironmentPerState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
