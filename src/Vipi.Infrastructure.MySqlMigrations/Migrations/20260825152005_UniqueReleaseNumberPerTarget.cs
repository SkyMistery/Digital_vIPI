using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class UniqueReleaseNumberPerTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocReleases_TargetType_TargetKey_VersionNumber",
                table: "DocReleases");

            migrationBuilder.CreateIndex(
                name: "IX_DocReleases_TargetType_TargetKey_VersionNumber",
                table: "DocReleases",
                columns: new[] { "TargetType", "TargetKey", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocReleases_TargetType_TargetKey_VersionNumber",
                table: "DocReleases");

            migrationBuilder.CreateIndex(
                name: "IX_DocReleases_TargetType_TargetKey_VersionNumber",
                table: "DocReleases",
                columns: new[] { "TargetType", "TargetKey", "VersionNumber" });
        }
    }
}
