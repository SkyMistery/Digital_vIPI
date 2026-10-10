using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class CentroDelleSessioniAtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "AtcSessions",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "AtcSessions",
                type: "double",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "AtcSessions");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "AtcSessions");
        }
    }
}
