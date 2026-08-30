using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RegistroSpesaTraduzione : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TranslationSpends",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Engine = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SourceLang = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    TargetLang = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    Characters = table.Column<long>(type: "INTEGER", nullable: false),
                    Segments = table.Column<int>(type: "INTEGER", nullable: false),
                    Discarded = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscardedCharacters = table.Column<long>(type: "INTEGER", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranslationSpends", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TranslationSpends_Engine",
                table: "TranslationSpends",
                column: "Engine");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TranslationSpends");
        }
    }
}
