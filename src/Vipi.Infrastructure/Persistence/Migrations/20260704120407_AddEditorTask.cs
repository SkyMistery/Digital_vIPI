using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEditorTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EditorTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    AssigneeUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssigneeName = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Priority = table.Column<string>(type: "TEXT", nullable: false),
                    DueAiracCycle = table.Column<string>(type: "TEXT", nullable: true),
                    TargetType = table.Column<string>(type: "TEXT", nullable: true),
                    TargetKey = table.Column<string>(type: "TEXT", nullable: true),
                    TargetLabel = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditorTasks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EditorTasks_AssigneeUserId",
                table: "EditorTasks",
                column: "AssigneeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EditorTasks_Status",
                table: "EditorTasks",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EditorTasks");
        }
    }
}
