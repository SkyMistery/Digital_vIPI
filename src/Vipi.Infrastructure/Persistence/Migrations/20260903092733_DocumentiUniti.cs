using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DocumentiUniti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentUnions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentUnions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentUnionMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UnionId = table.Column<int>(type: "INTEGER", nullable: false),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentUnionMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentUnionMembers_DocumentUnions_UnionId",
                        column: x => x.UnionId,
                        principalTable: "DocumentUnions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentUnionMembers_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentUnionMembers_DocumentId",
                table: "DocumentUnionMembers",
                column: "DocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentUnionMembers_UnionId_Order",
                table: "DocumentUnionMembers",
                columns: new[] { "UnionId", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentUnionMembers");

            migrationBuilder.DropTable(
                name: "DocumentUnions");
        }
    }
}
