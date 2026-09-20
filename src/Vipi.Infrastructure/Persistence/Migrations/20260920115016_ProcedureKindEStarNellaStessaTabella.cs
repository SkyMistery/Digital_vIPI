using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// <c>AirportSids</c> diventa <c>AirportProcedures</c> e prende la colonna <c>Kind</c> (<c>Sid</c>/<c>Star</c>):
    /// una tabella sola per le partenze e per gli arrivi.
    ///
    /// <para>🔴 <b>Corpo scritto a mano.</b> Lo scaffolding aveva proposto <c>DropTable("AirportSids")</c> +
    /// <c>CreateTable("AirportProcedures")</c> — sul database vero sono le ~1470 righe SID con priorità,
    /// pubblicazioni forzate e correzioni del punto, <b>buttate via</b>. Qui si rinomina la tabella e si
    /// aggiunge una colonna: i dati restano dove sono.</para>
    ///
    /// <para>Le righe che c'erano prima della colonna sono tutte SID, quindi <c>defaultValue: "Sid"</c> (gli enum
    /// si salvano come stringa, SPEC §6).</para>
    /// </summary>
    public partial class ProcedureKindEStarNellaStessaTabella : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AirportSids_AirportId_Order",
                table: "AirportSids");

            migrationBuilder.RenameTable(
                name: "AirportSids",
                newName: "AirportProcedures");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "AirportProcedures",
                type: "TEXT",
                nullable: false,
                defaultValue: "Sid");

            migrationBuilder.CreateIndex(
                name: "IX_AirportProcedures_AirportId_Order",
                table: "AirportProcedures",
                columns: new[] { "AirportId", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠️ Tornando indietro le STAR non hanno più un posto dove stare: si cancellano PRIMA del rename,
            // o resterebbero nella tabella delle SID come righe senza verso.
            migrationBuilder.Sql("DELETE FROM \"AirportProcedures\" WHERE \"Kind\" = 'Star';");

            migrationBuilder.DropIndex(
                name: "IX_AirportProcedures_AirportId_Order",
                table: "AirportProcedures");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "AirportProcedures");

            migrationBuilder.RenameTable(
                name: "AirportProcedures",
                newName: "AirportSids");

            migrationBuilder.CreateIndex(
                name: "IX_AirportSids_AirportId_Order",
                table: "AirportSids",
                columns: new[] { "AirportId", "Order" });
        }
    }
}
