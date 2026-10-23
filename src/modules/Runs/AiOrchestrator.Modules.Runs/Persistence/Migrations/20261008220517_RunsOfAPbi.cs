using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Runs.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RunsOfAPbi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pbi_runs",
                schema: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Repository = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    PbiId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FolderPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Branch = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Command = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Manual = table.Column<bool>(type: "INTEGER", nullable: false),
                    OutcomesJson = table.Column<string>(type: "TEXT", nullable: false),
                    EndRecordPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    RecordingPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Environment = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    WorkingDirectory = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    EndedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Outcome = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "TEXT", nullable: true),
                    Failure = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ProblemsJson = table.Column<string>(type: "TEXT", nullable: false),
                    EndRecord = table.Column<string>(type: "TEXT", nullable: true),
                    InputTokens = table.Column<long>(type: "INTEGER", nullable: true),
                    OutputTokens = table.Column<long>(type: "INTEGER", nullable: true),
                    CostUsd = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pbi_runs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pbi_runs_EndedAt",
                schema: "runs",
                table: "pbi_runs",
                column: "EndedAt");

            migrationBuilder.CreateIndex(
                name: "IX_pbi_runs_Repository_PbiId",
                schema: "runs",
                table: "pbi_runs",
                columns: new[] { "Repository", "PbiId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pbi_runs",
                schema: "runs");
        }
    }
}
