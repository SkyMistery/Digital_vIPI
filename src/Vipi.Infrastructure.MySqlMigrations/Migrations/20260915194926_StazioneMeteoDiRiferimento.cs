using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class StazioneMeteoDiRiferimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MetarStationIcao",
                table: "Airports",
                type: "varchar(4)",
                maxLength: 4,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MetarStationIcao",
                table: "Airports");
        }
    }
}
