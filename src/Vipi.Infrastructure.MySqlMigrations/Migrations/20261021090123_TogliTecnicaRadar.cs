using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <summary>
    /// Via la colonna della tecnica operativa del radar di torre (S120, 21 ottobre 2026, committente): dal 21 ottobre
    /// la tecnica è fatta di blocchi del documento, e il trasloco della 1.69.0 ha portato i testi in un paragrafo e
    /// svuotato la colonna. ⚠️ Distruttiva: prima di lanciarla in produzione la colonna dev'essere vuota
    /// (<c>SELECT Icao FROM Airports WHERE RadarTowerTechnique IS NOT NULL</c> → nessuna riga). Una riga rimasta è uno
    /// scalo il cui testo non ha trovato una sezione dove andare: il testo va copiato a mano prima.
    /// </summary>
    public partial class TogliTecnicaRadar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RadarTowerTechnique",
                table: "Airports");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RadarTowerTechnique",
                table: "Airports",
                type: "longtext",
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
