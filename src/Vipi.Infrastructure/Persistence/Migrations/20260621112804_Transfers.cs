using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Transfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Transfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirId = table.Column<int>(type: "INTEGER", nullable: false),
                    RelationKey = table.Column<string>(type: "TEXT", nullable: false),
                    RelationLabel = table.Column<string>(type: "TEXT", nullable: false),
                    Phase = table.Column<string>(type: "TEXT", nullable: false),
                    AirportIcao = table.Column<string>(type: "TEXT", nullable: false),
                    Cop = table.Column<string>(type: "TEXT", nullable: false),
                    FlRule = table.Column<string>(type: "TEXT", nullable: false),
                    HandlerChainJson = table.Column<string>(type: "TEXT", nullable: false),
                    StandardFallback = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transfers_Firs_FirId",
                        column: x => x.FirId,
                        principalTable: "Firs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_FirId_RelationKey_Phase_Order",
                table: "Transfers",
                columns: new[] { "FirId", "RelationKey", "Phase", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Transfers");
        }
    }
}
