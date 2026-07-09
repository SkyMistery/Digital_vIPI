using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocRelease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocReleases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TargetType = table.Column<string>(type: "TEXT", nullable: false),
                    TargetKey = table.Column<string>(type: "TEXT", nullable: false),
                    VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    ReleaseAiracCycle = table.Column<string>(type: "TEXT", nullable: false),
                    ReleaseEffectiveUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocReleases", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocReleases_TargetType_TargetKey_ReleaseEffectiveUtc",
                table: "DocReleases",
                columns: new[] { "TargetType", "TargetKey", "ReleaseEffectiveUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DocReleases_TargetType_TargetKey_VersionNumber",
                table: "DocReleases",
                columns: new[] { "TargetType", "TargetKey", "VersionNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocReleases");
        }
    }
}
