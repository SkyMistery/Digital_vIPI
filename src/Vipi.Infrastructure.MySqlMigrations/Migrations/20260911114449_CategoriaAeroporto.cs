using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class CategoriaAeroporto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La categoria dello scalo (carta docs/feature/2026-09-11-categorie-aeroporto.md). SOLO additiva, per
            // la finestra cieca: niente SQL e niente DropColumn fino al 16 settembre 2026. Le righe nascono
            // «Civil»; il travaso lo fa la passata d'avvio (ReconcileAirportCategoriesAsync), e IsMilitaryOnly resta
            // come specchio finché una migrazione dopo la finestra non la toglie.
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Airports",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Civil",
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");
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
