using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddForeignSubcentersAndAdjacency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdjacentForeignCallsigns",
                table: "NeighbourCandidates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdjacentHomeCallsigns",
                table: "NeighbourCandidates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsForeign",
                table: "Accs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdjacentForeignCallsigns",
                table: "NeighbourCandidates");

            migrationBuilder.DropColumn(
                name: "AdjacentHomeCallsigns",
                table: "NeighbourCandidates");

            migrationBuilder.DropColumn(
                name: "IsForeign",
                table: "Accs");
        }
    }
}
