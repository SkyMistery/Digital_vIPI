using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RunwayRuleThresholds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Le regole pista passano da condizioni vento-arco/precip a soglie operative (coda/traverso + superficie):
            // la vecchia semantica non si traduce, si riparte pulito (si reinseriscono dall'editor aeroporto).
            migrationBuilder.Sql("DELETE FROM \"AirportRunwayRules\";");

            migrationBuilder.DropColumn(name: "WindDirFrom", table: "AirportRunwayRules");
            migrationBuilder.DropColumn(name: "WindDirTo", table: "AirportRunwayRules");
            migrationBuilder.DropColumn(name: "WindSpeedMin", table: "AirportRunwayRules");
            migrationBuilder.DropColumn(name: "WindSpeedMax", table: "AirportRunwayRules");
            migrationBuilder.DropColumn(name: "Rain", table: "AirportRunwayRules");
            migrationBuilder.DropColumn(name: "Snow", table: "AirportRunwayRules");

            migrationBuilder.AddColumn<string>(
                name: "Name", table: "AirportRunwayRules", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "MaxTailwindKt", table: "AirportRunwayRules", type: "INTEGER", nullable: false, defaultValue: 5);
            migrationBuilder.AddColumn<int>(
                name: "MaxCrosswindKt", table: "AirportRunwayRules", type: "INTEGER", nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "Surface", table: "AirportRunwayRules", type: "TEXT", nullable: false, defaultValue: "Any");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Name", table: "AirportRunwayRules");
            migrationBuilder.DropColumn(name: "MaxTailwindKt", table: "AirportRunwayRules");
            migrationBuilder.DropColumn(name: "MaxCrosswindKt", table: "AirportRunwayRules");
            migrationBuilder.DropColumn(name: "Surface", table: "AirportRunwayRules");

            migrationBuilder.AddColumn<int>(name: "WindDirFrom", table: "AirportRunwayRules", type: "INTEGER", nullable: true);
            migrationBuilder.AddColumn<int>(name: "WindDirTo", table: "AirportRunwayRules", type: "INTEGER", nullable: true);
            migrationBuilder.AddColumn<int>(name: "WindSpeedMin", table: "AirportRunwayRules", type: "INTEGER", nullable: true);
            migrationBuilder.AddColumn<int>(name: "WindSpeedMax", table: "AirportRunwayRules", type: "INTEGER", nullable: true);
            migrationBuilder.AddColumn<bool>(name: "Rain", table: "AirportRunwayRules", type: "INTEGER", nullable: true);
            migrationBuilder.AddColumn<bool>(name: "Snow", table: "AirportRunwayRules", type: "INTEGER", nullable: true);
        }
    }
}
