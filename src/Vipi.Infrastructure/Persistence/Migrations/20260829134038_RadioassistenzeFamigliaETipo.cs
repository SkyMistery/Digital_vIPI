using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RadioassistenzeFamigliaETipo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Navaids_Code_Kind",
                table: "Navaids");

            // ⚠️ SI SVUOTA, e non è pigrizia: le righe di prima nascono da una classificazione SBAGLIATA —
            // la «natura» era il nome del file da cui arrivavano, e `itvor.vor` contiene VOR, TACAN e VORTAC
            // insieme. Convertirle vorrebbe dire portarsi dietro l'errore, per giunta con l'identità nuova da
            // ricostruire a indovinare. Il giro d'import le rifà tutte in un minuto, e questa volta giuste.
            // ⚠️ È anche il solo modo di dare un valore alla colonna `NaturalKey`, che è unica e che per le
            // righe esistenti nascerebbe vuota su tutte.
            // (Decisione del committente, 30 agosto 2026: «butta tutto e rifacciamo daccapo».)
            migrationBuilder.Sql("DELETE FROM Navaids;");

            // ⚠️ E si azzera anche lo STATO dell'import, o la tabella resta vuota per ventiquattro ore: il
            // giro è «gestito» (GatedImportLoop) e riparte solo quando è passato il periodo dall'ultimo
            // successo. Svuotare senza questo vorrebbe dire un'anagrafica vuota per un giorno intero, con le
            // tabelle dei SOP che non trovano più le righe che citano. Lo svuotamento e il riempimento sono
            // lo stesso atto.
            migrationBuilder.Sql("DELETE FROM ImportStates WHERE Category = 'Navaid';");

            migrationBuilder.RenameColumn(
                name: "DisplayType",
                table: "Navaids",
                newName: "Type");

            migrationBuilder.AddColumn<string>(
                name: "NaturalKey",
                table: "Navaids",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Navaids_NaturalKey",
                table: "Navaids",
                column: "NaturalKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Navaids_NaturalKey",
                table: "Navaids");

            migrationBuilder.DropColumn(
                name: "NaturalKey",
                table: "Navaids");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "Navaids",
                newName: "DisplayType");

            migrationBuilder.CreateIndex(
                name: "IX_Navaids_Code_Kind",
                table: "Navaids",
                columns: new[] { "Code", "Kind" },
                unique: true);
        }
    }
}
