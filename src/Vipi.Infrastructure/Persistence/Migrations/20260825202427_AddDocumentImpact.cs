using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    SourceKey = table.Column<string>(type: "TEXT", nullable: false),
                    ReasonKey = table.Column<string>(type: "TEXT", nullable: false),
                    ReasonArgsJson = table.Column<string>(type: "TEXT", nullable: true),
                    IsPublicNow = table.Column<bool>(type: "INTEGER", nullable: false),
                    RaisedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClearedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClearedByUserId = table.Column<int>(type: "INTEGER", nullable: false)
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
                });

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
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewReason",
                table: "Documents",
                type: "TEXT",
                nullable: true);
        }
    }
}
