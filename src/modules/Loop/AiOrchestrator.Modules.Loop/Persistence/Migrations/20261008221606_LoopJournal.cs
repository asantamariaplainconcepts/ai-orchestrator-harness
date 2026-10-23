using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Loop.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LoopJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "loop");

            migrationBuilder.CreateTable(
                name: "loop_asked_runs",
                schema: "loop",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Repository = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Pbi = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AskedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Consumed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loop_asked_runs", x => x.RunId);
                });

            migrationBuilder.CreateTable(
                name: "loop_journal",
                schema: "loop",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    At = table.Column<long>(type: "INTEGER", nullable: false),
                    Repository = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Pbi = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    From = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    To = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Why = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loop_journal", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_loop_asked_runs_Repository_Pbi_Consumed",
                schema: "loop",
                table: "loop_asked_runs",
                columns: new[] { "Repository", "Pbi", "Consumed" });

            migrationBuilder.CreateIndex(
                name: "IX_loop_journal_Repository_Pbi",
                schema: "loop",
                table: "loop_journal",
                columns: new[] { "Repository", "Pbi" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "loop_asked_runs",
                schema: "loop");

            migrationBuilder.DropTable(
                name: "loop_journal",
                schema: "loop");
        }
    }
}
