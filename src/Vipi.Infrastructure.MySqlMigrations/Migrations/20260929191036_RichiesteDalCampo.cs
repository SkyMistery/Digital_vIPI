using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
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
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FieldRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ReporterUserId = table.Column<int>(type: "int", nullable: false),
                    ReporterName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DocumentId = table.Column<int>(type: "int", nullable: true),
                    SectionKey = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReleaseNumber = table.Column<int>(type: "int", nullable: true),
                    Kind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Body = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HandledByUserId = table.Column<int>(type: "int", nullable: false),
                    HandledByName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HandledUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Reply = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DuplicateOfId = table.Column<int>(type: "int", nullable: true)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
