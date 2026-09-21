using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FrenoTraduzioneQuarantena : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TranslationQuarantines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceLang = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    TargetLang = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    SourceHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SourceText = table.Column<string>(type: "TEXT", nullable: false),
                    Strikes = table.Column<int>(type: "INTEGER", nullable: false),
                    CharactersWasted = table.Column<long>(type: "INTEGER", nullable: false),
                    Engine = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    FirstUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranslationQuarantines", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TranslationQuarantines_SourceLang_TargetLang_SourceHash",
                table: "TranslationQuarantines",
                columns: new[] { "SourceLang", "TargetLang", "SourceHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TranslationQuarantines");
        }
    }
}
