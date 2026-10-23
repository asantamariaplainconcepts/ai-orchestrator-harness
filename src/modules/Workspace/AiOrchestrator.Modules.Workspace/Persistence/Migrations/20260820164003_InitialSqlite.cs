using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Workspace.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "workspace");

            migrationBuilder.CreateTable(
                name: "check_results",
                schema: "workspace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FolderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkingPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CommandKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Commit = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ExitCode = table.Column<int>(type: "INTEGER", nullable: true),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    FinishedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_check_results", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "folders",
                schema: "workspace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    DefaultAgentId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsRepository = table.Column<bool>(type: "INTEGER", nullable: false),
                    Branch = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    BaseRef = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    UncommittedFiles = table.Column<int>(type: "INTEGER", nullable: false),
                    RemoteUrl = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ProbedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_folders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "preferences",
                schema: "workspace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_preferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "folder_commands",
                schema: "workspace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    CommandLine = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    WorkingDirectory = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenAsTab = table.Column<bool>(type: "INTEGER", nullable: false),
                    Gate = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    Stop = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    TimeoutSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    FolderId = table.Column<Guid>(type: "TEXT", nullable: false)
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
                name: "worktrees",
                schema: "workspace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Branch = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    UncommittedFiles = table.Column<int>(type: "INTEGER", nullable: false),
                    FolderId = table.Column<Guid>(type: "TEXT", nullable: false)
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
                name: "IX_folders_Path",
                schema: "workspace",
                table: "folders",
                column: "Path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_preferences_Name",
                schema: "workspace",
                table: "preferences",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_worktrees_FolderId_Path",
                schema: "workspace",
                table: "worktrees",
                columns: new[] { "FolderId", "Path" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "check_results",
                schema: "workspace");

            migrationBuilder.DropTable(
                name: "folder_commands",
                schema: "workspace");

            migrationBuilder.DropTable(
                name: "preferences",
                schema: "workspace");

            migrationBuilder.DropTable(
                name: "worktrees",
                schema: "workspace");

            migrationBuilder.DropTable(
                name: "folders",
                schema: "workspace");
        }
    }
}
