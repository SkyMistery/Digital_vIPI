using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountEventoInUso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountEventoInUso",
                columns: table => new
                {
                    VidPersonale = table.Column<int>(type: "INTEGER", nullable: false),
                    VidEvento = table.Column<int>(type: "INTEGER", nullable: false),
                    ScadeUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountEventoInUso", x => x.VidPersonale);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountEventoInUso");
        }
    }
}
