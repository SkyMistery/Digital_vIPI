using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAirport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AirportId",
                table: "Sectors",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Airports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Icao = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    FirId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Airports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Airports_Firs_FirId",
                        column: x => x.FirId,
                        principalTable: "Firs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sectors_AirportId",
                table: "Sectors",
                column: "AirportId");

            migrationBuilder.CreateIndex(
                name: "IX_Airports_FirId",
                table: "Airports",
                column: "FirId");

            migrationBuilder.CreateIndex(
                name: "IX_Airports_Icao",
                table: "Airports",
                column: "Icao",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Sectors_Airports_AirportId",
                table: "Sectors",
                column: "AirportId",
                principalTable: "Airports",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sectors_Airports_AirportId",
                table: "Sectors");

            migrationBuilder.DropTable(
                name: "Airports");

            migrationBuilder.DropIndex(
                name: "IX_Sectors_AirportId",
                table: "Sectors");

            migrationBuilder.DropColumn(
                name: "AirportId",
                table: "Sectors");
        }
    }
}
