using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Runs.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "runs");

            migrationBuilder.CreateTable(
                name: "agents",
                schema: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CommandLine = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "runs",
                schema: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TaskTitle = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    WorkflowId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    WorkflowLabel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AgentId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AgentName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FolderPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    WorktreeName = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    ResumedFromRunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SessionId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SessionKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    FinishedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CostUsd = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: true),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PermissionMode = table.Column<string>(type: "TEXT", nullable: true),
                    RequestedModel = table.Column<string>(type: "TEXT", nullable: true),
                    InputTokens = table.Column<long>(type: "INTEGER", nullable: false),
                    OutputTokens = table.Column<long>(type: "INTEGER", nullable: false),
                    CacheReadTokens = table.Column<long>(type: "INTEGER", nullable: false),
                    CacheWriteTokens = table.Column<long>(type: "INTEGER", nullable: false),
                    Activity = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivitySummary = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "run_commands",
                schema: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    CommandKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CommitSha = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ExitCode = table.Column<int>(type: "INTEGER", nullable: true),
                    TimedOut = table.Column<bool>(type: "INTEGER", nullable: false),
                    Error = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    FinishedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Stdout = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Stderr = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    OutputTruncated = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_run_commands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_run_commands_runs_RunId",
                        column: x => x.RunId,
                        principalSchema: "runs",
                        principalTable: "runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "run_diff_files",
                schema: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Added = table.Column<int>(type: "INTEGER", nullable: false),
                    Removed = table.Column<int>(type: "INTEGER", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_run_diff_files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_run_diff_files_runs_RunId",
                        column: x => x.RunId,
                        principalSchema: "runs",
                        principalTable: "runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "run_log_lines",
                schema: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_run_log_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_run_log_lines_runs_RunId",
                        column: x => x.RunId,
                        principalSchema: "runs",
                        principalTable: "runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "run_steps",
                schema: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    FinishedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Label = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ActionKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Meta = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    Hold = table.Column<bool>(type: "INTEGER", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_run_steps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_run_steps_runs_RunId",
                        column: x => x.RunId,
                        principalSchema: "runs",
                        principalTable: "runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agents_Key",
                schema: "runs",
                table: "agents",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_run_commands_RunId_Ordinal",
                schema: "runs",
                table: "run_commands",
                columns: new[] { "RunId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_run_diff_files_RunId",
                schema: "runs",
                table: "run_diff_files",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_run_log_lines_RunId_Ordinal",
                schema: "runs",
                table: "run_log_lines",
                columns: new[] { "RunId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_run_steps_RunId_Ordinal",
                schema: "runs",
                table: "run_steps",
                columns: new[] { "RunId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_runs_FolderPath",
                schema: "runs",
                table: "runs",
                column: "FolderPath");

            migrationBuilder.CreateIndex(
                name: "IX_runs_ResumedFromRunId",
                schema: "runs",
                table: "runs",
                column: "ResumedFromRunId");

            migrationBuilder.CreateIndex(
                name: "IX_runs_StartedAt",
                schema: "runs",
                table: "runs",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_runs_TaskKey",
                schema: "runs",
                table: "runs",
                column: "TaskKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agents",
                schema: "runs");

            migrationBuilder.DropTable(
                name: "run_commands",
                schema: "runs");

            migrationBuilder.DropTable(
                name: "run_diff_files",
                schema: "runs");

            migrationBuilder.DropTable(
                name: "run_log_lines",
                schema: "runs");

            migrationBuilder.DropTable(
                name: "run_steps",
                schema: "runs");

            migrationBuilder.DropTable(
                name: "runs",
                schema: "runs");
        }
    }
}
