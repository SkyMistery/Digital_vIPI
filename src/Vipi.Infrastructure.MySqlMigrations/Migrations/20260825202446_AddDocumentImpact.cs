using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentImpact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NeedsReviewUtc",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ReviewReason",
                table: "Documents");

            migrationBuilder.CreateTable(
                name: "DocumentImpacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceKey = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReasonKey = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReasonArgsJson = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsPublicNow = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RaisedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ClearedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ClearedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentImpacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentImpacts_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentImpacts_ClearedUtc_RaisedUtc",
                table: "DocumentImpacts",
                columns: new[] { "ClearedUtc", "RaisedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentImpacts_DocumentId_Kind_SourceKey_ClearedUtc",
                table: "DocumentImpacts",
                columns: new[] { "DocumentId", "Kind", "SourceKey", "ClearedUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentImpacts");

            migrationBuilder.AddColumn<DateTime>(
                name: "NeedsReviewUtc",
                table: "Documents",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewReason",
                table: "Documents",
                type: "longtext",
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
