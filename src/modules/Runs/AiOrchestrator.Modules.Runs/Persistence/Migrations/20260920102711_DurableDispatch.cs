using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Runs.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DurableDispatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "run_dispatches",
                schema: "runs",
                columns: table => new
                {
                    EffectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FolderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FolderPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Host = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    Repository = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    TaskKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RequestJson = table.Column<string>(type: "TEXT", nullable: false),
                    RequestHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PlanJson = table.Column<string>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    FailureCode = table.Column<string>(type: "TEXT", nullable: true),
                    FailureReason = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_run_dispatches", x => x.EffectId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_run_dispatches_FolderId",
                schema: "runs",
                table: "run_dispatches",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_run_dispatches_RunId",
                schema: "runs",
                table: "run_dispatches",
                column: "RunId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "run_dispatches",
                schema: "runs");
        }
    }
}
