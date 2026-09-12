using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AirportId = table.Column<int>(type: "int", nullable: false),
                    Declared = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PrepRvrM = table.Column<int>(type: "int", nullable: true),
                    PrepCeilingFt = table.Column<int>(type: "int", nullable: true),
                    LvpRvrM = table.Column<int>(type: "int", nullable: true),
                    LvpCeilingFt = table.Column<int>(type: "int", nullable: true),
                    CancelRvrM = table.Column<int>(type: "int", nullable: true),
                    CancelCeilingFt = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4")
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
