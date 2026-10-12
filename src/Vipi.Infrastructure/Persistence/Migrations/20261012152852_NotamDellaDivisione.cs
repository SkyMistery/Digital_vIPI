using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotamDellaDivisione : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IvaoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    StartUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Military = table.Column<bool>(type: "INTEGER", nullable: false),
                    Airports = table.Column<string>(type: "TEXT", nullable: false),
                    Centers = table.Column<string>(type: "TEXT", nullable: false),
                    SourceUpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notams", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notams_IvaoId",
                table: "Notams",
                column: "IvaoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notams");
        }
    }
}
