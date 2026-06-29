using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameRunwayRuleTimeToLocal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TimeToUtcMin",
                table: "AirportRunwayRules",
                newName: "TimeToLocalMin");

            migrationBuilder.RenameColumn(
                name: "TimeFromUtcMin",
                table: "AirportRunwayRules",
                newName: "TimeFromLocalMin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TimeToLocalMin",
                table: "AirportRunwayRules",
                newName: "TimeToUtcMin");

            migrationBuilder.RenameColumn(
                name: "TimeFromLocalMin",
                table: "AirportRunwayRules",
                newName: "TimeFromUtcMin");
        }
    }
}
