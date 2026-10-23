using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Runs.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ControlledExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ControlPlanHash",
                schema: "runs",
                table: "runs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ControlPlanJson",
                schema: "runs",
                table: "runs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "run_control_events",
                schema: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_run_control_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_run_control_events_runs_RunId",
                        column: x => x.RunId,
                        principalSchema: "runs",
                        principalTable: "runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_run_control_events_RunId_Sequence",
                schema: "runs",
                table: "run_control_events",
                columns: new[] { "RunId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "run_control_events",
                schema: "runs");

            migrationBuilder.DropColumn(
                name: "ControlPlanHash",
                schema: "runs",
                table: "runs");

            migrationBuilder.DropColumn(
                name: "ControlPlanJson",
                schema: "runs",
                table: "runs");
        }
    }
}
