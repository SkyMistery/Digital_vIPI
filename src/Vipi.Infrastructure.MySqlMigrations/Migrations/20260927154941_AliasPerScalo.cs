using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AliasPerScalo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SidFixAliases_Prefix",
                table: "SidFixAliases");

            migrationBuilder.AddColumn<string>(
                name: "Icao",
                table: "SidFixAliases",
                type: "varchar(4)",
                maxLength: 4,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SidFixAliases_Icao_Prefix",
                table: "SidFixAliases",
                columns: new[] { "Icao", "Prefix" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SidFixAliases_Icao_Prefix",
                table: "SidFixAliases");

            migrationBuilder.DropColumn(
                name: "Icao",
                table: "SidFixAliases");

            migrationBuilder.CreateIndex(
                name: "IX_SidFixAliases_Prefix",
                table: "SidFixAliases",
                column: "Prefix",
                unique: true);
        }
    }
}
