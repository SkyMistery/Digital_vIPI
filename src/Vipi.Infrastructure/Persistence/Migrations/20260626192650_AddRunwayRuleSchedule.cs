using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRunwayRuleSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DateParity",
                table: "AirportRunwayRules",
                type: "TEXT",
                nullable: false,
                defaultValue: "Any");

            migrationBuilder.AddColumn<int>(
                name: "DaysOfWeekMask",
                table: "AirportRunwayRules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimeFromUtcMin",
                table: "AirportRunwayRules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimeToUtcMin",
                table: "AirportRunwayRules",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateParity",
                table: "AirportRunwayRules");

            migrationBuilder.DropColumn(
                name: "DaysOfWeekMask",
                table: "AirportRunwayRules");

            migrationBuilder.DropColumn(
                name: "TimeFromUtcMin",
                table: "AirportRunwayRules");

            migrationBuilder.DropColumn(
                name: "TimeToUtcMin",
                table: "AirportRunwayRules");
        }
    }
}
