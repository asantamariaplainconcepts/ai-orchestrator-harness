using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Workspace.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropTheOldTree : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "check_results",
                schema: "workspace");

            migrationBuilder.DropTable(
                name: "folder_commands",
                schema: "workspace");

            migrationBuilder.DropTable(
                name: "terminal_sessions",
                schema: "workspace");

            migrationBuilder.DropTable(
                name: "worktrees",
                schema: "workspace");

            migrationBuilder.DropColumn(
                name: "BaseRef",
                schema: "workspace",
                table: "folders");

            migrationBuilder.DropColumn(
                name: "ProbedAt",
                schema: "workspace",
                table: "folders");

            migrationBuilder.DropColumn(
                name: "UncommittedFiles",
                schema: "workspace",
                table: "folders");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BaseRef",
                schema: "workspace",
                table: "folders",
                type: "TEXT",
                maxLength: 400,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "ProbedAt",
                schema: "workspace",
                table: "folders",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UncommittedFiles",
                schema: "workspace",
                table: "folders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "check_results",
                schema: "workspace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CommandKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Commit = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ExitCode = table.Column<int>(type: "INTEGER", nullable: true),
                    FinishedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    FolderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    WorkingPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_check_results", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "folder_commands",
                schema: "workspace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CommandLine = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    FolderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Gate = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    OpenAsTab = table.Column<bool>(type: "INTEGER", nullable: false),
                    Stop = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    TimeoutSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    WorkingDirectory = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_folder_commands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_folder_commands_folders_FolderId",
                        column: x => x.FolderId,
                        principalSchema: "workspace",
                        principalTable: "folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "terminal_sessions",
                schema: "workspace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AgentId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    FolderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    VendorSession = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    WorkingDirectory = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_terminal_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "worktrees",
                schema: "workspace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Branch = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    FolderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    UncommittedFiles = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_worktrees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_worktrees_folders_FolderId",
                        column: x => x.FolderId,
                        principalSchema: "workspace",
                        principalTable: "folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_check_results_FolderId_WorkingPath_CommandKey",
                schema: "workspace",
                table: "check_results",
                columns: new[] { "FolderId", "WorkingPath", "CommandKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_folder_commands_FolderId_Key",
                schema: "workspace",
                table: "folder_commands",
                columns: new[] { "FolderId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_terminal_sessions_Id",
                schema: "workspace",
                table: "terminal_sessions",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_worktrees_FolderId_Path",
                schema: "workspace",
                table: "worktrees",
                columns: new[] { "FolderId", "Path" },
                unique: true);
        }
    }
}
