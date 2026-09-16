using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <summary>
    /// Toglie <c>Airports.IsMilitaryOnly</c>, in pensione dall'11 settembre 2026: il suo posto l'ha preso
    /// <c>Category</c> (carta <c>docs/feature/2026-09-11-categorie-aeroporto.md</c>). Era rimasta come specchio
    /// perché la finestra cieca vietava di togliere colonne. Prima di toglierla si è misurato sulla copia di
    /// produzione del 16 settembre: nessuna riga da travasare, specchio sempre uguale alla categoria.
    /// <para>Il <c>Down</c> rimette lo specchio COI DATI, così una versione precedente ritrova i solo militari.</para>
    /// </summary>
    public partial class SpecchioSoloMilitareInPensione : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsMilitaryOnly",
                table: "Airports");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMilitaryOnly",
                table: "Airports",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE Airports SET IsMilitaryOnly = 1 WHERE Category = 'MilitaryOnly'");
        }
    }
}
