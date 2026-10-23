using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Runs.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropTheOldTree : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agents",
                schema: "runs");

            migrationBuilder.DropTable(
                name: "run_commands",
                schema: "runs");

            migrationBuilder.DropTable(
                name: "run_control_events",
                schema: "runs");

            migrationBuilder.DropTable(
                name: "run_diff_files",
                schema: "runs");

            migrationBuilder.DropTable(
                name: "run_dispatches",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agents",
                schema: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CommandLine = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "run_dispatches",
                schema: "runs",
                columns: table => new
                {
                    EffectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    DecisionSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    FailureCode = table.Column<string>(type: "TEXT", nullable: true),
                    FailureReason = table.Column<string>(type: "TEXT", nullable: true),
                    FolderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FolderPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Host = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    TaskKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PlanJson = table.Column<string>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Repository = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    RequestHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RequestJson = table.Column<string>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_run_dispatches", x => x.EffectId);
                });

            migrationBuilder.CreateTable(
                name: "runs",
                schema: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Activity = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivitySummary = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    AgentId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AgentName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AgentProcessId = table.Column<int>(type: "INTEGER", nullable: true),
                    CacheReadTokens = table.Column<long>(type: "INTEGER", nullable: false),
                    CacheWriteTokens = table.Column<long>(type: "INTEGER", nullable: false),
                    ControlPlanHash = table.Column<string>(type: "TEXT", nullable: true),
                    ControlPlanJson = table.Column<string>(type: "TEXT", nullable: true),
                    CostUsd = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: true),
                    FinishedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    FolderPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    InputTokens = table.Column<long>(type: "INTEGER", nullable: false),
                    IsTransition = table.Column<bool>(type: "INTEGER", nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    OutputTokens = table.Column<long>(type: "INTEGER", nullable: false),
                    TaskKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PermissionMode = table.Column<string>(type: "TEXT", nullable: true),
                    RemoteEnvironment = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    RequestedModel = table.Column<string>(type: "TEXT", nullable: true),
                    ResumedFromRunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SessionId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SessionKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    TaskTitle = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    WorkflowId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    WorkflowLabel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    WorktreeName = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true)
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
                    CommandKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CommitSha = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Error = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ExitCode = table.Column<int>(type: "INTEGER", nullable: true),
                    FinishedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    OutputTruncated = table.Column<bool>(type: "INTEGER", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Stderr = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Stdout = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    TimedOut = table.Column<bool>(type: "INTEGER", nullable: false)
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
                name: "run_control_events",
                schema: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "run_diff_files",
                schema: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Added = table.Column<int>(type: "INTEGER", nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
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
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_run_log_lines", x => new { x.RunId, x.Ordinal });
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
                    ActionKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FinishedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Hold = table.Column<bool>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Meta = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    State = table.Column<int>(type: "INTEGER", nullable: false)
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
                name: "IX_run_control_events_RunId_Sequence",
                schema: "runs",
                table: "run_control_events",
                columns: new[] { "RunId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_run_diff_files_RunId",
                schema: "runs",
                table: "run_diff_files",
                column: "RunId");

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
    }
}
