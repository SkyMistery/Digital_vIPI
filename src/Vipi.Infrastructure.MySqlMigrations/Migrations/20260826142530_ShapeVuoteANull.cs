using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class ShapeVuoteANull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Le shape rimaste in colonna come contenitore VUOTO: sono il residuo degli import fatti da quando
            // IVAO ha smesso di mandare i poligoni e risponde `regionMapPolygon: []`. Non sono una forma, ma
            // occupano il posto di una: `HasPolygon` guarda `!= null` e a schermo dicevano «ha un poligono» per
            // 59 posizioni d'aeroporto e 148 settori ACC, e le letture dell'AoR se le portavano dietro.
            //
            // ⚠️ Si azzera SOLO il vuoto, mai una forma che non sappiamo leggere: giudicare se una shape si
            // disegna non è compito di questa migrazione (vedi PolygonGeometry.IsEmptyShape).
            foreach (var tabella in new[] { "AccSectors", "AirportSectors" })
                migrationBuilder.Sql(
                    $"UPDATE {tabella} SET RegionMapPolygon = NULL " +
                    "WHERE RegionMapPolygon IN ('[]', '{}', '', 'null');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Non si torna indietro: il valore di prima era un'assenza scritta in tre modi diversi, e
            // reinventarne uno sarebbe rimettere in circolo proprio l'ambiguità che questa migrazione toglie.
        }
    }
}
