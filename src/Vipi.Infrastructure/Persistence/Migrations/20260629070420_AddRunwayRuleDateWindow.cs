using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRunwayRuleDateWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DateFromMonthDay",
                table: "AirportRunwayRules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DateToMonthDay",
                table: "AirportRunwayRules",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateFromMonthDay",
                table: "AirportRunwayRules");

            migrationBuilder.DropColumn(
                name: "DateToMonthDay",
                table: "AirportRunwayRules");
        }
    }
}
