using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CategoriaAeroporto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Gemella della migrazione MySQL omonima: vedi il commento lì. Le righe nascono «Civil», il travaso
            // lo fa la passata d'avvio (ReconcileAirportCategoriesAsync).
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Airports",
                type: "TEXT",
                nullable: false,
                defaultValue: "Civil");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "Airports");
        }
    }
}
