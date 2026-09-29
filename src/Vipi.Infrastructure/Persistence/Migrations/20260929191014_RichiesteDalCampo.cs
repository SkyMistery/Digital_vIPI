using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RichiesteDalCampo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FromRequestId",
                table: "EditorTasks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FieldRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReporterUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReporterName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: true),
                    SectionKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ReleaseNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Body = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    HandledByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    HandledByName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    HandledUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Reply = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    DuplicateOfId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FieldRequests_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FieldRequests_DocumentId",
                table: "FieldRequests",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldRequests_ReporterUserId",
                table: "FieldRequests",
                column: "ReporterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldRequests_Status_CreatedUtc",
                table: "FieldRequests",
                columns: new[] { "Status", "CreatedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FieldRequests");

            migrationBuilder.DropColumn(
                name: "FromRequestId",
                table: "EditorTasks");
        }
    }
}
