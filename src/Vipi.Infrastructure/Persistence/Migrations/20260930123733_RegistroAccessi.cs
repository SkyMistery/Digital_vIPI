using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RegistroAccessi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccessiAlSito",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Divisione = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    Acc = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    PrimoUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UltimoUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Giorni = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessiAlSito", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessiAlSito_UltimoUtc",
                table: "AccessiAlSito",
                column: "UltimoUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessiAlSito");
        }
    }
}
