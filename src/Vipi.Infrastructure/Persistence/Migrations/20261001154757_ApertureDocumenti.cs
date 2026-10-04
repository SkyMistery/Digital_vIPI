using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApertureDocumenti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AperturaDocumenti",
                columns: table => new
                {
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    Giorno = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Volte = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AperturaDocumenti", x => new { x.DocumentId, x.Giorno });
                    table.ForeignKey(
                        name: "FK_AperturaDocumenti_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AperturaDocumenti_Giorno",
                table: "AperturaDocumenti",
                column: "Giorno");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AperturaDocumenti");
        }
    }
}
