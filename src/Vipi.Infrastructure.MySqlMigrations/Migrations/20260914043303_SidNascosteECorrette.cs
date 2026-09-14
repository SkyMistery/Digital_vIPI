using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class SidNascosteECorrette : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FixOverride",
                table: "AirportSids",
                type: "longtext",
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "AirportSids",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TransitionOverride",
                table: "AirportSids",
                type: "longtext",
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FixOverride",
                table: "AirportSids");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "AirportSids");

            migrationBuilder.DropColumn(
                name: "TransitionOverride",
                table: "AirportSids");
        }
    }
}
