using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MemoriaDiTraduzione : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TranslationUnits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceLang = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    TargetLang = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    SourceHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SourceText = table.Column<string>(type: "TEXT", nullable: false),
                    TargetText = table.Column<string>(type: "TEXT", nullable: false),
                    Origin = table.Column<string>(type: "TEXT", nullable: false),
                    Engine = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReviewedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranslationUnits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TranslationUnits_SourceLang_TargetLang_SourceHash",
                table: "TranslationUnits",
                columns: new[] { "SourceLang", "TargetLang", "SourceHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TranslationUnits_TargetLang_ReviewedUtc",
                table: "TranslationUnits",
                columns: new[] { "TargetLang", "ReviewedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TranslationUnits");
        }
    }
}
