using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MinimiLvp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AirportLvpMinima",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AirportId = table.Column<int>(type: "INTEGER", nullable: false),
                    Declared = table.Column<bool>(type: "INTEGER", nullable: false),
                    PrepRvrM = table.Column<int>(type: "INTEGER", nullable: true),
                    PrepCeilingFt = table.Column<int>(type: "INTEGER", nullable: true),
                    LvpRvrM = table.Column<int>(type: "INTEGER", nullable: true),
                    LvpCeilingFt = table.Column<int>(type: "INTEGER", nullable: true),
                    CancelRvrM = table.Column<int>(type: "INTEGER", nullable: true),
                    CancelCeilingFt = table.Column<int>(type: "INTEGER", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AirportLvpMinima", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AirportLvpMinima_Airports_AirportId",
                        column: x => x.AirportId,
                        principalTable: "Airports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AirportLvpMinima_AirportId",
                table: "AirportLvpMinima",
                column: "AirportId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AirportLvpMinima");
        }
    }
}
