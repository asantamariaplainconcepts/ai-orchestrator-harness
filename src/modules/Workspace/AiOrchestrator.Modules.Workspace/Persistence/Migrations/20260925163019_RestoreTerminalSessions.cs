using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Workspace.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RestoreTerminalSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "terminal_sessions",
                schema: "workspace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FolderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkingDirectory = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AgentId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    VendorSession = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Label = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_terminal_sessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_terminal_sessions_Id",
                schema: "workspace",
                table: "terminal_sessions",
                column: "Id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "terminal_sessions",
                schema: "workspace");
        }
    }
}
