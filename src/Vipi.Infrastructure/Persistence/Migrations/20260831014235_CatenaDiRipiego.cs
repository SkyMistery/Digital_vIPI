using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CatenaDiRipiego : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SectorFallbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SectorCallsign = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetCallsign = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    BaseFeet = table.Column<int>(type: "INTEGER", nullable: true),
                    TopFeet = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectorFallbacks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SectorFallbacks_SectorCallsign_Order",
                table: "SectorFallbacks",
                columns: new[] { "SectorCallsign", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SectorFallbacks");
        }
    }
}
